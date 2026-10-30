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

namespace OODD.Charts.Transit.LoadBalance
{
    /// <summary>
    /// 飞机载重平衡图（重心包线 + 载重表）。
    /// <para>
    /// 横轴 = 重心（%MAC），纵轴 = 重量（kg）；包线是 <see cref="EnvelopePolygon"/>
    /// （数据坐标，稠密折线逼近燃油曲线），包线外铺斜纹、包线内留白。
    /// 载重表每一列的中心与图上同一重心值的 x 完全同源（同一个 <see cref="NumericAxis"/>）。
    /// </para>
    /// <para>
    /// 版面数值全部走 <see cref="LoadBalanceStyle"/>，默认值<b>未校准</b>（D22）；
    /// 样例包线是自造数据，不是任何真实机型的限制。
    /// </para>
    /// </summary>
    public sealed class LoadBalanceTemplate : IChartTemplate
    {
        public string Key => "transit.weight-balance";

        public string DisplayName => "飞机载重平衡图";

        /// <summary>版面令牌。</summary>
        public LoadBalanceStyle Tokens { get; set; } = new LoadBalanceStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            LoadBalanceDocument doc = Cast(document);
            LoadBalanceLayout layout = LoadBalanceLayout.Compute(doc, Tokens);
            return ChartGeometry.ForLayout(layout.Bounds, layout.PlotRect, layout);
        }

        private static LoadBalanceDocument Cast(ChartDocument document)
        {
            LoadBalanceDocument? typed = document as LoadBalanceDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(LoadBalanceDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            LoadBalanceDocument doc = Cast(document);
            LoadBalanceLayout layout = geometry.LayoutOf<LoadBalanceLayout>()
                                      ?? LoadBalanceLayout.Compute(doc, Tokens);
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;

            painter.FillRect(layout.Bounds, palette.PageBackground);

            DrawTitle(painter, layout, doc);
            DrawEnvelopeField(painter, layout);
            DrawGrid(painter, layout);
            DrawEnvelopes(painter, layout, doc);
            DrawAxes(painter, layout, doc);
            DrawCheckPoints(painter, layout, doc);
            DrawTable(painter, layout, doc);
            DrawFooter(painter, layout, doc);
            DrawFrame(painter, layout);
        }

        private static void DrawTitle(Painter p, LoadBalanceLayout layout, LoadBalanceDocument doc)
        {
            ChartStyle style = p.Style;
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            TransitStyle frame = tokens.Frame;

            string title = string.IsNullOrEmpty(doc.FormTitle) ? "飞机载重平衡图" : doc.FormTitle!;
            p.TextInBox(title, layout.TitleRect,
                new FontKey(style.Text.Family, frame.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(doc.AircraftId))
            {
                parts.Add(doc.AircraftId!);
            }
            parts.Add("空机 " + LoadBalanceLayout.Number(doc.EmptyWeight, frame.NumberFormat) + " kg"
                      + "／燃油 " + LoadBalanceLayout.Number(doc.FuelKg, frame.NumberFormat) + " kg");
            parts.Add("总重 " + LoadBalanceLayout.Number(doc.TotalWeightKg, frame.NumberFormat) + " kg"
                      + "／重心 " + LoadBalanceLayout.Number(doc.CgPercentMacOf(), "0.00") + " %MAC");

            p.TextInBox(string.Join("　", parts.ToArray()), layout.NoteRect,
                new FontKey(style.Text.Family, frame.SubtitleSize),
                palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);
        }

        /// <summary>包线外斜纹：先铺满绘图区，随后包线内用白色填掉。</summary>
        private static void DrawEnvelopeField(Painter p, LoadBalanceLayout layout)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            PatternFill hatch = PatternFill.Of(PatternKind.Diagonal, tokens.HatchSpacingMm,
                tokens.HatchStrokeMm, palette.Maintenance);

            SKRect plot = layout.PlotRect;
            using (SKPath field = p.BuildPath(new[]
            {
                new SKPoint(plot.Left, plot.Top),
                new SKPoint(plot.Right, plot.Top),
                new SKPoint(plot.Right, plot.Bottom),
                new SKPoint(plot.Left, plot.Bottom),
            }, true))
            {
                p.FillPathWithPattern(field, hatch);
            }
        }

        private static void DrawGrid(Painter p, LoadBalanceLayout layout)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            SKRect plot = layout.PlotRect;

            foreach (AxisMark mark in layout.CgMarks)
            {
                p.Line(mark.Position, plot.Top, mark.Position, plot.Bottom, palette.Grid, 0.5f);
            }
            foreach (AxisMark mark in layout.WeightMarks)
            {
                p.Line(plot.Left, mark.Position, plot.Right, mark.Position, palette.Grid, 0.5f);
            }

            p.Rect(plot, palette.PanelBorder, 0.8f);
        }

