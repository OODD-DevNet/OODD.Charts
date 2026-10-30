namespace OODD.Charts.Medical.Pain
{
    /// <summary>
    /// 疼痛评估记录单专属尺寸令牌。挂在模板上而不是 ChartStyle，新增表单不必改引擎。
    /// </summary>
    public sealed class PainChartStyle
    {
        /// <summary>单个评估时点的列宽（@96dpi）。每次评估都要写部位与措施，故比三测单宽。</summary>
        public float SlotWidth = 52f;

        /// <summary>NRS 每 1 分占的像素高（@96dpi）。</summary>
        public float ScoreRowHeight = 22f;

        /// <summary>附属行行高。疼痛单每次评估都要写部位/性质/措施，故统一逐槽细分。</summary>
        public float CellRowHeight = 28f;

        /// <summary>重度阈值线线粗。</summary>
        public float SevereStroke = 1.5f;

        /// <summary>重度评分点外圈相对标记半径的留白。</summary>
        public float SevereRingPadding = 2.5f;

        /// <summary>阈值线右侧文字，留空表示不写。</summary>
        public string SevereLineText = "NRS≥7 重度";

        public PainChartStyle Clone() => (PainChartStyle)MemberwiseClone();
    }
}
