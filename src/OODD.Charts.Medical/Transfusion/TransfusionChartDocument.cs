using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Medical.Transfusion
{
    /// <summary>输血反应记录单附属行的稳定标识。</summary>
    public static class TransfusionFields
    {
        public const string Symptoms = "symptoms";
        public const string BloodPressure = "blood-pressure";
        public const string PulseRespiration = "pulse-respiration";
        public const string Treatment = "treatment";
        public const string Medication = "medication";
        public const string Specimen = "specimen";
        public const string Recorder = "recorder";

        public static IList<string> DefaultOrder { get; } = new List<string>
        {
            Symptoms, BloodPressure, PulseRespiration, Treatment, Medication, Specimen, Recorder,
        };

        public static string DefaultLabel(string key)
        {
            switch (key)
            {
                case Symptoms: return "症状/体征";
                case BloodPressure: return "血压(mmHg)";
                case PulseRespiration: return "脉搏/呼吸";
                case Treatment: return "处理措施";
                case Medication: return "用药";
                case Specimen: return "标本/检验";
                case Recorder: return "记录者";
                default: return key;
            }
        }
    }

    /// <summary>输血反应观察的温度刻度与发热阈值。</summary>
    public sealed class TransfusionScale
    {
        public double MinCelsius = 36d;
        public double MaxCelsius = 41d;

        /// <summary>每 0.2 ℃ 一条细线。</summary>
        public double MinorStepCelsius = 0.2d;

        /// <summary>每 1 ℃ 一条粗线并标数。</summary>
        public double MajorStepCelsius = 1d;

        /// <summary>发热阈值，输注相关发热常取 38 ℃。</summary>
        public double FeverThreshold = 38d;

        public bool ShowFeverLine = true;
    }

    /// <summary>
    /// 输血反应记录单文档。横轴为输注开始后的观察时点（默认每 30 分钟一槽），
    /// 纵轴为体温 36–41 ℃；反应事件在对应时点画红色竖线，附属行记录症状、处理与用药。
    /// </summary>
    public sealed class TransfusionChartDocument : ChartDocument
    {
        public TransfusionChartDocument()
        {
            FormTitle = "输血反应记录单";
            Days = new List<ChartGroup>();
            Temperature = new PointSeries("temperature", TransfusionChartTemplate.AxisTemperature)
            {
                Symbol = MarkerSymbol.Ring,
                Color = SKColors.Blue,
                LegendText = "体温(℃)",
            };
            Events = new List<EventMark>();
            Cells = new List<CellValue>();
            Scale = new TransfusionScale();
            TimeLabels = new List<string>
            {
                "10:00", "10:30", "11:00", "11:30", "12:00", "12:30", "13:00", "13:30",
            };
            RowOrder = new List<string>(TransfusionFields.DefaultOrder);
        }

        public IList<ChartGroup> Days { get; }
        public PointSeries Temperature { get; }
        public IList<EventMark> Events { get; }
        public IList<CellValue> Cells { get; }

        public TransfusionScale Scale { get; set; }

        /// <summary>观察时点标签，长度决定每天几槽。</summary>
        public IList<string> TimeLabels { get; }

        public IList<string> RowOrder { get; }

        public string[] WeekNames { get; set; } = { "日", "一", "二", "三", "四", "五", "六" };

        public string DateFormat { get; set; } = "yyyy-MM-dd";

        /// <summary>取一段分组生成子文档，用于分页。</summary>
        public TransfusionChartDocument Subset(int startGroup, int groupCount)
        {
            TransfusionChartDocument copy = CloneShell();
            int slotsPerGroup = Math.Max(1, TimeLabels.Count);
            int firstSlot = startGroup * slotsPerGroup;
            int lastSlot = firstSlot + groupCount * slotsPerGroup;

            for (int g = startGroup; g < Math.Min(startGroup + groupCount, Days.Count); g++)
            {
                copy.Days.Add(Days[g]);
            }

            foreach (KeyValuePair<int, double> pair in Temperature.Values)
            {
                if (pair.Key >= firstSlot && pair.Key < lastSlot)
                {
                    copy.Temperature.Values[pair.Key - firstSlot] = pair.Value;
                }
            }
            foreach (KeyValuePair<int, string> pair in Temperature.ValueLabels)
            {
                if (pair.Key >= firstSlot && pair.Key < lastSlot)
                {
                    copy.Temperature.ValueLabels[pair.Key - firstSlot] = pair.Value;
                }
            }

            foreach (EventMark mark in Events)
            {
                if (mark.Slot >= firstSlot && mark.Slot < lastSlot)
                {
                    copy.Events.Add(new EventMark(mark.Slot - firstSlot, mark.Text)
                    {
                        DayCount = mark.DayCount,
                        Orientation = mark.Orientation,
                        Color = mark.Color,
                        Size = mark.Size,
                        DrawSeparator = mark.DrawSeparator,
                    });
                }
            }

            foreach (CellValue cell in Cells)
            {
                if (cell.Slot >= firstSlot && cell.Slot < lastSlot)
                {
                    copy.Cells.Add(new CellValue(cell.RowKey, cell.Slot - firstSlot, cell.Text)
                    {
                        Color = cell.Color,
                        Align = cell.Align,
                        Size = cell.Size,
                        Occurrence = cell.Occurrence,
                        OccurrenceCount = cell.OccurrenceCount,
                    });
                }
            }

            return copy;
        }

        private TransfusionChartDocument CloneShell()
        {
            TransfusionChartDocument copy = new TransfusionChartDocument
            {
                HospitalName = HospitalName,
                FormTitle = FormTitle,
                Patient = Patient,
                HeaderRightNote = HeaderRightNote,
                Scale = Scale,
                DateFormat = DateFormat,
                WeekNames = WeekNames,
            };
            copy.Temperature.Symbol = Temperature.Symbol;
            copy.Temperature.Color = Temperature.Color;
            copy.Temperature.MarkerRadius = Temperature.MarkerRadius;
            copy.Temperature.LineWidth = Temperature.LineWidth;
            copy.Temperature.Connect = Temperature.Connect;
            copy.Temperature.Dashed = Temperature.Dashed;
            copy.Temperature.BreakRule = Temperature.BreakRule;
            copy.Temperature.LegendText = Temperature.LegendText;
            copy.Temperature.ValueLabelColor = Temperature.ValueLabelColor;
            copy.Temperature.YOffset = Temperature.YOffset;

            foreach (string label in TimeLabels)
            {
                copy.TimeLabels.Add(label);
            }
            foreach (string row in RowOrder)
            {
                copy.RowOrder.Add(row);
            }
            foreach (LegendItem item in Legend)
            {
                copy.Legend.Add(item);
            }
            return copy;
        }
    }
}
