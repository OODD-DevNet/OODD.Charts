using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Transit.OversizeLoad
{
    /// <summary>车货外廓上的一个折点（米）：X 自车头向后，Y 自地面向上。</summary>
    public readonly struct OversizeProfilePoint
    {
        public OversizeProfilePoint(double xMeters, double yMeters)
        {
            XMeters = xMeters;
            YMeters = yMeters;
        }

        public double XMeters { get; }
        public double YMeters { get; }

        public SKPoint ToPoint()
        {
            return new SKPoint((float)XMeters, (float)YMeters);
        }
    }

    /// <summary>一个轴位：沿车长方向的位置（米）与该轴荷（吨）。</summary>
    public sealed class OversizeAxle
    {
        public OversizeAxle(double positionMeters, double loadTonnes)
        {
            if (double.IsNaN(positionMeters) || double.IsInfinity(positionMeters) || positionMeters < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(positionMeters), "轴位必须为非负有限值，实到 " + positionMeters);
            }
            if (double.IsNaN(loadTonnes) || double.IsInfinity(loadTonnes) || loadTonnes < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(loadTonnes), "轴荷必须为非负有限值，实到 " + loadTonnes);
            }

            PositionMeters = positionMeters;
            LoadTonnes = loadTonnes;
        }

        public double PositionMeters { get; }
        public double LoadTonnes { get; }
    }

    /// <summary>
    /// 超限运输许可附图文档：车货侧面外廓折点 + 轴位轴荷 + 允许偏差。
    /// <para>
    /// 库不内置任何车型轮廓：折点全部由文档给，样例是自造示例（非真实车型），
    /// 尺寸数字由坐标回写，调用方无法手填一个与图上不一致的数（docs/06 §4.14）。
    /// </para>
    /// </summary>
    public sealed class OversizeLoadDocument : ChartDocument
    {
        public OversizeLoadDocument()
        {
            FormTitle = "超限运输许可附图";
            Profile = new List<OversizeProfilePoint>();
            Axles = new List<OversizeAxle>();
        }

        public string? PermitNumber { get; set; }

        public string? VehiclePlate { get; set; }

        public string? CargoName { get; set; }

        /// <summary>车货总长（米）。</summary>
        public double LengthMeters { get; set; }

        /// <summary>车货总高（米）。</summary>
        public double HeightMeters { get; set; }

        /// <summary>车货总宽（米，图面上只作文字说明）。</summary>
        public double WidthMeters { get; set; }

        /// <summary>尺寸允许偏差（毫米，图面上以文字说明，不参与回写）。</summary>
        public double ToleranceMillimeters { get; set; } = 50d;

        /// <summary>侧面外廓折点（米，闭合与否均可）。</summary>
        public IList<OversizeProfilePoint> Profile { get; }

        /// <summary>需要做坐标式标注的关键高度（米，自地面起算，按需给出）。</summary>
        public IList<double> KeyHeightsMeters { get; } = new List<double>();

        /// <summary>轴位与轴荷（按位置升序）。</summary>
        public IList<OversizeAxle> Axles { get; }

        /// <summary>轴距链的分段占比：由 0、各轴位、总长切出，全为正。</summary>
        public IReadOnlyList<double> ChainFractions()
        {
            List<double> values = new List<double>(Axles.Count + 1);
            double previous = 0d;
            for (int i = 0; i < Axles.Count; i++)
            {
                double position = Axles[i].PositionMeters;
                if (position <= previous || position >= LengthMeters)
                {
                    throw new InvalidOperationException(
                        "轴位必须严格递增且落在 (0," + LengthMeters + ") 内：第 " + (i + 1)
                        + " 轴 " + position.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));
                }
                values.Add(position - previous);
                previous = position;
            }
            if (LengthMeters > previous)
            {
                values.Add(LengthMeters - previous);
            }
            return values;
        }

        /// <summary>总轴荷（吨）。</summary>
        public double TotalLoadTonnes()
        {
            double total = 0d;
            for (int i = 0; i < Axles.Count; i++)
            {
                total += Axles[i].LoadTonnes;
            }
            return total;
        }
    }
}
