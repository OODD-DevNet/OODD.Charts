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
    /// 八类特殊作业票自己的令牌：组合凭证家族共用令牌 + 作业票网格令牌 + 联名与副表标题。
    /// <para>
    /// <b>全部是结构尺寸，未按票样校准</b>（docs/05 §5 / D22）。八类作业的票面栏位由同一套
    /// 结构模板承载，不宣称与 GB 30871-2022 附录 A 逐项一致——该票样尚未取证。
    /// </para>
    /// </summary>
    public sealed class HotWorkTicketStyle
    {
        /// <summary>凭证家族共用尺寸。</summary>
        public TicketStyle Ticket { get; set; } = new TicketStyle();

        /// <summary>凭证家族配色。</summary>
        public TicketPalette Palette { get; set; } = new TicketPalette();

        /// <summary>作业票字段网格令牌。</summary>
        public WorkTicketGridStyle Grid { get; set; } = new WorkTicketGridStyle();

        /// <summary>票名默认值（文档的 <see cref="ChartDocument.FormTitle"/> 优先）。</summary>
        public string Title = "特殊作业票";

        /// <summary>措施区小标题默认值（文档的 <see cref="HotWorkTicketDocument.SectionTitle"/> 优先）。</summary>
        public string SectionTitle = "安全措施";

        /// <summary>副标题前缀；实际副标题 = 前缀 + 类别中文名。</summary>
        public string SubtitlePrefix = "作业类别：";

        /// <summary>危害辨识区的小标题。</summary>
        public string HazardsTitle = "危害辨识";

        /// <summary>作业联键。</summary>
        public string WorkCopyKey = "work";

        /// <summary>作业联名。</summary>
        public string WorkCopyLabel = "作业联";

        /// <summary>监护联键。</summary>
        public string GuardianCopyKey = "guardian";

        /// <summary>监护联名。</summary>
        public string GuardianCopyLabel = "监护联";

        /// <summary>浅拷贝。嵌套令牌与配色是可变对象，这里刻意共享引用。</summary>
        public HotWorkTicketStyle Clone()
        {
            return (HotWorkTicketStyle)MemberwiseClone();
        }
    }

    /// <summary>
    /// 八类特殊作业票 engineering.hot-work-ticket。
    /// <para>
    /// 两联纵叠（作业 / 监护），联间差异只落在三个字段上：作级别两联共有，
    /// 气体检测只在作业联、审批人只在监护联。危害辨识是副表（<see cref="AppendableRowGroup"/>），
    /// 条目数直接进整页高度。
    /// </para>
    /// <para>
    /// <b>D22</b>：栏位次序、联名、措施与危害条目都是结构样例，票样未取证，不宣称符合任何标准。
    /// </para>
    /// </summary>
    public sealed class HotWorkTicketTemplate : IChartTemplate
    {
        /// <summary>模板键（逐字，不得改）。</summary>
        public const string TemplateKey = "engineering.hot-work-ticket";

        /// <summary>模板键。</summary>
        public string Key
        {
            get { return TemplateKey; }
        }

        /// <summary>展示名。</summary>
        public string DisplayName
        {
            get { return "八类特殊作业票"; }
        }

        /// <summary>票面令牌；改令牌即改版面。</summary>
        public HotWorkTicketStyle Tokens { get; set; } = new HotWorkTicketStyle();

        /// <summary>求解版面。纯函数：只读文档与令牌，连续调用给出逐字段相同的版面。</summary>
        /// <param name="document">必须为 <see cref="HotWorkTicketDocument"/>。</param>
        /// <param name="style">全局样式（尺寸全部来自 <see cref="Tokens"/>）。</param>
        /// <param name="fonts">字体解析器（本模板不含文本测量）。</param>
        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            HotWorkTicketDocument doc = Cast(document);
            HotWorkTicketStyle tokens = Tokens;

            WorkTicketSheetSpec spec = CreateSpec(doc, tokens);
            WorkTicketLayout layout = WorkTicketSheet.Build(spec, tokens.Ticket, tokens.Grid);

            return ChartGeometry.ForLayout(layout.Bounds, layout.Bounds, layout);
        }

        /// <summary>按文档与令牌构造票面输入（供测试核对联次字段差异，不改文档）。</summary>
        /// <param name="document">特殊作业票文档。</param>
        /// <param name="tokens">票面令牌。</param>
        public static WorkTicketSheetSpec CreateSpec(HotWorkTicketDocument document, HotWorkTicketStyle tokens)
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
                HotWorkTicketFields.WorkUnit,
                HotWorkTicketFields.WorkPlace,
                HotWorkTicketFields.WorkContent,
                HotWorkTicketFields.WorkLeader,
                HotWorkTicketFields.Workers,
                HotWorkTicketFields.Guardian,
                HotWorkTicketFields.PlanStart,
                HotWorkTicketFields.PlanEnd,
            };

            List<string> workKeys = new List<string>(baseKeys)
            {
                HotWorkTicketFields.WorkLevel,
                HotWorkTicketFields.GasTest,
            };

            List<string> guardianKeys = new List<string>(baseKeys)
            {
                HotWorkTicketFields.WorkLevel,
                HotWorkTicketFields.Approver,
            };

            List<WorkTicketCopySpec> copies = new List<WorkTicketCopySpec>
            {
                new WorkTicketCopySpec(tokens.WorkCopyKey, tokens.WorkCopyLabel, workKeys),
                new WorkTicketCopySpec(tokens.GuardianCopyKey, tokens.GuardianCopyLabel, guardianKeys),
            };

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

            List<string> hazards = new List<string>(document.Hazards.Count);
            foreach (string hazard in document.Hazards)
            {
                hazards.Add(hazard);
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
                Subtitle = tokens.SubtitlePrefix + HotWorkCategories.Label(document.Category),
                UnitLine = document.UnitName,
                SerialText = document.SerialNo,
                StampText = document.StampText,
                Secondary = new WorkTicketRowBlock(tokens.HazardsTitle, hazards),
            };
        }

        /// <summary>把票面画到画布上；所有落点取自 Measure 产出的 <see cref="WorkTicketLayout"/>。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="geometry">Measure 的返回值。</param>
        /// <param name="document">特殊作业票文档。</param>
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
                    "几何里没有 WorkTicketLayout：请用 HotWorkTicketTemplate.Measure 产出的几何。");

            WorkTicketSheet.Draw(painter, layout, Tokens.Ticket, Tokens.Grid, Tokens.Palette);
        }

        private static HotWorkTicketDocument Cast(ChartDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            HotWorkTicketDocument? doc = document as HotWorkTicketDocument;
            if (doc == null)
            {
                throw new ArgumentException(
                    "八类特殊作业票模板只接受 HotWorkTicketDocument，实到 "
                    + document.GetType().FullName + "。", nameof(document));
            }
            return doc;
        }
    }
}
