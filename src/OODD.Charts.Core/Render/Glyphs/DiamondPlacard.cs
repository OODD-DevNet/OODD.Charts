using System;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// UN 危险货物菱形标志牌的<strong>结构框架</strong>：正方形旋转 45°、上半角图案槽、下半角类项号槽。
    /// <para>UN 官方象形图是受控图形，本库只画结构，图案由调用方注入 <see cref="SKPath"/>，
    /// 这样既保证几何正确（顶角朝上、四边等长），也不冒充原版象形图。</para>
    /// </summary>
    /// <remarks>
    /// <para>锚点语义：anchor 是菱形的<strong>中心</strong>；<c>size</c> 是对角线长，
    /// 也就是菱形外接正方形（边水平/垂直）的边长，故外接矩形是 size×size。</para>
    /// <para>正方形旋转 45° 后边长 = size / √2，四条边恒等长，顶角朝上——
    /// JT/T 617.5-2018 与 UN 建议书的版面口径都落在这组几何上。</para>
    /// <para>本符号不写字：类项号（如"3"）由模板用直立文本落在 <see cref="NumberSlotOf"/> 里，
    /// 避免旋转文字在 PDF/SVG 里退化成路径。</para>
    /// </remarks>
    public sealed class DiamondPlacard : IGlyph
    {
        private readonly SKPath? _symbolPath;
        private readonly bool _fillBackground;
        private readonly SKColor? _background;

        /// <summary>构造菱形标志牌结构。</summary>
        /// <param name="symbolPath">上半角图案（由调用方按 UN 原版提供的矢量路径，单位坐标任意，
        /// 符号只等比缩放并居中到图案槽）。引用由调用方持有并释放，本符号不 Dispose。</param>
        /// <param name="fillBackground">是否填充底色（UN 牌按类别有红/绿/黄等底色）。</param>
        /// <param name="background">底色；null 时用主色填充。</param>
        public DiamondPlacard(SKPath? symbolPath = null, bool fillBackground = false, SKColor? background = null)
        {
            _symbolPath = symbolPath;
            _fillBackground = fillBackground;
            _background = background;
        }

        /// <summary>菱形描边宽度相对 size 的因子。</summary>
        public float StrokeWidthFactor { get; set; } = 0.03f;

        /// <summary>注入图案是否实心填充（UN 象形图是实心色块）；false 时按描边画。</summary>
        public bool SymbolFill { get; set; } = true;

        /// <summary>
        /// 槽靠中心线一侧的内缩比例（相对半对角线）。上下两槽各自止于 ±该值，
        /// 因此两槽之间恒有 2×该值 的空隙——这是"槽不重叠"的第一重保证。
        /// </summary>
        public float SlotInnerFactor { get; set; } = 0.14f;

        /// <summary>槽靠顶/底角一侧的外缩比例（相对半对角线）；必须大于 <see cref="SlotInnerFactor"/> 且小于 1。</summary>
        public float SlotOuterFactor { get; set; } = 0.62f;

        /// <summary>
        /// 槽宽相对其所在三角形可用宽度的内缩比例，取值 (0,1]。
        /// 等于 1 时槽的上（下）外角正好落在菱形斜边上，小于 1 则整槽严格在三角形内部。
        /// </summary>
        public float SlotWidthInsetFactor { get; set; } = 0.92f;

        /// <summary>半对角线长（顶点到中心的距离）。</summary>
        /// <param name="size">标称尺寸（对角线长）。</param>
        public static float HalfDiagonalOf(float size)
        {
            return size * 0.5f;
        }

        /// <summary>顶角顶点相对锚点的偏移。</summary>
        /// <param name="size">标称尺寸。</param>
        public SKPoint Top(float size)
        {
            float half = HalfDiagonalOf(size);
            return new SKPoint(0f, -half);
        }

        /// <summary>底角顶点相对锚点的偏移。</summary>
        /// <param name="size">标称尺寸。</param>
        public SKPoint Bottom(float size)
        {
            float half = HalfDiagonalOf(size);
            return new SKPoint(0f, half);
        }

        /// <summary>左角顶点相对锚点的偏移。</summary>
        /// <param name="size">标称尺寸。</param>
        public SKPoint Left(float size)
        {
            float half = HalfDiagonalOf(size);
            return new SKPoint(-half, 0f);
        }

        /// <summary>右角顶点相对锚点的偏移。</summary>
        /// <param name="size">标称尺寸。</param>
        public SKPoint Right(float size)
        {
            float half = HalfDiagonalOf(size);
            return new SKPoint(half, 0f);
        }

        /// <summary>四个顶点的绝对坐标，顺序为顶、右、底、左（可直接喂给 <see cref="Painter.BuildPath"/>）。</summary>
        /// <param name="anchor">菱形中心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKPoint[] VerticesOf(SKPoint anchor, float size)
        {
            float half = HalfDiagonalOf(size);
            return new[]
            {
                new SKPoint(anchor.X, anchor.Y - half),
                new SKPoint(anchor.X + half, anchor.Y),
                new SKPoint(anchor.X, anchor.Y + half),
                new SKPoint(anchor.X - half, anchor.Y),
            };
        }

        /// <summary>菱形的外接矩形（顶/底/左/右四顶点围成）。</summary>
        /// <param name="anchor">菱形中心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKRect BoundsOf(SKPoint anchor, float size)
        {
            float half = HalfDiagonalOf(size);
            return new SKRect(anchor.X - half, anchor.Y - half, anchor.X + half, anchor.Y + half);
        }

        /// <summary>把单元格换算成居中锚点（模板从表格格子落牌时用）。</summary>
        /// <param name="cell">单元格矩形。</param>
        public static SKPoint CenterOf(SKRect cell)
        {
            return new SKPoint(cell.MidX, cell.MidY);
        }

        /// <summary>单元格里放得下的最大标称尺寸（对角线长 = 外接正方形边长）。</summary>
        /// <param name="cell">单元格矩形。</param>
        public static float FitSizeOf(SKRect cell)
        {
            float width = Math.Abs(cell.Width);
            float height = Math.Abs(cell.Height);
            return width < height ? width : height;
        }

        /// <summary>下半角放类项号的矩形（文字由模板自己画，符号本身不写文字）。</summary>
        /// <param name="anchor">菱形中心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKRect NumberSlotOf(SKPoint anchor, float size)
        {
            return SlotRect(anchor, size, true);
        }

        /// <summary>上半角放图案的矩形。</summary>
        /// <param name="anchor">菱形中心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKRect SymbolSlotOf(SKPoint anchor, float size)
        {
            return SlotRect(anchor, size, false);
        }

        /// <summary>副标志（如 6.1 附带腐蚀性）并排时的第二个菱形锚点偏移。</summary>
        /// <param name="size">标称尺寸。</param>
        /// <param name="gapFactor">两牌间隙相对 size 的因子。</param>
        public static SKPoint SubPlacardOffset(float size, float gapFactor = 0.35f)
        {
            return new SKPoint(size + size * gapFactor, 0f);
        }

        /// <summary>画菱形外框、可选底色与注入的图案。不画任何文字。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="anchor">菱形中心。</param>
        /// <param name="size">标称尺寸。</param>
        /// <param name="color">主色（边框色，底色缺省时也用它）。</param>
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

            float stroke = size * StrokeWidthFactor;
            using (SKPath outline = painter.BuildPath(VerticesOf(anchor, size), true))
            {
                if (_fillBackground)
                {
                    painter.FillPath(outline, _background.HasValue ? _background.Value : color);
                }
                painter.StrokePath(outline, color, stroke);
            }

            DrawSymbol(painter, anchor, size, color, stroke);
        }

        // 槽矩形：上（下）半三角里内接的一个矩形。
        // 约束推导：以中心为原点、半对角线 h 为半径，下半三角满足 |x| <= h - y（0 <= y <= h）。
        // 令槽的 y 区间为 [inner*h, outer*h]（0 < inner < outer < 1），半宽取 (h - outer*h) * inset（inset <= 1），
        // 则槽的四角都落在三角形内；上槽是它关于中心线的镜像。
        // 因为 y=0 这条中心线两侧各留了 inner*h 的空带，两槽必然不相交（inset<=1 时也保证不越出菱形）。
        private SKRect SlotRect(SKPoint anchor, float size, bool lower)
        {
            float half = HalfDiagonalOf(size);
            float inner = half * ClampRatio(SlotInnerFactor);
            float outer = half * ClampRatio(SlotOuterFactor);
            if (outer <= inner)
            {
                // 兜底：外缩必须大于内缩，否则槽塌成一条线；取中心线以外的一半三角。
                outer = (inner + half) * 0.5f;
            }

            float halfWidth = (half - outer) * ClampRatio(SlotWidthInsetFactor);
            float y0 = lower ? anchor.Y + inner : anchor.Y - outer;
            float y1 = lower ? anchor.Y + outer : anchor.Y - inner;
            return new SKRect(anchor.X - halfWidth, y0, anchor.X + halfWidth, y1);
        }

        private static float ClampRatio(float value)
        {
            if (value <= 0f)
            {
                return 0.05f;
            }
            return value > 1f ? 1f : value;
        }

        private void DrawSymbol(Painter painter, SKPoint anchor, float size, SKColor color, float stroke)
        {
            SKPath? injected = _symbolPath;
            if (injected == null)
            {
                return;
            }

            SKRect slot = SymbolSlotOf(anchor, size);
            SKRect bounds = injected.Bounds;
            if (bounds.Width <= 0f || bounds.Height <= 0f)
            {
                return;
            }

            // 等比缩放到图案槽并居中：缩放只做几何，图案槽的位置由结构框架定死，
            // 缩放因子完全由 size 与槽矩形推出，不含字面量像素。
            float scale = Math.Min(slot.Width / bounds.Width, slot.Height / bounds.Height);
            using (SKPath placed = new SKPath())
            {
                placed.AddPath(injected, SKPathAddMode.Append);
                placed.Transform(SKMatrix.CreateScale(scale, scale));
                placed.Transform(SKMatrix.CreateTranslation(slot.MidX - bounds.MidX * scale,
                                                           slot.MidY - bounds.MidY * scale));
                if (SymbolFill)
                {
                    painter.FillPath(placed, color);
                }
                else
                {
                    painter.StrokePath(placed, color, stroke);
                }
            }
        }
    }
}
