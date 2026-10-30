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

namespace OODD.Charts.Engineering.WellLog
{
    /// <summary>测井成果图的一次版面求解结果。</summary>
    public sealed class WellLogLayout
    {
        public WellLogLayout(SKRect bounds, SKRect titleRect, SKRect infoRect, SKRect plotRect, SKRect footerRect,
            IReadOnlyList<DomainRange> pages, int pageIndex, DomainRange pageRange,
            TrackStack stack, NumericAxis depthAxis, IReadOnlyList<AxisMark> depthMarks)
        {
            Bounds = bounds;
            TitleRect = titleRect;
            InfoRect = infoRect;
            PlotRect = plotRect;
            FooterRect = footerRect;
            Pages = pages;
            PageIndex = pageIndex;
            PageRange = pageRange;
            Stack = stack;
            DepthAxis = depthAxis;
            DepthMarks = depthMarks;
        }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect InfoRect { get; }
        public SKRect PlotRect { get; }
        public SKRect FooterRect { get; }
        public IReadOnlyList<DomainRange> Pages { get; }
        public int PageIndex { get; }
        public DomainRange PageRange { get; }
        public TrackStack Stack { get; }
        public NumericAxis DepthAxis { get; }
        public IReadOnlyList<AxisMark> DepthMarks { get; }

        public IReadOnlyList<TrackHeader> Headers => Stack.Headers;

        /// <summary>当前页深度区间的起点与终点（域值，m）。</summary>
        public double PageDepthFrom => PageRange.Start;

        public double PageDepthTo => PageRange.End;

        /// <summary>某道在页面上的落点：x 走道值轴，y 走共享深度轴。</summary>
        public SKPoint PointOf(int trackIndex, double depth, double value)
        {
            return Stack.PointOf(trackIndex, depth, value);
        }
    }

    /// <summary>
    /// 测井成果图（engineering.well-log）。
    /// 多道横向并置、共享自上而下递增的深度轴；线性道与对数道混排；
    /// 按 <see cref="WellLogDocument.PageDepth"/> 续页，每页重复同一套道头。
    /// </summary>
    public sealed class WellLogChartTemplate : IChartTemplate
    {
        public const string AxisDepth = "depth";

        public string Key => "engineering.well-log";

        public string DisplayName => "测井成果图";

        public WellLogChartStyle Tokens { get; set; } = new WellLogChartStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            WellLogDocument doc = Cast(document);
            WellLogChartStyle tokens = Tokens;
            if (doc.Tracks.Count == 0)
            {
                throw new InvalidOperationException("测井图至少要有一道，实到 0 道。");
            }
            if (doc.DepthBottom <= doc.DepthTop)
            {
                throw new InvalidOperationException("终止深度必须大于起始深度：[" + doc.DepthTop + "," + doc.DepthBottom + "]。");
            }

            List<DomainRange> pages = DepthPager.Split(doc.DepthTop, doc.DepthBottom, doc.PageDepth);
            int pageIndex = doc.PageIndex < 0 ? 0 : doc.PageIndex;
            if (pageIndex >= pages.Count)
            {
                pageIndex = pages.Count - 1;
            }
            DomainRange page = pages[pageIndex];

            float width = Unit.Mm(tokens.PaperWidthMm);
            SKRect bounds = new SKRect(0f, 0f, width, tokens.Common.Height);
            SKRect title = new SKRect(tokens.Common.BindingMargin, tokens.Common.Margin,
                bounds.Right - tokens.Common.Margin, tokens.Common.Margin + tokens.Common.TitleHeight);
            SKRect info = new SKRect(tokens.Common.BindingMargin, title.Bottom,
                bounds.Right - tokens.Common.Margin, title.Bottom + tokens.Common.TableRowHeight);
            SKRect plot = new SKRect(tokens.TrackLeft, tokens.PlotTop,
                tokens.TrackLeft + doc.Tracks.Count * tokens.TrackWidth + (doc.Tracks.Count - 1) * tokens.TrackGap,
                tokens.PlotTop + tokens.PlotHeight);
            SKRect footer = new SKRect(tokens.Common.BindingMargin,
                bounds.Bottom - tokens.Common.FooterHeight - tokens.Common.Margin,
                bounds.Right - tokens.Common.Margin, bounds.Bottom - tokens.Common.Margin);

            List<Track> tracks = new List<Track>();
            for (int i = 0; i < doc.Tracks.Count; i++)
            {
                WellLogTrack source = doc.Tracks[i];
                if (!(source.Max > source.Min))
                {
                    throw new InvalidOperationException("道 \"" + source.Key + "\" 的量程上限必须大于下限。");
                }
                if (source.Logarithmic && source.Min <= 0d)
                {
                    throw new InvalidOperationException("对数道 \"" + source.Key + "\" 的量程下限必须为正。");
                }

                float left = tokens.TrackLeft + i * (tokens.TrackWidth + tokens.TrackGap);
                float right = left + tokens.TrackWidth;
                NumericAxis valueAxis = new NumericAxis(source.Key, source.Min, source.Max,
                    left, right, AxisDirection.HorizontalRight,
                    source.Logarithmic ? AxisScale.Logarithmic : AxisScale.Linear);
                TrackKind kind = source.Logarithmic ? TrackKind.LogCurve : TrackKind.Curve;
                if (source.Intervals.Count > 0 && source.Samples.Count == 0)
                {
                    kind = TrackKind.PatternColumn;
                }
                tracks.Add(new Track(source.Key, source.HeaderLabel, tokens.TrackWidth, valueAxis, kind));
            }

