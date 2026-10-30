using SkiaSharp;

namespace OODD.Charts.Transit.OversizeLoad
{
    /// <summary>
    /// 超限运输许可附图（车货外廓 + 尺寸链 + 高度坐标式 + 轴荷）版面令牌。
    /// <para>
    /// <b>未校准</b>（docs/06 §4.14 / D22）：外廓折点、轴距、轴荷全部由文档给；
    /// 出图比例、尺寸线偏移、延伸线留空都是可调默认值，不宣称与许可附图图样 1:1 对齐。
    /// </para>
    /// </summary>
    public sealed class OversizeLoadStyle
    {
        public TransitStyle Frame { get; set; } = new TransitStyle();

        public TransitPalette Palette { get; set; } = new TransitPalette();

        /// <summary>出图比例（每米像素）。A18 的对照令牌：改它，轮廓与尺寸链整体缩放。</summary>
        public float ScalePixelsPerMeter = 52f;

        /// <summary>轮廓左端到版面左边界的留白（坐标式标注用）。</summary>
        public float OrdinateLeftWidth = 120f;

        /// <summary>坐标式基准线的横向偏移（相对轮廓左端）。</summary>
        public float OrdinateBaselineOffset = 56f;

        /// <summary>坐标式各条的阶梯间距。</summary>
        public float OrdinateStackPitch = 16f;

        /// <summary>轮廓描边线粗。</summary>
        public float ProfileStroke = 1.6f;

        /// <summary>地面线线粗。</summary>
        public float GroundStroke = 1.2f;

        /// <summary>轮半径（米）。</summary>
        public float WheelRadiusMeters = 0.5f;

        /// <summary>链式标注在地面以下的偏移。</summary>
        public float ChainOffset = 34f;

        /// <summary>地面线到页脚顶部之间的标注带高度（链式、轴荷、轴荷文字都在里面）。</summary>
        public float GroundBandHeight = 112f;

        /// <summary>高度尺寸线相对轮廓右端的水平间距。</summary>
        public float HeightDimensionGap = 26f;

        /// <summary>轴荷带顶面相对地面的偏移。</summary>
        public float AxleBarTopOffset = 56f;

        /// <summary>最大轴荷条的像素高度。</summary>
        public float AxleBarMaxHeight = 26f;

        /// <summary>轴荷条宽。</summary>
        public float AxleBarWidth = 12f;

        /// <summary>尺寸数字字号。</summary>
        public float DimensionTextSize = 8f;

        /// <summary>尺寸数字相对尺寸线的偏移（交给 DimensionTokens.TextOffset）。</summary>
        public float DimensionTextOffset = 3f;

        /// <summary>轴荷标签字号。</summary>
        public float AxleLabelSize = 7f;

        /// <summary>图例/说明字号。</summary>
        public float NoteSize = 8f;

        public OversizeLoadStyle Clone()
        {
            OversizeLoadStyle copy = (OversizeLoadStyle)MemberwiseClone();
            copy.Frame = Frame.Clone();
            copy.Palette = Palette.Clone();
            return copy;
        }
    }
}
