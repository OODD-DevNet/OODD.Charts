using OODD.Charts.Medical.ApacheIi;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>APACHE II 样例：72 岁男性重症肺炎，入 ICU 24 小时评分 44 分。</summary>
    public static class ApacheIiSample
    {
        public static ApacheIiDocument Build()
        {
            ApacheIiDocument doc = new ApacheIiDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "评估时点：入 ICU 24 小时内　评分医师：孙浩",
            };

            doc.Patient.Name = "陈国栋";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "72";
            doc.Patient.Department = "重症医学科";
            doc.Patient.Ward = "重症监护病房";
            doc.Patient.BedNumber = "6床";
            doc.Patient.AdmissionNumber = "2026031407";
            doc.Patient.AdmissionDate = "2026-03-14";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "重症肺炎合并呼吸衰竭"));
            doc.Patient.ExtraFields.Add(new HeaderField("time", "评估时间", "2026-03-16 08:00"));

            Physiology(doc, "temperature", "38.6", "2", "38.6℃，持续高热");
            Physiology(doc, "map", "58", "3", "58 mmHg，去甲肾上腺素维持");
            Physiology(doc, "heart-rate", "132", "3", "132 次/分，窦性心动过速");
            Physiology(doc, "respiratory-rate", "28", "3", "28 次/分，呼吸机辅助");
            Physiology(doc, "oxygenation", "PaO2 68", "3", "PaO2<70，氧合指数 113");
            Physiology(doc, "ph", "7.28", "2", "失代偿性酸中毒");
            Physiology(doc, "sodium", "149", "2", "高钠血症");
            Physiology(doc, "potassium", "5.9", "2", "高钾血症，心电图无改变");
            Physiology(doc, "creatinine", "286", "4", "急性肾损伤 2 期");
            Physiology(doc, "hematocrit", "28", "2", "中度贫血");
            Physiology(doc, "wbc", "19.8", "2", "感染未控制");
            Physiology(doc, "gcs", "9", "6", "GCS 9（E2V2M5）");

            Chronic(doc, "age", "72 岁", "5", "65–74 岁，5 分");
            Chronic(doc, "chronic-health", "内科非手术、非急诊入院", "5", "慢性器官功能不全，非手术 5 分");

            doc.SetCell(ApacheIiSections.Result, "total", 1, "44");
            doc.SetCell(ApacheIiSections.Result, "total", 2, "生理 34 + 年龄 5 + 慢性健康 5");

            doc.SetCell(ApacheIiSections.Result, "mortality", 1, "约 60%–65%");
            doc.SetCell(ApacheIiSections.Result, "mortality", 2, "非手术患者，参考病死率区间");

            return doc;
        }

        private static void Physiology(ApacheIiDocument doc, string rowKey,
            string value, string score, string note)
        {
            doc.SetCell(ApacheIiSections.Physiology, rowKey, 1, value);
            doc.SetCell(ApacheIiSections.Physiology, rowKey, 2, score);
            doc.SetCell(ApacheIiSections.Physiology, rowKey, 3, note);
        }

        private static void Chronic(ApacheIiDocument doc, string rowKey,
            string value, string score, string note)
        {
            doc.SetCell(ApacheIiSections.Chronic, rowKey, 1, value);
            doc.SetCell(ApacheIiSections.Chronic, rowKey, 2, score);
            doc.SetCell(ApacheIiSections.Chronic, rowKey, 3, note);
        }
    }
}
