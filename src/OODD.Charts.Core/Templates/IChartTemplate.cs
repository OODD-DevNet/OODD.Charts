using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Templates
{
    /// <summary>
    /// 一张表单的版面与绘制逻辑。新增行业表单 = 实现本接口，
    /// <b>不得</b>修改引擎任何类型（需求 FR-3 / 验收 A9）。
    /// </summary>
    public interface IChartTemplate
    {
        /// <summary>稳定标识，如 "medical.temperature-chart"。</summary>
        string Key { get; }

        /// <summary>人类可读名称。</summary>
        string DisplayName { get; }

        /// <summary>求解版面几何。不得产生副作用，可被反复调用。</summary>
        ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts);

        /// <summary>把内容画到 <paramref name="painter"/> 的画布上。</summary>
        void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document);
    }
}
