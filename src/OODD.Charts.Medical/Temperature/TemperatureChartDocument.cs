using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Medical.Temperature
{
    /// <summary>三测单附属表格的行标识。用常量而非枚举，便于业务方追加自定义行。</summary>
    public static class TemperatureFields
    {
        /// <summary>呼吸(次/分) —— 唯一保持逐槽细分的行。</summary>
        public const string Respiration = "respiration";

        public const string BloodPressure = "blood-pressure";
        public const string Intake = "intake";
        public const string Output = "output";
        public const string Stool = "stool";
        public const string Urine = "urine";
        public const string Weight = "weight";
        public const string Height = "height";
        public const string Allergy = "allergy";
        public const string Other = "other";

        /// <summary>国标常用顺序。</summary>
        public static IList<string> DefaultOrder { get; } = new List<string>
        {
            BloodPressure, Intake, Output, Stool, Urine, Weight, Height, Allergy, Other,
        };

        public static string DefaultLabel(string key)
        {
            switch (key)
            {
                case Respiration: return "呼吸(次/分)";
                case BloodPressure: return "血压(mmHg)";
                case Intake: return "入量(ml)";
                case Output: return "出量(ml)";
                case Stool: return "大便(次/日)";
                case Urine: return "小便(次/日)";
                case Weight: return "体重(kg)";
                case Height: return "身高(cm)";
                case Allergy: return "药物过敏";
                case Other: return "其他";
                default: return key;
            }
        }
    }

    /// <summary>三测单的体温/脉搏共享刻度参数。实测：脉搏 40–160 对齐体温 35–41。</summary>
    public sealed class TemperatureScale
    {
        /// <summary>网格顶端（不标数）。</summary>
        public double TopCelsius = 42d;

        /// <summary>网格底端（不标数），即事件带下沿。</summary>
        public double BottomCelsius = 34d;

        public double LabelFromCelsius = 35d;
        public double LabelToCelsius = 41d;

        /// <summary>每 0.2 ℃ 一格。</summary>
        public double MinorStepCelsius = 0.2d;

        public double MajorStepCelsius = 1d;

        public double PulseAtLabelFrom = 40d;
        public double PulseAtLabelTo = 160d;

        /// <summary>事件带（红字竖排区）的上沿体温。</summary>
        public double EventBandTopCelsius = 35d;
    }

    /// <summary>三测单文档。</summary>
    public sealed class TemperatureChartDocument : ChartDocument
    {
        public TemperatureChartDocument()
        {
            FormTitle = "体温单";
            Days = new List<ChartGroup>();
            Series = new List<PointSeries>();
            Cells = new List<CellValue>();
            Events = new List<EventMark>();
            Scale = new TemperatureScale();
            TimeLabels = new List<string> { "15", "19", "23", "3", "7", "11" };
            RowOrder = new List<string>(TemperatureFields.DefaultOrder);
        }

        /// <summary>列分组，通常一天一组。</summary>
        public IList<ChartGroup> Days { get; }

        public IList<PointSeries> Series { get; }
        public IList<CellValue> Cells { get; }
        public IList<EventMark> Events { get; }

        public TemperatureScale Scale { get; set; }

        /// <summary>一组内各槽的时段标签，长度须等于 <c>Style.Grid.SlotsPerGroup</c>。</summary>
        public IList<string> TimeLabels { get; }

        /// <summary>附属表格行的顺序（不含呼吸行，呼吸行固定在最上方）。</summary>
        public IList<string> RowOrder { get; }

        /// <summary>星期文本表，索引对应 <see cref="DayOfWeek"/>。</summary>
        public string[] WeekNames { get; set; } =
            { "日", "一", "二", "三", "四", "五", "六" };

        /// <summary>日期行文本格式。</summary>
        public string DateFormat { get; set; } = "M-d";

        /// <summary>总槽位数。</summary>
        public int TotalSlots(int slotsPerGroup) => Days.Count * slotsPerGroup;

        /// <summary>取一段分组生成子文档，用于分页。页眉与刻度参数原样带过去。</summary>
        public TemperatureChartDocument Subset(int startGroup, int groupCount)
        {
            TemperatureChartDocument copy = CloneShell();
            int slotsPerGroup = Math.Max(1, TimeLabels.Count);
            int firstSlot = startGroup * slotsPerGroup;
            int lastSlot = firstSlot + groupCount * slotsPerGroup;

            for (int g = startGroup; g < Math.Min(startGroup + groupCount, Days.Count); g++)
            {
                copy.Days.Add(Days[g]);
            }

            foreach (PointSeries series in Series)
            {
                PointSeries remapped = new PointSeries(series.Key, series.AxisKey)
                {
                    Symbol = series.Symbol,
                    Color = series.Color,
                    MarkerRadius = series.MarkerRadius,
                    LineWidth = series.LineWidth,
                    Connect = series.Connect,
                    Dashed = series.Dashed,
                    BreakRule = series.BreakRule,
                    LegendText = series.LegendText,
                    ValueLabelColor = series.ValueLabelColor,
                    YOffset = series.YOffset,
                };
                foreach (KeyValuePair<int, double> pair in series.Values)
                {
                    if (pair.Key >= firstSlot && pair.Key < lastSlot)
                    {
                        remapped.Values[pair.Key - firstSlot] = pair.Value;
                    }
                }
                foreach (KeyValuePair<int, string> pair in series.ValueLabels)
                {
                    if (pair.Key >= firstSlot && pair.Key < lastSlot)
                    {
                        remapped.ValueLabels[pair.Key - firstSlot] = pair.Value;
                    }
                }
                if (remapped.Values.Count > 0)
                {
                    copy.Series.Add(remapped);
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

            return copy;
        }

        private TemperatureChartDocument CloneShell()
        {
            TemperatureChartDocument copy = new TemperatureChartDocument
            {
                HospitalName = HospitalName,
                FormTitle = FormTitle,
                Patient = Patient,
                HeaderRightNote = HeaderRightNote,
                Scale = Scale,
                DateFormat = DateFormat,
                WeekNames = WeekNames,
            };
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
