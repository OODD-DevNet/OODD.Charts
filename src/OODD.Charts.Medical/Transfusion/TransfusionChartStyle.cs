namespace OODD.Charts.Medical.Transfusion
{
    /// <summary>输血反应记录单专属尺寸令牌。</summary>
    public sealed class TransfusionChartStyle
    {
        /// <summary>单个观察时点的列宽（@96dpi）。反应记录文字较多，故比三测单宽。</summary>
        public float SlotWidth = 44f;

        /// <summary>左侧标签列宽。要放得下“血压(mmHg)”这类中英混排行名。</summary>
        public float LabelWidth = 72f;

        /// <summary>每 0.2 ℃ 一格的像素高。默认 12px，36–41 ℃ 共 25 格 = 300px。</summary>
        public float CelsiusRowHeight = 12f;

        /// <summary>附属行行高。反应观察期每半小时都要写症状、处理与用药。</summary>
        public float CellRowHeight = 26f;

        /// <summary>反应事件竖线线粗。</summary>
        public float EventStroke = 1.2f;

        /// <summary>发热阈值线线粗。</summary>
        public float FeverStroke = 1.2f;

        /// <summary>事件文字大小。</summary>
        public float EventTextSize = 8f;

        /// <summary>发热阈值在刻度列里的短标签。</summary>
        public string FeverMarkText = "发热";

        /// <summary>发热阈值在图例里的说明。</summary>
        public string FeverLegendText = "38℃ 发热阈值";

        public TransfusionChartStyle Clone() => (TransfusionChartStyle)MemberwiseClone();
    }
}
