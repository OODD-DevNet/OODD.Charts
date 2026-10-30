using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Wpf
{
    /// <summary>
    /// WPF 位图桥：<see cref="Raster"/>（BGRA8888 预乘）直接灌进
    /// <see cref="WriteableBitmap"/>（<see cref="PixelFormats.Bgra32"/>），格式一一对应。
    /// </summary>
    public static class WpfChartExtensions
    {
        /// <summary>像素帧转 WPF 位图源（冻结后可跨线程复用）。</summary>
        public static WriteableBitmap ToWriteableBitmap(this Raster raster, double dpi = 96d)
        {
            WriteableBitmap bitmap = new WriteableBitmap(
                raster.Width,
                raster.Height,
                dpi,
                dpi,
                PixelFormats.Bgra32,
                null);
            bitmap.WritePixels(new Int32Rect(0, 0, raster.Width, raster.Height), raster.Pixels, raster.Stride, 0);
            bitmap.Freeze();
            return bitmap;
        }

        /// <summary>表单转 WPF 位图源。</summary>
        public static WriteableBitmap ToWriteableBitmap(this Chart chart, double dpi = 96d)
        {
            return ChartRaster.Render(chart, (float)dpi).ToWriteableBitmap(dpi);
        }

        /// <summary>任意矢量产物转 WPF 位图源。</summary>
        public static WriteableBitmap ToWriteableBitmap(this SKPicture picture, SKSizeI size, double dpi = 96d, SKColor? background = null)
        {
            return ChartRaster.RenderPicture(
                picture,
                new SKSize(size.Width, size.Height),
                (float)dpi,
                background).ToWriteableBitmap(dpi);
        }

        /// <summary>把表单编码成 PNG 字节，再包成 WPF <see cref="BitmapSource"/>（走解码器，慢，仅一次性导出用）。</summary>
        public static BitmapSource ToBitmapSource(this Chart chart, float dpi = 96f)
        {
            byte[] png = chart.Encode(ChartImageFormat.Png, dpi, 100);
            using (MemoryStream stream = new MemoryStream(png))
            {
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                BitmapSource frame = decoder.Frames[0];
                frame.Freeze();
                return frame;
            }
        }
    }
}
