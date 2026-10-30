using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms
{
    /// <summary>时间轴单据的一条附属行：低于网格，逐槽或按组填写。</summary>
    public sealed class TimelineRow
    {
        public TimelineRow(string key, string label, bool splitBySlot = true)
        {
            Key = key;
            Label = label;
            SplitBySlot = splitBySlot;
        }

        public string Key { get; }

        public string Label { get; }

        public bool SplitBySlot { get; }

        public float? Height { get; set; }
    }

    /// <summary>数值轴上的一条阈值线（如收缩压 90、舱压 2.0 ATA）。</summary>
    public sealed class ThresholdMark
    {
        public ThresholdMark(double value, string? text = null)
        {
            Value = value;
            Text = text;
        }

        public double Value { get; }

        /// <summary>画在左侧刻度列旁的小字；null 不写。</summary>
        public string? Text { get; }

        public SKColor? Color { get; set; }
    }

    /// <summary>
    /// 通用时间轴单据：横轴为分组（天/阶段）× 时点，纵轴为一条数值轴，
    /// 可叠加多条点系列、事件竖线与阈值线，下方为逐槽附属行。
    /// 适用于抢救记录、高压氧陪舱、镇痛泵日志这类“时刻 + 数值”型文书。
    /// </summary>
    public class TimelineFormDocument : ChartDocument
    {
        public const string DefaultAxisKey = "value";

        public TimelineFormDocument()
        {
            Days = new List<ChartGroup>();
            TimeLabels = new List<string>();
            Series = new List<PointSeries>();
            Cells = new List<CellValue>();
            Events = new List<EventMark>();
            Rows = new List<TimelineRow>();
            Thresholds = new List<ThresholdMark>();
        }

        public IList<ChartGroup> Days { get; }
        public IList<string> TimeLabels { get; }
        public IList<PointSeries> Series { get; }
        public IList<CellValue> Cells { get; }
        public IList<EventMark> Events { get; }
        public IList<TimelineRow> Rows { get; }
        public IList<ThresholdMark> Thresholds { get; }

        /// <summary>纵轴标识，系列必须引用同一个键。</summary>
        public string AxisKey { get; set; } = DefaultAxisKey;

        public double AxisMin { get; set; } = 0d;
        public double AxisMax { get; set; } = 10d;
        public double MinorStep { get; set; } = 1d;
        public double MajorStep { get; set; } = 5d;
        public string AxisFormat { get; set; } = "0.#";

        /// <summary>纵轴说明，竖排画在最左侧空白条里。</summary>
        public string? AxisLabel { get; set; }

        public string DateFormat { get; set; } = "yyyy-MM-dd";

        public int SlotsPerGroup => Math.Max(1, TimeLabels.Count);

        /// <summary>取若干分组生成子文档，用于分页。</summary>
        public TimelineFormDocument Subset(int startGroup, int groupCount)
        {
            TimelineFormDocument copy = CloneShell();
            int perGroup = SlotsPerGroup;
            int firstSlot = startGroup * perGroup;
            int lastSlot = firstSlot + groupCount * perGroup;

            for (int g = startGroup; g < Math.Min(startGroup + groupCount, Days.Count); g++)
            {
                copy.Days.Add(Days[g]);
            }

            foreach (PointSeries series in Series)
            {
                PointSeries clone = new PointSeries(series.Key, series.AxisKey)
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
                        clone.Values[pair.Key - firstSlot] = pair.Value;
                    }
                }
                foreach (KeyValuePair<int, string> pair in series.ValueLabels)
                {
                    if (pair.Key >= firstSlot && pair.Key < lastSlot)
                    {
                        clone.ValueLabels[pair.Key - firstSlot] = pair.Value;
                    }
                }
                copy.Series.Add(clone);
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

        private TimelineFormDocument CloneShell()
        {
            TimelineFormDocument copy = new TimelineFormDocument
            {
                HospitalName = HospitalName,
                FormTitle = FormTitle,
                Patient = Patient,
                HeaderRightNote = HeaderRightNote,
                AxisKey = AxisKey,
                AxisMin = AxisMin,
                AxisMax = AxisMax,
                MinorStep = MinorStep,
                MajorStep = MajorStep,
                AxisFormat = AxisFormat,
                AxisLabel = AxisLabel,
                DateFormat = DateFormat,
            };
            foreach (string label in TimeLabels)
            {
                copy.TimeLabels.Add(label);
            }
            foreach (TimelineRow row in Rows)
            {
                copy.Rows.Add(new TimelineRow(row.Key, row.Label, row.SplitBySlot) { Height = row.Height });
            }
            foreach (ThresholdMark mark in Thresholds)
            {
                copy.Thresholds.Add(new ThresholdMark(mark.Value, mark.Text) { Color = mark.Color });
            }
            foreach (LegendItem item in Legend)
            {
                copy.Legend.Add(item);
            }
            return copy;
        }
    }

    /// <summary>时间轴单据专属尺寸令牌。</summary>
    public sealed class TimelineFormStyle
    {
        public float SlotWidth = 32f;

        /// <summary>左侧刻度数字列宽。</summary>
        public float LabelWidth = 44f;

        public float DateRowHeight = 18f;
        public float TimeRowHeight = 18f;

        /// <summary>数值轴一个细格（MinorStep）的像素高。</summary>
        public float GridCellHeight = 18f;

        public float CellRowHeight = 30f;

        /// <summary>阈值线虚线粗。</summary>
        public float ThresholdStroke = 1.2f;

        /// <summary>事件竖线粗。</summary>
        public float EventStroke = 1f;

        public float EventTextSize = 8.5f;

        public float AxisLabelSize = 8f;

        public TimelineFormStyle Clone() => (TimelineFormStyle)MemberwiseClone();
    }
}
