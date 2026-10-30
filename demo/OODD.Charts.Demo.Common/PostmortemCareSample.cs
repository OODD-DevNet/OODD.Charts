using OODD.Charts.Medical.PostmortemCare;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>尸体护理 / 遗体转运记录样例：晚期肺癌患者安宁疗护病区死亡，遗体转运至太平间。</summary>
    public static class PostmortemCareSample
    {
        public static PostmortemCareDocument Build()
        {
            PostmortemCareDocument doc = new PostmortemCareDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "记录编号：ST-2026-013",
            };

            doc.Patient.Name = "李桂芳";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "72";
            doc.Patient.Department = "肿瘤内科";
            doc.Patient.Ward = "安宁疗护病区";
            doc.Patient.BedNumber = "6床";
            doc.Patient.AdmissionNumber = "2026021809";
            doc.Patient.AdmissionDate = "2026-02-18";
            doc.Patient.ExtraFields.Add(new HeaderField("death-date", "死亡日期", "2026-03-13"));
            doc.Patient.ExtraFields.Add(new HeaderField("destination", "遗体去向", "太平间"));

            doc.SetCell(PostmortemCareSections.Death, PostmortemCareRows.DeathTime, 1,
                "2026-03-13 23:40");
            doc.SetCell(PostmortemCareSections.Death, PostmortemCareRows.Diagnosis, 1,
                "右肺腺癌Ⅳ期伴多发转移");
            doc.SetCell(PostmortemCareSections.Death, PostmortemCareRows.Cause, 1,
                "呼吸循环衰竭");
            doc.SetCell(PostmortemCareSections.Death, PostmortemCareRows.FamilyConfirm, 1,
                "配偶张建国、女儿张丽在场，确认死亡并签署知情同意书");

            AddCare(doc, PostmortemCareRows.Clean, "√ 已完成", "王芳");
            AddCare(doc, PostmortemCareRows.CloseEyes, "√ 已完成", "王芳");
            AddCare(doc, PostmortemCareRows.FixLimbs, "√ 已完成", "王芳");
            AddCare(doc, PostmortemCareRows.ChangeClothes, "√ 已完成", "刘娜");
            AddCare(doc, PostmortemCareRows.Wound, "√ 已处理", "王芳");
            AddCare(doc, PostmortemCareRows.Wristband, "√ 已核对", "刘娜");

            doc.SetCell(PostmortemCareSections.Handover, PostmortemCareRows.Handover, 0,
                "2026-03-14 00:20");
            doc.SetCell(PostmortemCareSections.Handover, PostmortemCareRows.Handover, 1,
                "太平间（后勤保障科）");
            doc.SetCell(PostmortemCareSections.Handover, PostmortemCareRows.Handover, 2, "赵师傅");
            doc.SetCell(PostmortemCareSections.Handover, PostmortemCareRows.Handover, 3,
                "王芳、刘娜");

            doc.SetCell(PostmortemCareSections.Signature, PostmortemCareRows.Sign, 0, "王芳");
            doc.SetCell(PostmortemCareSections.Signature, PostmortemCareRows.Sign, 1, "周敏");
            doc.SetCell(PostmortemCareSections.Signature, PostmortemCareRows.Sign, 2, "2026-03-14");

            return doc;
        }

        private static void AddCare(PostmortemCareDocument doc, string rowKey, string status,
            string executor)
        {
            doc.SetCell(PostmortemCareSections.Care, rowKey, 1, status);
            doc.SetCell(PostmortemCareSections.Care, rowKey, 2, executor);
        }
    }
}
