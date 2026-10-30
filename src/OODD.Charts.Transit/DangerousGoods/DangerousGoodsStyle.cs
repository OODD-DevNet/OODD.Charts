using SkiaSharp;

namespace OODD.Charts.Transit.DangerousGoods
{
    /// <summary>
    /// 危险货物道路运输联单版面令牌。
    /// <para>
    /// <b>未校准</b>（docs/06 §4.13 / D22）：菱形牌的外形结构按 UN 建议书/JT/T 617 的通用版面，
    /// 但官方象形图是受控图形，本库只提供<b>可注入槽位</b>；牌面尺寸、类项号字号、
    /// 联次比例都是可调默认值，不宣称与任何国标图样 1:1 对齐。
    /// </para>
    /// </summary>
    public sealed class DangerousGoodsStyle
    {
        public TransitStyle Frame { get; set; } = new TransitStyle();

        public TransitPalette Palette { get; set; } = new TransitPalette();

        /// <summary>主标志菱形对角线长（px）。A18 的对照令牌。</summary>
        public float PlacardSize = 150f;

        /// <summary>菱形描边宽相对 size 的因子。</summary>
        public float PlacardStrokeFactor = 0.028f;

        /// <summary>主/副标志间隙相对 size 的因子。</summary>
        public float SubPlacardGapFactor = 0.35f;

        /// <summary>类项号字号相对菱形对角线长的因子。</summary>
        public float ClassNumberFactor = 0.26f;

        /// <summary>菱形牌下方 UN 编号字号。</summary>
        public float UnCaptionSize = 9f;

        /// <summary>联单区域高度。</summary>
        public float CopyHeight = 232f;

        /// <summary>联单表头（联名）高度。</summary>
        public float CopyHeaderHeight = 22f;

        /// <summary>联单字段行高。</summary>
        public float CopyRowHeight = 20f;

        /// <summary>字段名列宽。</summary>
        public float CopyLabelWidth = 62f;

        /// <summary>联单字号。</summary>
        public float CopyTextSize = 8f;

        /// <summary>联单内小标志牌尺寸。</summary>
        public float CopyPlacardSize = 54f;

        /// <summary>头部字段表字号。</summary>
        public float HeaderFieldSize = 8.5f;

        /// <summary>头部字段行高。</summary>
        public float HeaderFieldRowHeight = 21f;

        /// <summary>字段名列宽（头部）。</summary>
        public float HeaderLabelWidth = 78f;

        /// <summary>罐体示意区高度。</summary>
        public float TankBandHeight = 64f;

        /// <summary>罐体轮廓宽。</summary>
        public float TankBodyWidth = 210f;

        /// <summary>罐体轮廓高。</summary>
        public float TankBodyHeight = 34f;

        /// <summary>罐体标志牌宽。</summary>
        public float TankPlacardWidth = 26f;

        /// <summary>罐体标志牌高。</summary>
        public float TankPlacardHeight = 34f;

        /// <summary>骑缝线线粗。</summary>
        public float PerforationStroke = 0.8f;

        /// <summary>副标志（如 6.1/8）并排间隙因子。</summary>
        public float SubPlacardSizeFactor = 0.72f;

        public DangerousGoodsStyle Clone()
        {
            DangerousGoodsStyle copy = (DangerousGoodsStyle)MemberwiseClone();
            copy.Frame = Frame.Clone();
            copy.Palette = Palette.Clone();
            return copy;
        }
    }
}
