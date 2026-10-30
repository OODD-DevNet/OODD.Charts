using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Transit.OversizeLoad
{
    /// <summary>
    /// 超限许可附图版面：侧面外廓 + 轴距链（<see cref="DimensionChain"/>）+ 总高线性尺寸
    /// （<see cref="DimensionRun"/>）+ 关键高度坐标式（<see cref="DimensionOrdinateSet"/>）+ 轴荷带。
    /// <para>
    /// 所有量值都由像素经 <c>SetMeasuredFromPixels</c> / <c>ApplyScale</c> 回写，
    /// 版面与数据不可能各说一套（docs/06 §4.14 断言①）。
    /// </para>
    /// </summary>
    public sealed class OversizeLoadLayout
    {
        private readonly SKRect _drawingRect;

        private OversizeLoadLayout(OversizeLoadDocument document, OversizeLoadStyle tokens, SKRect bounds,
                                   SKRect titleRect, SKRect noteRect, SKRect drawingRect, SKRect footerRect,
                                   float originX, float groundY, float topY, float dimensionX,
                                   DimensionChain chain, DimensionRun heightRun, DimensionOrdinateSet ordinates,
                                   IReadOnlyList<float> ordinateHeights)
        {
            Document = document;
            Tokens = tokens;
            Bounds = bounds;
            TitleRect = titleRect;
            NoteRect = noteRect;
            _drawingRect = drawingRect;
            FooterRect = footerRect;
            OriginX = originX;
            GroundY = groundY;
            TopY = topY;
            DimensionX = dimensionX;
            Chain = chain;
            HeightRun = heightRun;
            HeightOrdinates = ordinates;
            _ordinateHeights = ordinateHeights;
        }

        public OversizeLoadDocument Document { get; }
        public OversizeLoadStyle Tokens { get; }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect NoteRect { get; }
        public SKRect DrawingRect => _drawingRect;
        public SKRect FooterRect { get; }

        /// <summary>轮廓左端（x=0 m）的像素 x。</summary>
        public float OriginX { get; }

        /// <summary>地面线 y。</summary>
        public float GroundY { get; }

        /// <summary>总高顶面 y。</summary>
        public float TopY { get; }

        /// <summary>总高尺寸线的 x。</summary>
        public float DimensionX { get; }

        public float PixelsPerMeter => Tokens.ScalePixelsPerMeter;

        /// <summary>轴距链（各段已回写米值，延伸线锚在各自的地面点上）。</summary>
        public DimensionChain Chain { get; }

        /// <summary>总高线性尺寸（已回写米值）。</summary>
        public DimensionRun HeightRun { get; }

        /// <summary>关键高度坐标式（已回写米值）。</summary>
        public DimensionOrdinateSet HeightOrdinates { get; }

        private readonly IReadOnlyList<float> _ordinateHeights;

        public static OversizeLoadLayout Compute(OversizeLoadDocument document, OversizeLoadStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (!(document.LengthMeters > 0d))
            {
                throw new InvalidOperationException("车货总长必须为正，实到 " + document.LengthMeters);
            }
            if (!(document.HeightMeters > 0d))
            {
                throw new InvalidOperationException("车货总高必须为正，实到 " + document.HeightMeters);
            }
            if (document.Profile.Count < 2)
            {
                throw new InvalidOperationException("外廓至少要有两个折点，实到 " + document.Profile.Count);
            }
            if (!(tokenScale(tokens) > 0f))
            {
                throw new InvalidOperationException("出图比例必须为正，实到 " + tokens.ScalePixelsPerMeter);
            }

            const double tolerance = 1e-6;
            for (int i = 0; i < document.Profile.Count; i++)
            {
                OversizeProfilePoint point = document.Profile[i];
                if (point.XMeters < -tolerance || point.XMeters > document.LengthMeters + tolerance
                    || point.YMeters < -tolerance || point.YMeters > document.HeightMeters + tolerance)
                {
                    throw new InvalidOperationException(
                        "第 " + (i + 1) + " 个折点 " + point.XMeters.ToString("0.###", CultureInfo.InvariantCulture)
                        + "," + point.YMeters.ToString("0.###", CultureInfo.InvariantCulture)
                        + " 超出车货外廓 " + document.LengthMeters.ToString("0.###", CultureInfo.InvariantCulture)
                        + "×" + document.HeightMeters.ToString("0.###", CultureInfo.InvariantCulture) + " m");
                }
            }

            TransitStyle frame = tokens.Frame;
            float left = frame.Margin + frame.BindingMargin;
            float right = frame.Width - frame.Margin;
            SKRect bounds = new SKRect(0f, 0f, frame.Width, frame.Height);
            SKRect titleRect = new SKRect(left, frame.Margin, right, frame.Margin + frame.TitleHeight);
            SKRect noteRect = new SKRect(left, titleRect.Bottom, right, titleRect.Bottom + frame.SubtitleSize * 2.4f);
            SKRect footerRect = new SKRect(left, frame.Height - frame.Margin - frame.FooterHeight,
                right, frame.Height - frame.Margin);

            float groundY = footerRect.Top - tokens.GroundBandHeight;
            float originX = left + tokens.OrdinateLeftWidth;
            float dimensionX = originX + (float)(document.LengthMeters * tokens.ScalePixelsPerMeter)
                               + tokens.HeightDimensionGap;
            if (dimensionX > right - 12f)
            {
                throw new InvalidOperationException(
                    "出图比例过大：总高尺寸线 x " + dimensionX.ToString("0.#", CultureInfo.InvariantCulture)
                    + " 超出右边界 " + (right - 12f).ToString("0.#", CultureInfo.InvariantCulture)
                    + "（可调小 ScalePixelsPerMeter）");
            }

            SKRect drawingRect = new SKRect(originX - 20f, noteRect.Bottom + 8f, right, groundY);
            float topY = groundY - (float)(document.HeightMeters * tokens.ScalePixelsPerMeter);
            if (topY < drawingRect.Top)
            {
                throw new InvalidOperationException(
                    "车货总高 " + document.HeightMeters.ToString("0.###", CultureInfo.InvariantCulture)
                    + " m 超出绘图区高度（可调小 ScalePixelsPerMeter）");
            }

            // 轴距链：端点在地面以下 ChainOffset；分段占比由轴位与总长给出。
            IReadOnlyList<double> fractions = document.ChainFractions();
            SKPoint chainOrigin = new SKPoint(originX, groundY + tokens.ChainOffset);
            SKPoint chainEnd = new SKPoint(originX + (float)(document.LengthMeters * tokens.ScalePixelsPerMeter),
                groundY + tokens.ChainOffset);
            DimensionChain chain = new DimensionChain(chainOrigin, chainEnd, fractions);
            chain.ApplyScale(tokens.ScalePixelsPerMeter, "m");
            for (int i = 0; i < chain.Runs.Count; i++)
            {
                DimensionRun run = chain.Runs[i];
                run.ExtensionGap = 2f;
                run.MeasuredFormat = "0.00";
                run.ProfileFrom = new SKPoint(run.From.X, groundY);
                run.ProfileTo = new SKPoint(run.To.X, groundY);
            }

            // 总高：轮廓右端竖直尺寸，延伸线从右侧地面角点与顶角点引出（留 gap，不碰轮廓）。
            float profileRight = originX + (float)(document.LengthMeters * tokens.ScalePixelsPerMeter);
            DimensionRun heightRun = new DimensionRun(
                new SKPoint(dimensionX, groundY), new SKPoint(dimensionX, topY),
                DimensionOrientation.Linear, ArrowKind.Filled, ArrowKind.Filled);
            heightRun.MeasuredFormat = "0.00";
            heightRun.UnitSuffix = "m";
            heightRun.ProfileFrom = new SKPoint(profileRight, groundY);
            heightRun.ProfileTo = new SKPoint(profileRight, topY);
            heightRun.SetMeasuredFromPixels(tokens.ScalePixelsPerMeter);

            // 关键高度坐标式：基准线在轮廓左侧，逐条向左阶梯错开（不侵占轮廓）。
            float baselineX = originX - tokens.OrdinateBaselineOffset;
            List<double> heights = new List<double>();
            for (int i = 0; i < document.KeyHeightsMeters.Count; i++)
            {
                double h = document.KeyHeightsMeters[i];
                if (h <= 0d || h > document.HeightMeters + tolerance)
                {
                    throw new InvalidOperationException(
                        "关键高度 " + h.ToString("0.###", CultureInfo.InvariantCulture)
                        + " 必须在 (0," + document.HeightMeters.ToString("0.###", CultureInfo.InvariantCulture) + "] 内。");
                }
                heights.Add(h * tokens.ScalePixelsPerMeter);
            }
            if (heights.Count == 0)
            {
                heights.Add(document.HeightMeters * tokens.ScalePixelsPerMeter);
            }
            List<float> ordinateHeights = new List<float>(heights.Count);
            for (int i = 0; i < heights.Count; i++)
            {
                ordinateHeights.Add((float)heights[i]);
            }

            DimensionOrdinateSet ordinates = new DimensionOrdinateSet(
                new SKPoint(baselineX, groundY), new SKPoint(baselineX, topY), heights, tokens.OrdinateStackPitch);
            ordinates.ApplyScale(tokens.ScalePixelsPerMeter, "m");
            for (int i = 0; i < ordinates.Runs.Count; i++)
            {
                ordinates.Runs[i].MeasuredFormat = "0.00";
            }

            return new OversizeLoadLayout(document, tokens, bounds, titleRect, noteRect, drawingRect, footerRect,
                originX, groundY, topY, dimensionX, chain, heightRun, ordinates, ordinateHeights);
        }

        private static float tokenScale(OversizeLoadStyle tokens)
        {
            return tokens.ScalePixelsPerMeter;
        }

        /// <summary>数据坐标（米）→ 像素。</summary>
        public SKPoint PointOf(double xMeters, double yMeters)
        {
            return new SKPoint(
                OriginX + (float)(xMeters * Tokens.ScalePixelsPerMeter),
                GroundY - (float)(yMeters * Tokens.ScalePixelsPerMeter));
        }

        /// <summary>轴荷带的矩形（高度按该轴荷占最大轴荷的比例）。</summary>
        public SKRect AxleBarRect(OversizeAxle axle)
        {
            if (axle == null)
            {
                throw new ArgumentNullException(nameof(axle));
            }
            double max = 0d;
            for (int i = 0; i < Document.Axles.Count; i++)
            {
                if (Document.Axles[i].LoadTonnes > max)
                {
                    max = Document.Axles[i].LoadTonnes;
                }
            }
            float ratio = max > 0d ? (float)(axle.LoadTonnes / max) : 0f;
            float height = ratio * Tokens.AxleBarMaxHeight;
            float center = PointOf(axle.PositionMeters, 0d).X;
            float top = GroundY + Tokens.AxleBarTopOffset;
            return new SKRect(center - Tokens.AxleBarWidth * 0.5f, top,
                center + Tokens.AxleBarWidth * 0.5f, top + height);
        }

        /// <summary>轮圆心像素（半径取自令牌）。</summary>
        public SKPoint WheelCenterOf(OversizeAxle axle)
        {
            return PointOf(axle.PositionMeters, Tokens.WheelRadiusMeters);
        }

        public float WheelRadiusPixels => Tokens.WheelRadiusMeters * Tokens.ScalePixelsPerMeter;

        /// <summary>坐标式各条的像素高度（自地面起算），供断言。</summary>
        public IReadOnlyList<float> OrdinateHeightsPixels => _ordinateHeights;
    }
}
