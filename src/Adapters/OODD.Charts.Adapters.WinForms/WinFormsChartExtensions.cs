using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.WinForms
{
    /// <summary>
    /// WinForms（GDI+）位图桥。把内核产出的裸像素贴进 <see cref="Bitmap"/>，
    /// 应用侧不需要再引 <c>SkiaSharp.Views.WindowsForms</c>。
    /// </summary>
    public static class WinFormsChartExtensions
    {
        /// <summary>
        /// 像素帧转 GDI+ 位图。<see cref="Raster"/> 是 BGRA8888 预乘、行无填充，
        /// 与 <see cref="PixelFormat.Format32bppPArgb"/> 完全同构，一次 Marshal.Copy 落位。
        /// </summary>
        public static Bitmap ToBitmap(this Raster raster, float dpi = 96f)
        {
            Bitmap bitmap = new Bitmap(raster.Width, raster.Height, PixelFormat.Format32bppPArgb);
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, raster.Width, raster.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppPArgb);
            try
            {
                Marshal.Copy(raster.Pixels, 0, data.Scan0, raster.Pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            if (dpi > 0f)
            {
                bitmap.SetResolution(dpi, dpi);
            }
            return bitmap;
        }

        /// <summary>表单直接转 GDI+ 位图（96dpi 之外想要打印级清晰度就传高 DPI）。</summary>
        public static Bitmap ToBitmap(this Chart chart, float dpi = 96f)
        {
            return ChartRaster.Render(chart, dpi).ToBitmap(dpi);
        }

        /// <summary>任意矢量产物转 GDI+ 位图。</summary>
        public static Bitmap ToBitmap(this SKPicture picture, SKSizeI size, float dpi = 96f, SKColor? background = null)
        {
            return ChartRaster.RenderPicture(
                picture,
                new SKSize(size.Width, size.Height),
                dpi,
                background).ToBitmap(dpi);
        }
    }
}
