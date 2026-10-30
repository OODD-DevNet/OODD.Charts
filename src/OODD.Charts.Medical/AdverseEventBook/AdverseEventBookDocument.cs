using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.AdverseEventBook
{
    /// <summary>不良事件登记本区段标识。</summary>
    public static class AdverseEventBookSections
    {
        public const string Event = "event";
        public const string Course = "course";
        public const string Analysis = "analysis";
        public const string Signature = "signature";
    }

    /// <summary>不良事件登记本行标识，供样例按名回填。</summary>
    public static class AdverseEventBookRows
    {
        public const string Event = "event-1";

        public const string Corrective = "corrective";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 不良事件登记本：一条事件占一页，依次登记事件要素、经过、
    /// 原因分析与整改闭环，末尾由科护士长与护理部审核签名。
    /// </summary>
    public sealed class AdverseEventBookDocument : FormSheetDocument
    {
        public AdverseEventBookDocument()
        {
            FormTitle = "不良事件登记本";
            FooterNote = "本表一事一记，Ⅱ级及以上事件 24 小时内上报护理部。";

            AddSection(new FormSection("一、事件信息",
                new FormColumn("事件编号", 1f),
                new FormColumn("发生日期", 1f),
                new FormColumn("事件类型", 1.4f),
                new FormColumn("分级", 0.8f),
                new FormColumn("涉及患者", 1f),
                new FormColumn("报告人", 1f))
            {
                Key = AdverseEventBookSections.Event,
                RowHeight = 30f,
            });
            AddRow(AdverseEventBookSections.Event, AdverseEventBookRows.Event);

            AddSection(new FormSection("二、事件经过",
                new FormColumn("经过记录", 1f, CellAlign.Near))
            {
                Key = AdverseEventBookSections.Course,
            });

            AddSection(new FormSection("三、原因分析与整改",
                new FormColumn("原因分析", 2f, CellAlign.Near),
                new FormColumn("整改措施", 2.4f, CellAlign.Near),
                new FormColumn("责任人", 1f),
                new FormColumn("完成期限", 1f),
                new FormColumn("闭环状态", 0.9f))
            {
                Key = AdverseEventBookSections.Analysis,
                RowHeight = 32f,
            });
            AddRow(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective);

            AddSection(new FormSection("四、审核签名",
                new FormColumn("科护士长", 1.6f),
                new FormColumn("护理部", 1.6f),
                new FormColumn("日期", 1.6f))
            {
                Key = AdverseEventBookSections.Signature,
                RowHeight = 32f,
            });
            AddRow(AdverseEventBookSections.Signature, AdverseEventBookRows.Sign);
        }
    }

    /// <summary>不良事件登记本版式：事件要素 + 经过叙述 + 整改闭环 + 审核签名。</summary>
    public sealed class AdverseEventBookTemplate : FormSheetTemplate
    {
        public AdverseEventBookTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.adverse-event-book";

        public override string DisplayName => "不良事件登记本";
    }
}
