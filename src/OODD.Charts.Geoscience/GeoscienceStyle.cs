using SkiaSharp;

namespace OODD.Charts.Geoscience
{
    /// <summary>
    /// 地学与气象域共用的版面令牌（幅面、留白、标题带、字号、线粗）。
    /// <para>
    /// 天气图的站点符号尺寸、T-lnP 的坐标纸格距、频率纸的分位刻度这些<b>表单自己的</b>
    /// 尺寸由各自模板在同名风格对象上追加；气象标准里的数值表不内置（docs/06 D21）。
    /// 验收 A18：改令牌必须改落点，每张新表单交付时带上这条断言。
    /// </para>
    /// </summary>
    public sealed class GeoscienceStyle
    {
        /// <summary>整张图逻辑尺寸（px @96dpi）。天气图与 T-lnP 纸都是竖向幅面。</summary>
        public float Width = 820f;

        public float Height = 1060f;

        public float Margin = 18f;

        /// <summary>右侧起算边距（天气图要素注记常偏右，留得比左边宽）。</summary>
        public float RightPad = 22f;

        public float TitleHeight = 50f;

        public float TitleSize = 15f;

        /// <summary>时次/层次说明字号（如 "500hPa 12 时"）。</summary>
        public float SubtitleSize = 9f;

        public float FooterHeight = 24f;

        public float FooterSize = 8.5f;

        public float BorderStroke = 1.1f;

        /// <summary>等值线（等压线/等高线）线粗，比坐标网格略粗。</summary>
        public float ContourStroke = 1f;

        public float GridStroke = 0.6f;

        /// <summary>站点注记与刻度字号（天气图上注记密度高，默认比工程图小一档）。</summary>
        public float LabelSize = 8f;

        /// <summary>三角坐标图（土壤质地、大卫三角形）的顶点标签字号。</summary>
        public float VertexLabelSize = 9f;

        public string NumberFormat = "0.#";

        public GeoscienceStyle Clone() => (GeoscienceStyle)MemberwiseClone();
    }

    /// <summary>
    /// 地学域配色。默认白底 + 冷灰网格（坐标纸要打印），
    /// 等压线与温度线各占一个独立槽位，换色不碰模板代码。
    /// </summary>
    public sealed class GeosciencePalette
    {
        public SKColor PageBackground = new SKColor(255, 255, 255);

        public SKColor TitleText = new SKColor(20, 20, 20);

        public SKColor SubtitleText = new SKColor(90, 90, 90);

        /// <summary>坐标轴与外框。</summary>
        public SKColor Ink = new SKColor(35, 35, 35);

        /// <summary>坐标网格（T-lnP 纸的绿线/红线属于表单自己的槽位，不由这里代表）。</summary>
        public SKColor Grid = new SKColor(205, 205, 205);

        public SKColor Text = new SKColor(40, 40, 40);

        public SKColor MutedText = new SKColor(125, 125, 125);

        /// <summary>主曲线（层结温度线、洪水频率曲线、状态点）。</summary>
        public SKColor Accent = new SKColor(24, 92, 158);

        /// <summary>第二条曲线（露温线、定线点据包）。</summary>
        public SKColor Secondary = new SKColor(150, 60, 150);

        /// <summary>等值线族。</summary>
        public SKColor Contour = new SKColor(70, 70, 70);

        /// <summary>区间带（频率纸的置信带、三角图分区底色）。</summary>
        public SKColor Band = new SKColor(230, 238, 246);

        /// <summary>特大值/异常点标记。</summary>
        public SKColor Warning = new SKColor(196, 46, 46);

        public SKColor PanelFill = new SKColor(247, 248, 250);

        public SKColor PanelBorder = new SKColor(150, 158, 166);

        public GeosciencePalette Clone() => (GeosciencePalette)MemberwiseClone();
    }
}
