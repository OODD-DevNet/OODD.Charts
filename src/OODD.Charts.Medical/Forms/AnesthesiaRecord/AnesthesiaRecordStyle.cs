namespace OODD.Charts.Medical.Forms.AnesthesiaRecord
{
    using SkiaSharp;

    /// <summary>
    /// 麻醉记录单专属尺寸令牌。全部版面数字都从这里取，模板里不出现字面量坐标。
    /// <para>
    /// 未校准：默认值按 WS 329—2024 附录 B 的版式关系（左脉搏/血压、右体温、下方用药与事件带）
    /// 取整得到，只保证结构关系成立，<b>不是</b>对纸面实物的 1:1 复刻，也不作出打印/校准承诺。
    /// 交付后按院方纸张实测微调即可。
    /// </para>
    /// </summary>
    public sealed class AnesthesiaRecordStyle
    {
        /// <summary>版面宽（96dpi 逻辑像素）。未校准。</summary>
        public float PaperWidth = 760f;

        public float HospitalRowHeight = 24f;
        public float TitleRowHeight = 22f;
        public float OperationRowHeight = 18f;

        /// <summary>时间轴带（刻度 + 标签）高。未校准。</summary>
        public float TimeBandHeight = 26f;

        /// <summary>绘图区上缘与时间带之间留白。</summary>
        public float PlotGap = 6f;

        /// <summary>
        /// 绘图区高。左轴 20–260 步长 20 共 13 条横线，默认每 20 单位 20px。
        /// 改这个令牌即整体缩放纵向，A18 用它证明落点由令牌驱动。
        /// </summary>
        public float PlotHeight = 260f;

        /// <summary>左侧刻度标签列宽（在网格外）。未校准。</summary>
        public float LeftLabelWidth = 64f;

        /// <summary>右侧体温刻度标签列宽（在网格外）。未校准。</summary>
        public float RightLabelWidth = 64f;

        /// <summary>持续用药带：每条药占一行。</summary>
        public float MedicationRowHeight = 20f;

        /// <summary>持续用药带上下留白。</summary>
        public float MedicationBandPadding = 6f;

        /// <summary>事件带高（成对符号 + 序号方格）。</summary>
        public float EventBandHeight = 48f;

        /// <summary>页脚高（11 种成对符号图例 + 采样校验告警行）。</summary>
        public float FooterHeight = 66f;

        /// <summary>时间轴细刻度（分钟）。</summary>
        public int TimeMinorMinutes = 15;

        /// <summary>时间轴粗刻度/带标签刻度（分钟）。</summary>
        public int TimeMajorMinutes = 30;

        /// <summary>成对事件符号的端点符号半径（px）。</summary>
        public float EventSymbolSize = 4.5f;

        /// <summary>事件序号方格的格高（px）。</summary>
        public float EventDigitBoxSize = 11f;

        /// <summary>WS 329—2024 对血压/脉搏的采样间隔要求（分钟），只用于文档校验告警。</summary>
        public float SamplingWarningMinutes = 5f;

        /// <summary>图例每行放几种成对符号。</summary>
        public int LegendItemsPerRow = 6;

        /// <summary>图例行高（两行 11 项要落在页脚的上半部）。</summary>
        public float LegendRowHeight = 18f;

        /// <summary>图例首行距页脚顶的距离。</summary>
        public float LegendRowTop = 8f;

        /// <summary>图例项左内缩：成对符号的半跨加半径约 19px，内缩不足会越出页面左缘。</summary>
        public float LegendItemInset = 4f;

        /// <summary>图例成对符号的标称尺寸（端点半径 = 0.5×，整对半跨 = 2.1×）。</summary>
        public float LegendSymbolSize = 9f;

        /// <summary>序号与右侧符号外缘的间距。</summary>
        public float LegendLabelGap = 3f;

        /// <summary>序号字宽：圆圈数字按全角算；第 11 项退回 (11)，也留得下。</summary>
        public float LegendLabelWidth = 18f;

        /// <summary>持续用药条的填色（表单令牌，不放进跨表单 ChartStyle）。</summary>
        public SKColor MedicationFill = new SKColor(228, 232, 236);

        public AnesthesiaRecordStyle Clone() => (AnesthesiaRecordStyle)MemberwiseClone();
    }
}
