using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Radar
{
    /// <summary>
    /// 雷达态势图（PPI）。一张图 = 盘面 + 标题 + 右上角南北向罗盘 + 可选信息面板。
    /// <para>
    /// 盘面支持 360° 整圆与 180° 扇区；量程、距标环数、扫描线与角度、光束宽度、
    /// 目标、选中项、轨迹保留点数、面板字段、全部配色都由文档与令牌控制。
    /// 模板只做“按几何落笔”，不保存任何版面状态，因此 <c>Measure</c> 幂等。
    /// </para>
    /// </summary>
    public sealed class RadarChartTemplate : IChartTemplate
    {
        public string Key => "radar.ppi";

        public string DisplayName => "雷达态势图";

        /// <summary>尺寸令牌。</summary>
        public RadarStyle Tokens { get; set; } = new RadarStyle();

        /// <summary>配色令牌；换肤只改这里。</summary>
        public RadarPalette Palette { get; set; } = new RadarPalette();

        /// <summary>按当前令牌求版面（纯函数）。</summary>
        public RadarLayout Layout(RadarDocument document) => RadarLayout.Compute(document, Tokens);

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            RadarDocument doc = Cast(document);
            RadarLayout layout = RadarLayout.Compute(doc, Tokens);
            return ChartGeometry.ForLayout(layout.Bounds, layout.ScopeRect, layout);
        }

        private static RadarDocument Cast(ChartDocument document)
        {
            RadarDocument? typed = document as RadarDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(RadarDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            RadarDocument doc = Cast(document);

            // 版面只有一个来源：Measure 求出的 RadarLayout。绘制不再重算，
            // 否则改令牌却没 Invalidate 时，落点会跑到 Measure 定下的 Bounds 之外。
            RadarLayout layout = geometry.LayoutOf<RadarLayout>() ?? RadarLayout.Compute(doc, Tokens);

            painter.FillRect(layout.Bounds, Palette.PageBackground);
            DrawTitle(painter, layout, doc);
            if (doc.ShowNorthIndicator)
            {
                DrawCompass(painter, layout);
            }
            DrawScope(painter, layout, doc);
            if (doc.ShowSweep)
            {
                DrawSweep(painter, layout, doc);
            }
            if (doc.ShowTrails)
            {
                DrawTrails(painter, layout, doc);
            }
            DrawTargets(painter, layout, doc);
            DrawPanel(painter, layout, doc);
        }

        private void DrawTitle(Painter p, RadarLayout layout, RadarDocument doc)
        {
            ChartStyle style = p.Style;
            SKRect rect = layout.TitleRect;

            float lineHeight = Tokens.TitleSize + 14f;
            SKRect titleBox = new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + lineHeight);
            string title = string.IsNullOrEmpty(doc.FormTitle) ? DisplayName : doc.FormTitle!;
            p.TextInBox(title, titleBox,
                new FontKey(style.Text.Family, Tokens.TitleSize, true),
                Palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            SKRect infoBox = new SKRect(rect.Left, titleBox.Bottom, rect.Right, rect.Bottom);
            p.TextInBox(BuildModeText(doc), infoBox,
                new FontKey(style.Text.Family, Tokens.SubtitleSize),
                Palette.SubtitleText, CellAlign.Near, CellAlign.Center, 2f);
        }

        private string BuildModeText(RadarDocument doc)
        {
            string scope;
            if (doc.Scope == RadarScope.Half)
            {
                double start = RadarInfoPanel.NormalizeBearing(doc.HalfStartBearing);
                double end = RadarInfoPanel.NormalizeBearing(start + 180d);
                scope = "180° 扇区 " + start.ToString("000", CultureInfo.InvariantCulture) + "°→"
                        + end.ToString("000", CultureInfo.InvariantCulture) + "°";
            }
            else
            {
                scope = "360° 全向";
            }

            return scope + "　量程 0–" + doc.MaxRange.ToString(Tokens.RangeFormat, CultureInfo.InvariantCulture)
                   + " " + doc.RangeUnit + "　北向上";
        }

        private void DrawCompass(Painter p, RadarLayout layout)
        {
            SKPoint c = layout.CompassCenter;
            float r = layout.CompassRadius;
            FontKey key = new FontKey(p.Style.Text.Family, Tokens.CompassLabelSize);

            p.Circle(c, r, Palette.ScopeBorder, Tokens.CompassStroke);

            // 南北向指针：上半段指北、下半段指南，两色区分。
            p.Line(c.X, c.Y, c.X, c.Y - r * 0.72f, Palette.North, 1.5f);
            p.Line(c.X, c.Y, c.X, c.Y + r * 0.72f, Palette.South, 1.2f);
            using (SKPath arrow = Arrow(c.X, c.Y - r * 0.94f, 4.2f, 6.2f))
            {
                FillPath(p, arrow, Palette.North);
            }

            SKRect northBox = new SKRect(c.X - r, c.Y - r * 0.88f, c.X - 4f, c.Y - r * 0.18f);
            SKRect southBox = new SKRect(c.X - r, c.Y + r * 0.18f, c.X - 4f, c.Y + r * 0.88f);
            p.TextInBox("北", northBox, key, Palette.North, CellAlign.Far, CellAlign.Center, 0f);
            p.TextInBox("南", southBox, key, Palette.South, CellAlign.Far, CellAlign.Center, 0f);
        }

        private void DrawScope(Painter p, RadarLayout layout, RadarDocument doc)
        {
            bool half = doc.Scope == RadarScope.Half;
            using (SKPath scope = layout.BuildScopePath())
            {
                FillPath(p, scope, Palette.ScopeFill);

                for (int ring = 1; ring <= layout.RingCount; ring++)
                {
                    float radius = layout.RadiusOf(layout.RingDistance(ring));
                    if (half)
                    {
                        using (SKPath arc = ArcPath(layout, radius, layout.StartBearing, 180d, false))
                        {
                            StrokePath(p, arc, Palette.Ring, Tokens.RingStroke);
                        }
                    }
                    else
                    {
                        p.Circle(layout.Center, radius, Palette.Ring, Tokens.RingStroke);
                    }
                }

                double step = Math.Max(1d, Tokens.BearingTickStep);
                if (half)
                {
                    int count = (int)Math.Floor(180d / step + 1e-9);
                    for (int k = 0; k <= count; k++)
                    {
                        DrawSpoke(p, layout, layout.StartBearing + k * step);
                    }
                    SKPoint left = layout.PointOf(layout.StartBearing, layout.MaxRange);
                    SKPoint right = layout.PointOf(layout.StartBearing + 180d, layout.MaxRange);
                    p.Line(left.X, left.Y, right.X, right.Y, Palette.Spoke, Tokens.SpokeStroke);
                }
                else
                {
                    int count = (int)Math.Round(360d / step);
                    for (int k = 0; k < count; k++)
                    {
                        DrawSpoke(p, layout, k * step);
                    }
                }

                StrokePath(p, scope, Palette.ScopeBorder, Tokens.ScopeBorderStroke);
            }

            DrawBearingLabels(p, layout, doc);
            DrawRangeLabels(p, layout, doc);
        }

        private void DrawSpoke(Painter p, RadarLayout layout, double bearing)
        {
            SKPoint edge = layout.PointOf(bearing, layout.MaxRange);
            p.Line(layout.Center.X, layout.Center.Y, edge.X, edge.Y, Palette.Spoke, Tokens.SpokeStroke);
        }

        private void DrawBearingLabels(Painter p, RadarLayout layout, RadarDocument doc)
        {
            FontKey key = new FontKey(p.Style.Text.Family, Tokens.BearingLabelSize);
            float labelRadius = layout.Radius + Tokens.LabelRadiusPad * 0.8f;
            double step = Math.Max(1d, Tokens.BearingTickStep);

            List<double> bearings = new List<double>();
            if (doc.Scope == RadarScope.Half)
            {
                int count = (int)Math.Floor(180d / step + 1e-9);
                for (int k = 0; k <= count; k++)
                {
                    bearings.Add(layout.StartBearing + k * step);
                }
            }
            else
            {
                int count = (int)Math.Round(360d / step);
                for (int k = 0; k < count; k++)
                {
                    bearings.Add(k * step);
                }
            }

            foreach (double bearing in bearings)
            {
                SKPoint at = PolarAt(layout, labelRadius, bearing);
                string text = RadarInfoPanel.NormalizeBearing(bearing)
                    .ToString(Tokens.BearingFormat, CultureInfo.InvariantCulture) + "°";
                SKRect box = new SKRect(at.X - 21f, at.Y - 8f, at.X + 21f, at.Y + 8f);
                ClampInto(box, layout.Bounds, 2f);
                p.TextInBox(text, box, key, Palette.BearingText, CellAlign.Center, CellAlign.Center, 0f);
            }
        }

        private void DrawRangeLabels(Painter p, RadarLayout layout, RadarDocument doc)
        {
            FontKey key = new FontKey(p.Style.Text.Family, Tokens.RangeLabelSize);
            double bearing = Tokens.RangeLabelBearing;
            if (doc.Scope == RadarScope.Half &&
                !WithinHalf(layout, RadarInfoPanel.NormalizeBearing(bearing)))
            {
                bearing = layout.StartBearing + 45d;
            }

            for (int ring = 1; ring <= layout.RingCount; ring++)
            {
                float radius = layout.RadiusOf(layout.RingDistance(ring));
                if (radius < 20f)
                {
                    continue;
                }
                SKPoint at = PolarAt(layout, radius, bearing);
                string text = layout.RingDistance(ring)
                    .ToString(Tokens.RangeFormat, CultureInfo.InvariantCulture);
                // 标注落在径线内侧，外圈数字不会跑到盘面之外。
                SKRect box = new SKRect(at.X - 52f, at.Y - 9f, at.X - 3f, at.Y + 9f);
                ClampInto(box, layout.Bounds, 2f);
                p.TextInBox(text, box, key, Palette.RangeText, CellAlign.Far, CellAlign.Center, 0f);
            }
        }

        private void DrawSweep(Painter p, RadarLayout layout, RadarDocument doc)
        {
            using (SKPath scope = layout.BuildScopePath())
            {
                int saved = p.Canvas.Save();
                p.Canvas.ClipPath(scope, SKClipOperation.Intersect, true);

                int segments = Math.Max(1, Tokens.SweepTailSegments);
                double beam = Math.Max(2d, Math.Min(doc.SweepBeamWidth, 179d));
                double leading = doc.SweepAngle;

                for (int i = 0; i < segments; i++)
                {
                    double from = leading - beam * (i + 1) / segments;
                    double to = leading - beam * i / segments;
                    double near = 1d - (double)i / segments;
                    int alpha = (int)Math.Round(4d + 122d * Math.Pow(near, 1.8d));
                    using (SKPath wedge = WedgePath(layout, from, to))
                    {
                        FillPath(p, wedge, WithAlpha(Palette.Sweep, alpha));
                    }
                }

                SKPoint tip = layout.PointOf(leading, layout.MaxRange);
                p.Line(layout.Center.X, layout.Center.Y, tip.X, tip.Y,
                    WithAlpha(Palette.Sweep, 70), Tokens.SweepStroke * 3.4f);
                p.Line(layout.Center.X, layout.Center.Y, tip.X, tip.Y,
                    Palette.Sweep, Tokens.SweepStroke);

                p.Canvas.RestoreToCount(saved);
            }
        }

        private void DrawTrails(Painter p, RadarLayout layout, RadarDocument doc)
        {
            foreach (RadarTarget target in doc.Targets)
            {
                if (target.Lost)
                {
                    continue;
                }

                IReadOnlyList<RadarTrackPoint> points = RetainedTrack(target, doc.TrailLength);
                if (points.Count < 2)
                {
                    continue;
                }

                int segments = points.Count - 1;
                for (int i = 0; i < segments; i++)
                {
                    SKPoint from = layout.PointOf(points[i].Bearing, points[i].Distance);
                    SKPoint to = layout.PointOf(points[i + 1].Bearing, points[i + 1].Distance);
                    if (!layout.Contains(from) || !layout.Contains(to))
                    {
                        continue;
                    }
                    int alpha = 36 + (int)Math.Round(150d * (i + 1) / segments);
                    p.Line(from.X, from.Y, to.X, to.Y,
                        WithAlpha(Palette.Trail, alpha), Tokens.TrailStroke);
                }
            }
        }

        private void DrawTargets(Painter p, RadarLayout layout, RadarDocument doc)
        {
            FontKey labelKey = new FontKey(p.Style.Text.Family, Tokens.TargetLabelSize);
            foreach (RadarTarget target in doc.Targets)
            {
                SKPoint at = layout.PointOf(target.Bearing, target.Distance);
                if (!layout.Contains(at))
                {
                    continue;
                }

                bool selected = string.Equals(target.Id, doc.SelectedTargetId, StringComparison.Ordinal);
                bool hovered = !selected && string.Equals(target.Id, doc.HoverTargetId, StringComparison.Ordinal);
                SKColor color = target.Color ?? (target.Lost ? Palette.LostTarget : Palette.Target);
                int glowAlpha = target.Lost ? 34 : Palette.TargetGlow.Alpha;
                p.FillCircle(at, Tokens.TargetGlowRadius, WithAlpha(color, glowAlpha));

                if (target.Lost)
                {
                    p.Circle(at, Tokens.TargetRadius + 1.4f, color, 1.1f);
                    p.Line(at.X - 2.4f, at.Y - 2.4f, at.X + 2.4f, at.Y + 2.4f, color, 1f);
                    p.Line(at.X + 2.4f, at.Y - 2.4f, at.X - 2.4f, at.Y + 2.4f, color, 1f);
                }
                else
                {
                    p.FillCircle(at, Tokens.TargetRadius, color);
                }

                if (selected)
                {
                    DrawSelection(p, at);
                }
                else if (hovered)
                {
                    // 悬停：只画一圈细环，不抢选中态的十字刻度。
                    p.Circle(at, Tokens.SelectedRingRadius * 0.8f, WithAlpha(Palette.Selected, 150), 1.1f);
                }

                if (doc.InfoPanel != null && doc.InfoPanel.ShowTargetLabels)
                {
                    p.Text(target.DisplayText,
                        at.X + Tokens.TargetGlowRadius + 2f,
                        at.Y - Tokens.TargetGlowRadius * 0.45f,
                        labelKey, selected || hovered ? Palette.Selected : Palette.Text);
                }
            }
        }

        private void DrawSelection(Painter p, SKPoint at)
        {
            float r = Tokens.SelectedRingRadius;
            float tick = 3.6f;
            p.Circle(at, r, Palette.Selected, 1.6f);
            p.Line(at.X - r - tick, at.Y, at.X - r + tick * 0.4f, at.Y, Palette.Selected, 1.4f);
            p.Line(at.X + r - tick * 0.4f, at.Y, at.X + r + tick, at.Y, Palette.Selected, 1.4f);
            p.Line(at.X, at.Y - r - tick, at.X, at.Y - r + tick * 0.4f, Palette.Selected, 1.4f);
            p.Line(at.X, at.Y + r - tick * 0.4f, at.X, at.Y + r + tick, Palette.Selected, 1.4f);
        }

        // ------------------------------------------------------------------ 信息面板

        private void DrawPanel(Painter p, RadarLayout layout, RadarDocument doc)
        {
            RadarInfoPanel? panel = doc.InfoPanel;
            if (!layout.HasPanel || panel == null)
            {
                return;
            }

            SKRect rect = layout.PanelRect;
            p.FillRect(rect, Palette.PanelFill);
            p.Frame(rect, Palette.PanelBorder, 1f);

            int saved = p.Canvas.Save();
            p.Canvas.ClipRect(rect);

            ChartStyle style = p.Style;
            FontKey titleKey = new FontKey(style.Text.Family, Tokens.PanelTitleSize, true);
            FontKey textKey = new FontKey(style.Text.Family, Tokens.PanelTextSize);
            float pad = Tokens.PanelPadding;
            float titleHeight = Tokens.PanelTitleSize + 10f;

            p.Text(panel.Title ?? string.Empty, rect.Left + pad, rect.Top + pad + Tokens.PanelTitleSize,
                titleKey, Palette.Text);
            string countText = doc.Targets.Count.ToString(CultureInfo.InvariantCulture) + " 个目标";
            p.Text(countText, rect.Right - pad - p.TextWidth(countText, textKey),
                rect.Top + pad + Tokens.PanelTitleSize, textKey, Palette.BearingText);

            float top = rect.Top + pad + titleHeight + 2f;
            if (doc.Targets.Count == 0)
            {
                p.TextInBox(panel.EmptyText,
                    new SKRect(rect.Left + pad, top, rect.Right - pad, rect.Bottom - pad),
                    textKey, Palette.BearingText, CellAlign.Center, CellAlign.Center, 0f);
                p.Canvas.RestoreToCount(saved);
                return;
            }

            int hidden = panel.Position == RadarPanelPosition.Bottom
                ? DrawPanelRows(p, panel, doc, textKey, rect, ref top)
                : DrawPanelColumn(p, panel, doc, textKey, rect, ref top);

            if (hidden > 0)
            {
                string note = "另有 " + hidden.ToString(CultureInfo.InvariantCulture) + " 个目标未显示";
                p.Text(note, rect.Left + pad, rect.Bottom - pad, textKey, Palette.BearingText);
            }

            p.Canvas.RestoreToCount(saved);
        }

        private int DrawPanelColumn(Painter p, RadarInfoPanel panel, RadarDocument doc,
            FontKey textKey, SKRect rect, ref float y)
        {
            FontKey headKey = new FontKey(p.Style.Text.Family, Tokens.PanelTextSize, true);
            float pad = Tokens.PanelPadding;
            float rowHeight = Tokens.PanelRowHeight;
            int hidden = 0;

            foreach (RadarTarget target in doc.Targets)
            {
                IReadOnlyList<KeyValuePair<string, string>> fields = panel.Resolve(target, doc);
                float blockHeight = rowHeight + fields.Count * rowHeight + Tokens.PanelBlockGap;
                if (y + blockHeight + rowHeight > rect.Bottom - pad)
                {
                    hidden++;
                    continue;
                }

                bool selected = string.Equals(target.Id, doc.SelectedTargetId, StringComparison.Ordinal);
                if (selected)
                {
                    SKRect highlight = new SKRect(rect.Left + pad * 0.5f, y - 2f,
                        rect.Right - pad * 0.5f, y + blockHeight - 2f);
                    p.FillRect(highlight, WithAlpha(Palette.Selected, 36));
                    p.Rect(highlight, WithAlpha(Palette.Selected, 120), 1f);
                }

                string head = target.DisplayText + (target.Lost ? "（丢失）" : string.Empty);
                p.Text(head, rect.Left + pad, y + Tokens.PanelTextSize, headKey,
                    selected ? Palette.Selected : Palette.Text);
                y += rowHeight;

                foreach (KeyValuePair<string, string> field in fields)
                {
                    p.Text(field.Key, rect.Left + pad + 2f, y + Tokens.PanelTextSize, textKey,
                        Palette.BearingText);
                    p.Text(field.Value, rect.Right - pad - p.TextWidth(field.Value, textKey),
                        y + Tokens.PanelTextSize, textKey, Palette.Text);
                    y += rowHeight;
                }
                y += Tokens.PanelBlockGap;
            }

            return hidden;
        }

        private int DrawPanelRows(Painter p, RadarInfoPanel panel, RadarDocument doc,
            FontKey textKey, SKRect rect, ref float y)
        {
            float pad = Tokens.PanelPadding;
            float rowHeight = Tokens.PanelRowHeight;
            int total = doc.Targets.Count;
            int fit = (int)Math.Floor((rect.Bottom - pad - y) / rowHeight);
            int drawCount = fit >= total ? total : Math.Max(0, fit - 1);
            int hidden = 0;

            for (int index = 0; index < total; index++)
            {
                if (index >= drawCount)
                {
                    hidden++;
                    continue;
                }

                RadarTarget target = doc.Targets[index];
                bool selected = string.Equals(target.Id, doc.SelectedTargetId, StringComparison.Ordinal);
                SKRect row = new SKRect(rect.Left + pad * 0.5f, y, rect.Right - pad * 0.5f, y + rowHeight);
                if (selected)
                {
                    p.FillRect(row, WithAlpha(Palette.Selected, 36));
                    p.Rect(row, WithAlpha(Palette.Selected, 120), 1f);
                }

                p.TextInBox(BuildRowText(panel, target, doc), row, textKey,
                    selected ? Palette.Selected : Palette.Text, CellAlign.Near, CellAlign.Center, pad * 0.5f);
                y += rowHeight;
            }

            return hidden;
        }

        private static string BuildRowText(RadarInfoPanel panel, RadarTarget target, RadarDocument doc)
        {
            List<string> parts = new List<string> { target.DisplayText };
            foreach (KeyValuePair<string, string> field in panel.Resolve(target, doc))
            {
                parts.Add(field.Key + " " + field.Value);
            }
            return string.Join("　", parts.ToArray());
        }

        // ------------------------------------------------------------------ 工具

        /// <summary>按保留数截取轨迹的末段；不足则全给。</summary>
        public static IReadOnlyList<RadarTrackPoint> RetainedTrack(RadarTarget target, int trailLength)
        {
            List<RadarTrackPoint> result = new List<RadarTrackPoint>();
            if (target == null || trailLength <= 0)
            {
                return result;
            }
            int take = Math.Min(trailLength, target.Track.Count);
            for (int i = target.Track.Count - take; i < target.Track.Count; i++)
            {
                result.Add(target.Track[i]);
            }
            return result;
        }

        private static bool WithinHalf(RadarLayout layout, double bearing)
        {
            double delta = bearing - layout.StartBearing;
            delta %= 360d;
            if (delta < 0d)
            {
                delta += 360d;
            }
            return delta <= 180d + 1e-6;
        }

        private static SKPoint PolarAt(RadarLayout layout, float radius, double bearing)
        {
            double radians = bearing * Math.PI / 180d;
            return new SKPoint(
                layout.Center.X + (float)(Math.Sin(radians) * radius),
                layout.Center.Y - (float)(Math.Cos(radians) * radius));
        }

        private static SKPath ArcPath(RadarLayout layout, float radius, double startBearing,
            double sweepDegrees, bool closeToCenter)
        {
            SKPath path = new SKPath();
            if (closeToCenter)
            {
                path.MoveTo(layout.Center);
            }

            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(sweepDegrees) / 2d));
            for (int i = 0; i <= steps; i++)
            {
                double bearing = startBearing + sweepDegrees * i / steps;
                SKPoint point = PolarAt(layout, radius, bearing);
                if (i == 0 && !closeToCenter)
                {
                    path.MoveTo(point);
                }
                else
                {
                    path.LineTo(point);
                }
            }

            if (closeToCenter)
            {
                path.Close();
            }
            return path;
        }

        private static SKPath WedgePath(RadarLayout layout, double fromBearing, double toBearing)
        {
            SKPath path = new SKPath();
            path.MoveTo(layout.Center);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(toBearing - fromBearing) / 4d));
            for (int i = 0; i <= steps; i++)
            {
                double bearing = fromBearing + (toBearing - fromBearing) * i / steps;
                path.LineTo(PolarAt(layout, layout.Radius, bearing));
            }
            path.Close();
            return path;
        }

        private static SKPath Arrow(float tipX, float tipY, float halfWidth, float height)
        {
            SKPath path = new SKPath();
            path.MoveTo(tipX, tipY);
            path.LineTo(tipX - halfWidth, tipY + height);
            path.LineTo(tipX + halfWidth, tipY + height);
            path.Close();
            return path;
        }

        private static void FillPath(Painter p, SKPath path, SKColor color)
        {
            using (SKPaint paint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            })
            {
                p.Canvas.DrawPath(path, paint);
            }
        }

        private static void StrokePath(Painter p, SKPath path, SKColor color, float width)
        {
            using (SKPaint paint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = width <= 0f ? 1f : width,
            })
            {
                p.Canvas.DrawPath(path, paint);
            }
        }

        private static SKColor WithAlpha(SKColor color, int alpha)
        {
            int value = Math.Max(0, Math.Min(255, alpha));
            return new SKColor(color.Red, color.Green, color.Blue, (byte)value);
        }

        private static void ClampInto(SKRect box, SKRect bounds, float margin)
        {
            float dx = 0f;
            if (box.Left < bounds.Left + margin)
            {
                dx = bounds.Left + margin - box.Left;
            }
            else if (box.Right > bounds.Right - margin)
            {
                dx = bounds.Right - margin - box.Right;
            }

            float dy = 0f;
            if (box.Top < bounds.Top + margin)
            {
                dy = bounds.Top + margin - box.Top;
            }
            else if (box.Bottom > bounds.Bottom - margin)
            {
                dy = bounds.Bottom - margin - box.Bottom;
            }

            if (dx != 0f || dy != 0f)
            {
                box.Offset(dx, dy);
            }
        }
    }
}
