using System;
using OODD.Charts.Medical.Pain;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>疼痛评估记录单样例：3 天 × 6 个评估时点，静息痛与活动痛各一条曲线。</summary>
    public static class PainSample
    {
        public static PainChartDocument Build()
        {
            PainChartDocument doc = new PainChartDocument
            {
                HospitalName = "测试医院",
                FormTitle = "疼痛评估记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "陈晓";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "48";
            doc.Patient.Department = "普外科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "18床";
            doc.Patient.AdmissionNumber = "2026030206";
            doc.Patient.AdmissionDate = "2026-03-02";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "腹腔镜术后"));
            doc.Patient.ExtraFields.Add(new HeaderField("allergy", "过敏史", "无"));

            DateTime start = new DateTime(2026, 3, 2);
            for (int day = 0; day < 3; day++)
            {
                doc.Days.Add(new ChartGroup(start.AddDays(day)));
            }

            PointSeries rest = new PointSeries("rest", PainChartTemplate.AxisNrs)
            {
                Symbol = MarkerSymbol.Ring,
                Color = SKColors.Blue,
                LegendText = "静息痛",
            };
            PointSeries activity = new PointSeries("activity", PainChartTemplate.AxisNrs)
            {
                Symbol = MarkerSymbol.Cross,
                Color = SKColors.Red,
                LegendText = "活动痛",
            };

            double?[] restScores =
            {
                6, 5, 4, null, 7, 4,
                4, 3, 2, 2, 3, null,
                3, 2, 2, 1, 2, 1,
            };
            double?[] activityScores =
            {
                8, 7, 6, 5, 8, 5,
                5, 4, 3, 3, 4, 3,
                4, 3, 3, 2, 3, 2,
            };

            for (int slot = 0; slot < restScores.Length; slot++)
            {
                if (restScores[slot].HasValue)
                {
                    rest.Values[slot] = restScores[slot]!.Value;
                }
                if (activityScores[slot].HasValue)
                {
                    activity.Values[slot] = activityScores[slot]!.Value;
                }
            }

            doc.Series.Add(rest);
            doc.Series.Add(activity);

            Add(doc, PainFields.Site, 0, "切口");
            Add(doc, PainFields.Character, 0, "胀痛");
            Add(doc, PainFields.Intervention, 0, "调整体位");
            Add(doc, PainFields.Medication, 0, "镇痛泵");
            Add(doc, PainFields.Assessor, 0, "甲");

            Add(doc, PainFields.Site, 4, "切口");
            Add(doc, PainFields.Character, 4, "锐痛");
            Add(doc, PainFields.Intervention, 4, "下床活动");
            Add(doc, PainFields.Medication, 4, "对乙酰氨基酚");
            Add(doc, PainFields.Assessor, 4, "乙");
            Add(doc, PainFields.Note, 4, "活动后加重");

            Add(doc, PainFields.Site, 8, "切口");
            Add(doc, PainFields.Character, 8, "隐痛");
            Add(doc, PainFields.Intervention, 8, "热敷");
            Add(doc, PainFields.Assessor, 8, "甲");

            Add(doc, PainFields.Site, 12, "切口");
            Add(doc, PainFields.Character, 12, "胀痛");
            Add(doc, PainFields.Intervention, 12, "深呼吸");
            Add(doc, PainFields.Medication, 12, "镇痛泵");
            Add(doc, PainFields.Assessor, 12, "甲");

            Add(doc, PainFields.Site, 16, "切口");
            Add(doc, PainFields.Character, 16, "隐痛");
            Add(doc, PainFields.Intervention, 16, "散步");
            Add(doc, PainFields.Assessor, 16, "乙");

            return doc;
        }

        private static void Add(PainChartDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }
    }
}
