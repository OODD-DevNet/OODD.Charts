namespace OODD.Charts.Engineering.WellLog
{
    /// <summary>
    /// 测井成果图专属令牌。幅面按 250mm 图幅令牌给出（mm → 逻辑像素走 Unit），
    /// 道宽、道头高、深度格距全部可调。
    /// <para>
    /// <b>未校准</b>：250mm 是图幅令牌，不代表已按 DZ/T 煤层气测井规范附录 C 的
    /// 图头分区与比例尺逐项核对；比例尺由文档字段给出，本条不宣称 1:1 对齐行业票样。
    /// </para>
    /// </summary>
    public sealed class WellLogChartStyle
    {
        public EngineeringStyle Common = new EngineeringStyle();

        public EngineeringPalette Palette = new EngineeringPalette();

        /// <summary>图幅宽（mm）；默认 250mm，250mm ≈ 945 逻辑像素。</summary>
        public float PaperWidthMm = 250f;

        /// <summary>绘图区上缘（逻辑像素）。</summary>
        public float PlotTop = 126f;

        /// <summary>道体高度（逻辑像素）。</summary>
        public float PlotHeight = 520f;

        public float TrackLeft = 78f;

        public float TrackWidth = 150f;

        public float TrackGap = 2f;

        public float TrackHeaderHeight = 36f;

        public double DepthTick = 10d;

        public float CurveStroke = 1.3f;

        public float MarkerRadius = 1.7f;

        public float GridStroke = 0.6f;

        public float FrameStroke = 1.1f;

        public float HeaderLabelSize = 9f;

        public float ScaleCaptionSize = 7.5f;

        public float DepthLabelSize = 7.5f;

        /// <summary>岩性图样间距（mm）。</summary>
        public float PatternSpacingMm = 1.6f;

        /// <summary>岩性图样线粗（mm）。</summary>
        public float PatternStrokeMm = 0.28f;

        public string FooterDefault = "注：曲线量程、线性/对数与岩性层段均由文档传入；深度轴自上而下递增。";

        public WellLogChartStyle Clone()
        {
            WellLogChartStyle copy = (WellLogChartStyle)MemberwiseClone();
            copy.Common = Common.Clone();
            copy.Palette = Palette.Clone();
            return copy;
        }
    }
}
