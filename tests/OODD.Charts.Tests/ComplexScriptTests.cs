using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;
using Xunit;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbFont = HarfBuzzSharp.Font;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 复杂文种整形（阿拉伯文连写、天城文辅音簇、RTL 版面方向）。
    /// <para>
    /// 判据不引用库内部实现：期望宽度由测试自己用 HarfBuzzSharp 对同一字体整形算出来，
    /// 再与 <see cref="Painter.TextWidth"/> 对账；同时用"逐字直排"的
    /// <c>SKFont.MeasureText</c> 作负对照——两条路若给同一个数，
    /// 说明整形根本没生效。
    /// </para>
    /// <para>
    /// 字体按运行时探测：Windows 上阿拉伯文落在 Segoe UI、天城文落在 Nirmala UI。
    /// 探测顺序与库的候选表一致，机器上没有任何覆盖字体时用例直接抛消息，不静默跳过。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class ComplexScriptTests
    {
        private const string Arabic = "مرحبا";
        private const string Devanagari = "नमस्ते";
        private const float Size = 24f;

        private static readonly FontKey AutoKey = new FontKey(null, Size);

        /// <summary>与 <c>TextShaper.ScriptFamilies</c> 的候选表同序；库换表时这里要跟着改。</summary>
        private static readonly Dictionary<string, string[]> ScriptFamilies =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "Arab", new[] { "Segoe UI", "Arial", "Tahoma", "Noto Naskh Arabic", "Noto Sans Arabic", "Dubai", "Geeza Pro" } },
                { "Deva", new[] { "Nirmala UI", "Mangal", "Noto Sans Devanagari", "Kohinoor Devanagari" } },
            };

        private static readonly Dictionary<string, string[]> ScriptLanguages =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "Arab", new[] { "ar" } },
                { "Deva", new[] { "hi", "mr", "ne" } },
            };

        // ============================================================ 宽度对账

        [Fact]
        public void 阿拉伯文_量宽等于HarfBuzz整形结果且不同于逐字直排()
        {
            using (SKTypeface covering = RequireCovering("Arab", Arabic))
            {
                float shaped = ShapeWidth(covering, Arabic, out int glyphCount);
                float naive;
                using (SKFont font = new SKFont(covering, Size))
                {
                    naive = font.MeasureText(Arabic);
                }

                Assert.True(glyphCount == Arabic.Length,
                            "阿拉伯文整形后仍是 " + Arabic.Length + " 个字形（基线量），实到 " + glyphCount);
                Assert.True(naive - shaped > 6f,
                            "逐字直排宽度 " + naive + " 与整形宽度 " + shaped
                            + " 太接近，用例没有区分度（连写没有发生？）");

                float actual = PainterWidth(Arabic, AutoKey);
                AssertClose(actual, shaped, 0.05f, "阿拉伯文量宽");
            }
        }

        [Fact]
        public void 天城文_量宽等于HarfBuzz整形结果且辅音簇被合并()
        {
            using (SKTypeface covering = RequireCovering("Deva", Devanagari))
            {
                float shaped = ShapeWidth(covering, Devanagari, out int glyphCount);
                float naive;
                using (SKFont font = new SKFont(covering, Size))
                {
                    naive = font.MeasureText(Devanagari);
                }

                Assert.True(glyphCount > 0 && glyphCount < Devanagari.Length,
                            "天城文整形后应有合并字形（辅音簇/组合符），字符 " + Devanagari.Length
                            + " 个，实到字形 " + glyphCount + " 个");
                Assert.True(Math.Abs(naive - shaped) > 3f,
                            "逐字直排宽度 " + naive + " 与整形宽度 " + shaped
                            + " 太接近，用例没有区分度（辅音簇没有重排？）");

                float actual = PainterWidth(Devanagari, AutoKey);
                AssertClose(actual, shaped, 0.05f, "天城文量宽");
            }
        }

        [Fact]
        public void 静态量宽与Painter量宽在复杂文种上一致()
        {
            AssertClose(Primitives.MeasureText(Chart.SharedFonts, Arabic, AutoKey),
                        PainterWidth(Arabic, AutoKey), 0.05f, "阿拉伯文静态量宽");
            AssertClose(Primitives.MeasureText(Chart.SharedFonts, Devanagari, AutoKey),
                        PainterWidth(Devanagari, AutoKey), 0.05f, "天城文静态量宽");
        }

        [Fact]
        public void 拉丁与中文量宽仍走逐字度量_不受整形路径影响()
        {
            // 复杂文种之外必须与改造前逐位相同：这几个字符串是既有 473 条用例与 40 张截图的版面来源。
            string[] texts = { "Blood pressure 120/80 mmHg", "血压 120/80 mmHg", "体温38.5", "OODD.Charts" };
            foreach (string text in texts)
            {
                using (SKFont font = new SKFont(Chart.SharedFonts.Resolve(AutoKey), Size))
                {
                    AssertClose(Primitives.MeasureText(Chart.SharedFonts, text, AutoKey),
                                font.MeasureText(text), 0.001f, "非复杂文种量宽（" + text + "）");
                }
            }
        }

        // ============================================================ 字体回退

        [Fact]
        public void 中文字体排阿拉伯文时按脚本换字体而不是画豆腐块()
        {
            string baseFamily = string.Empty;
            SKTypeface? baseTypeface = null;
            foreach (string family in new[] { "Microsoft YaHei", "SimSun", "Noto Sans CJK SC", "PingFang SC", "MS Gothic" })
            {
                SKTypeface? candidate = SKTypeface.FromFamilyName(family, SKFontStyle.Normal);
                if (candidate == null)
                {
                    continue;
                }
                if (!string.Equals(candidate.FamilyName, family, StringComparison.OrdinalIgnoreCase)
                    || candidate.ContainsGlyphs(Arabic))
                {
                    candidate.Dispose();
                    continue;
                }
                baseFamily = family;
                baseTypeface = candidate;
                break;
            }

            Assert.True(baseTypeface != null,
                        "本机找不到「有中文字形但不含阿拉伯文」的字体，无法验证脚本回退");

            using (baseTypeface)
            using (SKTypeface covering = RequireCovering("Arab", Arabic))
            {
                float expected = ShapeWidth(covering, Arabic, out _);
                float naive;
                using (SKFont font = new SKFont(baseTypeface!, Size))
                {
                    naive = font.MeasureText(Arabic);
                }

                Assert.True(Math.Abs(naive - expected) > 8f,
                            baseFamily + " 逐字直排宽度 " + naive + " 与整形宽度 " + expected
                            + " 差异太小，用例没有区分度");

                FontKey key = new FontKey(baseFamily, Size);
                AssertClose(PainterWidth(Arabic, key), expected, 0.5f,
                            baseFamily + " 排阿拉伯文的回退量宽");
            }
        }

        // ============================================================ 落墨与方向

        [Fact]
        public void 阿拉伯文落墨不越出整形后的步进宽度()
        {
            using (SKTypeface covering = RequireCovering("Arab", Arabic))
            {
                float shaped = ShapeWidth(covering, Arabic, out _);
                float naive;
                using (SKFont font = new SKFont(covering, Size))
                {
                    naive = font.MeasureText(Arabic);
                }
                Assert.True(naive - shaped > 6f, "区分度不足：直排 " + naive + " vs 整形 " + shaped);

                const float OriginX = 10f;
                using (ImageProbe probe = Render(220, 60, p => p.Text(Arabic, OriginX, 40f, AutoKey, SKColors.Black)))
                {
                    int[] ink = InkBounds(probe, 0, 0, 220, 60);
                    Assert.True(ink[0] >= 0, "阿拉伯文没落墨");

                    // 整形后的整段步进是 shaped；未整形会按 naive 铺开。右侧多出的窗口必须干净。
                    int limit = (int)Math.Ceiling(OriginX + shaped) + 5;
                    Assert.True(ink[1] <= limit,
                                "墨迹右缘 " + ink[1] + " 越出整形步进上界 " + limit
                                + "（直排宽度 " + naive + " 才是这个量级）");
                    Assert.False(probe.HasInkInBox(limit, 0, 220, 60),
                                 "整形步进之外仍有墨：文字可能按逐字直排画的");
                    Assert.True(ink[0] >= OriginX - 6, "墨迹越到基线起点左侧 6px 以外，起点没对准：" + ink[0]);
                }
            }
        }

        [Fact]
        public void 阿拉伯文夹数字时数字不参与整形的反向重排()
        {
            // HarfBuzz 不做 bidi：整段按 RTL 反转时 "123" 会变成 "321"。
            // 库把数字切成独立 LTR 段后再反转段序，所以数字段应原样落在最左端。
            // 判据是逐像素对账：混合文本左端 [10, 10+数字宽) 必须与单独画 "123" 完全一致。
            string mixed = "مرحبا 123";
            float digitsWidth;
            using (SKFont baseFont = new SKFont(Chart.SharedFonts.Resolve(AutoKey), Size))
            {
                digitsWidth = baseFont.MeasureText("123");
            }
            Assert.True(digitsWidth > 20f, "数字段宽度异常：" + digitsWidth);

            using (ImageProbe digitsOnly = Render(260, 60, p => p.Text("123", 10f, 40f, AutoKey, SKColors.Black)))
            using (ImageProbe combined = Render(260, 60, p => p.Text(mixed, 10f, 40f, AutoKey, SKColors.Black)))
            {
                int[] single = InkBounds(digitsOnly, 0, 0, 260, 60);
                Assert.True(single[0] >= 0, "对照图里没画出 \"123\"，用例失去意义");

                int right = (int)Math.Ceiling(10f + digitsWidth) + 1;
                int mismatches = 0;
                int ink = 0;
                for (int y = 5; y < 60; y++)
                {
                    for (int x = 8; x < right; x++)
                    {
                        bool a = digitsOnly.IsInk(x, y);
                        bool b = combined.IsInk(x, y);
                        if (a)
                        {
                            ink++;
                        }
                        if (a != b)
                        {
                            mismatches++;
                        }
                    }
                }

                Assert.True(ink > 30, "数字对照区里的墨太少（" + ink + "），对账不可信");
                Assert.True(mismatches == 0,
                            "混合文本左端与单独画 \"123\" 有 " + mismatches
                            + " 个像素不一致：数字被当成 RTL 段反向重排了（会画成 321）");
            }
        }

        [Fact]
        public void 阿拉伯文Near对齐贴右缘_Far对齐贴左缘_拉丁文方向不变()
        {
            SKRect box = new SKRect(10f, 5f, 190f, 45f);
            float width = PainterWidth(Arabic, AutoKey);
            Assert.True(width > 25f && width < 60f, "阿拉伯文宽度异常：" + width);

            using (ImageProbe near = Render(200, 60, p =>
                       p.TextInBox(Arabic, box, AutoKey, SKColors.Black, CellAlign.Near, CellAlign.Center)))
            using (ImageProbe far = Render(200, 60, p =>
                       p.TextInBox(Arabic, box, AutoKey, SKColors.Black, CellAlign.Far, CellAlign.Center)))
            {
                int[] nearInk = InkBounds(near, 0, 0, 200, 60);
                int[] farInk = InkBounds(far, 0, 0, 200, 60);
                Assert.True(nearInk[0] >= 0 && farInk[0] >= 0, "两种对齐都应有墨");

                // Near = 起始边 = 右缘：墨迹右端贴近 190-1，起点在 189-width 附近。
                Assert.True(nearInk[1] >= 180, "RTL 的 Near 没有贴右缘，墨迹右端 " + nearInk[1]);
                Assert.True(nearInk[0] >= (int)(190f - 1f - width - 6f),
                            "RTL 的 Near 把文字顶到了左边：墨迹左端 " + nearInk[0]
                            + "，应在 " + (190f - 1f - width) + " 附近");

                // Far = 结束边 = 左缘。
                Assert.True(farInk[0] <= 18, "RTL 的 Far 没有贴左缘，墨迹左端 " + farInk[0]);
                Assert.True(farInk[1] <= (int)(10f + 1f + width + 6f),
                            "RTL 的 Far 把文字推到了右边：墨迹右端 " + farInk[1]);
            }

            // 正对照：同一套 Near/Far 对 LTR 文本仍是左/右，说明上面红的是方向判断而不是对齐本身。
            using (ImageProbe latinNear = Render(200, 60, p =>
                       p.TextInBox("Hello", box, AutoKey, SKColors.Black, CellAlign.Near, CellAlign.Center)))
            using (ImageProbe latinFar = Render(200, 60, p =>
                       p.TextInBox("Hello", box, AutoKey, SKColors.Black, CellAlign.Far, CellAlign.Center)))
            {
                int[] a = InkBounds(latinNear, 0, 0, 200, 60);
                int[] b = InkBounds(latinFar, 0, 0, 200, 60);
                Assert.True(a[0] >= 0 && b[0] >= 0, "拉丁对照应各有墨");
                Assert.True(a[0] <= 18, "拉丁文 Near 应贴左缘，实到 " + a[0]);
                Assert.True(b[1] >= 180, "拉丁文 Far 应贴右缘，实到 " + b[1]);
            }
        }

        // ============================================================ SVG

        [Fact]
        public void 复杂文种导出SVG时按轮廓输出且不残留反查残文()
        {
            string deva = SvgOf(Devanagari);
            Assert.True(LongestPathData(deva) > 60,
                        "天城文 SVG 里没有实质轮廓路径：<path> 数 "
                        + Occurrences(deva, "<path") + "，最长 d 属性 " + LongestPathData(deva));
            Assert.False(deva.IndexOf("<text", StringComparison.Ordinal) >= 0,
                         "天城文 SVG 里仍有 <text> 节点：字形反查会把 \"नमस्ते\" 退化成缺字");
            Assert.False(deva.IndexOf("नमे", StringComparison.Ordinal) >= 0,
                         "SVG 里出现了实测的缺字形态 \"नमे\"（辅音簇被反查丢了）");

            string arabic = SvgOf(Arabic);
            Assert.True(LongestPathData(arabic) > 60, "阿拉伯文 SVG 里没有实质轮廓路径");
            Assert.False(arabic.IndexOf("<text", StringComparison.Ordinal) >= 0,
                         "阿拉伯文 SVG 里仍有 <text> 节点：连写形会被反查成表现形式码位");

            // 正对照：拉丁文仍必须是可检索的 <text>，证明轮廓化只针对复杂文种、没有一刀切。
            string latin = SvgOf("Hello");
            Assert.True(latin.IndexOf("<text", StringComparison.Ordinal) >= 0, "拉丁文 SVG 丢了 <text> 节点");
            Assert.True(latin.IndexOf("Hello", StringComparison.Ordinal) >= 0, "拉丁文 SVG 内容不可检索");
            Assert.False(latin.IndexOf("<path", StringComparison.Ordinal) >= 0,
                         "拉丁文不该被轮廓化（<path> 是复杂文种专用的退路）");
        }

        [Fact]
        public void 阿拉伯文夹数字导出SVG时数字仍是可检索文本且落在最左端()
        {
            string svg = SvgOf("مرحبا 123");
            Match match = Regex.Match(svg, "<text[^>]*?x=\"([^\"]*)\"[^>]*>([^<]*)</text>");
            Assert.True(match.Success, "混合文本的 SVG 里没有数字的 <text> 节点（数字被并进 RTL 轮廓了）");
            Assert.True(match.Groups[2].Value.Trim() == "123",
                        "数字节点的内容应为 \"123\"，实到 \"" + match.Groups[2].Value.Trim() + "\"");

            // 段序反转后数字段排在最左：逐字形 x 列表的第一个值就是起点 10。
            string firstX = match.Groups[1].Value.Split(',')[0].Trim();
            Assert.True(firstX == "10" || firstX.StartsWith("10.", StringComparison.Ordinal),
                        "数字段没有落在最左端（x=" + match.Groups[1].Value + "）：RTL 段序可能没有反转");
        }

        // ============================================================ 公共件

        private static float PainterWidth(string text, FontKey key)
        {
            SKImageInfo info = new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul);
            using (SKSurface? surface = SKSurface.Create(info))
            {
                Assert.NotNull(surface);
                using (Painter painter = new Painter(surface!.Canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    return painter.TextWidth(text, key);
                }
            }
        }

        /// <summary>按候选字族（与库同序）找到第一个真正覆盖该文本的字体；找不到就抛。</summary>
        private static SKTypeface RequireCovering(string script, string text)
        {
            string[] families = ScriptFamilies[script];
            List<string> tried = new List<string>();
            for (int i = 0; i < families.Length; i++)
            {
                tried.Add(families[i]);
                SKTypeface? candidate = SKTypeface.FromFamilyName(families[i], SKFontStyle.Normal);
                if (candidate == null)
                {
                    continue;
                }
                if (string.Equals(candidate.FamilyName, families[i], StringComparison.OrdinalIgnoreCase)
                    && candidate.ContainsGlyphs(text))
                {
                    return candidate;
                }
                candidate.Dispose();
            }

            string[] languages = ScriptLanguages[script];
            SKTypeface? match = SKFontManager.Default.MatchCharacter(
                null, 400, 5, SKFontStyleSlant.Upright, languages, char.ConvertToUtf32(text, 0));
            if (match != null)
            {
                return match;
            }

            throw new InvalidOperationException(
                "本机没有能覆盖 " + script + " 文本的字体（已试：" + string.Join(", ", tried) + "）。"
                + "复杂文种用例需要运行环境装有对应字体的系统字体。");
        }

        /// <summary>测试自己的 HarfBuzz 整形量宽，独立于库实现。</summary>
        private static float ShapeWidth(SKTypeface typeface, string text, out int glyphCount)
        {
            using (SKStreamAsset? stream = typeface.OpenStream(out int index))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("字体流打不开：" + typeface.FamilyName);
                }

                byte[] data = new byte[checked((int)stream.Length)];
                if (stream.Read(data, data.Length) != data.Length)
                {
                    throw new InvalidOperationException("字体流读不完整：" + typeface.FamilyName);
                }

                // 不用 HarfBuzzSharp.Blob.FromStream：它把临时数组 fixed 在自己的方法里，
                // 返回后底层指针仍被 hb_blob_create 引用，GC 移动数组会让 hb_shape_full 崩溃。
                IntPtr native = Marshal.AllocHGlobal(data.Length);
                HarfBuzzSharp.Blob blob;
                try
                {
                    Marshal.Copy(data, 0, native, data.Length);
                    blob = new HarfBuzzSharp.Blob(native, data.Length, HarfBuzzSharp.MemoryMode.Duplicate);
                }
                finally
                {
                    Marshal.FreeHGlobal(native);
                }

                using (blob)
                using (HarfBuzzSharp.Face face = new HarfBuzzSharp.Face(blob, index))
                using (HbFont font = new HbFont(face))
                using (HbBuffer buffer = new HbBuffer())
                {
                    int scale = (int)Math.Round(Size * 64.0);
                    font.SetScale(scale, scale);
                    buffer.AddUtf16(text);
                    buffer.GuessSegmentProperties();
                    font.Shape(buffer);

                    glyphCount = buffer.GlyphInfos.Length;
                    float width = 0f;
                    for (int i = 0; i < buffer.GlyphPositions.Length; i++)
                    {
                        width += buffer.GlyphPositions[i].XAdvance / 64f;
                    }
                    return width;
                }
            }
        }

        private static ImageProbe Render(int width, int height, Action<Painter> draw)
        {
            using (SKPictureRecorder recorder = new SKPictureRecorder())
            {
                SKCanvas recording = recorder.BeginRecording(new SKRect(0f, 0f, width, height));
                using (Painter painter = new Painter(recording, new ChartStyle(), Chart.SharedFonts))
                {
                    draw(painter);
                }

                using (SKPicture? picture = recorder.EndRecording())
                {
                    Assert.NotNull(picture);
                    SKImageInfo info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (SKSurface? surface = SKSurface.Create(info))
                    {
                        Assert.NotNull(surface);
                        SKCanvas canvas = surface!.Canvas;
                        canvas.Clear(SKColors.White);
                        canvas.DrawPicture(picture!);
                        using (SKImage image = surface.Snapshot())
                        using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            return ImageProbe.FromPng(data.ToArray());
                        }
                    }
                }
            }
        }

        /// <summary>窗口内墨迹外接框 {minX,maxX,minY,maxY}；无墨时 min/max 为 -1。</summary>
        private static int[] InkBounds(ImageProbe probe, int left, int top, int right, int bottom)
        {
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    if (!probe.IsInk(x, y))
                    {
                        continue;
                    }
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }
            return minX > maxX ? new[] { -1, -1, -1, -1 } : new[] { minX, maxX, minY, maxY };
        }

        private static string SvgOf(string text)
        {
            using (Chart chart = new Chart(new ChartDocument(), new ScriptTextTemplate(text), new ChartStyle()))
            {
                return ChartExport.ToSvgString(chart);
            }
        }

        private static int LongestPathData(string svg)
        {
            int longest = 0;
            int at = 0;
            while ((at = svg.IndexOf("d=\"", at, StringComparison.Ordinal)) >= 0)
            {
                int end = svg.IndexOf('"', at + 3);
                if (end < 0)
                {
                    break;
                }
                longest = Math.Max(longest, end - (at + 3));
                at = end + 1;
            }
            return longest;
        }

        private static int Occurrences(string text, string needle)
        {
            int count = 0;
            int at = 0;
            while ((at = text.IndexOf(needle, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += needle.Length;
            }
            return count;
        }

        private static void AssertClose(float actual, float expected, float tolerance, string label)
        {
            Assert.True(Math.Abs(actual - expected) <= tolerance,
                        label + "：期望 " + expected.ToString("0.###") + " ± "
                        + tolerance.ToString("0.###") + "，实到 " + actual.ToString("0.###"));
        }

        /// <summary>只落一段文字的试版，用来走真实的 Chart → SVG 导出链。</summary>
        private sealed class ScriptTextTemplate : IChartTemplate
        {
            private const float CanvasWidth = 240f;
            private const float CanvasHeight = 60f;

            private readonly string _text;

            public ScriptTextTemplate(string text)
            {
                _text = text;
            }

            public string Key => "test.complex-script.text";

            public string DisplayName => "复杂文种落字试版";

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                SKRect bounds = new SKRect(0f, 0f, CanvasWidth, CanvasHeight);
                return new ChartGeometry(
                    bounds,
                    new SlotAxis(0f, 10f, 1, 1),
                    new LinearAxis("rule", 0d, 10d, 0f, CanvasHeight, 1d, 5d),
                    new LinearAxis[0],
                    new Band("header", new SKRect(0f, 0f, CanvasWidth, 2f), new BandRow[0]),
                    new Band("date", new SKRect(0f, 2f, CanvasWidth, 4f), new BandRow[0]),
                    bounds,
                    new LabelColumn[0],
                    new Band("cells", bounds, new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, CanvasHeight - 2f, CanvasWidth, CanvasHeight), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                painter.Text(_text, 10f, 40f, AutoKey, SKColors.Black);
            }
        }
    }
}
