namespace OODD.Charts.Medical.Partogram
{
    /// <summary>
    /// 产程图专属的尺寸令牌。放在模板这一侧而不是 ChartStyle，
    /// 这样新增一张表单不需要动引擎（验收 A9）；ChartStyle 只留跨表单共用的令牌。
    /// <para>
    /// 微调用法：<c>var t = new PartogramChartTemplate(); t.Tokens.CmRowHeight = 34f;</c>
    /// 默认值实测自教科书版式。
    /// </para>
    /// </summary>
    public sealed class PartogramStyle
    {
        /// <summary>宫口轴每 1 cm 的像素高（@96dpi）。网格横线间距由它决定。</summary>
        public float CmRowHeight = 30f;

        /// <summary>时间带两行（日期 / 钟点）的高度。</summary>
        public float DateRowHeight = 18f;
        public float HourRowHeight = 18f;

        /// <summary>一个宫缩方块的宽（px @96dpi）。</summary>
        public float BlockWidth = 5f;

        /// <summary>宫缩方块基准高；实际高按持续时间在 Min/Max 间线性取值。</summary>
        public float BlockHeight = 5f;

        /// <summary>方块纵向堆叠间距。</summary>
        public float BlockGap = 1.5f;

        /// <summary>宫缩带行高。</summary>
        public float ContractionRowHeight = 36f;

        /// <summary>胎心带行高。</summary>
        public float FetalHeartRowHeight = 26f;

        /// <summary>胎心率异常时圈注椭圆与数字的水平留白。</summary>
        public float RingPaddingX = 3f;

        /// <summary>胎心率异常时圈注椭圆与数字的垂直留白。</summary>
        public float RingPaddingY = 2f;

        /// <summary>警戒线/处理线线粗。</summary>
        public float GuideStroke = 1.5f;

        /// <summary>破膜标记文字大小。</summary>
        public float MembraneSize = 8f;

        /// <summary>破膜箭头长。</summary>
        public float MembraneArrow = 14f;

        /// <summary>多少小时合并写一次日期。</summary>
        public int DateMergeHours = 4;

        public PartogramStyle Clone() => (PartogramStyle)MemberwiseClone();
    }
}
