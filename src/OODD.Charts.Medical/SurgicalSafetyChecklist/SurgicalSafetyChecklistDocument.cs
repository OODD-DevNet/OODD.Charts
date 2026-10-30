using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.SurgicalSafetyChecklist
{
    /// <summary>三阶段核查区段与签名区的稳定标识，供样例回填。</summary>
    public static class SurgicalSafetyChecklistSections
    {
        public const string SignIn = "signin";
        public const string TimeOut = "timeout";
        public const string SignOut = "signout";
        public const string Signature = "signature";
    }

    /// <summary>各核查条目的行标识。</summary>
    public static class SurgicalSafetyChecklistRows
    {
        public const string PatientIdentity = "patient-identity";
        public const string SiteMark = "site-mark";
        public const string Consent = "consent";
        public const string AnesthesiaRisk = "anesthesia-risk";
        public const string Allergy = "allergy";
        public const string Airway = "airway";

        public const string TeamIntro = "team-intro";
        public const string Procedure = "procedure";
        public const string Imaging = "imaging";
        public const string Antibiotic = "antibiotic";
        public const string Sterile = "sterile";

        public const string ProcedureName = "procedure-name";
        public const string Specimen = "specimen";
        public const string Counts = "counts";
        public const string Skin = "skin";
        public const string Lines = "lines";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 手术护理查核單 / 手术安全核查表。按 WHO 三联式核查流程编排：
    /// 麻醉前（Sign In）、切皮前（Time Out）、离室前（Sign Out），末尾三方签名。
    /// 核查项目与列结构固定，结果与签名由样例或业务方回填。
    /// </summary>
    public sealed class SurgicalSafetyChecklistDocument : FormSheetDocument
    {
        public SurgicalSafetyChecklistDocument()
        {
            FormTitle = "手术护理查核單/手术安全核查表";
            FooterNote = "本表由手术医师、麻醉医师与巡回护士三方共同核对并签名，随病历归档。";

            AddSection(new FormSection("一、麻醉前（Sign In）",
                new FormColumn("核查项目", 2f),
                new FormColumn("核查结果", 1.4f),
                new FormColumn("核查人", 1f))
            {
                Key = SurgicalSafetyChecklistSections.SignIn,
            });
            AddRow(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.PatientIdentity, "患者身份");
            AddRow(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.SiteMark, "手术部位标记");
            AddRow(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Consent, "知情同意");
            AddRow(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.AnesthesiaRisk, "麻醉风险评估");
            AddRow(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Allergy, "过敏史");
            AddRow(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Airway, "困难气道评估");

            AddSection(new FormSection("二、切皮前（Time Out）",
                new FormColumn("核查项目", 2f),
                new FormColumn("核查结果", 1.4f),
                new FormColumn("核查人", 1f))
            {
                Key = SurgicalSafetyChecklistSections.TimeOut,
            });
            AddRow(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.TeamIntro, "团队介绍");
            AddRow(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Procedure, "手术方式确认");
            AddRow(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Imaging, "影像资料");
            AddRow(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Antibiotic, "预防性抗生素");
            AddRow(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Sterile, "器械灭菌确认");

            AddSection(new FormSection("三、离室前（Sign Out）",
                new FormColumn("核查项目", 2f),
                new FormColumn("核查结果", 1.4f),
                new FormColumn("核查人", 1f))
            {
                Key = SurgicalSafetyChecklistSections.SignOut,
            });
            AddRow(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.ProcedureName, "手术名称确认");
            AddRow(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Specimen, "标本送检");
            AddRow(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Counts, "器械敷料清点");
            AddRow(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Skin, "皮肤完整性");
            AddRow(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Lines, "管路与引流");

            AddSection(new FormSection("四、三方签名",
                new FormColumn("手术医师", 1.4f),
                new FormColumn("麻醉医师", 1.4f),
                new FormColumn("巡回护士", 1.4f),
                new FormColumn("时间", 1.2f))
            {
                Key = SurgicalSafetyChecklistSections.Signature,
                RowHeight = 34f,
            });
            AddRow(SurgicalSafetyChecklistSections.Signature, SurgicalSafetyChecklistRows.Sign);
        }
    }

    /// <summary>手术安全核查表版式：A4 纵向，三段核查 + 三方签名竖排。</summary>
    public sealed class SurgicalSafetyChecklistTemplate : FormSheetTemplate
    {
        public SurgicalSafetyChecklistTemplate()
        {
            Tokens.Width = 794f;
        }

        public override string Key => "medical.surgical-safety-checklist";

        public override string DisplayName => "手术护理查核單/手术安全核查表";
    }
}
