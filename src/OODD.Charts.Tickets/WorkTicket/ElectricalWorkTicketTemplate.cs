using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Tickets.WorkTicket
{
    /// <summary>
    /// 电气第一种工作票自己的令牌：组合凭证家族共用令牌 + 作业票网格令牌 + 联名。
    /// <para>
    /// <b>全部是结构尺寸，未按票样校准</b>（docs/05 §5 / D22）。联名、栏位次序与措施行
    /// 都是结构样例，不宣称与 GB 26860-2011 附录 A 逐项一致——该票样尚未取证。
    /// </para>
    /// </summary>
    public sealed class ElectricalWorkTicketStyle
    {
        /// <summary>凭证家族共用尺寸（幅面、边距、行高、字号、印章直径）。</summary>
        public TicketStyle Ticket { get; set; } = new TicketStyle();

        /// <summary>凭证家族配色。</summary>
        public TicketPalette Palette { get; set; } = new TicketPalette();

        /// <summary>作业票字段网格令牌。</summary>
        public WorkTicketGridStyle Grid { get; set; } = new WorkTicketGridStyle();

        /// <summary>票名（默认值，文档的 <see cref="ChartDocument.FormTitle"/> 优先）。</summary>
        public string Title = "电气第一种工作票";

        /// <summary>措施区小标题的默认值（文档的 <see cref="ElectricalWorkTicketDocument.SectionTitle"/> 优先）。</summary>
        public string SectionTitle = "安全措施";

        /// <summary>存根联键。</summary>
        public string StubCopyKey = "stub";

        /// <summary>存根联名。</summary>
        public string StubCopyLabel = "存根联";

        /// <summary>工作联键。</summary>
        public string WorkCopyKey = "work";

        /// <summary>工作联名。</summary>
        public string WorkCopyLabel = "工作联";

        /// <summary>许可联键。</summary>
        public string PermitCopyKey = "permit";

        /// <summary>许可联名。</summary>
        public string PermitCopyLabel = "许可联";

        /// <summary>浅拷贝。嵌套令牌与配色是可变对象，这里刻意共享引用，与凭证家族其它令牌一致。</summary>
        public ElectricalWorkTicketStyle Clone()
        {
            return (ElectricalWorkTicketStyle)MemberwiseClone();
        }
    }

    /// <summary>
    /// 电气第一种工作票 engineering.electrical-work-ticket。
    /// <para>
    /// 三联纵叠（存根 / 工作 / 许可），联间字段差异由 <see cref="WorkTicketCopySpec"/> 的字段键集合决定：
    /// 三联共有前九项基本栏位，工作联加"终结时间"，许可联加"许可开始时间"与"装设接地线组数"。
    /// 差异不是画完再遮：每个联只落自己可见的字段，其余行留空但保持同一行网格，
    /// 便于像素用例在同一 y 上做"有墨 / 无墨"对照（<see cref="CollateSpec.DifferentiatingFields"/>）。
    /// </para>
    /// <para>
    /// <b>D22</b>：栏位次序、联名与措施行是结构样例，票样未取证，不宣称符合任何标准。
    /// 排版尺寸全部来自令牌，改令牌即改版面（A18）。
    /// </para>
    /// </summary>
    public sealed class ElectricalWorkTicketTemplate : IChartTemplate
    {
        /// <summary>模板键（逐字，不得改）。</summary>
        public const string TemplateKey = "engineering.electrical-work-ticket";

        /// <summary>模板键。</summary>
        public string Key
        {
            get { return TemplateKey; }
        }

        /// <summary>展示名。</summary>
        public string DisplayName
        {
            get { return "电气第一种工作票"; }
        }

        /// <summary>票面令牌；改令牌即改版面。</summary>
        public ElectricalWorkTicketStyle Tokens { get; set; } = new ElectricalWorkTicketStyle();

        /// <summary>
        /// 求解版面。纯函数：只读文档与令牌，不改任何字段，连续调用给出逐字段相同的版面。
        /// </summary>
        /// <param name="document">必须为 <see cref="ElectricalWorkTicketDocument"/>。</param>
        /// <param name="style">全局样式（本模板的尺寸全部来自 <see cref="Tokens"/>，此处只保留接口签名）。</param>
        /// <param name="fonts">字体解析器（本模板的排版不含文本测量，保留接口签名）。</param>
        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            ElectricalWorkTicketDocument doc = Cast(document);
            ElectricalWorkTicketStyle tokens = Tokens;

            WorkTicketSheetSpec spec = CreateSpec(doc, tokens);
            WorkTicketLayout layout = WorkTicketSheet.Build(spec, tokens.Ticket, tokens.Grid);

            return ChartGeometry.ForLayout(layout.Bounds, layout.Bounds, layout);
        }

        /// <summary>按文档与令牌构造票面输入（供测试核对联次字段差异，不改文档）。</summary>
        /// <param name="document">电气第一种工作票文档。</param>
        /// <param name="tokens">票面令牌。</param>
        public static WorkTicketSheetSpec CreateSpec(ElectricalWorkTicketDocument document,
            ElectricalWorkTicketStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            List<string> baseKeys = new List<string>
            {
                ElectricalWorkTicketFields.WorkLeader,
                ElectricalWorkTicketFields.WorkClass,
                ElectricalWorkTicketFields.WorkMembers,
                ElectricalWorkTicketFields.WorkPlace,
                ElectricalWorkTicketFields.WorkTask,
                ElectricalWorkTicketFields.PlanStart,
                ElectricalWorkTicketFields.PlanEnd,
                ElectricalWorkTicketFields.IssueDate,
                ElectricalWorkTicketFields.SafetyIssuer,
            };

            List<string> workKeys = new List<string>(baseKeys)
            {
                ElectricalWorkTicketFields.TerminateTime,
            };

            List<string> permitKeys = new List<string>(baseKeys)
            {
                ElectricalWorkTicketFields.PermitTime,
                ElectricalWorkTicketFields.GroundingCount,
            };

            List<WorkTicketCopySpec> copies = new List<WorkTicketCopySpec>
            {
                new WorkTicketCopySpec(tokens.StubCopyKey, tokens.StubCopyLabel, baseKeys),
                new WorkTicketCopySpec(tokens.WorkCopyKey, tokens.WorkCopyLabel, workKeys),
                new WorkTicketCopySpec(tokens.PermitCopyKey, tokens.PermitCopyLabel, permitKeys),
            };

            // 字段目录取文档的完整目录（12 项）；联次只引用其中的键，未引用项不占行。
            List<TicketField> fields = new List<TicketField>(document.Fields.Count);
            foreach (TicketField field in document.Fields)
            {
                fields.Add(field);
            }

            List<TicketMeasureRow> measures = new List<TicketMeasureRow>(document.Measures.Count);
            foreach (TicketMeasureRow row in document.Measures)
            {
                measures.Add(row);
            }

            List<string> signatures = new List<string>(document.Signatures.Count);
            foreach (string signature in document.Signatures)
            {
                signatures.Add(signature);
            }

            return new WorkTicketSheetSpec(
                string.IsNullOrEmpty(document.FormTitle) ? tokens.Title : document.FormTitle!,
                string.IsNullOrEmpty(document.SectionTitle) ? tokens.SectionTitle : document.SectionTitle,
                fields,
                measures,
                signatures,
                copies,
                document.Stamp)
            {
                UnitLine = document.UnitName,
                SerialText = document.SerialNo,
                StampText = document.StampText,
            };
        }

        /// <summary>把票面画到画布上；所有落点取自 Measure 产出的 <see cref="WorkTicketLayout"/>。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="geometry">Measure 的返回值。</param>
        /// <param name="document">电气第一种工作票文档。</param>
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

            WorkTicketLayout layout = geometry.LayoutOf<WorkTicketLayout>()
                ?? throw new InvalidOperationException(
                    "几何里没有 WorkTicketLayout：请用 ElectricalWorkTicketTemplate.Measure 产出的几何。");

            WorkTicketSheet.Draw(painter, layout, Tokens.Ticket, Tokens.Grid, Tokens.Palette);
        }

        private static ElectricalWorkTicketDocument Cast(ChartDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            ElectricalWorkTicketDocument? doc = document as ElectricalWorkTicketDocument;
            if (doc == null)
            {
                throw new ArgumentException(
                    "电气第一种工作票模板只接受 ElectricalWorkTicketDocument，实到 "
                    + document.GetType().FullName + "。", nameof(document));
            }
            return doc;
        }
    }
}
