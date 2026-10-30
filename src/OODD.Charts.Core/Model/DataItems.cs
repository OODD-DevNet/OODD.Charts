using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Model
{
    /// <summary>列轴上的一个分组（三测单里 = 一天）。</summary>
    public sealed class ChartGroup
    {
        public ChartGroup(DateTime date)
        {
            Date = date;
        }

        public DateTime Date { get; set; }

        /// <summary>覆盖日期行文本，如 "12-28"。</summary>
        public string? DateText { get; set; }

        /// <summary>覆盖星期行文本，如 "一"。</summary>
        public string? WeekText { get; set; }

        /// <summary>覆盖本组的时段标签（长度须等于 SlotsPerGroup）。</summary>
        public IList<string>? TimeLabels { get; set; }
    }

    /// <summary>
    /// 点系列：把“槽位 → 数值”画成带符号的折线。
    /// 落点由 <see cref="AxisKey"/> 指定的轴决定，因此体温与脉搏可以共用同一批横线。
    /// </summary>
    public sealed class PointSeries
    {
        public PointSeries(string key, string axisKey)
        {
            Key = key;
            AxisKey = axisKey;
        }

        public string Key { get; }

        /// <summary>落在哪个 <see cref="Geometry.LinearAxis"/> 上。</summary>
        public string AxisKey { get; set; }

        public MarkerSymbol Symbol { get; set; } = MarkerSymbol.Dot;
        public SKColor Color { get; set; } = SKColors.Black;
        public float MarkerRadius { get; set; } = -1f;
        public float LineWidth { get; set; } = -1f;
        public bool Connect { get; set; } = true;
        public bool Dashed { get; set; }
        public PointBreakRule BreakRule { get; set; } = PointBreakRule.BreakOnGap;

        /// <summary>图例文本；null 表示不进图例。</summary>
        public string? LegendText { get; set; }

        /// <summary>槽位索引 → 数值。保持插入序无关，内部按槽号取用。</summary>
        public IDictionary<int, double> Values { get; } = new SortedDictionary<int, double>();

        /// <summary>槽位索引 → 画在点旁的小字（如物理降温后的降幅）。</summary>
        public IDictionary<int, string> ValueLabels { get; } = new Dictionary<int, string>();

        /// <summary>小字颜色；null 跟随 <see cref="Color"/>。</summary>
        public SKColor? ValueLabelColor { get; set; }

        /// <summary>整体纵向偏移（px），用于把呼吸点画在脉搏曲线上方而不与网格冲突。</summary>
        public float YOffset { get; set; }

        /// <summary>取值；不存在返回 null。</summary>
        public double? ValueAt(int slot)
        {
            double value;
            return Values.TryGetValue(slot, out value) ? value : (double?)null;
        }

        /// <summary>
        /// 取该槽的值；缺测时按相邻两次实测线性内插，超出实测范围时取最近一次实测值。
        /// 用于破膜标记这类要落在曲线上、但不一定正好落在测量时刻的情形。
        /// </summary>
        public double? ValueInterpolatedAt(int slot)
        {
            if (Values.Count == 0)
            {
                return null;
            }

            double exact;
            if (Values.TryGetValue(slot, out exact))
            {
                return exact;
            }

            int below = int.MinValue;
            int above = int.MaxValue;
            foreach (KeyValuePair<int, double> pair in Values)
            {
                if (pair.Key < slot && pair.Key > below)
                {
                    below = pair.Key;
                }
                if (pair.Key > slot && pair.Key < above)
                {
                    above = pair.Key;
                }
            }

            double belowValue = double.NaN;
            double aboveValue = double.NaN;
            bool hasBelow = false;
            bool hasAbove = false;
            if (below != int.MinValue)
            {
                hasBelow = Values.TryGetValue(below, out belowValue);
            }
            if (above != int.MaxValue)
            {
                hasAbove = Values.TryGetValue(above, out aboveValue);
            }

            if (hasBelow && hasAbove)
            {
                double t = (double)(slot - below) / (above - below);
                return belowValue + (aboveValue - belowValue) * t;
            }
            if (hasBelow)
            {
                return belowValue;
            }
            if (hasAbove)
            {
                return aboveValue;
            }
            return null;
        }
    }

    /// <summary>附属表格里一个单元格的文本值。</summary>
    public sealed class CellValue
    {
        public CellValue(string rowKey, int slot, string text)
        {
            RowKey = rowKey;
            Slot = slot;
            Text = text;
        }

        public string RowKey { get; set; }

        /// <summary>槽位索引；对于按分组合并的行，会自动归并到所在组。</summary>
        public int Slot { get; set; }

        public string Text { get; set; }
        public SKColor? Color { get; set; }
        public CellAlign Align { get; set; } = CellAlign.Center;
        public float? Size { get; set; }

        /// <summary>同一合并格内的序号（从 0 起）。</summary>
        public int Occurrence { get; set; }

        /// <summary>该格内总条数，用于切分格高。设为 1 以下表示由引擎自动统计。</summary>
        public int OccurrenceCount { get; set; } = 0;
    }

    /// <summary>事件标记：三测单 34–35℃ 带内的“手术 / 分娩 / 入院”红字。</summary>
    public sealed class EventMark
    {
        public EventMark(int slot, string text)
        {
            Slot = slot;
            Text = text;
        }

        public int Slot { get; set; }
        public string Text { get; set; }

        /// <summary>术后/产后第 n 天；null 表示不写天数。</summary>
        public int? DayCount { get; set; }

        public EventMarkOrientation Orientation { get; set; } = EventMarkOrientation.Vertical;
        public SKColor? Color { get; set; }
        public float? Size { get; set; }

        /// <summary>是否在该事件前画红色竖分隔线。</summary>
        public bool DrawSeparator { get; set; } = true;
    }
}
