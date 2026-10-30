using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.HyperbaricEducation
{
    /// <summary>区段标识，供样例回填。</summary>
    public static class HyperbaricEducationSections
    {
        public const string Info = "info";
        public const string Teaching = "teaching";
        public const string Contraindication = "contraindication";
        public const string Signature = "signature";
    }

    /// <summary>行标识，供样例按名回填。</summary>
    public static class HyperbaricEducationRows
    {
        public const string Cabin = "cabin";
        public const string Session = "session";
        public const string FirstEntry = "first-entry";
        public const string Escort = "escort";

        public const string Preparation = "preparation";
        public const string EarPressure = "ear-pressure";
        public const string ForbiddenItems = "forbidden-items";
        public const string OxygenToxicity = "oxygen-toxicity";
        public const string EmergencyCall = "emergency-call";
        public const string PostObservation = "post-observation";

        public const string Absolute = "absolute";
        public const string Relative = "relative";
        public const string Conclusion = "conclusion";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 高压氧进舱宣教记录单。适用于首次或再次进舱前的患者/家属宣教：
    /// 记录舱次与陪舱方式、六项核心宣教要点及掌握情况、禁忌症排查结论，末尾双方签名。
    /// </summary>
    public sealed class HyperbaricEducationDocument : FormSheetDocument
    {
        public HyperbaricEducationDocument()
        {
            FormTitle = "高压氧进舱宣教记录单";
            FooterNote = "本单一式两份，高压氧科与病历各存一份；宣教内容已逐项口述并经患者/家属确认。";

            AddSection(new FormSection("一、进舱信息",
                new FormColumn("项目", 1f, CellAlign.Center, true),
                new FormColumn("内容", 2.2f, CellAlign.Near))
            {
                Key = HyperbaricEducationSections.Info,
                RowHeight = 30f,
            });
            AddRow(HyperbaricEducationSections.Info, HyperbaricEducationRows.Cabin, "舱号");
            AddRow(HyperbaricEducationSections.Info, HyperbaricEducationRows.Session, "舱次");
            AddRow(HyperbaricEducationSections.Info, HyperbaricEducationRows.FirstEntry, "首次进舱");
            AddRow(HyperbaricEducationSections.Info, HyperbaricEducationRows.Escort, "陪舱方式");

            AddSection(new FormSection("二、宣教内容",
                new FormColumn("宣教项目", 1.3f, CellAlign.Center, true),
                new FormColumn("内容要点", 3f, CellAlign.Near),
                new FormColumn("掌握情况", 1f, CellAlign.Center))
            {
                Key = HyperbaricEducationSections.Teaching,
                RowHeight = 30f,
            });
            AddRow(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.Preparation, "进舱前准备");
            AddRow(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EarPressure, "耳压平衡方法");
            AddRow(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.ForbiddenItems, "舱内禁带物品");
            AddRow(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.OxygenToxicity, "氧中毒先兆");
            AddRow(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EmergencyCall, "紧急呼叫方式");
            AddRow(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.PostObservation, "出舱后观察");

            AddSection(new FormSection("三、禁忌症确认",
                new FormColumn("项目", 1.3f, CellAlign.Center, true),
                new FormColumn("确认结果", 3f, CellAlign.Near))
            {
                Key = HyperbaricEducationSections.Contraindication,
                RowHeight = 30f,
            });
            AddRow(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Absolute,
                "绝对禁忌症（未经处理的气胸等）");
            AddRow(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Relative,
                "相对禁忌症（重度上感、幽闭恐惧）");
            AddRow(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Conclusion,
                "当前结论");

            AddSection(new FormSection("四、签名",
                new FormColumn("患者/家属", 1.6f, CellAlign.Center, true),
                new FormColumn("宣教护士", 1.6f, CellAlign.Center, true),
                new FormColumn("宣教时间", 1.6f, CellAlign.Center, true))
            {
                Key = HyperbaricEducationSections.Signature,
                RowHeight = 34f,
            });
            AddRow(HyperbaricEducationSections.Signature, HyperbaricEducationRows.Sign);
        }
    }

    /// <summary>进舱宣教版式：A4 纵向，四区段竖排。</summary>
    public sealed class HyperbaricEducationTemplate : FormSheetTemplate
    {
        public HyperbaricEducationTemplate()
        {
            Tokens.Width = 794f;
        }

        public override string Key => "medical.hyperbaric-education";

        public override string DisplayName => "高压氧进舱宣教记录单";
    }
}
