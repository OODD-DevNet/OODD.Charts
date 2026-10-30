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

namespace OODD.Charts.Transit.TrainGraph
{
    /// <summary>
    /// 列车运行图（含天窗带）。
    /// <para>
    /// 横轴 = 时分（<see cref="AxisRuler"/> 四级 1/5/10/60 分），纵轴 = 站名线
    /// （<see cref="WeightedCategoryAxis"/>，格距按区间运行时分加权），
    /// 运行线由两个站界点之间的解析斜线 + 停站水平段组成，天窗是
    /// <see cref="SpanBand"/>（时刻与站区间双约束）并裁剪到该站区间内。
    /// </para>
    /// <para>
    /// 线宽、站名列宽、时刻带高度全部走 <see cref="TrainGraphStyle"/>，默认值<b>未校准</b>（D22）。
    /// </para>
    /// </summary>
    public sealed class TrainGraphTemplate : IChartTemplate
    {
        public string Key => "transit.train-graph";

        public string DisplayName => "列车运行图";

        /// <summary>版面令牌。</summary>
        public TrainGraphStyle Tokens { get; set; } = new TrainGraphStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            TrainGraphDocument doc = Cast(document);
            TrainGraphLayout layout = TrainGraphLayout.Compute(doc, Tokens);
            return ChartGeometry.ForLayout(layout.Bounds, layout.PlotRect, layout);
        }

