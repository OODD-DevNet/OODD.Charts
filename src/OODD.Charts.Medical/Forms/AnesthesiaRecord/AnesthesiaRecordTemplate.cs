using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms.AnesthesiaRecord
{
    /// <summary>麻醉记录单的求解结果（所有落点都在这里，绘制代码不再做版面算术）。</summary>
    public sealed class AnesthesiaRecordLayout
    {
        internal AnesthesiaRecordLayout(
            SKRect bounds, SKRect header, SKRect patient, SKRect operation,
            SKRect timeBand, SKRect plot, SKRect medicationBand, SKRect eventBand, SKRect footer,
            NumericAxis timeAxis, NumericAxis leftAxis, NumericAxis rightAxis,
            IReadOnlyList<AxisMark> timeMarks, IReadOnlyList<AxisMark> leftMarks,
            IReadOnlyList<AxisMark> rightMarks,
            IReadOnlyList<SpanBand> medicationBands, IReadOnlyList<SKRect> medicationLanes,
            IReadOnlyList<SKPoint[]> eventEnds, float eventSymbolSize, IReadOnlyList<SKRect> legendItems)
        {
            Bounds = bounds;
            HeaderRect = header;
            PatientRect = patient;
            OperationRect = operation;
            TimeBandRect = timeBand;
            PlotRect = plot;
            MedicationBandRect = medicationBand;
            EventBandRect = eventBand;
            FooterRect = footer;
            TimeAxis = timeAxis;
            LeftAxis = leftAxis;
            RightAxis = rightAxis;
            TimeMarks = timeMarks;
            LeftMarks = leftMarks;
            RightMarks = rightMarks;
            MedicationBands = medicationBands;
            MedicationLanes = medicationLanes;
            EventEnds = eventEnds;
            EventSymbolSize = eventSymbolSize;
            LegendItems = legendItems;
        }

        public SKRect Bounds { get; }
        public SKRect HeaderRect { get; }
        public SKRect PatientRect { get; }
        public SKRect OperationRect { get; }
        public SKRect TimeBandRect { get; }
        public SKRect PlotRect { get; }
        public SKRect MedicationBandRect { get; }
        public SKRect EventBandRect { get; }
        public SKRect FooterRect { get; }

        public NumericAxis TimeAxis { get; }
        public NumericAxis LeftAxis { get; }
        public NumericAxis RightAxis { get; }

        public IReadOnlyList<AxisMark> TimeMarks { get; }
        public IReadOnlyList<AxisMark> LeftMarks { get; }
        public IReadOnlyList<AxisMark> RightMarks { get; }

        /// <summary>每条持续用药的时段带（域 = 分钟）。</summary>
        public IReadOnlyList<SpanBand> MedicationBands { get; }

        /// <summary>每条持续用药在带内的行矩形。</summary>
        public IReadOnlyList<SKRect> MedicationLanes { get; }

        /// <summary>成对事件的两个端点（0 = 早时刻，1 = 晚时刻）。</summary>
        public IReadOnlyList<SKPoint[]> EventEnds { get; }

        public float EventSymbolSize { get; }

        /// <summary>页脚图例每项的内容矩形（一对符号 + 圆圈序号），按行优先排列。</summary>
        public IReadOnlyList<SKRect> LegendItems { get; }

        /// <summary>左侧刻度标签列（网格外）。</summary>
        public SKRect LeftLabelRect
        {
            get { return new SKRect(Bounds.Left, PlotRect.Top, PlotRect.Left, PlotRect.Bottom); }
        }

        /// <summary>右侧体温刻度标签列（网格外）。</summary>
        public SKRect RightLabelRect
        {
            get { return new SKRect(PlotRect.Right, PlotRect.Top, Bounds.Right, PlotRect.Bottom); }
        }

        /// <summary>第 index 个事件两端符号的包裹矩形（含连线），供像素断言取窗口。</summary>
        public SKRect EventGlyphBox(int index)
        {
            if (index < 0 || index >= EventEnds.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            SKPoint a = EventEnds[index][0];
            SKPoint b = EventEnds[index][1];
            float pad = EventSymbolSize * 3f;
            return new SKRect(Math.Min(a.X, b.X) - pad, Math.Min(a.Y, b.Y) - pad,
                              Math.Max(a.X, b.X) + pad, Math.Max(a.Y, b.Y) + pad);
        }
    }

    /// <summary>
    /// 麻醉记录单 medical.anesthesia-record。
    /// <para>
    /// 左轴（脉搏/血压 20–260 步长 20）与右轴（体温 12–40 步长 2）共用同一段像素区间，
    /// 横线只由左轴枚举一次；标签列分别在网格左右两侧之外。事件用成对符号的两个端点落笔，
    /// 持续用药用 <see cref="SpanBand"/>。采样密度只走文档校验告警，不拦截绘制。
    /// </para>
    /// </summary>
    public sealed class AnesthesiaRecordTemplate : IChartTemplate
    {
        public const string LeftAxisKey = "anesthesia.bp-pulse";
        public const string RightAxisKey = "anesthesia.temperature";
        public const string TimeAxisKey = "anesthesia.time";

        public string Key => "medical.anesthesia-record";
        public string DisplayName => "麻醉记录单";

        /// <summary>表单专属令牌；改令牌即改版面。</summary>
        public AnesthesiaRecordStyle Tokens { get; set; } = new AnesthesiaRecordStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            AnesthesiaRecordDocument doc = Cast(document);
            AnesthesiaRecordStyle t = Tokens;

            int totalMinutes = Math.Max(1, doc.TotalMinutes);
            float width = t.PaperWidth;
            float plotLeft = t.LeftLabelWidth;
            float plotRight = width - t.RightLabelWidth;

            // ---- 页眉 ----
            float y = 0f;
            SKRect hospital = new SKRect(0f, y, width, y + t.HospitalRowHeight);
            y += t.HospitalRowHeight;
            SKRect title = new SKRect(0f, y, width, y + t.TitleRowHeight);
            y += t.TitleRowHeight;

            int patientRows = Math.Max(1, Primitives.HeaderFieldRowCount(
                fonts, doc.Patient.EnumerateAll(), style, width));
            SKRect patient = new SKRect(0f, y, width, y + patientRows * style.Header.PatientRowHeight);
            y = patient.Bottom;

            SKRect operation = new SKRect(0f, y, width, y + t.OperationRowHeight);
            y = operation.Bottom;

            SKRect header = new SKRect(0f, 0f, width, y);

            // ---- 时间带 ----
            SKRect timeBand = new SKRect(0f, y, width, y + t.TimeBandHeight);
            y = timeBand.Bottom + t.PlotGap;

            // ---- 绘图区：左右两轴共用 From/To ----
            SKRect plot = new SKRect(plotLeft, y, plotRight, y + t.PlotHeight);
            y = plot.Bottom;

            NumericAxis leftAxis = new NumericAxis(LeftAxisKey, 20d, 260d, plot.Bottom, plot.Top,
                AxisDirection.VerticalUp);
            NumericAxis rightAxis = new NumericAxis(RightAxisKey, 12d, 40d, plot.Bottom, plot.Top,
                AxisDirection.VerticalUp);
            NumericAxis timeAxis = new NumericAxis(TimeAxisKey, 0d, totalMinutes, plot.Left, plot.Right,
                AxisDirection.HorizontalRight);

            List<AxisTier> timeTiers = new List<AxisTier>
            {
                new AxisTier(Math.Max(1, t.TimeMinorMinutes), RulerTier.Hairline, false),
                new AxisTier(Math.Max(1, t.TimeMajorMinutes), RulerTier.Heavy, true),
            };
            List<AxisMark> timeMarks = AxisRuler.Marks(timeAxis, timeTiers);

            List<AxisMark> leftMarks = AxisRuler.Marks(leftAxis,
                new[] { new AxisTier(20d, RulerTier.Medium, true) });
            List<AxisMark> rightMarks = AxisRuler.Marks(rightAxis,
                new[] { new AxisTier(2d, RulerTier.Fine, true) });

            // ---- 持续用药带 ----
            int medRows = Math.Max(1, doc.ContinuousMedications.Count);
            SKRect medicationBand = new SKRect(0f, y, width, y + medRows * t.MedicationRowHeight
                                                              + t.MedicationBandPadding * 2f);
            y = medicationBand.Bottom;

            List<SpanBand> medBands = new List<SpanBand>();
            List<SKRect> medLanes = new List<SKRect>();
            for (int i = 0; i < doc.ContinuousMedications.Count; i++)
            {
                ContinuousMedication med = doc.ContinuousMedications[i];
                medBands.Add(new SpanBand("med-" + i, med.StartMin, med.EndMin, null, null, BandFill.Solid));
                float laneTop = medicationBand.Top + t.MedicationBandPadding + i * t.MedicationRowHeight;
                medLanes.Add(new SKRect(plot.Left, laneTop, plot.Right, laneTop + t.MedicationRowHeight));
            }

            // ---- 事件带 ----
            SKRect eventBand = new SKRect(0f, y, width, y + t.EventBandHeight);
            y = eventBand.Bottom;

            List<SKPoint[]> eventEnds = new List<SKPoint[]>();
            foreach (AnesthesiaEvent ev in doc.Events)
            {
                double? startSys = doc.ValueAt(ev.StartMin, p => p.Systolic);
                double? endSys = doc.ValueAt(ev.EndMin, p => p.Systolic);
                if (startSys == null || endSys == null)
                {
                    continue;
                }

                float x0 = timeAxis.PositionOf(ev.StartMin);
                float x1 = timeAxis.PositionOf(ev.EndMin);
                eventEnds.Add(new[]
                {
                    new SKPoint(x0, leftAxis.PositionOf(startSys.Value)),
                    new SKPoint(x1, leftAxis.PositionOf(endSys.Value)),
                });
            }

            // ---- 页脚 ----
            SKRect footer = new SKRect(0f, y, width, y + t.FooterHeight);
            y = footer.Bottom;

            IReadOnlyList<PairedEventGlyph> legendSymbols = AnesthesiaRecordDocument.CreatePairedSymbols();
            int legendPerRow = Math.Max(1, t.LegendItemsPerRow);
            float legendCellWidth = width / legendPerRow;
            // PairedEventGlyph: 端点半径 0.5×size、整对半跨 1.6×size → 外缘 2.1×size。
            float legendHalfSpan = t.LegendSymbolSize * 2.1f;
            List<SKRect> legendItems = new List<SKRect>();
            for (int i = 0; i < legendSymbols.Count; i++)
            {
                int row = i / legendPerRow;
                int col = i % legendPerRow;
                float top = footer.Top + t.LegendRowTop + row * t.LegendRowHeight;
                float glyphCenter = col * legendCellWidth + t.LegendItemInset + legendHalfSpan;
                legendItems.Add(new SKRect(
                    glyphCenter - legendHalfSpan, top,
                    glyphCenter + legendHalfSpan + t.LegendLabelGap + t.LegendLabelWidth,
                    top + t.LegendRowHeight - 4f));
            }

            AnesthesiaRecordLayout layout = new AnesthesiaRecordLayout(
                new SKRect(0f, 0f, width, y), header, patient, operation, timeBand, plot,
                medicationBand, eventBand, footer, timeAxis, leftAxis, rightAxis,
                timeMarks, leftMarks, rightMarks, medBands, medLanes, eventEnds, t.EventSymbolSize,
                legendItems);

            return ChartGeometry.ForLayout(layout.Bounds, plot, layout);
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            AnesthesiaRecordDocument doc = Cast(document);
            AnesthesiaRecordLayout layout = geometry.LayoutOf<AnesthesiaRecordLayout>()
                ?? throw new InvalidOperationException("几何里没有 AnesthesiaRecordLayout。");

            ChartStyle style = painter.Style;
            AnesthesiaRecordStyle t = Tokens;
            SKColor line = style.Colors.Grid;

            DrawHeader(painter, layout, doc, style);
            DrawTimeAxis(painter, layout, doc, style, line);
            DrawValueAxes(painter, layout, style, line);
            DrawSeries(painter, layout, doc, style);
            DrawMedications(painter, layout, doc, style, line);
            DrawEvents(painter, layout, doc, style, line);
            DrawFooter(painter, layout, doc, style, line);
            DrawFrame(painter, layout, style, line);
        }

        private void DrawHeader(Painter p, AnesthesiaRecordLayout layout, AnesthesiaRecordDocument doc,
            ChartStyle style)
        {
            ColorPalette colors = style.Colors;
            AnesthesiaRecordStyle t = Tokens;
            float hospitalsBottom = layout.HeaderRect.Top + t.HospitalRowHeight;
            p.TextInBox(doc.HospitalName ?? string.Empty,
                new SKRect(0f, layout.HeaderRect.Top, layout.Bounds.Right, hospitalsBottom),
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            float titleBottom = hospitalsBottom + t.TitleRowHeight;
            p.TextInBox(doc.FormTitle ?? "麻醉记录单",
                new SKRect(0f, hospitalsBottom, layout.Bounds.Right, titleBottom),
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.HeaderRightNote))
            {
                p.TextInBox(doc.HeaderRightNote!,
                    new SKRect(0f, hospitalsBottom, layout.Bounds.Right, titleBottom),
                    new FontKey(style.Text.Family, style.Text.HeaderSize),
                    colors.Text, CellAlign.Far, CellAlign.Center, 6f);
            }

            Primitives.DrawHeaderFields(p, layout.PatientRect, doc.Patient.EnumerateAll(), colors.Text);

            AnesthesiaOperation op = doc.Operation;
            string text = "术式：" + (op.Procedure ?? string.Empty)
                          + "　麻醉方式：" + (op.AnesthesiaType ?? string.Empty)
                          + "　麻醉医师：" + (op.Anesthetist ?? string.Empty)
                          + "　起止：" + (op.StartTime ?? string.Empty) + "–" + (op.EndTime ?? string.Empty);
            p.TextInBox(text, layout.OperationRect,
                new FontKey(style.Text.Family, style.Text.HeaderSize),
                colors.Text, CellAlign.Near, CellAlign.Center, 8f);
            p.Line(layout.OperationRect.Left, layout.OperationRect.Bottom, layout.OperationRect.Right,
                layout.OperationRect.Bottom, colors.Grid, style.Grid.MinorStroke);
        }

        private static void DrawTimeAxis(Painter p, AnesthesiaRecordLayout layout, AnesthesiaRecordDocument doc,
            ChartStyle style, SKColor line)
        {
            FontKey key = new FontKey(style.Text.Family, style.Text.TimeSize);
            SKRect band = layout.TimeBandRect;

            // 左/右轴列头，占时间带左右两端（仍在网格之外）。
            p.TextInBox("脉搏/血压", new SKRect(0f, band.Top, layout.PlotRect.Left, band.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);
            p.TextInBox("体温℃", new SKRect(layout.PlotRect.Right, band.Top, layout.Bounds.Right, band.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

            foreach (AxisMark mark in layout.TimeMarks)
            {
                float x = mark.Position;
                float tickBottom = mark.Tier == RulerTier.Heavy ? band.Bottom : band.MidY;
                p.Line(x, band.Bottom, x, tickBottom, line,
                    mark.Tier == RulerTier.Heavy ? style.Grid.MinorStroke + 0.5f : style.Grid.MinorStroke);

                if (mark.Labeled)
                {
                    p.TextInBox(AnesthesiaRecordDocument.TimeLabel(mark.Value),
                        new SKRect(x - 16f, band.Top, x + 16f, band.MidY),
                        key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }

            p.Line(0f, band.Bottom, layout.Bounds.Right, band.Bottom, line, style.Grid.MinorStroke);
        }

        private static void DrawValueAxes(Painter p, AnesthesiaRecordLayout layout, ChartStyle style, SKColor line)
        {
            FontKey key = new FontKey(style.Text.Family, style.Text.ScaleLabelSize);
            SKRect plot = layout.PlotRect;

            foreach (AxisMark mark in layout.LeftMarks)
            {
                float y = mark.Position;
                float width = Math.Abs(mark.Value % 100d) < 0.001d
                    ? style.Grid.MinorStroke + 0.6f
                    : style.Grid.MinorStroke;
                p.Line(plot.Left, y, plot.Right, y, line, width);
                p.TextInBox(mark.Label("0"),
                    new SKRect(0f, y - 8f, plot.Left - 4f, y + 8f),
                    key, style.Colors.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            foreach (AxisMark mark in layout.RightMarks)
            {
                float y = mark.Position;
                p.Line(plot.Right, y, plot.Right + 4f, y, line, style.Grid.MinorStroke);
                p.TextInBox(mark.Label("0"),
                    new SKRect(plot.Right + 6f, y - 7f, layout.Bounds.Right, y + 7f),
                    key, style.Colors.Text, CellAlign.Near, CellAlign.Center, 0f);
            }

            // 时间线贯穿绘图区与下方两条带，刻度带只在上方。
            foreach (AxisMark mark in layout.TimeMarks)
            {
                float x = mark.Position;
                p.Line(x, layout.TimeBandRect.Bottom, x, layout.EventBandRect.Bottom, line,
                    mark.Tier == RulerTier.Heavy ? style.Grid.MinorStroke + 0.5f : style.Grid.MinorStroke);
            }

            p.Rect(plot, line, style.Grid.MajorStroke);
        }

        private void DrawSeries(Painter p, AnesthesiaRecordLayout layout, AnesthesiaRecordDocument doc,
            ChartStyle style)
        {
            List<AnesthesiaPoint> points = new List<AnesthesiaPoint>(doc.Points);
            points.Sort((a, b) => a.TimeMin.CompareTo(b.TimeMin));

            DrawSeriesLine(p, layout, points, x => x.Pulse, layout.LeftAxis, style.Colors.Text,
                MarkerSymbol.Dot, false, style);
            DrawSeriesLine(p, layout, points, x => x.Systolic, layout.LeftAxis, style.Colors.Event,
                MarkerSymbol.Ring, false, style);
            DrawSeriesLine(p, layout, points, x => x.Diastolic, layout.LeftAxis, style.Colors.Event,
                MarkerSymbol.Dot, true, style);
            DrawSeriesLine(p, layout, points, x => x.Mean, layout.LeftAxis, style.Colors.ReferenceLineAlt,
                MarkerSymbol.None, true, style);
            DrawSeriesLine(p, layout, points, x => x.Temperature, layout.RightAxis,
                style.Colors.ReferenceLine, MarkerSymbol.Cross, false, style);
        }

        private static void DrawSeriesLine(Painter p, AnesthesiaRecordLayout layout,
            IReadOnlyList<AnesthesiaPoint> points, Func<AnesthesiaPoint, double> selector,
            NumericAxis axis, SKColor color, MarkerSymbol symbol, bool dashed, ChartStyle style)
        {
            if (points.Count == 0)
            {
                return;
            }

            List<SKPoint> line = new List<SKPoint>(points.Count);
            foreach (AnesthesiaPoint point in points)
            {
                line.Add(new SKPoint(layout.TimeAxis.PositionOf(point.TimeMin),
                    axis.PositionOf(selector(point))));
            }

            p.Polyline(line, color, style.Series.LineWidth, dashed);
            foreach (SKPoint point in line)
            {
                p.Marker(symbol, point, style.Series.MarkerRadius, color, style.Series.MarkerStroke);
            }
        }

        private void DrawMedications(Painter p, AnesthesiaRecordLayout layout, AnesthesiaRecordDocument doc,
            ChartStyle style, SKColor line)
        {
            AnesthesiaRecordStyle t = Tokens;
            FontKey key = new FontKey(style.Text.Family, style.Text.EventSize);
            p.Line(0f, layout.MedicationBandRect.Top, layout.Bounds.Right, layout.MedicationBandRect.Top,
                line, style.Grid.MinorStroke);
            p.Line(0f, layout.MedicationBandRect.Bottom, layout.Bounds.Right, layout.MedicationBandRect.Bottom,
                line, style.Grid.MinorStroke);
            p.TextInBox("持续用药", new SKRect(0f, layout.MedicationBandRect.Top,
                    layout.PlotRect.Left, layout.MedicationBandRect.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

            for (int i = 0; i < layout.MedicationBands.Count; i++)
            {
                SpanBand band = layout.MedicationBands[i];
                SKRect lane = layout.MedicationLanes[i];
                float left = layout.TimeAxis.PositionOf(band.FromDomain);
                float right = layout.TimeAxis.PositionOf(band.ToDomain);
                p.FillRect(new SKRect(left, lane.Top, right, lane.Bottom), t.MedicationFill);
                p.Frame(new SKRect(left, lane.Top, right, lane.Bottom), line, style.Grid.MinorStroke);

                ContinuousMedication med = doc.ContinuousMedications[i];
                p.TextInBox(med.Name + " " + med.Dose,
                    new SKRect(left + 2f, lane.Top, Math.Max(left + 30f, right), lane.Bottom),
                    key, style.Colors.Text, CellAlign.Near, CellAlign.Center, 2f);
            }
        }

        private void DrawEvents(Painter p, AnesthesiaRecordLayout layout, AnesthesiaRecordDocument doc,
            ChartStyle style, SKColor line)
        {
            AnesthesiaRecordStyle t = Tokens;
            IReadOnlyList<PairedEventGlyph> symbols = AnesthesiaRecordDocument.CreatePairedSymbols();
            FontKey key = new FontKey(style.Text.Family, style.Text.EventSize);
            SKColor color = style.Colors.Event;

            p.Line(0f, layout.EventBandRect.Top, layout.Bounds.Right, layout.EventBandRect.Top,
                line, style.Grid.MinorStroke);
            p.Line(0f, layout.EventBandRect.Bottom, layout.Bounds.Right, layout.EventBandRect.Bottom,
                line, style.Grid.MinorStroke);
            p.TextInBox("事件", new SKRect(0f, layout.EventBandRect.Top,
                    layout.PlotRect.Left, layout.EventBandRect.Bottom),
                key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

            int drawn = 0;
            for (int i = 0; i < doc.Events.Count && drawn < layout.EventEnds.Count; i++)
            {
                AnesthesiaEvent ev = doc.Events[i];
                double? start = doc.ValueAt(ev.StartMin, x => x.Systolic);
                double? end = doc.ValueAt(ev.EndMin, x => x.Systolic);
                if (start == null || end == null)
                {
                    continue;
                }

                SKPoint[] ends = layout.EventEnds[drawn];
                PairedEventGlyph glyph = symbols[((ev.SymbolIndex % symbols.Count) + symbols.Count) % symbols.Count];
                glyph.Draw(p, ends[0], ends[1], layout.EventSymbolSize, color, style.Series.MarkerStroke);

                // 未列图例的事件用圆圈序号标出（DigitBox 复用；这里只用 1 格）。
                DigitBox box = new DigitBox(1, 0.72f, 0f);
                SKPoint anchor = new SKPoint((ends[0].X + ends[1].X) / 2f,
                    layout.EventBandRect.Bottom - t.EventDigitBoxSize * 0.7f);
                box.Draw(p, anchor, t.EventDigitBoxSize, color);
                p.TextInBox(Circled(ev.Sequence),
                    box.BoxRect(anchor, t.EventDigitBoxSize, 0),
                    key, color, CellAlign.Center, CellAlign.Center, 0f);
                drawn++;
            }
        }

        private void DrawFooter(Painter p, AnesthesiaRecordLayout layout, AnesthesiaRecordDocument doc,
            ChartStyle style, SKColor line)
        {
            AnesthesiaRecordStyle t = Tokens;
            IReadOnlyList<PairedEventGlyph> symbols = AnesthesiaRecordDocument.CreatePairedSymbols();
            FontKey key = new FontKey(style.Text.Family, style.Text.TimeSize);

            p.Line(0f, layout.FooterRect.Top, layout.Bounds.Right, layout.FooterRect.Top, line,
                style.Grid.MinorStroke);

            // 图例项矩形由 Measure 算好：符号半跨与序号位置都从同一个矩形派生，
            // 逐项再按 i×行高 估算会像旧版那样把序号画进右侧符号里。
            for (int i = 0; i < symbols.Count && i < layout.LegendItems.Count; i++)
            {
                SKRect item = layout.LegendItems[i];
                float halfSpan = t.LegendSymbolSize * 2.1f;
                symbols[i].Draw(p, new SKPoint(item.Left + halfSpan, item.MidY),
                    t.LegendSymbolSize, style.Colors.Text);
                p.TextInBox(Circled(i + 1),
                    new SKRect(item.Right - t.LegendLabelWidth, item.Top, item.Right, item.Bottom),
                    key, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            IReadOnlyList<string> warnings = doc.SamplingWarnings();
            if (warnings.Count > 0)
            {
                p.TextInBox("校验告警：" + warnings[0],
                    new SKRect(4f, layout.FooterRect.Bottom - 14f, layout.Bounds.Right - 4f,
                        layout.FooterRect.Bottom - 1f),
                    key, style.Colors.Highlight, CellAlign.Near, CellAlign.Center, 0f);
            }
        }

        private static void DrawFrame(Painter p, AnesthesiaRecordLayout layout, ChartStyle style, SKColor line)
        {
            p.Line(0f, layout.TimeBandRect.Top, layout.Bounds.Right, layout.TimeBandRect.Top,
                line, style.Grid.BorderStroke);
            p.Line(0f, layout.TimeBandRect.Top, 0f, layout.FooterRect.Bottom,
                line, style.Grid.BorderStroke);
            p.Line(layout.Bounds.Right, layout.TimeBandRect.Top, layout.Bounds.Right, layout.FooterRect.Bottom,
                line, style.Grid.BorderStroke);
            p.Line(0f, layout.FooterRect.Bottom, layout.Bounds.Right, layout.FooterRect.Bottom,
                line, style.Grid.BorderStroke);
        }

        /// <summary>
        /// 序号标记。内置字体只覆盖 ①–⑩（⑪ 会落成豆腐块，PDF/SVG 里甚至变空文本），
        /// 超过 10 的一律退回 (11) 这样的括号数字。
        /// </summary>
        private static string Circled(int sequence)
        {
            string[] marks = { "①", "②", "③", "④", "⑤", "⑥", "⑦", "⑧", "⑨", "⑩" };
            int index = sequence - 1;
            if (index < 0)
            {
                index = 0;
            }
            if (index < marks.Length)
            {
                return marks[index];
            }
            return "(" + sequence.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        private static AnesthesiaRecordDocument Cast(ChartDocument document)
        {
            AnesthesiaRecordDocument? typed = document as AnesthesiaRecordDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(AnesthesiaRecordDocument) + "。",
                    nameof(document));
            }
            return typed;
        }
    }
}