        private static void DrawEnvelopes(Painter p, LoadBalanceLayout layout, LoadBalanceDocument doc)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.EnvelopeLabelSize);

            for (int i = 0; i < layout.Envelopes.Count; i++)
            {
                EnvelopePolygon envelope = layout.Envelopes[i];
                SKPoint[] pixels = layout.PixelVerticesOf(envelope);

                p.FillPolygon(pixels, palette.PageBackground);
                using (SKPath outline = p.BuildPath(pixels, true))
                {
                    p.StrokePath(outline, palette.Ink, tokens.EnvelopeStroke);
                }

                if (!string.IsNullOrEmpty(envelope.Label) && pixels.Length > 0)
                {
                    float top = float.MaxValue;
                    float sumX = 0f;
                    for (int v = 0; v < pixels.Length; v++)
                    {
                        if (pixels[v].Y < top)
                        {
                            top = pixels[v].Y;
                        }
                        sumX += pixels[v].X;
                    }
                    float centerX = sumX / pixels.Length;
                    p.TextInBox(envelope.Label, new SKRect(centerX - 60f, top - tokens.EnvelopeLabelSize * 1.6f,
                            centerX + 60f, top - 1f),
                        key, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
                }
            }
        }

        private static void DrawAxes(Painter p, LoadBalanceLayout layout, LoadBalanceDocument doc)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            TransitStyle frame = tokens.Frame;
            SKRect plot = layout.PlotRect;
            FontKey labelKey = new FontKey(p.Style.Text.Family, tokens.AxisLabelSize);
            FontKey titleKey = new FontKey(p.Style.Text.Family, tokens.AxisTitleSize, true);

            foreach (AxisMark mark in layout.CgMarks)
            {
                if (!mark.Labeled)
                {
                    continue;
                }
                p.TextInBox(LoadBalanceLayout.Number(mark.Value, frame.NumberFormat),
                    new SKRect(mark.Position - tokens.AxisWidth * 0.5f, plot.Bottom + 1f,
                        mark.Position + tokens.AxisWidth * 0.5f, plot.Bottom + 1f + tokens.AxisLabelSize * 1.6f),
                    labelKey, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            foreach (AxisMark mark in layout.WeightMarks)
            {
                if (!mark.Labeled)
                {
                    continue;
                }
                p.TextInBox(LoadBalanceLayout.Number(mark.Value, "0"),
                    new SKRect(plot.Left - tokens.AxisWidth + 2f, mark.Position - tokens.AxisLabelSize * 0.9f,
                        plot.Left - 4f, mark.Position + tokens.AxisLabelSize * 0.9f),
                    labelKey, palette.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            float titleTop = plot.Bottom + 2f + tokens.AxisLabelSize * 1.8f;
            p.TextInBox("重心（%MAC）", new SKRect(plot.Left, titleTop, plot.Right, titleTop + tokens.AxisTitleSize * 1.6f),
                titleKey, palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("重量（kg）", layout.WeightAxisTitleRect, titleKey, palette.SubtitleText,
                CellAlign.Near, CellAlign.Center, 2f);
        }

        private static void DrawCheckPoints(Painter p, LoadBalanceLayout layout, LoadBalanceDocument doc)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.PointLabelSize);

            foreach (CgCheckPoint point in doc.CheckPoints)
            {
                SKPoint pixel = layout.PixelOf(point.CgPercentMac, point.WeightKg);
                bool inside = layout.IsInside(point.CgPercentMac, point.WeightKg);
                SKColor color = inside ? palette.Secondary : palette.Accent;

                p.FillCircle(pixel, tokens.MarkerRadius, color);
                p.Text(point.Label, pixel.X + tokens.MarkerRadius + 2f,
                    pixel.Y - tokens.MarkerRadius - 2f, key, color);
            }
        }

        private static void DrawTable(Painter p, LoadBalanceLayout layout, LoadBalanceDocument doc)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            TransitStyle frame = tokens.Frame;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.TableTextSize);
            FontKey boldKey = new FontKey(p.Style.Text.Family, tokens.TableTextSize, true);
            SKRect table = layout.TableRect;

            p.Rect(table, palette.PanelBorder, 0.8f);
            SKRect header = layout.TableHeaderRect();
            p.FillRect(header, palette.PanelFill);
            p.Line(table.Left, header.Bottom, table.Right, header.Bottom, palette.PanelBorder, 0.8f);
            p.TextInBox("行项目", new SKRect(table.Left + 4f, header.Top, table.Left + 110f, header.Bottom),
                boldKey, palette.Text, CellAlign.Near, CellAlign.Center, 0f);

            int maxRows = (int)Math.Floor((tokens.TableHeight - tokens.TableRowHeight) / (double)tokens.TableRowHeight);
            int rowCount = Math.Min(doc.TableRows.Count, Math.Max(0, maxRows));

            for (int c = 0; c < doc.TableCgValues.Count; c++)
            {
                float center = layout.ColumnCenter(c);
                // 列位引导线从图顶拉到表底：表列中心与图 x 用同一个轴，像素上应是一条直线。
                p.Line(center, layout.PlotRect.Top, center, table.Bottom, palette.Grid, 0.6f);
                p.TextInBox(LoadBalanceLayout.Number(doc.TableCgValues[c], frame.NumberFormat),
                    new SKRect(center - 30f, header.Top, center + 30f, header.Bottom),
                    boldKey, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
                p.Line(center - 30f, header.Bottom,
                    center - 30f, table.Top + tokens.TableRowHeight * (rowCount + 1),
                    palette.PanelBorder, 0.5f);
            }

            for (int r = 0; r < rowCount; r++)
            {
                LoadBalanceTableRow row = doc.TableRows[r];
                SKRect rowRect = layout.TableRowRect(r);
                p.TextInBox(row.Label, new SKRect(table.Left + 4f, rowRect.Top, table.Left + 110f, rowRect.Bottom),
                    key, palette.Text, CellAlign.Near, CellAlign.Center, 0f);

                for (int c = 0; c < doc.TableCgValues.Count && c < row.Values.Count; c++)
                {
                    p.TextInBox(LoadBalanceLayout.Number(row.Values[c], frame.NumberFormat),
                        layout.TableCellRect(r, c), key, palette.Text,
                        CellAlign.Center, CellAlign.Center, 0f);
                }
            }
        }

        private static void DrawFooter(Painter p, LoadBalanceLayout layout, LoadBalanceDocument doc)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.Frame.FooterSize);

            string text = "斜纹 = 包线外（超限区）；白底 = 包线内。包线为自造示例数据，非真实机型限制；版面未校准。";
            p.TextInBox(text, layout.FooterRect, key, palette.MutedText, CellAlign.Near, CellAlign.Center, 2f);
        }

        private static void DrawFrame(Painter p, LoadBalanceLayout layout)
        {
            LoadBalanceStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            SKRect rect = new SKRect(frame.Margin, frame.Margin, frame.Width - frame.Margin,
                frame.Height - frame.Margin);
            p.Frame(rect, tokens.Palette.Ink, frame.BorderStroke);
        }
    }
}