            TrackLayoutOptions options = new TrackLayoutOptions
            {
                TrackGap = tokens.TrackGap,
                HeaderHeight = tokens.TrackHeaderHeight,
                SeparatorWidth = tokens.FrameStroke,
                DrawSeparators = true,
            };
            TrackStack stack = new TrackStack("well-log", tokens.TrackLeft, plot.Top, plot.Bottom,
                page.Start, page.End, tracks, options);
            NumericAxis depthAxis = new NumericAxis(AxisDepth, page.Start, page.End,
                plot.Top, plot.Bottom, AxisDirection.VerticalDown, AxisScale.Linear);
            List<AxisMark> marks = AxisRuler.Marks(depthAxis,
                new[] { new AxisTier(tokens.DepthTick, RulerTier.Medium, true) });

            WellLogLayout layout = new WellLogLayout(bounds, title, info, plot, footer,
                pages, pageIndex, page, stack, depthAxis, marks);
            return ChartGeometry.ForLayout(bounds, plot, layout);
        }

        public void DrawContent(Painter p, ChartGeometry geometry, ChartDocument document)
        {
            WellLogDocument doc = Cast(document);
            WellLogLayout layout = geometry.LayoutOf<WellLogLayout>()
                ?? throw new InvalidOperationException("版面不是 WellLogLayout。");
            WellLogChartStyle tokens = Tokens;
            EngineeringPalette palette = tokens.Palette;
            ChartStyle style = p.Style;
            FontKey titleFont = new FontKey(style.Text.Family, tokens.Common.TitleSize, true);
            FontKey infoFont = new FontKey(style.Text.Family, tokens.Common.SubtitleSize);
            FontKey headerFont = new FontKey(style.Text.Family, tokens.HeaderLabelSize, true);
            FontKey captionFont = new FontKey(style.Text.Family, tokens.ScaleCaptionSize);
            FontKey depthFont = new FontKey(style.Text.Family, tokens.DepthLabelSize);

            p.FillRect(layout.Bounds, palette.PageBackground);
            p.TextInBox(string.IsNullOrEmpty(doc.FormTitle) ? DisplayName : doc.FormTitle!,
                layout.TitleRect, titleFont, palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            string infoText = BuildInfo(doc, layout, tokens);
            p.TextInBox(infoText, layout.InfoRect, infoFont, palette.SubtitleText, CellAlign.Near, CellAlign.Center, 0f);
            string pageText = "第 " + (layout.PageIndex + 1) + " / " + layout.Pages.Count + " 页　"
                + layout.PageDepthFrom.ToString("0.##", CultureInfo.InvariantCulture) + "–"
                + layout.PageDepthTo.ToString("0.##", CultureInfo.InvariantCulture) + " m";
            p.TextInBox(pageText, layout.InfoRect, infoFont, palette.MutedText, CellAlign.Far, CellAlign.Center, 0f);

            p.Frame(layout.PlotRect, palette.Ink, tokens.FrameStroke);

            foreach (AxisMark mark in layout.DepthMarks)
            {
                p.Line(layout.PlotRect.Left, mark.Position, layout.PlotRect.Right, mark.Position,
                    palette.Grid, tokens.GridStroke);
                p.Line(layout.PlotRect.Left - 4f, mark.Position, layout.PlotRect.Left, mark.Position,
                    palette.Ink, tokens.FrameStroke);
                SKRect box = new SKRect(layout.PlotRect.Left - 55f, mark.Position - 7f,
                    layout.PlotRect.Left - 6f, mark.Position + 7f);
                p.TextInBox(mark.Label("0.##"), box, depthFont, palette.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            for (int i = 0; i < layout.Stack.Count; i++)
            {
                SKRect rect = layout.Stack.RectOf(i);
                p.Frame(rect, palette.PanelBorder, tokens.GridStroke);
                DrawTrackHeader(p, layout, i, headerFont, captionFont, palette, tokens);
                DrawTrackContent(p, doc, layout, i, palette, tokens);
            }

            p.TextInBox(string.IsNullOrEmpty(doc.FooterNote) ? tokens.FooterDefault : doc.FooterNote!,
                layout.FooterRect, captionFont, palette.MutedText, CellAlign.Near, CellAlign.Center, 0f);
        }

        private static void DrawTrackHeader(Painter p, WellLogLayout layout, int index, FontKey headerFont,
                                            FontKey captionFont, EngineeringPalette palette, WellLogChartStyle tokens)
        {
            TrackHeader header = layout.Headers[index];
            SKRect rect = header.Rect;
            if (rect.IsEmpty)
            {
                return;
            }
            p.FillRect(rect, palette.PanelFill);
            p.Frame(rect, palette.PanelBorder, tokens.FrameStroke);
            p.TextInBox(header.Label, new SKRect(rect.Left + 2f, rect.Top + 1f, rect.Right - 2f, rect.Top + rect.Height * 0.56f),
                headerFont, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox(header.ScaleCaption,
                new SKRect(rect.Left + 2f, rect.Top + rect.Height * 0.52f, rect.Right - 2f, rect.Bottom - 1f),
                captionFont, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawTrackContent(Painter p, WellLogDocument doc, WellLogLayout layout, int index,
                                             EngineeringPalette palette, WellLogChartStyle tokens)
        {
            WellLogTrack source = doc.Tracks[index];
            SKRect rect = layout.Stack.RectOf(index);
            SKColor color = index == 0 ? palette.Accent : (index % 2 == 1 ? palette.Secondary : palette.Ink);

            if (source.Intervals.Count > 0)
            {
                foreach (WellLogInterval interval in source.Intervals)
                {
                    double top = Math.Max(interval.TopDepth, layout.PageDepthFrom);
                    double bottom = Math.Min(interval.BottomDepth, layout.PageDepthTo);
                    if (bottom <= top)
                    {
                        continue;
                    }
                    float y0 = layout.Stack.DomainPositionOf(top);
                    float y1 = layout.Stack.DomainPositionOf(bottom);
                    SKRect band = new SKRect(rect.Left + 1f, Math.Min(y0, y1), rect.Right - 1f, Math.Max(y0, y1));
                    using (SKPath path = new SKPath())
                    {
                        path.AddRect(band);
                        PatternFill pattern = PatternFill.Of(interval.Pattern, tokens.PatternSpacingMm,
                            tokens.PatternStrokeMm, palette.PanelBorder);
                        p.FillPathWithPattern(path, pattern);
                    }
                    p.Line(rect.Left, y0, rect.Right, y0, palette.PanelBorder, tokens.GridStroke);
                    p.Line(rect.Left, y1, rect.Right, y1, palette.PanelBorder, tokens.GridStroke);
                    if (band.Height > 12f)
                    {
                        p.TextInBox(interval.Label, new SKRect(band.Left + 2f, band.Top + 1f, band.Right - 2f, band.Top + 12f),
                            new FontKey(p.Style.Text.Family, tokens.ScaleCaptionSize), palette.MutedText,
                            CellAlign.Near, CellAlign.Center, 1f);
                    }
                }
            }

            if (source.Samples.Count >= 2)
            {
                List<WellLogSample> samples = new List<WellLogSample>(source.Samples);
                samples.Sort((a, b) => a.Depth.CompareTo(b.Depth));
                List<SKPoint> points = new List<SKPoint>();
                foreach (WellLogSample sample in samples)
                {
                    if (sample.Depth < layout.PageDepthFrom || sample.Depth > layout.PageDepthTo)
                    {
                        continue;
                    }
                    if (double.IsNaN(sample.Value) || double.IsInfinity(sample.Value))
                    {
                        continue;
                    }
                    points.Add(new SKPoint(layout.Stack.Tracks[index].PositionOf(sample.Value),
                        layout.Stack.DomainPositionOf(sample.Depth)));
                }
                if (points.Count >= 2)
                {
                    p.Polyline(points, color, tokens.CurveStroke, false);
                }
                foreach (SKPoint point in points)
                {
                    p.FillCircle(point, tokens.MarkerRadius, color);
                }
            }

            if (source.Logarithmic || index == 0)
            {
                List<AxisTier> tiers = new List<AxisTier>();
                if (source.Logarithmic)
                {
                    tiers.Add(AxisRuler.LogTier(RulerTier.Fine, true, new[] { 1d, 2d, 5d }, source.Min, source.Max));
                }
                else
                {
                    double step = (source.Max - source.Min) / 4d;
                    if (step > 0d)
                    {
                        tiers.Add(new AxisTier(step, RulerTier.Fine, true));
                    }
                }
                List<AxisMark> marks = AxisRuler.Marks(layout.Stack.Tracks[index].ValueAxis, tiers);
                foreach (AxisMark mark in marks)
                {
                    p.Line(mark.Position, rect.Top, mark.Position, rect.Bottom, palette.Grid, tokens.GridStroke * 0.8f);
                }
            }
        }

        private static string BuildInfo(WellLogDocument doc, WellLogLayout layout, WellLogChartStyle tokens)
        {
            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(doc.WellId))
            {
                parts.Add("井号 " + doc.WellId);
            }
            if (!string.IsNullOrEmpty(doc.RunNumber))
            {
                parts.Add("井次 " + doc.RunNumber);
            }
            if (!string.IsNullOrEmpty(doc.Formation))
            {
                parts.Add("层位 " + doc.Formation);
            }
            parts.Add("比例尺 " + (string.IsNullOrEmpty(doc.ScaleCaption) ? "1:200" : doc.ScaleCaption));
            return string.Join("　", parts.ToArray());
        }

        private static WellLogDocument Cast(ChartDocument document)
        {
            WellLogDocument? doc = document as WellLogDocument;
            if (doc == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(WellLogDocument) + "。", nameof(document));
            }
            return doc;
        }
    }
}
