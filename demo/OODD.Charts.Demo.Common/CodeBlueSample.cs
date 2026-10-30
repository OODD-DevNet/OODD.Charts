using System;
using OODD.Charts.Medical.CodeBlue;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>Code Blue 抢救记录样例：0–60 分钟，含一次电除颤与恢复自主循环。</summary>
    public static class CodeBlueSample
    {
        public static CodeBlueDocument Build()
        {
            CodeBlueDocument doc = new CodeBlueDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "抢救地点：三病区 12 床",
            };

            doc.Patient.Name = "赵国强";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "67";
            doc.Patient.Department = "心内科";
            doc.Patient.Ward = "三病区";
            doc.Patient.BedNumber = "12床";
            doc.Patient.AdmissionNumber = "2026030902";
            doc.Patient.AdmissionDate = "2026-03-07";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "急性前壁心肌梗死"));

            ChartGroup group = new ChartGroup(new DateTime(2026, 3, 9))
            {
                DateText = "2026-03-09　14:32 心搏骤停，即刻启动 Code Blue",
            };
            doc.Days.Add(group);

            PointSeries heartRate = new PointSeries("hr", CodeBlueRows.AxisVital)
            {
                Symbol = MarkerSymbol.Dot,
                Color = SKColors.Red,
                Connect = false,
                LegendText = "心率",
            };
            PointSeries systolic = new PointSeries("sbp", CodeBlueRows.AxisVital)
            {
                Symbol = MarkerSymbol.Cross,
                Color = SKColors.Blue,
                Connect = false,
                LegendText = "收缩压",
            };

            int[] minutes = { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 };
            double?[] hrValues = { 128, 136, 142, 150, 88, 120, 118, 110, 106, 102, 100, 98, 96 };
            double?[] sbpValues = { 78, 86, 92, 96, 70, 88, 94, 100, 104, 106, 108, 110, 112 };
            for (int slot = 0; slot < minutes.Length; slot++)
            {
                if (hrValues[slot].HasValue)
                {
                    heartRate.Values[slot] = hrValues[slot]!.Value;
                }
                if (sbpValues[slot].HasValue)
                {
                    systolic.Values[slot] = sbpValues[slot]!.Value;
                }
            }
            doc.Series.Add(heartRate);
            doc.Series.Add(systolic);

            doc.Thresholds.Add(new ThresholdMark(90, "低灌注") { Color = SKColors.Red });

            doc.Events.Add(new EventMark(0, "发现骤停") { Color = SKColors.Red });
            doc.Events.Add(new EventMark(1, "肾上腺素 1mg") { Color = SKColors.Red });
            doc.Events.Add(new EventMark(3, "除颤 200J") { Color = SKColors.Red });
            doc.Events.Add(new EventMark(6, "自主循环恢复") { Color = SKColors.Red });

            Add(doc, CodeBlueRows.Rhythm, 0, "室颤");
            Add(doc, CodeBlueRows.Rhythm, 3, "室颤");
            Add(doc, CodeBlueRows.Rhythm, 4, "室上速");
            Add(doc, CodeBlueRows.Rhythm, 6, "窦性");
            Add(doc, CodeBlueRows.Rhythm, 8, "窦性");

            Add(doc, CodeBlueRows.Drug, 1, "肾上腺素 1mg");
            Add(doc, CodeBlueRows.Drug, 4, "胺碘酮 150mg");
            Add(doc, CodeBlueRows.Drug, 8, "肾上腺素 1mg");

            Add(doc, CodeBlueRows.Shock, 0, "胸外按压");
            Add(doc, CodeBlueRows.Shock, 2, "气管插管");
            Add(doc, CodeBlueRows.Shock, 3, "除颤 200J");
            Add(doc, CodeBlueRows.Shock, 6, "呼吸机辅助");

            Add(doc, CodeBlueRows.Nurse, 0, "张");
            Add(doc, CodeBlueRows.Nurse, 3, "张");
            Add(doc, CodeBlueRows.Nurse, 6, "李");
            Add(doc, CodeBlueRows.Nurse, 9, "李");

            return doc;
        }

        private static void Add(CodeBlueDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }
    }
}
