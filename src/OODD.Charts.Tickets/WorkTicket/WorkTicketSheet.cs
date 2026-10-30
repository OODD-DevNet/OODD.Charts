using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Tickets.WorkTicket
{
    /// <summary>
    /// 作业票的版面求解与落笔（电气第一种工作票与八类特殊作业票共用）。
    /// <para>
    /// 两张票的差别只在数据与令牌：票名、字段目录、措施行、副表（危害辨识）、联次定义、
    /// 印章状态全部由 <see cref="WorkTicketSheetSpec"/> 传入。版面求解是纯函数：
    /// 不读全局状态、不写回文档、连续调用给出逐字段相同的版面。
    /// </para>
    /// <para>
    /// <b>D22</b>：全部尺寸是结构尺寸，未按票样校准。票面栏位次序与联名不得据此声称
    /// 符合 GB 26860 附录 A / GB 30871 附录 A（两处票样均未取证，见 docs/05 §5）。
    /// </para>
    /// </summary>
    public static class WorkTicketSheet
    {
        /// <summary>措施行组的稳定键（供像素用例按语义取行）。</summary>
        public const string MeasureGroupKey = "measures";

        /// <summary>副表行组的稳定键。</summary>
        public const string SecondaryGroupKey = "secondary";

        /// <summary>按当前令牌与数据求版面。</summary>
        /// <param name="spec">票面输入。</param>
        /// <param name="ticket">凭证家族共用令牌（幅面、留白、行高、字号、印章直径）。</param>
        /// <param name="grid">作业票版面令牌。</param>
        /// <returns>整张多联票的版面。</returns>
        public static WorkTicketLayout Build(WorkTicketSheetSpec spec, TicketStyle ticket, WorkTicketGridStyle grid)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            if (spec.Copies.Count == 0)
            {
                throw new ArgumentException("至少需要一联。", nameof(spec));
            }

            Dictionary<string, TicketField> catalog = new Dictionary<string, TicketField>(StringComparer.Ordinal);
            foreach (TicketField field in spec.Fields)
            {
                catalog[field.Key] = field;
            }

            // 字段全集按"联序 + 联内声明序"首次出现排列，与 CollateSpec.AllFields 同一口径。
            List<string> allKeys = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string>[] shownPerCopy = new HashSet<string>[spec.Copies.Count];
            for (int c = 0; c < spec.Copies.Count; c++)
            {
                shownPerCopy[c] = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in spec.Copies[c].FieldKeys)
                {
                    if (!catalog.ContainsKey(key))
                    {
                        throw new ArgumentException(
                            "联 \"" + spec.Copies[c].Key + "\" 引用了字段目录里没有的键 \"" + key + "\"。", nameof(spec));
                    }
                    shownPerCopy[c].Add(key);
                    if (seen.Add(key))
                    {
                        allKeys.Add(key);
                    }
                }
            }

            List<string> commonKeys = new List<string>();
            foreach (string key in allKeys)
            {
                bool every = true;
                for (int c = 0; c < spec.Copies.Count; c++)
                {
                    if (!shownPerCopy[c].Contains(key))
                    {
                        every = false;
                        break;
                    }
                }
                if (every)
                {
                    commonKeys.Add(key);
                }
            }

            int fieldRowCount = allKeys.Count;
            int measureCount = spec.Measures.Count;
            int secondaryCount = spec.Secondary == null ? 0 : spec.Secondary.Rows.Count;
            float subtitleHeight = string.IsNullOrEmpty(spec.Subtitle) ? 0f : grid.SubtitleHeight;

            // 单联高由内容推出（AppendableRowGroup 的行数直接进高度），再与最小高度取大。
            float contentHeight =
                ticket.TitleHeight
                + subtitleHeight
                + grid.SerialHeight
                + fieldRowCount * ticket.RowHeight
                + grid.SectionGap + grid.SectionTitleHeight
                + measureCount * grid.MeasureRowHeight
                + (secondaryCount > 0
                    ? grid.SectionGap + grid.SectionTitleHeight + secondaryCount * grid.SecondaryRowHeight
                    : 0f)
                + grid.SectionGap + grid.SignatureHeight
                + ticket.StampDiameter * 0.5f + grid.StampBottomPad;

            float copyHeight = Math.Max(grid.MinCopyHeight, contentHeight);
            float totalHeight = copyHeight * spec.Copies.Count;
            SKRect bounds = new SKRect(0f, 0f, ticket.Width, totalHeight);

            List<CopyDef> copyDefs = new List<CopyDef>(spec.Copies.Count);
            for (int c = 0; c < spec.Copies.Count; c++)
            {
                List<string> extraKeys = new List<string>();
                foreach (string key in allKeys)
                {
                    if (shownPerCopy[c].Contains(key) && commonKeys.IndexOf(key) < 0)
                    {
                        extraKeys.Add(key);
                    }
                }
                copyDefs.Add(new CopyDef(spec.Copies[c].Key, spec.Copies[c].Label, copyHeight, commonKeys, extraKeys));
            }

            CollateSpec collate = new CollateSpec(copyDefs, grid.Perforation, CollateDirection.VerticalStack);
            if (collate.AllFields.Count != fieldRowCount)
            {
                throw new InvalidOperationException(
                    "字段行网格与 CollateSpec.AllFields 数量不一致：" + fieldRowCount
                    + " vs " + collate.AllFields.Count + "。");
            }

            StampRing ring = CreateStamp(grid);
            string stampText = ResolveStampText(spec);

            List<WorkTicketCopyLayout> copies = new List<WorkTicketCopyLayout>(spec.Copies.Count);
            for (int c = 0; c < spec.Copies.Count; c++)
            {
                copies.Add(BuildCopy(c, collate, bounds, spec, ticket, grid, ring, subtitleHeight, catalog, allKeys));
            }

            return new WorkTicketLayout(bounds, collate, copies, spec.Title, spec.SectionTitle, spec.Signatures,
                spec.StampState, stampText, copyHeight, fieldRowCount, measureCount, secondaryCount,
                spec.Subtitle, spec.UnitLine, spec.SerialText, spec.Secondary == null ? null : spec.Secondary.Title);
        }

        /// <summary>按令牌造章体几何（Measure 与 DrawContent 共用同一套参数）。</summary>
        /// <param name="grid">作业票版面令牌。</param>
        public static StampRing CreateStamp(WorkTicketGridStyle grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            return new StampRing(grid.StampRotationDegrees, grid.StampWithStar, grid.StampWithBar, grid.StampStrokeFactor);
        }

        /// <summary>章内文字：文档给了就用文档的，否则按状态取默认。</summary>
        /// <param name="spec">票面输入。</param>
        public static string ResolveStampText(WorkTicketSheetSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
            if (!string.IsNullOrEmpty(spec.StampText))
            {
                return spec.StampText!;
            }
            switch (spec.StampState)
            {
                case WorkTicketStampState.Executed: return "已执行";
                case WorkTicketStampState.Voided: return "作废";
                default: return "未执行";
            }
        }

        private static WorkTicketCopyLayout BuildCopy(
            int index, CollateSpec collate, SKRect bounds, WorkTicketSheetSpec spec,
            TicketStyle ticket, WorkTicketGridStyle grid, StampRing ring, float subtitleHeight,
            Dictionary<string, TicketField> catalog, List<string> allKeys)
        {
            CopyDef copy = collate.Copies[index];
            SKRect rect = collate.CopyRect(index, bounds);

            SKRect labelRect = new SKRect(
                rect.Left + ticket.Margin, rect.Top + ticket.Margin,
                rect.Left + ticket.BindingMargin, rect.Bottom - ticket.Margin);
            SKRect inner = new SKRect(
                rect.Left + ticket.BindingMargin + grid.CopyLabelWidth, rect.Top + ticket.Margin,
                rect.Right - ticket.Margin, rect.Bottom - ticket.Margin);

            float y = inner.Top;
            SKRect titleRect = NextRow(inner.Left, inner.Right, ref y, ticket.TitleHeight);
            SKRect subtitleRect = subtitleHeight > 0f
                ? NextRow(inner.Left, inner.Right, ref y, subtitleHeight)
                : new SKRect(inner.Left, y, inner.Right, y);
            SKRect serialRect = NextRow(inner.Left, inner.Right, ref y, grid.SerialHeight);

            List<WorkTicketFieldRow> fields = new List<WorkTicketFieldRow>(allKeys.Count);
            for (int r = 0; r < allKeys.Count; r++)
            {
                string key = allKeys[r];
                SKRect rowRect = NextRow(inner.Left, inner.Right, ref y, ticket.RowHeight);
                TicketField field = catalog[key];
                fields.Add(new WorkTicketFieldRow(r, key, field.Label, field.Value, rowRect, copy.ShowsField(key)));
            }

            y += grid.SectionGap;
            SKRect sectionTitleRect = NextRow(inner.Left, inner.Right, ref y, grid.SectionTitleHeight);

            AppendableRowGroup measureGroup = new AppendableRowGroup(
                MeasureGroupKey, spec.Measures.Count, grid.MeasureRowHeight, inner.Left, y, inner.Right);
            List<WorkTicketMeasureLayout> measures = new List<WorkTicketMeasureLayout>(spec.Measures.Count);
            for (int m = 0; m < spec.Measures.Count; m++)
            {
                SKRect row = measureGroup.Row(m);
                SKRect indexRect = new SKRect(row.Left, row.Top, row.Left + grid.MeasureIndexWidth, row.Bottom);
                SKRect stateRect = new SKRect(row.Right - grid.MeasureStateWidth, row.Top, row.Right, row.Bottom);
                SKRect textRect = new SKRect(indexRect.Right, row.Top, stateRect.Left, row.Bottom);
                SKRect boxRect = new TriStateBox(spec.Measures[m].State)
                    .BoxRect(new SKPoint(stateRect.MidX, stateRect.MidY), ticket.CheckBoxSize);
                measures.Add(new WorkTicketMeasureLayout(m, spec.Measures[m].Text, spec.Measures[m].State,
                    row, indexRect, textRect, stateRect, boxRect));
            }
            y = measureGroup.Bounds.Bottom;

            SKRect? secondaryTitleRect = null;
            AppendableRowGroup? secondaryGroup = null;
            List<WorkTicketTextRow> secondaryRows = new List<WorkTicketTextRow>();
            if (spec.Secondary != null && spec.Secondary.Rows.Count > 0)
            {
                y += grid.SectionGap;
                secondaryTitleRect = NextRow(inner.Left, inner.Right, ref y, grid.SectionTitleHeight);
                secondaryGroup = new AppendableRowGroup(SecondaryGroupKey, spec.Secondary.Rows.Count,
                    grid.SecondaryRowHeight, inner.Left, y, inner.Right);
                for (int s = 0; s < spec.Secondary.Rows.Count; s++)
                {
                    SKRect row = secondaryGroup.Row(s);
                    SKRect indexRect = new SKRect(row.Left, row.Top, row.Left + grid.SecondaryIndexWidth, row.Bottom);
                    SKRect textRect = new SKRect(indexRect.Right, row.Top, row.Right, row.Bottom);
                    secondaryRows.Add(new WorkTicketTextRow(s, spec.Secondary.Rows[s], row, indexRect, textRect));
                }
                y = secondaryGroup.Bounds.Bottom;
            }

            y += grid.SectionGap;
            SKRect signatureRect = NextRow(inner.Left, inner.Right, ref y, grid.SignatureHeight);

            List<SKRect> signatureCells = new List<SKRect>();
            if (spec.Signatures.Count > 0)
            {
                float[] columnBounds = CellGeometry.UniformBounds(
                    signatureRect.Left, signatureRect.Right, spec.Signatures.Count);
                for (int i = 0; i < spec.Signatures.Count; i++)
                {
                    signatureCells.Add(new SKRect(columnBounds[i], signatureRect.Top,
                        columnBounds[i + 1], signatureRect.Bottom));
                }
            }

            SKPoint stampCenter = new SKPoint(
                inner.Left + inner.Width * grid.StampXFactor, signatureRect.MidY);
            SKRect stampBounds = ring.BoundsOf(stampCenter, ticket.StampDiameter);

            return new WorkTicketCopyLayout(index, copy.Key, copy.Label, rect, labelRect, inner,
                titleRect, subtitleRect, serialRect, sectionTitleRect, signatureRect,
                stampCenter, stampBounds, fields, measureGroup, measures,
                secondaryTitleRect, secondaryGroup, secondaryRows, signatureCells);
        }

        private static SKRect NextRow(float left, float right, ref float y, float height)
        {
            SKRect rect = new SKRect(left, y, right, y + height);
            y += height;
            return rect;
        }

        // ------------------------------------------------------------------ 绘制

        /// <summary>把整张多联票画到画布上。所有落点取自 <paramref name="layout"/>，不重算版面。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="layout">版面。</param>
        /// <param name="ticket">凭证家族共用令牌。</param>
        /// <param name="grid">作业票版面令牌。</param>
        /// <param name="palette">配色。</param>
        public static void Draw(Painter painter, WorkTicketLayout layout, TicketStyle ticket,
            WorkTicketGridStyle grid, TicketPalette palette)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (ticket == null)
            {
                throw new ArgumentNullException(nameof(ticket));
            }
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            ChartStyleText text = ChartStyleText.Of(painter, ticket);
            painter.FillRect(layout.Bounds, palette.PageBackground);

            foreach (WorkTicketCopyLayout copy in layout.Copies)
            {
                DrawCopy(painter, layout, copy, ticket, grid, palette, text);
            }

            DrawPerforation(painter, layout, ticket, grid, palette);
        }

        private static void DrawCopy(Painter painter, WorkTicketLayout layout, WorkTicketCopyLayout copy,
            TicketStyle ticket, WorkTicketGridStyle grid, TicketPalette palette, ChartStyleText text)
        {
            // 联框与装订带。
            painter.Frame(copy.Rect, palette.Ink, ticket.BorderStroke);
            painter.Line(copy.LabelRect.Right, copy.Rect.Top + ticket.Margin,
                copy.LabelRect.Right, copy.Rect.Bottom - ticket.Margin, palette.Ink, ticket.BorderStroke);

            // 联名逐字竖排（不用 canvas 旋转：旋转文字在 PDF/SVG 里会退化成路径）。
            painter.VerticalText(copy.CopyLabel, copy.LabelRect.MidX,
                copy.LabelRect.Top + grid.CellPadding, text.CopyLabel, palette.MutedText);

            // 票名。
            painter.TextInBox(layout.Title, copy.TitleRect, text.Title, palette.TitleText,
                CellAlign.Center, CellAlign.Center, 0f);

            if (layout.Subtitle != null && copy.SubtitleRect.Height > 0f)
            {
                painter.TextInBox(layout.Subtitle, copy.SubtitleRect, text.Serial, palette.TitleText,
                    CellAlign.Center, CellAlign.Center, 0f);
            }

            if (layout.UnitLine != null)
            {
                painter.TextInBox(layout.UnitLine, copy.SerialRect, text.Serial, palette.Text,
                    CellAlign.Near, CellAlign.Center, grid.CellPadding);
            }
            if (layout.SerialText != null)
            {
                painter.TextInBox(layout.SerialText, copy.SerialRect, text.Serial, palette.Text,
                    CellAlign.Far, CellAlign.Center, grid.CellPadding);
            }
            painter.Line(copy.SerialRect.Left, copy.SerialRect.Bottom, copy.SerialRect.Right,
                copy.SerialRect.Bottom, palette.Grid, ticket.GridStroke);

            DrawFields(painter, copy, ticket, grid, palette, text);
            DrawMeasures(painter, layout, copy, ticket, grid, palette, text);
            DrawSecondary(painter, layout, copy, ticket, grid, palette, text);
            DrawSignatures(painter, layout, copy, ticket, grid, palette, text);
            DrawStamp(painter, layout, copy, ticket, grid, palette, text);
        }

        private static void DrawFields(Painter painter, WorkTicketCopyLayout copy, TicketStyle ticket,
            WorkTicketGridStyle grid, TicketPalette palette, ChartStyleText text)
        {
            foreach (WorkTicketFieldRow row in copy.Fields)
            {
                SKRect labelRect = new SKRect(row.Rect.Left, row.Rect.Top,
                    row.Rect.Left + grid.FieldLabelWidth, row.Rect.Bottom);
                if (row.Shown)
                {
                    painter.TextInBox(row.Label + "：", labelRect, text.FieldLabel, palette.Text,
                        CellAlign.Near, CellAlign.Center, grid.CellPadding);
                    if (!string.IsNullOrEmpty(row.Value))
                    {
                        SKRect valueRect = new SKRect(labelRect.Right + grid.LabelValueGap, row.Rect.Top,
                            row.Rect.Right, row.Rect.Bottom);
                        painter.TextInBox(row.Value!, valueRect, text.FieldValue, palette.Text,
                            CellAlign.Near, CellAlign.Center, 0f);
                    }
                }

                painter.Line(labelRect.Right, row.Rect.Top, labelRect.Right, row.Rect.Bottom,
                    palette.Grid, ticket.GridStroke);
                painter.Line(row.Rect.Left, row.Rect.Bottom, row.Rect.Right, row.Rect.Bottom,
                    palette.Grid, ticket.GridStroke);
            }
        }

        private static void DrawMeasures(Painter painter, WorkTicketLayout layout, WorkTicketCopyLayout copy,
            TicketStyle ticket, WorkTicketGridStyle grid, TicketPalette palette, ChartStyleText text)
        {
            painter.TextInBox(layout.SectionTitle, copy.SectionTitleRect, text.SectionTitle, palette.TitleText,
                CellAlign.Near, CellAlign.Center, grid.CellPadding);
            painter.TextInBox("确认：符合√　不涉及×", copy.SectionTitleRect, text.Serial, palette.MutedText,
                CellAlign.Far, CellAlign.Center, grid.CellPadding);

            foreach (WorkTicketMeasureLayout row in copy.Measures)
            {
                painter.TextInBox((row.Index + 1).ToString(CultureInfo.InvariantCulture), row.IndexRect,
                    text.FieldLabel, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
                painter.TextInBox(row.Text, row.TextRect, text.FieldValue, palette.Text,
                    CellAlign.Near, CellAlign.Center, grid.CellPadding);

                TriStateBox box = new TriStateBox(row.State);
                box.Draw(painter, new SKPoint(row.BoxRect.MidX, row.BoxRect.MidY), ticket.CheckBoxSize,
                    palette.CheckBox);

                painter.Line(row.IndexRect.Right, row.Row.Top, row.IndexRect.Right, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
                painter.Line(row.StateRect.Left, row.Row.Top, row.StateRect.Left, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
                painter.Line(row.Row.Left, row.Row.Bottom, row.Row.Right, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
            }
        }

        private static void DrawSecondary(Painter painter, WorkTicketLayout layout, WorkTicketCopyLayout copy,
            TicketStyle ticket, WorkTicketGridStyle grid, TicketPalette palette, ChartStyleText text)
        {
            if (copy.SecondaryTitleRect == null || copy.SecondaryGroup == null)
            {
                return;
            }

            painter.TextInBox(layout.SecondaryTitle ?? string.Empty, copy.SecondaryTitleRect.Value,
                text.SectionTitle, palette.TitleText, CellAlign.Near, CellAlign.Center, grid.CellPadding);

            foreach (WorkTicketTextRow row in copy.SecondaryRows)
            {
                painter.TextInBox((row.Index + 1).ToString(CultureInfo.InvariantCulture), row.IndexRect,
                    text.FieldLabel, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
                painter.TextInBox(row.Text, row.TextRect, text.FieldValue, palette.Text,
                    CellAlign.Near, CellAlign.Center, grid.CellPadding);
                painter.Line(row.IndexRect.Right, row.Row.Top, row.IndexRect.Right, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
                painter.Line(row.Row.Left, row.Row.Bottom, row.Row.Right, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
            }
        }

        private static void DrawSignatures(Painter painter, WorkTicketLayout layout, WorkTicketCopyLayout copy,
            TicketStyle ticket, WorkTicketGridStyle grid, TicketPalette palette, ChartStyleText text)
        {
            for (int i = 0; i < copy.SignatureCells.Count; i++)
            {
                SKRect cell = copy.SignatureCells[i];
                painter.TextInBox(layout.Signatures[i] + "：", cell, text.FieldLabel, palette.Text,
                    CellAlign.Near, CellAlign.Center, grid.CellPadding);
                if (i > 0)
                {
                    painter.Line(cell.Left, cell.Top, cell.Left, cell.Bottom, palette.Grid, ticket.GridStroke);
                }
            }
            painter.Line(copy.SignatureRect.Left, copy.SignatureRect.Bottom, copy.SignatureRect.Right,
                copy.SignatureRect.Bottom, palette.Grid, ticket.GridStroke);
        }

        private static void DrawStamp(Painter painter, WorkTicketLayout layout, WorkTicketCopyLayout copy,
            TicketStyle ticket, WorkTicketGridStyle grid, TicketPalette palette, ChartStyleText text)
        {
            bool unexecuted = layout.StampState == WorkTicketStampState.NotExecuted;
            SKColor color = unexecuted ? palette.Perforation : palette.Stamp;

            StampRing ring = CreateStamp(grid);
            ring.Draw(painter, copy.StampCenter, ticket.StampDiameter, color);

            if (layout.StampState == WorkTicketStampState.Voided)
            {
                // "作废"章的现实形态是在章面上交叉划两笔：两笔都是几何，不写文字。
                float reach = ticket.StampDiameter * 0.30f;
                painter.Line(copy.StampCenter.X - reach, copy.StampCenter.Y - reach,
                    copy.StampCenter.X + reach, copy.StampCenter.Y + reach, color, ticket.StampStroke);
                painter.Line(copy.StampCenter.X + reach, copy.StampCenter.Y - reach,
                    copy.StampCenter.X - reach, copy.StampCenter.Y + reach, color, ticket.StampStroke);
            }

            // 章内文字直立：旋转只作用于章体几何（docs/06 D20）。
            float baseline = copy.StampCenter.Y + ticket.StampDiameter * grid.StampTextOffsetFactor;
            float width = painter.TextWidth(layout.StampText, text.Stamp);
            painter.Text(layout.StampText, copy.StampCenter.X - width * 0.5f, baseline, text.Stamp, color);
        }

        private static void DrawPerforation(Painter painter, WorkTicketLayout layout, TicketStyle ticket,
            WorkTicketGridStyle grid, TicketPalette palette)
        {
            for (int seam = 1; seam < layout.Collate.Count; seam++)
            {
                float? y = layout.Collate.PerforationAt(seam, layout.Bounds);
                if (!y.HasValue)
                {
                    continue;
                }
                painter.DashedLine(layout.Bounds.Left + ticket.Margin, y.Value,
                    layout.Bounds.Right - ticket.Margin, y.Value, palette.Perforation, grid.PerforationStroke);
            }
        }

        /// <summary>
        /// 票面用到的字体键。集中在一处，避免 Measure 与绘制各拼一套字号。
        /// </summary>
        private sealed class ChartStyleText
        {
            private ChartStyleText(FontKey title, FontKey sectionTitle, FontKey serial,
                FontKey fieldLabel, FontKey fieldValue, FontKey stamp, FontKey copyLabel)
            {
                Title = title;
                SectionTitle = sectionTitle;
                Serial = serial;
                FieldLabel = fieldLabel;
                FieldValue = fieldValue;
                Stamp = stamp;
                CopyLabel = copyLabel;
            }

            public FontKey Title { get; }
            public FontKey SectionTitle { get; }
            public FontKey Serial { get; }
            public FontKey FieldLabel { get; }
            public FontKey FieldValue { get; }
            public FontKey Stamp { get; }
            public FontKey CopyLabel { get; }

            public static ChartStyleText Of(Painter painter, TicketStyle ticket)
            {
                string? family = painter.Style.Text.Family;
                return new ChartStyleText(
                    new FontKey(family, ticket.TitleSize, true),
                    new FontKey(family, ticket.FieldTextSize, true),
                    new FontKey(family, ticket.SerialSize),
                    new FontKey(family, ticket.FieldLabelSize),
                    new FontKey(family, ticket.FieldTextSize),
                    new FontKey(family, ticket.StampSize, true),
                    new FontKey(family, ticket.SerialSize));
            }
        }
    }
}
