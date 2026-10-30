using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Geoscience.Surface
{
    /// <summary>
    /// 天气图上的一个站点。位置用<b>页面坐标</b>（0..1，左→右、上→下）而不是经纬度：
    /// 本库不做地图投影，投影后的落点由调用方给出（docs/06 §4.10 "lat/lon 或页面坐标"）。
    /// 所有数值均为文档数据，库不内置任何观测值。
    /// </summary>
    public sealed class SurfaceStation
    {
        public SurfaceStation(string name, double pageX, double pageY)
        {
            Name = name;
            PageX = pageX;
            PageY = pageY;
        }

        /// <summary>站号/站名，画在站圈上方。</summary>
        public string Name { get; }

        /// <summary>横向页面坐标，0 = 面板左缘，1 = 右缘。</summary>
        public double PageX { get; set; }

        /// <summary>纵向页面坐标，0 = 面板上缘，1 = 下缘。</summary>
        public double PageY { get; set; }

        public double TemperatureC { get; set; }

        public double DewPointC { get; set; }

        /// <summary>海平面气压（hPa）。</summary>
        public double PressureHpa { get; set; } = 1000d;

        /// <summary>三小时气压变量（hPa），正为升压。</summary>
        public double PressureTendencyHpa { get; set; }

        /// <summary>风向（度）。0°＝北＝屏幕向上，顺时针增大，与 <see cref="Render.WindBarb"/> 同一口径。</summary>
        public double WindDirectionDegrees { get; set; }

        /// <summary>风速（m/s）。羽旗编码用的节数由模板按 <c>SurfaceChartStyle.KnotsPerMps</c> 换算。</summary>
        public double WindSpeedMps { get; set; }

        /// <summary>云量（八分制 0..8）。</summary>
        public int CloudOktas { get; set; }

        /// <summary>天气现象码（如 "RA"、"TSRA"）。只作文本落位，不内置符号码表。</summary>
        public string? WeatherCode { get; set; }
    }

    /// <summary>
    /// 一个层次的面板：地面层 <see cref="PressureHpa"/> 为 null，高空等压面给层次气压。
    /// 高空等压面图与地面图共用本模板（docs/06 §4.10 的叠放开关）。
    /// </summary>
    public sealed class SurfaceLevelPlot
    {
        public SurfaceLevelPlot(int? pressureHpa, string label)
        {
            PressureHpa = pressureHpa;
            Label = label;
        }

        /// <summary>层次气压（hPa）；null 表示地面层。</summary>
        public int? PressureHpa { get; }

        /// <summary>面板左上角的层次说明，如 "500hPa"。</summary>
        public string Label { get; }

        public IList<SurfaceStation> Stations { get; } = new List<SurfaceStation>();
    }

    /// <summary>
    /// 地面/高空天气图文档。示例数据由调用方给出：本类型不携带任何气候态或标准站点表。
    /// </summary>
    public sealed class SurfacePlotDocument : ChartDocument
    {
        public SurfacePlotDocument()
        {
            FormTitle = "地面天气图";
            Levels.Add(new SurfaceLevelPlot(null, "地面"));
        }

        /// <summary>层次面板，第一个通常是地面层。</summary>
        public IList<SurfaceLevelPlot> Levels { get; } = new List<SurfaceLevelPlot>();

        /// <summary>
        /// 多层堆叠开关：为 false 只画第一个层次（地面图）；为 true 按层次数纵向等分堆叠
        /// （高空等压面图）。这不是新模板键，而是同一模板的字段（docs/07 T260）。
        /// </summary>
        public bool StackPressureLevels { get; set; }

        /// <summary>图头右侧的时次说明，如 "2026-03-01 08 时（示例）"。</summary>
        public string? ValidTimeText { get; set; }
    }
}
