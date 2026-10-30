using System;
using OODD.Charts.Medical.Partogram;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>
    /// 产程图样例数据。一槽 = 30 分钟，因此第 h 小时的槽号是 h × 2。
    /// 产程取一位初产妇的常见走势：潜伏期宫口慢开，6 小时进活跃期后约 1 cm/h，
    /// 11 小时人工破膜，宫缩逐步加密增强。
    /// </summary>
    public static class PartogramSample
    {
        private static readonly int[] CervixHours = { 0, 2, 4, 6, 8, 10, 12, 14, 16 };
        private static readonly double[] CervixCm = { 1, 1.5, 2, 3, 4, 5, 6, 7, 8 };
        private static readonly double[] StationCm = { -3, -3, -2.5, -2, -1, 0, 1, 1.5, 2 };
        private static readonly int[] ContractionHours = { 0, 2, 4, 6, 8, 10, 12, 14, 16 };
        private static readonly int[] ContractionCounts = { 2, 2, 3, 3, 4, 4, 5, 5, 4 };
        private static readonly double[] ContractionSeconds = { 25, 30, 30, 35, 40, 45, 50, 50, 45 };

        public static PartogramDocument Build()
        {
            PartogramDocument doc = new PartogramDocument
            {
                HospitalName = "测试医院",
                FormTitle = "产程图",
                HeaderRightNote = "共 1 页　第 1 页",
                StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
                Hours = 16,
                SlotsPerHour = 2,
                AlertStartHour = 6,
                AlertStartCm = 3,
                AlertSlopeCmPerHour = 1,
                ActionOffsetHours = 4,
            };

            doc.Patient.Name = "王芳";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "29";
            doc.Patient.Department = "产科";
            doc.Patient.Ward = "LDR 一体化产房";
            doc.Patient.BedNumber = "3床";
            doc.Patient.AdmissionNumber = "2026011507";
            doc.Patient.AdmissionDate = "2026-01-15";
            doc.Patient.ExtraFields.Add(new HeaderField("gestation", "孕周", "39+2周"));
            doc.Patient.ExtraFields.Add(new HeaderField("parity", "产次", "G1P0"));

            doc.Cervix.Color = SKColors.Red;
            doc.Station.Color = SKColors.Blue;
            for (int i = 0; i < CervixHours.Length; i++)
            {
                doc.Cervix.Values[CervixHours[i] * doc.SlotsPerHour] = CervixCm[i];
                doc.Station.Values[StationSlot(doc, i)] = StationCm[i];
            }

            for (int hour = 0; hour <= doc.Hours; hour++)
            {
                int slot = hour * doc.SlotsPerHour;
                if (slot >= doc.TotalSlots)
                {
                    break;
                }
                doc.FetalHeart.Add(new FetalHeartReading(slot, FetalHeartAt(hour)));
            }

            for (int i = 0; i < ContractionHours.Length; i++)
            {
                doc.Contractions.Add(new ContractionReading(ContractionHours[i] * doc.SlotsPerHour,
                    ContractionCounts[i])
                {
                    DurationSeconds = ContractionSeconds[i],
                });
            }

            doc.Membrane.Add(new MembraneMark(11 * doc.SlotsPerHour) { Text = "破" });

            AddCells(doc);

            doc.Legend.Add(new LegendItem(doc.Cervix.Symbol, doc.Cervix.Color, "宫口扩张(cm)"));
            doc.Legend.Add(new LegendItem(doc.Station.Symbol, doc.Station.Color, "胎先露(cm)"));
            return doc;
        }

        private static int StationSlot(PartogramDocument doc, int index)
        {
            return CervixHours[index] * doc.SlotsPerHour;
        }

        /// <summary>第 6、13 小时各造一次异常胎心，用于验证红圈圈注。</summary>
        private static double FetalHeartAt(int hour)
        {
            if (hour == 6)
            {
                return 168;
            }
            if (hour == 13)
            {
                return 112;
            }

            double[] baseline = { 140, 138, 142, 136, 144, 138, 140, 134, 136, 132,
                                  138, 134, 136, 130, 134, 132 };
            return baseline[hour % baseline.Length];
        }

        private static void AddCells(PartogramDocument doc)
        {
            string[] systolic = { "118", "120", "116", "122", "118", "124", "120", "118", "122" };
            string[] diastolic = { "76", "78", "74", "80", "76", "82", "78", "76", "80" };
            string[] pulse = { "84", "86", "88", "92", "96", "102", "104", "108", "112" };
            string[] temp = { "36.8", "36.9", "37.0", "37.1", "37.3", "37.4", "37.5", "37.6", "37.7" };
            string[] exam = { "宫口1cm S-3", "宫口2cm S-3", "宫口3cm S-2", "宫口4cm S-1",
                              "宫口5cm S0", "宫口6cm S+1", "宫口7cm S+1", "宫口8cm S+2", "宫口8cm S+2" };

            for (int i = 0; i < CervixHours.Length; i++)
            {
                int slot = CervixHours[i] * doc.SlotsPerHour;
                if (slot >= doc.TotalSlots)
                {
                    break;
                }
                Add(doc, PartogramFields.BloodPressure, slot, systolic[i] + "/" + diastolic[i]);
                Add(doc, PartogramFields.Pulse, slot, pulse[i]);
                Add(doc, PartogramFields.Temperature, slot, temp[i]);
                Add(doc, PartogramFields.VaginalExam, slot, exam[i]);
            }

            Add(doc, PartogramFields.Oxytocin, 8 * doc.SlotsPerHour, "8滴/分", SKColors.Red);
            Add(doc, PartogramFields.Oxytocin, 12 * doc.SlotsPerHour, "20滴/分", SKColors.Red);
            Add(doc, PartogramFields.Oxytocin, 14 * doc.SlotsPerHour, "25滴/分", SKColors.Red);
            Add(doc, PartogramFields.Note, 11 * doc.SlotsPerHour, "人工破膜", SKColors.Red);
            Add(doc, PartogramFields.Note, 13 * doc.SlotsPerHour, "胎心减慢，左侧卧位", SKColors.Red);
        }

        private static void Add(PartogramDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }

        private static void Add(PartogramDocument doc, string rowKey, int slot, string text, SKColor color)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text) { Color = color });
        }
    }
}
