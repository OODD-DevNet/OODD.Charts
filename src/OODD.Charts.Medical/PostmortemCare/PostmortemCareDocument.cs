using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.PostmortemCare
{
    /// <summary>尸体护理 / 遗体转运记录区段标识。</summary>
    public static class PostmortemCareSections
    {
        public const string Death = "death";
        public const string Care = "care";
        public const string Handover = "handover";
        public const string Signature = "signature";
    }

    /// <summary>尸体护理 / 遗体转运记录行标识，供样例按名回填。</summary>
    public static class PostmortemCareRows
    {
        public const string DeathTime = "death-time";
        public const string Diagnosis = "diagnosis";
        public const string Cause = "cause";
        public const string FamilyConfirm = "family-confirm";

        public const string Clean = "clean";
        public const string CloseEyes = "close-eyes";
        public const string FixLimbs = "fix-limbs";
        public const string ChangeClothes = "change-clothes";
        public const string Wound = "wound";
        public const string Wristband = "wristband";

        public const string Handover = "handover";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 尸体护理 / 遗体转运记录：死亡信息、六项尸体护理操作、太平间交接
    /// 以及执行护士与护士长签名，一遗体一单随遗体流转。
    /// </summary>
    public sealed class PostmortemCareDocument : FormSheetDocument
    {
        public PostmortemCareDocument()
        {
            FormTitle = "尸体护理/遗体转运记录";
            FooterNote = "本记录随遗体交接单一并存档，保存三年。";

            AddSection(new FormSection("一、死亡信息",
                new FormColumn("项目", 1f, CellAlign.Near),
                new FormColumn("内容", 2.4f, CellAlign.Near))
            {
                Key = PostmortemCareSections.Death,
                RowHeight = 30f,
            });
            AddRow(PostmortemCareSections.Death, PostmortemCareRows.DeathTime, "死亡时间");
            AddRow(PostmortemCareSections.Death, PostmortemCareRows.Diagnosis, "死亡诊断");
            AddRow(PostmortemCareSections.Death, PostmortemCareRows.Cause, "死亡原因");
            AddRow(PostmortemCareSections.Death, PostmortemCareRows.FamilyConfirm, "家属确认");

            AddSection(new FormSection("二、尸体护理项目",
                new FormColumn("项目", 1.6f, CellAlign.Near),
                new FormColumn("完成情况", 1f),
                new FormColumn("执行人", 1f))
            {
                Key = PostmortemCareSections.Care,
                RowHeight = 30f,
            });
            AddRow(PostmortemCareSections.Care, PostmortemCareRows.Clean, "清洁遗体");
            AddRow(PostmortemCareSections.Care, PostmortemCareRows.CloseEyes, "闭合口眼");
            AddRow(PostmortemCareSections.Care, PostmortemCareRows.FixLimbs, "固定四肢");
            AddRow(PostmortemCareSections.Care, PostmortemCareRows.ChangeClothes, "更换衣物");
            AddRow(PostmortemCareSections.Care, PostmortemCareRows.Wound, "伤口处理");
            AddRow(PostmortemCareSections.Care, PostmortemCareRows.Wristband, "标识腕带");

            AddSection(new FormSection("三、遗体交接",
                new FormColumn("交接时间", 1.2f),
                new FormColumn("接收单位", 1.6f),
                new FormColumn("接收人", 1f),
                new FormColumn("护送人", 1f))
            {
                Key = PostmortemCareSections.Handover,
                RowHeight = 30f,
            });
            AddRow(PostmortemCareSections.Handover, PostmortemCareRows.Handover);

            AddSection(new FormSection("四、签名",
                new FormColumn("执行护士", 1.6f),
                new FormColumn("护士长", 1.6f),
                new FormColumn("日期", 1.6f))
            {
                Key = PostmortemCareSections.Signature,
                RowHeight = 32f,
            });
            AddRow(PostmortemCareSections.Signature, PostmortemCareRows.Sign);
        }
    }

    /// <summary>尸体护理 / 遗体转运记录版式：A4 纵向，死亡信息 + 护理项目 + 交接 + 签名。</summary>
    public sealed class PostmortemCareTemplate : FormSheetTemplate
    {
        public PostmortemCareTemplate()
        {
            Tokens.Width = 794f;
        }

        public override string Key => "medical.postmortem-care";

        public override string DisplayName => "尸体护理/遗体转运记录";
    }
}
