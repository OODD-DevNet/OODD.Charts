using System;
using System.IO;
using Microsoft.Maui.Controls;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Maui
{
    /// <summary>
    /// MAUI 图像桥：<see cref="Chart"/> / <see cref="Raster"/> / <see cref="SKPicture"/> → <see cref="ImageSource"/>。
    /// <para>
    /// <b>为什么只有"编码字节"这一条路（实测 Microsoft.Maui.Controls 10.0.20）：</b>
    /// <c>ImageSource</c> 的公开工厂只有 <c>FromFile / FromResource / FromStream / FromUri</c>，
    /// <c>Image.Source</c> 的类型就是 <c>ImageSource</c>；<c>Microsoft.Maui.Graphics</c> 里
    /// 已经没有能吃裸像素的公开位图类型（<c>Bitmap</c> 已移出主包，只剩
    /// <c>BitmapExportContext</c>、只读的 <c>IImage</c>，而 <c>IImage</c> 无法挂进 <c>Image.Source</c>，
    /// 除非宿主自己写 handler）。所以 <see cref="Raster"/> 的 BGRA 像素必须先编码成 PNG，
    /// 再交给各平台解码器（Android=BitmapFactory、iOS/macOS=CGImageSource、Windows=WinRT BitmapDecoder）。
    /// </para>
    /// <para>
    /// 代价：多一次 PNG 编解码，且解码在平台侧异步进行。需要"零编码、零解码"时走
    /// <see cref="ChartCanvasView"/>（矢量直绘 <c>SKCanvas</c>），或用 <c>chart.Record()</c> 自绘。
    /// </para>
    /// </summary>
    public static class MauiChartExtensions
    {
        /// <summary>
        /// 表单 → <see cref="ImageSource"/>（PNG 字节 + 惰性流回调）。
        /// <para>
        /// 本方法同步完成光栅化与编码，<b>不依赖 UI 线程</b>，可以在后台线程预先出图，
        /// 再把返回值赋给 <c>Image.Source</c>（赋值这一步必须在主线程，MAUI 的 BindableProperty
        /// 有线程检查，用 <c>window.Dispatcher.Dispatch</c>）。
        /// </para>
        /// </summary>
        /// <param name="chart">表单。不接管其生命周期。</param>
        /// <param name="dpi">出图 DPI；96 即 1 逻辑单位 = 1 像素。</param>
        public static ImageSource ToImageSource(this Chart chart, float dpi = 96f)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            return chart.Encode(ChartImageFormat.Png, dpi, 100).ToImageSource();
        }

        /// <summary>
        /// 裸像素帧 → <see cref="ImageSource"/>。等价于"先 <see cref="ToPngBytes(Raster)"/> 再包装"，
        /// 用于调用方已经拿着 <see cref="Raster"/>（比如后台线程出好图）的场景。
        /// </summary>
        public static ImageSource ToImageSource(this Raster raster)
        {
            return raster.ToPngBytes().ToImageSource();
        }

        /// <summary>任意矢量产物 → <see cref="ImageSource"/>（零适配路径的位图版）。</summary>
        public static ImageSource ToImageSource(this SKPicture picture, SKSize size, float dpi = 96f, SKColor? background = null)
        {
            return ChartRaster.RenderPicture(picture, size, dpi, background).ToImageSource();
        }

        /// <summary>
        /// 已编码的图片字节（PNG/JPEG/WebP，MAUI 各平台解码器都认）→ <see cref="ImageSource"/>。
        /// </summary>
        public static ImageSource ToImageSource(this byte[] encodedBytes)
        {
            if (encodedBytes == null)
            {
                throw new ArgumentNullException(nameof(encodedBytes));
            }

            // 流的持有方是 MAUI：StreamImageSource 每次加载都调用回调取流，解码完成后 Dispose 它。
            // 因此回调可能被调用多次（首帧、Cancel() 后重载、复用同一 ImageSource），
            // 必须"每次返回一条新流"，绝不能把同一个 MemoryStream 实例交出去两次；
            // 字节数组的所有权仍在本闭包（只读视图，writable:false），MAUI 不会释放它。
            byte[] bytes = encodedBytes;
            return ImageSource.FromStream(() => new MemoryStream(bytes, 0, bytes.Length, writable: false, publiclyVisible: true));
        }

        /// <summary>
        /// <see cref="Raster"/>（BGRA8888 预乘）→ PNG 字节。
        /// <para>用 <c>SKImage.FromPixelCopy</c> 而不是 <c>FromPixels</c>：前者把像素复制进 Skia 自己的
        /// 内存，返回的 <see cref="SKImage"/> 与调用方的 <c>byte[]</c> 生命周期解耦，
        /// 不会因为 <see cref="Raster"/> 是 <c>struct</c>、数组可能被上层提前丢弃而悬空。</para>
        /// </summary>
        public static byte[] ToPngBytes(this Raster raster)
        {
            SKImageInfo info = new SKImageInfo(raster.Width, raster.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (SKImage? image = SKImage.FromPixelCopy(info, raster.Pixels, raster.Stride))
            {
                if (image == null)
                {
                    throw new InvalidOperationException("像素帧装载失败：" + raster.Width + "x" + raster.Height + "。");
                }

                using (SKData? data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    if (data == null)
                    {
                        throw new InvalidOperationException("PNG 编码失败。");
                    }

                    return data.ToArray();
                }
            }
        }

        /// <summary>表单 → PNG 字节（不落盘，供自定义 handler / 缓存 / 分享用）。</summary>
        public static byte[] ToPngBytes(this Chart chart, float dpi = 96f)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            return chart.Encode(ChartImageFormat.Png, dpi, 100);
        }
    }
}
