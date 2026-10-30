using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.WorkloadBook
{
    /// <summary>工作量统计本区段标识。</summary>
    public static class WorkloadBookSections
    {
        public const string Workload = "workload";
        public const string Notes = "notes";
        public const string Signature = "signature";
    }

    /// <summary>工作量统计本行标识，供样例按名回填。</summary>
    public static class WorkloadBookRows
    {
        public const string Intravenous = "intravenous";
        public const string Intramuscular = "intramuscular";
        public const string IndwellingNeedle = "indwelling-needle";
        public const string Catheterization = "catheterization";
        public const string Suction = "suction";
        public const string OralCare = "oral-care";
        public const string PressureUlcer = "pressure-ulcer";
        public const string CriticalCare = "critical-care";
        public const string Total = "total";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 病区护理工作量统计本：按日登记各项护理操作次数，右侧列合计，
    /// 末行汇总每日总量，供护士长核对排班与绩效工作量。
    /// </summary>
    public sealed class WorkloadBookDocument : FormSheetDocument
    {
        public WorkloadBookDocument()
        {
            FormTitle = "工作量统计本";
            FooterNote = "每日由责任护士登记，护士长周末汇总核对后归档。";

            AddSection(new FormSection("一、护理工作量统计（2026-03-01 至 03-07）",
                new FormColumn("项目", 1.6f, CellAlign.Near),
                new FormColumn("03-01", 0.8f),
                new FormColumn("03-02", 0.8f),
                new FormColumn("03-03", 0.8f),
                new FormColumn("03-04", 0.8f),
                new FormColumn("03-05", 0.8f),
                new FormColumn("03-06", 0.8f),
                new FormColumn("03-07", 0.8f),
                new FormColumn("合计", 0.9f))
            {
                Key = WorkloadBookSections.Workload,
                RowHeight = 28f,
            });
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.Intravenous, "静脉输液");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.Intramuscular, "肌肉注射");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.IndwellingNeedle, "留置针穿刺");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.Catheterization, "导尿");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.Suction, "吸痰");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.OralCare, "口腔护理");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.PressureUlcer, "压疮换药");
            AddRow(WorkloadBookSections.Workload, WorkloadBookRows.CriticalCare, "危重患者护理");
            FormRow total = AddRow(WorkloadBookSections.Workload, WorkloadBookRows.Total, "合计");
            total.IsTotal = true;

            AddSection(new FormSection("二、统计说明",
                new FormColumn("统计口径", 1f, CellAlign.Near))
            {
                Key = WorkloadBookSections.Notes,
            });

            AddSection(new FormSection("三、签名",
                new FormColumn("统计人", 1.6f),
                new FormColumn("护士长审核", 1.6f),
                new FormColumn("日期", 1.6f))
            {
                Key = WorkloadBookSections.Signature,
                RowHeight = 32f,
            });
            AddRow(WorkloadBookSections.Signature, WorkloadBookRows.Sign);
        }
    }

    /// <summary>工作量统计本版式：项目列 + 七个日期列 + 合计列，横向台账。</summary>
    public sealed class WorkloadBookTemplate : FormSheetTemplate
    {
        public WorkloadBookTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.workload-book";

        public override string DisplayName => "工作量统计本";
    }
}
