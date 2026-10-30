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

namespace OODD.Charts.Engineering.Dga
{
    /// <summary>趋势轨共享轴上的一个取样日刻度。</summary>
    public readonly struct DgaDayTick
    {
        public DgaDayTick(double day, string dateText, float y)
        {
            Day = day;
            DateText = dateText ?? string.Empty;
            Y = y;
        }

        public double Day { get; }

        public string DateText { get; }

        /// <summary>该取样日在页面上的 y（各轨共用）。</summary>
        public float Y { get; }
    }

    /// <summary>DGA 图的一次版面求解结果：趋势轨、三角形映射、分区多边形与投点都从这里取。</summary>
    public sealed class DgaLayout
    {
        public DgaLayout(SKRect bounds, SKRect titleRect, SKRect trendRect, SKRect triangleRect, SKRect footerRect,
            SKRect triangleTitleRect, SKRect apexLabelRect,
            TrackStack trend, TriaxialMap triangle, IReadOnlyList<EnvelopePolygon> zonePolygons,
            IReadOnlyList<SKPoint> samplePoints, IReadOnlyList<DgaDayTick> dayTicks)
        {
            Bounds = bounds;
            TitleRect = titleRect;
            TrendRect = trendRect;
            TriangleRect = triangleRect;
            FooterRect = footerRect;
            TriangleTitleRect = triangleTitleRect;
            ApexLabelRect = apexLabelRect;
            Trend = trend;
            Triangle = triangle;
            ZonePolygons = zonePolygons;
            SamplePoints = samplePoints;
            DayTicks = dayTicks;
        }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect TrendRect { get; }
        public SKRect TriangleRect { get; }

        /// <summary>三角形标题带：在趋势区与顶点之间。</summary>
        public SKRect TriangleTitleRect { get; }

        /// <summary>顶点 A 的标注带：标题带之下、顶点之上。</summary>
        public SKRect ApexLabelRect { get; }

        public SKRect FooterRect { get; }
        public TrackStack Trend { get; }
        public TriaxialMap Triangle { get; }

        /// <summary>故障分区在页面像素上的多边形（顶点由文档三组分映射而来）。</summary>
        public IReadOnlyList<EnvelopePolygon> ZonePolygons { get; }

        public IReadOnlyList<SKPoint> SamplePoints { get; }

        public IReadOnlyList<DgaDayTick> DayTicks { get; }
    }

    /// <summary>
    /// 变压器油色谱 DGA（engineering.dga）。
    /// 上半部为多气体趋势轨，共享按天偏移的非等距取样轴；下半部为三轴反向刻度的大卫三角形，
    /// 分区多边形与投点均由文档给出，库只做三组分 → 像素的重心坐标映射。
    /// </summary>
    public sealed class DgaChartTemplate : IChartTemplate
    {
        public string Key => "engineering.dga";

        public string DisplayName => "变压器油色谱分析";

        public DgaChartStyle Tokens { get; set; } = new DgaChartStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            DgaChartDocument doc = Cast(document);
            DgaChartStyle tokens = Tokens;
            if (doc.GasSeries.Count == 0)
            {
                throw new InvalidOperationException("DGA 图至少需要一条气体趋势轨。");
            }

            SKRect bounds = new SKRect(0f, 0f, tokens.Common.Width, tokens.Common.Height);
            SKRect title = new SKRect(tokens.Common.BindingMargin, tokens.Common.Margin,
                bounds.Right - tokens.Common.Margin, tokens.Common.Margin + tokens.Common.TitleHeight);
            SKRect footer = new SKRect(tokens.Common.BindingMargin,
                bounds.Bottom - tokens.Common.FooterHeight - tokens.Common.Margin,
                bounds.Right - tokens.Common.Margin, bounds.Bottom - tokens.Common.Margin);

            // 所有轨共享的取样日集合：按天偏移排序，日期文字取首次出现的同一取样日。
            SortedDictionary<double, string> days = new SortedDictionary<double, string>();
            foreach (DgaGasSeries series in doc.GasSeries)
            {
                foreach (DgaGasSample sample in series.Samples)
                {
                    if (!days.ContainsKey(sample.DayOffset))
                    {
                        days[sample.DayOffset] = sample.DateText;
                    }
                }
            }
            if (days.Count < 2)
            {
                throw new InvalidOperationException("趋势轨至少需要两个取样日，实到 " + days.Count + "。");
            }

