using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Restraint
{
    /// <summary>
    /// 精神科保护性约束巡视记录单。
    /// 顶部是一条按小时铺开的约束时段条（Gantt），下方逐小时填写约束部位、皮肤、
    /// 末梢血运、定时松解、饮食排泄与巡视者；跨页时约束条按页裁切。
    /// </summary>
    public sealed class RestraintChartTemplate : IChartTemplate
    {
        public const string AxisState = "restraint-status";

        public const string BandHeader = "header";
        public const string BandDate = "date";
        public const string BandCellTable = "cell-table";
        public const string BandFooter = "footer";

        public const string RowHospital = "hospital";
        public const string RowTitle = "title";
        public const string RowPatient = "patient";
        public const string RowDate = "date";
        public const string RowHour = "hour";

        public string Key => "medical.restraint-observation";

        public string DisplayName => "保护性约束巡视记录单";

        public RestraintChartStyle Tokens { get; set; } = new RestraintChartStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            RestraintChartDocument doc = Cast(document);
            GridStyle grid = style.Grid;
            RestraintChartStyle tokens = Tokens;

            int perDay = Math.Max(1, doc.SlotsPerDay);
            int groups = Math.Max(1, doc.Days.Count);
            float left = grid.LeftStripWidth + tokens.LabelWidth;

            SlotAxis slots = new SlotAxis(left, tokens.SlotWidth, perDay, groups);
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
            dateRows.Add(new BandRow(RowDate, Row(ref y, tokens.DateRowHeight, right)));
            dateRows.Add(new BandRow(RowHour, Row(ref y, tokens.HourRowHeight, right)));
            Band dateBand = new Band(BandDate, new SKRect(0f, header.Rect.Bottom, right, y), dateRows);

            // ---- 约束时段条 ----
            float statusTop = dateBand.Rect.Bottom;
            float statusBottom = statusTop + tokens.StatusBandHeight;
            LinearAxis stateAxis = new LinearAxis(AxisState, 0d, 1d, statusTop, statusBottom, 1d, 1d);

            // ---- 附属表格带 ----
            float tableTop = statusBottom;
            List<BandRow> tableRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();

            foreach (string key in doc.RowOrder)
            {
                SKRect rect = new SKRect(slots.Left, tableTop, right, tableTop + tokens.CellRowHeight);
                tableRows.Add(new BandRow(key, new SKRect(0f, tableTop, right, tableTop + tokens.CellRowHeight)));
                cellRows.Add(new CellRow(key, RestraintFields.DefaultLabel(key), true, rect));
                tableTop += tokens.CellRowHeight;
            }

            Band cellTable = new Band(BandCellTable, new SKRect(0f, statusBottom, right, tableTop), tableRows);

            // ---- 页脚带 ----
            float footerTop = tableTop;
            float footerBottom = footerTop + style.Footer.Height;
            Band footer = new Band(BandFooter, new SKRect(0f, footerTop, right, footerBottom),
                new List<BandRow> { new BandRow("legend", new SKRect(0f, footerTop, right, footerBottom)) });

            return new ChartGeometry(
                new SKRect(0f, 0f, right, footerBottom),
                slots, stateAxis,
                new LinearAxis[0],
                header, dateBand,
                new SKRect(0f, statusTop, right, statusBottom),
                new LabelColumn[0],
                cellTable, cellRows, footer);
        }

        private static SKRect Row(ref float y, float height, float right)
        {
            SKRect rect = new SKRect(0f, y, right, y + height);
            y += height;
            return rect;
        }

        private static RestraintChartDocument Cast(ChartDocument document)
        {
            RestraintChartDocument? typed = document as RestraintChartDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(RestraintChartDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            RestraintChartDocument doc = Cast(document);
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;

            DrawHeader(p, geo, doc);
            DrawDateBand(p, geo, doc);
            DrawStatusBand(p, geo, doc, line);
            DrawGutterRules(p, geo, line);
            DrawCellTable(p, geo, doc, line);
            DrawLegend(p, geo, doc);
            DrawFrame(p, geo, line);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, RestraintChartDocument doc)
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

        private void DrawDateBand(Painter p, ChartGeometry geo, RestraintChartDocument doc)
        {
            ChartStyle style = p.Style;
            Band band = geo.DateBand;
            SKColor line = style.Colors.Grid;
            FontKey key = new FontKey(style.Text.Family, style.Text.DateSize);
            FontKey hourKey = new FontKey(style.Text.Family, style.Text.TimeSize);

            SKRect dateRow = band.RowRect(RowDate);
            SKRect hourRow = band.RowRect(RowHour);
            SKRect corner = new SKRect(style.Grid.LeftStripWidth, 0f, geo.Slots.Left, 0f);

            p.TextInBox("日期", new SKRect(corner.Left, dateRow.Top, corner.Right, dateRow.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("时间", new SKRect(corner.Left, hourRow.Top, corner.Right, hourRow.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            int perDay = geo.Slots.SlotsPerGroup;
            for (int g = 0; g < doc.Days.Count && g < geo.Slots.GroupCount; g++)
            {
                ChartGroup day = doc.Days[g];
                p.TextInBox(day.DateText ?? day.Date.ToString(doc.DateFormat,
                        System.Globalization.CultureInfo.InvariantCulture),
                    geo.Slots.GroupRect(g, dateRow.Top, dateRow.Bottom),
                    key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

                for (int h = 0; h < perDay; h++)
                {
                    if (Tokens.HourLabelStep > 1 && h % Tokens.HourLabelStep != 0)
                    {
                        continue;
                    }
                    string label = HourLabel(day, h);
                    p.TextInBox(label, geo.Slots.SlotRect(g * perDay + h, hourRow.Top, hourRow.Bottom),
                        hourKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }

            p.Line(0f, dateRow.Bottom, geo.Bounds.Right, dateRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, hourRow.Bottom, geo.Bounds.Right, hourRow.Bottom, line, style.Grid.MajorStroke);
        }

        private string HourLabel(ChartGroup day, int hour)
        {
            IList<string>? overrideLabels = day.TimeLabels;
            if (overrideLabels != null && hour < overrideLabels.Count)
            {
                return overrideLabels[hour];
            }
            return hour.ToString(Tokens.HourFormat, System.Globalization.CultureInfo.InvariantCulture);
        }

        private void DrawStatusBand(Painter p, ChartGeometry geo, RestraintChartDocument doc, SKColor line)
        {
            ChartStyle style = p.Style;
            SKRect band = geo.GridRect;
            FontKey labelKey = new FontKey(style.Text.Family, style.Text.CellLabelSize);

            // 时段条的行名落在左侧标签列里，与下方巡视行的标签列对齐。
            p.TextInBox(Tokens.StatusLabel,
                new SKRect(style.Grid.LeftStripWidth, band.Top, geo.Slots.Left, band.Bottom),
                labelKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

            p.Line(0f, band.Top, geo.Bounds.Right, band.Top, line, style.Grid.MinorStroke);
            p.Line(0f, band.Bottom, geo.Bounds.Right, band.Bottom, line, style.Grid.MinorStroke);
            for (int slot = 0; slot <= geo.Slots.TotalSlots; slot++)
            {
                float x = geo.Slots.SlotX(slot);
                p.Line(x, band.Top, x, band.Bottom, line, style.Grid.MinorStroke);
            }

            FontKey barKey = new FontKey(style.Text.Family, Tokens.BarTextSize);
            foreach (RestraintEpisode episode in doc.Episodes)
            {
                int start = Math.Max(0, episode.StartSlot);
                int end = Math.Min(geo.Slots.TotalSlots, episode.EndSlot);
                if (end <= start)
                {
                    continue;
                }

                SKRect bar = new SKRect(
                    geo.Slots.SlotX(start), band.Top + Tokens.BarInset,
                    geo.Slots.SlotX(end), band.Bottom - Tokens.BarInset);
                SKColor color = episode.Color ?? (episode.IsEmergency ? style.Colors.Highlight : style.Colors.Block);

                p.FillRect(bar, new SKColor(color.Red, color.Green, color.Blue, 48));
                p.Rect(bar, color, style.Grid.MinorStroke);

                string? text = episode.Text;
                if (!string.IsNullOrEmpty(text) && p.TextWidth(text!, barKey) <= bar.Width - 4f)
                {
                    p.TextInBox(text!, bar, barKey, style.Colors.Text,
                        CellAlign.Center, CellAlign.Center, 0f);
                }
            }
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

        private static void DrawCellTable(Painter p, ChartGeometry geo, RestraintChartDocument doc, SKColor line)
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

        private void DrawLegend(Painter p, ChartGeometry geo, RestraintChartDocument doc)
        {
            if (doc.Legend.Count > 0)
            {
                Primitives.DrawLegend(p, geo.Footer.Rect, doc.Legend, p.Style.Grid.LeftStripWidth + 4f);
                return;
            }

            ChartStyle style = p.Style;
            FontKey key = new FontKey(style.Text.Family, style.Text.LegendSize);
            float x = style.Grid.LeftStripWidth + 4f;
            float centerY = geo.Footer.Rect.MidY;

            DrawSwatch(p, ref x, centerY, style.Colors.Block, "约束时段", key);
            if (HasEmergency(doc))
            {
                DrawSwatch(p, ref x, centerY, style.Colors.Highlight, "紧急约束", key);
            }
        }

        private static bool HasEmergency(RestraintChartDocument doc)
        {
            foreach (RestraintEpisode episode in doc.Episodes)
            {
                if (episode.IsEmergency)
                {
                    return true;
                }
            }
            return false;
        }

        private static void DrawSwatch(Painter p, ref float x, float centerY, SKColor color,
            string text, FontKey key)
        {
            SKRect swatch = new SKRect(x, centerY - 5f, x + 18f, centerY + 5f);
            p.FillRect(swatch, new SKColor(color.Red, color.Green, color.Blue, 48));
            p.Rect(swatch, color, 1f);
            x += 22f;
            p.TextInBox(text, new SKRect(x, swatch.Top - 4f, x + 200f, swatch.Bottom + 4f),
                key, p.Style.Colors.Legend, CellAlign.Near, CellAlign.Center, 0f);
            x += p.TextWidth(text, key) + p.Style.Footer.ItemGap;
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
