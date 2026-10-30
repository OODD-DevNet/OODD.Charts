using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Transit.Stowage
{
    /// <summary>单箱尺寸档：20 尺占一个 bay 位，40 尺占两个相邻 bay 位（左对齐）。</summary>
    public enum ContainerSize
    {
        Twenty = 20,
        Forty = 40,
    }

    /// <summary>一个 bay 的档位定义：bay 号、左右舷 row 数与 tier 数。</summary>
    public sealed class StowageBay
    {
        public StowageBay(int number, int portRows, int starboardRows, int tiers)
        {
            if (portRows < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(portRows), "左舷 row 数不得为负，实到 " + portRows);
            }
            if (starboardRows < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(starboardRows), "右舷 row 数不得为负，实到 " + starboardRows);
            }
            if (portRows + starboardRows <= 0)
            {
                throw new ArgumentException("bay " + number + " 至少要有一排 row。");
            }
            if (tiers <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tiers), "tier 数必须为正，实到 " + tiers);
            }

            Number = number;
            PortRows = portRows;
            StarboardRows = starboardRows;
            Tiers = tiers;
        }

        /// <summary>bay 号（允许跳号：1、3、5…）。</summary>
        public int Number { get; }

        /// <summary>左舷（奇数 row：01、03…）排数。</summary>
        public int PortRows { get; }

        /// <summary>右舷（偶数 row：02、04…）排数。</summary>
        public int StarboardRows { get; }

        /// <summary>层数（tier 1 在最底）。</summary>
        public int Tiers { get; }

        public override string ToString()
        {
            return "Bay" + Number.ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + "(P" + PortRows + "/S" + StarboardRows + "/T" + Tiers + ")";
        }
    }

    /// <summary>一只集装箱：bay / row / tier 位号 + 尺寸、重量、目的港、危险品类别。</summary>
    public sealed class StowageContainer
    {
        public StowageContainer(int bay, int row, int tier, ContainerSize size, double weightKg,
                                string portCode, string? dangerousClass = null,
                                bool overHeight = false, bool isEmpty = false)
        {
            if (size != ContainerSize.Twenty && size != ContainerSize.Forty)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "箱型只支持 20/40 尺，实到 " + size);
            }
            if (weightKg < 0d || double.IsNaN(weightKg) || double.IsInfinity(weightKg))
            {
                throw new ArgumentOutOfRangeException(nameof(weightKg), "重量必须为非负有限值，实到 " + weightKg);
            }
            if (string.IsNullOrEmpty(portCode))
            {
                throw new ArgumentException("目的港不能为空。", nameof(portCode));
            }

            Bay = bay;
            Row = row;
            Tier = tier;
            Size = size;
            WeightKg = weightKg;
            PortCode = portCode;
            DangerousClass = dangerousClass;
            OverHeight = overHeight;
            IsEmpty = isEmpty;
        }

        /// <summary>bay 号（40 尺时是左端 bay 号）。</summary>
        public int Bay { get; }

        /// <summary>row 号：奇数在左舷，偶数在右舷。</summary>
        public int Row { get; }

        /// <summary>tier 号：1 在最底。</summary>
        public int Tier { get; }

        public ContainerSize Size { get; }

        public double WeightKg { get; }

        public string PortCode { get; }

        /// <summary>危险品类别（如 "3"、"6.1"）；null 表示普通箱。</summary>
        public string? DangerousClass { get; }

        /// <summary>超限高箱（角标标记）。</summary>
        public bool OverHeight { get; }

        /// <summary>空箱（角标标记）。</summary>
        public bool IsEmpty { get; }

        /// <summary>占几个 bay 位。</summary>
        public int BaySlots => Size == ContainerSize.Forty ? 2 : 1;

        /// <summary>是否重箱（超过标称 24 t 的样例阈值；版面只用于角标，不参与判定）。</summary>
        public bool IsHeavy => WeightKg >= 24000d;
    }

    /// <summary>
    /// 集装箱积载图文档：一份数据、两个视图（纵向剖面 + 横剖面）。
    /// <para>
    /// 库不内置船舶资料：bay 的跳号规则、每 bay 的 row/tier 数、目的港与箱位全部由文档给；
    /// 样例数据为自造示例，未与任何船公司图样对齐（D22）。
    /// </para>
    /// </summary>
    public sealed class StowageDocument : ChartDocument
    {
        public StowageDocument()
        {
            FormTitle = "集装箱积载图";
            Bays = new List<StowageBay>();
            Containers = new List<StowageContainer>();
        }

        public string? VesselId { get; set; }

        public string? VoyageId { get; set; }

        /// <summary>横剖面图展示的 bay 号；必须在 <see cref="Bays"/> 中存在。</summary>
        public int CrossSectionBay { get; set; }

        public IList<StowageBay> Bays { get; }

        public IList<StowageContainer> Containers { get; }

        /// <summary>按 bay 号找定义；不存在返回 null。</summary>
        public StowageBay? BayOf(int number)
        {
            for (int i = 0; i < Bays.Count; i++)
            {
                if (Bays[i].Number == number)
                {
                    return Bays[i];
                }
            }
            return null;
        }

        /// <summary>row 号是否在该 bay 的排位范围内（奇数左舷、偶数右舷）。</summary>
        public bool RowExists(int bayNumber, int row)
        {
            StowageBay? bay = BayOf(bayNumber);
            if (bay == null || row <= 0)
            {
                return false;
            }
            if ((row & 1) == 1)
            {
                return (row + 1) / 2 <= bay.PortRows;
            }
            return row / 2 <= bay.StarboardRows;
        }

        /// <summary>tier 号是否在该 bay 的层数范围内（1 起）。</summary>
        public bool TierExists(int bayNumber, int tier)
        {
            StowageBay? bay = BayOf(bayNumber);
            return bay != null && tier >= 1 && tier <= bay.Tiers;
        }
    }
}
