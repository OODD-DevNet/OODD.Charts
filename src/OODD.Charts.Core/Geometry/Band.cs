using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>波段内的一个子行。</summary>
    public sealed class BandRow
    {
        public BandRow(string key, SKRect rect)
        {
            Key = key;
            Rect = rect;
        }

        public string Key { get; }
        public SKRect Rect { get; }
    }

    /// <summary>
    /// 表单的一条水平区域（页眉带、日期表头带、附属表格带、页脚带）。
    /// 网格带不用本类型，它由 <see cref="LinearAxis"/> 直接界定。
    /// </summary>
    public sealed class Band
    {
        private readonly Dictionary<string, BandRow> _byKey;

        public Band(string key, SKRect rect, IEnumerable<BandRow> rows)
        {
            Key = key;
            Rect = rect;
            Rows = new List<BandRow>(rows);
            _byKey = new Dictionary<string, BandRow>(StringComparer.Ordinal);
            foreach (BandRow row in Rows)
            {
                _byKey[row.Key] = row;
            }
        }

        public string Key { get; }
        public SKRect Rect { get; }
        public IReadOnlyList<BandRow> Rows { get; }

        /// <summary>按子行键取矩形；不存在返回 null。</summary>
        public BandRow? Row(string key)
        {
            BandRow? found;
            return _byKey.TryGetValue(key, out found) ? found : null;
        }

        public SKRect RowRect(string key)
        {
            BandRow? row = Row(key);
            if (row == null)
            {
                throw new KeyNotFoundException("波段 " + Key + " 内不存在子行 " + key);
            }
            return row.Rect;
        }
    }

    /// <summary>附属表格的一行定义。</summary>
    public sealed class CellRow
    {
        public CellRow(string key, string label, bool splitBySlot, SKRect rect)
        {
            Key = key;
            Label = label;
            SplitBySlot = splitBySlot;
            Rect = rect;
        }

        /// <summary>稳定标识，供 <c>CellValue.RowKey</c> 引用。</summary>
        public string Key { get; }

        /// <summary>显示在左侧标签列的文字。</summary>
        public string Label { get; }

        /// <summary>true = 保持逐槽细分；false = 按分组合并（如一天一行只填一次）。</summary>
        public bool SplitBySlot { get; }

        public SKRect Rect { get; }
    }

    /// <summary>网格左侧的刻度标签列。</summary>
    public sealed class LabelColumn
    {
        public LabelColumn(string key, string axisKey, SKRect rect, string format, double labelFrom, double labelTo)
        {
            Key = key;
            AxisKey = axisKey;
            Rect = rect;
            Format = format;
            LabelFrom = labelFrom;
            LabelTo = labelTo;
        }

        public string Key { get; }

        /// <summary>该列显示哪个轴的数值。</summary>
        public string AxisKey { get; }

        public SKRect Rect { get; }

        /// <summary>数值格式化串，如 "0.#"。</summary>
        public string Format { get; }

        /// <summary>只在该闭区间内的粗刻度才写标签（三测单体温轴画到 34/42 的线但不标数）。</summary>
        public double LabelFrom { get; }
        public double LabelTo { get; }
    }
}
