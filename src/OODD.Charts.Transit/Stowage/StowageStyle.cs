using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Transit.Stowage
{
    /// <summary>
    /// 集装箱积载图版面令牌。
    /// <para>
    /// <b>未校准</b>（docs/06 §4.12 / D22）：BAY 图字段顺序与分色未与任何船公司图样对齐，
    /// tier 高度、row 间距、row 向两舷扩散的比例都是可调默认值；
    /// <see cref="ContainerFieldOrder"/> 就是"字段顺序"的显式令牌。
    /// </para>
    /// </summary>
    public sealed class StowageStyle
    {
        public TransitStyle Frame { get; set; } = new TransitStyle();

        public TransitPalette Palette { get; set; } = new TransitPalette();

        /// <summary>双联张之间的间距。</summary>
        public float PanelGap = 18f;

        /// <summary>层高（px）。A18 的对照令牌：改它，箱块纵向位置整体变化。</summary>
        public float TierHeight = 44f;

        /// <summary>相邻 row 的基准间距（px，从舷中起算）。</summary>
        public float RowPitch = 30f;

        /// <summary>row 向两舷的额外扩散因子：第 n 个 row 的距舷中距离 = pitch·(n + factor·n·(n−1))。</summary>
        public float RowSpreadFactor = 0.16f;

        /// <summary>箱块宽相对 row 间距的比例。</summary>
        public float RowBandFactor = 0.86f;

        /// <summary>箱块与档位边界的留白。</summary>
        public float ContainerInset = 1.5f;

        /// <summary>箱块描边线粗。</summary>
        public float ContainerStroke = 0.9f;

        /// <summary>中心线线粗。</summary>
        public float CenterlineStroke = 1f;

        /// <summary>Bay / Row / Tier 标签字号。</summary>
        public float LabelSize = 7.5f;

        /// <summary>面板标题字号。</summary>
        public float PanelTitleSize = 9f;

        /// <summary>箱块内文字字号。</summary>
        public float ContainerTextSize = 6.5f;

        /// <summary>图例字号。</summary>
        public float LegendSize = 8f;

        /// <summary>超限高箱/重箱的角标边长。</summary>
        public float CornerMarkSize = 5f;

        /// <summary>
        /// 箱块文字字段顺序（字段键：size / weight / port / dg / status）。
        /// 取证缺口：字段顺序与分色未与船公司图样对齐，故做成令牌而非硬编码。
        /// </summary>
        public IList<string> ContainerFieldOrder = new List<string> { "size", "weight", "port", "dg" };

        /// <summary>字段分隔符。</summary>
        public string FieldSeparator = "/";

        /// <summary>目的港分色（按文档中首次出现顺序循环取用）。未校准：分色未取证。</summary>
        public IList<SKColor> PortColors = new List<SKColor>
        {
            new SKColor(214, 232, 245),
            new SKColor(232, 242, 214),
            new SKColor(250, 236, 205),
            new SKColor(240, 220, 232),
            new SKColor(222, 231, 244),
            new SKColor(226, 241, 236),
        };

        /// <summary>箱块内文字最大字符数（§4.12：45 字符）。超出截断，避免压出箱块。</summary>
        public int MaxContainerTextChars = 45;

        public StowageStyle Clone()
        {
            StowageStyle copy = (StowageStyle)MemberwiseClone();
            copy.Frame = Frame.Clone();
            copy.Palette = Palette.Clone();
            copy.ContainerFieldOrder = new List<string>(ContainerFieldOrder);
            copy.PortColors = new List<SKColor>(PortColors);
            return copy;
        }
    }
}
