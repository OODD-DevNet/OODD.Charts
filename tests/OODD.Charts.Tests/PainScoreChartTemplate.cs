using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A9 反证用的一张"引擎从没见过"的表单：NRS 疼痛评分记录单。
    /// 文档类型也是新建的，只继承 <see cref="ChartDocument"/>。
    /// </summary>
    internal sealed class PainScoreDocument : ChartDocument
    {
        public IList<ChartGroup> Days { get; } = new List<ChartGroup>();

        public PointSeries Scores { get; } =
            new PointSeries("nrs", PainScoreChartTemplate.AxisNrs)
            {
                Symbol = MarkerSymbol.DotRing,
                Color = SKColors.Blue,
                LegendText = "NRS",
            };

        public IList<CellValue> Cells { get; } = new List<CellValue>();
    }

    /// <summary>
    /// A9 / FR-3 的可执行证据：新增一张行业表单 = 只实现 <see cref="IChartTemplate"/>。
    /// <para>
    /// 本类放在测试工程里，能编译通过就说明引擎没要求实现者改任何 Core 类型；
    /// 专属尺寸（1 分 = 20px、每天 3 个时点）全部是它自己的公共字段，
    /// 不出现在 <see cref="ChartStyle"/> 上——这正是产程图重构后确立的边界。
    /// </para>
    /// </summary>
    internal sealed class PainScoreChartTemplate : IChartTemplate
    {
        public const string AxisNrs = "nrs";
        public const string RowNote = "note";
        public const string RowAssessor = "assessor";

        // ---------------------------------------------------------- 模板自带 tokens

        /// <summary>1 分占多少像素。</summary>
        public float ScoreRowHeight = 20f;

        /// <summary>一个时点列的宽度。</summary>
        public float SlotWidth = 24f;

        /// <summary>每天几个评估时点。</summary>
        public int SlotsPerDay = 3;

        /// <summary>评分轴量程。</summary>
        public double ScoreMin = 0d;
        public double ScoreMax = 10d;

        /// <summary>每几分画一条粗线。</summary>
        public double MajorEvery = 5d;

        /// <summary>刻度列宽。</summary>
        public float ScaleColumnWidth = 36f;

        public float ScoreLineWidth = 2f;
        public string[] SlotLabels = { "08", "14", "20" };

        public string Key => "test.pain-score-chart";

        public string DisplayName => "疼痛评分记录单";

        // ------------------------------------------------------------------ Measure

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            PainScoreDocument doc = (PainScoreDocument)document;
            int groups = Math.Max(1, doc.Days.Count);

            float leftStrip = style.Grid.LeftStripWidth;
            float gridLeft = leftStrip + ScaleColumnWidth;
            SlotAxis slots = new SlotAxis(gridLeft, SlotWidth, SlotsPerDay, groups);
            float boundsWidth = slots.Right + leftStrip;

            Band header = new Band(
                "header",
                new SKRect(0f, 0f, boundsWidth, style.Header.HospitalRowHeight + style.Header.TitleRowHeight + style.Header.PatientRowHeight),
                new[]
                {
                    Row("hospital", 0f, style.Header.HospitalRowHeight, boundsWidth),
                    Row("title", style.Header.HospitalRowHeight, style.Header.HospitalRowHeight + style.Header.TitleRowHeight, boundsWidth),
                    Row("patient", style.Header.HospitalRowHeight + style.Header.TitleRowHeight,
                        style.Header.HospitalRowHeight + style.Header.TitleRowHeight + style.Header.PatientRowHeight, boundsWidth),
                });

            float dateTop = header.Rect.Bottom;
            float dateBottom = dateTop + style.Header.DateRowHeight;
            float timeBottom = dateBottom + style.Header.TimeRowHeight;
            Band dateBand = new Band(
                "date",
                new SKRect(gridLeft, dateTop, slots.Right, timeBottom),
                new[]
                {
                    new BandRow("date", new SKRect(gridLeft, dateTop, slots.Right, dateBottom)),
                    new BandRow("time", new SKRect(gridLeft, dateBottom, slots.Right, timeBottom)),
                });

            float gridTop = timeBottom;
            float gridBottom = gridTop + (float)((ScoreMax - ScoreMin) * ScoreRowHeight);
            LinearAxis ruleAxis = new LinearAxis(AxisNrs, ScoreMin, ScoreMax, gridTop, gridBottom, 1d, MajorEvery);

            List<LabelColumn> labelColumns = new List<LabelColumn>
            {
                new LabelColumn("nrs-scale", AxisNrs,
                    new SKRect(leftStrip, gridTop, gridLeft, gridBottom), "0", ScoreMin, ScoreMax),
            };

            float cellTop = gridBottom;
            float cellRowHeight = style.Grid.CellRowHeight;
            List<CellRow> cellRows = new List<CellRow>
            {
                new CellRow(RowNote, "备注", false,
                    new SKRect(0f, cellTop, boundsWidth, cellTop + cellRowHeight)),
                new CellRow(RowAssessor, "评估者", false,
                    new SKRect(0f, cellTop + cellRowHeight, boundsWidth, cellTop + cellRowHeight * 2f)),
            };
            Band cellTable = new Band("cells",
                new SKRect(0f, cellTop, boundsWidth, cellTop + cellRowHeight * 2f),
                new BandRow[0]);

            Band footer = new Band("footer",
                new SKRect(0f, cellTable.Rect.Bottom, boundsWidth, cellTable.Rect.Bottom + style.Footer.Height),
                new BandRow[0]);

            return new ChartGeometry(
                new SKRect(0f, 0f, boundsWidth, footer.Rect.Bottom),
                slots,
                ruleAxis,
                new LinearAxis[0],
                header,
                dateBand,
                new SKRect(gridLeft, gridTop, slots.Right, gridBottom),
                labelColumns,
                cellTable,
                cellRows,
                footer);
        }

        private static BandRow Row(string key, float top, float bottom, float right)
        {
            return new BandRow(key, new SKRect(0f, top, right, bottom));
        }

        // --------------------------------------------------------------- DrawContent

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            PainScoreDocument doc = (PainScoreDocument)document;
            ChartStyle style = painter.Style;
            SKColor ink = style.Colors.Text;
            SKColor grid = style.Colors.Grid;

            DrawHeader(painter, geometry, doc, style, ink);
            DrawDateBand(painter, geometry, doc, style, ink);
            DrawGrid(painter, geometry, style, grid);
            DrawScale(painter, geometry, style, ink);
            DrawScores(painter, geometry, doc, style);
            DrawCells(painter, geometry, doc, style);
            DrawFooter(painter, geometry, doc, style);
        }

        private static void DrawHeader(Painter painter, ChartGeometry geometry, PainScoreDocument doc,
                                       ChartStyle style, SKColor ink)
        {
            FontKey hospital = new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold);
            FontKey title = new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold);
            FontKey patient = new FontKey(style.Text.Family, style.Text.HeaderSize, false);

            painter.TextInBox(doc.HospitalName ?? string.Empty, geometry.Header.RowRect("hospital"),
                hospital, ink, CellAlign.Center, CellAlign.Center);
            painter.TextInBox(doc.FormTitle ?? string.Empty, geometry.Header.RowRect("title"),
                title, ink, CellAlign.Center, CellAlign.Center);

            List<string> pairs = new List<string>();
            foreach (KeyValuePair<string, string> pair in doc.Patient.EnumerateAll())
            {
                pairs.Add(pair.Key + ": " + pair.Value);
            }

            painter.TextInBox(string.Join("　", pairs.ToArray()), geometry.Header.RowRect("patient"),
                patient, ink, CellAlign.Near, CellAlign.Center, style.Header.PatientPadding);
        }

        private void DrawDateBand(Painter painter, ChartGeometry geometry, PainScoreDocument doc,
                                  ChartStyle style, SKColor ink)
        {
            FontKey date = new FontKey(style.Text.Family, style.Text.DateSize, false);
            FontKey time = new FontKey(style.Text.Family, style.Text.TimeSize, false);
            BandRow dateRow = geometry.DateBand.Row("date") ?? geometry.DateBand.Rows[0];
            BandRow timeRow = geometry.DateBand.Row("time") ?? geometry.DateBand.Rows[1];

            SlotAxis slots = geometry.Slots;
            for (int group = 0; group < slots.GroupCount; group++)
            {
                ChartGroup? day = group < doc.Days.Count ? doc.Days[group] : null;
                string label = day == null
                    ? string.Empty
                    : (day.DateText ?? day.Date.ToString("MM-dd"));
                painter.TextInBox(label, slots.GroupRect(group, dateRow.Rect.Top, dateRow.Rect.Bottom),
                    date, ink, CellAlign.Center, CellAlign.Center);

                for (int inner = 0; inner < slots.SlotsPerGroup; inner++)
                {
                    string slotLabel = SlotLabelFor(doc, group, inner, style);
                    painter.TextInBox(slotLabel,
                        slots.SlotRect(group * slots.SlotsPerGroup + inner, timeRow.Rect.Top, timeRow.Rect.Bottom),
                        time, ink, CellAlign.Center, CellAlign.Center);
                }
            }
        }

        /// <summary>文档自带的时段名优先，否则用模板 tokens 里的默认表——表单自己的事，引擎不问。</summary>
        private string SlotLabelFor(PainScoreDocument doc, int group, int inner, ChartStyle style)
        {
            if (group < doc.Days.Count)
            {
                IList<string>? labels = doc.Days[group].TimeLabels;
                if (labels != null && inner < labels.Count)
                {
                    return labels[inner];
                }
            }
            return inner < SlotLabels.Length ? SlotLabels[inner] : string.Empty;
        }

        private void DrawGrid(Painter painter, ChartGeometry geometry, ChartStyle style, SKColor grid)
        {
            foreach (AxisTick tick in geometry.RuleAxis.Ticks())
            {
                float width = tick.Kind == TickKind.Major ? style.Grid.MajorStroke : style.Grid.MinorStroke;
                painter.Line(geometry.GridRect.Left, tick.Y, geometry.GridRect.Right, tick.Y, grid, width);
            }

            SlotAxis slots = geometry.Slots;
            for (int slot = 0; slot <= slots.TotalSlots; slot++)
            {
                bool isGroupEdge = slot % slots.SlotsPerGroup == 0;
                float width = isGroupEdge ? style.Grid.BorderStroke : style.Grid.MinorStroke;
                painter.Line(slots.SlotX(slot), geometry.GridRect.Top, slots.SlotX(slot), geometry.GridRect.Bottom, grid, width);
            }

            painter.Frame(geometry.GridRect, grid, style.Grid.BorderStroke);
        }

        private void DrawScale(Painter painter, ChartGeometry geometry, ChartStyle style, SKColor ink)
        {
            LabelColumn column = geometry.LabelColumns[0];
            FontKey key = new FontKey(style.Text.Family, style.Text.ScaleLabelSize, false);
            float half = ScoreRowHeight / 2f;

            foreach (AxisTick tick in geometry.RuleAxis.Ticks())
            {
                if (tick.Kind != TickKind.Major)
                {
                    continue;
                }
                if (tick.Value < column.LabelFrom || tick.Value > column.LabelTo)
                {
                    continue;
                }
                SKRect cell = new SKRect(column.Rect.Left, tick.Y - half, column.Rect.Right, tick.Y + half);
                painter.TextInBox(tick.Value.ToString(column.Format), cell, key, ink,
                    CellAlign.Center, CellAlign.Center);
            }
        }

        private void DrawScores(Painter painter, ChartGeometry geometry, PainScoreDocument doc, ChartStyle style)
        {
            PointSeries series = doc.Scores;
            List<SKPoint> points = new List<SKPoint>();
            foreach (KeyValuePair<int, double> pair in series.Values)
            {
                points.Add(new SKPoint(geometry.SlotCenterX(pair.Key),
                    geometry.YOf(series.AxisKey, pair.Value) + series.YOffset));
            }

            float radius = series.MarkerRadius > 0f ? series.MarkerRadius : style.Series.MarkerRadius;
            float line = series.LineWidth > 0f ? series.LineWidth : ScoreLineWidth;

            if (points.Count >= 2 && series.Connect)
            {
                painter.Polylines(SplitOnGaps(series, geometry), series.Color, line, series.Dashed);
            }
            foreach (SKPoint point in points)
            {
                painter.Marker(series.Symbol, point, radius, series.Color, style.Series.MarkerStroke);
            }
        }

        /// <summary>
        /// 缺测断开：相邻两个实测槽之间只要隔了空槽，就断成两段折线。
        /// 判据来自 <see cref="PointSeries.BreakRule"/>，由模板自己实现——引擎不替表单决定这件事。
        /// </summary>
        private static IEnumerable<List<SKPoint>> SplitOnGaps(PointSeries series, ChartGeometry geometry)
        {
            List<List<SKPoint>> segments = new List<List<SKPoint>>();
            List<SKPoint> current = new List<SKPoint>();
            int previous = int.MinValue;

            foreach (KeyValuePair<int, double> pair in series.Values)
            {
                bool contiguous = series.BreakRule == PointBreakRule.Always || pair.Key == previous + 1;

                if (current.Count > 0 && !contiguous)
                {
                    segments.Add(current);
                    current = new List<SKPoint>();
                }

                current.Add(new SKPoint(geometry.SlotCenterX(pair.Key),
                    geometry.YOf(series.AxisKey, pair.Value) + series.YOffset));
                previous = pair.Key;
            }

            if (current.Count > 0)
            {
                segments.Add(current);
            }
            return segments;
        }

        private void DrawCells(Painter painter, ChartGeometry geometry, PainScoreDocument doc, ChartStyle style)
        {
            FontKey labelKey = new FontKey(style.Text.Family, style.Text.CellLabelSize, false);
            FontKey valueKey = new FontKey(style.Text.Family, style.Text.CellValueSize, false);

            foreach (CellRow row in geometry.CellRows)
            {
                painter.Line(row.Rect.Left, row.Rect.Top, row.Rect.Right, row.Rect.Top,
                    style.Colors.Grid, style.Grid.MinorStroke);
                painter.TextInBox(row.Label,
                    new SKRect(0f, row.Rect.Top, geometry.GridRect.Left, row.Rect.Bottom),
                    labelKey, style.Colors.Text, CellAlign.Center, CellAlign.Center);
            }

            foreach (CellValue value in doc.Cells)
            {
                SKRect cell = geometry.CellRect(value.RowKey, value.Slot);
                painter.TextInBox(value.Text, cell, valueKey,
                    value.Color ?? style.Colors.CellValue, value.Align, CellAlign.Center);
            }
        }

        private void DrawFooter(Painter painter, ChartGeometry geometry, PainScoreDocument document, ChartStyle style)
        {
            PainScoreDocument doc = (PainScoreDocument)document;
            SKRect band = geometry.Footer.Rect;
            float x = band.Left + style.Footer.ItemGap;
            FontKey key = new FontKey(style.Text.Family, style.Text.LegendSize, false);

            foreach (LegendItem item in doc.Legend)
            {
                SKPoint center = new SKPoint(x + style.Series.MarkerRadius, band.MidY);
                painter.Marker(item.Symbol, center, style.Series.MarkerRadius, item.Color, style.Series.MarkerStroke);
                x += style.Series.MarkerRadius * 2f + style.Footer.SymbolTextGap;
                float width = painter.TextWidth(item.Text, key);
                painter.Text(item.Text, x, band.MidY + style.Text.LegendSize / 3f, key, item.Color);
                x += width + style.Footer.ItemGap;
            }
        }
    }
}
