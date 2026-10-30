using OODD.Charts.Medical.BodyComposition;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>身体组成分析样例：45 岁女性体检者，体脂率偏高、骨骼肌量偏低。</summary>
    public static class BodyCompositionSample
    {
        public static BodyCompositionDocument Build()
        {
            BodyCompositionDocument doc = new BodyCompositionDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "体检号：TJ-2026-031208",
            };

            doc.Patient.Name = "陈雅琴";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "45";
            doc.Patient.Department = "健康管理中心";
            doc.Patient.Ward = "体检区";
            doc.Patient.BedNumber = "—";
            doc.Patient.AdmissionNumber = "TJ2026031208";
            doc.Patient.AdmissionDate = "2026-03-12";
            doc.Patient.ExtraFields.Add(new HeaderField("package", "体检套餐", "女性全面套餐（含体成分）"));
            doc.Patient.ExtraFields.Add(new HeaderField("history", "既往史", "高血压 5 年，规律服药"));

            // 一、检查信息
            doc.SetCell(BodyCompositionSections.Info, BodyCompositionRows.ExamDate, 1, "2026-03-12 08:30");
            doc.SetCell(BodyCompositionSections.Info, BodyCompositionRows.Device, 1,
                "InBody 770 生物电阻抗分析仪");
            doc.SetCell(BodyCompositionSections.Info, BodyCompositionRows.Position, 1,
                "立位，空腹 2 小时，排空膀胱后测量");

            // 二、测量结果
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Weight, 1, "68.2");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Weight, 3, "正常");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Height, 1, "159.5");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Height, 3, "正常");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Bmi, 1, "26.8");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Bmi, 3, "偏高 ↑");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.BodyFat, 1, "32.4");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.BodyFat, 3, "偏高 ↑");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.SkeletalMuscle, 1, "20.1");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.SkeletalMuscle, 3, "偏低 ↓");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.VisceralFat, 1, "9");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.VisceralFat, 3, "偏高 ↑");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Bmr, 1, "1286");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Bmr, 3, "正常");

            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.BodyWater, 1, "45.3");
            doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.BodyWater, 3, "正常");

            // 异常判读标红
            doc.Row(BodyCompositionSections.Results, BodyCompositionRows.Bmi)!.Cells[3].Color = SKColors.Red;
            doc.Row(BodyCompositionSections.Results, BodyCompositionRows.BodyFat)!.Cells[3].Color = SKColors.Red;
            doc.Row(BodyCompositionSections.Results, BodyCompositionRows.SkeletalMuscle)!.Cells[3].Color = SKColors.Red;
            doc.Row(BodyCompositionSections.Results, BodyCompositionRows.VisceralFat)!.Cells[3].Color = SKColors.Red;

            // 三、结论
            doc.Section(BodyCompositionSections.Conclusion)!.Note =
                "1. BMI 26.8、体脂率 32.4% 偏高，骨骼肌量 20.1kg 偏低，呈肌少性肥胖倾向；建议每周 3 次抗阻训练，每日蛋白质摄入 1.2g/kg 体重。\n" +
                "2. 内脏脂肪等级 9 偏高，建议减少精制碳水与夜间加餐，控制体重每月 1–2kg；3 个月后复查体成分。";

            // 四、签名
            doc.SetCell(BodyCompositionSections.Signature, BodyCompositionRows.Sign, 0, "刘倩");
            doc.SetCell(BodyCompositionSections.Signature, BodyCompositionRows.Sign, 1, "周明");
            doc.SetCell(BodyCompositionSections.Signature, BodyCompositionRows.Sign, 2, "2026-03-12 10:35");

            return doc;
        }
    }
}
