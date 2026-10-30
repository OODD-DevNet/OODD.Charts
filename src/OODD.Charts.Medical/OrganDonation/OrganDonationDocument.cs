using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.OrganDonation
{
    /// <summary>人体器官捐献过程记录表 / 器官分配转运记录单区段标识。</summary>
    public static class OrganDonationSections
    {
        public const string Donor = "donor";
        public const string Chain = "chain";
        public const string Handover = "handover";
        public const string Signature = "signature";
    }

    /// <summary>人体器官捐献过程记录表 / 器官分配转运记录单行标识，供样例按名回填。</summary>
    public static class OrganDonationRows
    {
        public const string Age = "age";
        public const string Disease = "disease";
        public const string Assessment = "assessment";
        public const string Family = "family";

        public const string Evaluation = "evaluation";
        public const string Consent = "consent";
        public const string Ethics = "ethics";
        public const string Procurement = "procurement";
        public const string Allocation = "allocation";
        public const string Transport = "transport";
        public const string Transplant = "transplant";

        public const string Handover = "handover";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 人体器官捐献过程记录表 / 器官分配转运记录单：捐献者信息、
    /// 从评估到移植完成的事件链、低温转运交接说明及四方签名。
    /// </summary>
    public sealed class OrganDonationDocument : FormSheetDocument
    {
        public OrganDonationDocument()
        {
            FormTitle = "人体器官捐献过程记录表/器官分配转运记录单";
            FooterNote = "本表由 OPO 协调员填写，与知情同意书、伦理批件一并归档，永久保存。";

            AddSection(new FormSection("一、捐献者信息",
                new FormColumn("项目", 1f, CellAlign.Near),
                new FormColumn("内容", 2.4f, CellAlign.Near))
            {
                Key = OrganDonationSections.Donor,
                RowHeight = 30f,
            });
            AddRow(OrganDonationSections.Donor, OrganDonationRows.Age, "年龄");
            AddRow(OrganDonationSections.Donor, OrganDonationRows.Disease, "原发疾病");
            AddRow(OrganDonationSections.Donor, OrganDonationRows.Assessment, "评估结论");
            AddRow(OrganDonationSections.Donor, OrganDonationRows.Family, "家属关系");

            AddSection(new FormSection("二、捐献过程事件链",
                new FormColumn("环节", 1.2f),
                new FormColumn("时间", 1.4f),
                new FormColumn("责任人", 1.2f),
                new FormColumn("结果", 1.4f),
                new FormColumn("备注", 1.8f, CellAlign.Near))
            {
                Key = OrganDonationSections.Chain,
                RowHeight = 30f,
            });
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Evaluation, "捐献评估");
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Consent, "家属知情同意");
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Ethics, "伦理审批");
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Procurement, "器官获取");
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Allocation, "分配确认");
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Transport, "低温转运");
            AddRow(OrganDonationSections.Chain, OrganDonationRows.Transplant, "移植完成");

            AddSection(new FormSection("三、转运与交接",
                new FormColumn("转运交接记录", 1f, CellAlign.Near))
            {
                Key = OrganDonationSections.Handover,
            });

            AddSection(new FormSection("四、签名",
                new FormColumn("OPO 协调员", 1.6f),
                new FormColumn("获取医师", 1.6f),
                new FormColumn("接收医院代表", 1.6f),
                new FormColumn("日期", 1.4f))
            {
                Key = OrganDonationSections.Signature,
                RowHeight = 32f,
            });
            AddRow(OrganDonationSections.Signature, OrganDonationRows.Sign);
        }
    }

    /// <summary>器官捐献过程记录版式：捐献者信息 + 七环节事件链 + 转运交接 + 四方签名。</summary>
    public sealed class OrganDonationTemplate : FormSheetTemplate
    {
        public OrganDonationTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.organ-donation";

        public override string DisplayName => "人体器官捐献过程记录表/器官分配转运记录单";
    }
}
