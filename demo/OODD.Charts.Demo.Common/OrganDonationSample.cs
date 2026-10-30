using OODD.Charts.Medical.OrganDonation;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>器官捐献过程记录样例：35 岁男性颅脑外伤脑死亡捐献者，捐献肝脏与双肾。</summary>
    public static class OrganDonationSample
    {
        public static OrganDonationDocument Build()
        {
            OrganDonationDocument doc = new OrganDonationDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "记录编号：OD-2026-006",
            };

            doc.Patient.Name = "周建军";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "35";
            doc.Patient.Department = "重症医学科";
            doc.Patient.Ward = "ICU";
            doc.Patient.BedNumber = "3床";
            doc.Patient.AdmissionNumber = "2026031804";
            doc.Patient.AdmissionDate = "2026-03-18";
            doc.Patient.ExtraFields.Add(new HeaderField("donation-no", "捐献编号", "OD-2026-006"));
            doc.Patient.ExtraFields.Add(new HeaderField("opo", "OPO", "省人体器官捐献办公室"));

            doc.SetCell(OrganDonationSections.Donor, OrganDonationRows.Age, 1, "35 岁，男性");
            doc.SetCell(OrganDonationSections.Donor, OrganDonationRows.Disease, 1,
                "重型颅脑外伤（车祸伤）后脑疝形成");
            doc.SetCell(OrganDonationSections.Donor, OrganDonationRows.Assessment, 1,
                "经两次脑死亡判定（2026-03-21 08:30、14:00）结果一致，符合捐献条件；" +
                "家属自愿无偿捐献肝脏及双肾，器官功能良好。");
            doc.SetCell(OrganDonationSections.Donor, OrganDonationRows.Family, 1,
                "配偶王丽（知情同意书签署人）、父亲周德海");

            AddStep(doc, OrganDonationRows.Evaluation, "2026-03-21 09:10",
                "OPO 协调员 陈立", "评估通过", "器官功能良好，无捐献禁忌");
            AddStep(doc, OrganDonationRows.Consent, "2026-03-21 11:00",
                "协调员 陈立、ICU 主任 王强", "同意捐献", "配偶及父母共同签署，见证人在场");
            AddStep(doc, OrganDonationRows.Ethics, "2026-03-21 15:30",
                "伦理委员会 张敏", "审批通过", "伦理审查编号 LL-2026-021");
            AddStep(doc, OrganDonationRows.Procurement, "2026-03-22 02:10",
                "获取医师 李强", "获取完成", "肝脏 1 例、肾脏 2 例，热缺血 8 分钟");
            AddStep(doc, OrganDonationRows.Allocation, "2026-03-22 03:20",
                "分配系统 刘洋", "分配完成", "肝脏至省人民医院，肾脏至市第一医院");
            AddStep(doc, OrganDonationRows.Transport, "2026-03-22 04:10",
                "转运员 赵磊", "已发出", "UW 液灌注，冷缺血计时开始");
            AddStep(doc, OrganDonationRows.Transplant, "2026-03-22 10:30",
                "接收医院 王主任", "移植成功", "肝脏血流开放，尿量正常");

            doc.Section(OrganDonationSections.Handover)!.Note =
                "肝脏经 UW 液灌注后低温转运，预计冷缺血时间 6 小时 20 分；" +
                "交接双方核对器官袋编号与血型，签字确认。";

            doc.SetCell(OrganDonationSections.Signature, OrganDonationRows.Sign, 0, "陈立");
            doc.SetCell(OrganDonationSections.Signature, OrganDonationRows.Sign, 1, "李强");
            doc.SetCell(OrganDonationSections.Signature, OrganDonationRows.Sign, 2, "王主任");
            doc.SetCell(OrganDonationSections.Signature, OrganDonationRows.Sign, 3, "2026-03-22");

            return doc;
        }

        private static void AddStep(OrganDonationDocument doc, string rowKey, string time,
            string owner, string result, string remark)
        {
            doc.SetCell(OrganDonationSections.Chain, rowKey, 1, time);
            doc.SetCell(OrganDonationSections.Chain, rowKey, 2, owner);
            doc.SetCell(OrganDonationSections.Chain, rowKey, 3, result);
            doc.SetCell(OrganDonationSections.Chain, rowKey, 4, remark);
        }
    }
}
