using System;
using System.Runtime.InteropServices;
using Eto.Drawing;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Eto
{
    /// <summary>
    /// Eto.Forms 位图桥。把内核产出的裸像素直接贴进 <see cref="Bitmap"/>，
    /// 应用侧不需要再引 SkiaSharp.Views 之类的平台视图控件。
    /// </summary>
    public static class EtoChartExtensions
    {
        /// <summary>
        /// 像素帧转 Eto 位图。<see cref="Raster"/> 是 BGRA8888 预乘、行无填充，
        /// 与 WinForms/WPF/GTK 后端上 <see cref="PixelFormat.Format32bppRgba"/> 的内存布局一致，
        /// 走 <see cref="Bitmap.Lock()"/> 拿原生缓冲逐行 <c>Marshal.Copy</c> 即可。
        /// <para>
        /// 设计文档 §8.2 提出的“PNG 字节 → MemoryStream → System.Drawing.Image”路线只在
        /// GDI+ 后端成立且多一次编解码；这里改用直拷像素，跨后端通用。
        /// Eto 位图不携带 DPI 元数据，<paramref name="dpi"/> 仅作语义标记；
        /// 需要按 DPI 出图请用 <see cref="ToEtoBitmap(Chart,double)"/> 重载。
        /// 位图依赖已初始化的 Eto 平台（<c>Platform.Initialize</c> 或 <c>Application</c> 启动后），
        /// 创建与 <c>Lock</c> 均应在 UI 线程进行。
        /// </para>
        /// </summary>
        public static Bitmap ToEtoBitmap(this Raster raster, double dpi = 96d)
        {
            if (raster.Pixels == null)
            {
                throw new ArgumentException("像素帧缺少像素数据。", nameof(raster));
            }

            Bitmap bitmap = new Bitmap(raster.Width, raster.Height, PixelFormat.Format32bppRgba);
            using (BitmapData data = bitmap.Lock())
            {
                if (data.BitsPerPixel != 32)
                {
                    // Format32bppRgba 在受支持后端都是 32bpp；出现偏差说明宿主平台不支持该桥，
                    // 宁可快速失败，也不要把像素错位贴成花屏。
                    bitmap.Dispose();
                    throw new PlatformNotSupportedException(
                        "当前 Eto 平台的 Format32bppRgba 不是 32bpp（实际 " + data.BitsPerPixel +
                        "bpp），像素桥不适用，请改走 Chart.Encode PNG 字节自行解码。");
                }

                // Eto 的 ScanWidth 可能带行对齐填充，Flipped 后端行序自下而上；
                // 两种偏差都按行折算目标偏移，源侧（Raster.Stride）恒无填充。
                int stride = data.ScanWidth;
                bool bottomUp = stride < 0 || data.Flipped;
                if (stride < 0)
                {
                    stride = -stride;
                }

                // Marshal.Copy(byte[],…) 自行处理源数组钉固；目标侧只需按行折算偏移。
                if (!bottomUp && stride == raster.Stride)
                {
                    Marshal.Copy(raster.Pixels, 0, data.Data, raster.Pixels.Length);
                }
                else
                {
                    for (int y = 0; y < raster.Height; y++)
                    {
                        int row = bottomUp ? raster.Height - 1 - y : y;
                        Marshal.Copy(raster.Pixels, y * raster.Stride, IntPtr.Add(data.Data, row * stride), raster.Stride);
                    }
                }
            }
            return bitmap;
        }

        /// <summary>
        /// 表单直接转 Eto 位图。渲染按 <paramref name="dpi"/> 出像素
        /// （96dpi 之外想要打印级清晰度就传高 DPI），Eto 侧只见像素不见 DPI。
        /// </summary>
        public static Bitmap ToEtoBitmap(this Chart chart, double dpi = 96d)
        {
            return ChartRaster.Render(chart, (float)dpi).ToEtoBitmap(dpi);
        }

        /// <summary>任意矢量产物转 Eto 位图（零适配路径的位图版）。</summary>
        public static Bitmap ToEtoBitmap(this SKPicture picture, SKSizeI size, double dpi = 96d, SKColor? background = null)
        {
            return ChartRaster.RenderPicture(
                picture,
                new SKSize(size.Width, size.Height),
                (float)dpi,
                background).ToEtoBitmap(dpi);
        }
    }
}
