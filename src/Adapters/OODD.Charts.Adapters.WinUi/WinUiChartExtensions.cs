using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using OODD.Charts.Rendering;
using SkiaSharp;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml.Media.Imaging;

namespace OODD.Charts.Adapters.WinUi
{
    /// <summary>
    /// WinUI 3 位图桥：<see cref="Raster"/>（BGRA8888 预乘）原样灌进
    /// <see cref="WriteableBitmap"/> 的 <see cref="WriteableBitmap.PixelBuffer"/>。
    /// <para>
    /// 为什么不需要字节重排：<see cref="Raster"/> 的像素来自 Skia 的
    /// <c>SKColorType.Bgra8888</c> + <c>SKAlphaType.Premul</c> —— 内存布局是每像素
    /// B、G、R、A 四个字节；而 WinUI 的 <c>WriteableBitmap</c> 内部缓冲固定就是
    /// <c>BitmapPixelFormat.Bgra8</c> + <c>BitmapAlphaMode.Premultiplied</c>
    /// （它只接受预排列，UWP 时代还接受 Straight，WinUI 3 已统一为预乘）。
    /// 字节序与 alpha 语义双双对齐，所以 <see cref="Raster.Pixels"/> 可以整块 memcpy，
    /// 行跨度 <see cref="Raster.Stride"/>（恒为 Width×4，无填充）也与 WinUI 的缓冲跨度一致。
    /// </para>
    /// <para>
    /// 线程约束：WinUI 的 <see cref="WriteableBitmap"/> 是 WinRT/STA 亲和的 COM 对象，
    /// 必须在 UI 线程创建并写缓冲；后台渲染请先把 <see cref="Raster"/>（托管数组，可跨线程）
    /// 算好，再用 <c>DispatcherQueue.TryEnqueue</c> 回 UI 线程调用本类方法。
    /// </para>
    /// <para>
    /// 实测说明（WindowsAppSDK 1.7.260224002 / net8.0-windows10.0.19041.0）：
    /// WinUI 的 <c>WriteableBitmap</c> 没有 WPF 的 <c>WritePixels</c>/<c>DpiX</c>，
    /// 也没有 <c>WriteableBitmap(SoftwareBitmap)</c> 构造；<c>SoftwareBitmap.CopyPixelsFromBuffer</c>
    /// 在其投影里是 <c>BitmapBuffer.CopyPixelsFromBuffer()</c> 显式接口。
    /// 本文件只保留能编译通过的 <c>PixelBuffer.AsStream()</c> 写入形态。
    /// </para>
    /// </summary>
    public static class WinUiChartExtensions
    {
        /// <summary>
        /// 像素帧转 WinUI 位图源：新建 <see cref="WriteableBitmap"/>(Width, Height)，
        /// 把 <see cref="Raster.Pixels"/> 一次性写进 <see cref="WriteableBitmap.PixelBuffer"/>。
        /// </summary>
        /// <param name="raster">光栅化产物（BGRA8888 预乘）。</param>
        /// <param name="dpi">
        /// 出图时使用的 DPI，仅作语义标注透传（WinUI 位图不携带 DPI 元数据，
        /// XAML 按"1 位图像素 = 1 有效像素"摆放；物理尺寸由宿主控件换算）。
        /// </param>
        public static WriteableBitmap ToWriteableBitmap(this Raster raster, double dpi = 96d)
        {
            _ = dpi; // WinUI 位图无 DPI 属性，保留参数是为了与 WPF/WinForms 适配器签名对称。

            if (raster.Pixels == null)
            {
                throw new ArgumentNullException(nameof(raster));
            }

            WriteableBitmap bitmap = new WriteableBitmap(raster.Width, raster.Height);
            using (Stream stream = bitmap.PixelBuffer.AsStream())
            {
                stream.Write(raster.Pixels, 0, raster.Pixels.Length);
            }

            return bitmap;
        }

        /// <summary>表单转 WinUI 位图源（几何按 96dpi 求解，<paramref name="dpi"/> 只提清晰度）。</summary>
        public static WriteableBitmap ToWriteableBitmap(this Chart chart, double dpi = 96d)
        {
            return ChartRaster.Render(chart, (float)dpi).ToWriteableBitmap(dpi);
        }

        /// <summary>任意矢量产物转 WinUI 位图源（零适配路径的位图版）。</summary>
        public static WriteableBitmap ToWriteableBitmap(this SKPicture picture, SKSizeI size, double dpi = 96d, SKColor? background = null)
        {
            return ChartRaster.RenderPicture(
                picture,
                new SKSize(size.Width, size.Height),
                (float)dpi,
                background).ToWriteableBitmap(dpi);
        }

        /// <summary>
        /// 备用桥：把已编码（PNG/JPEG）的字节流经 <see cref="InMemoryRandomAccessStream"/>
        /// 交给 <see cref="BitmapSource.SetSource(IRandomAccessStream)"/>。
        /// 走解码器、有分配，适合一次性导出预览，不适合每帧刷新，故不作主路径。
        /// </summary>
        public static void SetEncodedPixelSource(this BitmapSource bitmap, byte[] encodedBytes)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }
            if (encodedBytes == null)
            {
                throw new ArgumentNullException(nameof(encodedBytes));
            }

            using (InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream())
            using (MemoryStream source = new MemoryStream(encodedBytes))
            {
                source.CopyTo(stream.AsStreamForWrite());
                bitmap.SetSource(stream);
            }
        }
    }
}
