using OODD.Charts.Medical.AdverseEventBook;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>不良事件登记本样例：老年患者夜间如厕跌倒，已整改闭环。</summary>
    public static class AdverseEventBookSample
    {
        public static AdverseEventBookDocument Build()
        {
            AdverseEventBookDocument doc = new AdverseEventBookDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "登记号：AE-2026-017",
            };

            doc.Patient.Name = "王秀兰";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "78";
            doc.Patient.Department = "老年医学科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "18床";
            doc.Patient.AdmissionNumber = "2026030206";
            doc.Patient.AdmissionDate = "2026-03-02";
            doc.Patient.ExtraFields.Add(new HeaderField("level", "事件级别", "Ⅱ级"));

            doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 0, "AE-2026-017");
            doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 1, "2026-03-11");
            doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 2, "跌倒坠床");
            doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 3, "Ⅱ级");
            doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 4, "王秀兰");
            doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 5, "陈静");

            doc.Section(AdverseEventBookSections.Course)!.Note =
                "患者夜间如厕时于床旁跌倒，左膝着地，无骨折，生命体征平稳；立即评估并通知值班医师与家属。";

            doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 0,
                "夜间照明不足、陪护离岗");
            doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 1,
                "加装夜灯、加强宣教与巡视");
            doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 2,
                "护士长 周敏");
            doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 3,
                "2026-03-20");
            doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 4,
                "√ 已闭环");

            doc.SetCell(AdverseEventBookSections.Signature, AdverseEventBookRows.Sign, 0, "周敏");
            doc.SetCell(AdverseEventBookSections.Signature, AdverseEventBookRows.Sign, 1, "赵敏");
            doc.SetCell(AdverseEventBookSections.Signature, AdverseEventBookRows.Sign, 2, "2026-03-21");

            return doc;
        }
    }
}
