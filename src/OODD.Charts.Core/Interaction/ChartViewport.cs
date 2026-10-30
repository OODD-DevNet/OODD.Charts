using System;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Interaction
{
    /// <summary>
    /// 视口：把宿主指针坐标（DIP、物理像素或逻辑点，取决于适配器）映射到表单的 96dpi 逻辑坐标。
    /// <para>
    /// 适配器每次绘制时按实际落位（缩放 + 偏移）构造一个实例并交给
    /// <see cref="ChartInteractionHost"/>，指针事件因此能精确落到目标上，而不必关心宿主单位。
    /// </para>
    /// </summary>
    public readonly struct ChartViewport
    {
        private ChartViewport(SKSize chartSize, float scale, float offsetX, float offsetY)
        {
            ChartSize = chartSize;
            Scale = scale;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }

        /// <summary>未绘制过（或没有表单）时的空视口；<see cref="IsEmpty"/> 为 true。</summary>
        public static ChartViewport Empty => default;

        /// <summary>表单 96dpi 逻辑尺寸。</summary>
        public SKSize ChartSize { get; }

        /// <summary>逻辑单位 → 视图单位 的缩放比。</summary>
        public float Scale { get; }

        /// <summary>表单左上角在视图中的横向偏移（视图单位）。</summary>
        public float OffsetX { get; }

        /// <summary>表单左上角在视图中的纵向偏移（视图单位）。</summary>
        public float OffsetY { get; }

        public bool IsEmpty => ChartSize.Width <= 0f || ChartSize.Height <= 0f || Scale <= 0f;

        /// <summary>用适配器算出的落位直接构造：<paramref name="scale"/> 是逻辑→视图比，偏移是视图单位。</summary>
        public static ChartViewport FromPlacement(SKSize chartSize, float scale, float offsetX, float offsetY)
        {
            if (chartSize.Width <= 0f || chartSize.Height <= 0f || scale <= 0f
                || float.IsNaN(scale) || float.IsInfinity(scale)
                || float.IsNaN(offsetX) || float.IsNaN(offsetY)
                || float.IsInfinity(offsetX) || float.IsInfinity(offsetY))
            {
                return Empty;
            }

            return new ChartViewport(chartSize, scale, offsetX, offsetY);
        }

        /// <summary>
        /// 按适配方式推导落位。Fit 等比贴合视图并居中；Actual 从原点起按
        /// <paramref name="dpiBase"/>/96 × <paramref name="scaleFactor"/> 绘制（超出裁切，不居中）。
        /// </summary>
        public static ChartViewport Create(SKSize chartSize, float viewWidth, float viewHeight,
            ChartFit fit, double scaleFactor = 1d, double dpiBase = 96d)
        {
            if (chartSize.Width <= 0f || chartSize.Height <= 0f || viewWidth <= 0f || viewHeight <= 0f
                || float.IsNaN(viewWidth) || float.IsNaN(viewHeight)
                || float.IsInfinity(viewWidth) || float.IsInfinity(viewHeight))
            {
                return Empty;
            }

            double factor = scaleFactor > 0d ? scaleFactor : 1d;
            double scale = fit == ChartFit.Fit
                ? Math.Min(viewWidth / chartSize.Width, viewHeight / chartSize.Height) * factor
                : (dpiBase > 0d ? dpiBase : 96d) / 96d * factor;
            if (scale <= 0d)
            {
                return Empty;
            }

            float offsetX = 0f;
            float offsetY = 0f;
            if (fit == ChartFit.Fit)
            {
                offsetX = (float)((viewWidth - chartSize.Width * scale) / 2d);
                offsetY = (float)((viewHeight - chartSize.Height * scale) / 2d);
            }

            return new ChartViewport(chartSize, (float)scale, offsetX, offsetY);
        }

        /// <summary>视图坐标 → 表单逻辑坐标。</summary>
        public SKPoint ToChart(SKPoint view)
        {
            if (IsEmpty)
            {
                return view;
            }

            return new SKPoint((view.X - OffsetX) / Scale, (view.Y - OffsetY) / Scale);
        }

        /// <summary>表单逻辑坐标 → 视图坐标。</summary>
        public SKPoint ToView(SKPoint chart)
        {
            if (IsEmpty)
            {
                return chart;
            }

            return new SKPoint(chart.X * Scale + OffsetX, chart.Y * Scale + OffsetY);
        }

        /// <summary>点是否落在表单版面内（逻辑坐标）。</summary>
        public bool ContainsChart(SKPoint chartPoint)
        {
            return !IsEmpty
                   && chartPoint.X >= 0f && chartPoint.Y >= 0f
                   && chartPoint.X <= ChartSize.Width && chartPoint.Y <= ChartSize.Height;
        }
    }
}
