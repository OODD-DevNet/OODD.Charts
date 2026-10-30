using SkiaSharp;

namespace OODD.Charts.Engineering
{
    /// <summary>
    /// 工程域表单共用的版面令牌（图纸幅面、留白、标题带、字号、线粗）。
    /// <para>
    /// 这里只放<b>跨表单共用</b>的那一层：具体表单自己的尺寸（筛系档位、道宽、包线边界点距……）
    /// 由该表单的模板在同名风格对象上追加字段，不散成字面量。验收 A18 的口径是
    /// "改这里的任一令牌，出图落点必须跟着变"——每张新表单交付时都要带上这条断言。
    /// </para>
    /// </summary>
    public sealed class EngineeringStyle
    {
        /// <summary>整张图的逻辑尺寸（px @96dpi）。默认按 A4 横向绘图区给。</summary>
        public float Width = 1075f;

        public float Height = 760f;

        /// <summary>图纸四周留白（装订边另算，见 <see cref="BindingMargin"/>）。</summary>
        public float Margin = 20f;

        /// <summary>装订侧留白（多联次/左装订时比其它三边宽）。</summary>
        public float BindingMargin = 28f;

        /// <summary>标题带高度：上行图名 + 下行工程信息。</summary>
        public float TitleHeight = 56f;

        public float TitleSize = 16f;

        /// <summary>标题下的工程/样品说明字号。</summary>
        public float SubtitleSize = 9f;

        /// <summary>页脚带（签发、页码）高度与字号。</summary>
        public float FooterHeight = 26f;

        public float FooterSize = 8.5f;

        /// <summary>坐标区与表格共用的外框线粗。</summary>
        public float BorderStroke = 1.2f;

        /// <summary>网格线粗。</summary>
        public float GridStroke = 0.7f;

        /// <summary>轴标签与刻度字号。</summary>
        public float LabelSize = 8.5f;

        /// <summary>表格正文行高与字号。</summary>
        public float TableRowHeight = 18f;

        public float TableTextSize = 9f;

        /// <summary>数值格式化（工程图纸默认保留一位小数）。</summary>
        public string NumberFormat = "0.0";

        public EngineeringStyle Clone() => (EngineeringStyle)MemberwiseClone();
    }

    /// <summary>
    /// 工程域配色。默认是<b>白底黑线</b>（图纸要打印、要归档，暗底只适合屏显）；
    /// 每个槽位都是独立语义，换色不需要碰模板代码。
    /// </summary>
    public sealed class EngineeringPalette
    {
        /// <summary>整张图的底色（模板先铺满它）。</summary>
        public SKColor PageBackground = new SKColor(255, 255, 255);

        public SKColor TitleText = new SKColor(20, 20, 20);

        public SKColor SubtitleText = new SKColor(90, 90, 90);

        /// <summary>外框与主要图线。</summary>
        public SKColor Ink = new SKColor(30, 30, 30);

        public SKColor Grid = new SKColor(198, 198, 198);

        public SKColor Text = new SKColor(35, 35, 35);

        /// <summary>次要信息（页脚、说明）。</summary>
        public SKColor MutedText = new SKColor(120, 120, 120);

        /// <summary>主数据曲线/填色。</summary>
        public SKColor Accent = new SKColor(28, 96, 168);

        /// <summary>第二条数据线（与主色同明度、不同色相，保证黑白打印可分）。
        /// 黑白打印下靠线型而不是颜色区分，这条只是彩色屏显用。</summary>
        public SKColor Secondary = new SKColor(176, 96, 24);

        /// <summary>超限/告警带。</summary>
        public SKColor Warning = new SKColor(196, 46, 46);

        public SKColor PanelFill = new SKColor(246, 248, 250);

        public SKColor PanelBorder = new SKColor(150, 158, 166);

        public EngineeringPalette Clone() => (EngineeringPalette)MemberwiseClone();
    }
}
