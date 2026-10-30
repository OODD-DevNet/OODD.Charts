using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 可被任意表单模板复用的绘制构件。模板只负责“版面怎么摆 + 数据往哪落”，
    /// 具体的线、格、符号、文本由这里画，避免每张新表单重写一遍底层图元。
    /// </summary>
    public static class Primitives
    {
        // ------------------------------------------------------------ 网格

        /// <summary>
        /// 画横向刻度线。线的位置与粗细完全由 <see cref="ChartGeometry.RuleAxis"/> 决定，
        /// 因此其它共用该像素区间的轴（脉搏、胎心）不需要再画线。
        /// </summary>
        public static void DrawRuleLines(Painter p, ChartGeometry geo, SKColor color)
        {
            ChartStyle style = p.Style;
            float top = geo.GridRect.Top;
            float bottom = geo.GridRect.Bottom;

            foreach (AxisTick tick in geo.RuleAxis.Ticks())
            {
                if (tick.Y < top - 0.5f || tick.Y > bottom + 0.5f)
                {
                    continue;
                }

                float width = tick.Kind == TickKind.Minor ? style.Grid.MinorStroke : style.Grid.MajorStroke;
                p.Line(geo.GridLeft, tick.Y, geo.GridRight, tick.Y, color, width);
            }
        }

        /// <summary>画逐槽竖线；按分组的边界可选加粗。</summary>
        public static void DrawSlotLines(Painter p, ChartGeometry geo, SKColor color, bool emphasizeGroups)
        {
            ChartStyle style = p.Style;
            float top = geo.GridRect.Top;
            float bottom = geo.GridRect.Bottom;

            for (int slot = 0; slot <= geo.Slots.TotalSlots; slot++)
            {
                float x = geo.Slots.SlotX(slot);
                bool groupEdge = emphasizeGroups && slot % geo.Slots.SlotsPerGroup == 0;
                p.Line(x, top, x, bottom, color, groupEdge ? style.Grid.MajorStroke : style.Grid.MinorStroke);
            }
        }

        /// <summary>画逐槽竖线，贯穿网格带与附属表格带（附属表格按分组合并，故只画组边界）。</summary>
        public static void DrawFullHeightSlotLines(Painter p, ChartGeometry geo, SKColor color)
        {
            ChartStyle style = p.Style;
            float top = geo.GridRect.Top;
            float bottom = geo.CellTable.Rect.Bottom;

            for (int slot = 0; slot <= geo.Slots.TotalSlots; slot++)
            {
                float x = geo.Slots.SlotX(slot);
                bool groupEdge = slot % geo.Slots.SlotsPerGroup == 0;
                if (groupEdge)
                {
                    p.Line(x, top, x, bottom, color, style.Grid.MinorStroke);
                }
                else
                {
                    p.Line(x, top, x, geo.GridRect.Bottom, color, style.Grid.MinorStroke);
                }
            }
        }

        /// <summary>画一个波段的外框与子行分隔线。</summary>
        public static void DrawBand(Painter p, Band band, SKRect horizontalSpan, SKColor color, float width)
        {
            foreach (BandRow row in band.Rows)
            {
                p.Line(horizontalSpan.Left, row.Rect.Bottom, horizontalSpan.Right, row.Rect.Bottom, color, width);
            }
            p.Line(horizontalSpan.Left, band.Rect.Bottom, horizontalSpan.Right, band.Rect.Bottom, color, width);
        }

        // ------------------------------------------------------------ 刻度标签列

        /// <summary>画左侧刻度标签列的分隔线与数字。</summary>
        public static void DrawLabelColumns(Painter p, ChartGeometry geo, SKColor lineColor, SKColor textColor)
        {
            ChartStyle style = p.Style;
            FontKey key = new FontKey(style.Text.Family, style.Text.ScaleLabelSize);

            foreach (LabelColumn column in geo.LabelColumns)
            {
                p.Line(column.Rect.Right, geo.GridRect.Top, column.Rect.Right, geo.GridRect.Bottom,
                       lineColor, style.Grid.MinorStroke);

                LinearAxis axis = geo.Axis(column.AxisKey);
                foreach (AxisTick tick in axis.Ticks())
                {
                    if (tick.Kind == TickKind.Minor)
                    {
                        continue;
                    }
                    if (tick.Y < geo.GridRect.Top || tick.Y > geo.GridRect.Bottom)
                    {
                        continue;
                    }
                    if (tick.Value < column.LabelFrom - 1e-9 || tick.Value > column.LabelTo + 1e-9)
                    {
                        continue;
                    }

                    string label = FormatTick(tick.Value, column.Format);
                    float half = style.Text.ScaleLabelSize;
                    float top = tick.Y - half;
                    float bottom = tick.Y + half;
                    if (top < geo.GridRect.Top)
                    {
                        top = geo.GridRect.Top;
                        bottom = top + half * 2f;
                    }
                    if (bottom > geo.GridRect.Bottom)
                    {
                        bottom = geo.GridRect.Bottom;
                        top = bottom - half * 2f;
                    }

                    SKRect slot = new SKRect(column.Rect.Left, top, column.Rect.Right, bottom);
                    p.TextInBox(label, slot, key, textColor, CellAlign.Center, CellAlign.Center, 0f);
                }
            }
        }

        public static string FormatTick(double value, string format)
        {
            try
            {
                return value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return value.ToString();
            }
        }

        // ------------------------------------------------------------ 点系列

        /// <summary>画一条点系列（折线 + 符号 + 点旁小字），含缺测断线。</summary>
        public static void DrawSeries(Painter p, ChartGeometry geo, PointSeries series)
        {
            if (series.Values.Count == 0)
            {
                return;
            }

            ChartStyle style = p.Style;
            LinearAxis axis = geo.Axis(series.AxisKey);
            float radius = series.MarkerRadius > 0f ? series.MarkerRadius : style.Series.MarkerRadius;
            float width = series.LineWidth > 0f ? series.LineWidth : style.Series.LineWidth;

            List<int> slots = SortedSlots(series.Values);
            List<List<SKPoint>> segments = SplitSegments(slots, geo.Slots.SlotsPerGroup, series.BreakRule,
                slot => new SKPoint(geo.SlotCenterX(slot), axis.YOf(series.Values[slot]) + series.YOffset));

            if (series.Connect)
            {
                foreach (List<SKPoint> segment in segments)
                {
                    if (segment.Count > 1)
                    {
                        p.Polyline(segment, series.Color, width, series.Dashed);
                    }
                }
            }

            foreach (int slot in slots)
            {
                SKPoint center = new SKPoint(geo.SlotCenterX(slot), axis.YOf(series.Values[slot]) + series.YOffset);
                p.Marker(series.Symbol, center, radius, series.Color, style.Series.MarkerStroke);

                string? note;
                if (series.ValueLabels.TryGetValue(slot, out note) && !string.IsNullOrEmpty(note))
                {
                    FontKey key = new FontKey(style.Text.Family, style.Text.AnnotationSize);
                    SKColor noteColor = series.ValueLabelColor ?? series.Color;
                    p.Text(note!, center.X + style.Series.LabelOffset, center.Y - style.Series.LabelOffset, key, noteColor);
                }
            }
        }

        private static List<int> SortedSlots(IDictionary<int, double> values)
        {
            List<int> slots = new List<int>(values.Keys);
            slots.Sort();
            return slots;
        }

        /// <summary>按断开策略把点集切成若干连续段。</summary>
        public static List<List<SKPoint>> SplitSegments(List<int> slots, int slotsPerGroup,
            PointBreakRule rule, Func<int, SKPoint> locate)
        {
            List<List<SKPoint>> segments = new List<List<SKPoint>>();
            List<SKPoint> current = new List<SKPoint>();

            for (int i = 0; i < slots.Count; i++)
            {
                int slot = slots[i];
                if (i > 0)
                {
                    int previous = slots[i - 1];
                    bool gap = rule == PointBreakRule.BreakOnGap && slot != previous + 1;
                    bool dayEdge = rule == PointBreakRule.BreakOnDayBoundary &&
                                   slot / slotsPerGroup != previous / slotsPerGroup;
                    if ((gap || dayEdge) && current.Count > 0)
                    {
                        segments.Add(current);
                        current = new List<SKPoint>();
                    }
                }
                current.Add(locate(slot));
            }

            if (current.Count > 0)
            {
                segments.Add(current);
            }
            return segments;
        }

        // ------------------------------------------------------------ 附属表格

        /// <summary>把一批 <see cref="CellValue"/> 落到各自的单元格里。</summary>
        public static void DrawCells(Painter p, ChartGeometry geo, IEnumerable<CellValue> values, SKColor defaultColor)
        {
            Dictionary<string, Dictionary<int, int>> counters = CountOccurrences(values);

            foreach (CellValue value in values)
            {
                CellRow? row = geo.Row(value.RowKey);
                if (row == null)
                {
                    continue;
                }

                int total = TotalPerCell(counters, value, geo);
                int occurrence = value.Occurrence;
                SKRect rect = geo.CellRect(value.RowKey, value.Slot, occurrence, total);
                float size = value.Size ?? p.Style.Text.CellValueSize;
                FontKey key = new FontKey(p.Style.Text.Family, size);
                p.TextInBox(value.Text, rect, key, value.Color ?? defaultColor, value.Align, CellAlign.Center);
            }
        }

        private static Dictionary<string, Dictionary<int, int>> CountOccurrences(IEnumerable<CellValue> values)
        {
            Dictionary<string, Dictionary<int, int>> counters = new Dictionary<string, Dictionary<int, int>>(StringComparer.Ordinal);
            foreach (CellValue value in values)
            {
                Dictionary<int, int>? bucket;
                if (!counters.TryGetValue(value.RowKey, out bucket))
                {
                    bucket = new Dictionary<int, int>();
                    counters[value.RowKey] = bucket;
                }
                int existing;
                bucket.TryGetValue(value.Slot, out existing);
                bucket[value.Slot] = existing + 1;
            }
            return counters;
        }

        private static int TotalPerCell(Dictionary<string, Dictionary<int, int>> counters, CellValue value, ChartGeometry geo)
        {
            if (value.OccurrenceCount > 1)
            {
                return value.OccurrenceCount;
            }

            // 合并格按组聚合计数；分槽格按槽聚合。
            int key = geo.Row(value.RowKey) != null && !geo.Row(value.RowKey)!.SplitBySlot
                ? geo.Slots.GroupOfSlot(value.Slot)
                : value.Slot;

            Dictionary<int, int>? bucket;
            int total = 1;
            if (counters.TryGetValue(value.RowKey, out bucket))
            {
                int found;
                bucket.TryGetValue(key, out found);
                if (found > 1)
                {
                    total = found;
                }
            }
            return total;
        }

        // ------------------------------------------------------------ 页眉字段栏

        /// <summary>不建 Painter 时量文本宽，必须与 <see cref="Painter.TextWidth"/> 走同一套字体构造。</summary>
        public static float MeasureText(IFontResolver fonts, string text, FontKey key)
        {
            using (SKFont font = new SKFont(fonts.Resolve(key), key.Size))
            {
                return font.MeasureText(text ?? string.Empty);
            }
        }

        /// <summary>
        /// 按可用宽度贪心分行。Measure 与 Draw 必须调用同一个函数，
        /// 否则行数算少会让字段栏压住日期表头。
        /// </summary>
        public static List<List<int>> WrapFields(float[] widths, float available, float minGap, int maxPerRow)
        {
            List<List<int>> rows = new List<List<int>>();
            if (widths.Length == 0)
            {
                return rows;
            }

            List<int> current = new List<int>();
            float used = 0f;
            for (int i = 0; i < widths.Length; i++)
            {
                float need = current.Count == 0 ? widths[i] : minGap + widths[i];
                bool full = maxPerRow > 0 && current.Count >= maxPerRow;
                if (current.Count > 0 && (used + need > available || full))
                {
                    rows.Add(current);
                    current = new List<int>();
                    used = 0f;
                    need = widths[i];
                }
                current.Add(i);
                used += need;
            }
            rows.Add(current);
            return rows;
        }

        /// <summary>
        /// 画“标签：值”字段栏。每行按实测宽度均分剩余空间两端对齐，
        /// 放不下的字段自动换行，绝不与相邻字段重叠。
        /// </summary>
        public static void DrawHeaderFields(Painter p, SKRect area,
            IReadOnlyList<KeyValuePair<string, string>> fields, SKColor color)
        {
            ChartStyle style = p.Style;
            if (fields.Count == 0 || area.Width <= 0f)
            {
                return;
            }

            FontKey key = new FontKey(style.Text.Family, style.Text.HeaderSize);
            float pad = style.Header.PatientPadding;
            float minGap = style.Header.FieldGap;
            // 末尾留 3px 呼吸位，否则满行时最后一个字会压在页框线上。
            float available = area.Width - pad * 2f - 3f;

            string[] text = new string[fields.Count];
            float[] widths = new float[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                text[i] = fields[i].Key + "：" + fields[i].Value;
                widths[i] = p.TextWidth(text[i], key);
            }

            List<List<int>> rows = WrapFields(widths, available, minGap, style.Header.FieldsPerRow);
            float rowHeight = area.Height / rows.Count;

            for (int r = 0; r < rows.Count; r++)
            {
                List<int> row = rows[r];
                float total = 0f;
                for (int i = 0; i < row.Count; i++)
                {
                    total += widths[row[i]];
                }
                float gap = row.Count > 1
                    ? Math.Max(minGap, (available - total) / (row.Count - 1))
                    : 0f;
                float x = area.Left + pad;
                float top = area.Top + r * rowHeight;
                SKRect line = new SKRect(0f, top, area.Right, top + rowHeight);
                for (int i = 0; i < row.Count; i++)
                {
                    SKRect box = new SKRect(x, line.Top, area.Right, line.Bottom);
                    p.TextInBox(text[row[i]], box, key, color, CellAlign.Near, CellAlign.Center, 0f);
                    x += widths[row[i]] + gap;
                }
            }
        }

        /// <summary>字段栏在给定宽度下需要的行数。</summary>
        public static int HeaderFieldRowCount(IFontResolver fonts,
            IReadOnlyList<KeyValuePair<string, string>> fields, ChartStyle style, float areaWidth)
        {
            if (fields.Count == 0)
            {
                return 0;
            }

            FontKey key = new FontKey(style.Text.Family, style.Text.HeaderSize);
            float[] widths = new float[fields.Count];
            for (int i = 0; i < fields.Count; i++)
            {
                widths[i] = MeasureText(fonts, fields[i].Key + "：" + fields[i].Value, key);
            }

            float available = areaWidth - style.Header.PatientPadding * 2f - 3f;
            return WrapFields(widths, available, style.Header.FieldGap, style.Header.FieldsPerRow).Count;
        }

        // ------------------------------------------------------------ 图例

        /// <summary>画页脚图例：符号 + 文字，横向排布。</summary>
        public static void DrawLegend(Painter p, SKRect band, IEnumerable<LegendItem> items, float startX)
        {
            ChartStyle style = p.Style;
            FontKey key = new FontKey(style.Text.Family, style.Text.LegendSize);
            float x = startX;
            float centerY = band.MidY;

            foreach (LegendItem item in items)
            {
                p.Marker(item.Symbol, new SKPoint(x + style.Series.MarkerRadius, centerY),
                         style.Series.MarkerRadius, item.Color, style.Series.MarkerStroke);
                float textX = x + style.Series.MarkerRadius * 2f + style.Footer.SymbolTextGap;
                p.TextInBox(item.Text, new SKRect(textX, band.Top, band.Right, band.Bottom), key,
                            style.Colors.Legend, CellAlign.Near, CellAlign.Center, 0f);
                x = textX + p.TextWidth(item.Text, key) + style.Footer.ItemGap;
            }
        }
    }
}
