using SkiaSharp;

namespace OODD.Charts.Geoscience.Surface
{
    /// <summary>
    /// 站点符号位里各要素的锚点象限。用枚举而不是字面量偏移：
    /// 默认按 MH/T 4016.8 / WMO No.306 FM-12 的通行排布（左上气温、右上露点、
    /// 右下海压、左下风杆），具体像素位置仍由令牌给（docs/06 §4.10）。
    /// </summary>
    public enum ElementAnchorSlot
    {
        TopLeft = 0,
        TopRight = 1,
        BottomRight = 2,
        BottomLeft = 3,
    }

    /// <summary>
    /// 地面天气图专属尺寸令牌。组合 <see cref="GeoscienceStyle"/> 与 <see cref="GeosciencePalette"/>，
    /// 只追加本表单需要的字段。
    /// <para>
    /// <b>未校准</b>：站点符号尺寸、要素间距、网格密度是按"能读、不重叠"定的演示值，
    /// 没有与 MH/T 4016.8-2008 / QX/T 804-2025 的图样逐项对齐（docs/06 D22）。
    /// 换一套令牌即可整体改版，模板代码不含字面量位置。
    /// </para>
    /// </summary>
    public sealed class SurfaceChartStyle
    {
        public GeoscienceStyle Base = new GeoscienceStyle();

        public GeosciencePalette Palette = new GeosciencePalette();

        /// <summary>站点符号的标称尺寸（px @96dpi）。站圈半径与风杆长度都按它缩放。</summary>
        public float StationSize = 30f;

        /// <summary>站圈半径 / 标称尺寸。必须与风杆的 <c>WindBarb.StationRadiusFactor</c> 一致，否则杆根离圈。</summary>
        public float StationRadiusFactor = 0.28f;

        /// <summary>要素文字框与站圈之间的间距（px）。</summary>
        public float ElementGap = 6f;

        public float ElementTextSize = 8f;

        /// <summary>要素文字框高 / 字号。</summary>
        public float ElementBoxHeightFactor = 1.45f;

        public ElementAnchorSlot TemperatureSlot = ElementAnchorSlot.TopLeft;
        public ElementAnchorSlot DewPointSlot = ElementAnchorSlot.TopRight;
        public ElementAnchorSlot PressureSlot = ElementAnchorSlot.BottomRight;
        public ElementAnchorSlot WindBarbSlot = ElementAnchorSlot.BottomLeft;

        /// <summary>风向杆羽旗在杆的哪一侧（北半球惯例为左）。</summary>
        public bool BarbsOnLeft = true;

        /// <summary>米每秒 → 节。1 m/s = 1.943844 kn（换算定义，不是标准数值表）。</summary>
        public float KnotsPerMps = 1.943844f;

        /// <summary>面板内经纬网风格的参考网格线数（横向与纵向各几条）。</summary>
        public int GridDivisions = 4;

        /// <summary>相邻层次面板之间的间隙（px）。</summary>
        public float PanelGap = 10f;

        /// <summary>面板内缩：站点落点不许压到面板框线（px）。</summary>
        public float PanelInset = 38f;

        /// <summary>站名文字大小。</summary>
        public float StationNameSize = 7.5f;

        /// <summary>天气现象码文字大小。</summary>
        public float WeatherCodeSize = 7.5f;

        /// <summary>站名距站圈中心 / 标称尺寸。</summary>
        public float StationNameOffsetFactor = 1.45f;

        /// <summary>天气现象码距站圈中心 / 标称尺寸。</summary>
        public float WeatherCodeOffsetFactor = 2.05f;

        public string TemperatureFormat = "0.0";

        public string DewPointFormat = "0.0";

        public string PressureFormat = "0.0";

        public string TendencyFormat = "+0.0;-0.0;0.0";

        /// <summary>页脚说明（取证状态写在图上，避免版面被当作国标 1:1）。</summary>
        public string FooterNote = "示例填图：要素锚点与尺寸为可调令牌，未按 MH/T 4016.8 图样逐项校准";

        public SurfaceChartStyle Clone() => (SurfaceChartStyle)MemberwiseClone();
    }
}
