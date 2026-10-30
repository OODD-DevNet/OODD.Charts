using System;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 两点 + 两端符号 + 连线的复合事件符号（麻醉记录单的 ∨∨—∨∨ 这类）。
    /// 两端点各落在各自轴上，所以不能用单点 Marker 简化。
    /// </summary>
    /// <remarks>
    /// <para>
    /// WS 329—2024 附录 B 的成对连线符号本质是"两个时刻、两个值"的一次表达：
    /// 左端是上一时刻的值，右端是本时刻的值，中间一道连线。因此这里必须收两个端点，
    /// 而不是一个中心加半径。
    /// </para>
    /// <para>
    /// 锚点语义（<see cref="Draw(Painter,SKPoint,float,SKColor)"/>）：anchor 是整对符号的中心，
    /// 画一对水平符号，两端点分列 anchor 左右 <see cref="PairHalfSpanFactor"/>×size。
    /// </para>
    /// </remarks>
    public sealed class PairedEventGlyph : IGlyph
    {
        private readonly float _symbolGapFactor;

        /// <summary>构造复合事件符号。</summary>
        /// <param name="startSymbol">左端（较早时刻）的端点符号。</param>
        /// <param name="endSymbol">右端（较晚时刻）的端点符号。</param>
        /// <param name="connectWithLine">两端之间是否画连线。</param>
        /// <param name="symbolGapFactor">连线让位因子：连线从两端符号外缘断开，
        /// 每端让出 符号半径 × 本因子 的距离，避免连线穿过 ∩/∨ 的尖。必须为正。</param>
        public PairedEventGlyph(MarkerSymbol startSymbol, MarkerSymbol endSymbol, bool connectWithLine,
                                float symbolGapFactor = 2.2f)
        {
            if (symbolGapFactor <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(symbolGapFactor), "让位因子必须为正。");
            }

            StartSymbol = startSymbol;
            EndSymbol = endSymbol;
            ConnectWithLine = connectWithLine;
            _symbolGapFactor = symbolGapFactor;
        }

        /// <summary>左端点符号。</summary>
        public MarkerSymbol StartSymbol { get; }

        /// <summary>右端点符号。</summary>
        public MarkerSymbol EndSymbol { get; }

        /// <summary>两端之间是否画连线。</summary>
        public bool ConnectWithLine { get; }

        /// <summary>连线让位因子（相对符号半径）。</summary>
        public float SymbolGapFactor { get { return _symbolGapFactor; } }

        /// <summary>端点符号半径相对 size 的因子。默认 0.5，即 size 就是一对符号的标称直径。</summary>
        public float MarkerRadiusFactor { get; set; } = 0.5f;

        /// <summary>连线与符号的共同描边宽度相对 size 的因子。</summary>
        public float StrokeWidthFactor { get; set; } = 0.1f;

        /// <summary>
        /// 中心锚点版本里半跨相对 size 的因子。必须大于
        /// <see cref="MarkerRadiusFactor"/> × <see cref="SymbolGapFactor"/>，否则连线被让位吃光、只剩两个符号。
        /// </summary>
        public float PairHalfSpanFactor { get; set; } = 1.6f;

        /// <summary>某端符号需要让出的连线长度。</summary>
        /// <param name="symbolSize">端点符号半径。</param>
        public float RetractOf(float symbolSize)
        {
            return symbolSize * _symbolGapFactor;
        }

        /// <summary>直接用两端点画（模板算好了两端落位时用这个，最直白）。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="from">左端（较早时刻）端点，即端点符号中心。</param>
        /// <param name="to">右端（较晚时刻）端点，即端点符号中心。</param>
        /// <param name="symbolSize">端点符号半径（像素）。</param>
        /// <param name="color">主色。</param>
        /// <param name="stroke">描边宽度（像素）。</param>
        public void Draw(Painter painter, SKPoint from, SKPoint to, float symbolSize, SKColor color, float stroke)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (symbolSize <= 0f)
            {
                return;
            }

            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float length = (float)Math.Sqrt((double)dx * dx + (double)dy * dy);
            float retract = RetractOf(symbolSize);

            // 连线从两端符号的外缘断开：两端都让位后仍有剩余才画，否则宁可无线，
            // 也不让线穿过 ∩/∨ 的尖（那是这类符号最容易被画坏的地方）。
            if (ConnectWithLine && length > retract * 2f)
            {
                float ux = dx / length;
                float uy = dy / length;
                painter.Line(from.X + ux * retract, from.Y + uy * retract,
                             to.X - ux * retract, to.Y - uy * retract, color, stroke);
            }

            painter.Marker(StartSymbol, from, symbolSize, color, stroke);
            painter.Marker(EndSymbol, to, symbolSize, color, stroke);
        }

        /// <summary>以 anchor 为中心水平画一对符号。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="anchor">整对符号的中心。</param>
        /// <param name="size">标称尺寸（像素）。</param>
        /// <param name="color">主色。</param>
        public void Draw(Painter painter, SKPoint anchor, float size, SKColor color)
        {
            if (size <= 0f)
            {
                return;
            }

            float radius = size * MarkerRadiusFactor;
            float halfSpan = size * PairHalfSpanFactor;
            Draw(painter,
                 new SKPoint(anchor.X - halfSpan, anchor.Y),
                 new SKPoint(anchor.X + halfSpan, anchor.Y),
                 radius, color, size * StrokeWidthFactor);
        }
    }
}
