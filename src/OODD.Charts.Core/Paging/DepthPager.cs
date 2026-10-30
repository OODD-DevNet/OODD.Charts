using System;
using System.Collections.Generic;
using System.Globalization;

namespace OODD.Charts
{
    /// <summary>分页方向：纵向切页（一页接一页往下）或横向切页（长图左右展开成多页）。</summary>
    public enum PagerDirection
    {
        /// <summary>纵向：如测井成果图按深度自上而下续页。</summary>
        Vertical,
        /// <summary>横向：如管道 CIPS 电位记录长图按里程左右展开成多页。</summary>
        Horizontal,
    }

    /// <summary>
    /// 一段连续域值区间，供页眉写"起/止 桩号或深度"。
    /// 注意：本类型刻意不声明为 readonly struct——<see cref="IsLast"/> 需要由切分器置位，
    /// 而 readonly struct 禁止可变自动属性，三套 TFM 下均无法编译。
    /// </summary>
    public struct DomainRange
    {
        /// <summary>构造一段域区间；<paramref name="index"/> 为从 0 起的段序号。</summary>
        public DomainRange(double start, double end, int index)
        {
            Start = start;
            End = end;
            Index = index;
            IsLast = false;
        }

        /// <summary>区间起点（含）。</summary>
        public double Start { get; }

        /// <summary>区间终点（末页含，其余页作为下一段起点出现）。</summary>
        public double End { get; }

        /// <summary>段序号，从 0 起。</summary>
        public int Index { get; }

        /// <summary>是否最后一段，由切分器置位。</summary>
        public bool IsLast { get; set; }

        /// <summary>
        /// 以不变文化（InvariantCulture）输出 "[start,end]"，数字用 "0.###"，
        /// 保证中文环境下面值稳定。
        /// </summary>
        public override string ToString()
        {
            string start = Start.ToString("0.###", CultureInfo.InvariantCulture);
            string end = End.ToString("0.###", CultureInfo.InvariantCulture);
            return "[" + start + "," + end + "]";
        }
    }

    /// <summary>
    /// 按连续域值（深度、里程、时间）切页的切分器。与 <see cref="ChartPager"/>
    /// （按逻辑组数切，如体温单按天）互补：本类依据的是连续量而非离散组，
    /// 适用于测井成果图（纵向按深度续页）与管道 CIPS 电位记录图（横向按里程分页）。
    /// 全部方法为纯函数，无副作用，不依赖 SkiaSharp。
    /// </summary>
    public static class DepthPager
    {
        /// <summary>
        /// 把 [from,to] 按 segmentLength 切分。返回的每段 End 不超过 to，最后一段允许短。
        /// 内部一律用 "from + i*segmentLength" 的乘法式算第 i 段起点，
        /// <b>不用</b> "v += segmentLength" 的累加式——0.1 之类的浮点值逐段累加会漂移，
        /// 不同 TFM/不同运行时的舍入差异可能抖出不同的段数与接缝，续页连续性即被破坏。
        /// </summary>
        /// <param name="from">域值起点。</param>
        /// <param name="to">域值终点，必须大于 from。</param>
        /// <param name="segmentLength">每页覆盖的域值长度，必须为正。</param>
        /// <exception cref="ArgumentException">segmentLength &lt;= 0 或 to &lt;= from 时抛出。</exception>
        public static List<DomainRange> Split(double from, double to, double segmentLength)
        {
            return SplitCore(from, to, segmentLength, 0d);
        }

        /// <summary>
        /// 与 <see cref="Split"/> 相同，但每段向两端各外扩 overlap（用于续页接缝处曲线不断），
        /// 外扩不越出 [from,to]。起始段不外扩左端、末段不外扩右端（被 from/to 截住）。
        /// </summary>
        /// <param name="from">域值起点。</param>
        /// <param name="to">域值终点，必须大于 from。</param>
        /// <param name="segmentLength">每页覆盖的域值长度（不含外扩），必须为正。</param>
        /// <param name="overlap">两端外扩长度，必须非负。</param>
        /// <exception cref="ArgumentException">segmentLength &lt;= 0、overlap &lt; 0 或 to &lt;= from 时抛出。</exception>
        public static List<DomainRange> SplitWithOverlap(double from, double to, double segmentLength, double overlap)
        {
            if (overlap < 0d)
            {
                throw new ArgumentException("外扩长度不能为负：" + overlap, nameof(overlap));
            }

            return SplitCore(from, to, segmentLength, overlap);
        }

        /// <summary>
        /// 给定点序列，找出落在 <paramref name="range"/> [Start,End]（含端点）内的下标区间
        /// [firstIndex,lastIndex]；无命中（含空序列）返回 (0,-1) 而不是抛异常——空数据是常态。
        /// 假定 <paramref name="domainValues"/> 按域值<b>单调递增</b>排列（测井深度、桩号、时间戳
        /// 天然如此）；乱序输入只保证 first/last 是命中下标的最小/最大值，不保证中间点都命中。
        /// </summary>
        /// <param name="domainValues">各点的域值序列，假定单调递增。</param>
        /// <param name="range">目标段。</param>
        /// <param name="firstIndex">输出：首个命中下标；无命中为 0。</param>
        /// <param name="lastIndex">输出：末个命中下标；无命中为 -1。</param>
        public static void IndexRangeOf(IReadOnlyList<double> domainValues, DomainRange range,
                                        out int firstIndex, out int lastIndex)
        {
            firstIndex = 0;
            lastIndex = -1;
            if (domainValues == null || domainValues.Count == 0)
            {
                return;
            }

            // 线性扫描一次即可：命中区间在单调序列上必然连续。
            for (int i = 0; i < domainValues.Count; i++)
            {
                double v = domainValues[i];
                if (v >= range.Start && v <= range.End)
                {
                    if (lastIndex < 0)
                    {
                        firstIndex = i;
                    }
                    lastIndex = i;
                }
                else if (lastIndex >= 0 && v > range.End)
                {
                    // 已过界，单调前提下无需再扫。
                    break;
                }
            }
        }

        private static List<DomainRange> SplitCore(double from, double to, double segmentLength, double overlap)
        {
            if (segmentLength <= 0d)
            {
                throw new ArgumentException("分段长度必须为正：" + segmentLength, nameof(segmentLength));
            }
            if (to <= from)
            {
                throw new ArgumentException("域终点必须大于起点：[" + from + "," + to + "]");
            }

            // 段数 = ceil((to-from)/segmentLength)。当 (to-from) 恰为 segmentLength 的
            // 整数倍时，除法结果可能是 3.0000000000000004 这类略大于整数的浮点噪声，
            // 直接 ceil 会多切出一段空页；先减 1e-9 容差再取整，把噪声压回整数。
            double ratio = (to - from) / segmentLength;
            int count = (int)Math.Ceiling(ratio - 1e-9);
            if (count < 1)
            {
                count = 1;
            }

            List<DomainRange> ranges = new List<DomainRange>(count);
            for (int i = 0; i < count; i++)
            {
                // 起点/终点都用 from + i*segmentLength 乘法式计算，绝不累加，见类注释。
                double start = from + i * segmentLength;
                double end = i == count - 1 ? to : from + (i + 1) * segmentLength;
                if (end > to)
                {
                    end = to;
                }

                if (overlap > 0d)
                {
                    start = Math.Max(from, start - overlap);
                    end = Math.Min(to, end + overlap);
                }

                DomainRange range = new DomainRange(start, end, i);
                range.IsLast = i == count - 1;
                ranges.Add(range);
            }
            return ranges;
        }
    }
}
