using System;
using System.Collections.Generic;

namespace OODD.Charts.Geometry
{
    /// <summary>一条刻度线。</summary>
    public readonly struct AxisTick
    {
        public AxisTick(double value, float y, TickKind kind)
        {
            Value = value;
            Y = y;
            Kind = kind;
        }

        /// <summary>该刻度线对应的数据值。</summary>
        public double Value { get; }

        /// <summary>该刻度线的像素 y 坐标（屏幕坐标，向下增大）。</summary>
        public float Y { get; }

        /// <summary>细线 / 粗线 / 粗线带标签。</summary>
        public TickKind Kind { get; }
    }

    /// <summary>
    /// 线性数值轴。负责把数据值映射到像素，或反向求解。
    /// 多条 <see cref="LinearAxis"/> 可以共用同一段像素区间，从而实现
    /// “体温 ℃ 与脉搏 次/分 共用一批横线”这类表单特有的双刻度需求。
    /// </summary>
    public sealed class LinearAxis
    {
        public LinearAxis(string key, double min, double max, float topY, float bottomY,
                          double minorStep, double majorStep, bool increasingUpward = true)
        {
            if (max <= min)
            {
                throw new ArgumentException("轴上限必须大于下限：" + key + " [" + min + "," + max + "]");
            }
            if (minorStep <= 0d || majorStep <= 0d)
            {
                throw new ArgumentException("步长必须为正：" + key);
            }

            Key = key;
            Min = min;
            Max = max;
            TopY = Math.Min(topY, bottomY);
            BottomY = Math.Max(topY, bottomY);
            MinorStep = minorStep;
            MajorStep = majorStep;
            IncreasingUpward = increasingUpward;
        }

        public string Key { get; }
        public double Min { get; }
        public double Max { get; }

        /// <summary>像素区间上边界（较小 y）。</summary>
        public float TopY { get; }

        /// <summary>像素区间下边界（较大 y）。</summary>
        public float BottomY { get; }

        public double MinorStep { get; }
        public double MajorStep { get; }

        /// <summary>值向上增大为 true（体温、胎心、宫口）；向下增大为 false（先露下降 S-3→S+3）。</summary>
        public bool IncreasingUpward { get; }

        /// <summary>轴总像素长度。</summary>
        public float Length => BottomY - TopY;

        /// <summary>每单位值占多少像素。</summary>
        public float PixelsPerUnit => Length / (float)(Max - Min);

        /// <summary>数据值 → 像素 y。</summary>
        public float YOf(double value)
        {
            double t = (value - Min) / (Max - Min);
            if (t < 0d)
            {
                t = 0d;
            }
            else if (t > 1d)
            {
                t = 1d;
            }

            return IncreasingUpward
                ? (float)(BottomY - t * Length)
                : (float)(TopY + t * Length);
        }

        /// <summary>像素 y → 数据值。</summary>
        public double ValueOf(float y)
        {
            double t = (y - TopY) / Length;
            return IncreasingUpward
                ? Min + (1d - t) * (Max - Min)
                : Min + t * (Max - Min);
        }

        /// <summary>
        /// 枚举刻度线。从 <paramref name="from"/> 起按 <see cref="MinorStep"/> 递增，
        /// 落在 <see cref="MajorStep"/> 整数倍上的标记为粗线。
        /// </summary>
        public IEnumerable<AxisTick> Ticks(double? from = null, double? to = null)
        {
            double lo = Math.Max(Min, from ?? Min);
            double hi = Math.Min(Max, to ?? Max);

            // 从 lo 向下对齐到 MinorStep 的整数倍，保证刻度不因起点漂移而错位。
            double start = Math.Floor(lo / MinorStep + 1e-9) * MinorStep;

            for (double v = start; v <= hi + 1e-9; v += MinorStep)
            {
                if (v < Min - 1e-9)
                {
                    continue;
                }

                double ratio = Math.Abs(v / MajorStep - Math.Round(v / MajorStep));
                TickKind kind = ratio < 1e-6 ? TickKind.Major : TickKind.Minor;
                yield return new AxisTick(v, YOf(v), kind);
            }
        }

        /// <summary>该值是否落在粗刻度上。</summary>
        public bool IsMajor(double value)
        {
            return Math.Abs(value / MajorStep - Math.Round(value / MajorStep)) < 1e-6;
        }
    }
}
