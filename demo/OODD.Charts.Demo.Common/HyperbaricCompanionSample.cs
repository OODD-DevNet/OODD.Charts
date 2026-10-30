using System;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Medical.HyperbaricCompanion;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>高压氧陪舱记录样例：突发性聋患者首次 2.0 ATA 治疗，全程 120 分钟。</summary>
    public static class HyperbaricCompanionSample
    {
        public static HyperbaricCompanionDocument Build()
        {
            HyperbaricCompanionDocument doc = new HyperbaricCompanionDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "治疗舱号：2 号舱　第 1 次治疗",
            };

            doc.Patient.Name = "刘桂芳";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "58";
            doc.Patient.Department = "神经内科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "8床";
            doc.Patient.AdmissionNumber = "2026030806";
            doc.Patient.AdmissionDate = "2026-03-08";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "左耳突发性聋（病程 10 天）"));
            doc.Patient.ExtraFields.Add(new HeaderField("plan", "治疗方案",
                "2.0 ATA 面罩吸氧 60 分钟，每日 1 次，共 10 次"));

            ChartGroup group = new ChartGroup(new DateTime(2026, 3, 12))
            {
                DateText = "2026-03-12　08:30 首次进舱",
            };
            doc.Days.Add(group);

            PointSeries pressure = new PointSeries("pressure", HyperbaricCompanionRows.AxisPressure)
            {
                Symbol = MarkerSymbol.Dot,
                Color = SKColors.Blue,
                Connect = true,
                LegendText = "舱压",
            };
            double[] values = { 1.0, 1.5, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 2.0, 1.6, 1.3, 1.1, 1.0 };
            for (int slot = 0; slot < values.Length; slot++)
            {
                pressure.Values[slot] = values[slot];
            }
            doc.Series.Add(pressure);

            doc.Thresholds.Add(new ThresholdMark(2.2, "稳压上限") { Color = SKColors.Red });

            doc.Events.Add(new EventMark(0, "开始加压"));
            doc.Events.Add(new EventMark(2, "达 2.0 ATA"));
            doc.Events.Add(new EventMark(3, "稳压吸氧"));
            doc.Events.Add(new EventMark(9, "开始减压"));
            doc.Events.Add(new EventMark(12, "安全出舱"));

            Add(doc, HyperbaricCompanionRows.Persons, 0, "家属 王强");
            Add(doc, HyperbaricCompanionRows.Persons, 3, "家属 王强");

            Add(doc, HyperbaricCompanionRows.Oxygen, 3, "面罩吸氧 100%");

            Add(doc, HyperbaricCompanionRows.Status, 3, "平稳");
            Add(doc, HyperbaricCompanionRows.Status, 6, "耳压不适");
            Add(doc, HyperbaricCompanionRows.Status, 7, "已缓解");

            Add(doc, HyperbaricCompanionRows.Nurse, 0, "张");
            Add(doc, HyperbaricCompanionRows.Nurse, 6, "张");
            Add(doc, HyperbaricCompanionRows.Nurse, 12, "李");

            return doc;
        }

        private static void Add(HyperbaricCompanionDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }
    }
}
