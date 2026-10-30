using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 分类轴上的一个停靠项（一站、一个日期、一个 Bay 号）。
    /// </summary>
    public readonly struct CategoryStop
    {
        /// <param name="key">稳定标识，允许跳号（如车站编号 K0、K3、K7），查找只认它。</param>
        /// <param name="label">显示文字。</param>
        /// <param name="weight">本项<b>之后</b>那一段的相对宽度（列车运行图用区间运行时分的分钟数）。
        /// 末项通常给 0，表示它落在轴的收口端。</param>
        public CategoryStop(string key, string label, double weight)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("停靠项必须有 key。", nameof(key));
            }
            if (weight < 0d || double.IsNaN(weight) || double.IsInfinity(weight))
            {
                throw new ArgumentException("权重必须为非负有限值，实到 " + weight, nameof(weight));
            }

            Key = key;
            Label = label ?? string.Empty;
            Weight = weight;
        }

        public string Key { get; }
        public string Label { get; }
        public double Weight { get; }

        public override string ToString() => Key + "(" + Label + ")w=" + Weight;
    }

    /// <summary>
    /// 非等距分类轴：每格宽度按权重分配。
    /// <para>
    /// 用途是列车运行图"按区间运行时分定站名线"、集装箱积载图 Bay 位不等宽、
    /// 药敏报告的抑菌圈刻度条这类<b>格距承载数据</b>的版面。
    /// 与 <see cref="NumericAxis"/> 的区别：这里的"位置"不是连续值的映射，
    /// 而是离散项之间的一段段带宽，所以反查只能问"第几项"，不能问"这个像素是什么值"。
    /// </para>
    /// </summary>
    public sealed class WeightedCategoryAxis
    {
        private readonly CategoryStop[] _stops;
        private readonly double[] _starts;
        private readonly double[] _widths;

        /// <param name="from">像素起点（水平轴为左，垂直轴为上；不要求小于 to）。</param>
        /// <param name="to">像素终点。</param>
        /// <param name="stops">停靠项，顺序即版面顺序。</param>
        /// <param name="direction">轴走向；只影响 <see cref="BandOf(int)"/> 产出的矩形朝向。</param>
        /// <param name="crossStart"><see cref="BandOf(int)"/> 用的另一维起点。</param>
        /// <param name="crossEnd"><see cref="BandOf(int)"/> 用的另一维终点。</param>
        public WeightedCategoryAxis(float from, float to, IEnumerable<CategoryStop> stops,
                                    AxisDirection direction = AxisDirection.HorizontalRight,
                                    float crossStart = 0f, float crossEnd = 0f)
        {
            if (stops == null)
            {
                throw new ArgumentNullException(nameof(stops));
            }

            List<CategoryStop> list = new List<CategoryStop>(stops);
            if (list.Count == 0)
            {
                throw new ArgumentException("分类轴至少要有一个停靠项。", nameof(stops));
            }
            if (to == from)
            {
                throw new ArgumentException("分类轴长度为 0：from=" + from + " to=" + to, nameof(to));
            }

            double total = 0d;
            foreach (CategoryStop stop in list)
            {
                total += stop.Weight;
            }
            if (total <= 0d)
            {
                throw new ArgumentException("权重总和必须为正，否则无法分配格距。", nameof(stops));
            }

            _stops = list.ToArray();
            _starts = new double[_stops.Length];
            _widths = new double[_stops.Length];

            float span = to - from;
            double cursor = 0d;
            for (int i = 0; i < _stops.Length; i++)
            {
                // 起点按累计权重算，末项的收口直接落到 to，避免浮点累加让最后一格差半像素。
                _starts[i] = from + (float)((cursor / total) * span);
                _widths[i] = i == _stops.Length - 1
                    ? to - _starts[i]
                    : (float)((_stops[i].Weight / total) * span);
                cursor += _stops[i].Weight;
            }

            From = from;
            To = to;
            TotalWeight = total;
            Direction = direction;
            CrossStart = crossStart;
            CrossEnd = crossEnd;
        }

        public float From { get; }
        public float To { get; }
        public AxisDirection Direction { get; }
        public float CrossStart { get; }
        public float CrossEnd { get; }

        /// <summary>停靠项数。</summary>
        public int Count => _stops.Length;

        /// <summary>权重总和（归一化前的分母）。</summary>
        public double TotalWeight { get; }

        /// <summary>轴像素长度，恒为非负。</summary>
        public float Length => Math.Abs(To - From);

        /// <summary>第 index 个停靠项的分界像素。</summary>
        public float PositionOf(int index)
        {
            ThrowIfOutOfRange(index);
            return (float)_starts[index];
        }

        /// <summary>第 index 项所占有格的宽度（与权重成正比）。</summary>
        public float WidthOf(int index)
        {
            ThrowIfOutOfRange(index);
            return (float)_widths[index];
        }

        /// <summary>第 index 项所占有格的中点。</summary>
        public float CenterOf(int index)
        {
            ThrowIfOutOfRange(index);
            return (float)(_starts[index] + _widths[index] / 2d);
        }

        /// <summary>第 index 项之后、到下一项收口的带（跨维取构造时给的 cross 区间）。</summary>
        public SKRect BandOf(int index) => BandOf(index, CrossStart, CrossEnd);

        /// <summary>同上，跨维区间由调用方给（版面里这一维通常只有落笔时才知道）。</summary>
        public SKRect BandOf(int index, float crossStart, float crossEnd)
        {
            ThrowIfOutOfRange(index);
            double start = _starts[index];
            double end = start + _widths[index];
            bool horizontal = Direction == AxisDirection.HorizontalRight
                           || Direction == AxisDirection.HorizontalLeft;

            return horizontal
                ? new SKRect((float)start, Math.Min(crossStart, crossEnd), (float)end, Math.Max(crossStart, crossEnd))
                : new SKRect(Math.Min(crossStart, crossEnd), (float)start, Math.Max(crossStart, crossEnd), (float)end);
        }

        public string LabelOf(int index)
        {
            ThrowIfOutOfRange(index);
            return _stops[index].Label;
        }

        public string KeyOf(int index)
        {
            ThrowIfOutOfRange(index);
            return _stops[index].Key;
        }

        public double WeightOf(int index)
        {
            ThrowIfOutOfRange(index);
            return _stops[index].Weight;
        }

        public CategoryStop StopOf(int index)
        {
            ThrowIfOutOfRange(index);
            return _stops[index];
        }

        /// <summary>按 key 找下标；不存在返回 -1（key 允许跳号，因此不能拿下标当编号用）。</summary>
        public int IndexOf(string key)
        {
            if (key == null)
            {
                return -1;
            }

            for (int i = 0; i < _stops.Length; i++)
            {
                if (string.Equals(_stops[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>像素落在哪一格里（含左边界，末格含右边界）。不在轴范围内返回 -1。</summary>
        public int IndexAt(float position)
        {
            float lo = Math.Min(From, To);
            float hi = Math.Max(From, To);
            if (position < lo - PixelTolerance || position > hi + PixelTolerance)
            {
                return -1;
            }

            if (To >= From)
            {
                for (int i = _stops.Length - 1; i >= 0; i--)
                {
                    if (position >= _starts[i] - PixelTolerance)
                    {
                        return i;
                    }
                }

                return 0;
            }

            // 像素递减的走向（HorizontalLeft、自下而上的垂直轴）：下标越大，像素越小。
            int result = 0;
            for (int i = 0; i < _stops.Length; i++)
            {
                if (position <= _starts[i] + PixelTolerance)
                {
                    result = i;
                }
            }

            return result;
        }

        private const float PixelTolerance = 1e-3f;

        private void ThrowIfOutOfRange(int index)
        {
            if (index < 0 || index >= _stops.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "分类轴下标越界：" + index + "，共 " + _stops.Length + " 项。");
            }
        }
    }
}
