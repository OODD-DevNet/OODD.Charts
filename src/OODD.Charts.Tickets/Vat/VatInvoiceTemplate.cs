using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Tickets.Vat
{
    /// <summary>
    /// 增值税专用发票自己的令牌：组合凭证家族共用令牌，再加上发票网格尺寸、密码区格位、
    /// 金额压印字号与联名。
    /// <para>
    /// <b>全部是结构尺寸，未按票样校准</b>（docs/05 §5 / D22）。密码区 4×21 格、8 格发票号码、
    /// 3/4 联划分都是结构样例，不宣称符合任何税务票样或填写规范。
    /// </para>
    /// </summary>
    public sealed class VatInvoiceStyle
    {
        /// <summary>凭证家族共用尺寸（幅面、边距、线粗）。</summary>
        public TicketStyle Ticket { get; set; } = new TicketStyle();

        /// <summary>凭证家族配色。</summary>
        public TicketPalette Palette { get; set; } = new TicketPalette();

        /// <summary>票名默认值（文档的 <see cref="ChartDocument.FormTitle"/> 优先）。</summary>
        public string Title = "增值税专用发票";

        /// <summary>票名行高。</summary>
        public float TitleHeight = 30f;

        /// <summary>票名字号。</summary>
        public float TitleSize = 16f;

        /// <summary>联名 / 提示小字字号。</summary>
        public float UsageSize = 9f;

        /// <summary>代码 / 号码 / 日期行高。</summary>
        public float MetaRowHeight = 18f;

        /// <summary>代码 / 号码 / 日期标签字号。</summary>
        public float MetaLabelSize = 8f;

        /// <summary>代码 / 号码 / 日期值字号。</summary>
        public float MetaValueSize = 9f;

        /// <summary>8 格发票号码的格高。</summary>
        public float InvoiceNumberBoxSize = 13f;

        /// <summary>发票号码单格宽相对格高的因子。</summary>
        public float InvoiceNumberBoxWidthFactor = 0.62f;

        /// <summary>购买方 / 销售方小标题行高。</summary>
        public float PartyTitleHeight = 14f;

        /// <summary>购买方 / 销售方信息行高。</summary>
        public float PartyRowHeight = 17f;

        /// <summary>购买方 / 销售方标签列宽。</summary>
        public float PartyLabelWidth = 84f;

        /// <summary>购买方列与销售方列之间的空隙。</summary>
        public float PartyColumnGap = 8f;

        /// <summary>购买方 / 销售方文字字号。</summary>
        public float PartySize = 8.5f;

        /// <summary>密码区小标题行高。</summary>
        public float CipherLabelHeight = 14f;

        /// <summary>密码区单格格高。</summary>
        public float CipherBoxSize = 12f;

        /// <summary>密码区单格宽相对格高的因子。</summary>
        public float CipherBoxWidthFactor = 0.62f;

        /// <summary>密码区字符字号。</summary>
        public float CipherCharSize = 7.5f;

        /// <summary>密码区列数（默认 21）。</summary>
        public int CipherColumns = 21;

        /// <summary>密码区行数（默认 4，合计 84 格）。</summary>
        public int CipherRows = 4;

        /// <summary>明细表头行高。</summary>
        public float ItemHeaderHeight = 16f;

        /// <summary>明细行高（AppendableRowGroup 的生长步长）。</summary>
        public float ItemRowHeight = 16f;

        /// <summary>明细最少行数（不足时补空行，避免薄发票）。</summary>
        public int MinItemRows = 2;

        /// <summary>明细列宽权重（名称 / 规格 / 单位 / 数量 / 单价 / 金额 / 税率 / 税额）。</summary>
        public IReadOnlyList<double> ItemColumnWeights = new[]
        {
            3.0d, 1.2d, 0.6d, 0.9d, 1.1d, 1.3d, 0.7d, 1.1d,
        };

        /// <summary>明细表头文字（与权重同序）。</summary>
        public IReadOnlyList<string> ItemHeaders = new[]
        {
            "货物或应税劳务名称", "规格型号", "单位", "数量", "单价", "金额", "税率", "税额",
        };

        /// <summary>明细文字字号。</summary>
        public float ItemSize = 8f;

        /// <summary>合计行高。</summary>
        public float TotalRowHeight = 16f;

        /// <summary>价税合计行高。</summary>
        public float AmountRowHeight = 26f;

        /// <summary>"价税合计"标签列宽。</summary>
        public float AmountLabelWidth = 96f;

        /// <summary>"（小写）"金额列宽。</summary>
        public float AmountLowerWidth = 190f;

        /// <summary>大写格与小写格之间的空槽宽（此槽内不画线、不落字）。</summary>
        public float AmountGutter = 8f;

        /// <summary>压印文字的左右留白。</summary>
        public float AmountPaddingX = 3f;

        /// <summary>压印文字的上下留白。</summary>
        public float AmountPaddingY = 3f;

        /// <summary>压印名义字号。</summary>
        public float PressedBaseSize = 13f;

        /// <summary>压印字号下限建议值（触底时仍以不越界优先）。</summary>
        public float PressedMinSize = 7f;

        /// <summary>压印文字是否加粗。</summary>
        public bool PressedBold = true;

        /// <summary>"价税合计"标签字号。</summary>
        public float AmountLabelSize = 9f;

        /// <summary>"（小写）"金额字号。</summary>
        public float LowerAmountSize = 11f;

        /// <summary>备注行高。</summary>
        public float RemarkHeight = 18f;

        /// <summary>签署行高。</summary>
        public float FooterHeight = 18f;

        /// <summary>签署文字字号。</summary>
        public float FooterSize = 8.5f;

        /// <summary>签署列标签。</summary>
        public IReadOnlyList<string> FooterLabels = new[] { "收款人", "复核", "开票人" };

        /// <summary>联次附加字段的单行高（行数 = 联数）。</summary>
        public float CopyFieldRowHeight = 15f;

        /// <summary>区段之间的空隙。</summary>
        public float SectionGap = 4f;

        /// <summary>单元格内文字留白。</summary>
        public float CellPadding = 3f;

        /// <summary>单联最小高度。</summary>
        public float MinCopyHeight = 300f;

        /// <summary>联与联之间的骑缝形式。</summary>
        public PerforationStyle Perforation = PerforationStyle.DashedCut;

        /// <summary>骑缝线线粗。</summary>
        public float PerforationStroke = 1f;

        /// <summary>浅拷贝。嵌套令牌与配色是可变对象，这里刻意共享引用。</summary>
        public VatInvoiceStyle Clone()
        {
            return (VatInvoiceStyle)MemberwiseClone();
        }
    }

    /// <summary>
    /// 增值税专用发票 finance.vat-invoice。
    /// <para>
    /// 3/4 联开关由 <see cref="VatInvoiceDocument.CopyCount"/> 决定；每联只显示自己的附加字段
    /// （存根编号 / 记账凭证号 / 抵扣认证期 / 报销人），其余联的附加字段行留空。
    /// 发票号码用 8 格 <see cref="DigitBox"/>；密码区用 4 行 × 21 格 <see cref="DigitBox"/>；
    /// 价税合计（大写）用 <see cref="PressedAmount"/> 压进固定单元格，不压到相邻的"（小写）"格。
    /// </para>
    /// <para>
    /// <b>D22</b>：票面栏位、联名、密码区格位与压印规则都是结构样例，未按票样校准，
    /// 不宣称符合任何税务规范。
    /// </para>
    /// </summary>
    public sealed class VatInvoiceTemplate : IChartTemplate
    {
        /// <summary>模板键（逐字，不得改）。</summary>
        public const string TemplateKey = "finance.vat-invoice";

        /// <summary>模板键。</summary>
        public string Key
        {
            get { return TemplateKey; }
        }

        /// <summary>展示名。</summary>
        public string DisplayName
        {
            get { return "增值税专用发票"; }
        }

        /// <summary>票面令牌；改令牌即改版面。</summary>
        public VatInvoiceStyle Tokens { get; set; } = new VatInvoiceStyle();

        /// <summary>求解版面。纯函数：只读文档与令牌，连续调用给出逐字段相同的版面。</summary>
        /// <param name="document">必须为 <see cref="VatInvoiceDocument"/>。</param>
        /// <param name="style">全局样式（字族用于压印文本测量）。</param>
        /// <param name="fonts">字体解析器。</param>
        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            VatInvoiceDocument doc = Cast(document);
            VatInvoiceStyle tokens = Tokens;

            VatInvoiceLayout layout = VatInvoiceSheet.Build(doc, tokens, fonts, style.Text.Family);
            return ChartGeometry.ForLayout(layout.Bounds, layout.Bounds, layout);
        }

        /// <summary>把发票画到画布上；所有落点取自 Measure 产出的 <see cref="VatInvoiceLayout"/>。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="geometry">Measure 的返回值。</param>
        /// <param name="document">发票文档。</param>
        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }
            Cast(document);

            VatInvoiceLayout layout = geometry.LayoutOf<VatInvoiceLayout>()
                ?? throw new InvalidOperationException(
                    "几何里没有 VatInvoiceLayout：请用 VatInvoiceTemplate.Measure 产出的几何。");

            VatInvoiceSheet.Draw(painter, layout, Tokens);
        }

        private static VatInvoiceDocument Cast(ChartDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            VatInvoiceDocument? doc = document as VatInvoiceDocument;
            if (doc == null)
            {
                throw new ArgumentException(
                    "增值税专用发票模板只接受 VatInvoiceDocument，实到 "
                    + document.GetType().FullName + "。", nameof(document));
            }
            return doc;
        }
    }
}
