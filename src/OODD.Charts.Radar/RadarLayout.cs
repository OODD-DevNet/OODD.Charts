using System;
using SkiaSharp;

namespace OODD.Charts.Radar
{
    /// <summary>
    /// 一次雷达版面求解的完整结果：盘心、半径、盘面矩形、面板矩形、罗盘圆心。
    /// 极坐标与像素坐标的换算、命中测试都在这里，模板只负责落笔。
    /// <para>
    /// 方位角约定：0° = 正北（屏幕上方），顺时针增大；距离 0 = 盘心，MaxRange = 最外圈。
    /// </para>
    /// </summary>
    public sealed class RadarLayout
    {
        private RadarLayout(
            SKRect bounds, SKRect titleRect, SKRect scopeRect, SKRect panelRect, bool hasPanel,
            SKPoint center, float radius, SKPoint compassCenter, float compassRadius,
            RadarScope scope, double startBearing, double maxRange, int ringCount)
        {
            Bounds = bounds;
            TitleRect = titleRect;
            ScopeRect = scopeRect;
            PanelRect = panelRect;
            HasPanel = hasPanel;
            Center = center;
            Radius = radius;
            CompassCenter = compassCenter;
            CompassRadius = compassRadius;
            Scope = scope;
            StartBearing = startBearing;
            MaxRange = maxRange;
            RingCount = ringCount;
        }

        public SKRect Bounds { get; }

        public SKRect TitleRect { get; }

        /// <summary>盘面外接矩形：整圆为正方形；半圆为宽度 2R、高度 R。</summary>
        public SKRect ScopeRect { get; }

        public SKRect PanelRect { get; }

        public bool HasPanel { get; }

        public SKPoint Center { get; }

        public float Radius { get; }

        public SKPoint CompassCenter { get; }

        public float CompassRadius { get; }

        public RadarScope Scope { get; }

        /// <summary>扇区起始方位（整圆恒为 0）。</summary>
        public double StartBearing { get; }

        public double MaxRange { get; }

        public int RingCount { get; }

        /// <summary>覆盖角度：360° 或 180°。</summary>
        public double CoveredDegrees => Scope == RadarScope.Half ? 180d : 360d;

        /// <summary>第 ring 个距标环代表的距离（ring 从 1 到 RingCount）。</summary>
        public double RingDistance(int ring)
        {
            int count = Math.Max(1, RingCount);
            return MaxRange * Math.Max(0, Math.Min(ring, count)) / count;
        }

        /// <summary>距离 → 像素半径；超量程按最外圈截断。</summary>
        public float RadiusOf(double distance)
        {
            if (MaxRange <= 0d)
            {
                return 0f;
            }
            double t = Math.Max(0d, Math.Min(distance, MaxRange)) / MaxRange;
            return (float)(t * Radius);
        }

        /// <summary>极坐标 → 屏幕坐标。</summary>
        public SKPoint PointOf(double bearing, double distance)
        {
            double radians = bearing * Math.PI / 180d;
            float r = RadiusOf(distance);
            return new SKPoint(
                Center.X + (float)(Math.Sin(radians) * r),
                Center.Y - (float)(Math.Cos(radians) * r));
        }

        /// <summary>屏幕坐标 → 方位角（归一到 [0, 360)）。</summary>
        public double BearingOf(SKPoint point)
        {
            double dx = point.X - Center.X;
            double dy = point.Y - Center.Y;
            double degrees = Math.Atan2(dx, -dy) * 180d / Math.PI;
            double normalized = degrees % 360d;
            return normalized < 0d ? normalized + 360d : normalized;
        }

        /// <summary>屏幕坐标 → 距离；盘心返回 0。</summary>
        public double DistanceOf(SKPoint point)
        {
            double dx = point.X - Center.X;
            double dy = point.Y - Center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (Radius <= 0f)
            {
                return 0d;
            }
            return dist / Radius * MaxRange;
        }

        /// <summary>点是否落在盘面覆盖区内（半圆雷达只看扇区）。</summary>
        public bool Contains(SKPoint point)
        {
            double dx = point.X - Center.X;
            double dy = point.Y - Center.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist > Radius + 1d)
            {
                return false;
            }

            if (Scope == RadarScope.Full)
            {
                return true;
            }

            double delta = BearingOf(point) - StartBearing;
            delta %= 360d;
            if (delta < 0d)
            {
                delta += 360d;
            }
            return delta <= 180d + 1e-6;
        }

