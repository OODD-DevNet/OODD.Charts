using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Medical.Partogram
{
    /// <summary>产程图附属行的稳定标识。用常量而非枚举，便于院方追加自定义行。</summary>
    public static class PartogramFields
    {
        /// <summary>胎心(次/分) —— 专用带，异常值会被红圈注。</summary>
        public const string FetalHeart = "fetal-heart";

        /// <summary>宫缩(次/10min) —— 专用带，画实心方块。</summary>
        public const string Contraction = "contraction";

        public const string BloodPressure = "blood-pressure";
        public const string Pulse = "pulse";
        public const string Temperature = "temperature";
        public const string Oxytocin = "oxytocin";
        public const string VaginalExam = "vaginal-exam";
        public const string Note = "note";

        /// <summary>教科书常用顺序（不含胎心、宫缩两条专用带）。</summary>
        public static IList<string> DefaultOrder { get; } = new List<string>
        {
            BloodPressure, Pulse, Temperature, Oxytocin, VaginalExam, Note,
        };

        public static string DefaultLabel(string key)
        {
            switch (key)
            {
                case FetalHeart: return "胎心(次/分)";
                case Contraction: return "宫缩(次/10min)";
                case BloodPressure: return "血压(mmHg)";
                case Pulse: return "脉搏(次/分)";
                case Temperature: return "体温(℃)";
                case Oxytocin: return "催产素(滴/分)";
                case VaginalExam: return "阴道检查";
                case Note: return "备注";
                default: return key;
            }
        }
    }

    /// <summary>
    /// 产程图刻度参数。左轴宫口扩张自上而下 0→10 cm、右轴胎先露自上而下 S-3→S+3，
    /// 这是国内教科书产程图画法（两线均向右下延伸并在近全开处交汇），
    /// 与 WHO 版向上递增的画法相反；靠 <see cref="PartogramScale.LinearAxisIncreasing"/> 切换。
    /// </summary>
    public sealed class PartogramScale
    {
        public double DilationMinCm = 0d;
        public double DilationMaxCm = 10d;

        /// <summary>每小格多少厘米。实测版式取 1。</summary>
        public double DilationMinorCm = 1d;

        /// <summary>每粗线多少厘米。</summary>
        public double DilationMajorCm = 2d;

        /// <summary>胎先露上下限（相对坐骨棘水平，cm）。</summary>
        public double StationFrom = -3d;
        public double StationTo = 3d;

        /// <summary>先露轴标签步长。</summary>
        public double StationStep = 1d;

        /// <summary>
        /// true = 两轴都向上递增（WHO 画法）；false = 都向下递增（国内教科书画法，默认）。
        /// </summary>
        public bool LinearAxisIncreasing = false;

        /// <summary>胎心率刻度带范围，仅用于决定行高与异常判定的显示口径。</summary>
        public double FetalHeartNormalLow = 120d;
        public double FetalHeartNormalHigh = 160d;

        /// <summary>一个宫缩格内最多叠几个方块。</summary>
        public int ContractionMaxBlocks = 6;

        /// <summary>宫缩持续时间映射到方块高度的区间（秒）。</summary>
        public double ContractionShortSeconds = 20d;
        public double ContractionLongSeconds = 50d;
    }

    /// <summary>一次胎心听诊。</summary>
    public sealed class FetalHeartReading
    {
        public FetalHeartReading(int slot, double bpm)
        {
            Slot = slot;
            BeatsPerMinute = bpm;
        }

        /// <summary>槽位索引；产程图一槽 = 30 分钟。</summary>
        public int Slot { get; set; }

        public double BeatsPerMinute { get; set; }

        /// <summary>覆盖异常判定（如医嘱划定的个体化阈值）。</summary>
        public bool? Abnormal { get; set; }

        public SKColor? Color { get; set; }
    }

    /// <summary>一次宫缩计数。</summary>
    public sealed class ContractionReading
    {
        public ContractionReading(int slot, int countPerTenMinutes)
        {
            Slot = slot;
            CountPerTenMinutes = countPerTenMinutes;
        }

        public int Slot { get; set; }

        /// <summary>每 10 分钟宫缩次数，决定叠几个方块。</summary>
        public int CountPerTenMinutes { get; set; }

        /// <summary>单次持续秒数；决定方块高度。0 表示用基准高。</summary>
        public double DurationSeconds { get; set; }

        public SKColor? Color { get; set; }
    }

    /// <summary>破膜标记：在宫口曲线上当前的位置画一个指向曲线的箭头。</summary>
    public sealed class MembraneMark
    {
        public MembraneMark(int slot)
        {
            Slot = slot;
        }

        public int Slot { get; set; }
        public string Text { get; set; } = "破";

        /// <summary>覆盖箭头指向的宫口值；null 表示取该槽宫口实测值。</summary>
        public double? DilationCm { get; set; }

        public SKColor? Color { get; set; }
    }

    /// <summary>产程图文档。</summary>
    public sealed class PartogramDocument : ChartDocument
    {
        /// <summary>宫口扩张轴键。</summary>
        public const string AxisDilation = "dilation";

        /// <summary>胎先露下降轴键。</summary>
        public const string AxisStation = "station";

        public PartogramDocument()
        {
            FormTitle = "产程图";
            StartTime = new DateTime(2026, 1, 1, 8, 0, 0);
            Hours = 16;
            SlotsPerHour = 2;
            Cervix = new PointSeries("cervix", AxisDilation)
            {
                Symbol = MarkerSymbol.Ring,
                BreakRule = PointBreakRule.Always,
                LegendText = "宫口扩张(cm)",
            };
            Station = new PointSeries("station", AxisStation)
            {
                Symbol = MarkerSymbol.Cross,
                BreakRule = PointBreakRule.Always,
                LegendText = "胎先露(cm)",
            };
            FetalHeart = new List<FetalHeartReading>();
            Contractions = new List<ContractionReading>();
            Membrane = new List<MembraneMark>();
            Cells = new List<CellValue>();
            Scale = new PartogramScale();
            RowOrder = new List<string>(PartogramFields.DefaultOrder);
        }

        /// <summary>产程起点，用于把槽位换算成钟点。</summary>
        public DateTime StartTime { get; set; }

        /// <summary>横轴总小时数。</summary>
        public int Hours { get; set; }

        /// <summary>每小时几槽。默认 2，即一槽 30 分钟。</summary>
        public int SlotsPerHour { get; set; }

        /// <summary>宫口扩张曲线，落在 <c>dilation</c> 轴。</summary>
        public PointSeries Cervix { get; set; }

        /// <summary>胎先露下降曲线，落在 <c>station</c> 轴。</summary>
        public PointSeries Station { get; set; }

        public IList<FetalHeartReading> FetalHeart { get; }
        public IList<ContractionReading> Contractions { get; }
        public IList<MembraneMark> Membrane { get; }

        /// <summary>产程图下方的按小时表格。</summary>
        public IList<CellValue> Cells { get; }

        public PartogramScale Scale { get; set; }

        /// <summary>下方表格行的顺序。</summary>
        public IList<string> RowOrder { get; }

        /// <summary>钟点文本格式。用 "%H" 而不是 "H"：后者会被当成一字符标准格式符而抛 FormatException。</summary>
        public string HourFormat { get; set; } = "%H";

        /// <summary>日期文本格式。</summary>
        public string DateFormat { get; set; } = "M-d";

        // ---- 警戒线 / 处理线 ----

        public bool ShowGuideLines { get; set; } = true;

        /// <summary>警戒线起点小时（自 <see cref="StartTime"/> 起算）。</summary>
        public double AlertStartHour { get; set; }

        /// <summary>警戒线起点宫口，教科书取 3 cm。</summary>
        public double AlertStartCm { get; set; } = 3d;

        /// <summary>警戒线斜率，教科书取活跃期 1 cm/h。</summary>
        public double AlertSlopeCmPerHour { get; set; } = 1d;

        /// <summary>处理线相对警戒线右移多少小时，教科书取 4。</summary>
        public double ActionOffsetHours { get; set; } = 4d;

        public string AlertLineText { get; set; } = "警戒线";
        public string ActionLineText { get; set; } = "处理线";

        /// <summary>总槽数。</summary>
        public int TotalSlots => Math.Max(1, Hours) * Math.Max(1, SlotsPerHour);

        /// <summary>第 n 槽对应的钟点。</summary>
        public DateTime TimeOfSlot(int slot)
        {
            int perHour = Math.Max(1, SlotsPerHour);
            return StartTime.AddHours(slot / perHour).AddMinutes((slot % perHour) * (60 / perHour));
        }

        /// <summary>胎心率是否异常。</summary>
        public bool IsAbnormal(FetalHeartReading reading)
        {
            if (reading.Abnormal.HasValue)
            {
                return reading.Abnormal.Value;
            }
            return reading.BeatsPerMinute < Scale.FetalHeartNormalLow
                   || reading.BeatsPerMinute > Scale.FetalHeartNormalHigh;
        }

        /// <summary>取一段小时区间生成子文档，用于分页。页眉与刻度参数原样带过去。</summary>
        public PartogramDocument Subset(int startHour, int hourCount)
        {
            int perHour = Math.Max(1, SlotsPerHour);
            int firstSlot = startHour * perHour;
            int lastSlot = firstSlot + hourCount * perHour;

            PartogramDocument copy = new PartogramDocument
            {
                HospitalName = HospitalName,
                FormTitle = FormTitle,
                Patient = Patient,
                HeaderRightNote = HeaderRightNote,
                StartTime = TimeOfSlot(firstSlot),
                Hours = hourCount,
                SlotsPerHour = perHour,
                Scale = Scale,
                HourFormat = HourFormat,
                DateFormat = DateFormat,
                ShowGuideLines = ShowGuideLines,
                AlertStartHour = AlertStartHour - startHour,
                AlertStartCm = AlertStartCm,
                AlertSlopeCmPerHour = AlertSlopeCmPerHour,
                ActionOffsetHours = ActionOffsetHours,
                AlertLineText = AlertLineText,
                ActionLineText = ActionLineText,
            };

            foreach (string row in RowOrder)
            {
                copy.RowOrder.Add(row);
            }
            foreach (LegendItem item in Legend)
            {
                copy.Legend.Add(item);
            }

            CopySeries(Cervix, copy.Cervix, firstSlot, lastSlot);
            CopySeries(Station, copy.Station, firstSlot, lastSlot);

            foreach (FetalHeartReading r in FetalHeart)
            {
                if (r.Slot >= firstSlot && r.Slot < lastSlot)
                {
                    copy.FetalHeart.Add(new FetalHeartReading(r.Slot - firstSlot, r.BeatsPerMinute)
                    {
                        Abnormal = r.Abnormal,
                        Color = r.Color,
                    });
                }
            }

            foreach (ContractionReading r in Contractions)
            {
                if (r.Slot >= firstSlot && r.Slot < lastSlot)
                {
                    copy.Contractions.Add(new ContractionReading(r.Slot - firstSlot, r.CountPerTenMinutes)
                    {
                        DurationSeconds = r.DurationSeconds,
                        Color = r.Color,
                    });
                }
            }

            foreach (MembraneMark m in Membrane)
            {
                if (m.Slot >= firstSlot && m.Slot < lastSlot)
                {
                    copy.Membrane.Add(new MembraneMark(m.Slot - firstSlot)
                    {
                        Text = m.Text,
                        DilationCm = m.DilationCm,
                        Color = m.Color,
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

        private static void CopySeries(PointSeries source, PointSeries target, int firstSlot, int lastSlot)
        {
            target.Symbol = source.Symbol;
            target.Color = source.Color;
            target.MarkerRadius = source.MarkerRadius;
            target.LineWidth = source.LineWidth;
            target.Connect = source.Connect;
            target.Dashed = source.Dashed;
            target.BreakRule = source.BreakRule;
            target.LegendText = source.LegendText;
            target.ValueLabelColor = source.ValueLabelColor;
            target.YOffset = source.YOffset;

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
        }
    }
}
