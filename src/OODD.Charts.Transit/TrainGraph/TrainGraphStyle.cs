using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Transit.TrainGraph
{
    /// <summary>
    /// 列车运行图版面令牌。
    /// <para>
    /// <b>未校准</b>（docs/06 §4.2 / D22）：四级线宽、站名列宽、时刻带高度都是可调默认值，
    /// 未按任何国标票样做像素级校准，因此不得宣称与国标 1:1 对齐。
    /// 改这里的任何一个值都必须改变出图落点（A18），测试
    /// <c>TransitFormsTests.运行图改站名列宽令牌落点随之变化</c> 钉住这条。
    /// </para>
    /// </summary>
    public sealed class TrainGraphStyle
    {
        /// <summary>运输域共用的幅面/留白/字号/线粗。</summary>
        public TransitStyle Frame { get; set; } = new TransitStyle();

        /// <summary>运输域配色。</summary>
        public TransitPalette Palette { get; set; } = new TransitPalette();

        /// <summary>左侧站名列宽（px @96dpi）。<b>未校准</b>。</summary>
        public float StationLabelWidth = 88f;

        /// <summary>站名与站名线之间的留白。</summary>
        public float StationLabelPadding = 5f;

        /// <summary>站名线（横线）线粗。<b>未校准</b>。</summary>
        public float StationStroke = 0.9f;

        /// <summary>时刻带（横轴下方的刻度带）高度。</summary>
        public float TimeAxisHeight = 30f;

        /// <summary>标题下方的副标题/说明带高度。</summary>
        public float NoteBandHeight = 18f;

        /// <summary>四级刻度线宽：1 分毫线。<b>未校准</b>。</summary>
        public float HairlineStroke = 0.4f;

        /// <summary>四级刻度线宽：5 分细线。<b>未校准</b>。</summary>
        public float FineStroke = 1.0f;

        /// <summary>四级刻度线宽：10 分中线。<b>未校准</b>。</summary>
        public float MediumStroke = 1.8f;

        /// <summary>四级刻度线宽：60 分粗线（唯一带标签的一级）。<b>未校准</b>。</summary>
        public float HeavyStroke = 3.0f;

        /// <summary>毫线层级的步长（分钟）。</summary>
        public double HairlineMinutes = 1d;

        /// <summary>细线层级的步长（分钟）。</summary>
        public double FineMinutes = 5d;

        /// <summary>中线层级的步长（分钟）。</summary>
        public double MediumMinutes = 10d;

        /// <summary>粗线层级的步长（分钟）。</summary>
        public double HeavyMinutes = 60d;

        /// <summary>刻度线伸入时刻带的比例（相对时刻带高）。</summary>
        public float TickBandFraction = 0.45f;

        /// <summary>时刻标签字号。</summary>
        public float TimeLabelSize = 7.5f;

        /// <summary>运行线线粗（单方向）。</summary>
        public float TrainStroke = 1.3f;

        /// <summary>车次标注字号。</summary>
        public float TrainLabelSize = 7f;

        /// <summary>天窗带斜纹间距（mm，真实毫米口径）。<b>未校准</b>。</summary>
        public float WindowHatchSpacingMm = 2.2f;

        /// <summary>天窗带斜纹线粗（mm）。</summary>
        public float WindowHatchStrokeMm = 0.3f;

        /// <summary>天窗带描边线粗。</summary>
        public float WindowStroke = 0.8f;

        /// <summary>天窗带斜纹在色板里的不透明度（0–255）。</summary>
        public byte WindowHatchAlpha = 255;

        /// <summary>图例文字字号。</summary>
        public float LegendSize = 8f;

        /// <summary>复制一份；改副本不影响原实例。</summary>
        public TrainGraphStyle Clone()
        {
            TrainGraphStyle copy = (TrainGraphStyle)MemberwiseClone();
            copy.Frame = Frame.Clone();
            copy.Palette = Palette.Clone();
            return copy;
        }

        /// <summary>取某一级刻度的线宽令牌。</summary>
        public float StrokeOf(RulerTier tier)
        {
            switch (tier)
            {
                case RulerTier.Hairline: return HairlineStroke;
                case RulerTier.Fine: return FineStroke;
                case RulerTier.Medium: return MediumStroke;
                default: return HeavyStroke;
            }
        }
    }
}
