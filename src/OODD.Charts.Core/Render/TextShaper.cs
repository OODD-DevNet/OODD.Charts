using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HarfBuzzSharp;
using SkiaSharp;
using HbBuffer = HarfBuzzSharp.Buffer;
using HbFont = HarfBuzzSharp.Font;

namespace OODD.Charts.Render
{
    /// <summary>矢量导出的落字方式。<see cref="Outlines"/> 只对需要整形的文种生效。</summary>
    internal enum TextPaintMode
    {
        /// <summary>按字形编号落字（光栅与 PDF 用）。</summary>
        Text,

        /// <summary>
        /// 按轮廓落笔（SVG 用）。Skia 的 SVG 序列化会把字形编号反查回 Unicode，
        /// 天城文这类"多个码位合成一个字形"的文字会被反查成缺字（实测 "नमस्ते" → "नमे"），
        /// 所以复杂文种在 SVG 里只能走轮廓。
        /// </summary>
        Outlines,
    }

    /// <summary>
    /// 复杂文种整形。Skia 只做码位→字形的一一映射，阿拉伯文的连写、天城文的辅音簇重排
    /// 都不会发生（"مرحبا" 逐字直排会得到孤立形，宽 66.1px；整形后为连写形，宽 52.1px）。
    /// 这里用 HarfBuzzSharp 补上 OpenType 整形，并负责三件事：
    /// ① 按 Unicode 脚本切段，普通文种仍走原来的 <c>DrawText</c>，行为与像素基线不变；
    /// ② 基础字体缺该文种字形时按脚本换字体（中文表单里排阿拉伯语不会落成豆腐块）；
    /// ③ 量宽与落字共用同一份整形结果，排版宽度不会和实际墨迹脱节。
    /// </summary>
    internal sealed class TextShaper : IDisposable
    {
        /// <summary>需要整形的脚本标签（HarfBuzz 的 ISO 15924 四字母标签）。</summary>
        private static readonly HashSet<string> ComplexScripts = new HashSet<string>(StringComparer.Ordinal)
        {
            "Arab", "Hebr", "Syrc", "Thaa", "Nkoo", "Samr", "Mand", "Adlm",
            "Deva", "Beng", "Guru", "Gujr", "Orya", "Taml", "Telu", "Knda", "Mlym", "Sinh",
            "Thai", "Laoo", "Tibt", "Mymr", "Khmr", "Mong", "Ethi",
            "Tale", "Lana", "Java", "Bali", "Cham", "Tglg", "Bugi", "Batk", "Kali",
            "Mtei", "Lepc", "Saur", "Phag", "Cakm", "Brah", "Gran", "Shrd", "Khoj",
            "Sind", "Tirh", "Modi", "Takr", "Sidd", "Marc", "Newa",
        };

