using SkiaSharp;

namespace OODD.Charts.Radar
{
    /// <summary>
    /// 雷达图专属尺寸令牌。sheet 尺寸、盘面留白、面板与字号都在这里，
    /// 绘制代码不出现字面量尺寸（验收 A9）。
    /// </summary>
    public sealed class RadarStyle
    {
        /// <summary>整张图的逻辑尺寸（px @96dpi）。</summary>
        public float Width = 920f;

        public float Height = 780f;

        public float Margin = 18f;

        /// <summary>标题带高度：上行标题 + 下行模式说明。</summary>
        public float TitleHeight = 64f;

        public float TitleSize = 18f;

        /// <summary>标题下的模式/量程说明字号。</summary>
        public float SubtitleSize = 9.5f;

        /// <summary>信息面板宽（右侧布局）。</summary>
        public float PanelWidth = 270f;

        public float PanelGap = 16f;

        public float PanelPadding = 10f;

        public float PanelTitleSize = 11f;

        public float PanelTextSize = 9.5f;

        public float PanelRowHeight = 16f;

        public float PanelBlockGap = 7f;

        /// <summary>方位标签尺寸。</summary>
        public float BearingLabelSize = 9f;

        public float RangeLabelSize = 8.5f;

        /// <summary>方位线（辐条）间隔，默认 30°。</summary>
        public double BearingTickStep = 30d;

        /// <summary>距标数值标注所在的方位，避开 30° 整倍数辐条。</summary>
        public double RangeLabelBearing = 45d;

        /// <summary>方位标签到盘面外缘的留白。</summary>
        public float LabelRadiusPad = 16f;

        public float RingStroke = 1f;

        public float SpokeStroke = 0.8f;

        public float ScopeBorderStroke = 1.8f;

        /// <summary>右上角罗盘半径。</summary>
        public float CompassRadius = 26f;

        public float CompassStroke = 1.2f;

        public float CompassLabelSize = 10f;

        /// <summary>扫描拖尾切成多少段做渐隐。</summary>
        public int SweepTailSegments = 24;

        public float SweepStroke = 1.6f;

        public float TargetRadius = 3.6f;

        public float TargetGlowRadius = 8f;

        /// <summary>选中目标的外圈半径。</summary>
        public float SelectedRingRadius = 11f;

        public float TargetLabelSize = 8.5f;

        public float TrailStroke = 1.1f;

        /// <summary>方位文本格式，默认三位数（000）。</summary>
        public string BearingFormat = "000";

        /// <summary>距标数值格式。</summary>
        public string RangeFormat = "0.#";

        public RadarStyle Clone() => (RadarStyle)MemberwiseClone();
    }

    /// <summary>
    /// 雷达配色。默认是暗底绿光屏；改这里即可换色（例如打印用的浅底黑线），
    /// 不需要碰模板代码。所有字段都是独立的语义槽位：
    /// 盘面底色、环线、辐条、方位字、距标字、扫描、目标、丢失回波、选中、轨迹、面板、南北向。
    /// </summary>
    public sealed class RadarPalette
    {
        /// <summary>整张图的底色（模板会先铺满它）。</summary>
        public SKColor PageBackground = new SKColor(5, 18, 13);

        public SKColor TitleText = new SKColor(180, 245, 205);

        public SKColor SubtitleText = new SKColor(95, 190, 130);

        /// <summary>盘面填充。</summary>
        public SKColor ScopeFill = new SKColor(7, 30, 21);

        public SKColor ScopeBorder = new SKColor(70, 210, 130);

        public SKColor Ring = new SKColor(30, 110, 70);

        public SKColor Spoke = new SKColor(24, 88, 58);

        public SKColor BearingText = new SKColor(120, 220, 160);

        public SKColor RangeText = new SKColor(95, 190, 130);

        /// <summary>扫描扇与扫描线本体。</summary>
        public SKColor Sweep = new SKColor(90, 255, 150);

        public SKColor Target = new SKColor(140, 255, 175);

        /// <summary>目标光晕（低透明度叠在光点外）。</summary>
        public SKColor TargetGlow = new SKColor(80, 255, 140, 70);

        public SKColor LostTarget = new SKColor(150, 175, 162);

        /// <summary>选中目标的高亮色（圈、角标、面板行）。</summary>
        public SKColor Selected = new SKColor(255, 196, 60);

        public SKColor Trail = new SKColor(70, 190, 120);

        public SKColor Text = new SKColor(205, 245, 220);

        public SKColor PanelFill = new SKColor(9, 36, 26);

        public SKColor PanelBorder = new SKColor(45, 150, 95);

        /// <summary>罗盘指北针颜色。</summary>
        public SKColor North = new SKColor(255, 100, 84);

        /// <summary>罗盘指南针颜色。</summary>
        public SKColor South = new SKColor(170, 225, 195);

        public RadarPalette Clone() => (RadarPalette)MemberwiseClone();
    }
}
