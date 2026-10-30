using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.Pain
{
    /// <summary>疼痛评估记录单附属行的稳定标识。用常量而非枚举，便于院方追加自定义行。</summary>
    public static class PainFields
    {
        public const string Site = "site";
        public const string Character = "character";
        public const string Intervention = "intervention";
        public const string Medication = "medication";
        public const string Assessor = "assessor";
        public const string Note = "note";

        public static IList<string> DefaultOrder { get; } = new List<string>
        {
            Site, Character, Intervention, Medication, Assessor, Note,
        };

        public static string DefaultLabel(string key)
        {
            switch (key)
            {
                case Site: return "疼痛部位";
                case Character: return "疼痛性质";
                case Intervention: return "干预措施";
                case Medication: return "药物/剂量";
                case Assessor: return "评估者";
                case Note: return "备注";
                default: return key;
            }
        }
    }

    /// <summary>NRS 评分轴与重度阈值。</summary>
    public sealed class PainScale
    {
        public double MinScore = 0d;
        public double MaxScore = 10d;

        /// <summary>每 1 分一条细线。</summary>
        public double MinorStep = 1d;

        /// <summary>每 2 分一条粗线并标数。</summary>
        public double MajorStep = 2d;

        /// <summary>重度疼痛阈值，NRS 通常取 7。</summary>
        public double SevereThreshold = 7d;

        public bool ShowSevereLine = true;
    }

    /// <summary>
    /// 疼痛评估记录单文档（NRS 评分）。一槽 = 一次评估，
    /// 静息痛与活动痛各一条折线，附属行记录部位、性质、干预与用药。
    /// </summary>
    public sealed class PainChartDocument : ChartDocument
    {
        public PainChartDocument()
        {
            FormTitle = "疼痛评估记录单";
            Days = new List<ChartGroup>();
            Series = new List<PointSeries>();
            Cells = new List<CellValue>();
            Scale = new PainScale();
            TimeLabels = new List<string> { "02", "06", "10", "14", "18", "22" };
            RowOrder = new List<string>(PainFields.DefaultOrder);
        }

        /// <summary>列分组，通常一天一组。</summary>
        public IList<ChartGroup> Days { get; }

        public IList<PointSeries> Series { get; }
        public IList<CellValue> Cells { get; }

        public PainScale Scale { get; set; }

        /// <summary>一组内各评估时点的标签，长度须等于 <c>Style.Grid.SlotsPerGroup</c>。</summary>
        public IList<string> TimeLabels { get; }

        /// <summary>附属行顺序。</summary>
        public IList<string> RowOrder { get; }

        public string[] WeekNames { get; set; } = { "日", "一", "二", "三", "四", "五", "六" };

        public string DateFormat { get; set; } = "M-d";

        /// <summary>取一段分组生成子文档，用于分页。</summary>
        public PainChartDocument Subset(int startGroup, int groupCount)
        {
            PainChartDocument copy = CloneShell();
            int slotsPerGroup = Math.Max(1, TimeLabels.Count);
            int firstSlot = startGroup * slotsPerGroup;
            int lastSlot = firstSlot + groupCount * slotsPerGroup;

            for (int g = startGroup; g < Math.Min(startGroup + groupCount, Days.Count); g++)
            {
                copy.Days.Add(Days[g]);
            }

            foreach (PointSeries series in Series)
            {
                PointSeries remapped = CopySeries(series, firstSlot, lastSlot);
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

            return copy;
        }

        private static PointSeries CopySeries(PointSeries source, int firstSlot, int lastSlot)
        {
            PointSeries target = new PointSeries(source.Key, source.AxisKey)
            {
                Symbol = source.Symbol,
                Color = source.Color,
                MarkerRadius = source.MarkerRadius,
                LineWidth = source.LineWidth,
                Connect = source.Connect,
                Dashed = source.Dashed,
                BreakRule = source.BreakRule,
                LegendText = source.LegendText,
                ValueLabelColor = source.ValueLabelColor,
                YOffset = source.YOffset,
            };

            foreach (KeyValuePair<int, double> pair in source.Values)
            {
                if (pair.Key >= firstSlot && pair.Key < lastSlot)
                {
                    target.Values[pair.Key - firstSlot] = pair.Value;
                }
            }
            foreach (KeyValuePair<int, string> pair in source.ValueLabels)
            {
                if (pair.Key >= firstSlot && pair.Key < lastSlot)
                {
                    target.ValueLabels[pair.Key - firstSlot] = pair.Value;
                }
            }
            return target;
        }

        private PainChartDocument CloneShell()
        {
            PainChartDocument copy = new PainChartDocument
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
