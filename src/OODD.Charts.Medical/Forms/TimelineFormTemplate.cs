using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms
{
    /// <summary>
    /// 通用时间轴单据模板。页面结构与三测单同源：页眉 → 日期/时点带 → 数值网格 →
    /// 附属行 → 页脚；区别是分组、时点和数值区间全部来自文档，可复用于抢救、
    /// 高压氧陪舱、镇痛泵等以“分钟/小时 + 一个主指标”为核心的文书。
    /// </summary>
    public class TimelineFormTemplate : IChartTemplate
    {
        public const string BandHeader = "header";
        public const string BandDate = "date";
        public const string BandCellTable = "cells";
        public const string BandFooter = "footer";

        public const string RowHospital = "hospital";
        public const string RowTitle = "title";
        public const string RowPatient = "patient";
        public const string RowDate = "date";
        public const string RowTime = "time";

        public virtual string Key => "medical.timeline-form";

        public virtual string DisplayName => "通用时间轴单据";

        public TimelineFormStyle Tokens { get; set; } = new TimelineFormStyle();

        private static SKColor ThresholdBackdrop => new SKColor(255, 255, 255, 232);

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            TimelineFormDocument doc = Cast(document);
            TimelineFormStyle tokens = Tokens;
            GridStyle grid = style.Grid;

            int perGroup = doc.SlotsPerGroup;
            int groups = Math.Max(1, doc.Days.Count);
            float left = grid.LeftStripWidth + tokens.LabelWidth;

            SlotAxis slots = new SlotAxis(left, tokens.SlotWidth, perGroup, groups);
            float right = slots.Right;

            float y = 0f;
            List<BandRow> headerRows = new List<BandRow>();
            headerRows.Add(new BandRow(RowHospital, Row(ref y, style.Header.HospitalRowHeight, right)));
            headerRows.Add(new BandRow(RowTitle, Row(ref y, style.Header.TitleRowHeight, right)));
            int patientRows = Math.Max(1, Primitives.HeaderFieldRowCount(
                fonts, doc.Patient.EnumerateAll(), style, right));
            headerRows.Add(new BandRow(RowPatient, Row(ref y, patientRows * style.Header.PatientRowHeight, right)));
            Band header = new Band(BandHeader, new SKRect(0f, 0f, right, y), headerRows);

            List<BandRow> dateRows = new List<BandRow>();
            dateRows.Add(new BandRow(RowDate, Row(ref y, tokens.DateRowHeight, right)));
            dateRows.Add(new BandRow(RowTime, Row(ref y, tokens.TimeRowHeight, right)));
            Band dateBand = new Band(BandDate, new SKRect(0f, header.Rect.Bottom, right, y), dateRows);

            int cells = (int)Math.Round((doc.AxisMax - doc.AxisMin) / doc.MinorStep);
            if (cells <= 0)
            {
                throw new InvalidOperationException("数值轴刻度区间无效。");
            }

            float gridTop = dateBand.Rect.Bottom;
            float gridBottom = gridTop + cells * tokens.GridCellHeight;
            LinearAxis axis = new LinearAxis(doc.AxisKey, doc.AxisMin, doc.AxisMax,
                gridTop, gridBottom, doc.MinorStep, doc.MajorStep);
            SKRect gridRect = new SKRect(0f, gridTop, right, gridBottom);

            List<LabelColumn> labelColumns = new List<LabelColumn>
            {
                new LabelColumn("value", doc.AxisKey,
                    new SKRect(grid.LeftStripWidth, gridTop, slots.Left, gridBottom),
                    doc.AxisFormat, doc.AxisMin, doc.AxisMax),
            };

            float tableTop = gridBottom;
            List<BandRow> tableRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();
            foreach (TimelineRow row in doc.Rows)
            {
                float height = row.Height ?? tokens.CellRowHeight;
                SKRect rect = new SKRect(slots.Left, tableTop, right, tableTop + height);
                tableRows.Add(new BandRow(row.Key, new SKRect(0f, tableTop, right, tableTop + height)));
                cellRows.Add(new CellRow(row.Key, row.Label, row.SplitBySlot, rect));
                tableTop += height;
            }

            Band cellTable = new Band(BandCellTable, new SKRect(0f, gridBottom, right, tableTop), tableRows);
            float footerTop = tableTop;
            float footerBottom = footerTop + style.Footer.Height;
            Band footer = new Band(BandFooter, new SKRect(0f, footerTop, right, footerBottom),
                new List<BandRow> { new BandRow("footer", new SKRect(0f, footerTop, right, footerBottom)) });

            return new ChartGeometry(
                new SKRect(0f, 0f, right, footerBottom),
                slots, axis, new LinearAxis[0],
                header, dateBand, gridRect, labelColumns,
                cellTable, cellRows, footer);
        }

        private static SKRect Row(ref float y, float height, float right)
        {
            SKRect rect = new SKRect(0f, y, right, y + height);
            y += height;
            return rect;
        }

        private static TimelineFormDocument Cast(ChartDocument document)
        {
            TimelineFormDocument? typed = document as TimelineFormDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(TimelineFormDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            TimelineFormDocument doc = Cast(document);
            SKColor line = p.Style.Colors.Grid;

            DrawHeader(p, geo, doc);
            DrawDateBand(p, geo, doc);
            Primitives.DrawRuleLines(p, geo, line);
            Primitives.DrawSlotLines(p, geo, line, false);
            Primitives.DrawLabelColumns(p, geo, line, p.Style.Colors.Text);
            DrawAxisLabel(p, geo, doc);
            DrawCellTable(p, geo, doc, line);
            DrawThresholds(p, geo, doc);
            DrawEvents(p, geo, doc);
            foreach (PointSeries series in doc.Series)
            {
                Primitives.DrawSeries(p, geo, series);
            }
            DrawLegend(p, geo, doc);
            DrawFrame(p, geo, line);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, TimelineFormDocument doc)
        {
            ChartStyle style = p.Style;
            Band header = geo.Header;

            p.TextInBox(NullToEmpty(doc.HospitalName), header.RowRect(RowHospital),
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox(NullToEmpty(doc.FormTitle), header.RowRect(RowTitle),
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            if (!string.IsNullOrEmpty(doc.HeaderRightNote))
            {
                p.TextInBox(doc.HeaderRightNote!, header.RowRect(RowTitle),
                    new FontKey(style.Text.Family, style.Text.HeaderSize),
                    style.Colors.Text, CellAlign.Far, CellAlign.Center, 4f);
            }
            Primitives.DrawHeaderFields(p, header.RowRect(RowPatient), doc.Patient.EnumerateAll(), style.Colors.Text);
        }

        private static void DrawDateBand(Painter p, ChartGeometry geo, TimelineFormDocument doc)
        {
            ChartStyle style = p.Style;
            Band band = geo.DateBand;
            SKColor line = style.Colors.Grid;
            FontKey dateKey = new FontKey(style.Text.Family, style.Text.DateSize);
            FontKey timeKey = new FontKey(style.Text.Family, style.Text.TimeSize);

            SKRect dateRow = band.RowRect(RowDate);
            SKRect timeRow = band.RowRect(RowTime);
            float cornerLeft = style.Grid.LeftStripWidth;
            float cornerRight = geo.Slots.Left;

            p.TextInBox("日期", new SKRect(cornerLeft, dateRow.Top, cornerRight, dateRow.Bottom),
                dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("时点", new SKRect(cornerLeft, timeRow.Top, cornerRight, timeRow.Bottom),
                dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            int perGroup = geo.Slots.SlotsPerGroup;
            for (int g = 0; g < doc.Days.Count && g < geo.Slots.GroupCount; g++)
            {
                ChartGroup group = doc.Days[g];
                string dateText = group.DateText ?? group.Date.ToString(doc.DateFormat,
                    System.Globalization.CultureInfo.InvariantCulture);
                p.TextInBox(dateText, geo.Slots.GroupRect(g, dateRow.Top, dateRow.Bottom),
                    dateKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                p.Line(geo.Slots.GroupX(g), dateRow.Top, geo.Slots.GroupX(g), timeRow.Bottom,
                    line, style.Grid.MinorStroke);

                IList<string>? overrides = group.TimeLabels;
                for (int s = 0; s < perGroup; s++)
                {
                    string label = overrides != null && s < overrides.Count
                        ? overrides[s]
                        : (s < doc.TimeLabels.Count ? doc.TimeLabels[s] : string.Empty);
                    p.TextInBox(label, geo.Slots.SlotRect(g * perGroup + s, timeRow.Top, timeRow.Bottom),
                        timeKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }
            p.Line(geo.Slots.GroupX(doc.Days.Count), dateRow.Top, geo.Slots.GroupX(doc.Days.Count),
                timeRow.Bottom, line, style.Grid.MinorStroke);

            p.Line(0f, dateRow.Bottom, geo.Bounds.Right, dateRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, timeRow.Bottom, geo.Bounds.Right, timeRow.Bottom, line, style.Grid.MajorStroke);
        }

        private void DrawAxisLabel(Painter p, ChartGeometry geo, TimelineFormDocument doc)
        {
            if (string.IsNullOrEmpty(doc.AxisLabel))
            {
                return;
            }
            ChartStyle style = p.Style;
            p.VerticalText(doc.AxisLabel!, style.Grid.LeftStripWidth / 2f, geo.GridRect.Top + 4f,
                new FontKey(style.Text.Family, Tokens.AxisLabelSize), style.Colors.Text,
                1.1f, geo.GridRect.Bottom);
        }

        private static void DrawCellTable(Painter p, ChartGeometry geo, TimelineFormDocument doc, SKColor line)
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

        private void DrawThresholds(Painter p, ChartGeometry geo, TimelineFormDocument doc)
        {
            if (doc.Thresholds.Count == 0)
            {
                return;
            }
            ChartStyle style = p.Style;
            FontKey key = new FontKey(style.Text.Family, style.Text.AnnotationSize);

            foreach (ThresholdMark threshold in doc.Thresholds)
            {
                if (threshold.Value <= doc.AxisMin || threshold.Value >= doc.AxisMax)
                {
                    continue;
                }
                SKColor color = threshold.Color ?? style.Colors.ReferenceLine;
                float y = geo.YOf(doc.AxisKey, threshold.Value);
                p.DashedLine(geo.GridLeft, y, geo.GridRight, y, color, Tokens.ThresholdStroke);
                if (string.IsNullOrEmpty(threshold.Text))
                {
                    continue;
                }
                // 阈值常落在两条粗刻度之间，写进刻度列会压住相邻数字，改成线内侧上方的小字。
                float labelWidth = p.TextWidth(threshold.Text!, key) + 6f;
                SKRect labelBox = new SKRect(geo.GridLeft + 2f, y - style.Text.AnnotationSize - 3f,
                                             geo.GridLeft + 2f + labelWidth, y - 1f);
                // 底下横着网格线，垫一层浅白再写字，避免字被线切碎。
                p.FillRect(labelBox, ThresholdBackdrop);
                p.TextInBox(threshold.Text!, labelBox, key, color, CellAlign.Near, CellAlign.Center, 0f);
            }
        }

        private void DrawEvents(Painter p, ChartGeometry geo, TimelineFormDocument doc)
        {
            if (doc.Events.Count == 0)
            {
                return;
            }
            ChartStyle style = p.Style;
            int index = 0;

            foreach (EventMark mark in doc.Events)
            {
                if (mark.Slot < 0 || mark.Slot >= geo.Slots.TotalSlots)
                {
                    continue;
                }
                SKColor color = mark.Color ?? style.Colors.Highlight;
                float x = geo.SlotCenterX(mark.Slot);
                if (mark.DrawSeparator)
                {
                    p.DashedLine(x, geo.GridRect.Top, x, geo.GridRect.Bottom, color, Tokens.EventStroke);
                }
                if (string.IsNullOrEmpty(mark.Text))
                {
                    continue;
                }
                FontKey key = new FontKey(style.Text.Family, mark.Size ?? Tokens.EventTextSize);
                float width = p.TextWidth(mark.Text, key) + 4f;
                bool rightHalf = mark.Slot >= geo.Slots.TotalSlots / 2;
                // 相邻事件只隔一两个槽时横排文字会互相压住，按奇偶错开两行。
                float rowOffset = index % 2 == 0 ? 0f : key.Size + 3f;
                float top = geo.GridRect.Top + 1f + rowOffset;
                SKRect box = rightHalf
                    ? new SKRect(x - 2f - width, top, x - 2f, top + key.Size + 2f)
                    : new SKRect(x + 2f, top, x + 2f + width, top + key.Size + 2f);
                float shift = 0f;
                if (box.Left < geo.GridLeft)
                {
                    shift = geo.GridLeft - box.Left;
                }
                else if (box.Right > geo.GridRight)
                {
                    shift = geo.GridRight - box.Right;
                }
                if (shift != 0f)
                {
                    box = new SKRect(box.Left + shift, box.Top, box.Right + shift, box.Bottom);
                }
                // 事件字压在网格线上会碎，先垫浅白底再写字。
                p.FillRect(box, ThresholdBackdrop);
                p.TextInBox(mark.Text, box, key, color, CellAlign.Center, CellAlign.Center, 0f);
                index++;
            }
        }

        private void DrawLegend(Painter p, ChartGeometry geo, TimelineFormDocument doc)
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
                if (doc.Events.Count > 0)
                {
                    items.Add(new LegendItem(MarkerSymbol.None, p.Style.Colors.Highlight, "事件"));
                }
                foreach (ThresholdMark threshold in doc.Thresholds)
                {
                    if (!string.IsNullOrEmpty(threshold.Text))
                    {
                        items.Add(new LegendItem(MarkerSymbol.None,
                            threshold.Color ?? p.Style.Colors.ReferenceLine, threshold.Text!));
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
            p.Line(0f, geo.DateBand.Rect.Top, geo.Bounds.Right, geo.DateBand.Rect.Top,
                line, style.Grid.BorderStroke);
            p.Line(geo.Bounds.Right, geo.DateBand.Rect.Top, geo.Bounds.Right, geo.Footer.Rect.Bottom,
                line, style.Grid.BorderStroke);
            p.Line(0f, geo.Footer.Rect.Bottom, geo.Bounds.Right, geo.Footer.Rect.Bottom,
                line, style.Grid.BorderStroke);
            p.Line(style.Grid.LeftStripWidth, geo.DateBand.Rect.Top, style.Grid.LeftStripWidth,
                geo.Footer.Rect.Bottom, line, style.Grid.MinorStroke);
            p.Line(geo.Slots.Left, geo.DateBand.Rect.Top, geo.Slots.Left, geo.Footer.Rect.Bottom,
                line, style.Grid.MinorStroke);
        }

        private static string NullToEmpty(string? value) => value ?? string.Empty;
    }
}
