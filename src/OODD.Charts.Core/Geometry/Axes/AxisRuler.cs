using System;
using System.Collections.Generic;
using System.Globalization;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 刻度线的粗细层级。四级是为了列车运行图这类"1 分毫线 / 5 分细线 / 10 分中线 / 60 分粗线"
    /// 的版面；三测单只有两级的需求是它的退化情形。
    /// </summary>
    public enum RulerTier
    {
        /// <summary>毫线：比细线更淡的时间格。</summary>
        Hairline = 0,

        /// <summary>细线。</summary>
        Fine = 1,

        /// <summary>中线。</summary>
        Medium = 2,

        /// <summary>粗线：通常是唯一带数字标签的一级。</summary>
        Heavy = 3,
    }

    /// <summary>一条刻度：值、像素位置、层级、是否写标签。</summary>
    public readonly struct AxisMark
    {
        public AxisMark(double value, float position, RulerTier tier, bool labeled)
        {
            Value = value;
            Position = position;
            Tier = tier;
            Labeled = labeled;
        }

        /// <summary>该刻度对应的域值（对数轴上就是<b>真实值</b>，不是幂次）。</summary>
        public double Value { get; }

        /// <summary>该刻度的像素位置。</summary>
        public float Position { get; }

        public RulerTier Tier { get; }

        /// <summary>是否在该刻度处写标签。由层级表决定，不由实现猜。</summary>
        public bool Labeled { get; }

        /// <summary>按不变文化格式化标签：中文区域会把小数点换成逗号，
        /// 那会让 net462 与 net8.0 的导出内容不再逐字节一致。</summary>
        public string Label(string format = "0.####")
        {
            return Value.ToString(format, CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return Value.ToString("0.####", CultureInfo.InvariantCulture) + "@"
                 + Position.ToString("0.###", CultureInfo.InvariantCulture) + " " + Tier;
        }
    }

    /// <summary>
    /// 一级刻度的定义。两种给法：
    /// <list type="bullet">
    /// <item><description>等差：<c>new AxisTier(step, tier, labeled)</c>，按步长枚举。</description></item>
    /// <item><description>表驱动：<c>new AxisTier(tier, labeled, values)</c>，值序列完全由调用方给定。
    /// 对数轴的"真值标签"（0.075、0.25、…、60）与概率轴的分位点表都必须走这一条，
    /// 因为"改表即改版面"是这类频率纸/级配图的可维护性前提。</description></item>
    /// </list>
    /// </summary>
    public readonly struct AxisTier
    {
        /// <summary>等差层级。<paramref name="step"/> 必须为正。</summary>
        public AxisTier(double step, RulerTier tier, bool labeled)
        {
            if (!(step > 0d))
            {
                throw new ArgumentException("刻度步长必须为正，实到 " + step, nameof(step));
            }

            Step = step;
            Values = null;
            Tier = tier;
            Labeled = labeled;
        }

        /// <summary>表驱动层级。值不必有序，<see cref="AxisRuler.Marks"/> 会排序并夹进轴域。</summary>
        public AxisTier(RulerTier tier, bool labeled, IEnumerable<double> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            List<double> copy = new List<double>(values);
            if (copy.Count == 0)
            {
                throw new ArgumentException("表驱动刻度至少要给一个值。", nameof(values));
            }
            foreach (double v in copy)
            {
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    throw new ArgumentException("表驱动刻度含非有限值。", nameof(values));
                }
            }

            Step = 0d;
            Values = copy;
            Tier = tier;
            Labeled = labeled;
        }

        /// <summary>等差步长；为 0 表示本层级是表驱动。</summary>
        public double Step { get; }

        /// <summary>表驱动的值序列；null 表示等差层级。</summary>
        public IReadOnlyList<double>? Values { get; }

        public bool IsTableDriven => Values != null;

        public RulerTier Tier { get; }
        public bool Labeled { get; }
    }

    /// <summary>
    /// 刻度枚举器。把若干 <see cref="AxisTier"/> 合成为一条轴上的全部刻度线。
    /// <para>
    /// 合成规则：<b>同值合并，高层级胜出，labeled 取或</b>。
    /// 于是列车运行图给 1/5/10/60 四级，60 分处那条线既是粗线又带标签，
    /// 而 10 分处那条线不会因为它同时是"1 分的整数倍"就被降成毫线。
    /// </para>
    /// </summary>
    public static class AxisRuler
    {
        /// <summary>同值判定的相对容差。用于合并相邻层级的重合刻度，以及把
        /// 0.30000000000000004 这类步长累加误差归到 0.3 上。</summary>
        private const double MergeEpsilon = 1e-9;

        /// <summary>
        /// 枚举刻度，按域值<b>升序</b>返回（与轴方向无关；落笔时的像素顺序由
        /// <see cref="NumericAxis.PositionOf"/> 负责）。
        /// </summary>
        /// <param name="axis">目标轴。</param>
        /// <param name="tiers">层级表，可多于一项。</param>
        /// <param name="from">只枚举 ≥ 此值的刻度；null 用轴下限。</param>
        /// <param name="to">只枚举 ≤ 此值的刻度；null 用轴上限。</param>
        public static List<AxisMark> Marks(NumericAxis axis, IEnumerable<AxisTier> tiers,
                                          double? from = null, double? to = null)
        {
            if (axis == null)
            {
                throw new ArgumentNullException(nameof(axis));
            }
            if (tiers == null)
            {
                throw new ArgumentNullException(nameof(tiers));
            }

            double lo = Math.Max(axis.Min, from ?? axis.Min);
            double hi = Math.Min(axis.Max, to ?? axis.Max);
            List<AxisMark> raw = new List<AxisMark>();

            foreach (AxisTier tier in tiers)
            {
                foreach (double v in CandidateValues(tier, lo, hi))
                {
                    if (v < lo - MergeEpsilon || v > hi + MergeEpsilon)
                    {
                        continue;
                    }

                    double clamped = v < lo ? lo : v > hi ? hi : v;
                    raw.Add(new AxisMark(clamped, axis.PositionOf(clamped), tier.Tier, tier.Labeled));
                }
            }

            return Merge(raw);
        }

        /// <summary>
        /// 把"每个十进位内的尾数表"扩成整条对数轴的真值表。
        /// <para>
        /// 十进位之间用 <c>×10</c> 与 <c>÷10</c> 逐步复制，不用 <c>Math.Pow(10, n)</c> 反解——
        /// 后者在小指数上会引入末位误差，而级配图、频率纸的标签必须是 0.075 而不是 0.07499999999999998。
        /// 产出的层级是<b>表驱动</b>的，标签即真值。
        /// </para>
        /// </summary>
        /// <param name="tier">该尾数序列落在哪个粗细层级。</param>
        /// <param name="labeled">这些刻度是否写标签。</param>
        /// <param name="mantissas">一个十进位内的尾数，如 {1, 2, 5} 或标准筛孔序列。每项必须为正。</param>
        /// <param name="axisMin">轴域下限（含），必须为正。</param>
        /// <param name="axisMax">轴域上限（含），必须为正。</param>
        public static AxisTier LogTier(RulerTier tier, bool labeled, IEnumerable<double> mantissas,
                                       double axisMin, double axisMax)
        {
            if (mantissas == null)
            {
                throw new ArgumentNullException(nameof(mantissas));
            }
            if (!(axisMin > 0d) || !(axisMax > 0d))
            {
                throw new ArgumentException("对数刻度的轴域必须全为正：[" + axisMin + "," + axisMax + "]", nameof(axisMin));
            }

            double lo = Math.Min(axisMin, axisMax);
            double hi = Math.Max(axisMin, axisMax);
            List<double> values = new List<double>();

            foreach (double baseMantissa in mantissas)
            {
                if (!(baseMantissa > 0d))
                {
                    throw new ArgumentException("尾数必须为正，实到 " + baseMantissa, nameof(mantissas));
                }

                // 向下复制到 < lo。
                List<double> downward = new List<double>();
                double down = baseMantissa;
                while (down / 10d >= lo * (1d - 1e-12))
                {
                    down = down / 10d;
                    downward.Add(down);
                }

                // 向上复制到 > hi。
                List<double> upward = new List<double>();
                double up = baseMantissa;
                while (up * 10d <= hi * (1d + 1e-12))
                {
                    up = up * 10d;
                    upward.Add(up);
                }

                downward.Reverse();
                values.AddRange(downward);
                values.Add(baseMantissa);
                values.AddRange(upward);
            }

            if (values.Count == 0)
            {
                throw new ArgumentException("尾数表在给定轴域内没有产点。", nameof(mantissas));
            }

            return new AxisTier(tier, labeled, values);
        }

        /// <summary>把整条层级序列里的重合刻度合并：层级取高、labeled 取或、位置按低→高重算。</summary>
        private static List<AxisMark> Merge(List<AxisMark> raw)
        {
            raw.Sort((a, b) => a.Value.CompareTo(b.Value));

            List<AxisMark> merged = new List<AxisMark>();
            foreach (AxisMark mark in raw)
            {
                if (merged.Count > 0 && NearlySame(merged[merged.Count - 1].Value, mark.Value))
                {
                    AxisMark prev = merged[merged.Count - 1];
                    RulerTier tier = mark.Tier > prev.Tier ? mark.Tier : prev.Tier;
                    bool labeled = prev.Labeled || mark.Labeled;
                    merged[merged.Count - 1] = new AxisMark(prev.Value, prev.Position, tier, labeled);
                    continue;
                }

                merged.Add(mark);
            }

            return merged;
        }

        private static bool NearlySame(double a, double b)
        {
            double scale = Math.Max(1d, Math.Max(Math.Abs(a), Math.Abs(b)));
            return Math.Abs(a - b) <= MergeEpsilon * scale;
        }

        private static IEnumerable<double> CandidateValues(AxisTier tier, double lo, double hi)
        {
            if (tier.IsTableDriven)
            {
                return tier.Values!;
            }

            return Enumerate(tier.Step, lo, hi);
        }

        private static IEnumerable<double> Enumerate(double step, double lo, double hi)
        {
            // 从 lo 向下对齐到步长整数倍，保证刻度不因起点漂移而错位（与 LinearAxis.Ticks 同判据）。
            double start = Math.Floor(lo / step + MergeEpsilon) * step;
            int guard = 0;
            for (double v = start; v <= hi + step * MergeEpsilon; v += step)
            {
                yield return Snap(v, step);

                // 步长极小时防止枚举失控；调用方要更多刻度应当用表驱动层级。
                if (++guard > 100000)
                {
                    yield break;
                }
            }
        }

        /// <summary>把累加误差吸回最近的步长整数倍：0.30000000000000004 → 0.3。</summary>
        private static double Snap(double value, double step)
        {
            double multiple = Math.Round(value / step, MidpointRounding.AwayFromZero);
            double snapped = multiple * step;
            return Math.Abs(snapped - value) <= Math.Abs(value) * 1e-12 + step * 1e-12 ? snapped : value;
        }
    }
}
