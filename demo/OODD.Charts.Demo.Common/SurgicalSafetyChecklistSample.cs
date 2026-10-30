using OODD.Charts.Medical.SurgicalSafetyChecklist;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>手术安全核查表样例：52 岁女性腹腔镜胆囊切除术，三方核查均已完成。</summary>
    public static class SurgicalSafetyChecklistSample
    {
        public static SurgicalSafetyChecklistDocument Build()
        {
            SurgicalSafetyChecklistDocument doc = new SurgicalSafetyChecklistDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "手术日期：2026-03-16　手术间：OR-3",
            };

            doc.Patient.Name = "张慧敏";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "52";
            doc.Patient.Department = "普外科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "15床";
            doc.Patient.AdmissionNumber = "2026031421";
            doc.Patient.AdmissionDate = "2026-03-14";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "胆囊结石伴慢性胆囊炎"));
            doc.Patient.ExtraFields.Add(new HeaderField("operation", "手术名称", "腹腔镜胆囊切除术"));

            // 一、麻醉前（Sign In）
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.PatientIdentity, 1,
                "√ 是（与腕带、病历一致）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.PatientIdentity, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.SiteMark, 1,
                "√ 是（脐部及右上腹已标记）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.SiteMark, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Consent, 1,
                "√ 已核对（同意书齐全）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Consent, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.AnesthesiaRisk, 1,
                "√ 是（ASA II 级）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.AnesthesiaRisk, 2, "孙瑶");

            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Allergy, 1,
                "√ 无（否认药物过敏）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Allergy, 2, "孙瑶");

            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Airway, 1,
                "√ 无（Mallampati II 级）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.Airway, 2, "孙瑶");

            // 二、切皮前（Time Out）
            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.TeamIntro, 1,
                "√ 是（四方已自我介绍）");
            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.TeamIntro, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Procedure, 1,
                "√ 已核对（腹腔镜胆囊切除）");
            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Procedure, 2, "陈立");

            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Imaging, 1,
                "√ 已核对（CT、MRCP 已上屏）");
            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Imaging, 2, "陈立");

            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Antibiotic, 1,
                "√ 是（头孢呋辛 1.5g 已用）");
            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Antibiotic, 2, "孙瑶");

            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Sterile, 1,
                "√ 已核对（灭菌指示合格）");
            doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Sterile, 2, "王芳");

            // 三、离室前（Sign Out）
            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.ProcedureName, 1,
                "√ 已核对（腹腔镜胆囊切除）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.ProcedureName, 2, "陈立");

            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Specimen, 1,
                "√ 是（胆囊标本已送检）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Specimen, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Counts, 1,
                "√ 已核对（器械敷料相符）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Counts, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Skin, 1,
                "√ 是（皮肤完整无压红）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Skin, 2, "王芳");

            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Lines, 1,
                "√ 是（腹腔引流管 1 根）");
            doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Lines, 2, "王芳");

            // 四、三方签名
            doc.SetCell(SurgicalSafetyChecklistSections.Signature, SurgicalSafetyChecklistRows.Sign, 0, "陈立");
            doc.SetCell(SurgicalSafetyChecklistSections.Signature, SurgicalSafetyChecklistRows.Sign, 1, "孙瑶");
            doc.SetCell(SurgicalSafetyChecklistSections.Signature, SurgicalSafetyChecklistRows.Sign, 2, "王芳");
            doc.SetCell(SurgicalSafetyChecklistSections.Signature, SurgicalSafetyChecklistRows.Sign, 3,
                "2026-03-16 08:40");

            return doc;
        }
    }
}
