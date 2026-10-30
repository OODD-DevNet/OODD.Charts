using System;
using System.Collections.Generic;
using System.IO;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts
{
    /// <summary>PDF 输出选项。</summary>
    public sealed class PdfOptions
    {
        /// <summary>页面尺寸等于内容尺寸，不留边距。</summary>
        public static readonly PdfOptions Auto = new PdfOptions();

        /// <summary>A4 纵向、12pt 页边距、按宽度缩放适配。</summary>
        public static readonly PdfOptions A4Portrait = new PdfOptions
        {
            PageWidthPt = 595.28f,
            PageHeightPt = 841.89f,
            MarginPt = 12f,
        };

        /// <summary>A4 横向。</summary>
        public static readonly PdfOptions A4Landscape = new PdfOptions
        {
            PageWidthPt = 841.89f,
            PageHeightPt = 595.28f,
            MarginPt = 12f,
        };

        /// <summary>A5 纵向（三测单常用纸张）。</summary>
        public static readonly PdfOptions A5Portrait = new PdfOptions
        {
            PageWidthPt = 419.53f,
            PageHeightPt = 595.28f,
            MarginPt = 10f,
        };

        /// <summary>0 = 按内容自适应。</summary>
        public float PageWidthPt { get; set; }

        /// <summary>0 = 按内容自适应。</summary>
        public float PageHeightPt { get; set; }

        public float MarginPt { get; set; }

        /// <summary>是否允许为了放进页面而缩小内容。</summary>
        public bool ShrinkToFit { get; set; } = true;

        /// <summary>是否允许为了填满页面宽度而放大内容。</summary>
        public bool ExpandToFitWidth { get; set; }

        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Creator { get; set; }
    }

    /// <summary>
    /// 导出器。
    /// <para>
    /// <b>实现纪律</b>：光栅化一律走 <c>SKSurface + canvas.DrawPicture</c>。
    /// <c>SKImage.FromPicture(SKPicture, SKSizeI)</c> 在 SkiaSharp 3.116.1 上会抛
    /// 不可捕获的 <c>AccessViolationException</c>（实测，见 docs/01 §6 E1 与 docs/02 §7.2）。
    /// </para>
    /// </summary>
    public static class ChartExport
    {
        /// <summary>逻辑像素（96dpi）→ PostScript 点（72/inch）。</summary>
        public const float PointsPerLogicalPixel = 72f / 96f;

        private static SKEncodedImageFormat ToNative(ChartImageFormat format)
        {
            switch (format)
            {
                case ChartImageFormat.Jpeg:
                    return SKEncodedImageFormat.Jpeg;
                case ChartImageFormat.Webp:
                    return SKEncodedImageFormat.Webp;
                default:
                    return SKEncodedImageFormat.Png;
            }
        }

        /// <summary>把已渲染的位图编码成字节。</summary>
        public static byte[] EncodeImage(SKImage image, ChartImageFormat format, int quality)
        {
            if (image == null)
            {
                throw new ArgumentNullException(nameof(image));
            }
            using (SKData? data = image.Encode(ToNative(format), ClampQuality(quality)))
            {
                if (data == null)
                {
                    throw new InvalidOperationException("编码失败：" + format);
                }
                return data.ToArray();
            }
        }

        private static int ClampQuality(int quality)
        {
            if (quality < 0)
            {
                return 0;
            }
            return quality > 100 ? 100 : quality;
        }

        public static void WriteImage(Chart chart, Stream output, ChartImageFormat format, float dpi, int quality)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }
            using (SKImage image = chart.RenderImage(dpi))
            {
                byte[] bytes = EncodeImage(image, format, quality);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        public static byte[] ToPng(Chart chart, float dpi = 96f) => chart.Encode(ChartImageFormat.Png, dpi, 100);
        public static byte[] ToJpeg(Chart chart, float dpi = 96f, int quality = 90) => chart.Encode(ChartImageFormat.Jpeg, dpi, quality);
        public static byte[] ToWebp(Chart chart, float dpi = 96f, int quality = 90) => chart.Encode(ChartImageFormat.Webp, dpi, quality);

        // ---------------------------------------------------------------- PDF

        public static void WritePdf(Chart chart, Stream output, PdfOptions options)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }
            WritePdfPages(new[] { chart }, output, options ?? PdfOptions.Auto);
        }

        /// <summary>多页 PDF：每页一张 <see cref="Chart"/>，页眉由调用方通过分页文档保证重复。</summary>
        public static void WritePdf(IEnumerable<Chart> pages, Stream output, PdfOptions? options = null)
        {
            WritePdfPages(pages, output, options ?? PdfOptions.Auto);
        }

        private static void WritePdfPages(IEnumerable<Chart> pages, Stream output, PdfOptions options)
        {
            if (pages == null)
            {
                throw new ArgumentNullException(nameof(pages));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            SKDocumentPdfMetadata? metadata = null;
            if (options.Title != null || options.Author != null || options.Creator != null)
            {
                metadata = new SKDocumentPdfMetadata
                {
                    Title = options.Title,
                    Author = options.Author,
                    Creator = options.Creator ?? "OODD.Charts",
                };
            }

            using (SKDocument document = metadata != null
                       ? SKDocument.CreatePdf(output, metadata.Value)
                       : SKDocument.CreatePdf(output))
            {
                if (document == null)
                {
                    throw new InvalidOperationException("无法创建 PDF 文档。");
                }

                foreach (Chart page in pages)
                {
                    // 落字闸门：PDF 内嵌字形同样要走 Skia 的字面缓存，与 SVG 导出互斥。
                    TextRenderGate.EnterRender();
                    try
                    {
                        using (SKPicture picture = page.Record())
                        {
                            SKRect bounds = picture.CullRect;
                            float contentW = bounds.Width * PointsPerLogicalPixel;
                            float contentH = bounds.Height * PointsPerLogicalPixel;

                            float pageW = options.PageWidthPt > 0f ? options.PageWidthPt : contentW;
                            float pageH = options.PageHeightPt > 0f ? options.PageHeightPt : contentH;

                            using (SKCanvas canvas = document.BeginPage(pageW, pageH))
                            {
                                float scale = contentW <= 0f ? 1f : PointsPerLogicalPixel;
                                float drawW = contentW;
                                float drawH = contentH;

                                if (options.PageWidthPt > 0f && options.PageHeightPt > 0f)
                                {
                                    float availW = pageW - options.MarginPt * 2f;
                                    float availH = pageH - options.MarginPt * 2f;
                                    float fit = Math.Min(availW / contentW, availH / contentH);
                                    if ((fit < 1f && options.ShrinkToFit) || (fit > 1f && options.ExpandToFitWidth))
                                    {
                                        scale *= fit;
                                        drawW *= fit;
                                        drawH *= fit;
                                    }
                                }

                                canvas.Translate(options.MarginPt + (pageW - options.MarginPt * 2f - drawW) / 2f,
                                                 options.MarginPt);
                                canvas.Scale(scale);
                                canvas.DrawPicture(picture);
                            }
                            document.EndPage();
                        }
                    }
                    finally
                    {
                        TextRenderGate.ExitRender();
                    }
                }

                document.Close();
            }
        }

        // ---------------------------------------------------------------- SVG

        /// <summary>
        /// SVG 文本丢字（SkiaSharp 3.116.1 的并发缺陷）的两道防线：<see cref="TextRenderGate"/> 独占导出 +
        /// <see cref="SvgMaxAttempts"/> 次重绘。
        /// <para>
        /// 现象：把带文字的 <c>SKPicture</c> 回放给 <c>SKSvgCanvas</c> 时，若别的线程正在栅格化文字，
        /// 字形→码位的反查会被写坏，产物里出现内容空的 <c>&lt;text&gt;</c> 节点——那个字在 SVG 里
        /// 既检索不到、也看不见。位图和 PDF 的落墨不受影响（实测 480/480 张墨量恒等）。
        /// </para>
        /// <para>
        /// 实测（net8.0 / Windows，每档 400 次导出、每次 6 个文字节点）：
        /// ① 纯串行 1000 次 0 例；② 8 线程并发导出 197/200 例、2 线程 190/200 例；
        /// ③ 只把"导出 vs 导出"用进程级互斥锁挡住、但 8 线程并发出<b>同字族</b>位图，仍要丢字：
        /// 大表单一次导出几百个文字节点，撞上的概率按节点数放大——实测 19～23/120 例，
        /// <b>纯靠重绘压不住</b>（撞的是一个持续竞争窗口，重跑还是撞）；
        /// ④ 并发位图道换成 Arial（不同字面）0/400 例、换成只画线不画字 0/800 例；
        /// ⑤ 导出道或位图道各自 new 一个 <c>SystemFontResolver</c> 仍然照丢。
        /// ④⑤ 说明共享的是 Skia 原生字面/字形缓存，不是我们的解析器缓存，
        /// 所以唯一有效的隔离是<b>让导出独占落字</b>——即 <see cref="TextRenderGate"/> 的写锁：
        /// 出图之间照旧并发（读锁），导出时排空。上闸门后同一压力 0/120 例（连跑 5 轮）。
        /// 重绘留着兜"宿主绕开本库自己拿画布写字"这条管不到的路。
        /// </para>
        /// </summary>
        private const int SvgMaxAttempts = 3;

        /// <summary>
        /// 导出 SVG。产物为真矢量（含 <c>&lt;path&gt;</c>、<c>&lt;text&gt;</c>，不含 <c>&lt;image&gt;</c>）。
        /// <para>
        /// 多线程同时调用是安全的：导出按 <see cref="TextRenderGate"/> 独占落字，并对 Skia 偶发的空
        /// <c>&lt;text&gt;</c> 节点重绘；<see cref="SvgMaxAttempts"/> 次仍丢字就抛
        /// <see cref="InvalidOperationException"/>，而不是交付缺字的产物（成因与实测见 <see cref="SvgMaxAttempts"/>）。
        /// </para>
        /// </summary>
        public static void WriteSvg(Chart chart, Stream output)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            byte[] svg = RenderSvg(chart);
            output.Write(svg, 0, svg.Length);
        }

        private static byte[] RenderSvg(Chart chart)
        {
            // 独占落字：等所有在飞的出图排空，并挡住新的出图。写锁本身也串行化了导出之间。
            // 版面求解（量字宽）也在锁内，它同样要碰 Skia 的字面缓存。
            TextRenderGate.EnterExport();
            try
            {
                SKSize size = chart.MeasureSize();
                SKRect frame = new SKRect(0f, 0f, size.Width, size.Height);
                for (int attempt = 1; ; attempt++)
                {
                    byte[] svg = RenderSvgOnce(chart, frame);
                    if (!HasEmptyTextNode(svg))
                    {
                        return svg;
                    }
                    if (attempt >= SvgMaxAttempts)
                    {
                        throw new InvalidOperationException(
                            "SVG 连续 " + attempt + " 次导出都出现内容空的 <text> 节点：SkiaSharp 3.116.1 在"
                            + "其他线程同时栅格化文字时会丢失字形→码位反查。请把 SVG 导出与其他 Skia 落字串行，或改用位图/PDF 导出。");
                    }
                }
            }
            finally
            {
                TextRenderGate.ExitExport();
            }
        }

        private static byte[] RenderSvgOnce(Chart chart, SKRect frame)
        {
            using (MemoryStream buffer = new MemoryStream())
            {
                using (SKPicture picture = chart.Record())
                using (SKCanvas? canvas = SKSvgCanvas.Create(frame, buffer))
                {
                    if (canvas == null)
                    {
                        throw new InvalidOperationException("无法创建 SVG 画布。");
                    }
                    canvas.Clear(SKColors.Transparent);
                    canvas.DrawPicture(picture);
                }
                return buffer.ToArray();
            }
        }

        /// <summary>
        /// 丢字产物的特征：<c>&lt;text ...&gt;&lt;/text&gt;</c>——开标签的 <c>&gt;</c> 紧挨闭标签。
        /// 实测丢字恒以"某一个节点内容变空、节点总数不变"的形态出现，所以不需要期望文本台账。
        /// <c>&lt;/text&gt;</c> 全是 ASCII，而 UTF-8 的续字节恒 ≥ 0x80，按字节匹配不会误命中多字节字符内部。
        /// </summary>
        private static bool HasEmptyTextNode(byte[] svg)
        {
            for (int i = 0; i + 7 <= svg.Length; i++)
            {
                if (svg[i] != (byte)'<' || svg[i + 1] != (byte)'/'
                    || svg[i + 2] != (byte)'t' || svg[i + 3] != (byte)'e'
                    || svg[i + 4] != (byte)'x' || svg[i + 5] != (byte)'t'
                    || svg[i + 6] != (byte)'>')
                {
                    continue;
                }
                if (i > 0 && svg[i - 1] == (byte)'>')
                {
                    return true;
                }
            }
            return false;
        }

        public static string ToSvgString(Chart chart)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                WriteSvg(chart, stream);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }
}
