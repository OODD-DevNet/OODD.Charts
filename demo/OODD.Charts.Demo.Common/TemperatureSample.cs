using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>三测单样例数据。槽位编号 = 天序号 × 6 + 当日第几次（0 起）。</summary>
    public static class TemperatureSample
    {
        public static TemperatureChartDocument Build()
        {
            TemperatureChartDocument doc = new TemperatureChartDocument
            {
                HospitalName = "测试医院",
                FormTitle = "体温单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "测试";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "16";
            doc.Patient.Department = "中医科";
            doc.Patient.Ward = "一病区";
            doc.Patient.BedNumber = "6床";
            doc.Patient.AdmissionNumber = "20250626";
            doc.Patient.AdmissionDate = "2025-12-28";

            DateTime start = new DateTime(2025, 12, 28);
            for (int day = 0; day < 6; day++)
            {
                doc.Days.Add(new ChartGroup(start.AddDays(day)));
            }

            // 腋温 ○ 蓝 —— 落在体温轴
            PointSeries axillary = new PointSeries("axillary", TemperatureChartTemplate.AxisTemperature)
            {
                Symbol = MarkerSymbol.Ring,
                Color = SKColors.Blue,
                LegendText = "腋表",
            };
            double[] temps =
            {
                36.8, 37.2, 36.9, 37.4, 38.1, 37.6,
                37.0, 37.3, 36.8, 37.1, 36.9, 37.0,
                38.6, 39.2, 38.4, 37.8, 37.2, 36.9,
                36.7, 37.0, 36.8, 37.1, 36.6, 36.9,
                37.5, 37.9, 38.2, 37.7, 37.1, 36.8,
                36.9, 37.2, 37.0, 36.7, 37.1, 36.8,
            };
            for (int i = 0; i < temps.Length; i++)
            {
                axillary.Values[i] = temps[i];
            }
            // 物理降温 30 分钟后复测：红圈 + 虚线连接
            axillary.ValueLabels[12] = "↓";
            doc.Series.Add(axillary);

            // 脉搏 ● 红 —— 落在脉搏轴
            PointSeries pulse = new PointSeries("pulse", TemperatureChartTemplate.AxisPulse)
            {
                Symbol = MarkerSymbol.Dot,
                Color = SKColors.Red,
                LegendText = "脉搏",
            };
            double[] pulses =
            {
                80, 84, 78, 90, 96, 88,
                76, 82, 79, 85, 80, 77,
                104, 118, 110, 98, 92, 86,
                82, 88, 80, 84, 79, 81,
                94, 100, 106, 98, 90, 84,
                86, 90, 82, 85, 88, 80,
            };
            for (int i = 0; i < pulses.Length; i++)
            {
                pulse.Values[i] = pulses[i];
            }
            doc.Series.Add(pulse);

            // 心率 × 蓝 —— 落在脉搏轴，只有第 4、5 天测过（演示缺测断线）
            PointSeries heart = new PointSeries("heart", TemperatureChartTemplate.AxisPulse)
            {
                Symbol = MarkerSymbol.Cross,
                Color = SKColors.Blue,
                LegendText = "心率",
            };
            heart.Values[18] = 96;
            heart.Values[19] = 104;
            heart.Values[20] = 112;
            heart.Values[21] = 108;
            doc.Series.Add(heart);

            // 呼吸 —— 逐槽细分行
            AddCell(doc, TemperatureFields.Respiration, 5, "20");
            AddCell(doc, TemperatureFields.Respiration, 11, "22");
            AddCell(doc, TemperatureFields.Respiration, 17, "32");
            AddCell(doc, TemperatureFields.Respiration, 23, "18");
            AddCell(doc, TemperatureFields.Respiration, 29, "19");
            AddCell(doc, TemperatureFields.Respiration, 35, "18");

            // 以下各行按日合并（一天一次），槽号取当天任意一个即可
            AddCell(doc, TemperatureFields.BloodPressure, 24, "147/65");
            AddCell(doc, TemperatureFields.BloodPressure, 21, "125/80");
            AddCell(doc, TemperatureFields.Intake, 25, "1450");
            AddCell(doc, TemperatureFields.Intake, 18, "1200");
            AddCell(doc, TemperatureFields.Output, 25, "1380");
            AddCell(doc, TemperatureFields.Output, 16, "1150");
            AddCell(doc, TemperatureFields.Stool, 25, "1");
            AddCell(doc, TemperatureFields.Stool, 16, "0");
            AddCell(doc, TemperatureFields.Urine, 25, "4");
            AddCell(doc, TemperatureFields.Urine, 16, "3");
            AddCell(doc, TemperatureFields.Weight, 25, "62.5");
            AddCell(doc, TemperatureFields.Weight, 0, "62.8");
            AddCell(doc, TemperatureFields.Height, 0, "172");
            AddCell(doc, TemperatureFields.Allergy, 0, "青霉素");
            AddCell(doc, TemperatureFields.Other, 25, "外出检查");

            // 事件带：入院 / 手术 / 术后天数
            doc.Events.Add(Mark(0, "入院"));
            doc.Events.Add(Mark(6, "手术"));
            doc.Events.Add(Mark(12, "术后", 1));
            doc.Events.Add(Mark(18, string.Empty, 2));
            doc.Events.Add(Mark(24, string.Empty, 3));
            doc.Events.Add(Mark(30, "转出"));

            doc.Legend.Add(new LegendItem(MarkerSymbol.Dot, SKColors.Red, "脉搏"));
            doc.Legend.Add(new LegendItem(MarkerSymbol.Ring, SKColors.Blue, "腋表"));
            doc.Legend.Add(new LegendItem(MarkerSymbol.Cross, SKColors.Blue, "心率"));

            return doc;
        }

        private static void AddCell(TemperatureChartDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }

        private static EventMark Mark(int slot, string text) => new EventMark(slot, text);

        private static EventMark Mark(int slot, string text, int dayCount)
            => new EventMark(slot, text) { DayCount = dayCount };
    }
}