            double maxDay = 0d;
            foreach (double day in days.Keys)
            {
                if (day > maxDay)
                {
                    maxDay = day;
                }
            }
            if (!(maxDay > 0d))
            {
                throw new InvalidOperationException("取样日偏移的最大值必须为正，实到 " + maxDay + "。");
            }

            float domainFrom = tokens.TrendTop + tokens.TrackHeaderHeight;
            float domainTo = domainFrom + tokens.TrendHeight;
            // 轨道总宽不得超过版心：按 A4 横向 1075px、右边距 20px，5 轨 × 210 + 4 × 12 会伸出 139px。
            float trackWidth = tokens.TrackWidth;
            int seriesCount = doc.GasSeries.Count;
            if (seriesCount > 0)
            {
                float usableRight = tokens.Common.Width - tokens.Common.Margin;
                float fit = (usableRight - tokens.TrendLeft - (seriesCount - 1) * tokens.TrackGap) / seriesCount;
                if (fit < trackWidth)
                {
                    trackWidth = Math.Max(24f, fit);
                }
            }
            SKRect trendRect = new SKRect(tokens.TrendLeft, tokens.TrendTop,
                tokens.TrendLeft + seriesCount * trackWidth + (seriesCount - 1) * tokens.TrackGap,
                domainTo);

            List<Track> tracks = new List<Track>();
            for (int i = 0; i < seriesCount; i++)
            {
                DgaGasSeries series = doc.GasSeries[i];
                double maxValue = 0d;
                foreach (DgaGasSample sample in series.Samples)
                {
                    if (sample.Value > maxValue)
                    {
                        maxValue = sample.Value;
                    }
                }
                double axisMax = maxValue > 0d ? maxValue * tokens.ValueHeadroom : 1d;
                float left = tokens.TrendLeft + i * (trackWidth + tokens.TrackGap);
                NumericAxis axis = new NumericAxis(series.Key, 0d, axisMax, left, left + trackWidth,
                    AxisDirection.HorizontalRight, AxisScale.Linear);
                string label = string.IsNullOrEmpty(series.Unit)
                    ? series.Label
                    : series.Label + " (" + series.Unit + ")";
                tracks.Add(new Track(series.Key, label, trackWidth, axis, TrackKind.Curve));
            }

            TrackLayoutOptions options = new TrackLayoutOptions
            {
                TrackGap = tokens.TrackGap,
                HeaderHeight = tokens.TrackHeaderHeight,
                SeparatorWidth = 1f,
                DrawSeparators = true,
            };
            TrackStack stack = new TrackStack("dga-trend", tokens.TrendLeft, domainFrom, domainTo,
                0d, maxDay, tracks, options);

            List<DgaDayTick> dayTicks = new List<DgaDayTick>();
            foreach (KeyValuePair<double, string> day in days)
            {
                dayTicks.Add(new DgaDayTick(day.Key, day.Value, stack.DomainPositionOf(day.Key)));
            }

            // 等边三角形：A 在顶点、B 在左下、C 在右下。
            // 趋势区与顶点之间只有一条缝：标题与顶点 A 标注各占一带，缝不够就把三角形整体下移。
            float side = tokens.TriangleSide;
            SKRect triangleTitleRect = new SKRect(tokens.TriangleLeft - 60f, trendRect.Bottom + 4f,
                tokens.TriangleLeft + side + 60f, trendRect.Bottom + 4f + tokens.TriangleTitleBand);
            float apexTop = triangleTitleRect.Bottom + 2f;
            float triangleTop = Math.Max(tokens.TriangleTop, apexTop + tokens.ApexLabelBand);
            SKRect apexLabelRect = new SKRect(tokens.TriangleLeft + side * 0.5f - 60f, apexTop,
                tokens.TriangleLeft + side * 0.5f + 60f, triangleTop);
            float height = (float)(side * Math.Sqrt(3d) / 2d);
            SKPoint vertexA = new SKPoint(tokens.TriangleLeft + side * 0.5f, triangleTop);
            SKPoint vertexB = new SKPoint(tokens.TriangleLeft, triangleTop + height);
            SKPoint vertexC = new SKPoint(tokens.TriangleLeft + side, triangleTop + height);
            SKRect triangleRect = new SKRect(tokens.TriangleLeft, triangleTop,
                tokens.TriangleLeft + side, triangleTop + height);
            TriaxialMap map = new TriaxialMap(vertexA, vertexB, vertexC);

