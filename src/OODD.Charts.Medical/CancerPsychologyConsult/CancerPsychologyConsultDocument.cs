using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.CancerPsychologyConsult
{
    /// <summary>区段与行标识，供样例回填。</summary>
    public static class CancerPsychologyConsultSections
    {
        public const string Application = "application";
        public const string Assessment = "assessment";
        public const string Opinion = "opinion";
        public const string Signature = "signature";
    }

    /// <summary>
    /// 癌症心理服务会诊回覆。跨科室往来文书：上半为申请目的，
    /// 中段为量表评估，下半为诊断意见与处置建议，末尾双方签名。
    /// </summary>
    public sealed class CancerPsychologyConsultDocument : FormSheetDocument
    {
        public CancerPsychologyConsultDocument()
        {
            FormTitle = "癌症心理服务会诊回覆";
            FooterNote = "本单一式两份，申请科室与心理科各存一份。";

            FormSection application = AddSection(new FormSection("一、会诊申请",
                new FormColumn("申请科室", 1.2f),
                new FormColumn("申请医师", 1f),
                new FormColumn("申请时间", 1.4f))
            {
                Key = CancerPsychologyConsultSections.Application,
                RowHeight = 30f,
            });
            AddRow(CancerPsychologyConsultSections.Application, "basic", null, null, null);

            FormSection assessment = AddSection(new FormSection("二、心理评估",
                new FormColumn("评估项目", 1f),
                new FormColumn("评估结果", 1.5f),
                new FormColumn("评估依据 / 说明", 2f))
            {
                Key = CancerPsychologyConsultSections.Assessment,
                RowHeight = 30f,
            });
            AddRow(CancerPsychologyConsultSections.Assessment, "mood", "情绪状态");
            AddRow(CancerPsychologyConsultSections.Assessment, "anxiety", "焦虑（SAS）");
            AddRow(CancerPsychologyConsultSections.Assessment, "depression", "抑郁（SDS）");
            AddRow(CancerPsychologyConsultSections.Assessment, "sleep", "睡眠质量（PSQI）");
            AddRow(CancerPsychologyConsultSections.Assessment, "risk", "自杀 / 自伤风险");

            FormSection opinion = AddSection(new FormSection("三、会诊意见",
                new FormColumn("会诊诊断", 1.2f),
                new FormColumn("处置建议", 2.4f))
            {
                Key = CancerPsychologyConsultSections.Opinion,
                RowHeight = 30f,
            });
            AddRow(CancerPsychologyConsultSections.Opinion, "diagnosis");

            FormSection signature = AddSection(new FormSection("四、签名",
                new FormColumn("申请医师", 1.4f),
                new FormColumn("签名时间", 1f),
                new FormColumn("会诊医师", 1.4f),
                new FormColumn("签名时间", 1f))
            {
                Key = CancerPsychologyConsultSections.Signature,
                RowHeight = 34f,
            });
            AddRow(CancerPsychologyConsultSections.Signature, "sign", null, null, null, null);
        }
    }

    /// <summary>会诊回覆版式：A4 纵向，四区段竖排。</summary>
    public sealed class CancerPsychologyConsultTemplate : FormSheetTemplate
    {
        public CancerPsychologyConsultTemplate()
        {
            Tokens.Width = 794f;
        }

        public override string Key => "medical.cancer-psychology-consult";

        public override string DisplayName => "癌症心理服务会诊回覆";
    }
}
