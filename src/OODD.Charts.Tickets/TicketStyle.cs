using SkiaSharp;

namespace OODD.Charts.Tickets
{
    /// <summary>
    /// 凭证家族（工作票 / 特殊作业票 / 发票）共用的版面令牌。
    /// <para>
    /// 票面的栏位次序与文字属于 docs/05 §5「版面待取证」项：本层只给<b>结构尺寸</b>
    /// （幅面、边距、行高、勾选框、印章、骑缝），不给任何"符合 GB 26860 附录 A"式的断言。
    /// 具体票据自己的字段网格由该票据模板追加（docs/06 D22）。
    /// </para>
    /// </summary>
    public sealed class TicketStyle
    {
        /// <summary>单联幅面（px @96dpi）。默认 A4 纵向。</summary>
        public float Width = 794f;

        public float Height = 1123f;

        /// <summary>票面四周留白。</summary>
        public float Margin = 24f;

        /// <summary>装订侧留白（左侧打孔装订的联单比其它三边宽）。</summary>
        public float BindingMargin = 30f;

        /// <summary>标题带（票名 + 编号）。</summary>
        public float TitleHeight = 46f;

        public float TitleSize = 15f;

        /// <summary>编号 / "共 N 页第 M 页"字号。</summary>
        public float SerialSize = 8.5f;

        /// <summary>字段标签与填写文字字号（票面密度高，比工程图小一档）。</summary>
        public float FieldLabelSize = 8f;

        public float FieldTextSize = 9f;

        /// <summary>一行字段的高度。</summary>
        public float RowHeight = 20f;

        /// <summary>勾选框边长与框线粗。</summary>
        public float CheckBoxSize = 9f;

        public float CheckBoxStroke = 0.9f;

        /// <summary>"已执行/作废/未执行"印章的直径与字号。</summary>
        public float StampDiameter = 58f;

        public float StampSize = 11f;

        /// <summary>印章压过栏位时留给文字的最低可读对比（压印框线粗）。</summary>
        public float StampStroke = 2.2f;

        /// <summary>骑缝线：虚线段长与间隔。</summary>
        public float PerforationDash = 4f;

        public float PerforationGap = 3f;

        /// <summary>外框与栏目分隔线。</summary>
        public float BorderStroke = 1.2f;

        public float GridStroke = 0.6f;

        public TicketStyle Clone() => (TicketStyle)MemberwiseClone();
    }

    /// <summary>
    /// 凭证域配色。默认白底黑字（要打印、要盖章、要归档），
    /// 印章与勾选状态各占独立槽位；红色只用于印章，不用于正文。
    /// </summary>
    public sealed class TicketPalette
    {
        public SKColor PageBackground = new SKColor(255, 255, 255);

        public SKColor TitleText = new SKColor(20, 20, 20);

        /// <summary>栏目框线。</summary>
        public SKColor Ink = new SKColor(30, 30, 30);

        public SKColor Grid = new SKColor(170, 170, 170);

        public SKColor Text = new SKColor(35, 35, 35);

        /// <summary>提示性小字（填写说明、编号）。</summary>
        public SKColor MutedText = new SKColor(120, 120, 120);

        /// <summary>印章颜色（红章是行业习惯，不是标准规定值）。</summary>
        public SKColor Stamp = new SKColor(188, 32, 32);

        /// <summary>勾选框未选中时的边框色。</summary>
        public SKColor CheckBox = new SKColor(90, 90, 90);

        /// <summary>骑缝线。</summary>
        public SKColor Perforation = new SKColor(150, 150, 150);

        /// <summary>需要填写但为空的栏位底色（提醒用，不改变线框）。</summary>
        public SKColor FillPrompt = new SKColor(245, 246, 248);

        public TicketPalette Clone() => (TicketPalette)MemberwiseClone();
    }
}
