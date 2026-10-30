using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Tickets.Vat
{
    /// <summary>
    /// 价税合计的中文大写"压印"排版：把整串大写金额压进一个固定宽度的单元格，
    /// 字号按可用宽度与单字宽度等比缩小，逐字居中落在自己的字格内。
    /// <para>
    /// 之所以要单独做：大写金额（如"壹佰贰拾叁万肆仟伍佰陆拾柒元捌角玖分"）比小写长得多，
    /// 若直接按名义字号落笔会越过单元格边线压到右侧"（小写）"格上。这里的约束是
    /// <b>字格序列整体落在单元格内、相邻字格不重叠</b>，字号不够时就缩字号，而不是让字压出去。
    /// </para>
    /// <para>
    /// <b>D22</b>：字号、留白、缩字规则都是结构规则，未按发票票样校准；
    /// "压印"只是版面上把长文本压进固定格的说法，不代表任何印刷工艺。
    /// </para>
    /// </summary>
    public sealed class PressedAmount
    {
        private readonly FontKey _baseKey;
        private readonly float _charInsetFactor;

        private PressedAmount(string text, SKRect box, FontKey baseKey, float baseSize, float fontSize,
            bool compressed, bool belowMinimum, float naturalWidth, float availableWidth, float pitch,
            float baseline, IReadOnlyList<SKRect> charCells, float charInsetFactor)
        {
            Text = text;
            Box = box;
            _baseKey = baseKey;
            BaseSize = baseSize;
            FontSize = fontSize;
            Compressed = compressed;
            BelowMinimum = belowMinimum;
            NaturalWidth = naturalWidth;
            AvailableWidth = availableWidth;
            Pitch = pitch;
            Baseline = baseline;
            CharCells = charCells;
            _charInsetFactor = charInsetFactor;
        }

        /// <summary>要压印的文字。</summary>
        public string Text { get; }

        /// <summary>压印单元格（绘制与几何断言的外框）。</summary>
        public SKRect Box { get; }

        /// <summary>名义字号（令牌给的字号）。</summary>
        public float BaseSize { get; }

        /// <summary>实际落笔字号。</summary>
        public float FontSize { get; }

        /// <summary>是否发生了压缩（实际字号小于名义字号）。</summary>
        public bool Compressed { get; }

        /// <summary>可用宽度即使压到令牌下限仍不够时为 true；此时按"不越界"优先继续缩字。</summary>
        public bool BelowMinimum { get; }

        /// <summary>名义字号下的整串宽度。</summary>
        public float NaturalWidth { get; }

        /// <summary>单元格内可用于落字的宽度（已扣左右留白）。</summary>
        public float AvailableWidth { get; }

        /// <summary>单字字格宽度（= 可用宽度 / 字数）。</summary>
        public float Pitch { get; }

        /// <summary>基线 y（所有字共用）。</summary>
        public float Baseline { get; }

        /// <summary>逐字字格，长度 = 文字长度。</summary>
        public IReadOnlyList<SKRect> CharCells { get; }

        /// <summary>字格序列左缘。</summary>
        public float InkLeft
        {
            get { return CharCells.Count == 0 ? Box.Left : CharCells[0].Left; }
        }

        /// <summary>字格序列右缘；保证不大于 <see cref="Box"/> 的右缘。</summary>
        public float InkRight
        {
            get { return CharCells.Count == 0 ? Box.Left : CharCells[CharCells.Count - 1].Right; }
        }

        /// <summary>
        /// 求解压印排版。
        /// </summary>
        /// <param name="text">文字；null/空时返回零字格的空排版。</param>
        /// <param name="box">单元格矩形。</param>
        /// <param name="fonts">字体解析器（与 <see cref="Painter"/> 同一套字体）。</param>
        /// <param name="baseKey">名义字体键（字号取 <see cref="FontKey.Size"/>，粗细沿用）。</param>
        /// <param name="minSize">字号下限建议值；即使触底也以"不越界"优先（见 <see cref="BelowMinimum"/>）。</param>
        /// <param name="horizontalPadding">单元格左右留白。</param>
        /// <param name="verticalPadding">单元格上下留白。</param>
        /// <param name="charInsetFactor">单字格内再留的相对间隙（相对字格宽），用于避免相邻字贴边。</param>
        public static PressedAmount Layout(string text, SKRect box, IFontResolver fonts, FontKey baseKey,
            float minSize, float horizontalPadding, float verticalPadding, float charInsetFactor = 0.06f)
        {
            if (fonts == null)
            {
                throw new ArgumentNullException(nameof(fonts));
            }
            if (!(box.Width > 0f) || !(box.Height > 0f))
            {
                throw new ArgumentException("压印单元格宽高必须大于 0。", nameof(box));
            }
            if (minSize < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(minSize), "字号下限不能为负。");
            }

            string value = text ?? string.Empty;
            float left = box.Left + horizontalPadding;
            float right = box.Right - horizontalPadding;
            float available = right - left;
            if (!(available > 0f) || value.Length == 0)
            {
                return new PressedAmount(value, box, baseKey, baseKey.Size, 0f, false, false,
                    0f, Math.Max(0f, available), 0f, box.MidY, new SKRect[0], charInsetFactor);
            }

            float natural = Primitives.MeasureText(fonts, value, baseKey);
            float pitch = available / value.Length;

            float size = baseKey.Size;
            if (natural > available && natural > 0f)
            {
                size = baseKey.Size * (available / natural);
            }

            // 单字宽度也要压进字格，否则相邻字会互相压住。
            float widest = 0f;
            for (int i = 0; i < value.Length; i++)
            {
                float width = Primitives.MeasureText(fonts, char.ToString(value[i]), baseKey);
                if (width > widest)
                {
                    widest = width;
                }
            }
            if (widest > 0f && widest > pitch)
            {
                size = Math.Min(size, baseKey.Size * (pitch / widest));
            }

            // 纵向也要装得下，避免压出上下边线。
            size = Math.Min(size, box.Height - 2f * verticalPadding);

            // 令牌下限只是建议：一旦触底，仍以"不越界、不压邻格"优先继续缩字，
            // 由 BelowMinimum 把这次让步暴露给调用方可断言。
            bool belowMinimum = size < minSize;
            if (size < 0.5f)
            {
                size = 0.5f;
            }

            bool compressed = size < baseKey.Size - 0.0001f;
            FontKey actualKey = new FontKey(baseKey.Family, size, baseKey.Bold);

            float baseline;
            using (SKFont font = new SKFont(fonts.Resolve(actualKey), actualKey.Size))
            {
                SKFontMetrics metrics = font.Metrics;
                baseline = box.MidY - (metrics.Ascent + metrics.Descent) * 0.5f;
            }

            SKRect[] cells = new SKRect[value.Length];
            for (int i = 0; i < value.Length; i++)
            {
                cells[i] = new SKRect(left + i * pitch, box.Top + verticalPadding,
                    left + (i + 1) * pitch, box.Bottom - verticalPadding);
            }

            return new PressedAmount(value, box, baseKey, baseKey.Size, size, compressed, belowMinimum,
                natural, available, pitch, baseline, cells, charInsetFactor);
        }

        /// <summary>逐字落笔。每个字在自己的字格内居中并横向收一点，保证不越过格边。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="color">文字色。</param>
        public void Draw(Painter painter, SKColor color)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (Text.Length == 0 || FontSize <= 0f)
            {
                return;
            }

            FontKey key = new FontKey(_baseKey.Family, FontSize, _baseKey.Bold);
            float inset = Pitch * _charInsetFactor;

            painter.PushClipRect(Box);
            try
            {
                for (int i = 0; i < Text.Length; i++)
                {
                    char ch = Text[i];
                    if (char.IsWhiteSpace(ch))
                    {
                        continue;
                    }
                    string glyph = char.ToString(ch);
                    float width = painter.TextWidth(glyph, key);
                    float x = CharCells[i].MidX - width * 0.5f;
                    float cellLeft = CharCells[i].Left + inset;
                    float cellRight = CharCells[i].Right - inset;
                    if (x < cellLeft)
                    {
                        x = cellLeft;
                    }
                    else if (x + width > cellRight)
                    {
                        x = cellRight - width;
                    }
                    painter.Text(glyph, x, Baseline, key, color);
                }
            }
            finally
            {
                painter.Pop();
            }
        }

        /// <summary>逐格文字，便于日志与断言（每个字格一项）。</summary>
        public IReadOnlyList<string> Glyphs
        {
            get
            {
                string[] glyphs = new string[Text.Length];
                for (int i = 0; i < Text.Length; i++)
                {
                    glyphs[i] = char.ToString(Text[i]);
                }
                return glyphs;
            }
        }

        /// <summary>返回压印的文字与字号，便于日志。</summary>
        public override string ToString()
        {
            return "PressedAmount(\"" + Text + "\", " + FontSize + "px)";
        }
    }
}
