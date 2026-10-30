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
    /// 通用表格式单据模板：页眉 + 若干区段（标题带 / 列头 / 行 / 合并备注）+ 页脚。
    /// 会诊、核查、台账、评分、检验报告都由同一套引擎排版，新增此类单据只需拼区段。
    /// </summary>
    public class FormSheetTemplate : IChartTemplate
    {
        public const string BandHeader = "header";
        public const string BandDate = "date";
        public const string BandCellTable = "sections";
        public const string BandFooter = "footer";

        public const string RowHospital = "hospital";
        public const string RowTitle = "title";
        public const string RowPatient = "patient";

        public virtual string Key => "medical.form-sheet";

        public virtual string DisplayName => "通用表格式单据";

        public FormSheetStyle Tokens { get; set; } = new FormSheetStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            FormSheetDocument doc = Cast(document);
            FormSheetStyle tokens = Tokens;
            GridStyle grid = style.Grid;
            float width = tokens.Width;

            float y = 0f;
            List<BandRow> headerRows = new List<BandRow>();
            headerRows.Add(new BandRow(RowHospital, Row(ref y, style.Header.HospitalRowHeight, width)));
            headerRows.Add(new BandRow(RowTitle, Row(ref y, style.Header.TitleRowHeight, width)));
            int patientRows = Math.Max(1, Primitives.HeaderFieldRowCount(
                fonts, doc.Patient.EnumerateAll(), style, width));
            headerRows.Add(new BandRow(RowPatient,
                Row(ref y, patientRows * style.Header.PatientRowHeight, width)));
            Band header = new Band(BandHeader, new SKRect(0f, 0f, width, y), headerRows);

            Band dateBand = new Band(BandDate, new SKRect(0f, y, width, y), new BandRow[0]);

            float bodyTop = y;
            List<SectionLayout> sections = Layout(doc, fonts, bodyTop);
            float bodyBottom = bodyTop;
            List<BandRow> sectionRows = new List<BandRow>();
            List<CellRow> cellRows = new List<CellRow>();
            for (int i = 0; i < sections.Count; i++)
            {
                SectionLayout layout = sections[i];
                string key = SectionKey(layout.Section, i);
                sectionRows.Add(new BandRow(key, layout.Rect));
                cellRows.Add(new CellRow(key, layout.Section.Title, false, layout.Rect));
                bodyBottom = Math.Max(bodyBottom, layout.Rect.Bottom);
            }

            SKRect bodyRect = new SKRect(0f, bodyTop, width, bodyBottom);
            Band cellTable = new Band(BandCellTable, bodyRect, sectionRows);

            float footerTop = bodyBottom;
            float footerBottom = footerTop + style.Footer.Height;
            Band footer = new Band(BandFooter, new SKRect(0f, footerTop, width, footerBottom),
                new List<BandRow> { new BandRow("footer", new SKRect(0f, footerTop, width, footerBottom)) });

            LinearAxis ruleAxis = new LinearAxis("form", 0d, 1d, bodyTop,
                Math.Max(bodyTop + 1f, bodyBottom), 0.5d, 0.5d);

            return new ChartGeometry(
                new SKRect(0f, 0f, width, footerBottom),
                new SlotAxis(0f, width, 1, 1),
                ruleAxis,
                new LinearAxis[0],
                header, dateBand, bodyRect, new LabelColumn[0],
                cellTable, cellRows, footer);
        }

        private static SKRect Row(ref float y, float height, float right)
        {
            SKRect rect = new SKRect(0f, y, right, y + height);
            y += height;
            return rect;
        }

        private static FormSheetDocument Cast(ChartDocument document)
        {
            FormSheetDocument? typed = document as FormSheetDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(FormSheetDocument) + "。", nameof(document));
            }
            return typed;
        }

        private static string SectionKey(FormSection section, int index)
        {
            if (!string.IsNullOrEmpty(section.Key))
            {
                return section.Key!;
            }
            return "section-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private sealed class RowLayout
        {
            public RowLayout(FormRow row, SKRect rect, List<List<string>> lines)
            {
                Row = row;
                Rect = rect;
                Lines = lines;
            }

            public FormRow Row { get; }
            public SKRect Rect { get; }
            public List<List<string>> Lines { get; }
        }

        private sealed class SectionLayout
        {
            public SectionLayout(FormSection section, SKRect rect)
            {
                Section = section;
                Rect = rect;
            }

            public FormSection Section { get; }
            public SKRect Rect { get; set; }
            public SKRect? TitleRect { get; set; }
            public SKRect? HeaderRect { get; set; }
            public SKRect? NoteRect { get; set; }
            public float[] ColumnWidths { get; set; } = new float[0];
            public List<RowLayout> Rows { get; } = new List<RowLayout>();
        }

        private float LineHeight(float size) => size * Tokens.LineSpacing;

        private List<SectionLayout> Layout(FormSheetDocument doc, IFontResolver fonts, float top)
        {
            FormSheetStyle tokens = Tokens;
            List<SectionLayout> result = new List<SectionLayout>();
            float y = top;

            foreach (FormSection section in doc.Sections)
            {
                SectionLayout layout = new SectionLayout(section, new SKRect(0f, y, tokens.Width, y));

                if (!string.IsNullOrEmpty(section.Title))
                {
                    layout.TitleRect = new SKRect(0f, y, tokens.Width, y + tokens.SectionTitleHeight);
                    y += tokens.SectionTitleHeight;
                }

                float[] widths = ColumnWidths(section);
                layout.ColumnWidths = widths;
                bool hasColumns = section.Columns.Count > 0;
                bool hasHeader = hasColumns && section.ShowColumnHeader;
                if (hasHeader)
                {
                    layout.HeaderRect = new SKRect(0f, y, tokens.Width, y + tokens.ColumnHeaderHeight);
                    y += tokens.ColumnHeaderHeight;
                }

                float rowBase = section.RowHeight ?? tokens.RowHeight;
                FontKey key = new FontKey(null, tokens.TextSize);
                foreach (FormRow row in section.Rows)
                {
                    List<List<string>> lines = new List<List<string>>();
                    int maxLines = 1;
                    float maxSize = tokens.TextSize;
                    for (int c = 0; c < widths.Length; c++)
                    {
                        FormCell cell = c < row.Cells.Count ? row.Cells[c] : new FormCell();
                        float size = cell.Size ?? tokens.TextSize;
                        if (size > maxSize)
                        {
                            maxSize = size;
                        }
                        List<string> cellLines = WrapCell(fonts, cell.Text, key, size,
                            row.Wrap, widths[c] - tokens.CellPadding * 2f);
                        lines.Add(cellLines);
                        if (cellLines.Count > maxLines)
                        {
                            maxLines = cellLines.Count;
                        }
                    }

                    float needed = maxLines * LineHeight(maxSize) + tokens.CellPadding * 2f;
                    float height = row.Height ?? Math.Max(rowBase, needed);
                    RowLayout rowLayout = new RowLayout(row, new SKRect(0f, y, tokens.Width, y + height), lines);
                    layout.Rows.Add(rowLayout);
                    y += height;
                }

                if (!string.IsNullOrEmpty(section.Note))
                {
                    FontKey noteKey = new FontKey(null, tokens.TextSize);
                    List<string> noteLines = WrapCell(fonts, section.Note, noteKey, tokens.TextSize,
                        true, tokens.Width - tokens.CellPadding * 2f);
                    float needed = noteLines.Count * LineHeight(tokens.TextSize) + tokens.CellPadding * 2f;
                    float height = Math.Max(tokens.NoteRowHeight, needed);
                    layout.NoteRect = new SKRect(0f, y, tokens.Width, y + height);
                    y += height;
                }

                layout.Rect = new SKRect(0f, layout.Rect.Top, tokens.Width, y);
                if (layout.Rect.Height <= 0f)
                {
                    continue;
                }
                result.Add(layout);
            }

            return result;
        }

        private float[] ColumnWidths(FormSection section)
        {
            FormSheetStyle tokens = Tokens;
            if (section.Columns.Count == 0)
            {
                return new[] { tokens.Width };
            }

            float total = 0f;
            foreach (FormColumn column in section.Columns)
            {
                total += Math.Max(0.01f, column.Weight);
            }

            float[] widths = new float[section.Columns.Count];
            for (int i = 0; i < section.Columns.Count; i++)
            {
                widths[i] = tokens.Width * Math.Max(0.01f, section.Columns[i].Weight) / total;
            }
            return widths;
        }

        /// <summary>
        /// 按像素宽贪心断行。中日韩字符逐字断，西文单词优先整体换行，
        /// 超长单词再逐字拆开。Measure 与 Draw 共用本函数，保证行高一致。
        /// </summary>
        private static List<string> WrapCell(IFontResolver fonts, string? text, FontKey key,
            float size, bool wrap, float maxWidth)
        {
            List<string> lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                lines.Add(string.Empty);
                return lines;
            }

            FontKey sized = new FontKey(key.Family, size, key.Bold);
            string value = text!;
            if (!wrap || maxWidth <= 0f)
            {
                lines.Add(value);
                return lines;
            }

            if (value.IndexOf('\n') >= 0)
            {
                foreach (string paragraph in value.Split('\n'))
                {
                    lines.AddRange(WrapCell(fonts, paragraph, key, size, true, maxWidth));
                }
                return lines;
            }

            List<string> tokens = Tokenize(value);
            string current = string.Empty;
            bool pendingSpace = false;

            foreach (string token in tokens)
            {
                if (token == " ")
                {
                    pendingSpace = current.Length > 0;
                    continue;
                }

                string candidate = pendingSpace ? current + " " + token : current + token;
                if (current.Length == 0 || Measure(fonts, candidate, sized) <= maxWidth)
                {
                    if (current.Length == 0 && Measure(fonts, token, sized) > maxWidth)
                    {
                        // 单词本身超宽，逐字拆。
                        for (int i = 0; i < token.Length; i++)
                        {
                            string piece = current + char.ToString(token[i]);
                            if (current.Length > 0 && Measure(fonts, piece, sized) > maxWidth)
                            {
                                lines.Add(current);
                                current = char.ToString(token[i]);
                            }
                            else
                            {
                                current = piece;
                            }
                        }
                        pendingSpace = false;
                        continue;
                    }

                    current = candidate;
                    pendingSpace = false;
                    continue;
                }

                lines.Add(current);
                current = token;
                pendingSpace = false;
                if (Measure(fonts, current, sized) > maxWidth)
                {
                    string overflow = current;
                    current = string.Empty;
                    for (int i = 0; i < overflow.Length; i++)
                    {
                        string piece = current + char.ToString(overflow[i]);
                        if (current.Length > 0 && Measure(fonts, piece, sized) > maxWidth)
                        {
                            lines.Add(current);
                            current = char.ToString(overflow[i]);
                        }
                        else
                        {
                            current = piece;
                        }
                    }
                }
            }

            if (current.Length > 0 || lines.Count == 0)
            {
                lines.Add(current);
            }
            return lines;
        }

        private static bool IsCjk(char c)
        {
            return (c >= 0x2E80 && c <= 0x9FFF) || (c >= 0xF900 && c <= 0xFAFF)
                || (c >= 0xFF00 && c <= 0xFFEF) || (c >= 0x3000 && c <= 0x303F);
        }

        private static List<string> Tokenize(string text)
        {
            List<string> tokens = new List<string>();
            System.Text.StringBuilder word = new System.Text.StringBuilder();

            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (word.Length > 0)
                    {
                        tokens.Add(word.ToString());
                        word.Length = 0;
                    }
                    tokens.Add(" ");
                    continue;
                }

                if (IsCjk(c))
                {
                    if (word.Length > 0)
                    {
                        tokens.Add(word.ToString());
                        word.Length = 0;
                    }
                    tokens.Add(char.ToString(c));
                    continue;
                }

                word.Append(c);
            }

            if (word.Length > 0)
            {
                tokens.Add(word.ToString());
            }
            return tokens;
        }

        private static float Measure(IFontResolver fonts, string text, FontKey key)
        {
            return Primitives.MeasureText(fonts, text, key);
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geo, ChartDocument document)
        {
            FormSheetDocument doc = Cast(document);
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;

            DrawHeader(p, geo, doc);
            p.Line(0f, geo.Header.Rect.Bottom, geo.Bounds.Right, geo.Header.Rect.Bottom,
                line, style.Grid.BorderStroke);

            List<SectionLayout> sections = Layout(doc, p.Fonts, geo.CellTable.Rect.Top);
            DrawSections(p, sections);
            p.Frame(geo.CellTable.Rect, line, style.Grid.BorderStroke);

            DrawFooter(p, geo, doc);
            p.Frame(geo.Bounds, line, style.Grid.BorderStroke);
        }

        private static void DrawHeader(Painter p, ChartGeometry geo, FormSheetDocument doc)
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

        private void DrawSections(Painter p, List<SectionLayout> sections)
        {
            FormSheetStyle tokens = Tokens;
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;
            SKColor text = style.Colors.Text;

            foreach (SectionLayout section in sections)
            {
                if (section.TitleRect.HasValue)
                {
                    p.TextInBox(NullToEmpty(section.Section.Title), section.TitleRect.Value,
                        new FontKey(style.Text.Family, tokens.TitleTextSize, true),
                        text, CellAlign.Near, CellAlign.Center, tokens.CellPadding + 2f);
                }

                    if (section.HeaderRect.HasValue)
                    {
                        p.FillRect(section.HeaderRect.Value, text.WithAlpha(24));
                        float x = 0f;
                        for (int c = 0; c < section.Section.Columns.Count; c++)
                        {
                            SKRect cell = new SKRect(x, section.HeaderRect.Value.Top,
                                x + section.ColumnWidths[c], section.HeaderRect.Value.Bottom);
                            p.TextInBox(section.Section.Columns[c].Title, cell,
                                new FontKey(style.Text.Family, tokens.TextSize, section.Section.Columns[c].Bold),
                                text, section.Section.Columns[c].Align, CellAlign.Center, tokens.CellPadding);
                            x += section.ColumnWidths[c];
                        }
                        p.Line(0f, section.HeaderRect.Value.Bottom, tokens.Width,
                            section.HeaderRect.Value.Bottom, line, style.Grid.MinorStroke);
                    }

                    float rowsTop = section.HeaderRect?.Bottom ?? section.Rect.Top;
                    foreach (RowLayout row in section.Rows)
                    {
                        if (row.Row.IsTotal)
                        {
                            p.FillRect(row.Rect, text.WithAlpha(16));
                        }

                        float x = 0f;
                        for (int c = 0; c < section.ColumnWidths.Length; c++)
                        {
                            SKRect cellRect = new SKRect(x, row.Rect.Top, x + section.ColumnWidths[c], row.Rect.Bottom);
                            FormCell cell = c < row.Row.Cells.Count ? row.Row.Cells[c] : new FormCell();
                            FormColumn column = c < section.Section.Columns.Count
                                ? section.Section.Columns[c]
                                : new FormColumn(string.Empty, 1f);
                            float size = cell.Size ?? tokens.TextSize;
                            FontKey key = new FontKey(style.Text.Family, size, cell.Bold || row.Row.IsTotal);
                            DrawLines(p, row.Lines.Count > c ? row.Lines[c] : new List<string> { string.Empty },
                                cellRect, key, cell.Color ?? text, cell.Align ?? column.Align, tokens);
                            x += section.ColumnWidths[c];
                        }

                        p.Line(0f, row.Rect.Bottom, tokens.Width, row.Rect.Bottom, line, style.Grid.MinorStroke);
                    }

                    float contentBottom = section.NoteRect?.Top ?? section.Rect.Bottom;
                    if (section.ColumnWidths.Length > 1 && contentBottom > rowsTop)
                    {
                        float x = 0f;
                        for (int c = 0; c < section.ColumnWidths.Length - 1; c++)
                        {
                            x += section.ColumnWidths[c];
                            p.Line(x, rowsTop, x, contentBottom, line, style.Grid.MinorStroke);
                        }
                    }

                    if (section.NoteRect.HasValue)
                    {
                        p.Line(0f, section.NoteRect.Value.Top, tokens.Width,
                            section.NoteRect.Value.Top, line, style.Grid.MinorStroke);
                        FontKey noteKey = new FontKey(style.Text.Family, tokens.TextSize);
                        List<string> noteLines = SplitNote(p, section.Section.Note, noteKey);
                        DrawLines(p, noteLines, section.NoteRect.Value, noteKey, text,
                            CellAlign.Near, tokens);
                    }

                    p.Line(0f, section.Rect.Bottom, tokens.Width, section.Rect.Bottom,
                        line, style.Grid.MinorStroke);
                }
        }

        /// <summary>用与 Measure 相同的断行算法重算备注行；版面阶段已量过，这里只重算文本。</summary>
        private List<string> SplitNote(Painter p, string? note, FontKey key)
        {
            return WrapCell(p.Fonts, note, key, key.Size, true, Tokens.Width - Tokens.CellPadding * 2f);
        }

        private static void DrawLines(Painter p, List<string> lines, SKRect rect, FontKey key,
            SKColor color, CellAlign align, FormSheetStyle tokens)
        {
            if (lines.Count == 0)
            {
                return;
            }

            if (lines.Count == 1)
            {
                p.TextInBox(lines[0], rect, key, color, align, CellAlign.Center, tokens.CellPadding);
                return;
            }

            float lineHeight = key.Size * tokens.LineSpacing;
            float top = rect.Top + tokens.CellPadding;
            foreach (string line in lines)
            {
                SKRect lineRect = new SKRect(rect.Left, top, rect.Right, top + lineHeight);
                p.TextInBox(line, lineRect, key, color, align, CellAlign.Near, tokens.CellPadding);
                top += lineHeight;
            }
        }

        private void DrawFooter(Painter p, ChartGeometry geo, FormSheetDocument doc)
        {
            ChartStyle style = p.Style;
            SKColor line = style.Colors.Grid;
            SKRect band = geo.Footer.Rect;

            p.Line(0f, band.Top, geo.Bounds.Right, band.Top, line, style.Grid.BorderStroke);

            FontKey key = new FontKey(style.Text.Family, style.Text.AnnotationSize);
            if (!string.IsNullOrEmpty(doc.FooterNote))
            {
                p.TextInBox(doc.FooterNote!, new SKRect(0f, band.Top, geo.Bounds.Right, band.Bottom),
                    key, style.Colors.Text, CellAlign.Near, CellAlign.Center, 8f);
            }

            if (doc.Legend.Count > 0)
            {
                float startX = geo.Bounds.Width / 2f;
                Primitives.DrawLegend(p, band, doc.Legend, startX);
            }
        }

        private static string NullToEmpty(string? value) => value ?? string.Empty;
    }
}