        /// <summary>按脚本给出的候选字族。名字只是运行时向系统要字体，库不打包任何字体文件。</summary>
        private static readonly Dictionary<string, string[]> ScriptFamilies =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "Arab", new[] { "Segoe UI", "Arial", "Tahoma", "Noto Naskh Arabic", "Noto Sans Arabic", "Dubai", "Geeza Pro" } },
                { "Hebr", new[] { "Segoe UI", "Arial", "Noto Sans Hebrew", "David", "Arial Hebrew" } },
                { "Syrc", new[] { "Segoe UI", "Noto Sans Syriac", "Estrangelo Edessa" } },
                { "Thaa", new[] { "MV Boli", "Noto Sans Thaana", "Segoe UI" } },
                { "Nkoo", new[] { "Ebrima", "Noto Sans NKo" } },
                { "Deva", new[] { "Nirmala UI", "Mangal", "Noto Sans Devanagari", "Kohinoor Devanagari" } },
                { "Beng", new[] { "Nirmala UI", "Vrinda", "Noto Sans Bengali", "Bangla Sangam MN" } },
                { "Guru", new[] { "Nirmala UI", "Raavi", "Noto Sans Gurmukhi" } },
                { "Gujr", new[] { "Nirmala UI", "Shruti", "Noto Sans Gujarati" } },
                { "Orya", new[] { "Nirmala UI", "Kalinga", "Noto Sans Oriya" } },
                { "Taml", new[] { "Nirmala UI", "Latha", "Noto Sans Tamil" } },
                { "Telu", new[] { "Nirmala UI", "Gautami", "Noto Sans Telugu" } },
                { "Knda", new[] { "Nirmala UI", "Tunga", "Noto Sans Kannada" } },
                { "Mlym", new[] { "Nirmala UI", "Kartika", "Noto Sans Malayalam" } },
                { "Sinh", new[] { "Nirmala UI", "Iskoola Pota", "Noto Sans Sinhala" } },
                { "Thai", new[] { "Leelawadee UI", "Tahoma", "Noto Sans Thai", "Thonburi" } },
                { "Laoo", new[] { "Leelawadee UI", "Noto Sans Lao", "Lao Sangam MN" } },
                { "Tibt", new[] { "Microsoft Himalaya", "Noto Serif Tibetan", "Kailasa" } },
                { "Mymr", new[] { "Myanmar Text", "Noto Sans Myanmar" } },
                { "Khmr", new[] { "Khmer UI", "Noto Sans Khmer" } },
                { "Mong", new[] { "Mongolian Baiti", "Noto Sans Mongolian" } },
                { "Ethi", new[] { "Ebrima", "Nyala", "Noto Sans Ethiopic" } },
            };

        private static readonly Dictionary<string, string[]> ScriptLanguages =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "Arab", new[] { "ar" } },
                { "Hebr", new[] { "he" } },
                { "Syrc", new[] { "syr" } },
                { "Thaa", new[] { "dv" } },
                { "Nkoo", new[] { "nqo" } },
                { "Deva", new[] { "hi", "mr", "ne" } },
                { "Beng", new[] { "bn" } },
                { "Guru", new[] { "pa" } },
                { "Gujr", new[] { "gu" } },
                { "Orya", new[] { "or" } },
                { "Taml", new[] { "ta" } },
                { "Telu", new[] { "te" } },
                { "Knda", new[] { "kn" } },
                { "Mlym", new[] { "ml" } },
                { "Sinh", new[] { "si" } },
                { "Thai", new[] { "th" } },
                { "Laoo", new[] { "lo" } },
                { "Tibt", new[] { "bo" } },
                { "Mymr", new[] { "my" } },
                { "Khmr", new[] { "km" } },
                { "Mong", new[] { "mn" } },
                { "Ethi", new[] { "am" } },
            };

        /// <summary>
        /// 脚本字体缓存。字体解析器只认 <see cref="Fonts.FontKey"/>，不知道文本要用哪种文字；
        /// 按"基础字族 + 粗细 + 脚本"存一份，同一进程里所有图表共用。缓存项不释放：
        /// 它可能正被别的 Painter 使用，而 SKTypeface 只是 Skia 引用计数的一个包装。
        /// </summary>
        private static readonly Dictionary<string, SKTypeface> ScriptTypefaces =
            new Dictionary<string, SKTypeface>(StringComparer.Ordinal);

        private static readonly object ScriptTypefaceSync = new object();

        private readonly Func<Fonts.FontKey, SKFont>? _fontOf;
        private readonly Fonts.IFontResolver? _resolver;
        private readonly Dictionary<Fonts.FontKey, SKFont> _ownFonts = new Dictionary<Fonts.FontKey, SKFont>();
        private readonly Dictionary<string, SKFont> _scriptFonts = new Dictionary<string, SKFont>(StringComparer.Ordinal);
        private readonly Dictionary<SKFont, ShapedFont> _hbFonts = new Dictionary<SKFont, ShapedFont>();
        private readonly Dictionary<string, TextLayout> _layouts = new Dictionary<string, TextLayout>(StringComparer.Ordinal);

        /// <summary>由承载它的 <see cref="Painter"/> 提供已有字体，避免同一 <see cref="Fonts.FontKey"/> 建两份。</summary>
        public TextShaper(Func<Fonts.FontKey, SKFont> fontOf)
        {
            _fontOf = fontOf ?? throw new ArgumentNullException(nameof(fontOf));
        }

        /// <summary>独立量宽用（没有 Painter 的静态路径）。</summary>
        public TextShaper(Fonts.IFontResolver resolver)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        }

        // ---------------------------------------------------------------- 对外

        /// <summary>
        /// 文本里是否含需要整形的字符。先按码位区间快筛，避免给每个中文标签都调用一次原生脚本查询。
        /// </summary>
        public static bool NeedsShaping(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            for (int i = 0; i < text!.Length; i++)
            {
                if (IsComplexChar(text[i]))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>基线起点方向：首个强方向脚本是 RTL 时对齐要以右为起始边。</summary>
        public static bool IsRightToLeft(string? text)
        {
            if (!NeedsShaping(text))
            {
                return false;
            }

            List<Run> runs = SplitRuns(text!);
            for (int i = 0; i < runs.Count; i++)
            {
                // 数字恒为 LTR 段，不能拿它当段落方向（"١٢٣ مرحبا" 的段落方向仍是 RTL）。
                if (!runs[i].IsDigit && !IsCommonScript(runs[i].Script))
                {
                    return runs[i].RightToLeft;
                }
            }
            return false;
        }

        public float Measure(string text, Fonts.FontKey key)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }
            if (!NeedsShaping(text))
            {
                return FontOf(key).MeasureText(text);
            }
            return GetLayout(text, key).Width;
        }

        public void Draw(SKCanvas canvas, string text, float x, float y, Fonts.FontKey key, SKPaint paint, TextPaintMode mode)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (!NeedsShaping(text))
            {
                canvas.DrawText(text, x, y, FontOf(key), paint);
                return;
            }

            TextLayout layout = GetLayout(text, key);
            float pen = 0f;
            for (int i = 0; i < layout.Segments.Length; i++)
            {
                Segment segment = layout.Segments[i];
                if (!segment.Complex)
                {
                    // 同一段里夹着的数字/拉丁仍按原路径落字：SVG 里还是可检索的 <text>，
                    // 光栅侧的栅格缓存行为也与整形前一致。
                    canvas.DrawText(segment.Text, x + pen, y, segment.Font, paint);
                }
                else if (mode == TextPaintMode.Outlines)
                {
                    DrawOutlines(canvas, segment, x + pen, y, paint);
                }
                else
                {
                    canvas.DrawText(BlobOf(segment), x + pen, y, paint);
                }
                pen += segment.Advance;
            }
        }

        // ---------------------------------------------------------------- 排版

        private TextLayout GetLayout(string text, Fonts.FontKey key)
        {
            string cacheKey = LayoutCacheKey(key, text);
            TextLayout? cached;
            if (_layouts.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            TextLayout layout = BuildLayout(text, key);
            _layouts[cacheKey] = layout;
            return layout;
        }

        private TextLayout BuildLayout(string text, Fonts.FontKey key)
        {
            SKFont baseFont = FontOf(key);
            List<Run> runs = SplitRuns(text);
            List<Segment> segments = new List<Segment>(runs.Count);
            bool rightToLeft = false;
            bool sawStrong = false;

            for (int i = 0; i < runs.Count; i++)
            {
                Run run = runs[i];
                Segment segment = new Segment { Text = run.Text, Script = run.Script, RightToLeft = run.RightToLeft };
                if (!sawStrong && !run.IsDigit && !IsCommonScript(run.Script))
                {
                    rightToLeft = run.RightToLeft;
                    sawStrong = true;
                }

                if (!ComplexScripts.Contains(run.Script))
                {
                    segment.Font = baseFont;
                    segment.Advance = baseFont.MeasureText(run.Text);
                }
                else
                {
                    segment.Font = ResolveScriptFont(run, key, baseFont);
                    ShapedFont? shaper = ShapedFontOf(segment.Font);
                    if (shaper == null || !Shape(shaper, segment))
                    {
                        // 字体流打不开（例如某些平台给不出 SKStreamAsset）时不假装整形：
                        // 退回逐字直排，宁可少连写也不要宽度和墨迹各算一套。
                        segment.Complex = false;
                        segment.Advance = segment.Font.MeasureText(run.Text);
                    }
                    else
                    {
                        segment.Complex = true;
                    }
                }
                segments.Add(segment);
            }

            if (rightToLeft)
            {
                segments.Reverse();
            }

            float width = 0f;
            for (int i = 0; i < segments.Count; i++)
            {
                width += segments[i].Advance;
            }

            return new TextLayout(segments.ToArray(), width, rightToLeft);
        }

        private bool Shape(ShapedFont shaper, Segment segment)
        {
            using (HbBuffer buffer = new HbBuffer())
            {
                buffer.AddUtf16(segment.Text);
                buffer.GuessSegmentProperties();
                buffer.Direction = segment.RightToLeft ? Direction.RightToLeft : Direction.LeftToRight;
                buffer.Script = Script.Parse(segment.Script);
                shaper.Font.Shape(buffer);

                GlyphInfo[] infos = buffer.GlyphInfos;
                GlyphPosition[] positions = buffer.GlyphPositions;
                ushort[] glyphs = new ushort[infos.Length];
                SKPoint[] points = new SKPoint[infos.Length];
                float pen = 0f;
                for (int i = 0; i < infos.Length; i++)
                {
                    glyphs[i] = (ushort)infos[i].Codepoint;
                    // HarfBuzz 的 y 轴向上，Skia 向下；x/y offset 与 advance 都是 26.6 定点。
                    points[i] = new SKPoint(pen + positions[i].XOffset / 64f, -positions[i].YOffset / 64f);
                    pen += positions[i].XAdvance / 64f;
                }

                segment.Glyphs = glyphs;
                segment.Positions = points;
                segment.Advance = pen;
                return true;
            }
        }

        // ---------------------------------------------------------------- 分段

        private static List<Run> SplitRuns(string text)
        {
            List<Run> runs = new List<Run>();
            int start = 0;
            string? current = null;
            bool rightToLeft = false;
            bool isDigit = false;

            for (int i = 0; i < text.Length;)
            {
                int codePoint = char.ConvertToUtf32(text, i);
                int width = char.IsHighSurrogate(text[i]) ? 2 : 1;
                string script = UnicodeFunctions.Default.GetScript(codePoint).ToString();

                // 数字不参与整形的方向处理：HarfBuzz 只按给定方向反转整段字形，
                // "مرحبا 123" 若把数字并进 RTL 段会画成 "321"。这里按段切出来，
                // 由段序反转把它放到正确的视觉位置，段内保持 LTR（UAX#9 的嵌入方向）。
                bool digit = IsDigit(codePoint);
                if (!digit && IsCommonScript(script))
                {
                    // 空格/标点跟着当前段走，不单独起段（否则字体切换会改变空格宽度）。
                    i += width;
                    continue;
                }

                bool runRightToLeft = !digit && IsRightToLeftScript(script);
                if (current == null)
                {
                    current = script;
                    rightToLeft = runRightToLeft;
                    isDigit = digit;
                }
                else if (!string.Equals(script, current, StringComparison.Ordinal) || rightToLeft != runRightToLeft)
                {
                    runs.Add(new Run(text.Substring(start, i - start), current, rightToLeft, isDigit));
                    start = i;
                    current = script;
                    rightToLeft = runRightToLeft;
                    isDigit = digit;
                }
                i += width;
            }

            if (start < text.Length)
            {
                runs.Add(new Run(text.Substring(start), current ?? "Zyyy", rightToLeft, isDigit));
            }
            return runs;
        }

        private static bool IsDigit(int codePoint)
        {
            return codePoint <= 0xFFFF && char.IsDigit((char)codePoint);
        }

        private static bool IsRightToLeftScript(string script)
        {
            return Script.Parse(script).HorizontalDirection == Direction.RightToLeft;
        }

        private static bool IsCommonScript(string script)
        {
            return string.Equals(script, "Zyyy", StringComparison.Ordinal)
                || string.Equals(script, "Zinh", StringComparison.Ordinal)
                || string.Equals(script, "Zzzz", StringComparison.Ordinal);
        }

        private static bool IsComplexChar(char c)
        {
            // 只圈需要整形的区块；CJK/拉丁/希腊/西里尔不在其中，所以既有版面不会误入整形路径。
            return (c >= '\u0590' && c <= '\u08FF')     // 希伯来、阿拉伯、叙利亚、它拿、NKo、曼达
                || (c >= '\u0900' && c <= '\u0DFF')     // 天城文至僧伽罗
                || (c >= '\u0E00' && c <= '\u0EFF')     // 泰文、老挝文
                || (c >= '\u0F00' && c <= '\u109F')     // 藏文、缅甸文
                || (c >= '\u1780' && c <= '\u18AF')     // 高棉文、蒙古文
                || (c >= '\u1A20' && c <= '\u1B7F')     // 傣仂文、巴厘文
                || (c >= '\u1CD0' && c <= '\u1CFF')     // 吠陀附加符
                || (c >= '\uA800' && c <= '\uAB6F')     // 天城扩展、爪哇文、占文等
                || (c >= '\uFB1D' && c <= '\uFDFF')     // 希伯来/阿拉伯表现形式 A
                || (c >= '\uFE70' && c <= '\uFEFF');    // 阿拉伯表现形式 B
        }

        // ---------------------------------------------------------------- 字体

        private SKFont FontOf(Fonts.FontKey key)
        {
            if (_fontOf != null)
            {
                return _fontOf(key);
            }

            SKFont? font;
            if (!_ownFonts.TryGetValue(key, out font))
            {
                font = new SKFont(_resolver!.Resolve(key), key.Size);
                _ownFonts[key] = font;
            }
            return font;
        }

        private SKFont ResolveScriptFont(Run run, Fonts.FontKey key, SKFont baseFont)
        {
            if (Covers(baseFont.Typeface, run.Text))
            {
                return baseFont;
            }

            SKTypeface? typeface = ResolveScriptTypeface(baseFont.Typeface, run.Script, key.Bold, run.Text);
            if (typeface == null)
            {
                return baseFont;
            }

            string cacheKey = run.Script + "|" + (key.Bold ? "b" : "n") + "|" + baseFont.Size.ToString("R", CultureInfo.InvariantCulture);
            SKFont? font;
            if (!_scriptFonts.TryGetValue(cacheKey, out font))
            {
                font = new SKFont(typeface, baseFont.Size);
                _scriptFonts[cacheKey] = font;
            }
            return font;
        }

        private static SKTypeface? ResolveScriptTypeface(SKTypeface baseTypeface, string script, bool bold, string text)
        {
            string cacheKey = baseTypeface.FamilyName + "|" + (bold ? "b" : "n") + "|" + script;
            lock (ScriptTypefaceSync)
            {
                SKTypeface? cached;
                if (ScriptTypefaces.TryGetValue(cacheKey, out cached) && Covers(cached, text))
                {
                    return cached;
                }
            }

            SKFontStyle style = bold ? SKFontStyle.Bold : SKFontStyle.Normal;
            string[]? families;
            if (ScriptFamilies.TryGetValue(script, out families))
            {
                for (int i = 0; i < families.Length; i++)
                {
                    SKTypeface? candidate = SKTypeface.FromFamilyName(families[i], style);
                    if (candidate == null)
                    {
                        continue;
                    }
                    if (!string.Equals(candidate.FamilyName, families[i], StringComparison.OrdinalIgnoreCase)
                        || !Covers(candidate, text))
                    {
                        candidate.Dispose();
                        continue;
                    }
                    Remember(cacheKey, candidate);
                    return candidate;
                }
            }

            int codePoint = FirstCoverableCodePoint(text);
            if (codePoint > 0)
            {
                string[]? languages;
                ScriptLanguages.TryGetValue(script, out languages);
                SKTypeface? match = SKFontManager.Default.MatchCharacter(
                    null, bold ? 700 : 400, 5, SKFontStyleSlant.Upright, languages, codePoint);
                if (match != null)
                {
                    if (Covers(match, text))
                    {
                        Remember(cacheKey, match);
                        return match;
                    }
                    match.Dispose();
                }
            }
            return null;
        }

        private static void Remember(string cacheKey, SKTypeface typeface)
        {
            lock (ScriptTypefaceSync)
            {
                ScriptTypefaces[cacheKey] = typeface;
            }
        }

        private static bool Covers(SKTypeface typeface, string text)
        {
            StringBuilder probe = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]) && !char.IsControl(text[i]))
                {
                    probe.Append(text[i]);
                }
            }
            return probe.Length == 0 || typeface.ContainsGlyphs(probe.ToString());
        }

        private static int FirstCoverableCodePoint(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]) && !char.IsControl(text[i]))
                {
                    return char.ConvertToUtf32(text, i);
                }
            }
            return 0;
        }

        // ---------------------------------------------------------------- HarfBuzz 资源

        private ShapedFont? ShapedFontOf(SKFont font)
        {
            ShapedFont? shaper;
            if (_hbFonts.TryGetValue(font, out shaper))
            {
                return shaper;
            }

            shaper = ShapedFont.Create(font);
            if (shaper == null)
            {
                return null;
            }
            _hbFonts[font] = shaper;
            return shaper;
        }

        // ---------------------------------------------------------------- 落笔

        private static SKTextBlob BlobOf(Segment segment)
        {
            if (segment.Blob == null)
            {
                using (SKTextBlobBuilder builder = new SKTextBlobBuilder())
                {
                    builder.AddPositionedRun(segment.Glyphs!, segment.Font, segment.Positions!);
                    segment.Blob = builder.Build()!;
                }
            }
            return segment.Blob;
        }

        private static void DrawOutlines(SKCanvas canvas, Segment segment, float x, float y, SKPaint paint)
        {
            using (SKPath path = new SKPath())
            {
                for (int i = 0; i < segment.Glyphs!.Length; i++)
                {
                    using (SKPath? glyph = segment.Font.GetGlyphPath(segment.Glyphs[i]))
                    {
                        if (glyph == null || glyph.IsEmpty)
                        {
                            continue;
                        }
                        glyph.Transform(SKMatrix.CreateTranslation(
                            x + segment.Positions![i].X, y + segment.Positions[i].Y));
                        path.AddPath(glyph);
                    }
                }
                if (!path.IsEmpty)
                {
                    canvas.DrawPath(path, paint);
                }
            }
        }

        private static string LayoutCacheKey(Fonts.FontKey key, string text)
        {
            return (key.Family ?? "\u0000") + "\u0001"
                 + key.Size.ToString("R", CultureInfo.InvariantCulture) + "\u0001"
                 + (key.Bold ? "b" : "n") + "\u0002" + text;
        }

        // ---------------------------------------------------------------- 释放

        public void Dispose()
        {
            foreach (TextLayout layout in _layouts.Values)
            {
                layout.Dispose();
            }
            _layouts.Clear();

            foreach (ShapedFont shaper in _hbFonts.Values)
            {
                shaper.Dispose();
            }
            _hbFonts.Clear();

            foreach (SKFont font in _scriptFonts.Values)
            {
                font.Dispose();
            }
            _scriptFonts.Clear();

            foreach (SKFont font in _ownFonts.Values)
            {
                font.Dispose();
            }
            _ownFonts.Clear();
        }

        // ---------------------------------------------------------------- 结构

        private struct Run
        {
            public Run(string text, string script, bool rightToLeft, bool isDigit)
            {
                Text = text;
                Script = script;
                RightToLeft = rightToLeft;
                IsDigit = isDigit;
            }

            public string Text { get; }

            public string Script { get; }

            public bool RightToLeft { get; }

            /// <summary>独立出来的数字段：方向恒为 LTR，不作为段落方向依据。</summary>
            public bool IsDigit { get; }
        }

        private sealed class Segment : IDisposable
        {
            public string Text = string.Empty;

            public string Script = "Zyyy";

            public bool RightToLeft;

            public bool Complex;

            public SKFont Font = null!;

            public float Advance;

            public ushort[]? Glyphs;

            public SKPoint[]? Positions;

            public SKTextBlob? Blob;

            public void Dispose()
            {
                if (Blob != null)
                {
                    Blob.Dispose();
                    Blob = null;
                }
            }
        }

        private sealed class TextLayout : IDisposable
        {
            public TextLayout(Segment[] segments, float width, bool rightToLeft)
            {
                Segments = segments;
                Width = width;
                RightToLeft = rightToLeft;
            }

            public Segment[] Segments { get; }

            public float Width { get; }

            public bool RightToLeft { get; }

            public void Dispose()
            {
                for (int i = 0; i < Segments.Length; i++)
                {
                    Segments[i].Dispose();
                }
            }
        }

        /// <summary>一个 <see cref="SKFont"/> 对应的 HarfBuzz 面与字体；字体流要活到面释放为止。</summary>
        private sealed class ShapedFont : IDisposable
        {
            private readonly MemoryStream _stream;
            private readonly Blob _blob;
            private readonly Face _face;

            private ShapedFont(MemoryStream stream, Blob blob, Face face, HbFont font)
            {
                _stream = stream;
                _blob = blob;
                _face = face;
                Font = font;
            }

            public HbFont Font { get; }

            public static ShapedFont? Create(SKFont font)
            {
                using (SKStreamAsset? stream = font.Typeface.OpenStream(out int index))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    long length = stream.Length;
                    if (length <= 0 || length > int.MaxValue)
                    {
                        return null;
                    }

                    byte[] data = new byte[length];
                    stream.Read(data, (int)length);

                    MemoryStream memory = new MemoryStream(data, false);
                    Blob blob = Blob.FromStream(memory);
                    Face face = new Face(blob, index);
                    HbFont hbFont = new HbFont(face);
                    int scale = (int)Math.Round(font.Size * 64.0);
                    hbFont.SetScale(scale, scale);
                    return new ShapedFont(memory, blob, face, hbFont);
                }
            }

            public void Dispose()
            {
                Font.Dispose();
                _face.Dispose();
                _blob.Dispose();
                _stream.Dispose();
            }
        }
    }
}
