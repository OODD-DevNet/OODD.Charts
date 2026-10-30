using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Pain
{
    /// <summary>
    /// 疼痛评估记录单（NRS 0–10）。纵轴 1 分一格、2 分一条粗线，
    /// 静息痛与活动痛各一条折线；NRS ≥ 7 画红色虚线阈值，并把重度评分点圈红。
    /// 附属行逐槽细分，每次评估都能单独填写部位、性质、干预与用药。
    /// </summary>
    public sealed class PainChartTemplate : IChartTemplate
    {
        public const string AxisNrs = "nrs";

        public const string BandHeader = "header";
        public const string BandDate = "date";
        public const string BandCellTable = "cell-table";
        public const string BandFooter = "footer";

        public const string RowHospital = "hospital";
        public const string RowTitle = "title";
        public const string RowPatient = "patient";
        public const string RowDate = "date";
        public const string RowWeek = "week";
        public const string RowTime = "time";

        public string Key => "medical.pain-assessment";

        public string DisplayName => "疼痛评估记录单";

        /// <summary>疼痛单专属尺寸令牌。</summary>
        public PainChartStyle Tokens { get; set; } = new PainChartStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            PainChartDocument doc = Cast(document);
            PainScale scale = doc.Scale;
            GridStyle grid = style.Grid;
            PainChartStyle tokens = Tokens;

            int slotsPerGroup = grid.SlotsPerGroup > 0 ? grid.SlotsPerGroup : 6;
            int groups = Math.Max(1, doc.Days.Count);

            float left = grid.LeftStripWidth + grid.FirstLabelWidth;
            SlotAxis slots = new SlotAxis(left, tokens.SlotWidth, slotsPerGroup, groups);
            float right = slots.Right;

            // ---- 页眉带 ----
            float y = 0f;
            List<BandRow> headerRows = new List<BandRow>();
            headerRows.Add(new BandRow(RowHospital, Row(ref y, style.Header.HospitalRowHeight, right)));
            headerRows.Add(new BandRow(RowTitle, Row(ref y, style.Header.TitleRowHeight, right)));
            int patientRows = Math.Max(1, Primitives.HeaderFieldRowCount(
                fonts, doc.Patient.EnumerateAll(), style, right));
            headerRows.Add(new BandRow(RowPatient,
                Row(ref y, patientRows * style.Header.PatientRowHeight, right)));
            Band header = new Band(BandHeader, new SKRect(0f, 0f, right, y), headerRows);

            // ---- 日期表头带 ----
            List<BandRow> dateRows = new List<BandRow>();
            dateRows.Add(new BandRow(RowDate, Row(ref y, style.Header.DateRowHeight, right)));
            dateRows.Add(new BandRow(RowWeek, Row(ref y, style.Header.WeekRowHeight, right)));
            dateRows.Add(new BandRow(RowTime, Row(ref y, style.Header.TimeRowHeight, right)));
            Band dateBand = new Band(BandDate, new SKRect(0f, header.Rect.Bottom, right, y), dateRows);

            // ---- 评分网格带 ----
            int cells = (int)Math.Round((scale.MaxScore - scale.MinScore) / scale.MinorStep);
            if (cells <= 0)
            {
                throw new InvalidOperationException("NRS 评分刻度区间无效。");
            }

            float gridTop = dateBand.Rect.Bottom;
            float gridBottom = gridTop + cells * tokens.ScoreRowHeight;

            LinearAxis nrsAxis = new LinearAxis(
                AxisNrs, scale.MinScore, scale.MaxScore,
                gridTop, gridBottom, scale.MinorStep, scale.MajorStep);

            SKRect gridRect = new SKRect(0f, gridTop, right, gridBottom);

            List<LabelColumn> labelColumns = new List<LabelColumn>
            {
                new LabelColumn("nrs", AxisNrs,
                    new SKRect(grid.LeftStripWidth, gridTop, slots.Left, gridBottom),
                    "0", scale.MinScore, scale.MaxScore),
            };

            // ---- 附属表格带 ----
            float tableTop = gridBottom;
            List<BandRow> tableRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();

            foreach (string key in doc.RowOrder)
            {
                SKRect rect = new SKRect(slots.Left, tableTop, right, tableTop + tokens.CellRowHeight);
                tableRows.Add(new BandRow(key, new SKRect(0f, tableTop, right, tableTop + tokens.CellRowHeight)));
                cellRows.Add(new CellRow(key, PainFields.DefaultLabel(key), true, rect));
                tableTop += tokens.CellRowHeight;
            }

            Band cellTable = new Band(BandCellTable, new SKRect(0f, gridBottom, right, tableTop), tableRows);

            // ---- 页脚带 ----
            float footerTop = tableTop;
            float footerBottom = footerTop + style.Footer.Height;
            Band footer = new Band(BandFooter, new SKRect(0f, footerTop, right, footerBottom),
                new List<BandRow> { new BandRow("legend", new SKRect(0f, footerTop, right, footerBottom)) });

            return new ChartGeometry(
                new SKRect(0f, 0f, right, footerBottom),
                slots, nrsAxis,
                new LinearAxis[0],
                header, dateBand, gridRect, labelColumns,
                cellTable, cellRows, footer);
        }

        private static SKRect Row(ref float y, float height, float right)
        {
            SKRect rect = new SKRect(0f, y, right, y + height);
            y += height;
            return rect;
        }

        private static PainChartDocument Cast(ChartDocument document)
        {
            PainChartDocument? typed = document as PainChartDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(PainChartDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            PainChartDocument doc = Cast(document);
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;

            DrawHeader(p, geo, doc);
            DrawDateBand(p, geo, doc);
            Primitives.DrawRuleLines(p, geo, line);
            Primitives.DrawSlotLines(p, geo, line, false);
            Primitives.DrawLabelColumns(p, geo, line, style.Colors.Text);
            DrawGutterRules(p, geo, line);
            DrawCellTable(p, geo, doc, line);
            DrawSevereLine(p, geo, doc);
            DrawSeries(p, geo, doc);
            DrawLegend(p, geo, doc);
            DrawFrame(p, geo, line);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, PainChartDocument doc)
        {
            ChartStyle style = p.Style;
            ColorPalette colors = style.Colors;
            Band header = geo.Header;

            p.TextInBox(NullToEmpty(doc.HospitalName), header.RowRect(RowHospital),
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            p.TextInBox(NullToEmpty(doc.FormTitle), header.RowRect(RowTitle),
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.HeaderRightNote))
            {
                p.TextInBox(doc.HeaderRightNote!, header.RowRect(RowTitle),
                    new FontKey(style.Text.Family, style.Text.HeaderSize),
                    colors.Text, CellAlign.Far, CellAlign.Center, 4f);
            }

            Primitives.DrawHeaderFields(p, header.RowRect(RowPatient), doc.Patient.EnumerateAll(), colors.Text);
        }

        private static void DrawDateBand(Painter p, ChartGeometry geo, PainChartDocument doc)
        {
            ChartStyle style = p.Style;
            Band band = geo.DateBand;
            SKColor line = style.Colors.Grid;
            ColorPalette colors = style.Colors;
            FontKey dateKey = new FontKey(style.Text.Family, style.Text.DateSize);
            FontKey timeKey = new FontKey(style.Text.Family, style.Text.TimeSize);

            SKRect dateRow = band.RowRect(RowDate);
            SKRect weekRow = band.RowRect(RowWeek);
            SKRect timeRow = band.RowRect(RowTime);

            SKRect corner = new SKRect(style.Grid.LeftStripWidth, 0f, geo.Slots.Left, 0f);
            DrawCornerLabel(p, dateRow, corner, "日期", dateKey, colors.Text);
            DrawCornerLabel(p, weekRow, corner, "星期", dateKey, colors.Text);
            DrawCornerLabel(p, timeRow, corner, "时间", dateKey, colors.Text);

            int perGroup = geo.Slots.SlotsPerGroup;
            for (int g = 0; g < doc.Days.Count && g < geo.Slots.GroupCount; g++)
            {
                ChartGroup day = doc.Days[g];
                p.TextInBox(day.DateText ?? day.Date.ToString(doc.DateFormat,
                        System.Globalization.CultureInfo.InvariantCulture),
                    geo.Slots.GroupRect(g, dateRow.Top, dateRow.Bottom),
                    dateKey, colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                p.TextInBox(day.WeekText ?? doc.WeekNames[(int)day.Date.DayOfWeek % 7],
                    geo.Slots.GroupRect(g, weekRow.Top, weekRow.Bottom),
                    dateKey, colors.Text, CellAlign.Center, CellAlign.Center, 0f);

                IList<string>? overrideLabels = day.TimeLabels;
                for (int s = 0; s < perGroup; s++)
                {
                    int slot = g * perGroup + s;
                    string label = overrideLabels != null && s < overrideLabels.Count
                        ? overrideLabels[s]
                        : (s < doc.TimeLabels.Count ? doc.TimeLabels[s] : string.Empty);
                    p.TextInBox(label, geo.Slots.SlotRect(slot, timeRow.Top, timeRow.Bottom),
                        timeKey, colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }

            p.Line(0f, dateRow.Bottom, geo.Bounds.Right, dateRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, weekRow.Bottom, geo.Bounds.Right, weekRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, timeRow.Bottom, geo.Bounds.Right, timeRow.Bottom, line, style.Grid.MajorStroke);
        }

        private static void DrawCornerLabel(Painter p, SKRect row, SKRect corner, string text,
            FontKey key, SKColor color)
        {
            p.TextInBox(text, new SKRect(corner.Left, row.Top, corner.Right, row.Bottom),
                key, color, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawGutterRules(Painter p, ChartGeometry geo, SKColor line)
        {
            ChartStyle style = p.Style;
            float top = geo.DateBand.Rect.Top;
            float bottom = geo.CellTable.Rect.Bottom;

            p.Line(0f, top, 0f, bottom, line, style.Grid.MinorStroke);
            p.Line(style.Grid.LeftStripWidth, top, style.Grid.LeftStripWidth, bottom, line, style.Grid.MinorStroke);
            p.Line(geo.Slots.Left, top, geo.Slots.Left, bottom, line, style.Grid.MinorStroke);
        }

        private static void DrawCellTable(Painter p, ChartGeometry geo, PainChartDocument doc, SKColor line)
        {
            ChartStyle style = p.Style;
            float right = geo.Bounds.Right;
            FontKey labelKey = new FontKey(style.Text.Family, style.Text.CellLabelSize);

            foreach (CellRow row in geo.CellRows)
            {
                p.Line(0f, row.Rect.Bottom, right, row.Rect.Bottom, line, style.Grid.MinorStroke);
                p.TextInBox(row.Label,
                    new SKRect(style.Grid.LeftStripWidth, row.Rect.Top, geo.Slots.Left, row.Rect.Bottom),
                    labelKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

                for (int slot = 0; slot <= geo.Slots.TotalSlots; slot++)
                {
                    float x = geo.Slots.SlotX(slot);
                    p.Line(x, row.Rect.Top, x, row.Rect.Bottom, line, style.Grid.MinorStroke);
                }
            }

            Primitives.DrawCells(p, geo, doc.Cells, style.Colors.CellValue);
        }

        /// <summary>重度阈值：横虚线 + 刻度列里的红色 7。</summary>
        private void DrawSevereLine(Painter p, ChartGeometry geo, PainChartDocument doc)
        {
            PainScale scale = doc.Scale;
            if (!scale.ShowSevereLine)
            {
                return;
            }
            if (scale.SevereThreshold <= scale.MinScore || scale.SevereThreshold >= scale.MaxScore)
            {
                return;
            }

            ChartStyle style = p.Style;
            SKColor red = style.Colors.Highlight;
            float y = geo.YOf(AxisNrs, scale.SevereThreshold);

            p.DashedLine(geo.GridLeft, y, geo.GridRight, y, red, Tokens.SevereStroke);

            LabelColumn column = geo.LabelColumns[0];
            float half = style.Text.ScaleLabelSize;
            p.TextInBox(Primitives.FormatTick(scale.SevereThreshold, column.Format),
                new SKRect(column.Rect.Left, y - half, column.Rect.Right, y + half),
                new FontKey(style.Text.Family, style.Text.ScaleLabelSize, true),
                red, CellAlign.Center, CellAlign.Center, 0f);
        }

        private void DrawSeries(Painter p, ChartGeometry geo, PainChartDocument doc)
        {
            ChartStyle style = p.Style;

            foreach (PointSeries series in doc.Series)
            {
                Primitives.DrawSeries(p, geo, series);
            }

            // 重度点圈注：画在所有折线之后，避免被后续系列的连线压住。
            SKColor ring = style.Colors.Highlight;
            foreach (PointSeries series in doc.Series)
            {
                float radius = series.MarkerRadius > 0f ? series.MarkerRadius : style.Series.MarkerRadius;
                float rx = radius + Tokens.SevereRingPadding;

                foreach (KeyValuePair<int, double> pair in series.Values)
                {
                    if (pair.Value < doc.Scale.SevereThreshold)
                    {
                        continue;
                    }
                    float x = geo.SlotCenterX(pair.Key);
                    float y = geo.YOf(series.AxisKey, pair.Value) + series.YOffset;
                    p.Ellipse(new SKRect(x - rx, y - rx, x + rx, y + rx), ring, style.Series.MarkerStroke);
                }
            }
        }

        private void DrawLegend(Painter p, ChartGeometry geo, PainChartDocument doc)
        {
            List<LegendItem> items = new List<LegendItem>();
            foreach (LegendItem item in doc.Legend)
            {
                items.Add(item);
            }
            if (items.Count == 0)
            {
                foreach (PointSeries series in doc.Series)
                {
                    if (!string.IsNullOrEmpty(series.LegendText))
                    {
                        items.Add(new LegendItem(series.Symbol, series.Color, series.LegendText!));
                    }
                }
            }
            if (doc.Scale.ShowSevereLine && !string.IsNullOrEmpty(Tokens.SevereLineText))
            {
                items.Add(new LegendItem(MarkerSymbol.None, p.Style.Colors.Highlight, Tokens.SevereLineText));
            }
            if (items.Count == 0)
            {
                return;
            }

            Primitives.DrawLegend(p, geo.Footer.Rect, items, p.Style.Grid.LeftStripWidth + 4f);
        }

        private static void DrawFrame(Painter p, ChartGeometry geo, SKColor line)
        {
            ChartStyle style = p.Style;
            p.Line(0f, geo.DateBand.Rect.Top, geo.Bounds.Right, geo.DateBand.Rect.Top, line, style.Grid.BorderStroke);
            p.Line(geo.Bounds.Right, geo.DateBand.Rect.Top, geo.Bounds.Right, geo.Footer.Rect.Bottom,
                line, style.Grid.BorderStroke);
            p.Line(0f, geo.Footer.Rect.Bottom, geo.Bounds.Right, geo.Footer.Rect.Bottom,
                line, style.Grid.BorderStroke);
        }

        private static string NullToEmpty(string? value) => value ?? string.Empty;
    }
}
