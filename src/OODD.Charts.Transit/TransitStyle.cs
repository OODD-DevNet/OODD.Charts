using SkiaSharp;

namespace OODD.Charts.Transit
{
    /// <summary>
    /// 交通运输域共用的版面令牌（幅面、留白、标题带、字号、线粗）。
    /// <para>
    /// 运行图的四级线宽、积载图的箱位格尺寸、联单的菱形边长这些<b>表单自己的</b>尺寸
    /// 不在这里：由各自模板在同名风格对象上追加，并按 docs/06 D22 标注是否已对国标取证。
    /// 验收 A18 的口径不变：改令牌必须改落点。
    /// </para>
    /// </summary>
    public sealed class TransitStyle
    {
        /// <summary>整张图逻辑尺寸（px @96dpi）。运行图/积载图都是横幅面，默认按横向给。</summary>
        public float Width = 1240f;

        public float Height = 700f;

        public float Margin = 20f;

        /// <summary>装订侧留白（联单类多联票据左侧要打孔）。</summary>
        public float BindingMargin = 26f;

        public float TitleHeight = 52f;

        public float TitleSize = 16f;

        /// <summary>车次/船名等次级说明字号。</summary>
        public float SubtitleSize = 9f;

        public float FooterHeight = 26f;

        public float FooterSize = 8.5f;

        /// <summary>外框线粗。</summary>
        public float BorderStroke = 1.2f;

        /// <summary>站名线/格网线等细线线粗——四级线宽中的"细"档由表单令牌另给。</summary>
        public float GridStroke = 0.7f;

        /// <summary>图内文字（站名、箱号、货名）字号。</summary>
        public float LabelSize = 8.5f;

        /// <summary>表格正文行高与字号。</summary>
        public float TableRowHeight = 18f;

        public float TableTextSize = 9f;

        /// <summary>时间/里程格式化。</summary>
        public string TimeFormat = "HH:mm";

        public string NumberFormat = "0.#";

        public TransitStyle Clone() => (TransitStyle)MemberwiseClone();
    }

    /// <summary>
    /// 运输域配色。默认白底（运行图、许可附图都要打印），
    /// 危险货物标志色单独给槽位（<c>PlacardBorder</c>），改色不需要碰模板代码。
    /// </summary>
    public sealed class TransitPalette
    {
        public SKColor PageBackground = new SKColor(255, 255, 255);

        public SKColor TitleText = new SKColor(20, 20, 20);

        public SKColor SubtitleText = new SKColor(90, 90, 90);

        /// <summary>主要图线（运行线、轮廓线）。</summary>
        public SKColor Ink = new SKColor(28, 28, 28);

        public SKColor Grid = new SKColor(200, 200, 200);

        public SKColor Text = new SKColor(35, 35, 35);

        public SKColor MutedText = new SKColor(125, 125, 125);

        /// <summary>上行/重载方向的运行线。</summary>
        public SKColor Accent = new SKColor(178, 34, 34);

        /// <summary>下行/空载方向：黑白打印时可只改线型，颜色只是屏显区分。</summary>
        public SKColor Secondary = new SKColor(30, 90, 160);

        /// <summary>天窗带/封锁时段。</summary>
        public SKColor Maintenance = new SKColor(120, 120, 120);

        /// <summary>危险标志牌的边框与象形图（UN 建议色由票样注入，这里只给默认红）。
        /// 取证据不足，见 docs/06 D22：不得据此宣称符合 GB 190 的色值。</summary>
        public SKColor PlacardBorder = new SKColor(198, 26, 26);

        public SKColor PanelFill = new SKColor(246, 248, 250);

        public SKColor PanelBorder = new SKColor(150, 158, 166);

        public TransitPalette Clone() => (TransitPalette)MemberwiseClone();
    }
}