        private static TrainGraphDocument Cast(ChartDocument document)
        {
            TrainGraphDocument? typed = document as TrainGraphDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(TrainGraphDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            TrainGraphDocument doc = Cast(document);
            TrainGraphLayout layout = geometry.LayoutOf<TrainGraphLayout>()
                                      ?? TrainGraphLayout.Compute(doc, Tokens);
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;

            painter.FillRect(layout.Bounds, palette.PageBackground);

            DrawTitle(painter, layout, doc);
            DrawWindows(painter, layout, doc);
            DrawStationLines(painter, layout);
            DrawTimeRuler(painter, layout);
            DrawTrains(painter, layout, doc);
            DrawFooter(painter, layout, doc);
            DrawFrame(painter, layout);
        }

        private static void DrawTitle(Painter p, TrainGraphLayout layout, TrainGraphDocument doc)
        {
            ChartStyle style = p.Style;
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            TransitStyle frame = tokens.Frame;

            string title = string.IsNullOrEmpty(doc.FormTitle) ? "列车运行图" : doc.FormTitle!;
            p.TextInBox(title, layout.TitleRect,
                new FontKey(style.Text.Family, frame.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(doc.LineName))
            {
                parts.Add(doc.LineName!);
            }
            parts.Add("车站 " + doc.Stations.Count.ToString(CultureInfo.InvariantCulture)
                      + " 座／列车 " + doc.Trains.Count.ToString(CultureInfo.InvariantCulture)
                      + " 列／天窗 " + doc.Windows.Count.ToString(CultureInfo.InvariantCulture) + " 处");
            parts.Add("横轴 " + layout.ClockText(0d) + "–" + layout.ClockText(doc.Minutes));

            p.TextInBox(string.Join("　", parts.ToArray()), layout.NoteRect,
                new FontKey(style.Text.Family, frame.SubtitleSize),
                palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawWindows(Painter p, TrainGraphLayout layout, TrainGraphDocument doc)
        {
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            PatternFill hatch = PatternFill.Of(PatternKind.Diagonal, tokens.WindowHatchSpacingMm,
                tokens.WindowHatchStrokeMm,
                new SKColor(palette.Maintenance.Red, palette.Maintenance.Green, palette.Maintenance.Blue,
                    tokens.WindowHatchAlpha));

            foreach (TrainWindow window in doc.Windows)
            {
                SKRect rect = layout.WindowRect(window);
                if (!(rect.Width > 0f) || !(rect.Height > 0f))
                {
                    continue;
                }

                if (window.Fill == BandFill.Solid)
                {
                    p.FillRect(rect, palette.PanelFill);
                }
                else if (window.Fill == BandFill.Hatch)
                {
                    using (SKPath path = p.BuildPath(new[]
                    {
                        new SKPoint(rect.Left, rect.Top),
                        new SKPoint(rect.Right, rect.Top),
                        new SKPoint(rect.Right, rect.Bottom),
                        new SKPoint(rect.Left, rect.Bottom),
                    }, true))
                    {
                        p.FillPathWithPattern(path, hatch);
                    }
                }

                // 带的外框只沿墙内画：左右沿时刻、上下沿站区间，因此天然不越出所给站区间。
                p.DashedLine(rect.Left, rect.Top, rect.Left, rect.Bottom, palette.Maintenance, tokens.WindowStroke);
                p.DashedLine(rect.Right, rect.Top, rect.Right, rect.Bottom, palette.Maintenance, tokens.WindowStroke);
                p.DashedLine(rect.Left, rect.Top, rect.Right, rect.Top, palette.Maintenance, tokens.WindowStroke);
                p.DashedLine(rect.Left, rect.Bottom, rect.Right, rect.Bottom, palette.Maintenance, tokens.WindowStroke);

                if (!string.IsNullOrEmpty(window.Label)
                    && p.TextWidth(window.Label, new FontKey(p.Style.Text.Family, tokens.LegendSize)) < rect.Width - 4f)
                {
                    p.TextInBox(window.Label, new SKRect(rect.Left, rect.Top, rect.Right, rect.Top + tokens.LegendSize * 1.6f),
                        new FontKey(p.Style.Text.Family, tokens.LegendSize), palette.MutedText,
                        CellAlign.Center, CellAlign.Center, 1f);
                }
            }
        }

        private static void DrawStationLines(Painter p, TrainGraphLayout layout)
        {
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            SKRect plot = layout.PlotRect;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.Frame.LabelSize);

            for (int i = 0; i < layout.StationAxis.Count; i++)
            {
                float y = layout.StationLineY(i);
                p.Line(plot.Left, y, plot.Right, y, palette.Grid, tokens.StationStroke);

                float half = tokens.Frame.LabelSize * 0.9f;
                SKRect labelBox = new SKRect(plot.Left - tokens.StationLabelWidth,
                    y - half, plot.Left - tokens.StationLabelPadding, y + half);
                p.TextInBox(layout.StationAxis.LabelOf(i), labelBox, key, palette.Text,
                    CellAlign.Far, CellAlign.Center, 0f);
            }
        }

        private static void DrawTimeRuler(Painter p, TrainGraphLayout layout)
        {
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            SKRect plot = layout.PlotRect;
            float tickEnd = layout.TimeAxisRect.Top + layout.TimeAxisRect.Height * tokens.TickBandFraction;
            FontKey labelKey = new FontKey(p.Style.Text.Family, tokens.TimeLabelSize);

            foreach (AxisMark mark in layout.Marks)
            {
                float width = tokens.StrokeOf(mark.Tier);
                // 刻度只画在时刻轴带内：从绘图区底边向下到 TickBandFraction，不穿进运行图。
                p.Line(mark.Position, plot.Bottom, mark.Position, tickEnd, palette.Grid, width);

                if (!mark.Labeled)
                {
                    continue;
                }

                // 标签一律直立落位（不转画布），保证 SVG/PDF 里仍是可检索文本（D20）。
                p.TextInBox(layout.ClockText(mark.Value),
                    new SKRect(mark.Position - 24f, tickEnd, mark.Position + 24f, layout.TimeAxisRect.Bottom),
                    labelKey, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }
        }

        private static void DrawTrains(Painter p, TrainGraphLayout layout, TrainGraphDocument doc)
        {
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey codeKey = new FontKey(p.Style.Text.Family, tokens.TrainLabelSize, true);

            foreach (TrainRun train in doc.Trains)
            {
                IReadOnlyList<SKPoint> points = layout.PointsOf(train);
                if (points.Count == 0)
                {
                    continue;
                }

                SKColor color = train.Color ?? (train.IsUp ? palette.Accent : palette.Secondary);
                p.Polyline(points, color, tokens.TrainStroke, false);

                SKPoint first = points[0];
                p.Text(train.Code, first.X + 2f, first.Y - 3f, codeKey, color);
            }
        }

        private static void DrawFooter(Painter p, TrainGraphLayout layout, TrainGraphDocument doc)
        {
            TrainGraphStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.LegendSize);

            string text = "图例：上行运行线（红）／下行运行线（蓝）／天窗斜纹（灰，未校准线宽）";
            if (doc.Windows.Count > 0)
            {
                text += "　天窗：" + doc.Windows[0].Label;
                for (int i = 1; i < doc.Windows.Count; i++)
                {
                    text += "、" + doc.Windows[i].Label;
                }
            }

            p.TextInBox(text, layout.FooterRect, key, palette.MutedText, CellAlign.Near, CellAlign.Center, 2f);
        }

        private static void DrawFrame(Painter p, TrainGraphLayout layout)
        {
            TrainGraphStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            SKRect rect = new SKRect(frame.Margin, frame.Margin, frame.Width - frame.Margin,
                frame.Height - frame.Margin);
            p.Frame(rect, tokens.Palette.Ink, frame.BorderStroke);
        }
    }
}
