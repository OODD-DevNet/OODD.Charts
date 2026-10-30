using System;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Medical.Pca;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>PCA 记录样例：腹腔镜结肠癌根治术后第 1 天，q2h 记录按压与进药量。</summary>
    public static class PcaSample
    {
        public static PcaDocument Build()
        {
            PcaDocument doc = new PcaDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "镇痛泵编号：PCA-0314-15",
            };

            doc.Patient.Name = "周敏";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "45";
            doc.Patient.Department = "普外科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "15床";
            doc.Patient.AdmissionNumber = "2026031209";
            doc.Patient.AdmissionDate = "2026-03-12";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "乙状结肠癌，腹腔镜根治术后第 1 天"));
            doc.Patient.ExtraFields.Add(new HeaderField("pump", "泵型号", "Graseby 3200 PCA 泵"));
            doc.Patient.ExtraFields.Add(new HeaderField("mixture", "药物配比",
                "舒芬太尼 50μg + 罗哌卡因 200mg 加生理盐水至 100ml"));
            doc.Patient.ExtraFields.Add(new HeaderField("bolus", "单次剂量", "2ml（舒芬太尼 1μg/ml）"));
            doc.Patient.ExtraFields.Add(new HeaderField("lock", "锁定时间", "15 分钟"));

            ChartGroup group = new ChartGroup(new DateTime(2026, 3, 14))
            {
                DateText = "2026-03-14　术后第 1 天",
            };
            doc.Days.Add(group);

            PointSeries presses = new PointSeries("presses", PcaRows.AxisPresses)
            {
                Symbol = MarkerSymbol.Dot,
                Color = SKColors.Red,
                Connect = false,
                LegendText = "有效按压",
            };
            double[] values = { 0, 1, 2, 0, 1, 3, 2, 1, 0, 1, 0, 0 };
            for (int slot = 0; slot < values.Length; slot++)
            {
                presses.Values[slot] = values[slot];
            }
            doc.Series.Add(presses);

            doc.Thresholds.Add(new ThresholdMark(6, "频繁按压需评估"));

            doc.Events.Add(new EventMark(5, "追加吗啡 2mg"));

            string[] volume =
            {
                "0", "2.0", "4.0", "4.0", "6.0", "10.0",
                "14.0", "16.0", "16.0", "18.0", "18.0", "18.0",
            };
            for (int slot = 0; slot < volume.Length; slot++)
            {
                Add(doc, PcaRows.Volume, slot, volume[slot]);
                Add(doc, PcaRows.Lock, slot, "15min");
            }

            string[] nrs = { "3", "2", "2", "1", "2", "4", "3", "2", "1", "2", "1", "1" };
            for (int slot = 0; slot < nrs.Length; slot++)
            {
                Add(doc, PcaRows.Nrs, slot, nrs[slot]);
            }

            Add(doc, PcaRows.Rescue, 5, "吗啡 2mg");

            Add(doc, PcaRows.Nurse, 0, "张");
            Add(doc, PcaRows.Nurse, 4, "李");
            Add(doc, PcaRows.Nurse, 8, "张");

            return doc;
        }

        private static void Add(PcaDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }
    }
}
