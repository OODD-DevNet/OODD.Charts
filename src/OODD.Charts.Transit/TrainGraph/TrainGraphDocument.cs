using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Transit.TrainGraph
{
    /// <summary>运行图上的一个车站。停靠项权重是该站到下一站的区间运行时分（分钟）。</summary>
    public sealed class TrainStation
    {
        public TrainStation(string key, string name, double runMinutesToNext)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("车站必须有 key。", nameof(key));
            }
            if (runMinutesToNext < 0d || double.IsNaN(runMinutesToNext) || double.IsInfinity(runMinutesToNext))
            {
                throw new ArgumentException("区间运行时分必须是非负有限值，实到 " + runMinutesToNext, nameof(runMinutesToNext));
            }

            Key = key;
            Name = name ?? key;
            RunMinutesToNext = runMinutesToNext;
        }

        public string Key { get; }
        public string Name { get; }

        /// <summary>本站到下一站的区间运行时分（分钟）；末站给 0。</summary>
        public double RunMinutesToNext { get; }
    }

    /// <summary>列车在某一站的到发时刻（分钟，自 <see cref="TrainGraphDocument.StartClockMinutes"/> 起算）。</summary>
    public sealed class TrainStop
    {
        public TrainStop(int stationIndex, double arriveMinutes, double departMinutes)
        {
            if (stationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stationIndex), "站序不能为负：" + stationIndex);
            }
            if (double.IsNaN(arriveMinutes) || double.IsNaN(departMinutes)
                || double.IsInfinity(arriveMinutes) || double.IsInfinity(departMinutes))
            {
                throw new ArgumentException("到发时刻必须是有限值。", nameof(arriveMinutes));
            }
            if (departMinutes < arriveMinutes)
            {
                throw new ArgumentException("发车时刻早于到达时刻：到 " + arriveMinutes + " 发 " + departMinutes,
                    nameof(departMinutes));
            }

            StationIndex = stationIndex;
            ArriveMinutes = arriveMinutes;
            DepartMinutes = departMinutes;
        }

        public int StationIndex { get; }
        public double ArriveMinutes { get; }
        public double DepartMinutes { get; }

        /// <summary>停站分。</summary>
        public double DwellMinutes => DepartMinutes - ArriveMinutes;
    }

    /// <summary>一趟列车：车次 + 逐站到发时刻。运行线在区间内是解析斜线，不做逐点折线。</summary>
    public sealed class TrainRun
    {
        public TrainRun(string code, bool isUp)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("车次不能为空。", nameof(code));
            }

            Code = code;
            IsUp = isUp;
            Stops = new List<TrainStop>();
        }

        public string Code { get; }

        /// <summary>true = 上行（默认红色），false = 下行（默认蓝色）。</summary>
        public bool IsUp { get; }

        /// <summary>覆写运行线颜色；null 用色板的方向槽位。</summary>
        public SKColor? Color { get; set; }

        public IList<TrainStop> Stops { get; }

        /// <summary>追加一个停站；返回自身便于链式书写。</summary>
        public TrainRun Stop(int stationIndex, double arriveMinutes, double departMinutes)
        {
            Stops.Add(new TrainStop(stationIndex, arriveMinutes, departMinutes));
            return this;
        }
    }

    /// <summary>天窗（施工封锁时段）：时刻区间 + 站区间，二者同时约束带的形状。</summary>
    public sealed class TrainWindow
    {
        public TrainWindow(string key, string label, double fromMinutes, double toMinutes,
                           int fromStation, int toStation, BandFill fill = BandFill.Hatch)
        {
            if (fromStation > toStation)
            {
                throw new ArgumentException("天窗站区间的起点不能晚于终点：" + key, nameof(fromStation));
            }

            Key = key;
            Label = label ?? key;
            FromMinutes = fromMinutes;
            ToMinutes = toMinutes;
            FromStation = fromStation;
            ToStation = toStation;
            Fill = fill;

            // 用 Core 的 SpanBand 做一次校验：时刻倒置、非有限值都会当场抛出。
            ToBand();
        }

        public string Key { get; }
        public string Label { get; }
        public double FromMinutes { get; }
        public double ToMinutes { get; }

        /// <summary>起始站序（含）。</summary>
        public int FromStation { get; }

        /// <summary>终止站序（含）。</summary>
        public int ToStation { get; }

        public BandFill Fill { get; }

        /// <summary>转成 Core 的时段带（时刻 + 分类双约束）。</summary>
        public SpanBand ToBand()
        {
            return new SpanBand(Key, FromMinutes, ToMinutes, FromStation, ToStation, Fill);
        }
    }

    /// <summary>
    /// 列车运行图文档。横轴为时分（四级刻度 1/5/10/60 分），
    /// 纵轴为站名线（间距按区间运行时分加权），运行线在区间内是解析斜线。
    /// </summary>
    public sealed class TrainGraphDocument : ChartDocument
    {
        public TrainGraphDocument()
        {
            FormTitle = "列车运行图";
            Stations = new List<TrainStation>();
            Trains = new List<TrainRun>();
            Windows = new List<TrainWindow>();
        }

        /// <summary>线路名（副标题）。</summary>
        public string? LineName { get; set; }

        /// <summary>横轴起点的钟点（分钟，0 表示 00:00）。</summary>
        public double StartClockMinutes { get; set; } = 360d;

        /// <summary>横轴跨度（分钟）。</summary>
        public double Minutes { get; set; } = 240d;

        public IList<TrainStation> Stations { get; }

        public IList<TrainRun> Trains { get; }

        public IList<TrainWindow> Windows { get; }

        /// <summary>全部区间运行时分之和（站名线权重的分母）。</summary>
        public double TotalRunMinutes
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < Stations.Count; i++)
                {
                    total += Stations[i].RunMinutesToNext;
                }
                return total;
            }
        }
    }
}
