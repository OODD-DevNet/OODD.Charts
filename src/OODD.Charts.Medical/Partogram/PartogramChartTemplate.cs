using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Partogram
{
    /// <summary>
    /// 产程图（partogram）模板。
    /// 横轴一槽 = 30 分钟、一组 = 1 小时；纵轴左为宫口扩张 0–10 cm、右为胎先露 S-3…S+3，
    /// 两条轴共用同一段像素区间，因此横线只由宫口轴决定一次。
    /// 默认按国内教科书画法：两轴都自上而下递增，宫口与先露曲线都向右下走。
    /// </summary>
    public sealed class PartogramChartTemplate : IChartTemplate
    {
        public const string BandHeader = "header";
        public const string BandTime = "time";
        public const string BandCellTable = "cell-table";
        public const string BandFooter = "footer";

        public const string RowHospital = "hospital";
        public const string RowTitle = "title";
        public const string RowPatient = "patient";
        public const string RowDate = "date";
        public const string RowHour = "hour";

        public string Key => "medical.partogram";
        public string DisplayName => "产程图";

        /// <summary>
        /// 产程图专属尺寸令牌。改这里即可按院方版式微调，不需要动引擎，也不需要动 ChartStyle。
        /// </summary>
        public PartogramStyle Tokens { get; set; } = new PartogramStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            PartogramDocument doc = Cast(document);
            GridStyle grid = style.Grid;
            PartogramStyle pg = Tokens;
            PartogramScale scale = doc.Scale;

            int slotsPerHour = Math.Max(1, doc.SlotsPerHour);
            int hours = Math.Max(1, doc.Hours);

            // 左侧留白条 + 标签区（宫口刻度与行标签共用，各占不同纵向带）；右侧先露刻度列。
            float left = grid.LeftStripWidth + grid.FirstLabelWidth + grid.SecondLabelWidth;
            SlotAxis slots = new SlotAxis(left, grid.SlotWidth, slotsPerHour, hours);
            float right = slots.Right + grid.SecondLabelWidth;

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

            // ---- 时间带 ----
            List<BandRow> timeRows = new List<BandRow>();
            timeRows.Add(new BandRow(RowDate, Row(ref y, pg.DateRowHeight, right)));
            timeRows.Add(new BandRow(RowHour, Row(ref y, pg.HourRowHeight, right)));
            Band timeBand = new Band(BandTime, new SKRect(0f, header.Rect.Bottom, right, y), timeRows);

            // ---- 网格带 ----
            int cells = (int)Math.Round((scale.DilationMaxCm - scale.DilationMinCm) / scale.DilationMinorCm);
            if (cells <= 0)
            {
                throw new InvalidOperationException("宫口扩张刻度区间无效。");
            }

            float gridTop = timeBand.Rect.Bottom;
            float gridBottom = gridTop + cells * pg.CmRowHeight;
            bool upward = scale.LinearAxisIncreasing;

            LinearAxis dilationAxis = new LinearAxis(
                PartogramDocument.AxisDilation, scale.DilationMinCm, scale.DilationMaxCm,
                gridTop, gridBottom, scale.DilationMinorCm, scale.DilationMajorCm, upward);

            // 先露轴与宫口轴共用像素区间：两端对齐，中间线性内插。
            LinearAxis stationAxis = new LinearAxis(
                PartogramDocument.AxisStation, Math.Min(scale.StationFrom, scale.StationTo),
                Math.Max(scale.StationFrom, scale.StationTo),
                gridTop, gridBottom, scale.StationStep, scale.StationStep, upward);

            SKRect gridRect = new SKRect(0f, gridTop, right, gridBottom);

            List<LabelColumn> labelColumns = new List<LabelColumn>
            {
                new LabelColumn("dilation", PartogramDocument.AxisDilation,
                    new SKRect(grid.LeftStripWidth, gridTop, slots.Left, gridBottom),
                    "0.#", scale.DilationMinCm, scale.DilationMaxCm),
                new LabelColumn("station", PartogramDocument.AxisStation,
                    new SKRect(slots.Right, gridTop, right, gridBottom),
                    "+0;-0", Math.Min(scale.StationFrom, scale.StationTo),
                    Math.Max(scale.StationFrom, scale.StationTo)),
            };

            // ---- 胎心带 + 宫缩带 + 附属表格带 ----
            List<BandRow> tableRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();

            float tableTop = gridBottom;
            tableRows.Add(new BandRow(PartogramFields.FetalHeart,
                new SKRect(0f, tableTop, right, tableTop + pg.FetalHeartRowHeight)));
            tableTop += pg.FetalHeartRowHeight;
            tableRows.Add(new BandRow(PartogramFields.Contraction,
                new SKRect(0f, tableTop, right, tableTop + pg.ContractionRowHeight)));
            tableTop += pg.ContractionRowHeight;

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
                slots, dilationAxis,
                new[] { stationAxis },
                header, timeBand, gridRect, labelColumns,
                cellTable, cellRows, footer);
        }

        private static void AddRow(string key, bool splitBySlot, float height, ref float y, float right,
            SlotAxis slots, List<BandRow> tableRows, List<CellRow> cellRows)
        {
            SKRect rect = new SKRect(slots.Left, y, right, y + height);
            tableRows.Add(new BandRow(key, new SKRect(0f, y, right, y + height)));
            cellRows.Add(new CellRow(key, PartogramFields.DefaultLabel(key), splitBySlot, rect));
            y += height;
        }

        private static SKRect Row(ref float y, float height, float right)
        {
            SKRect rect = new SKRect(0f, y, right, y + height);
            y += height;
            return rect;
        }

        private static PartogramDocument Cast(ChartDocument document)
        {
            PartogramDocument? typed = document as PartogramDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(PartogramDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            PartogramDocument doc = Cast(document);
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;

            DrawHeader(p, geo, doc);
            DrawTimeBand(p, geo, doc);
            Primitives.DrawRuleLines(p, geo, line);
            Primitives.DrawSlotLines(p, geo, line, false);
            DrawHourRules(p, geo, doc, line);
            Primitives.DrawLabelColumns(p, geo, line, style.Colors.Text);
            DrawGutterRules(p, geo, line);
            DrawGuideLines(p, geo, doc);
            Primitives.DrawSeries(p, geo, doc.Cervix);
            Primitives.DrawSeries(p, geo, doc.Station);
            DrawMembrane(p, geo, doc);
            DrawFetalHeart(p, geo, doc);
            DrawContractions(p, geo, doc);
            DrawCellTable(p, geo, doc, line);
            DrawLegend(p, geo, doc);
            DrawFrame(p, geo, line);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, PartogramDocument doc)
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

        private void DrawTimeBand(Painter p, ChartGeometry geo, PartogramDocument doc)
        {
            ChartStyle style = p.Style;
            Band band = geo.DateBand;
            SKColor line = style.Colors.Grid;
            FontKey key = new FontKey(style.Text.Family, style.Text.DateSize);

            SKRect dateRow = band.RowRect(RowDate);
            SKRect hourRow = band.RowRect(RowHour);

            SKRect corner = new SKRect(style.Grid.LeftStripWidth, 0f, geo.Slots.Left, 0f);
            p.TextInBox("日期", new SKRect(corner.Left, dateRow.Top, corner.Right, dateRow.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox("时间", new SKRect(corner.Left, hourRow.Top, corner.Right, hourRow.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            int mergeHours = Math.Max(1, Tokens.DateMergeHours);
            int hours = geo.Slots.GroupCount;
            for (int start = 0; start < hours; start += mergeHours)
            {
                int span = Math.Min(mergeHours, hours - start);
                int firstSlot = start * geo.Slots.SlotsPerGroup;
                int lastSlot = (start + span) * geo.Slots.SlotsPerGroup - 1;
                SKRect box = geo.Slots.SlotRangeRect(firstSlot, lastSlot, dateRow.Top, dateRow.Bottom);
                string text = doc.TimeOfSlot(firstSlot).ToString(doc.DateFormat,
                    System.Globalization.CultureInfo.InvariantCulture);
                p.TextInBox(text, box, key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            for (int hour = 0; hour < hours; hour++)
            {
                int slot = hour * geo.Slots.SlotsPerGroup;
                SKRect box = geo.Slots.GroupRect(hour, hourRow.Top, hourRow.Bottom);
                string text = doc.TimeOfSlot(slot).ToString(doc.HourFormat,
                    System.Globalization.CultureInfo.InvariantCulture);
                p.TextInBox(text, box, key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            p.Line(0f, dateRow.Bottom, geo.Bounds.Right, dateRow.Bottom, line, style.Grid.MinorStroke);
            p.Line(0f, hourRow.Bottom, geo.Bounds.Right, hourRow.Bottom, line, style.Grid.MajorStroke);
        }

        /// <summary>钟点边界加粗；半天与日边界再粗一档。</summary>
        private void DrawHourRules(Painter p, ChartGeometry geo, PartogramDocument doc, SKColor line)
        {
            ChartStyle style = p.Style;
            float top = geo.GridRect.Top;
            float bottom = geo.CellTable.Rect.Bottom;
            int perHour = Math.Max(1, doc.SlotsPerHour);
            int mergeHours = Math.Max(1, Tokens.DateMergeHours);

            for (int slot = 0; slot <= geo.Slots.TotalSlots; slot++)
            {
                if (slot % perHour != 0)
                {
                    continue;
                }
                float x = geo.Slots.SlotX(slot);
                int hour = slot / perHour;
                float width = hour % mergeHours == 0
                    ? style.Grid.MajorStroke
                    : style.Grid.MinorStroke + 0.5f;
                p.Line(x, top, x, bottom, line, width);
            }
        }

        /// <summary>左右两侧留白区与刻度列的分隔线，贯穿全表。</summary>
        private static void DrawGutterRules(Painter p, ChartGeometry geo, SKColor line)
        {
            ChartStyle style = p.Style;
            float top = geo.DateBand.Rect.Top;
            float bottom = geo.CellTable.Rect.Bottom;

            p.Line(0f, top, 0f, bottom, line, style.Grid.MinorStroke);
            p.Line(style.Grid.LeftStripWidth, top, style.Grid.LeftStripWidth, bottom, line, style.Grid.MinorStroke);
            p.Line(geo.Slots.Left, top, geo.Slots.Left, bottom, line, style.Grid.MinorStroke);
            p.Line(geo.Slots.Right, top, geo.Slots.Right, bottom, line, style.Grid.MinorStroke);
        }

        /// <summary>
        /// 警戒线 / 处理线。两者平行，斜率都是 <c>AlertSlopeCmPerHour</c>，
        /// 处理线在警戒线右侧 <c>ActionOffsetHours</c> 小时。
        /// </summary>
        private void DrawGuideLines(Painter p, ChartGeometry geo, PartogramDocument doc)
        {
            if (!doc.ShowGuideLines || doc.AlertSlopeCmPerHour <= 0d)
            {
                return;
            }

            DrawGuide(p, geo, doc, doc.AlertStartHour, p.Style.Colors.ReferenceLine, doc.AlertLineText);
            DrawGuide(p, geo, doc, doc.AlertStartHour + doc.ActionOffsetHours,
                p.Style.Colors.ReferenceLineAlt, doc.ActionLineText);
        }

        private void DrawGuide(Painter p, ChartGeometry geo, PartogramDocument doc,
            double startHour, SKColor color, string label)
        {
            double slope = doc.AlertSlopeCmPerHour;
            PartogramScale scale = doc.Scale;

            // 线从“该小时宫口 = AlertStartCm”这一点起笔画到上限，不回溯到 0 cm；
            // 终点取“到顶”与“横轴右端”的较早者。YOf 自带区间钳制，越界只会贴边。
            double from = Math.Max(0d, startHour);
            double to = Math.Min((double)doc.Hours,
                startHour + (scale.DilationMaxCm - doc.AlertStartCm) / slope);
            if (to <= from)
            {
                return;
            }

            double fromCm = doc.AlertStartCm + (from - startHour) * slope;
            double toCm = doc.AlertStartCm + (to - startHour) * slope;
            SKPoint first = new SKPoint(XOfHour(geo, doc, from),
                geo.YOf(PartogramDocument.AxisDilation, fromCm));
            SKPoint last = new SKPoint(XOfHour(geo, doc, to),
                geo.YOf(PartogramDocument.AxisDilation, toCm));

            p.Line(first.X, first.Y, last.X, last.Y, color, Tokens.GuideStroke);

            ChartStyle style = p.Style;
            FontKey key = new FontKey(style.Text.Family, style.Text.TimeSize);
            SKPoint top = first.Y <= last.Y ? first : last;

            // 标签贴着线的上端、落在网格内侧，避免压到时间行。
            SKRect box = new SKRect(top.X + 2f, top.Y + 1f, top.X + 64f,
                top.Y + style.Text.TimeSize * 2f + 1f);
            p.TextInBox(label, box, key, color, CellAlign.Near, CellAlign.Near, 0f);
        }

        private void DrawMembrane(Painter p, ChartGeometry geo, PartogramDocument doc)
        {
            if (doc.Membrane.Count == 0)
            {
                return;
            }

            ChartStyle style = p.Style;
            SKColor color = style.Colors.Event;
            FontKey key = new FontKey(style.Text.Family, Tokens.MembraneSize);

            foreach (MembraneMark mark in doc.Membrane)
            {
                if (mark.Slot < 0 || mark.Slot >= geo.Slots.TotalSlots)
                {
                    continue;
                }

                double cm = mark.DilationCm ?? doc.Cervix.ValueInterpolatedAt(mark.Slot) ?? double.NaN;
                if (double.IsNaN(cm))
                {
                    continue;
                }

                SKPoint tip = new SKPoint(geo.SlotCenterX(mark.Slot),
                    geo.YOf(PartogramDocument.AxisDilation, cm) + style.Series.MarkerRadius * 2f);
                float arrow = Tokens.MembraneArrow;
                SKPoint tail = new SKPoint(tip.X - arrow, tip.Y - arrow);

                p.Line(tail.X, tail.Y, tip.X, tip.Y, mark.Color ?? color, style.Grid.MinorStroke);
                p.Line(tip.X, tip.Y, tip.X - arrow * 0.45f, tip.Y, mark.Color ?? color, style.Grid.MinorStroke);
                p.Text(mark.Text, tail.X - p.TextWidth(mark.Text, key) - 2f, tail.Y + key.Size / 3f,
                    key, mark.Color ?? color);
            }
        }

        private void DrawFetalHeart(Painter p, ChartGeometry geo, PartogramDocument doc)
        {
            ChartStyle style = p.Style;
            BandRow? row = geo.CellTable.Row(PartogramFields.FetalHeart);
            if (row == null)
            {
                return;
            }

            SKRect band = row.Rect;
            FontKey key = new FontKey(style.Text.Family, style.Text.CellValueSize);
            string format = "0.#";

            foreach (FetalHeartReading reading in doc.FetalHeart)
            {
                if (reading.Slot < 0 || reading.Slot >= geo.Slots.TotalSlots)
                {
                    continue;
                }

                string text = reading.BeatsPerMinute.ToString(format,
                    System.Globalization.CultureInfo.InvariantCulture);
                float centerX = geo.SlotCenterX(reading.Slot);
                float width = p.TextWidth(text, key);
                SKColor color = reading.Color ?? (doc.IsAbnormal(reading) ? style.Colors.Highlight
                                                                          : style.Colors.Text);

                p.TextInBox(text, new SKRect(centerX - width, band.Top, centerX + width, band.Bottom),
                    key, color, CellAlign.Center, CellAlign.Center, 0f);

                if (doc.IsAbnormal(reading))
                {
                    float rx = width / 2f + Tokens.RingPaddingX;
                    float ry = band.Height / 2f - Tokens.RingPaddingY;
                    p.Ellipse(new SKRect(centerX - rx, band.MidY - ry, centerX + rx, band.MidY + ry),
                        color, style.Grid.MinorStroke);
                }
            }
        }

        private void DrawContractions(Painter p, ChartGeometry geo, PartogramDocument doc)
        {
            ChartStyle style = p.Style;
            PartogramStyle pg = Tokens;
            PartogramScale scale = doc.Scale;
            BandRow? row = geo.CellTable.Row(PartogramFields.Contraction);
            if (row == null)
            {
                return;
            }

            SKRect band = row.Rect;
            foreach (ContractionReading reading in doc.Contractions)
            {
                if (reading.Slot < 0 || reading.Slot >= geo.Slots.TotalSlots)
                {
                    continue;
                }

                int count = Math.Min(reading.CountPerTenMinutes, scale.ContractionMaxBlocks);
                if (count <= 0)
                {
                    continue;
                }

                float blockHeight = BlockHeight(pg, scale, reading.DurationSeconds);
                float stack = count * blockHeight + (count - 1) * pg.BlockGap;
                if (stack > band.Height - 2f)
                {
                    stack = band.Height - 2f;
                    blockHeight = (stack - (count - 1) * pg.BlockGap) / count;
                }

                SKRect cell = geo.Slots.SlotRect(reading.Slot, band.Top, band.Bottom);
                float x = cell.MidX - pg.BlockWidth / 2f;
                float y = cell.Bottom - 1f;
                SKColor color = reading.Color ?? style.Colors.Block;

                for (int i = 0; i < count; i++)
                {
                    float top = y - blockHeight;
                    p.FillRect(new SKRect(x, top, x + pg.BlockWidth, y), color);
                    y = top - pg.BlockGap;
                }
            }
        }

        private static float BlockHeight(PartogramStyle pg, PartogramScale scale, double seconds)
        {
            if (seconds <= 0d)
            {
                return pg.BlockHeight;
            }

            double t = (seconds - scale.ContractionShortSeconds)
                       / Math.Max(1e-6, scale.ContractionLongSeconds - scale.ContractionShortSeconds);
            if (t < 0d)
            {
                t = 0d;
            }
            else if (t > 1d)
            {
                t = 1d;
            }

            return (float)(pg.BlockHeight * (0.6d + 1.0d * t));
        }

        private static void DrawCellTable(Painter p, ChartGeometry geo, PartogramDocument doc, SKColor line)
        {
            ChartStyle style = p.Style;
            float right = geo.Bounds.Right;

            foreach (CellRow row in geo.CellRows)
            {
                p.Line(0f, row.Rect.Bottom, right, row.Rect.Bottom, line, style.Grid.MinorStroke);

                SKRect label = new SKRect(style.Grid.LeftStripWidth, row.Rect.Top, geo.Slots.Left, row.Rect.Bottom);
                p.TextInBox(row.Label, label, new FontKey(style.Text.Family, style.Text.CellLabelSize),
                    style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

                for (int g = 0; g <= geo.Slots.GroupCount; g++)
                {
                    float x = geo.Slots.GroupX(g);
                    p.Line(x, row.Rect.Top, x, row.Rect.Bottom, line, style.Grid.MinorStroke);
                }
            }

            // 胎心带与宫缩带的标签同样落在左侧留白里
            foreach (string key in new[] { PartogramFields.FetalHeart, PartogramFields.Contraction })
            {
                BandRow? band = geo.CellTable.Row(key);
                if (band == null)
                {
                    continue;
                }
                SKRect label = new SKRect(style.Grid.LeftStripWidth, band.Rect.Top, geo.Slots.Left, band.Rect.Bottom);
                p.TextInBox(PartogramFields.DefaultLabel(key), label,
                    new FontKey(style.Text.Family, style.Text.CellLabelSize),
                    style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);
            }

            Primitives.DrawCells(p, geo, doc.Cells, style.Colors.CellValue);
        }

        private static void DrawLegend(Painter p, ChartGeometry geo, PartogramDocument doc)
        {
            List<LegendItem> items = new List<LegendItem>();
            foreach (LegendItem item in doc.Legend)
            {
                items.Add(item);
            }
            if (items.Count == 0)
            {
                if (doc.Cervix.Values.Count > 0 && !string.IsNullOrEmpty(doc.Cervix.LegendText))
                {
                    items.Add(new LegendItem(doc.Cervix.Symbol, doc.Cervix.Color, doc.Cervix.LegendText!));
                }
                if (doc.Station.Values.Count > 0 && !string.IsNullOrEmpty(doc.Station.LegendText))
                {
                    items.Add(new LegendItem(doc.Station.Symbol, doc.Station.Color, doc.Station.LegendText!));
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
        }

        /// <summary>小时（自产程起点，可为小数）→ 像素 x。警戒线这类连续量用它落点。</summary>
        public static float XOfHour(ChartGeometry geo, PartogramDocument doc, double hour)
        {
            int perHour = Math.Max(1, doc.SlotsPerHour);
            return geo.Slots.Left + (float)(hour * perHour * geo.Slots.SlotWidth);
        }

        private static string NullToEmpty(string? value) => value ?? string.Empty;
    }
}
