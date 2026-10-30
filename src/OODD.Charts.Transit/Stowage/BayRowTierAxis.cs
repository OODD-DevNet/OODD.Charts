using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Transit.Stowage
{
    /// <summary>
    /// Bay / Row / Tier 三向档位轴（docs/06 §4.12 的 <c>BayRowTierAxis</c>）。
    /// <para>
    /// Core 里没有这个类（积载图专用），本类在其所在域程序集内等价实现：
    /// bay 向与 tier 向直接用 <see cref="WeightedCategoryAxis"/>（bay 等距但编号跳号，tier 自底向上），
    /// row 向是"舷中向两舷非等距"的专用公式，故单独落在本类里，并交给调用方以
    /// <see cref="RowDistance"/> 做单调性断言。
    /// </para>
    /// </summary>
    public sealed class BayRowTierAxis
    {
        private readonly List<int> _bayNumbers;
        private readonly Dictionary<int, int> _bayIndex;
        private float _rowScale;

        public BayRowTierAxis(IReadOnlyList<int> bayNumbers, int tierCount, StowageStyle tokens,
                              SKRect profilePlot, SKRect sectionPlot)
        {
            if (bayNumbers == null)
            {
                throw new ArgumentNullException(nameof(bayNumbers));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (bayNumbers.Count == 0)
            {
                throw new ArgumentException("积载图至少要有一个 bay。", nameof(bayNumbers));
            }
            if (tierCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tierCount), "tier 数必须为正，实到 " + tierCount);
            }

            _bayNumbers = new List<int>(bayNumbers);
            _bayNumbers.Sort();
            _bayIndex = new Dictionary<int, int>();
            List<CategoryStop> stops = new List<CategoryStop>(_bayNumbers.Count);
            for (int i = 0; i < _bayNumbers.Count; i++)
            {
                int number = _bayNumbers[i];
                if (_bayIndex.ContainsKey(number))
                {
                    throw new ArgumentException("bay 号重复：" + number, nameof(bayNumbers));
                }
                _bayIndex.Add(number, i);
                // 每一项的权重 = 它自己那一格的宽度；等权 => 等距（编号照样跳号）。
                stops.Add(new CategoryStop("bay:" + number.ToString(CultureInfo.InvariantCulture),
                    number.ToString("00", CultureInfo.InvariantCulture), 1d));
            }

            BayAxis = new WeightedCategoryAxis(profilePlot.Left, profilePlot.Right, stops,
                AxisDirection.HorizontalRight, profilePlot.Top, profilePlot.Bottom);
            TierCount = tierCount;
            Tokens = tokens;
            ProfilePlot = profilePlot;
            SectionPlot = sectionPlot;

            // row 向距离 = pitch·(n + spread·n·(n−1))，n 为自舷中起算的第几排；
            // 若最外排超出横剖面可用半宽，由 <see cref="FitRows"/> 整体等比收窄（仍保持单调与非等距）。
            _rowScale = 1f;
        }

        public WeightedCategoryAxis BayAxis { get; }

        public int TierCount { get; }

        public StowageStyle Tokens { get; }

        public SKRect ProfilePlot { get; }

        public SKRect SectionPlot { get; }

        /// <summary>文档中出现过的最大 row 号对应的"单位距离"（用于收窄比例）。</summary>
        public float MaxUnitDistance { get; private set; }

        /// <summary>像素/单位距离比例（≤1；由 <see cref="FitRows"/> 设定）。</summary>
        public float RowScale => _rowScale;

        /// <summary>按实际最大 row 号收窄 row 间距，保证最外排仍在横剖面里。</summary>
        public void FitRows(int maxRow)
        {
            if (maxRow <= 0)
            {
                return;
            }
            float unit = (float)UnitRowDistance(maxRow);
            MaxUnitDistance = unit;
            float halfWidth = SectionPlot.Width * 0.5f;
            float allowed = halfWidth - Tokens.RowPitch * Tokens.RowBandFactor * 0.5f - Tokens.ContainerInset - 2f;
            if (allowed <= 0f || unit <= 0f)
            {
                throw new InvalidOperationException(
                    "横剖面宽度不足：可用半宽 " + halfWidth.ToString("0.#", CultureInfo.InvariantCulture)
                    + "，最外排单位距离 " + unit.ToString("0.###", CultureInfo.InvariantCulture));
            }
            float scale = allowed / unit;
            // 只收窄不放宽：RowPitch 是上限意图，放宽会让令牌失真。
            _rowScale = scale < 1f ? scale : 1f;
        }

        /// <summary>row 号对应的"单位距离"（未乘像素比例）。奇数左舷、偶数右舷。</summary>
        public double UnitRowDistance(int row)
        {
            int n = (row & 1) == 1 ? (row + 1) / 2 : row / 2;
            if (n <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(row), "row 号必须为正，实到 " + row);
            }
            double x = n;
            return x + Tokens.RowSpreadFactor * x * (x - 1d);
        }

        /// <summary>row 号到舷中线的像素距离（奇左偶右，非等距且单调增）。</summary>
        public float RowDistance(int row)
        {
            return (float)(UnitRowDistance(row) * Tokens.RowPitch * _rowScale);
        }

        /// <summary>row 中心 x：奇数在中心线左侧，偶数在右侧。</summary>
        public float RowCenter(int row)
        {
            float distance = RowDistance(row);
            return (row & 1) == 1 ? CenterlineX - distance : CenterlineX + distance;
        }

        /// <summary>横剖面的舷中线 x。</summary>
        public float CenterlineX => SectionPlot.MidX;

        /// <summary>bay 在剖面轴上的下标；不存在返回 -1。</summary>
        public int BayIndex(int bayNumber)
        {
            int index;
            return _bayIndex.TryGetValue(bayNumber, out index) ? index : -1;
        }

        public IReadOnlyList<int> BayNumbers => _bayNumbers;

        /// <summary>下一个更高编号的 bay；无则返回 null。</summary>
        public int? NextBay(int bayNumber)
        {
            int index = BayIndex(bayNumber);
            if (index < 0 || index + 1 >= _bayNumbers.Count)
            {
                return null;
            }
            return _bayNumbers[index + 1];
        }

        /// <summary>bay 档位矩形（未内缩）。</summary>
        public SKRect BayBand(int bayNumber)
        {
            int index = BayIndex(bayNumber);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bayNumber), "bay " + bayNumber + " 不在文档中。");
            }
            float left = BayAxis.PositionOf(index);
            float right = left + BayAxis.WidthOf(index);
            return new SKRect(left, ProfilePlot.Top, right, ProfilePlot.Bottom);
        }

        /// <summary>tier 的自底向上档位矩形（横向区间由调用方给）。</summary>
        public SKRect TierBand(int tier, float left, float right)
        {
            if (tier < 1 || tier > TierCount)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), "tier " + tier + " 越界（1.." + TierCount + "）。");
            }
            float bottom = ProfilePlot.Bottom - (tier - 1) * Tokens.TierHeight;
            float top = bottom - Tokens.TierHeight;
            return new SKRect(left, top, right, bottom);
        }
    }
}
