using System;
using System.Runtime.InteropServices;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Rendering
{
    /// <summary>宿主控件里表单的适配方式。</summary>
    public enum ChartFit
    {
        /// <summary>按宿主控件的缩放系数原尺寸绘制，超出控件范围就裁切。</summary>
        Actual = 0,

        /// <summary>等比缩放到控件客户区，居中留白。</summary>
        Fit = 1,
    }

    /// <summary>
    /// 一帧裸像素，格式固定为 BGRA8888 预乘。
    /// WinForms / WPF / Eto 这类只能吃位图的宿主把它贴进自己的像素缓冲区即可，
    /// 不需要在应用侧再引一层 SkiaSharp 视图控件。
    /// </summary>
    public readonly struct Raster
    {
        internal Raster(byte[] pixels, int width, int height)
        {
            Pixels = pixels;
            Width = width;
            Height = height;
        }

        /// <summary>连续行存储的像素，长度等于 <see cref="Stride"/> × <see cref="Height"/>。</summary>
        public byte[] Pixels { get; }

        public int Width { get; }
        public int Height { get; }

        /// <summary>行字节跨度；BGRA8888 下恒为 Width × 4，无对齐填充。</summary>
        public int Stride => Width * 4;
    }

    /// <summary>把表单光栅化成裸像素的公共入口，供各平台适配器复用。</summary>
    public static class ChartRaster
    {
        /// <summary>
        /// 渲染成一帧像素。DPI 用 <c>canvas.Scale</c> 实现，几何仍按 96dpi 求解。
        /// </summary>
        public static Raster Render(Chart chart, float dpi = 96f)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            TextRenderGate.EnterRender();
            try
            {
                SKSize size = chart.MeasureSize();
                using (SKPicture picture = chart.Record(dpi / 96f))
                {
                    return RenderPicture(picture, size, dpi, chart.Style.Colors.Background);
                }
            }
            finally
            {
                TextRenderGate.ExitRender();
            }
        }

        /// <summary>
        /// 把任意矢量产物光栅化成裸像素（零适配路径的位图版）。
        /// </summary>
        public static Raster RenderPicture(SKPicture picture, SKSize size, float dpi = 96f, SKColor? background = null)
        {
            if (picture == null)
            {
                throw new ArgumentNullException(nameof(picture));
            }
            if (dpi <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dpi));
            }
            if (size.Width <= 0f || size.Height <= 0f)
            {
                throw new ArgumentException("尺寸必须为正。", nameof(size));
            }

            float scale = dpi / 96f;
            SKImageInfo info = new SKImageInfo(
                Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
                Math.Max(1, (int)Math.Ceiling(size.Height * scale)),
                SKColorType.Bgra8888,
                SKAlphaType.Premul);

            // 注意：不要用 SKImage.FromPicture —— SkiaSharp 3.116.1 上会抛不可捕获的
            // AccessViolationException。必须走 Surface + DrawPicture。见 docs/02 §7.2。
            using (SKSurface? surface = SKSurface.Create(info))
            {
                if (surface == null)
                {
                    throw new InvalidOperationException("无法创建 " + info.Width + "x" + info.Height + " 渲染面。");
                }

                SKCanvas canvas = surface.Canvas;
                canvas.Clear(background ?? SKColors.Transparent);
                canvas.Scale(scale);
                canvas.DrawPicture(picture);

                using (SKImage? image = surface.Snapshot())
                using (SKPixmap? pixmap = image == null ? null : image.PeekPixels())
                {
                    if (pixmap == null)
                    {
                        throw new InvalidOperationException("光栅化失败：Surface 未产生像素。");
                    }

                    byte[] pixels = new byte[pixmap.BytesSize];
                    Marshal.Copy((IntPtr)pixmap.GetPixels(), pixels, 0, pixels.Length);
                    return new Raster(pixels, info.Width, info.Height);
                }
            }
        }
    }
}
