using SkiaSharp;

namespace OODD.Charts.Medical.Forms.VaccinationBooklet
{
    /// <summary>
    /// 预防接种证专属令牌（幅面按 mm 写，落笔时经 <see cref="Unit"/> 换算）。
    /// <para>
    /// 未校准：210×145mm 是国家规范附录 5-2 的幅面令牌，装订厚度 ≤2.6mm 是印刷约束，
    /// 本库只记录不实现；<see cref="AllowLayoutDeviation"/> 为 false 时拒绝增删字段。
    /// </para>
    /// <para>
    /// 专色：SkiaSharp 是 RGB 管线，<see cref="SpotColor"/> 只是 C85 M30 Y65 K65 的近似 RGB，
    /// 原始 CMYK 值保存在 <see cref="VaccinationBookletDocument.SpotColorCmyk"/> 供印厂使用，
    /// 真 CMYK 分色不在本库范畴。
    /// </para>
    /// </summary>
    public sealed class VaccinationBookletStyle
    {
        /// <summary>单页宽（mm）。</summary>
        public float PageWidthMm = 210f;

        /// <summary>单页高（mm）。</summary>
        public float PageHeightMm = 145f;

        public float PageMargin = 22f;
        public float TitleSize = 20f;
        public float BodySize = 9f;

        /// <summary>记录表表格头高（双语列头）。</summary>
        public float TableHeaderHeight = 22f;

        /// <summary>表格首行距页顶的距离。</summary>
        public float TableTop = 62f;

        /// <summary>记录行高下限。A18 断言用它证明落点由令牌驱动；铺满打开时它只兜底不决定实际行高。</summary>
        public float RecordRowHeight = 18f;

        /// <summary>记录表下缘距页底的距离（px）：表框拉到这里，给页码留出位置。</summary>
        public float RecordTableBottomMargin = 34f;

        /// <summary>记录表数据行区是否均摊铺满整页（关闭后按 <see cref="RecordRowHeight"/> 定高，页面会留大片空白）。</summary>
        public bool FillRecordPage = true;

        /// <summary>每页放多少条预印疫苗行。</summary>
        public int ScheduleRowsPerPage = 4;

        /// <summary>记录表列宽比例（疫苗 / 剂次 / 日期 / 企业 / 批号）。</summary>
        public float[] ColumnWeights = { 0.40f, 0.12f, 0.16f, 0.19f, 0.13f };

        /// <summary>
        /// 专色的近似 RGB。C85 M30 Y65 K65 → R≈13、G≈63、B≈58。
        /// 不是分色结果，只是屏幕/PDF 上的近似呈现。
        /// </summary>
        public SKColor SpotColor = new SKColor(13, 63, 58);

        /// <summary>是否允许在锁定版面上增删字段。规范要求"不得自行变更加印"，默认 false。</summary>
        public bool AllowLayoutDeviation = false;

        public VaccinationBookletStyle Clone()
        {
            VaccinationBookletStyle copy = (VaccinationBookletStyle)MemberwiseClone();
            copy.ColumnWeights = (float[])ColumnWeights.Clone();
            return copy;
        }
    }
}
