using System;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>作业票措施确认框的三态。</summary>
    public enum TriState
    {
        /// <summary>未确认：空方框 □。</summary>
        Unchecked = 0,
        /// <summary>已确认：方框内打勾 ☑（勾由线段画成，不是字符）。</summary>
        Checked = 1,
        /// <summary>不涉及：方框内划叉（不是字符，同理）。</summary>
        NotApplicable = 2,
    }

    /// <summary>
    /// 作业票措施确认的三态方框。勾选与划叉都是几何线段，不是字符
    /// （字符在不同字体下不可靠，且本库要导出 PDF/SVG）。
    /// </summary>
    /// <remarks>
    /// 锚点语义：anchor 是方框的几何中心，方框是边长 = size 的正方形，
    /// 所以 <c>size</c> 取行高时正好与长文本同一基准。
    /// GB 30871-2022 八类特殊作业票的"符合 / 不符合 / 不涉及"三态即由本符号承载，
    /// 文字由模板落在框外，框内只有几何。
    /// </remarks>
    public sealed class TriStateBox : IGlyph
    {
        private readonly TriState _state;

        /// <summary>构造三态方框。</summary>
        /// <param name="state">当前状态。</param>
        public TriStateBox(TriState state)
        {
            _state = state;
        }

        /// <summary>当前状态。</summary>
        public TriState State { get { return _state; } }

        /// <summary>框线与内部标记的描边宽度相对 size 的因子。</summary>
        public float StrokeWidthFactor { get; set; } = 0.09f;

        /// <summary>勾选两笔的转折点在框内的位置（相对框左上角的因子，x/y 两个方向）。</summary>
        public SKPoint CheckElbowFactor { get; set; } = new SKPoint(0.40f, 0.76f);

        /// <summary>方框矩形（anchor 为中心）。</summary>
        /// <param name="anchor">方框几何中心。</param>
        /// <param name="size">标称尺寸（方框边长）。</param>
        public SKRect BoxRect(SKPoint anchor, float size)
        {
            float half = size * 0.5f;
            return new SKRect(anchor.X - half, anchor.Y - half, anchor.X + half, anchor.Y + half);
        }

        /// <summary>勾选三笔的落点（左上起笔、转折、右上收笔），绝对坐标，供模板与测试核对。</summary>
        /// <param name="anchor">方框几何中心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKPoint[] CheckPoints(SKPoint anchor, float size)
        {
            SKRect box = BoxRect(anchor, size);
            float elbowX = box.Left + size * ClampFactor(CheckElbowFactor.X);
            float elbowY = box.Top + size * ClampFactor(CheckElbowFactor.Y);
            return new[]
            {
                new SKPoint(box.Left + size * 0.16f, box.Top + size * 0.52f),
                new SKPoint(elbowX, elbowY),
                new SKPoint(box.Right - size * 0.14f, box.Top + size * 0.24f),
            };
        }

        /// <summary>划叉两笔的端点（左上、右下、右上、左下），绝对坐标。</summary>
        /// <param name="anchor">方框几何中心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKPoint[] CrossPoints(SKPoint anchor, float size)
        {
            SKRect box = BoxRect(anchor, size);
            float inset = size * 0.18f;
            return new[]
            {
                new SKPoint(box.Left + inset, box.Top + inset),
                new SKPoint(box.Right - inset, box.Bottom - inset),
                new SKPoint(box.Right - inset, box.Top + inset),
                new SKPoint(box.Left + inset, box.Bottom - inset),
            };
        }

        /// <summary>按状态画框与标记。不写任何字符。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="anchor">方框几何中心。</param>
        /// <param name="size">标称尺寸（方框边长）。</param>
        /// <param name="color">主色。</param>
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
            painter.Frame(BoxRect(anchor, size), color, stroke);

            if (_state == TriState.Checked)
            {
                SKPoint[] check = CheckPoints(anchor, size);
                painter.Polyline(check, color, stroke, false);
            }
            else if (_state == TriState.NotApplicable)
            {
                SKPoint[] cross = CrossPoints(anchor, size);
                painter.Line(cross[0].X, cross[0].Y, cross[1].X, cross[1].Y, color, stroke);
                painter.Line(cross[2].X, cross[2].Y, cross[3].X, cross[3].Y, color, stroke);
            }
        }

        private static float ClampFactor(float value)
        {
            if (value <= 0f)
            {
                return 0.4f;
            }
            return value > 1f ? 1f : value;
        }
    }
}
