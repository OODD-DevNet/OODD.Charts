namespace OODD.Charts.Medical.Restraint
{
    /// <summary>
    /// 保护性约束巡视记录单专属尺寸令牌。一天 24 槽，槽宽 26px，
    /// 既放得下“07”这类钟点，也放得下“左腕”“完好”这类短记录。
    /// </summary>
    public sealed class RestraintChartStyle
    {
        public float SlotWidth = 26f;

        /// <summary>左侧标签列宽。要放得下“饮食/排泄”这类最长行名。</summary>
        public float LabelWidth = 48f;

        public float DateRowHeight = 18f;
        public float HourRowHeight = 18f;

        /// <summary>顶部约束时段条的像素高。</summary>
        public float StatusBandHeight = 48f;

        /// <summary>附属行行高。巡视项一律逐槽填写。</summary>
        public float CellRowHeight = 30f;

        /// <summary>约束条上下留白。</summary>
        public float BarInset = 6f;

        /// <summary>约束条内文字大小。</summary>
        public float BarTextSize = 8f;

        /// <summary>每隔几小时写一个钟点标签；1 = 每小时都写。</summary>
        public int HourLabelStep = 1;

        /// <summary>钟点文本格式。</summary>
        public string HourFormat = "00";

        /// <summary>约束条左侧的行名。</summary>
        public string StatusLabel = "约束时段";

        public RestraintChartStyle Clone() => (RestraintChartStyle)MemberwiseClone();
    }
}