        /// <summary>盘面轮廓：整圆，或从起始方位扫 180° 的半圆。</summary>
        public SKPath BuildScopePath()
        {
            SKPath path = new SKPath();
            path.FillType = SKPathFillType.Winding;
            if (Scope == RadarScope.Full)
            {
                path.AddCircle(Center.X, Center.Y, Radius);
                return path;
            }

            SKRect oval = new SKRect(Center.X - Radius, Center.Y - Radius,
                                      Center.X + Radius, Center.Y + Radius);
            path.MoveTo(Center);
            path.ArcTo(oval, (float)(StartBearing - 90d), 180f, false);
            path.Close();
            return path;
        }

        /// <summary>在容差内离该点最近的目标；没有返回 null。</summary>
        public RadarTarget? HitTest(RadarDocument document, SKPoint point, float tolerance)
        {
            if (document == null)
            {
                return null;
            }

            RadarTarget? best = null;
            float bestDistance = tolerance;
            foreach (RadarTarget target in document.Targets)
            {
                SKPoint at = PointOf(target.Bearing, target.Distance);

                // 半圆雷达不画扇区外的目标，命中测试也必须一致，否则会选中"看不见的点"。
                if (!Contains(at))
                {
                    continue;
                }

                float dx = at.X - point.X;
                float dy = at.Y - point.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                if (dist <= bestDistance)
                {
                    best = target;
                    bestDistance = dist;
                }
            }
            return best;
        }

        /// <summary>纯几何求解：同文档同令牌必然得到同一版面（Measure 幂等的前提）。</summary>
        public static RadarLayout Compute(RadarDocument document, RadarStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            float width = Math.Max(420f, tokens.Width);
            float height = Math.Max(360f, tokens.Height);
            SKRect bounds = new SKRect(0f, 0f, width, height);

            float margin = tokens.Margin;
            SKPoint compass = new SKPoint(
                width - margin - tokens.CompassRadius - 2f,
                margin + tokens.TitleHeight * 0.46f);
            SKRect title = new SKRect(
                margin,
                margin,
                width - margin - tokens.CompassRadius * 2f - 12f,
                margin + tokens.TitleHeight);

            SKRect content = new SKRect(margin, title.Bottom + 10f, width - margin, height - margin);
            RadarInfoPanel? panelConfig = document.InfoPanel;
            bool hasPanel = panelConfig != null && panelConfig.Show;
            SKRect panel = SKRect.Empty;
            SKRect discArea = content;

            if (hasPanel)
            {
                if (panelConfig!.Position == RadarPanelPosition.Right)
                {
                    float panelWidth = Math.Min(tokens.PanelWidth, content.Width * 0.42f);
                    panel = new SKRect(content.Right - panelWidth, content.Top, content.Right, content.Bottom);
                    discArea = new SKRect(content.Left, content.Top, panel.Left - tokens.PanelGap, content.Bottom);
                }
                else
                {
                    int count = Math.Max(1, document.Targets.Count);
                    // 多留一行给“另有 N 个目标未显示”。
                    float desired = tokens.PanelPadding * 2f + tokens.PanelTitleSize + 10f
                                    + (count + 1) * tokens.PanelRowHeight;
                    float panelHeight = Math.Min(desired, content.Height * 0.42f);
                    panel = new SKRect(content.Left, content.Bottom - panelHeight, content.Right, content.Bottom);
                    discArea = new SKRect(content.Left, content.Top, content.Right, panel.Top - tokens.PanelGap);
                }
            }

            float pad = tokens.LabelRadiusPad;
            float radius;
            SKPoint center;
            if (document.Scope == RadarScope.Half)
            {
                radius = Math.Max(40f, Math.Min(discArea.Width / 2f - pad, discArea.Height - pad));
                center = new SKPoint(discArea.MidX, discArea.Bottom - pad * 0.35f);
            }
            else
            {
                radius = Math.Max(40f, Math.Min(discArea.Width, discArea.Height) / 2f - pad);
                center = new SKPoint(discArea.MidX, discArea.MidY);
            }

            SKRect scopeRect = document.Scope == RadarScope.Half
                ? new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y)
                : new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius);

            double startBearing = document.Scope == RadarScope.Half
                ? RadarInfoPanel.NormalizeBearing(document.HalfStartBearing)
                : 0d;

            return new RadarLayout(
                bounds, title, scopeRect, panel, hasPanel,
                center, radius, compass, tokens.CompassRadius,
                document.Scope, startBearing, Math.Max(1e-6, document.MaxRange),
                Math.Max(1, document.RingCount));
        }
    }
}
