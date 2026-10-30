using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Uno
{
    /// <summary>
    /// Uno Platform 位图桥：<see cref="Raster"/>（BGRA8888 预乘）灌进 Uno 的
    /// WinUI 契约 <see cref="WriteableBitmap"/>（Uno 6.x 为
    /// <c>Microsoft.UI.Xaml.Media.Imaging.WriteableBitmap</c>；Uno 5.x 同名类型在
    /// <c>Windows.UI.Xaml.Media.Imaging</c>，换包即可）。
    /// <para>
    /// 为什么用 <c>PixelBuffer</c> 而不是 <c>WritePixels</c>：Uno 的
    /// <see cref="WriteableBitmap"/> 只实现 UWP/WinUI 契约（构造
    /// <c>(int, int)</c> + <c>SetSource</c> + <c>PixelBuffer</c> + <c>Invalidate</c>），
    /// 没有 WPF 那个带 <c>PixelFormat</c> 的构造重载和 <c>WritePixels</c>——实测
    /// Uno.UI 5.4.22 / Uno.WinUI 6.7.135 的程序集元数据里根本没有 <c>WritePixels</c> 项。
    /// <c>PixelBuffer</c> 是 <c>w × h × 4</c> 字节、行距 <c>PixelWidth × 4</c> 的 BGRA8888
    /// 裸缓冲（Uno 自己的 <c>WriteableBitmap.skia.cs</c> 就是按
    /// <c>SKColorType.Bgra8888</c> 读写的），与 <see cref="Raster"/> 逐字节同构，
    /// 因此不需要任何色彩转换，也不必退化成 PNG 编解码。
    /// </para>
    /// <para>
    /// 线程约束：和所有 XAML 契约一样，<c>WriteableBitmap</c> 是 DependencyObject，必须在
    /// UI 线程创建与写入；像素准备好后必须调
    /// <see cref="WriteableBitmap.Invalidate"/>，否则 Skia 后端仍显示上一次
    /// <c>TryOpenSourceSync</c> 缓存的画面。
    /// </para>
    /// </summary>
    public static class UnoChartExtensions
    {
        /// <summary>
        /// 像素帧转 Uno 位图源。
        /// </summary>
        /// <param name="raster">光栅化产物，行列紧凑、无对齐填充。</param>
        /// <param name="dpi">
        /// 出图 DPI。<b>仅参与契约统一，不写进位图</b>：WinUI/UWP 契约的
        /// <see cref="WriteableBitmap"/> 没有 DPI 元数据（WPF 那份有 <c>DpiX/DpiY</c>），
        /// Uno 的布局一律按"1 像素 = 1 逻辑单位"排布，清晰度完全由调用方给的像素数决定
        /// —— 见 <see cref="ChartControl"/>：它按目标设备像素宽出图，再除以
        /// <c>RasterizationScale</c> 落回逻辑尺寸，从而 1:1 上屏、不被重采样。
        /// </param>
        public static WriteableBitmap ToWriteableBitmap(this Raster raster, double dpi = 96d)
        {
            _ = dpi; // Uno 的位图不带 DPI 元数据，参数仅为与 WPF/WinForms/WinUI 适配器签名对称。

            if (raster.Pixels == null)
            {
                throw new ArgumentException("空 Raster 无法转成位图。", nameof(raster));
            }
            if (raster.Width <= 0 || raster.Height <= 0)
            {
                throw new ArgumentException("Raster 尺寸必须为正。", nameof(raster));
            }

            WriteableBitmap bitmap = new WriteableBitmap(raster.Width, raster.Height);
            // AsStream 走 Uno 自带的那份互操作扩展。注意命名空间陷阱：Uno 把
            // WindowsRuntimeBufferExtensions 放在 System.Runtime.InteropServices.WindowsRuntime
            // （.NET/WinUI 原生那份放在 System.Runtime.InteropServices），只 using 后者时
            // 编译器只认 WindowsRuntimeStreamExtensions.AsStream(IRandomAccessStream)，
            // 报 CS1929 而不是"找不到类型"，很容易误判成 PixelBuffer 不支持直写。
            using (Stream buffer = bitmap.PixelBuffer.AsStream())
            {
                buffer.Write(raster.Pixels, 0, raster.Pixels.Length);
            }

            bitmap.Invalidate();
            return bitmap;
        }

        /// <summary>表单转 Uno 位图源（先按 <paramref name="dpi"/> 光栅化，再走像素桥）。</summary>
        public static WriteableBitmap ToWriteableBitmap(this Chart chart, double dpi = 96d)
        {
            return ChartRaster.Render(chart, (float)dpi).ToWriteableBitmap(dpi);
        }

        /// <summary>任意矢量产物转 Uno 位图源。</summary>
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
