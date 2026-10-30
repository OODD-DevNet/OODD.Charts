using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Transfusion
{
    /// <summary>
    /// 输血反应记录单。横轴为输注开始后的观察时点（默认 8 个半小时点），
    /// 纵轴为体温 36–41 ℃（每 0.2 ℃ 一格、1 ℃ 一条粗线）。
    /// 反应事件在对应时点画红色竖线并置顶标注，38 ℃ 发热阈值画红色虚线。
    /// </summary>
    public sealed class TransfusionChartTemplate : IChartTemplate
    {
        public const string AxisTemperature = "temperature";

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

        public string Key => "medical.transfusion-reaction";

        public string DisplayName => "输血反应记录单";

        public TransfusionChartStyle Tokens { get; set; } = new TransfusionChartStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            TransfusionChartDocument doc = Cast(document);
            TransfusionScale scale = doc.Scale;
            GridStyle grid = style.Grid;
            TransfusionChartStyle tokens = Tokens;

            int slotsPerGroup = doc.TimeLabels.Count > 0 ? doc.TimeLabels.Count : grid.SlotsPerGroup;
            int groups = Math.Max(1, doc.Days.Count);
            float left = grid.LeftStripWidth + tokens.LabelWidth;

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

            // ---- 体温网格带 ----
            int cells = (int)Math.Round((scale.MaxCelsius - scale.MinCelsius) / scale.MinorStepCelsius);
            if (cells <= 0)
            {
                throw new InvalidOperationException("体温刻度区间无效。");
            }

            float gridTop = dateBand.Rect.Bottom;
            float gridBottom = gridTop + cells * tokens.CelsiusRowHeight;

            LinearAxis temperatureAxis = new LinearAxis(
                AxisTemperature, scale.MinCelsius, scale.MaxCelsius,
                gridTop, gridBottom, scale.MinorStepCelsius, scale.MajorStepCelsius);

            SKRect gridRect = new SKRect(0f, gridTop, right, gridBottom);

            List<LabelColumn> labelColumns = new List<LabelColumn>
            {
                new LabelColumn("temperature", AxisTemperature,
                    new SKRect(grid.LeftStripWidth, gridTop, slots.Left, gridBottom),
                    "0.#", scale.MinCelsius, scale.MaxCelsius),
            };

            // ---- 附属表格带 ----
            float tableTop = gridBottom;
            List<BandRow> tableRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();

            foreach (string key in doc.RowOrder)
            {
                SKRect rect = new SKRect(slots.Left, tableTop, right, tableTop + tokens.CellRowHeight);
                tableRows.Add(new BandRow(key, new SKRect(0f, tableTop, right, tableTop + tokens.CellRowHeight)));
                cellRows.Add(new CellRow(key, TransfusionFields.DefaultLabel(key), true, rect));
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
                slots, temperatureAxis,
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

        private static TransfusionChartDocument Cast(ChartDocument document)
        {
            TransfusionChartDocument? typed = document as TransfusionChartDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(TransfusionChartDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            TransfusionChartDocument doc = Cast(document);
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;

            DrawHeader(p, geo, doc);
            DrawDateBand(p, geo, doc);
            Primitives.DrawRuleLines(p, geo, line);
            Primitives.DrawSlotLines(p, geo, line, false);
            Primitives.DrawLabelColumns(p, geo, line, style.Colors.Text);
            DrawGutterRules(p, geo, line);
            DrawCellTable(p, geo, doc, line);
            DrawFeverLine(p, geo, doc);
            DrawEvents(p, geo, doc);
            Primitives.DrawSeries(p, geo, doc.Temperature);
            DrawLegend(p, geo, doc);
            DrawFrame(p, geo, line);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, TransfusionChartDocument doc)
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

        private static void DrawDateBand(Painter p, ChartGeometry geo, TransfusionChartDocument doc)
        {
            ChartStyle style = p.Style;
            Band band = geo.DateBand;
            SKColor line = style.Colors.Grid;
            FontKey dateKey = new FontKey(style.Text.Family, style.Text.DateSize);
            FontKey timeKey = new FontKey(style.Text.Family, style.Text.TimeSize);

            SKRect dateRow = band.RowRect(RowDate);
            SKRect weekRow = band.RowRect(RowWeek);
            SKRect timeRow = band.RowRect(RowTime);
            SKRect corner = new SKRect(style.Grid.LeftStripWidth, 0f, geo.Slots.Left, 0f);

            p.TextInBox("日期", new SKRect(corner.Left, dateRow.Top, corner.Right, dateRow.Bottom),
                dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("星期", new SKRect(corner.Left, weekRow.Top, corner.Right, weekRow.Bottom),
                dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("时间", new SKRect(corner.Left, timeRow.Top, corner.Right, timeRow.Bottom),
                dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            int perGroup = geo.Slots.SlotsPerGroup;
            for (int g = 0; g < doc.Days.Count && g < geo.Slots.GroupCount; g++)
            {
                ChartGroup day = doc.Days[g];
                p.TextInBox(day.DateText ?? day.Date.ToString(doc.DateFormat,
                        System.Globalization.CultureInfo.InvariantCulture),
                    geo.Slots.GroupRect(g, dateRow.Top, dateRow.Bottom),
                    dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                p.TextInBox(day.WeekText ?? doc.WeekNames[(int)day.Date.DayOfWeek % 7],
                    geo.Slots.GroupRect(g, weekRow.Top, weekRow.Bottom),
                    dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

                IList<string>? overrideLabels = day.TimeLabels;
                for (int s = 0; s < perGroup; s++)
                {
                    string label = overrideLabels != null && s < overrideLabels.Count
                        ? overrideLabels[s]
                        : (s < doc.TimeLabels.Count ? doc.TimeLabels[s] : string.Empty);
                    p.TextInBox(label, geo.Slots.SlotRect(g * perGroup + s, timeRow.Top, timeRow.Bottom),
                        timeKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }

            p.Line(0f, dateRow.Bottom, geo.Bounds.Right, dateRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, weekRow.Bottom, geo.Bounds.Right, weekRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, timeRow.Bottom, geo.Bounds.Right, timeRow.Bottom, line, style.Grid.MajorStroke);
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

        private static void DrawCellTable(Painter p, ChartGeometry geo, TransfusionChartDocument doc, SKColor line)
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

        /// <summary>38 ℃ 发热阈值：红色虚线 + 刻度列里的“发热”小字。</summary>
        private void DrawFeverLine(Painter p, ChartGeometry geo, TransfusionChartDocument doc)
        {
            TransfusionScale scale = doc.Scale;
            if (!scale.ShowFeverLine)
            {
                return;
            }
            if (scale.FeverThreshold <= scale.MinCelsius || scale.FeverThreshold >= scale.MaxCelsius)
            {
                return;
            }

            ChartStyle style = p.Style;
            SKColor red = style.Colors.ReferenceLine;
            float y = geo.YOf(AxisTemperature, scale.FeverThreshold);
            p.DashedLine(geo.GridLeft, y, geo.GridRight, y, red, Tokens.FeverStroke);

            if (string.IsNullOrEmpty(Tokens.FeverMarkText))
            {
                return;
            }

            LabelColumn column = geo.LabelColumns[0];
            float top = y + style.Text.ScaleLabelSize + 1f;
            p.TextInBox(Tokens.FeverMarkText,
                new SKRect(column.Rect.Left, top, column.Rect.Right, top + style.Text.AnnotationSize + 4f),
                new FontKey(style.Text.Family, style.Text.AnnotationSize),
                red, CellAlign.Center, CellAlign.Center, 0f);
        }

        private void DrawEvents(Painter p, ChartGeometry geo, TransfusionChartDocument doc)
        {
            if (doc.Events.Count == 0)
            {
                return;
            }

            ChartStyle style = p.Style;
            SKColor red = style.Colors.Highlight;
            float top = geo.GridRect.Top;
            float bottom = geo.GridRect.Bottom;

            foreach (EventMark mark in doc.Events)
            {
                if (mark.Slot < 0 || mark.Slot >= geo.Slots.TotalSlots)
                {
                    continue;
                }

                float x = geo.SlotCenterX(mark.Slot);
                p.DashedLine(x, top, x, bottom, red, Tokens.EventStroke);

                string text = mark.Text;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                FontKey key = new FontKey(style.Text.Family, mark.Size ?? Tokens.EventTextSize);
                float width = p.TextWidth(text, key) + 4f;
                bool rightHalf = mark.Slot >= geo.Slots.TotalSlots / 2;
                SKRect box = rightHalf
                    ? new SKRect(x - 2f - width, top + 1f, x - 2f, top + key.Size + 3f)
                    : new SKRect(x + 2f, top + 1f, x + 2f + width, top + key.Size + 3f);

                p.TextInBox(text, box, key, mark.Color ?? red, CellAlign.Center, CellAlign.Center, 0f);
            }
        }

        private void DrawLegend(Painter p, ChartGeometry geo, TransfusionChartDocument doc)
        {
            List<LegendItem> items = new List<LegendItem>();
            foreach (LegendItem item in doc.Legend)
            {
                items.Add(item);
            }
            if (items.Count == 0)
            {
                if (!string.IsNullOrEmpty(doc.Temperature.LegendText))
                {
                    items.Add(new LegendItem(doc.Temperature.Symbol, doc.Temperature.Color,
                        doc.Temperature.LegendText!));
                }
                if (doc.Events.Count > 0)
                {
                    items.Add(new LegendItem(MarkerSymbol.Cross, p.Style.Colors.Highlight, "反应事件"));
                }
                if (doc.Scale.ShowFeverLine && !string.IsNullOrEmpty(Tokens.FeverLegendText))
                {
                    items.Add(new LegendItem(MarkerSymbol.None, p.Style.Colors.ReferenceLine,
                        Tokens.FeverLegendText));
                }
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
