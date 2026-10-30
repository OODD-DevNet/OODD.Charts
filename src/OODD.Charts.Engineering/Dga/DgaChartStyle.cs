namespace OODD.Charts.Engineering.Dga
{
    /// <summary>
    /// 变压器油色谱 DGA 专属令牌。上半部趋势轨共用一条按天偏移的取样轴，
    /// 下半部大卫三角形的顶点、刻度与分区配色都从这里给。
    /// <para>
    /// <b>未校准</b>：三角形边长、刻度密度与分区底色透明度是屏显/打印的默认值，
    /// 分区顶点一律由文档传入；DL/T 722 的分区坐标未核实，库不内置、也不宣称与标准图样 1:1 一致。
    /// </para>
    /// </summary>
    public sealed class DgaChartStyle
    {
        public EngineeringStyle Common = new EngineeringStyle();

        public EngineeringPalette Palette = new EngineeringPalette();

        // ------------------------------------------------------------ 趋势轨

        public float TrendLeft = 96f;

        public float TrendTop = 112f;

        public float TrendHeight = 252f;

        /// <summary>单条气体轨宽度。</summary>
        public float TrackWidth = 210f;

        public float TrackGap = 12f;

        public float TrackHeaderHeight = 32f;

        /// <summary>量程上限按数据最大值放大的余量（1.15 表示留 15% 头顶空间）。</summary>
        public double ValueHeadroom = 1.15d;

        public float TrendStroke = 1.4f;

        public float TrendMarkerRadius = 2.2f;

        public float TrendGridStroke = 0.6f;

        public float TrendTickSize = 7.5f;

        public float TrendLabelSize = 8f;

        /// <summary>左侧取样日期标签的宽度（绘图区外）。</summary>
        public float DayLabelWidth = 58f;

        public string TrendTitle = "多气体趋势（共享非等距取样日期轴）";

        // ------------------------------------------------------------ 三角形

        public float TriangleLeft = 340f;

        public float TriangleTop = 432f;

        /// <summary>三角形标题带：标题与顶点 A 的标注必须各占一条，不能共享趋势区下的同一条缝。</summary>
        public float TriangleTitleBand = 16f;

        /// <summary>顶点 A 标注带：紧贴三角形顶点之上，与标题带之间留 2px。</summary>
        public float ApexLabelBand = 14f;

        /// <summary>等边三角形边长（逻辑像素）。</summary>
        public float TriangleSide = 320f;

        public float TriangleStroke = 1.4f;

        public float ZoneStroke = 0.9f;

        /// <summary>分区填色透明度（0–255）；0 表示只描边。</summary>
        public byte ZoneFillAlpha = 46;

        public float TriangleTickSize = 7f;

        public float ZoneLabelSize = 8.5f;

        public float VertexLabelSize = 9f;

        public float TrianglePointRadius = 3.2f;

        /// <summary>三边刻度内缩（像素）。</summary>
        public float EdgeTickLength = 5f;

        /// <summary>三边刻度数字离边的距离（像素）。</summary>
        public float EdgeLabelGap = 13f;

        public string ComponentALabel = "CH4";

        public string ComponentBLabel = "C2H4";

        public string ComponentCLabel = "C2H2";

        public string TriangleTitle = "大卫三角形（分区顶点由文档给定，坐标未校准）";

        public string SampleLegendText = "投点：油色谱三组分";

        public string FooterDefault = "注：三边刻度各自反向；分区坐标来自文档，库不内置标准分区表。";

        public DgaChartStyle Clone()
        {
            DgaChartStyle copy = (DgaChartStyle)MemberwiseClone();
            copy.Common = Common.Clone();
            copy.Palette = Palette.Clone();
            return copy;
        }
    }
}
