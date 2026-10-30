using SkiaSharp;

namespace OODD.Charts.Transit.LoadBalance
{
    /// <summary>
    /// 载重平衡图版面令牌。
    /// <para>
    /// <b>未校准</b>（docs/06 §4.5 / D22）：包线折点坐标由文档给（样例是自造示例包线，
    /// 不是真实机型限制），绘图区高度、表格行高、图样间距都是可调默认值。
    /// </para>
    /// </summary>
    public sealed class LoadBalanceStyle
    {
        public TransitStyle Frame { get; set; } = new TransitStyle();

        public TransitPalette Palette { get; set; } = new TransitPalette();

        /// <summary>左右刻度标签列宽。</summary>
        public float AxisWidth = 48f;

        /// <summary>重心包线绘图区高度（px）。A18 的对照令牌。</summary>
        public float PlotHeight = 380f;

        /// <summary>重量轴标题独占带高（px）：标题落在绘图区上缘之外，不压斜纹与网格。</summary>
        public float AxisTitleBand = 16f;

        /// <summary>图与表之间的间隙；须容下重心轴刻度数字与轴名（未校准）。</summary>
        public float TableGap = 40f;

        /// <summary>载重表高度。</summary>
        public float TableHeight = 96f;

        /// <summary>载重表行高。</summary>
        public float TableRowHeight = 22f;

        /// <summary>重心轴的刻度步长（%MAC）。</summary>
        public double CgTickStep = 5d;

        /// <summary>重量轴的刻度步长（kg）。</summary>
        public double WeightTickStep = 10000d;

        /// <summary>包线外斜纹间距（mm，真实毫米口径）。</summary>
        public float HatchSpacingMm = 2f;

        /// <summary>包线外斜纹线粗（mm）。</summary>
        public float HatchStrokeMm = 0.3f;

        /// <summary>包线描边线粗。</summary>
        public float EnvelopeStroke = 1.4f;

        /// <summary>包线名称字号。</summary>
        public float EnvelopeLabelSize = 8f;

        /// <summary>装载点半径。</summary>
        public float MarkerRadius = 4f;

        /// <summary>装载点描边线粗。</summary>
        public float MarkerStroke = 1.2f;

        /// <summary>装载点标签字号。</summary>
        public float PointLabelSize = 7.5f;

        /// <summary>轴刻度数字字号。</summary>
        public float AxisLabelSize = 7.5f;

        /// <summary>轴标题字号。</summary>
        public float AxisTitleSize = 8.5f;

        /// <summary>表头/表体字号。</summary>
        public float TableTextSize = 8f;

        public LoadBalanceStyle Clone()
        {
            LoadBalanceStyle copy = (LoadBalanceStyle)MemberwiseClone();
            copy.Frame = Frame.Clone();
            copy.Palette = Palette.Clone();
            return copy;
        }
    }
}
