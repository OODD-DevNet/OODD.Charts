using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Transit.TrainGraph
{
    /// <summary>
    /// 运行图版面。一次求解、绘制与断言共用同一份实例（D15 挂点），
    /// 因此"站界 y"只有一个来源：<see cref="StationAxis"/>。
    /// </summary>
    public sealed class TrainGraphLayout
    {
        public const string TimeAxisKey = "time";

        private TrainGraphLayout(TrainGraphDocument document, TrainGraphStyle tokens,
                                 SKRect bounds, SKRect titleRect, SKRect noteRect,
                                 SKRect plotRect, SKRect timeAxisRect, SKRect footerRect,
                                 NumericAxis timeAxis, WeightedCategoryAxis stationAxis,
                                 IReadOnlyList<AxisMark> marks)
        {
            Document = document;
            Tokens = tokens;
            Bounds = bounds;
            TitleRect = titleRect;
            NoteRect = noteRect;
            PlotRect = plotRect;
            TimeAxisRect = timeAxisRect;
            FooterRect = footerRect;
            TimeAxis = timeAxis;
            StationAxis = stationAxis;
            Marks = marks;
        }

        public TrainGraphDocument Document { get; }
        public TrainGraphStyle Tokens { get; }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect NoteRect { get; }
        public SKRect PlotRect { get; }
        public SKRect TimeAxisRect { get; }
        public SKRect FooterRect { get; }

        /// <summary>时分轴（线性，横向）。</summary>
        public NumericAxis TimeAxis { get; }

        /// <summary>站名线轴（权重按区间运行时分，纵向）。</summary>
        public WeightedCategoryAxis StationAxis { get; }

        /// <summary>四级刻度点（1/5/10/60 分，已按层级合并）。</summary>
        public IReadOnlyList<AxisMark> Marks { get; }

        /// <summary>求解版面。纯函数：不读不写任何模板状态。</summary>
        public static TrainGraphLayout Compute(TrainGraphDocument document, TrainGraphStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (document.Stations.Count < 2)
            {
                throw new InvalidOperationException("运行图至少需要 2 个车站，实到 " + document.Stations.Count + "。");
            }
            if (!(document.Minutes > 0d))
            {
                throw new InvalidOperationException("横轴跨度必须为正，实到 " + document.Minutes + "。");
            }
            if (!(document.TotalRunMinutes > 0d))
            {
                throw new InvalidOperationException("各区间运行时分之和必须为正，否则站名线无法定距。");
            }

            TransitStyle frame = tokens.Frame;
            float width = frame.Width;
            float height = frame.Height;
            float left = frame.Margin + frame.BindingMargin;
            float right = width - frame.Margin;

            SKRect bounds = new SKRect(0f, 0f, width, height);
            SKRect titleRect = new SKRect(left, frame.Margin, right, frame.Margin + frame.TitleHeight);
            SKRect noteRect = new SKRect(left, titleRect.Bottom, right, titleRect.Bottom + tokens.NoteBandHeight);

            float plotLeft = left + tokens.StationLabelWidth;
            float plotTop = noteRect.Bottom;
            float plotBottom = height - frame.Margin - frame.FooterHeight - tokens.TimeAxisHeight;
            SKRect plotRect = new SKRect(plotLeft, plotTop, right, plotBottom);
            if (!(plotRect.Height > 0f) || !(plotRect.Width > 0f))
            {
                throw new InvalidOperationException(
                    "运行图绘图区非正：" + plotRect.Width.ToString("0.#", CultureInfo.InvariantCulture) + "×"
                    + plotRect.Height.ToString("0.#", CultureInfo.InvariantCulture));
            }

            SKRect timeAxisRect = new SKRect(plotLeft, plotBottom, right, plotBottom + tokens.TimeAxisHeight);
            SKRect footerRect = new SKRect(left, timeAxisRect.Bottom, right, height - frame.Margin);

            NumericAxis timeAxis = new NumericAxis(TimeAxisKey, 0d, document.Minutes,
                plotRect.Left, plotRect.Right, AxisDirection.HorizontalRight);

            List<CategoryStop> stops = new List<CategoryStop>();
            for (int i = 0; i < document.Stations.Count; i++)
            {
                TrainStation station = document.Stations[i];
                stops.Add(new CategoryStop(station.Key, station.Name, station.RunMinutesToNext));
            }

            WeightedCategoryAxis stationAxis = new WeightedCategoryAxis(
                plotRect.Top, plotRect.Bottom, stops, AxisDirection.VerticalDown,
                plotRect.Left, plotRect.Right);

            List<AxisTier> tiers = new List<AxisTier>
            {
                new AxisTier(tokens.HairlineMinutes, RulerTier.Hairline, false),
                new AxisTier(tokens.FineMinutes, RulerTier.Fine, false),
                new AxisTier(tokens.MediumMinutes, RulerTier.Medium, false),
                new AxisTier(tokens.HeavyMinutes, RulerTier.Heavy, true),
            };
            List<AxisMark> marks = AxisRuler.Marks(timeAxis, tiers);

            return new TrainGraphLayout(document, tokens, bounds, titleRect, noteRect, plotRect,
                timeAxisRect, footerRect, timeAxis, stationAxis, marks);
        }

        /// <summary>第 <paramref name="stationIndex"/> 条站名线的像素 y。</summary>
        public float StationLineY(int stationIndex)
        {
            return StationAxis.PositionOf(stationIndex);
        }

        /// <summary>天窗带的像素矩形：时刻区间与站区间同时裁剪。</summary>
        public SKRect WindowRect(TrainWindow window)
        {
            if (window == null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            float x0 = TimeAxis.PositionOf(window.FromMinutes);
            float x1 = TimeAxis.PositionOf(window.ToMinutes);
            int last = StationAxis.Count - 1;
            int fromCategory = Math.Min(Math.Max(window.FromStation, 0), last);
            int toCategory = Math.Min(Math.Max(window.ToStation, fromCategory), last);
            float y0 = StationAxis.PositionOf(fromCategory);
            float y1 = StationAxis.PositionOf(toCategory);
            return new SKRect(Math.Min(x0, x1), Math.Min(y0, y1), Math.Max(x0, x1), Math.Max(y0, y1));
        }

        /// <summary>
        /// 一趟列车的运行线顶点序列：站界点上恰好取 <see cref="StationAxis"/> 的 y，
        /// 区间内是相邻两点之间的解析斜线（不采样）。绘制与断言共用此方法。
        /// </summary>
        public IReadOnlyList<SKPoint> PointsOf(TrainRun train)
        {
            if (train == null)
            {
                throw new ArgumentNullException(nameof(train));
            }

            List<SKPoint> points = new List<SKPoint>();
            List<TrainStop> stops = new List<TrainStop>(train.Stops);
            stops.Sort((a, b) => a.StationIndex.CompareTo(b.StationIndex));

            for (int i = 0; i < stops.Count; i++)
            {
                TrainStop stop = stops[i];
                float y = StationAxis.PositionOf(stop.StationIndex);
                points.Add(new SKPoint(TimeAxis.PositionOf(stop.ArriveMinutes), y));
                if (stop.DepartMinutes > stop.ArriveMinutes)
                {
                    points.Add(new SKPoint(TimeAxis.PositionOf(stop.DepartMinutes), y));
                }
            }

            return points;
        }

        /// <summary>时刻标签（钟点，走 <see cref="TransitStyle.TimeFormat"/>）。</summary>
        public string ClockText(double minutes)
        {
            return new DateTime(2000, 1, 1).AddMinutes(Document.StartClockMinutes + minutes)
                .ToString(Tokens.Frame.TimeFormat, CultureInfo.InvariantCulture);
        }
    }
}
