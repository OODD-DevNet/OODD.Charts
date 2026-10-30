using OODD.Charts.Medical.CancerPsychologyConsult;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>癌症心理服务会诊回覆样例：乳腺癌术后化疗患者的焦虑与失眠会诊。</summary>
    public static class CancerPsychologyConsultSample
    {
        public static CancerPsychologyConsultDocument Build()
        {
            CancerPsychologyConsultDocument doc = new CancerPsychologyConsultDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "会诊单号：HZ-2026-0318",
            };

            doc.Patient.Name = "王丽";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "46";
            doc.Patient.Department = "肿瘤内科";
            doc.Patient.Ward = "三病区";
            doc.Patient.BedNumber = "12床";
            doc.Patient.AdmissionNumber = "2026030508";
            doc.Patient.AdmissionDate = "2026-03-05";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "乳腺浸润性导管癌术后"));

            doc.SetCell(CancerPsychologyConsultSections.Application, "basic", 0, "肿瘤内科");
            doc.SetCell(CancerPsychologyConsultSections.Application, "basic", 1, "李敏");
            doc.SetCell(CancerPsychologyConsultSections.Application, "basic", 2, "2026-03-18 09:20");
            doc.Section(CancerPsychologyConsultSections.Application)!.Note =
                "会诊目的：患者确诊乳腺癌术后第 2 周期化疗，近两周持续失眠、夜间惊恐发作，反复询问“是不是治不好了”，请协助评估并制定干预方案。";

            doc.SetCell(CancerPsychologyConsultSections.Assessment, "mood", 1, "焦虑、低落");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "mood", 2, "患者自述，家属补充");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "anxiety", 1, "62 分");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "anxiety", 2, "SAS 中度焦虑");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "depression", 1, "58 分");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "depression", 2, "SDS 轻度抑郁");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "sleep", 1, "14 分");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "sleep", 2, "PSQI 睡眠质量差");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "risk", 1, "低");
            doc.SetCell(CancerPsychologyConsultSections.Assessment, "risk", 2, "无自伤计划，有家属陪护");

            doc.SetCell(CancerPsychologyConsultSections.Opinion, "diagnosis", 0,
                "化疗相关焦虑障碍伴失眠，未达抑郁发作诊断标准。");
            doc.SetCell(CancerPsychologyConsultSections.Opinion, "diagnosis", 1,
                "1. 每晚睡前正念呼吸训练 10 分钟，连续两周；\n" +
                "2. 与主管医师沟通，化疗前 30 分钟安排一次简短心理疏导；\n" +
                "3. 睡眠卫生宣教，必要时短期使用唑吡坦 5mg；\n" +
                "4. 一周后复评 SAS / PSQI，出现自伤念头立即呼叫心理科。");

            doc.SetCell(CancerPsychologyConsultSections.Signature, "sign", 0, "李敏");
            doc.SetCell(CancerPsychologyConsultSections.Signature, "sign", 1, "2026-03-18 09:25");
            doc.SetCell(CancerPsychologyConsultSections.Signature, "sign", 2, "周宁");
            doc.SetCell(CancerPsychologyConsultSections.Signature, "sign", 3, "2026-03-18 14:10");

            return doc;
        }
    }
}
