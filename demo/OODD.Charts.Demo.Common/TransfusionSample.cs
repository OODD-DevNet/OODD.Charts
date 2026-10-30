using System;
using OODD.Charts.Medical.Transfusion;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>输血反应记录单样例：1 天 × 8 个观察时点，体温在 11:30 越过 38 ℃ 发热阈值。</summary>
    public static class TransfusionSample
    {
        public static TransfusionChartDocument Build()
        {
            TransfusionChartDocument doc = new TransfusionChartDocument
            {
                HospitalName = "测试医院",
                FormTitle = "输血反应记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "李国栋";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "61";
            doc.Patient.Department = "血液科";
            doc.Patient.Ward = "五病区";
            doc.Patient.BedNumber = "23床";
            doc.Patient.AdmissionNumber = "2026021103";
            doc.Patient.AdmissionDate = "2026-02-10";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "急性髓系白血病"));
            doc.Patient.ExtraFields.Add(new HeaderField("blood-type", "血型", "A 型 RhD 阳性"));
            doc.Patient.ExtraFields.Add(new HeaderField("blood-bag", "血袋号", "B2026021107"));
            doc.Patient.ExtraFields.Add(new HeaderField("product", "制品", "悬浮红细胞 2U"));

            doc.Days.Add(new ChartGroup(new DateTime(2026, 2, 11)));

            double[] temps = { 36.6, 36.8, 37.1, 38.9, 39.2, 38.4, 37.6, 37.2 };
            for (int slot = 0; slot < temps.Length; slot++)
            {
                doc.Temperature.Values[slot] = temps[slot];
            }

            doc.Events.Add(new EventMark(3, "寒战") { Color = SkiaSharp.SKColors.Red });
            doc.Events.Add(new EventMark(6, "送检") { Color = SkiaSharp.SKColors.Red });

            Add(doc, TransfusionFields.Symptoms, 0, "无");
            Add(doc, TransfusionFields.Symptoms, 3, "寒战");
            Add(doc, TransfusionFields.Symptoms, 4, "寒战");
            Add(doc, TransfusionFields.Symptoms, 6, "缓解");

            Add(doc, TransfusionFields.BloodPressure, 0, "128/76");
            Add(doc, TransfusionFields.BloodPressure, 3, "104/62");
            Add(doc, TransfusionFields.BloodPressure, 4, "100/60");
            Add(doc, TransfusionFields.BloodPressure, 6, "118/70");

            Add(doc, TransfusionFields.PulseRespiration, 0, "78/18");
            Add(doc, TransfusionFields.PulseRespiration, 3, "118/26");
            Add(doc, TransfusionFields.PulseRespiration, 4, "122/28");
            Add(doc, TransfusionFields.PulseRespiration, 6, "92/20");

            Add(doc, TransfusionFields.Treatment, 3, "停输");
            Add(doc, TransfusionFields.Treatment, 4, "吸氧");
            Add(doc, TransfusionFields.Treatment, 6, "观察");

            Add(doc, TransfusionFields.Medication, 3, "地塞米松");
            Add(doc, TransfusionFields.Medication, 4, "异丙嗪");

            Add(doc, TransfusionFields.Specimen, 6, "血袋+标本");

            Add(doc, TransfusionFields.Recorder, 0, "甲");
            Add(doc, TransfusionFields.Recorder, 3, "甲");
            Add(doc, TransfusionFields.Recorder, 4, "甲");
            Add(doc, TransfusionFields.Recorder, 6, "乙");

            return doc;
        }

        private static void Add(TransfusionChartDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }
    }
}
