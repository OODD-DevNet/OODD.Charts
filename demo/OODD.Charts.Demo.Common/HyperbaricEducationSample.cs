using OODD.Charts.Medical.HyperbaricEducation;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>高压氧进舱宣教样例：慢阻肺急性加重合并一氧化碳中毒患者首次进舱，配偶陪舱。</summary>
    public static class HyperbaricEducationSample
    {
        public static HyperbaricEducationDocument Build()
        {
            HyperbaricEducationDocument doc = new HyperbaricEducationDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "宣教单号：HBO-2026-0312-07",
            };

            doc.Patient.Name = "李建国";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "64";
            doc.Patient.Department = "高压氧科";
            doc.Patient.Ward = "急诊留观区";
            doc.Patient.BedNumber = "3床";
            doc.Patient.AdmissionNumber = "2026031206";
            doc.Patient.AdmissionDate = "2026-03-12";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "慢阻肺急性加重合并急性一氧化碳中毒"));
            doc.Patient.ExtraFields.Add(new HeaderField("oxygen-cabin", "氧舱编号", "2号多人舱"));

            doc.SetCell(HyperbaricEducationSections.Info, HyperbaricEducationRows.Cabin, 1, "2 号多人舱（治疗压力 2.0 ATA）");
            doc.SetCell(HyperbaricEducationSections.Info, HyperbaricEducationRows.Session, 1, "第 1 次　全程 90 分钟");
            doc.SetCell(HyperbaricEducationSections.Info, HyperbaricEducationRows.FirstEntry, 1, "√ 是　□ 否");
            doc.SetCell(HyperbaricEducationSections.Info, HyperbaricEducationRows.Escort, 1, "家属陪舱（配偶 王秀兰），舱外医护全程监护");

            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.Preparation, 1,
                "排空大小便，更换纯棉衣物，去除义齿、手表及首饰；进舱前排痰、测体温，携带病历与胸部 CT 片。");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.Preparation, 2, "√ 已掌握（口述复述）");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EarPressure, 1,
                "升压时做捏鼻鼓气、吞咽或咀嚼动作；出现耳闷、耳痛立即告知陪舱人员，必要时暂停加压。");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EarPressure, 2, "√ 已掌握（口述复述）");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.ForbiddenItems, 1,
                "严禁携带火柴、打火机、手机、电子烟、暖宝宝、酒精及易燃易爆物品；不穿化纤衣物，不使用护肤品。");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.ForbiddenItems, 2, "√ 已掌握（口述复述）");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.OxygenToxicity, 1,
                "如出现面部肌肉抽搐、恶心、眩晕、视野缩小或烦躁不安，立即示意并做平稳深呼吸，必要时摘除面罩。");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.OxygenToxicity, 2, "√ 已掌握（口述复述）");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EmergencyCall, 1,
                "舱内不适立即按压呼叫按钮或举手示意，通过对讲报告舱外医护，不得自行敲击舱壁或强行开门。");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EmergencyCall, 2, "√ 已掌握（口述复述）");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.PostObservation, 1,
                "出舱后留观 30 分钟，注意耳鸣、胸闷、皮肤瘙痒；24 小时内避免剧烈活动、饮酒及热水浴。");
            doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.PostObservation, 2, "√ 已掌握（口述复述）");

            doc.SetCell(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Absolute, 1,
                "√ 已排除（进舱前胸片及查体均未发现气胸、纵隔气肿）");
            doc.SetCell(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Relative, 1,
                "√ 已排除（上感已缓解，血压 138/86 mmHg，无幽闭恐惧表现）");
            doc.SetCell(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Conclusion, 1,
                "√ 无（无绝对禁忌，准予首次进舱，舱内密切观察）");

            doc.SetCell(HyperbaricEducationSections.Signature, HyperbaricEducationRows.Sign, 0, "王秀兰（配偶）");
            doc.SetCell(HyperbaricEducationSections.Signature, HyperbaricEducationRows.Sign, 1, "周敏");
            doc.SetCell(HyperbaricEducationSections.Signature, HyperbaricEducationRows.Sign, 2, "2026-03-12 15:10");

            return doc;
        }
    }
}
