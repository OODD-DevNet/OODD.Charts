using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms.VaccinationBooklet
{
    /// <summary>预防接种证一个开面的求解结果。</summary>
    public sealed class VaccinationBookletLayout
    {
        private readonly VaccinationBookletStyle _tokens;

        internal VaccinationBookletLayout(BookletImposition imposition, int spreadIndex, Spread spread,
            SKRect bounds, SKRect left, SKRect right, IReadOnlyList<VaccinationScheduleRow> scheduleRows,
            IReadOnlyList<int> recordPages, VaccinationBookletStyle tokens)
        {
            Imposition = imposition;
            SpreadIndex = spreadIndex;
            CurrentSpread = spread;
            Bounds = bounds;
            LeftRect = left;
            RightRect = right;
            ScheduleRows = scheduleRows;
            RecordPages = recordPages;
            _tokens = tokens;
        }

        public BookletImposition Imposition { get; }
        public int SpreadIndex { get; }
        public Spread CurrentSpread { get; }
        public int LeftPage => CurrentSpread.LeftLogicalPage;
        public int RightPage => CurrentSpread.RightLogicalPage;

        public SKRect Bounds { get; }
        public SKRect LeftRect { get; }
        public SKRect RightRect { get; }

        /// <summary>预印疫苗行（数据驱动，数量来自文档的国家免疫规划表）。</summary>
        public IReadOnlyList<VaccinationScheduleRow> ScheduleRows { get; }

        /// <summary>接种记录页的逻辑页号，按阅读顺序（3、10、4、9、5、8）。</summary>
        public IReadOnlyList<int> RecordPages { get; }

        public int RowsPerPage => Math.Max(1, _tokens.ScheduleRowsPerPage);

        public SKRect PageRect(int logicalPage)
        {
            if (logicalPage == LeftPage) return LeftRect;
            if (logicalPage == RightPage) return RightRect;
            throw new ArgumentOutOfRangeException(nameof(logicalPage),
                "逻辑页 " + logicalPage + " 不在当前开面（L=" + LeftPage + " R=" + RightPage + "）。");
        }

        /// <summary>某逻辑页上的记录表列头矩形；不在当前开面时抛异常。</summary>
        public SKRect TableHeaderRect(int logicalPage)
        {
            SKRect page = PageRect(logicalPage);
            return new SKRect(page.Left + _tokens.PageMargin, page.Top + _tokens.TableTop,
                page.Right - _tokens.PageMargin, page.Top + _tokens.TableTop + _tokens.TableHeaderHeight);
        }

        /// <summary>第 globalIndex 条预印行的矩形；超出记录页容量时返回空矩形。</summary>
        public SKRect ScheduleRowRect(int globalIndex)
        {
            if (globalIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(globalIndex));
            }
            int pageIndex = globalIndex / RowsPerPage;
            if (pageIndex >= RecordPages.Count)
            {
                return SKRect.Empty;
            }
            int page = RecordPages[pageIndex];
            SKRect pageRect = PageRect(page);
            int row = globalIndex % RowsPerPage;
            float top = pageRect.Top + _tokens.TableTop + _tokens.TableHeaderHeight
                        + row * RecordRowHeight;
            return new SKRect(pageRect.Left + _tokens.PageMargin, top,
                pageRect.Right - _tokens.PageMargin, top + RecordRowHeight);
        }

        /// <summary>
        /// 实际行高：<see cref="VaccinationBookletStyle.FillRecordPage"/> 打开时把 RowsPerPage 行
        /// 均摊到数据行区，整页不空；关闭时退回令牌定高。<see cref="VaccinationBookletStyle.RecordRowHeight"/> 只作下限。
        /// </summary>
        public float RecordRowHeight
        {
            get
            {
                if (!_tokens.FillRecordPage)
                {
                    return _tokens.RecordRowHeight;
                }
                int page = LeftPage > 0 ? LeftPage : RightPage;
                if (page <= 0)
                {
                    return _tokens.RecordRowHeight;
                }
                float available = RecordRowsRect(page).Height;
                return Math.Max(_tokens.RecordRowHeight, available / RowsPerPage);
            }
        }

        /// <summary>记录表整表外框：自 TableTop 起，到页底 <c>RecordTableBottomMargin</c> 之上。</summary>
        public SKRect RecordTableRect(int logicalPage)
        {
            SKRect page = PageRect(logicalPage);
            return new SKRect(page.Left + _tokens.PageMargin, page.Top + _tokens.TableTop,
                page.Right - _tokens.PageMargin, page.Bottom - _tokens.RecordTableBottomMargin);
        }

        /// <summary>
        /// 记录表数据行区外框（表头之下到表底）。续页不画列头，只给这一圈边框，
        /// 于是「续页没有表格」的问题消除，而列头带仍保持空白。
        /// </summary>
        public SKRect RecordRowsRect(int logicalPage)
        {
            SKRect table = RecordTableRect(logicalPage);
            return new SKRect(table.Left, table.Top + _tokens.TableHeaderHeight, table.Right, table.Bottom);
        }

        /// <summary>第 globalIndex 条预印行所在的逻辑页。</summary>
        public int PageOfRow(int globalIndex)
        {
            int pageIndex = globalIndex / RowsPerPage;
            return pageIndex < RecordPages.Count ? RecordPages[pageIndex] : 0;
        }

        /// <summary>第 column 列的列宽比例（归一化前由令牌给出）。</summary>
        public float ColumnWeight(int column)
        {
            float[] weights = _tokens.ColumnWeights;
            return weights[Math.Max(0, Math.Min(weights.Length - 1, column))];
        }

        /// <summary>专色色块矩形（在基本信息页 11 上）；该页不在当前开面时为空。</summary>
        public SKRect SpotSwatchRect
        {
            get
            {
                if (LeftPage != 11 && RightPage != 11)
                {
                    return SKRect.Empty;
                }
                SKRect page = PageRect(11);
                float size = 16f;
                return new SKRect(page.Right - _tokens.PageMargin - size,
                    page.Bottom - _tokens.PageMargin - size,
                    page.Right - _tokens.PageMargin, page.Bottom - _tokens.PageMargin);
            }
        }
    }

    /// <summary>
    /// 预防接种证 medical.vaccination-booklet。
    /// <para>
    /// 开面配对交给 <see cref="BookletImposition"/>（封二+基本信息、记录一+二…）；
    /// 表格列头只在记录首页画一次，跨开面不重复。专色只以近似 RGB 呈现，
    /// 原始 CMYK 值保留在文档字段里。锁定令牌 <c>AllowLayoutDeviation=false</c> 时
    /// <see cref="AddField"/> / <see cref="RemoveField"/> 直接抛异常。
    /// </para>
    /// </summary>
    public sealed class VaccinationBookletTemplate : IChartTemplate
    {
        public const int FirstRecordPage = 3;

        public static readonly IReadOnlyList<int> RecordPageOrder = new List<int> { 3, 10, 4, 9, 5, 8 }.AsReadOnly();

        public string Key => "medical.vaccination-booklet";
        public string DisplayName => "预防接种证";

        /// <summary>表单专属令牌；改令牌即改版面。</summary>
        public VaccinationBookletStyle Tokens { get; set; } = new VaccinationBookletStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            VaccinationBookletDocument doc = Cast(document);
            VaccinationBookletStyle t = Tokens;
            ValidateColumnWeights(t);

            float pageWidth = Unit.Mm(t.PageWidthMm);
            float pageHeight = Unit.Mm(t.PageHeightMm);

            BookletImposition imposition = BookletImposition.SaddleStitch(doc.TotalLogicalPages);
            int index = doc.SpreadIndex;
            if (index < 0) index = 0;
            if (index >= imposition.Spreads.Count) index = imposition.Spreads.Count - 1;
            Spread spread = imposition.Spreads[index];

            SKRect left = new SKRect(0f, 0f, pageWidth, pageHeight);
            SKRect right = new SKRect(pageWidth, 0f, pageWidth * 2f, pageHeight);

            VaccinationBookletLayout layout = new VaccinationBookletLayout(
                imposition, index, spread, new SKRect(0f, 0f, pageWidth * 2f, pageHeight),
                left, right, new List<VaccinationScheduleRow>(doc.NationalSchedule), RecordPageOrder, t);

            return ChartGeometry.ForLayout(layout.Bounds, layout.Bounds, layout);
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            VaccinationBookletDocument doc = Cast(document);
            VaccinationBookletLayout layout = geometry.LayoutOf<VaccinationBookletLayout>()
                ?? throw new InvalidOperationException("几何里没有 VaccinationBookletLayout。");

            ChartStyle style = painter.Style;
            VaccinationBookletStyle t = Tokens;
            SKColor line = style.Colors.Grid;

            DrawPage(painter, layout, doc, style, t, layout.LeftPage, layout.LeftRect, line);
            DrawPage(painter, layout, doc, style, t, layout.RightPage, layout.RightRect, line);

            // 开面中缝
            painter.Line(layout.LeftRect.Right, 0f, layout.LeftRect.Right, layout.Bounds.Bottom,
                line, style.Grid.BorderStroke);
            painter.Rect(layout.Bounds, line, style.Grid.BorderStroke);
        }

        private void DrawPage(Painter p, VaccinationBookletLayout layout, VaccinationBookletDocument doc,
            ChartStyle style, VaccinationBookletStyle t, int page, SKRect rect, SKColor line)
        {
            if (page <= 0)
            {
                return;
            }

            FontKey titleKey = new FontKey(style.Text.Family, t.TitleSize, true);
            FontKey bodyKey = new FontKey(style.Text.Family, t.BodySize);

            if (page == 1)
            {
                p.TextInBox("预防接种证", new SKRect(rect.Left, rect.Top + 80f, rect.Right, rect.Top + 130f),
                    titleKey, t.SpotColor, CellAlign.Center, CellAlign.Center, 0f);
                p.TextInBox("Vaccination Certificate",
                    new SKRect(rect.Left, rect.Top + 132f, rect.Right, rect.Top + 154f),
                    bodyKey, t.SpotColor, CellAlign.Center, CellAlign.Center, 0f);
                p.TextInBox(doc.Patient.Name ?? string.Empty,
                    new SKRect(rect.Left, rect.Bottom - 90f, rect.Right, rect.Bottom - 64f),
                    bodyKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            }
            else if (page == 2)
            {
                DrawTextBlock(p, "使用说明 / Instructions",
                    new SKRect(rect.Left + t.PageMargin, rect.Top + 26f, rect.Right - t.PageMargin,
                        rect.Top + 52f), titleKey, t.SpotColor);
                float y = rect.Top + 66f;
                p.TextInBox("本证记录国家免疫规划疫苗接种信息，请妥善保管。",
                    new SKRect(rect.Left + t.PageMargin, y, rect.Right - t.PageMargin, y + 18f),
                    bodyKey, style.Colors.Text, CellAlign.Near, CellAlign.Center, 0f);
                for (int i = 0; i < doc.Assessments.Count; i++)
                {
                    float top = y + 22f + i * 18f;
                    p.TextInBox(doc.Assessments[i],
                        new SKRect(rect.Left + t.PageMargin, top, rect.Right - t.PageMargin, top + 18f),
                        bodyKey, style.Colors.Text, CellAlign.Near, CellAlign.Center, 0f);
                }
            }
            else if (page == 11)
            {
                DrawTextBlock(p, "基本信息 / Basic Information",
                    new SKRect(rect.Left + t.PageMargin, rect.Top + 22f, rect.Right - t.PageMargin,
                        rect.Top + 46f), titleKey, t.SpotColor);

                IReadOnlyList<KeyValuePair<string, string>> fields = doc.Patient.EnumerateAll();
                float top = rect.Top + 58f;
                for (int i = 0; i < fields.Count; i++)
                {
                    p.TextInBox(fields[i].Key + "：" + fields[i].Value,
                        new SKRect(rect.Left + t.PageMargin, top, rect.Right - t.PageMargin, top + 20f),
                        bodyKey, style.Colors.Text, CellAlign.Near, CellAlign.Center, 0f);
                    top += 20f;
                }

                SKRect swatch = layout.SpotSwatchRect;
                if (!swatch.IsEmpty)
                {
                    p.FillRect(swatch, t.SpotColor);
                    p.TextInBox("专色 " + doc.SpotColorCmyk,
                        new SKRect(rect.Left + t.PageMargin, swatch.Top, swatch.Left - 4f, swatch.Bottom),
                        bodyKey, style.Colors.Text, CellAlign.Far, CellAlign.Center, 0f);
                }
            }
            else if (RecordPageOrderContains(page))
            {
                DrawRecordPage(p, layout, doc, style, t, page, rect, line);
            }
            else if (page == 6)
            {
                DrawTextBlock(p, "接种评估 / Assessment",
                    new SKRect(rect.Left + t.PageMargin, rect.Top + 30f, rect.Right - t.PageMargin,
                        rect.Top + 56f), titleKey, t.SpotColor);
            }
            else if (page == 7)
            {
                DrawTextBlock(p, "备注 / Remarks",
                    new SKRect(rect.Left + t.PageMargin, rect.Top + 30f, rect.Right - t.PageMargin,
                        rect.Top + 56f), titleKey, t.SpotColor);
            }
            else
            {
                p.TextInBox("接种单位（盖章）",
                    new SKRect(rect.Left + t.PageMargin, rect.Bottom - 60f, rect.Right - t.PageMargin,
                        rect.Bottom - 34f),
                    bodyKey, style.Colors.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            p.TextInBox(page.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new SKRect(rect.Left, rect.Bottom - 18f, rect.Right, rect.Bottom - 2f),
                new FontKey(style.Text.Family, style.Text.AnnotationSize),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
        }

        private void DrawRecordPage(Painter p, VaccinationBookletLayout layout, VaccinationBookletDocument doc,
            ChartStyle style, VaccinationBookletStyle t, int page, SKRect rect, SKColor line)
        {
            FontKey headKey = new FontKey(style.Text.Family, style.Text.CellLabelSize);
            FontKey bodyKey = new FontKey(style.Text.Family, t.BodySize);

            DrawTextBlock(p, "接种记录 / Vaccination Record",
                new SKRect(rect.Left + t.PageMargin, rect.Top + 20f, rect.Right - t.PageMargin,
                    rect.Top + 44f),
                new FontKey(style.Text.Family, t.TitleSize, true), t.SpotColor);

            if (page == FirstRecordPage)
            {
                SKRect header = layout.TableHeaderRect(page);
                string[] zh = { "疫苗名称", "剂次", "接种日期", "生产企业", "批号" };
                string[] en = { "Vaccine", "Dose", "Date", "Manufacturer", "Lot" };
                float[] bounds = ColumnBounds(header, t, layout);
                for (int c = 0; c < zh.Length; c++)
                {
                    SKRect cell = new SKRect(bounds[c], header.Top, bounds[c + 1], header.Bottom);
                    p.Frame(cell, line, style.Grid.MinorStroke);
                    p.TextInBox(zh[c], new SKRect(cell.Left, cell.Top, cell.Right, cell.MidY),
                        headKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                    p.TextInBox(en[c], new SKRect(cell.Left, cell.MidY, cell.Right, cell.Bottom),
                        new FontKey(style.Text.Family, style.Text.TimeSize),
                        style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
                }
            }

            // 首记录页画整表外框（含列头带），续页只画数据行区外框：列头不重复，但页面仍有表格。
            p.Frame(page == FirstRecordPage ? layout.RecordTableRect(page) : layout.RecordRowsRect(page),
                line, style.Grid.MinorStroke);

            float[] columns = ColumnBounds(
                new SKRect(rect.Left + t.PageMargin, 0f, rect.Right - t.PageMargin, 0f), t, layout);
            SKRect rows = layout.RecordRowsRect(page);
            for (int c = 1; c < columns.Length; c++)
            {
                p.Line(columns[c], rows.Top, columns[c], rows.Bottom, line, style.Grid.MinorStroke);
            }

            for (int i = 0; i < layout.ScheduleRows.Count; i++)
            {
                if (layout.PageOfRow(i) != page)
                {
                    continue;
                }

                SKRect row = layout.ScheduleRowRect(i);
                p.Line(row.Left, row.Bottom, row.Right, row.Bottom, line, style.Grid.MinorStroke);

                VaccinationScheduleRow pre = layout.ScheduleRows[i];
                p.TextInBox(pre.Vaccine, new SKRect(columns[0], row.Top, columns[1], row.Bottom),
                    bodyKey, style.Colors.Text, CellAlign.Near, CellAlign.Center, 2f);
                p.TextInBox(pre.Dose, new SKRect(columns[1], row.Top, columns[2], row.Bottom),
                    bodyKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);

                VaccinationRecord? record = FindRecord(doc, pre);
                if (record != null)
                {
                    p.TextInBox(record.Date, new SKRect(columns[2], row.Top, columns[3], row.Bottom),
                        bodyKey, style.Colors.CellValue, CellAlign.Center, CellAlign.Center, 1f);
                    p.TextInBox(record.Manufacturer, new SKRect(columns[3], row.Top, columns[4], row.Bottom),
                        bodyKey, style.Colors.CellValue, CellAlign.Near, CellAlign.Center, 2f);
                    p.TextInBox(record.Lot, new SKRect(columns[4], row.Top, columns[5], row.Bottom),
                        bodyKey, style.Colors.CellValue, CellAlign.Center, CellAlign.Center, 1f);
                }
            }
        }

        private static void DrawTextBlock(Painter p, string text, SKRect rect, FontKey key, SKColor color)
        {
            p.TextInBox(text, rect, key, color, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static float[] ColumnBounds(SKRect header, VaccinationBookletStyle t,
            VaccinationBookletLayout layout)
        {
            float total = 0f;
            for (int i = 0; i < 5; i++) total += layout.ColumnWeight(i);
            float[] bounds = new float[6];
            bounds[0] = header.Left;
            float cursor = header.Left;
            for (int i = 0; i < 5; i++)
            {
                float width = i == 4
                    ? header.Right - cursor
                    : header.Width * (layout.ColumnWeight(i) / total);
                cursor += width;
                bounds[i + 1] = cursor;
            }
            return bounds;
        }

        private static VaccinationRecord? FindRecord(VaccinationBookletDocument doc, VaccinationScheduleRow row)
        {
            for (int i = 0; i < doc.Records.Count; i++)
            {
                VaccinationRecord record = doc.Records[i];
                if (string.Equals(record.Vaccine, row.Vaccine, StringComparison.Ordinal)
                    && string.Equals(record.Dose, row.Dose, StringComparison.Ordinal))
                {
                    return record;
                }
            }
            return null;
        }

        private static bool RecordPageOrderContains(int page)
        {
            for (int i = 0; i < RecordPageOrder.Count; i++)
            {
                if (RecordPageOrder[i] == page) return true;
            }
            return false;
        }

        private static void ValidateColumnWeights(VaccinationBookletStyle t)
        {
            if (t.ColumnWeights == null || t.ColumnWeights.Length != 5)
            {
                throw new InvalidOperationException("接种记录表必须有 5 列列宽权重。");
            }
            float sum = 0f;
            foreach (float weight in t.ColumnWeights)
            {
                if (!(weight > 0f) || float.IsInfinity(weight))
                {
                    throw new InvalidOperationException("列宽权重必须是正有限数，实际为 " + weight + "。");
                }
                sum += weight;
            }
            if (!(sum > 0f))
            {
                throw new InvalidOperationException("列宽权重之和必须为正。");
            }
        }

        /// <summary>
        /// 增加一个页眉字段。锁定令牌为 false（默认）时抛异常——规范"不得自行变更加印"。
        /// </summary>
        public HeaderField AddField(VaccinationBookletDocument document, string key, string label,
            string? value)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!Tokens.AllowLayoutDeviation)
            {
                throw new InvalidOperationException(
                    "AllowLayoutDeviation=false：接种证版面已按规范锁定，不得增加字段 \"" + key + "\"。");
            }
            HeaderField field = new HeaderField(key, label, value);
            document.Patient.ExtraFields.Add(field);
            return field;
        }

        /// <summary>删除一个页眉字段。锁定令牌为 false（默认）时抛异常。</summary>
        public bool RemoveField(VaccinationBookletDocument document, string key)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (!Tokens.AllowLayoutDeviation)
            {
                throw new InvalidOperationException(
                    "AllowLayoutDeviation=false：接种证版面已按规范锁定，不得删除字段 \"" + key + "\"。");
            }
            for (int i = 0; i < document.Patient.ExtraFields.Count; i++)
            {
                if (string.Equals(document.Patient.ExtraFields[i].Key, key, StringComparison.Ordinal))
                {
                    document.Patient.ExtraFields.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        private static VaccinationBookletDocument Cast(ChartDocument document)
        {
            VaccinationBookletDocument? typed = document as VaccinationBookletDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(VaccinationBookletDocument) + "。",
                    nameof(document));
            }
            return typed;
        }
    }
}
