using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Temperature
{
    /// <summary>
    /// 三测单（体温单）模板。
    /// 版面尺寸默认值全部实测自参考图：槽宽 20、细格高 15、粗线每 75px（1 ℃）、
    /// 附属行 30px、按 6 槽合并成日列、刻度标签列 40+40、最左空白条 24。
    /// </summary>
    public sealed class TemperatureChartTemplate : IChartTemplate
    {
        public const string AxisTemperature = "temperature";
        public const string AxisPulse = "pulse";

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

        public string Key => "medical.temperature-chart";
        public string DisplayName => "三测单（体温单）";

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            TemperatureChartDocument doc = Cast(document);
            TemperatureScale scale = doc.Scale;
            GridStyle grid = style.Grid;

            int slotsPerGroup = grid.SlotsPerGroup > 0 ? grid.SlotsPerGroup : 6;
            int groups = Math.Max(1, doc.Days.Count);

            float right = grid.LeftStripWidth + grid.FirstLabelWidth + grid.SecondLabelWidth
                          + groups * slotsPerGroup * grid.SlotWidth;

            SlotAxis slots = new SlotAxis(
                grid.LeftStripWidth + grid.FirstLabelWidth + grid.SecondLabelWidth,
                grid.SlotWidth, slotsPerGroup, groups);

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

            // ---- 网格带：体温轴决定所有横线位置 ----
            int cells = (int)Math.Round((scale.TopCelsius - scale.BottomCelsius) / scale.MinorStepCelsius);
            if (cells <= 0)
            {
                throw new InvalidOperationException("体温刻度区间无效。");
            }

            float gridTop = dateBand.Rect.Bottom;
            float gridBottom = gridTop + cells * grid.CellHeight;

            LinearAxis temperatureAxis = new LinearAxis(
                AxisTemperature, scale.BottomCelsius, scale.TopCelsius,
                gridTop, gridBottom, scale.MinorStepCelsius, scale.MajorStepCelsius);

            // 脉搏轴与体温轴共用像素区间：160 次/分压在 41 ℃ 线上，40 次/分压在 35 ℃ 线上。
            LinearAxis pulseAxis = new LinearAxis(
                AxisPulse, scale.PulseAtLabelFrom, scale.PulseAtLabelTo,
                temperatureAxis.YOf(scale.LabelToCelsius), temperatureAxis.YOf(scale.LabelFromCelsius),
                (scale.PulseAtLabelTo - scale.PulseAtLabelFrom) /
                    ((scale.LabelToCelsius - scale.LabelFromCelsius) * (scale.MajorStepCelsius / scale.MinorStepCelsius)),
                (scale.PulseAtLabelTo - scale.PulseAtLabelFrom) / (scale.LabelToCelsius - scale.LabelFromCelsius));

            SKRect gridRect = new SKRect(0f, gridTop, right, gridBottom);

            List<LabelColumn> labelColumns = new List<LabelColumn>
            {
                new LabelColumn("pulse", AxisPulse,
                    new SKRect(grid.LeftStripWidth, gridTop, grid.LeftStripWidth + grid.FirstLabelWidth, gridBottom),
                    "0", scale.PulseAtLabelFrom, scale.PulseAtLabelTo),
                new LabelColumn("temperature", AxisTemperature,
                    new SKRect(grid.LeftStripWidth + grid.FirstLabelWidth, gridTop, slots.Left, gridBottom),
                    "0", scale.LabelFromCelsius, scale.LabelToCelsius),
            };

            // ---- 附属表格带 ----
            float tableTop = gridBottom;
            List<BandRow> tableRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();

            AddRow(TemperatureFields.Respiration, true, grid.CellHeight, ref tableTop, right, slots, tableRows, cellRows);
            foreach (string key in doc.RowOrder)
            {
                AddRow(key, false, grid.CellRowHeight, ref tableTop, right, slots, tableRows, cellRows);
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
                new[] { pulseAxis },
                header, dateBand, gridRect, labelColumns,
                cellTable, cellRows, footer);
        }

        private static void AddRow(string key, bool splitBySlot, float height, ref float y, float right,
            SlotAxis slots, List<BandRow> tableRows, List<CellRow> cellRows)
        {
            SKRect rect = new SKRect(slots.Left, y, right, y + height);
            tableRows.Add(new BandRow(key, new SKRect(0f, y, right, y + height)));
            cellRows.Add(new CellRow(key, TemperatureFields.DefaultLabel(key), splitBySlot, rect));
            y += height;
        }

        private static SKRect Row(ref float y, float height, float right)
        {
            SKRect rect = new SKRect(0f, y, right, y + height);
            y += height;
            return rect;
        }

        private static TemperatureChartDocument Cast(ChartDocument document)
        {
            TemperatureChartDocument? typed = document as TemperatureChartDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(TemperatureChartDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            TemperatureChartDocument doc = Cast(document);
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;

            DrawHeader(p, geo, doc);
            DrawDateBand(p, geo, doc);
            Primitives.DrawRuleLines(p, geo, line);
            Primitives.DrawSlotLines(p, geo, line, false);
            Primitives.DrawLabelColumns(p, geo, line, style.Colors.Text);
            DrawLabelColumnRules(p, geo, line);
            DrawCellTable(p, geo, doc, line);
            DrawEvents(p, geo, doc);
            DrawSeries(p, geo, doc);
            DrawLegend(p, geo, doc);
            DrawFrame(p, geo, line);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, TemperatureChartDocument doc)
        {
            ChartStyle style = p.Style;
            ColorPalette colors = style.Colors;

            Band header = geo.Header;
            SKRect hospital = header.RowRect(RowHospital);
            SKRect title = header.RowRect(RowTitle);
            SKRect patient = header.RowRect(RowPatient);

            p.TextInBox(NullToEmpty(doc.HospitalName), hospital,
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            p.TextInBox(NullToEmpty(doc.FormTitle), title,
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.HeaderRightNote))
            {
                p.TextInBox(doc.HeaderRightNote!, title,
                    new FontKey(style.Text.Family, style.Text.HeaderSize),
                    colors.Text, CellAlign.Far, CellAlign.Center, 4f);
            }

            // 患者信息栏：按实测宽度自动分行、行内两端对齐。
            Primitives.DrawHeaderFields(p, patient, doc.Patient.EnumerateAll(), colors.Text);
        }

        private static void DrawDateBand(Painter p, ChartGeometry geo, TemperatureChartDocument doc)
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

            // 左侧角标
            SKRect corner = new SKRect(style.Grid.LeftStripWidth, 0f, geo.Slots.Left, 0f);
            DrawCornerLabel(p, dateRow, corner, "日期", dateKey, colors.Text);
            DrawCornerLabel(p, weekRow, corner, "星期", dateKey, colors.Text);
            DrawCornerLabel(p, timeRow, corner, "时间", dateKey, colors.Text);

            int slotsPerGroup = geo.Slots.SlotsPerGroup;
            for (int g = 0; g < doc.Days.Count && g < geo.Slots.GroupCount; g++)
            {
                ChartGroup day = doc.Days[g];
                SKRect group = geo.Slots.GroupRect(g, dateRow.Top, dateRow.Bottom);
                SKRect week = geo.Slots.GroupRect(g, weekRow.Top, weekRow.Bottom);

                string dateText = day.DateText ?? day.Date.ToString(doc.DateFormat,
                    System.Globalization.CultureInfo.InvariantCulture);
                string weekText = day.WeekText ?? doc.WeekNames[(int)day.Date.DayOfWeek % 7];

                p.TextInBox(dateText, group, dateKey, colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                p.TextInBox(weekText, week, dateKey, colors.Text, CellAlign.Center, CellAlign.Center, 0f);

                IList<string>? overrideLabels = day.TimeLabels;
                for (int s = 0; s < slotsPerGroup; s++)
                {
                    int slot = g * slotsPerGroup + s;
                    SKRect cell = geo.Slots.SlotRect(slot, timeRow.Top, timeRow.Bottom);
                    string label = overrideLabels != null && s < overrideLabels.Count
                        ? overrideLabels[s]
                        : (s < doc.TimeLabels.Count ? doc.TimeLabels[s] : string.Empty);
                    p.TextInBox(label, cell, timeKey, colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }

            // 表头带内的分隔线
            p.Line(0f, dateRow.Bottom, geo.Bounds.Right, dateRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, weekRow.Bottom, geo.Bounds.Right, weekRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, timeRow.Bottom, geo.Bounds.Right, timeRow.Bottom, line, style.Grid.MajorStroke);
        }

        private static void DrawCornerLabel(Painter p, SKRect row, SKRect corner, string text,
            FontKey key, SKColor color)
        {
            SKRect box = new SKRect(corner.Left, row.Top, corner.Right, row.Bottom);
            p.TextInBox(text, box, key, color, CellAlign.Center, CellAlign.Center, 0f);
        }

        /// <summary>画刻度标签列自身的竖分隔线（x=0 / 24 / 64）。</summary>
        private static void DrawLabelColumnRules(Painter p, ChartGeometry geo, SKColor line)
        {
            ChartStyle style = p.Style;
            float top = geo.DateBand.Rect.Top;
            float bottom = geo.CellTable.Rect.Bottom;

            p.Line(0f, top, 0f, bottom, line, style.Grid.MinorStroke);
            p.Line(style.Grid.LeftStripWidth, top, style.Grid.LeftStripWidth, bottom, line, style.Grid.MinorStroke);
            p.Line(geo.Slots.Left, top, geo.Slots.Left, bottom, line, style.Grid.MinorStroke);

            // x=64 只存在于网格带内（体温标签列与脉搏标签列的分界）
            float x = style.Grid.LeftStripWidth + style.Grid.FirstLabelWidth;
            p.Line(x, geo.GridRect.Top, x, geo.GridRect.Bottom, line, style.Grid.MinorStroke);
        }

        private static void DrawCellTable(Painter p, ChartGeometry geo, TemperatureChartDocument doc, SKColor line)
        {
            ChartStyle style = p.Style;
            float right = geo.Bounds.Right;

            foreach (CellRow row in geo.CellRows)
            {
                p.Line(0f, row.Rect.Bottom, right, row.Rect.Bottom, line, style.Grid.MinorStroke);

                SKRect label = new SKRect(style.Grid.LeftStripWidth, row.Rect.Top, geo.Slots.Left, row.Rect.Bottom);
                p.TextInBox(row.Label, label, new FontKey(style.Text.Family, style.Text.CellLabelSize),
                    style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

                if (row.SplitBySlot)
                {
                    for (int slot = 0; slot <= geo.Slots.TotalSlots; slot++)
                    {
                        float x = geo.Slots.SlotX(slot);
                        p.Line(x, row.Rect.Top, x, row.Rect.Bottom, line, style.Grid.MinorStroke);
                    }
                }
                else
                {
                    for (int g = 0; g <= geo.Slots.GroupCount; g++)
                    {
                        float x = geo.Slots.GroupX(g);
                        p.Line(x, row.Rect.Top, x, row.Rect.Bottom, line, style.Grid.MinorStroke);
                    }
                }
            }

            Primitives.DrawCells(p, geo, doc.Cells, style.Colors.CellValue);
        }

        private static void DrawEvents(Painter p, ChartGeometry geo, TemperatureChartDocument doc)
        {
            if (doc.Events.Count == 0)
            {
                return;
            }

            ChartStyle style = p.Style;
            TemperatureScale scale = doc.Scale;
            float top = geo.YOf(AxisTemperature, scale.EventBandTopCelsius);
            float bottom = geo.GridRect.Bottom;
            SKColor red = style.Colors.Event;

            foreach (EventMark mark in doc.Events)
            {
                if (mark.Slot < 0 || mark.Slot >= geo.Slots.TotalSlots)
                {
                    continue;
                }

                float left = geo.Slots.SlotX(mark.Slot);
                if (mark.DrawSeparator)
                {
                    p.Line(left, top, left, bottom, red, style.EventBand.Stroke);
                }

                float centerX = geo.Slots.SlotCenterX(mark.Slot);
                FontKey key = new FontKey(style.Text.Family, mark.Size ?? style.Text.EventSize);
                SKColor color = mark.Color ?? red;

                if (mark.Orientation == EventMarkOrientation.Horizontal)
                {
                    SKRect box = new SKRect(left, top, left + geo.Slots.GroupWidth, bottom);
                    p.TextInBox(mark.Text, box, key, color, CellAlign.Center, CellAlign.Center, 0f);
                }
                else
                {
                    string text = mark.DayCount.HasValue ? mark.Text + mark.DayCount.Value : mark.Text;
                    p.VerticalText(text, centerX, top + style.EventBand.TextInset, key, color,
                        style.EventBand.LineSpacing, bottom - style.EventBand.TextInset);
                }
            }

            p.Line(geo.Slots.Right, top, geo.Slots.Right, bottom, red, style.EventBand.Stroke);
        }

        private static void DrawSeries(Painter p, ChartGeometry geo, TemperatureChartDocument doc)
        {
            foreach (PointSeries series in doc.Series)
            {
                Primitives.DrawSeries(p, geo, series);
            }
        }

        private static void DrawLegend(Painter p, ChartGeometry geo, TemperatureChartDocument doc)
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
