using SkiaSharp;

namespace OODD.Charts.Medical.Forms.AstReport
{
    /// <summary>
    /// 药敏报告单专属令牌。
    /// <para>
    /// 未校准：RangeBar 各段权重按 WS/T 639—2018 §3.3.4 的 S｜SDD｜I｜R 顺序给出，
    /// 段宽比例是令牌，不是对某台仪器判读尺的复刻。
    /// </para>
    /// </summary>
    public sealed class AstReportStyle
    {
        public float PaperWidth = 700f;
        public float Margin = 8f;
        public float HospitalRowHeight = 22f;
        public float TitleRowHeight = 22f;

        /// <summary>药名标签列宽（在 RangeBar 之外）。</summary>
        public float BarLabelWidth = 150f;

        /// <summary>结果列宽（MIC + 判读字母）。</summary>
        public float ResultWidth = 84f;

        /// <summary>RangeBar 上方的分类标签带高。</summary>
        public float CategoryBandHeight = 20f;

        /// <summary>单条 RangeBar 高。</summary>
        public float BarHeight = 16f;

        /// <summary>行高（A18 断言用它证明落点由令牌驱动）。</summary>
        public float RowHeight = 28f;

        public float FooterHeight = 20f;

        /// <summary>S 段权重。</summary>
        public double SusceptibleWeight = 1d;

        /// <summary>SDD 段权重。</summary>
        public double DoseDependentWeight = 1.5d;

        /// <summary>I 段权重。</summary>
        public double IntermediateWeight = 1d;

        /// <summary>R 段权重。</summary>
        public double ResistantWeight = 1.5d;

        public SKColor SusceptibleColor = new SKColor(46, 139, 87);
        public SKColor DoseDependentColor = new SKColor(198, 156, 32);
        public SKColor IntermediateColor = new SKColor(222, 125, 24);
        public SKColor ResistantColor = new SKColor(178, 34, 34);

        public SKColor ColorOf(string category)
        {
            switch (category)
            {
                case "S": return SusceptibleColor;
                case "SDD": return DoseDependentColor;
                case "I": return IntermediateColor;
                case "R": return ResistantColor;
                default: return SKColors.Gray;
            }
        }

        public AstReportStyle Clone() => (AstReportStyle)MemberwiseClone();
    }
}
