using System;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using OODD.Charts.Rendering;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace OODD.Charts.Adapters.Avalonia
{
    /// <summary>
    /// Avalonia 位图桥：内核产出的 <see cref="Raster"/> 是 BGRA8888 预乘，
    /// 与 Avalonia 的 <see cref="PixelFormat.Bgra8888"/> + <see cref="AlphaFormat.Premul"/>
    /// 逐字节同构，因此不需要任何通道换序或预乘换算，直接把像素灌进帧缓冲即可。
    /// </summary>
    public static class AvaloniaChartExtensions
    {
        /// <summary>
        /// 像素帧转 Avalonia <see cref="WriteableBitmap"/>。
        /// <para>
        /// <see cref="WriteableBitmap.Lock"/> 返回的 <see cref="ILockedFramebuffer.RowBytes"/>
        /// 可能按 4/16/64 字节对齐而大于 <see cref="Raster.Stride"/>，故按行搬运，
        /// 不能整块拷贝；<c>dpi</c> 只写进位图的物理尺寸元数据，不影响像素本身。
        /// </para>
        /// </summary>
        public static WriteableBitmap ToWriteableBitmap(this Raster raster, double dpi = 96d)
        {
            WriteableBitmap bitmap = new WriteableBitmap(
                new PixelSize(raster.Width, raster.Height),
                new Vector(dpi, dpi),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);

            using (ILockedFramebuffer locked = bitmap.Lock())
            {
                // RowBytes 可能含行尾填充，逐行对齐搬运，杜绝错位花屏。
                int rowBytes = locked.RowBytes;
                int stride = raster.Stride;
                IntPtr destination = (IntPtr)locked.Address;
                if (rowBytes == stride)
                {
                    Marshal.Copy(raster.Pixels, 0, destination, raster.Pixels.Length);
                }
                else
                {
                    for (int y = 0; y < raster.Height; y++)
                    {
                        Marshal.Copy(
                            raster.Pixels,
                            y * stride,
                            IntPtr.Add(destination, y * rowBytes),
                            stride);
                    }
                }
            }

            return bitmap;
        }

        /// <summary>
        /// 表单转 <see cref="WriteableBitmap"/>。DPI 同时决定像素密度与位图元数据：
        /// 96dpi 出图即 1 逻辑单位对应 1 像素，宿主缩放到指定设备像素宽时传反推的 DPI 即可免重采样。
        /// </summary>
        public static WriteableBitmap ToWriteableBitmap(this Chart chart, double dpi = 96d)
        {
            return ChartRaster.Render(chart, (float)dpi).ToWriteableBitmap(dpi);
        }

        /// <summary>任意矢量产物转 <see cref="WriteableBitmap"/>（零适配路径的位图版）。</summary>
        public static WriteableBitmap ToWriteableBitmap(this SKPicture picture, SKSizeI size, double dpi = 96d, SKColor? background = null)
        {
            return ChartRaster.RenderPicture(
                picture,
                new SKSize(size.Width, size.Height),
                (float)dpi,
                background).ToWriteableBitmap(dpi);
        }
    }
}