            List<EnvelopePolygon> zones = new List<EnvelopePolygon>();
            for (int i = 0; i < doc.Zones.Count; i++)
            {
                DgaZone zone = doc.Zones[i];
                List<SKPoint> vertices = new List<SKPoint>();
                foreach (TriaxialComponents component in zone.Vertices)
                {
                    vertices.Add(map.PointOf(component.A, component.B, component.C));
                }
                zones.Add(new EnvelopePolygon("zone-" + i.ToString(CultureInfo.InvariantCulture),
                    zone.Name, vertices));
            }

            List<SKPoint> samplePoints = new List<SKPoint>();
            foreach (DgaSample sample in doc.Samples)
            {
                samplePoints.Add(map.PointOf(sample.Components.A, sample.Components.B, sample.Components.C));
            }

            DgaLayout layout = new DgaLayout(bounds, title, trendRect, triangleRect, footer,
                triangleTitleRect, apexLabelRect, stack, map, zones, samplePoints, dayTicks);
            return ChartGeometry.ForLayout(bounds, trendRect, layout);
        }

        public void DrawContent(Painter p, ChartGeometry geometry, ChartDocument document)
        {
            DgaChartDocument doc = Cast(document);
            DgaLayout layout = geometry.LayoutOf<DgaLayout>()
                ?? throw new InvalidOperationException("版面不是 DgaLayout。");
            DgaChartStyle tokens = Tokens;
            EngineeringPalette palette = tokens.Palette;
            ChartStyle style = p.Style;
            FontKey titleFont = new FontKey(style.Text.Family, tokens.Common.TitleSize, true);
            FontKey subtitleFont = new FontKey(style.Text.Family, tokens.Common.SubtitleSize);
            FontKey trendFont = new FontKey(style.Text.Family, tokens.TrendTickSize);
            FontKey labelFont = new FontKey(style.Text.Family, tokens.TrendLabelSize);
            FontKey zoneFont = new FontKey(style.Text.Family, tokens.ZoneLabelSize);
            FontKey vertexFont = new FontKey(style.Text.Family, tokens.VertexLabelSize);

            p.FillRect(layout.Bounds, palette.PageBackground);
            p.TextInBox(string.IsNullOrEmpty(doc.FormTitle) ? DisplayName : doc.FormTitle!,
                layout.TitleRect, titleFont, palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox(BuildSubtitle(doc), new SKRect(layout.TitleRect.Left, layout.TitleRect.Bottom,
                    layout.TitleRect.Right, layout.TitleRect.Bottom + tokens.Common.SubtitleSize + 8f),
                subtitleFont, palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);

            DrawTrend(p, layout, tokens, palette, trendFont, labelFont, doc);
            DrawTriangle(p, layout, tokens, palette, zoneFont, vertexFont, trendFont, doc);

            p.TextInBox(string.IsNullOrEmpty(doc.FooterNote) ? tokens.FooterDefault : doc.FooterNote!,
                layout.FooterRect, subtitleFont, palette.MutedText, CellAlign.Near, CellAlign.Far, 0f);
        }

        private static void DrawTrend(Painter p, DgaLayout layout, DgaChartStyle tokens,
            EngineeringPalette palette, FontKey tickFont, FontKey labelFont, DgaChartDocument doc)
        {
            TrackStack stack = layout.Trend;

            p.TextInBox(tokens.TrendTitle, new SKRect(stack.Left, layout.TrendRect.Top - 20f,
                    layout.TrendRect.Right, layout.TrendRect.Top),
                labelFont, palette.MutedText, CellAlign.Near, CellAlign.Center, 0f);

            // 取样日网格：所有轨共用同一条 y，非等距由域值直接决定。
            foreach (DgaDayTick day in layout.DayTicks)
            {
                p.Line(stack.Left, day.Y, stack.Right, day.Y, palette.Grid, tokens.TrendGridStroke);
                p.TextInBox(day.DateText, new SKRect(stack.Left - tokens.DayLabelWidth - 4f, day.Y - 7f,
                        stack.Left - 4f, day.Y + 7f),
                    tickFont, palette.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            // 道头：每页同位置；道头格与道体同宽。
            for (int i = 0; i < stack.Count; i++)
            {
                TrackHeader header = stack.Headers[i];
                SKRect rect = header.Rect;
                p.FillRect(rect, palette.PanelFill);
                p.Frame(rect, palette.PanelBorder, 0.8f);
                p.TextInBox(header.Label, new SKRect(rect.Left + 2f, rect.Top + 1f, rect.Right - 2f, rect.Top + 18f),
                    labelFont, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
                p.TextInBox(header.ScaleCaption, new SKRect(rect.Left + 2f, rect.Top + 17f,
                        rect.Right - 2f, rect.Bottom),
                    tickFont, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
            }

            for (int i = 0; i < stack.Count; i++)
            {
                DgaGasSeries series = doc.GasSeries[i];
                List<DgaGasSample> samples = new List<DgaGasSample>(series.Samples);
                samples.Sort((a, b) => a.DayOffset.CompareTo(b.DayOffset));
                List<SKPoint> points = new List<SKPoint>();
                foreach (DgaGasSample sample in samples)
                {
                    points.Add(stack.PointOf(i, sample.DayOffset, sample.Value));
                }
                if (points.Count >= 2)
                {
                    p.Polyline(points, palette.Accent, tokens.TrendStroke, false);
                }
                foreach (SKPoint point in points)
                {
                    p.FillCircle(point, tokens.TrendMarkerRadius, palette.Accent);
                }

                SKRect trackRect = stack.RectOf(i);
                p.Line(trackRect.Left, trackRect.Top, trackRect.Left, trackRect.Bottom,
                    palette.PanelBorder, tokens.TrendGridStroke);
                p.Line(trackRect.Right, trackRect.Top, trackRect.Right, trackRect.Bottom,
                    palette.PanelBorder, tokens.TrendGridStroke);
            }

            p.Frame(layout.TrendRect, palette.PanelBorder, 0.8f);
        }

        private static void DrawTriangle(Painter p, DgaLayout layout, DgaChartStyle tokens,
            EngineeringPalette palette, FontKey zoneFont, FontKey vertexFont, FontKey tickFont,
            DgaChartDocument doc)
        {
            TriaxialMap map = layout.Triangle;

            p.TextInBox(tokens.TriangleTitle, layout.TriangleTitleRect,
                zoneFont, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);

            // 分区填色：颜色按分区序循环，透明度统一由令牌给；边界只描边。
            for (int i = 0; i < layout.ZonePolygons.Count; i++)
            {
                EnvelopePolygon zone = layout.ZonePolygons[i];
                SKColor baseColor = ZoneColor(palette, i);
                if (tokens.ZoneFillAlpha > 0)
                {
                    p.FillPolygon(zone.Vertices, WithAlpha(baseColor, tokens.ZoneFillAlpha));
                }
                p.StrokePath(p.BuildPath(zone.Vertices, true), baseColor, tokens.ZoneStroke);
            }

            // 三角外框
            List<SKPoint> frame = new List<SKPoint> { map.VertexA, map.VertexB, map.VertexC };
            p.StrokePath(p.BuildPath(frame, true), palette.Ink, tokens.TriangleStroke);

            // 三边刻度各自反向：AB 读 B、BC 读 C、CA 读 A，均从 0 走到该组分满值。
            DrawEdgeScale(p, map, TriaxialEdge.AB, palette, tickFont, tokens);
            DrawEdgeScale(p, map, TriaxialEdge.BC, palette, tickFont, tokens);
            DrawEdgeScale(p, map, TriaxialEdge.CA, palette, tickFont, tokens);

            p.TextInBox("A · " + tokens.ComponentALabel, layout.ApexLabelRect,
                vertexFont, palette.Ink, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("B · " + tokens.ComponentBLabel, new SKRect(map.VertexB.X - 66f,
                    map.VertexB.Y + 2f, map.VertexB.X - 2f, map.VertexB.Y + 18f),
                vertexFont, palette.Ink, CellAlign.Far, CellAlign.Center, 0f);
            p.TextInBox("C · " + tokens.ComponentCLabel, new SKRect(map.VertexC.X + 2f,
                    map.VertexC.Y + 2f, map.VertexC.X + 66f, map.VertexC.Y + 18f),
                vertexFont, palette.Ink, CellAlign.Near, CellAlign.Center, 0f);

            // 区名标注：落在分区多边形顶点均值处。
            for (int i = 0; i < layout.ZonePolygons.Count; i++)
            {
                EnvelopePolygon zone = layout.ZonePolygons[i];
                float sumX = 0f, sumY = 0f;
                foreach (SKPoint vertex in zone.Vertices)
                {
                    sumX += vertex.X;
                    sumY += vertex.Y;
                }
                float cx = sumX / zone.Vertices.Count;
                float cy = sumY / zone.Vertices.Count;
                p.TextInBox(zone.Label, new SKRect(cx - 58f, cy - 8f, cx + 58f, cy + 8f),
                    zoneFont, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            // 投点：油色谱取样在三角图上的位置。
            foreach (SKPoint point in layout.SamplePoints)
            {
                p.Circle(point, tokens.TrianglePointRadius, palette.Warning, 1.3f);
                p.Line(point.X - tokens.TrianglePointRadius, point.Y,
                    point.X + tokens.TrianglePointRadius, point.Y, palette.Warning, 1f);
                p.Line(point.X, point.Y - tokens.TrianglePointRadius,
                    point.X, point.Y + tokens.TrianglePointRadius, palette.Warning, 1f);
            }

            // 右侧图例：分区色块 + 区名。
            float legendX = layout.TriangleRect.Right + 28f;
            float legendY = layout.TriangleRect.Top + 6f;
            for (int i = 0; i < layout.ZonePolygons.Count; i++)
            {
                EnvelopePolygon zone = layout.ZonePolygons[i];
                SKColor color = ZoneColor(palette, i);
                p.FillRect(new SKRect(legendX, legendY, legendX + 10f, legendY + 10f),
                    WithAlpha(color, 120));
                p.Rect(new SKRect(legendX, legendY, legendX + 10f, legendY + 10f), color, 0.9f);
                p.TextInBox(zone.Label, new SKRect(legendX + 16f, legendY - 3f,
                        legendX + 230f, legendY + 13f),
                    zoneFont, palette.Text, CellAlign.Near, CellAlign.Center, 0f);
                legendY += 16f;
            }
            legendY += 6f;
            p.Circle(new SKPoint(legendX + 5f, legendY + 5f), tokens.TrianglePointRadius, palette.Warning, 1.2f);
            p.TextInBox(tokens.SampleLegendText, new SKRect(legendX + 16f, legendY - 3f,
                    legendX + 260f, legendY + 13f),
                zoneFont, palette.Text, CellAlign.Near, CellAlign.Center, 0f);

            if (doc.Samples.Count > 0)
            {
                legendY += 20f;
                p.TextInBox(doc.Samples[doc.Samples.Count - 1].DateText, new SKRect(legendX,
                        legendY, legendX + 260f, legendY + 14f),
                    tickFont, palette.MutedText, CellAlign.Near, CellAlign.Center, 0f);
            }
        }

        private static void DrawEdgeScale(Painter p, TriaxialMap map, TriaxialEdge edge,
            EngineeringPalette palette, FontKey font, DgaChartStyle tokens)
        {
            float cx = (map.VertexA.X + map.VertexB.X + map.VertexC.X) / 3f;
            float cy = (map.VertexA.Y + map.VertexB.Y + map.VertexC.Y) / 3f;

            for (int k = 1; k < 5; k++)
            {
                double t = k / 5d;
                SKPoint point = map.PointOnEdge(edge, t);
                float vx = point.X - cx;
                float vy = point.Y - cy;
                float length = (float)Math.Sqrt(vx * vx + vy * vy);
                if (length <= 1e-6f)
                {
                    continue;
                }
                float ux = vx / length;
                float uy = vy / length;

                p.Line(point.X, point.Y, point.X + ux * tokens.EdgeTickLength,
                    point.Y + uy * tokens.EdgeTickLength, palette.Ink, 0.9f);

                float labelX = point.X + ux * tokens.EdgeLabelGap;
                float labelY = point.Y + uy * tokens.EdgeLabelGap;
                p.TextInBox((t * 100d).ToString("0", CultureInfo.InvariantCulture),
                    new SKRect(labelX - 14f, labelY - 6f, labelX + 14f, labelY + 6f),
                    font, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }
        }

        private static SKColor ZoneColor(EngineeringPalette palette, int index)
        {
            switch (index % 3)
            {
                case 0: return palette.Accent;
                case 1: return palette.Secondary;
                default: return palette.Warning;
            }
        }

        private static SKColor WithAlpha(SKColor color, byte alpha)
        {
            return new SKColor(color.Red, color.Green, color.Blue, alpha);
        }

        private static string BuildSubtitle(DgaChartDocument doc)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(doc.TransformerId))
            {
                parts.Add("主变 " + doc.TransformerId);
            }
            if (!string.IsNullOrEmpty(doc.OilBrand))
            {
                parts.Add("油种 " + doc.OilBrand);
            }
            if (!string.IsNullOrEmpty(doc.SampleDateText))
            {
                parts.Add("取样 " + doc.SampleDateText);
            }
            return string.Join("　", parts.ToArray());
        }

        private static DgaChartDocument Cast(ChartDocument document)
        {
            DgaChartDocument? doc = document as DgaChartDocument;
            if (doc == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(DgaChartDocument) + "。", nameof(document));
            }
            return doc;
        }
    }
}
