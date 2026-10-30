using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms.AstReport
{
    /// <summary>药敏报告单的求解结果。</summary>
    public sealed class AstReportLayout
    {
        internal AstReportLayout(SKRect bounds, SKRect headerBand, SKRect categoryBand,
            WeightedCategoryAxis rangeBar, IReadOnlyList<SKRect> rows,
            IReadOnlyList<SKRect> bars, IReadOnlyList<SKRect> labels, IReadOnlyList<SKRect> results,
            SKRect footer)
        {
            Bounds = bounds;
            HeaderBand = headerBand;
            CategoryBand = categoryBand;
            RangeBar = rangeBar;
            RowRects = rows;
            BarRects = bars;
            LabelRects = labels;
            ResultRects = results;
            Footer = footer;
        }

        public SKRect Bounds { get; }
        public SKRect HeaderBand { get; }
        public SKRect CategoryBand { get; }

        /// <summary>
        /// 判读尺：S｜SDD｜I｜R 不等宽段。它是 <see cref="WeightedCategoryAxis"/> 的直接实例，
        /// 没有任何 bespoke 轴类。
        /// </summary>
        public WeightedCategoryAxis RangeBar { get; }

        public IReadOnlyList<SKRect> RowRects { get; }
        public IReadOnlyList<SKRect> BarRects { get; }
        public IReadOnlyList<SKRect> LabelRects { get; }
        public IReadOnlyList<SKRect> ResultRects { get; }
        public SKRect Footer { get; }

        /// <summary>第 row 行、第 segment 段在 RangeBar 上的矩形。</summary>
        public SKRect SegmentRect(int row, int segment)
        {
            SKRect bar = BarRects[row];
            return RangeBar.BandOf(segment, bar.Top, bar.Bottom);
        }
    }

    /// <summary>
    /// 药敏报告单 medical.ast-report。
    /// <para>
    /// RangeBar（S｜SDD｜I｜R）直接由 <see cref="WeightedCategoryAxis"/> 承载，
    /// 零新增轴类；段宽由权重令牌决定。
    /// </para>
    /// </summary>
    public sealed class AstReportTemplate : IChartTemplate
    {
        public string Key => "medical.ast-report";
        public string DisplayName => "药敏报告单";

        /// <summary>表单专属令牌；改令牌即改版面。</summary>
        public AstReportStyle Tokens { get; set; } = new AstReportStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            AstReportDocument doc = Cast(document);
            AstReportStyle t = Tokens;

            float width = t.PaperWidth;
            float y = 0f;
            float hospitalBottom = y + t.HospitalRowHeight;
            float titleBottom = hospitalBottom + t.TitleRowHeight;
            int patientRows = Math.Max(1, Primitives.HeaderFieldRowCount(
                fonts, doc.Patient.EnumerateAll(), style, width));
            float patientBottom = titleBottom + patientRows * style.Header.PatientRowHeight;
            SKRect headerBand = new SKRect(0f, 0f, width, patientBottom);
            y = patientBottom;

            float barLeft = t.Margin + t.BarLabelWidth;
            float barRight = width - t.Margin - t.ResultWidth;
            float categoryHeight = t.CategoryBandHeight;
            SKRect categoryBand = new SKRect(barLeft, y, barRight, y + categoryHeight);
            y = categoryBand.Bottom + 4f;

            List<CategoryStop> stops = new List<CategoryStop>
            {
                new CategoryStop("S", "S", t.SusceptibleWeight),
                new CategoryStop("SDD", "SDD", t.DoseDependentWeight),
                new CategoryStop("I", "I", t.IntermediateWeight),
                new CategoryStop("R", "R", t.ResistantWeight),
            };
            WeightedCategoryAxis rangeBar = new WeightedCategoryAxis(barLeft, barRight, stops,
                AxisDirection.HorizontalRight);

            int rowCount = Math.Max(1, doc.Antibiotics.Count);
            List<SKRect> rows = new List<SKRect>(rowCount);
            List<SKRect> bars = new List<SKRect>(rowCount);
            List<SKRect> labels = new List<SKRect>(rowCount);
            List<SKRect> results = new List<SKRect>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                float top = y + i * t.RowHeight;
                SKRect row = new SKRect(t.Margin, top, width - t.Margin, top + t.RowHeight);
                rows.Add(row);
                bars.Add(new SKRect(barLeft, row.MidY - t.BarHeight / 2f,
                    barRight, row.MidY + t.BarHeight / 2f));
                labels.Add(new SKRect(t.Margin, top, barLeft - 6f, row.Bottom));
                results.Add(new SKRect(barRight + 6f, top, width - t.Margin, row.Bottom));
            }
            y += rowCount * t.RowHeight;

            SKRect footer = new SKRect(0f, y, width, y + t.FooterHeight);

            AstReportLayout layout = new AstReportLayout(
                new SKRect(0f, 0f, width, footer.Bottom), headerBand, categoryBand,
                rangeBar, rows, bars, labels, results, footer);

            return ChartGeometry.ForLayout(layout.Bounds, layout.CategoryBand, layout);
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            AstReportDocument doc = Cast(document);
            AstReportLayout layout = geometry.LayoutOf<AstReportLayout>()
                ?? throw new InvalidOperationException("几何里没有 AstReportLayout。");

            ChartStyle style = painter.Style;
            AstReportStyle t = Tokens;
            SKColor line = style.Colors.Grid;

            DrawHeader(painter, layout, doc, style, t);
            DrawRangeScale(painter, layout, style, t, line);

            FontKey labelKey = new FontKey(style.Text.Family, style.Text.CellLabelSize);
            FontKey valueKey = new FontKey(style.Text.Family, style.Text.CellValueSize);
            for (int i = 0; i < layout.RowRects.Count; i++)
            {
                SKRect row = layout.RowRects[i];
                painter.Line(row.Left, row.Bottom, row.Right, row.Bottom, line, style.Grid.MinorStroke);

                if (i < doc.Antibiotics.Count)
                {
                    AstAntibiotic item = doc.Antibiotics[i];
                    painter.TextInBox(item.Name, layout.LabelRects[i], labelKey, style.Colors.Text,
                        CellAlign.Near, CellAlign.Center, 2f);
                    DrawBar(painter, layout, i, item.Category, style, t, line);
                    painter.TextInBox("MIC " + item.Mic + "  " + item.Category, layout.ResultRects[i],
                        valueKey, style.Colors.CellValue, CellAlign.Near, CellAlign.Center, 2f);
                }
            }

            painter.TextInBox("判读：S 敏感｜SDD 剂量依赖敏感｜I 中介｜R 耐药（WS/T 639—2018 §3.3.4）",
                layout.Footer, new FontKey(style.Text.Family, style.Text.AnnotationSize),
                style.Colors.Text, CellAlign.Near, CellAlign.Center, t.Margin);

            painter.Line(0f, layout.Footer.Top, layout.Bounds.Right, layout.Footer.Top,
                line, style.Grid.BorderStroke);
            painter.Line(0f, layout.Bounds.Top, 0f, layout.Bounds.Bottom, line, style.Grid.BorderStroke);
            painter.Line(layout.Bounds.Right, layout.Bounds.Top, layout.Bounds.Right, layout.Bounds.Bottom,
                line, style.Grid.BorderStroke);
            painter.Line(0f, layout.Bounds.Bottom, layout.Bounds.Right, layout.Bounds.Bottom,
                line, style.Grid.BorderStroke);
        }

        private static void DrawHeader(Painter p, AstReportLayout layout, AstReportDocument doc,
            ChartStyle style, AstReportStyle t)
        {
            float hospitalBottom = layout.HeaderBand.Top + t.HospitalRowHeight;
            float titleBottom = hospitalBottom + t.TitleRowHeight;
            p.TextInBox(doc.HospitalName ?? string.Empty,
                new SKRect(0f, layout.HeaderBand.Top, layout.Bounds.Right, hospitalBottom),
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox(doc.FormTitle ?? "药敏报告单",
                new SKRect(0f, hospitalBottom, layout.Bounds.Right, titleBottom),
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            Primitives.DrawHeaderFields(p,
                new SKRect(0f, titleBottom, layout.Bounds.Right, layout.HeaderBand.Bottom),
                doc.Patient.EnumerateAll(), style.Colors.Text);

            string organism = "标本：" + (doc.Specimen ?? string.Empty)
                              + "　检出菌：" + (doc.Organism ?? string.Empty);
            p.TextInBox(organism,
                new SKRect(0f, titleBottom, layout.Bounds.Right, layout.HeaderBand.Bottom),
                new FontKey(style.Text.Family, style.Text.HeaderSize),
                style.Colors.CellValue, CellAlign.Far, CellAlign.Center, 8f);
        }

        private static void DrawRangeScale(Painter p, AstReportLayout layout, ChartStyle style,
            AstReportStyle t, SKColor line)
        {
            FontKey key = new FontKey(style.Text.Family, style.Text.CellLabelSize);
            SKRect band = layout.CategoryBand;
            for (int i = 0; i < layout.RangeBar.Count; i++)
            {
                SKRect cell = layout.RangeBar.BandOf(i, band.Top, band.Bottom);
                p.FillRect(cell, t.ColorOf(layout.RangeBar.KeyOf(i)));
                p.Frame(cell, line, style.Grid.MinorStroke);
                p.TextInBox(layout.RangeBar.LabelOf(i), cell,
                    new FontKey(style.Text.Family, style.Text.CellLabelSize, true),
                    SKColors.White, CellAlign.Center, CellAlign.Center, 0f);
            }

            p.TextInBox("药敏判读尺（RangeBar）",
                new SKRect(0f, band.Top, band.Left - 6f, band.Bottom), key, style.Colors.Text,
                CellAlign.Far, CellAlign.Center, 0f);
        }

        private static void DrawBar(Painter p, AstReportLayout layout, int row, string category,
            ChartStyle style, AstReportStyle t, SKColor line)
        {
            int selected = layout.RangeBar.IndexOf(category);
            for (int i = 0; i < layout.RangeBar.Count; i++)
            {
                SKRect cell = layout.SegmentRect(row, i);
                if (i == selected)
                {
                    p.FillRect(cell, t.ColorOf(category));
                }
                p.Frame(cell, line, style.Grid.MinorStroke);
            }

            if (selected >= 0)
            {
                p.TextInBox(category, layout.SegmentRect(row, selected),
                    new FontKey(style.Text.Family, style.Text.CellLabelSize, true),
                    SKColors.White, CellAlign.Center, CellAlign.Center, 0f);
            }
        }

        private static AstReportDocument Cast(ChartDocument document)
        {
            AstReportDocument? typed = document as AstReportDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(AstReportDocument) + "。",
                    nameof(document));
            }
            return typed;
        }
    }
}
