using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 圆形红章（"已执行/作废/未执行"）。章面文字由模板单独落位且<strong>不随章旋转</strong>，
    /// 只有外圈、星形与横杠是旋转几何。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 锚点语义：anchor 是章面圆心，<c>size</c> 是外圈直径，所以 <see cref="BoundsOf"/> 给出的
    /// 外接矩形把描边也算进去，盖章时按它裁剪就不会越出版面。
    /// </para>
    /// <para>
    /// 为什么文字不进旋转：本库导出 PDF/SVG，用 canvas 旋转落文字会让文字退化成带变换的路径、
    /// 无法检索（同一教训见 <see cref="Painter"/> 的逐字竖排）。所以章体的倾角只作用于几何，
    /// 章名与"已执行"这类字样由模板用直立文本沿 <see cref="TextArcRadiusOf"/> 的基线圆逐字落位。
    /// </para>
    /// <para>GB 26860-2011 附录 A 电气第一种工作票完工后的红章即由本符号承载。</para>
    /// </remarks>
    public sealed class StampRing : IGlyph
    {
        private readonly float _rotationDegrees;
        private readonly float _strokeWidthFactor;

        /// <summary>构造圆形章。</summary>
        /// <param name="rotationDegrees">章体倾角（度，顺时针），手工盖章的现实姿态；0 表示正章。</param>
        /// <param name="withStar">章心是否画五角星。</param>
        /// <param name="withBottomLine">是否画横杠（"已执行"常压一道横杠）。</param>
        /// <param name="strokeWidthFactor">外圈描边宽度相对 size 的因子，必须为正。</param>
        public StampRing(float rotationDegrees, bool withStar = true, bool withBottomLine = true,
                         float strokeWidthFactor = 0.06f)
        {
            if (strokeWidthFactor <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(strokeWidthFactor), "描边因子必须为正。");
            }

            _rotationDegrees = rotationDegrees;
            _strokeWidthFactor = strokeWidthFactor;
            WithStar = withStar;
            WithBottomLine = withBottomLine;
        }

        /// <summary>章体倾角（度，顺时针）。文字不随它旋转。</summary>
        public float RotationDegrees { get { return _rotationDegrees; } }

        /// <summary>章心是否画五角星。</summary>
        public bool WithStar { get; }

        /// <summary>是否画横杠。</summary>
        public bool WithBottomLine { get; }

        /// <summary>外圈描边宽度相对 size 的因子。</summary>
        public float StrokeWidthFactor { get { return _strokeWidthFactor; } }

        /// <summary>五角星外接圆半径相对 size 的因子。</summary>
        public float StarRadiusFactor { get; set; } = 0.17f;

        /// <summary>五角星内凹点半径相对星半径的因子（正五角星约 0.382）。</summary>
        public float StarInnerRatio { get; set; } = 0.382f;

        /// <summary>横杠相对于圆心的纵向偏移（相对 size 的因子，正值向下）。</summary>
        public float BarOffsetFactor { get; set; } = 0.26f;

        /// <summary>横杠半长相对 size 的因子。</summary>
        public float BarHalfLengthFactor { get; set; } = 0.33f;

        /// <summary>基线圆相对外圈内侧再让出的距离（相对 size 的因子）。</summary>
        public float TextInsetFactor { get; set; } = 0.07f;

        /// <summary>章的外接矩形（含描边），用于叠在表格上不越出版面。</summary>
        /// <param name="anchor">章面圆心。</param>
        /// <param name="size">外圈直径。</param>
        public SKRect BoundsOf(SKPoint anchor, float size)
        {
            float half = size * 0.5f + StrokeOf(size) * 0.5f;
            return new SKRect(anchor.X - half, anchor.Y - half, anchor.X + half, anchor.Y + half);
        }

        /// <summary>章内上弧文字的基线圆半径（供模板放文字）。模板沿该圆逐字落<strong>直立</strong>文本。</summary>
        /// <param name="size">外圈直径。</param>
        public float TextArcRadiusOf(float size)
        {
            float radius = size * 0.5f - StrokeOf(size) - size * TextInsetFactor;
            return radius < 0f ? 0f : radius;
        }

        /// <summary>基线圆上某角度的落字点：角度 0°＝正上方、顺时针增大，已计入章体倾角，
        /// 但落下的字本身仍是直立的。</summary>
        /// <param name="anchor">章面圆心。</param>
        /// <param name="size">外圈直径。</param>
        /// <param name="angleDegrees">相对正上方的顺时针角度。</param>
        public SKPoint TextPointAt(SKPoint anchor, float size, float angleDegrees)
        {
            float radius = TextArcRadiusOf(size);
            double radians = (double)(angleDegrees + _rotationDegrees) * Math.PI / 180.0;
            return new SKPoint(anchor.X + (float)Math.Sin(radians) * radius,
                               anchor.Y - (float)Math.Cos(radians) * radius);
        }

        /// <summary>章内横杠（"已执行"常压一道横杠）的两端点。返回的是画布上的最终位置（已含章体倾角）。</summary>
        /// <param name="anchor">章面圆心。</param>
        /// <param name="size">外圈直径。</param>
        /// <param name="from">横杠左端。</param>
        /// <param name="to">横杠右端。</param>
        public void BarEnds(SKPoint anchor, float size, out SKPoint from, out SKPoint to)
        {
            SKPoint localFrom;
            SKPoint localTo;
            BarEndsLocal(anchor, size, out localFrom, out localTo);
            from = Rotate(anchor, localFrom, _rotationDegrees);
            to = Rotate(anchor, localTo, _rotationDegrees);
        }

        /// <summary>五角星的十个顶点（含章体倾角），可直接喂给 <see cref="Painter.FillPolygon"/>。</summary>
        /// <param name="anchor">章面圆心。</param>
        /// <param name="size">外圈直径。</param>
        public SKPoint[] StarPoints(SKPoint anchor, float size)
        {
            List<SKPoint> points = StarPointsLocal(anchor, size);
            SKPoint[] rotated = new SKPoint[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                rotated[i] = Rotate(anchor, points[i], _rotationDegrees);
            }
            return rotated;
        }

        /// <summary>画外圈、五角星与横杠。这三样是旋转几何；章面文字一律不在这里落。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="anchor">章面圆心（也是旋转支点）。</param>
        /// <param name="size">外圈直径。</param>
        /// <param name="color">主色（红章由调用方传红色）。</param>
        public void Draw(Painter painter, SKPoint anchor, float size, SKColor color)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (size <= 0f)
            {
                return;
            }

            float stroke = StrokeOf(size);

            // 旋转只作用于几何：外圈是旋转不变的圆，星与横杠按章体倾角走；
            // 闭包里一律用未旋转的局部坐标，避免与 RotateAround 的变换叠加两次。
            painter.RotateAround(_rotationDegrees, anchor, () =>
            {
                painter.Circle(anchor, size * 0.5f, color, stroke);

                if (WithStar)
                {
                    painter.FillPolygon(StarPointsLocal(anchor, size), color);
                }

                if (WithBottomLine)
                {
                    SKPoint from;
                    SKPoint to;
                    BarEndsLocal(anchor, size, out from, out to);
                    painter.Line(from.X, from.Y, to.X, to.Y, color, stroke);
                }
            });
        }

        private void BarEndsLocal(SKPoint anchor, float size, out SKPoint from, out SKPoint to)
        {
            float offset = size * BarOffsetFactor;
            float half = size * BarHalfLengthFactor;
            float y = anchor.Y + offset;
            from = new SKPoint(anchor.X - half, y);
            to = new SKPoint(anchor.X + half, y);
        }

        private List<SKPoint> StarPointsLocal(SKPoint anchor, float size)
        {
            float outer = size * StarRadiusFactor;
            float inner = outer * (StarInnerRatio <= 0f ? 0.382f : StarInnerRatio);
            List<SKPoint> points = new List<SKPoint>(10);
            for (int i = 0; i < 10; i++)
            {
                // 顶角朝上：0 号角在正上方，之后每个顶点转 36°，外径与内径交替。
                double radians = (double)i * 36.0 * Math.PI / 180.0;
                float radius = (i % 2) == 0 ? outer : inner;
                points.Add(new SKPoint(anchor.X + (float)Math.Sin(radians) * radius,
                                       anchor.Y - (float)Math.Cos(radians) * radius));
            }
            return points;
        }

        private float StrokeOf(float size)
        {
            return size * _strokeWidthFactor;
        }

        // 与 canvas.RotateDegrees 同向（屏幕坐标系里正角为顺时针）的点旋转，
        // 供公共落位方法与 RotateAround 的输出口径一致。
        private static SKPoint Rotate(SKPoint pivot, SKPoint point, float degrees)
        {
            double radians = (double)degrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            float dx = point.X - pivot.X;
            float dy = point.Y - pivot.Y;
            return new SKPoint(pivot.X + dx * cos - dy * sin, pivot.Y + dx * sin + dy * cos);
        }
    }
}
