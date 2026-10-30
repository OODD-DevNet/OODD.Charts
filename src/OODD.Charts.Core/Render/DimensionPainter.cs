using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 工程尺寸标注的落笔。几何全部来自 <see cref="DimensionRun"/>，这里只负责"按令牌画"：
    /// 延伸线 → 尺寸线（必要时按中断矩形分两段）→ 端部符号 → 数字。
    /// </summary>
    /// <remarks>
    /// 族别差异只在这里体现：<see cref="DimensionOrientation.Radial"/> 的起点是圆心，画中心十字线而不画起点箭头；
    /// <see cref="DimensionOrientation.Ordinate"/> 的引出线自证起点，不画延伸线，基准刻线由 <see cref="DrawSet"/> 补；
    /// <see cref="DimensionOrientation.Chain"/> 照常画延伸线，但交接点只在 <see cref="DimensionChain"/> 里落一条。
    /// 数字一律<b>水平落位</b>：本库导出 PDF/SVG 时旋转文字会退化成路径、无法检索，
    /// 所以即使尺寸线是竖向或斜向，数字也正着写在尺寸线外侧。
    /// </remarks>
    public static class DimensionPainter
    {
        /// <summary>小于该量级视为零长/零宽，不再落笔（浮点兜底阈值，不是版面尺寸）。</summary>
        private const float NoExtent = 0.01f;

        /// <summary>与坐标轴平行判定用的方向余弦阈值（浮点兜底阈值，不是版面尺寸）。</summary>
        private const double AxisEpsilon = 1e-6;

        /// <summary>画一段尺寸线：延伸线、尺寸线（含中断）、两端符号、数字。</summary>
        /// <param name="painter">落笔用的绘制器。</param>
        /// <param name="run">该段的几何。</param>
        /// <param name="tokens">线宽、箭头长、字位等令牌。</param>
        /// <param name="lineColor">线色。</param>
        /// <param name="textColor">数字色。</param>
        /// <param name="textFont">数字字体（字高即其 Size）。</param>
        /// <param name="drawExtensions">是否落延伸线（链式交接点已在 <see cref="DimensionChain"/> 内部去过重）。</param>
        public static void Draw(Painter painter, DimensionRun run, DimensionTokens tokens,
                                SKColor lineColor, SKColor textColor, FontKey textFont,
                                bool drawExtensions = true)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (run == null)
            {
                throw new ArgumentNullException(nameof(run));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (!(run.LengthInPixels > NoExtent))
            {
                return;
            }

            float gap = run.ExtensionGapOr(tokens.ExtensionGap);
            float over = run.ExtensionOverOr(tokens.ExtensionOver);
            float offset = run.TextOffsetOr(tokens.TextOffset);

            run.Direction(out float ux, out float uy);
            run.Normal(out float nx, out float ny);

            DrawExtensionLines(painter, run, tokens, lineColor, gap, over, drawExtensions);
            DrawCenterMark(painter, run, tokens, lineColor);

            SKRect knockout = PrepareKnockout(painter, run, tokens, textFont, offset);
            DrawMeasureLine(painter, run, tokens, lineColor, ux, uy, knockout);

            if (run.Orientation != DimensionOrientation.Radial)
            {
                DrawArrow(painter, run.StartTip, -ux, -uy, nx, ny, run.ArrowFrom, tokens, lineColor);
            }
            DrawArrow(painter, run.EndTip, ux, uy, nx, ny, run.ArrowTo, tokens, lineColor);

            DrawNumber(painter, run, textFont, textColor, offset);
        }

        /// <summary>画一组（链式或坐标式）：逐条调用 <see cref="Draw"/>，并按需在基准位置补一条基准刻线。</summary>
        /// <param name="painter">落笔用的绘制器。</param>
        /// <param name="runs">该组的各段（链式或坐标式自己的 Runs 直接传进来即可）。</param>
        /// <param name="tokens">令牌。</param>
        /// <param name="lineColor">线色。</param>
        /// <param name="textColor">数字色。</param>
        /// <param name="textFont">数字字体。</param>
        /// <param name="drawBaseline">是否补画基准刻线（坐标式必须 true，独立读数才有共同起点）。</param>
        public static void DrawSet(Painter painter, IEnumerable<DimensionRun> runs, DimensionTokens tokens,
                                   SKColor lineColor, SKColor textColor, FontKey textFont, bool drawBaseline)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (runs == null)
            {
                throw new ArgumentNullException(nameof(runs));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            List<DimensionRun> list = new List<DimensionRun>();
            foreach (DimensionRun run in runs)
            {
                if (run == null)
                {
                    continue;
                }
                Draw(painter, run, tokens, lineColor, textColor, textFont);
                list.Add(run);
            }

            if (drawBaseline)
            {
                DrawBaselineTick(painter, list, tokens, lineColor);
            }
        }

        // ------------------------------------------------------------ 延伸线与中心标记

        private static void DrawExtensionLines(Painter painter, DimensionRun run, DimensionTokens tokens,
                                               SKColor color, float gap, float over, bool drawExtensions)
        {
            if (!drawExtensions || !UsesExtensionLine(run.Orientation) || !run.HasProfilePoints)
            {
                return;
            }

            run.ExtensionLines(run.ProfileFrom, run.ProfileTo, gap, over,
                               out SKPoint fromA, out SKPoint fromB, out SKPoint toA, out SKPoint toB);

            if (run.ShowExtensionFrom)
            {
                painter.Line(fromA.X, fromA.Y, fromB.X, fromB.Y, color, tokens.ExtensionLineWidth);
            }
            if (run.ShowExtensionTo)
            {
                painter.Line(toA.X, toA.Y, toB.X, toB.Y, color, tokens.ExtensionLineWidth);
            }
        }

        /// <summary>半径式与坐标式分别靠中心标记、引出线自证起点，不再画延伸线。</summary>
        private static bool UsesExtensionLine(DimensionOrientation orientation)
        {
            return orientation == DimensionOrientation.Linear || orientation == DimensionOrientation.Chain;
        }

        /// <summary>半径式在圆心画两条互相垂直的短细线，即制图惯例的中心线交点。</summary>
        private static void DrawCenterMark(Painter painter, DimensionRun run, DimensionTokens tokens, SKColor color)
        {
            if (run.Orientation != DimensionOrientation.Radial)
            {
                return;
            }

            double half = (double)tokens.ArrowLength * (double)tokens.CenterMarkRatio;
            run.Direction(out float ux, out float uy);
            run.Normal(out float nx, out float ny);

            Segment(painter, run.From, ux, uy, -half, half, color, tokens.ExtensionLineWidth);
            Segment(painter, run.From, nx, ny, -half, half, color, tokens.ExtensionLineWidth);
        }

        /// <summary>从 <paramref name="origin"/> 沿单位向量取 [<paramref name="from"/>, <paramref name="to"/>] 一段落笔。</summary>
        private static void Segment(Painter painter, SKPoint origin, double dx, double dy,
                                    double from, double to, SKColor color, float width)
        {
            painter.Line((float)((double)origin.X + dx * from), (float)((double)origin.Y + dy * from),
                         (float)((double)origin.X + dx * to), (float)((double)origin.Y + dy * to), color, width);
        }

        private static SKPoint Along(SKPoint origin, double dx, double dy, double distance)
        {
            return new SKPoint((float)((double)origin.X + dx * distance), (float)((double)origin.Y + dy * distance));
        }

        // ------------------------------------------------------------ 尺寸线与中断

        private static void DrawMeasureLine(Painter painter, DimensionRun run, DimensionTokens tokens, SKColor color,
                                           float ux, float uy, SKRect knockout)
        {
            double length = run.LengthInPixels;
            float start = 0f;
            float stop = 0f;
            bool broken = !knockout.IsEmpty && TryKnockoutSpan(run, knockout, ux, uy, out start, out stop)
                          && start > NoExtent && stop < length - NoExtent && stop > start;

            if (!broken)
            {
                painter.Line(run.From.X, run.From.Y, run.To.X, run.To.Y, color, tokens.LineWidth);
                return;
            }

            // GB/ISO 惯例：数字压在尺寸线上时把该线断开，用两段线绕开数字，
            // 而不是垫一块白底矩形——白底在有底色或叠图的版面上会露馅。
            Segment(painter, run.From, ux, uy, 0d, start, color, tokens.LineWidth);
            Segment(painter, run.From, ux, uy, stop, length, color, tokens.LineWidth);
        }

        /// <summary>求尺寸线被中断矩形遮住的区间（以 <see cref="DimensionRun.From"/> 为原点、沿测量方向的像素）。</summary>
        private static bool TryKnockoutSpan(DimensionRun run, SKRect band, float ux, float uy,
                                            out float start, out float stop)
        {
            start = 0f;
            stop = 0f;

            double offsetX = (double)run.From.X - band.MidX;
            double offsetY = (double)run.From.Y - band.MidY;

            double lo = double.NegativeInfinity;
            double hi = double.PositiveInfinity;
            if (!Slab(ux, offsetX, band.Width / 2d, ref lo, ref hi) ||
                !Slab(uy, offsetY, band.Height / 2d, ref lo, ref hi) ||
                !(hi > lo) || double.IsInfinity(lo) || double.IsInfinity(hi))
            {
                return false;
            }

            start = (float)lo;
            stop = (float)hi;
            return true;
        }

        /// <summary>
        /// 单坐标轴约束：线上点的该轴坐标 = <paramref name="offset"/> + <paramref name="unit"/> × s，
        /// 要求 |坐标| ≤ <paramref name="half"/>，把满足条件的 s 区间并入 [lo,hi]。
        /// 尺寸线斜穿矩形时，切出来的正好是矩形在这条线上的截段。
        /// </summary>
        private static bool Slab(double unit, double offset, double half, ref double lo, ref double hi)
        {
            if (Math.Abs(unit) < AxisEpsilon)
            {
                return Math.Abs(offset) <= half;
            }

            double a = (-half - offset) / unit;
            double b = (half - offset) / unit;
            if (a > b)
            {
                double swap = a;
                a = b;
                b = swap;
            }

            lo = Math.Max(lo, a);
            hi = Math.Min(hi, b);
            return hi >= lo;
        }

        /// <summary>按实测字宽算出中断矩形并写回 run；不需要中断（无字／不压线／令牌关闭）时写空。</summary>
        private static SKRect PrepareKnockout(Painter painter, DimensionRun run, DimensionTokens tokens,
                                              FontKey textFont, float offset)
        {
            string? label = run.DisplayText;
            if (string.IsNullOrEmpty(label) || !tokens.BreakLineForText)
            {
                run.SetTextKnockout(0f, 0f, null);
                return SKRect.Empty;
            }

            double width = painter.TextWidth(label!, textFont) + (double)tokens.KnockoutPadding * 2d;
            double height = TextHeight(painter, textFont) + (double)tokens.KnockoutPadding * 2d;

            // 数字盒只在与尺寸线相交时才挖断：抬得够高（|offset| ≥ 半盒高）就不压线了。
            if (!(width > NoExtent) || !(height > NoExtent) || Math.Abs((double)offset) >= height / 2d - NoExtent)
            {
                run.SetTextKnockout(0f, 0f, null);
                return SKRect.Empty;
            }

            run.SetTextKnockout((float)width, (float)height, offset);
            return run.TextKnockout;
        }

        // ------------------------------------------------------------ 端部符号

        private static void DrawArrow(Painter painter, SKPoint tip, float towardX, float towardY,
                                      float normalX, float normalY, ArrowKind kind,
                                      DimensionTokens tokens, SKColor color)
        {
            double ux = towardX;
            double uy = towardY;
            double nx = normalX;
            double ny = normalY;
            double length = (double)tokens.ArrowLength;

            switch (kind)
            {
                case ArrowKind.Filled:
                {
                    // 实心三角：尖端落在被测端点，底边沿测量方向往回缩，半宽由比例令牌给。
                    double half = length * (double)tokens.ArrowBaseRatio;
                    painter.FillPolygon(new SKPoint[]
                    {
                        tip,
                        Corner(tip, ux, uy, -length, nx, ny, half),
                        Corner(tip, ux, uy, -length, nx, ny, -half),
                    }, color);
                    break;
                }

                case ArrowKind.Open:
                {
                    // 开口箭：从端点往回岔开两条短斜线，比实心三角张得更开。
                    double half = length * (double)tokens.ArrowBaseRatio * 2d;
                    SKPoint upper = Corner(tip, ux, uy, -length, nx, ny, half);
                    SKPoint lower = Corner(tip, ux, uy, -length, nx, ny, -half);
                    painter.Line(tip.X, tip.Y, upper.X, upper.Y, color, tokens.ArrowWidth);
                    painter.Line(tip.X, tip.Y, lower.X, lower.Y, color, tokens.ArrowWidth);
                    break;
                }

                case ArrowKind.Dot:
                {
                    // 小圆点：圆心略微退进线内，免得与尺寸线脱节。
                    double radius = length * (double)tokens.DotRadiusRatio;
                    SKPoint center = Along(tip, ux, uy, -radius);
                    painter.Circle(center, (float)radius, color, tokens.ArrowWidth);
                    break;
                }

                case ArrowKind.Slash:
                {
                    // 短斜线：以 45° 跨过端点（建筑制图与坐标式基准端惯例），总长即箭头长令牌。
                    double dx = ux + nx;
                    double dy = uy + ny;
                    double norm = Math.Sqrt(dx * dx + dy * dy);
                    if (norm < AxisEpsilon)
                    {
                        break;
                    }
                    double half = length * 0.5d;
                    SKPoint low = Along(tip, dx / norm, dy / norm, -half);
                    SKPoint high = Along(tip, dx / norm, dy / norm, half);
                    painter.Line(low.X, low.Y, high.X, high.Y, color, tokens.ArrowWidth);
                    break;
                }
            }
        }

        /// <summary>沿两个互相垂直的单位向量取点：先沿 (ax,ay) 走 a，再沿 (bx,by) 走 b。</summary>
        private static SKPoint Corner(SKPoint origin, double ax, double ay, double a,
                                      double bx, double by, double b)
        {
            return new SKPoint((float)((double)origin.X + ax * a + bx * b),
                               (float)((double)origin.Y + ay * a + by * b));
        }

        // ------------------------------------------------------------ 数字与基准刻线

        private static void DrawNumber(Painter painter, DimensionRun run, FontKey textFont,
                                       SKColor color, float offset)
        {
            string? label = run.DisplayText;
            if (string.IsNullOrEmpty(label))
            {
                return;
            }

            SKPoint center = run.TextAnchorAt(offset);
            double width = painter.TextWidth(label!, textFont);
            SKFontMetrics metrics = painter.MetricsOf(textFont);

            // 水平落位：Skia 的落字 y 是基线，按字体度量把几何中心换算成基线。
            // 不用 canvas.Rotate —— 旋转文字导出 PDF/SVG 会退化成路径、无法检索。
            float x = (float)((double)center.X - width / 2d);
            float baseline = (float)((double)center.Y - (metrics.Ascent + metrics.Descent) / 2d);
            painter.Text(label!, x, baseline, textFont, color);
        }

        /// <summary>
        /// 基准刻线：沿公共测量方向取各组的最靠前起点，画一条垂直于测量方向的刻线，
        /// 把所有独立读数的尺寸线钉在同一个基准上；法向铺不开时（链式）退化成端部刻线并带超出量。
        /// </summary>
        private static void DrawBaselineTick(Painter painter, List<DimensionRun> runs,
                                             DimensionTokens tokens, SKColor color)
        {
            if (runs.Count == 0)
            {
                return;
            }

            DimensionRun first = runs[0];
            first.Direction(out float ux, out float uy);
            first.Normal(out float nx, out float ny);

            double along = double.PositiveInfinity;
            double near = 0d;
            double far = 0d;

            foreach (DimensionRun run in runs)
            {
                double ax = (double)run.From.X - first.From.X;
                double ay = (double)run.From.Y - first.From.Y;
                double s = ax * ux + ay * uy;
                double t = ax * nx + ay * ny;

                along = Math.Min(along, Math.Min(s, s + run.LengthInPixels));
                near = Math.Min(near, t);
                far = Math.Max(far, t);
            }

            if (double.IsPositiveInfinity(along))
            {
                return;
            }

            if (!(far - near > NoExtent))
            {
                // 各段同高（链式）：法向没有跨度，按超出量给基准端留一个刻线头。
                double over = (double)tokens.ExtensionOver;
                near = -over;
                far = over;
            }

            SKPoint foot = Along(first.From, ux, uy, along);
            Segment(painter, foot, nx, ny, near, far, color, tokens.LineWidth);
        }

        /// <summary>字体视觉高度（无度量的字体退化为字号，绝不用字面量像素）。</summary>
        private static double TextHeight(Painter painter, FontKey textFont)
        {
            SKFontMetrics metrics = painter.MetricsOf(textFont);
            double height = (double)-metrics.Ascent + metrics.Descent;
            if (!(height > NoExtent))
            {
                height = textFont.Size > 0f ? textFont.Size : 1d;
            }
            return height;
        }
    }

    /// <summary>
    /// 尺寸标注令牌。所有长度都可配，绘制代码里不出现字面量像素；
    /// 比例类字段（<see cref="ArrowBaseRatio"/> 等）是形状参数而非像素。
    /// </summary>
    public sealed class DimensionTokens
    {
        /// <summary>尺寸线线粗。</summary>
        public float LineWidth = 1f;

        /// <summary>箭头（含开口箭、圆点、斜线端部）的长度。</summary>
        public float ArrowLength = 5f;

        /// <summary>开口箭、圆点、斜线这类线状端部的线粗。</summary>
        public float ArrowWidth = 1.2f;

        /// <summary>延伸线线粗（比尺寸线细，符合制图惯例）。</summary>
        public float ExtensionLineWidth = 0.8f;

        /// <summary>延伸线与被测轮廓之间的留空（gap）。</summary>
        public float ExtensionGap = 2f;

        /// <summary>延伸线超出尺寸线端点的量（over）。</summary>
        public float ExtensionOver = 3f;

        /// <summary>数字相对尺寸线的默认偏移（正=向测量线外侧抬，负=压在线上留中断）。</summary>
        public float TextOffset = 3f;

        /// <summary>数字盒四周的额外留白，决定中断矩形的宽裕程度。</summary>
        public float KnockoutPadding = 2f;

        /// <summary>GB/ISO 两种惯例：数字压在尺寸线上时中断该线。为 false 时数字直接叠在线上且线不断。</summary>
        public bool BreakLineForText = true;

        /// <summary>实心三角底边半宽 / 箭头长（形状比例）。</summary>
        public float ArrowBaseRatio = 0.18f;

        /// <summary>圆点端部半径 / 箭头长（形状比例）。</summary>
        public float DotRadiusRatio = 0.24f;

        /// <summary>半径式中心十字的半长 / 箭头长（形状比例）。</summary>
        public float CenterMarkRatio = 1.6f;

        /// <summary>复制一份，改副本不影响原实例；与既有样式类同一实现方式。</summary>
        public DimensionTokens Clone()
        {
            return (DimensionTokens)MemberwiseClone();
        }
    }
}
