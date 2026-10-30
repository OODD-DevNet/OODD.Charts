using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Tickets.Vat
{
    /// <summary>购买方 / 销售方信息栏的一行。</summary>
    public sealed class VatPartyRowLayout
    {
        /// <summary>构造一行。</summary>
        public VatPartyRowLayout(string label, string? value, SKRect row, SKRect labelRect, SKRect valueRect)
        {
            Label = label;
            Value = value;
            Row = row;
            LabelRect = labelRect;
            ValueRect = valueRect;
        }

        public string Label { get; }
        public string? Value { get; }
        public SKRect Row { get; }
        public SKRect LabelRect { get; }
        public SKRect ValueRect { get; }
    }

    /// <summary>明细表的一行（不足最小行数时 <see cref="Item"/> 为 null，行仍占位）。</summary>
    public sealed class VatItemRowLayout
    {
        /// <summary>构造明细行。</summary>
        public VatItemRowLayout(int index, SKRect row, IReadOnlyList<SKRect> cells,
            IReadOnlyList<string> texts, VatInvoiceItem? item)
        {
            Index = index;
            Row = row;
            Cells = cells;
            Texts = texts;
            Item = item;
        }

        public int Index { get; }
        public SKRect Row { get; }
        public IReadOnlyList<SKRect> Cells { get; }
        public IReadOnlyList<string> Texts { get; }
        public VatInvoiceItem? Item { get; }
    }

    /// <summary>联次附加字段的一行；<see cref="Shown"/> 为 false 的行只保留行线、不落字。</summary>
    public sealed class VatCopyFieldRowLayout
    {
        /// <summary>构造附加字段行。</summary>
        public VatCopyFieldRowLayout(string key, string label, string? value, SKRect row,
            SKRect labelRect, SKRect valueRect, bool shown)
        {
            Key = key;
            Label = label;
            Value = value;
            Row = row;
            LabelRect = labelRect;
            ValueRect = valueRect;
            Shown = shown;
        }

        public string Key { get; }
        public string Label { get; }
        public string? Value { get; }
        public SKRect Row { get; }
        public SKRect LabelRect { get; }
        public SKRect ValueRect { get; }
        public bool Shown { get; }
    }

    /// <summary>单联发票版面。</summary>
    public sealed class VatCopyLayout
    {
        /// <summary>构造单联版面。</summary>
        public VatCopyLayout(int index, VatCopyRole role, string copyKey, string copyLabel, SKRect rect,
            SKRect inner, SKRect titleRect, SKRect metaRect, SKRect codeRect, SKRect numberRect,
            SKRect numberLabelRect, IReadOnlyList<SKRect> numberCells, SKRect numberGridRect, SKRect dateRect,
            SKRect partyRect, SKRect buyerTitleRect, SKRect sellerTitleRect,
            IReadOnlyList<VatPartyRowLayout> buyerRows, IReadOnlyList<VatPartyRowLayout> sellerRows,
            SKRect cipherLabelRect, IReadOnlyList<SKRect> cipherCells,
            SKRect itemHeaderRect, IReadOnlyList<SKRect> itemHeaderCells,
            IReadOnlyList<VatItemRowLayout> itemRows, SKRect totalRowRect, SKRect totalAmountCell,
            SKRect totalTaxCell, SKRect amountRowRect, SKRect amountLabelRect, SKRect upperAmountRect,
            SKRect amountGutterRect, SKRect lowerAmountRect, PressedAmount pressed,
            SKRect remarkRect, SKRect footerRect, IReadOnlyList<SKRect> footerCells,
            IReadOnlyList<VatCopyFieldRowLayout> copyFieldRows)
        {
            Index = index;
            Role = role;
            CopyKey = copyKey;
            CopyLabel = copyLabel;
            Rect = rect;
            Inner = inner;
            TitleRect = titleRect;
            MetaRect = metaRect;
            CodeRect = codeRect;
            NumberRect = numberRect;
            NumberLabelRect = numberLabelRect;
            NumberCells = numberCells;
            NumberGridRect = numberGridRect;
            DateRect = dateRect;
            PartyRect = partyRect;
            BuyerTitleRect = buyerTitleRect;
            SellerTitleRect = sellerTitleRect;
            BuyerRows = buyerRows;
            SellerRows = sellerRows;
            CipherLabelRect = cipherLabelRect;
            CipherCells = cipherCells;
            ItemHeaderRect = itemHeaderRect;
            ItemHeaderCells = itemHeaderCells;
            ItemRows = itemRows;
            TotalRowRect = totalRowRect;
            TotalAmountCell = totalAmountCell;
            TotalTaxCell = totalTaxCell;
            AmountRowRect = amountRowRect;
            AmountLabelRect = amountLabelRect;
            UpperAmountRect = upperAmountRect;
            AmountGutterRect = amountGutterRect;
            LowerAmountRect = lowerAmountRect;
            Pressed = pressed;
            RemarkRect = remarkRect;
            FooterRect = footerRect;
            FooterCells = footerCells;
            CopyFieldRows = copyFieldRows;
        }

        public int Index { get; }
        public VatCopyRole Role { get; }
        public string CopyKey { get; }
        public string CopyLabel { get; }

        public SKRect Rect { get; }
        public SKRect Inner { get; }
        public SKRect TitleRect { get; }
        public SKRect MetaRect { get; }
        public SKRect CodeRect { get; }
        public SKRect NumberRect { get; }
        public SKRect NumberLabelRect { get; }

        /// <summary>8 格发票号码的逐格矩形。</summary>
        public IReadOnlyList<SKRect> NumberCells { get; }

        /// <summary>8 格发票号码的整排矩形。</summary>
        public SKRect NumberGridRect { get; }

        public SKRect DateRect { get; }
        public SKRect PartyRect { get; }
        public SKRect BuyerTitleRect { get; }
        public SKRect SellerTitleRect { get; }
        public IReadOnlyList<VatPartyRowLayout> BuyerRows { get; }
        public IReadOnlyList<VatPartyRowLayout> SellerRows { get; }

        public SKRect CipherLabelRect { get; }

        /// <summary>密码区 4 行 × 21 格，按行优先展开，共 84 格。</summary>
        public IReadOnlyList<SKRect> CipherCells { get; }

        public SKRect ItemHeaderRect { get; }
        public IReadOnlyList<SKRect> ItemHeaderCells { get; }
        public IReadOnlyList<VatItemRowLayout> ItemRows { get; }
        public SKRect TotalRowRect { get; }
        public SKRect TotalAmountCell { get; }
        public SKRect TotalTaxCell { get; }

        public SKRect AmountRowRect { get; }
        public SKRect AmountLabelRect { get; }

        /// <summary>价税合计（大写）单元格。</summary>
        public SKRect UpperAmountRect { get; }

        /// <summary>大写格与小写格之间的空槽；绘制时这里没有任何线或字。</summary>
        public SKRect AmountGutterRect { get; }

        /// <summary>价税合计（小写）单元格。</summary>
        public SKRect LowerAmountRect { get; }

        /// <summary>压印后的逐字排版。</summary>
        public PressedAmount Pressed { get; }

        public SKRect RemarkRect { get; }
        public SKRect FooterRect { get; }
        public IReadOnlyList<SKRect> FooterCells { get; }
        public IReadOnlyList<VatCopyFieldRowLayout> CopyFieldRows { get; }
    }

    /// <summary>整张多联发票的版面。</summary>
    public sealed class VatInvoiceLayout
    {
        /// <summary>构造版面。</summary>
        public VatInvoiceLayout(SKRect bounds, CollateSpec collate, IReadOnlyList<VatCopyLayout> copies,
            float copyHeight, int itemRowCount, string title, double amountTotal, double taxTotal,
            double grandTotal, string uppercaseAmount, string? invoiceCode, string? invoiceNumber,
            string? invoiceDateText, string? cipherText, string? remark, string? payee, string? reviewer,
            string? drawer)
        {
            Bounds = bounds;
            Collate = collate;
            Copies = copies;
            CopyHeight = copyHeight;
            ItemRowCount = itemRowCount;
            Title = title;
            AmountTotal = amountTotal;
            TaxTotal = taxTotal;
            GrandTotal = grandTotal;
            UppercaseAmount = uppercaseAmount;
            InvoiceCode = invoiceCode;
            InvoiceNumber = invoiceNumber;
            InvoiceDateText = invoiceDateText;
            CipherText = cipherText;
            Remark = remark;
            Payee = payee;
            Reviewer = reviewer;
            Drawer = drawer;
        }

        public SKRect Bounds { get; }
        public CollateSpec Collate { get; }
        public IReadOnlyList<VatCopyLayout> Copies { get; }
        public float CopyHeight { get; }
        public int ItemRowCount { get; }
        public string Title { get; }
        public double AmountTotal { get; }
        public double TaxTotal { get; }
        public double GrandTotal { get; }
        public string UppercaseAmount { get; }
        public string? InvoiceCode { get; }
        public string? InvoiceNumber { get; }
        public string? InvoiceDateText { get; }
        public string? CipherText { get; }
        public string? Remark { get; }
        public string? Payee { get; }
        public string? Reviewer { get; }
        public string? Drawer { get; }

        /// <summary>联数。</summary>
        public int CopyCount
        {
            get { return Copies.Count; }
        }

        /// <summary>取第 index 联。</summary>
        public VatCopyLayout Copy(int index)
        {
            if (index < 0 || index >= Copies.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "联序号 " + index + " 越界（共 " + Copies.Count + " 联）。");
            }
            return Copies[index];
        }
    }

    /// <summary>
    /// 增值税专用发票的版面求解与落笔。
    /// <para>
    /// 3/4 联由 <see cref="CollateSpec"/> 纵向切分；每联的附加字段行数 = 联数，
    /// 每联只落自己那一行，其余行留空——联间差异因此能被像素用例在同一 y 上做"有墨 / 无墨"对照。
    /// </para>
    /// <para>
    /// <b>D22</b>：所有尺寸是结构尺寸，未按发票票样校准；密码区格位、金额压印规则、
    /// 联次划分都不代表任何税务规范。
    /// </para>
    /// </summary>
    public static class VatInvoiceSheet
    {
        /// <summary>共有字段键：所有联都出现（用于 CollateSpec 的结构校验）。</summary>
        public static readonly string[] CommonFieldKeys =
        {
            "invoice-code",
            "invoice-number",
            "invoice-date",
            "buyer-name",
            "buyer-tax-id",
            "buyer-address",
            "buyer-bank",
            "seller-name",
            "seller-tax-id",
            "seller-address",
            "seller-bank",
            "item-table",
            "totals",
            "pressed-amount",
            "remark",
            "payee",
            "reviewer",
            "drawer",
        };

        /// <summary>按文档、令牌与字体求版面。纯函数：不修改文档与令牌。</summary>
        /// <param name="document">发票文档。</param>
        /// <param name="tokens">发票令牌。</param>
        /// <param name="fonts">字体解析器（压印排版需要量文本宽）。</param>
        /// <param name="family">首选字族（与绘制时的 ChartStyle.Text.Family 一致）。</param>
        public static VatInvoiceLayout Build(VatInvoiceDocument document, VatInvoiceStyle tokens,
            IFontResolver fonts, string? family)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (fonts == null)
            {
                throw new ArgumentNullException(nameof(fonts));
            }
            if (document.CopyCount != 3 && document.CopyCount != 4)
            {
                throw new InvalidOperationException(
                    "发票联数只支持 3 或 4，实到 " + document.CopyCount + "。");
            }

            TicketStyle ticket = tokens.Ticket;
            IReadOnlyList<VatCopyRole> roles = document.Roles;
            int itemRowCount = Math.Max(tokens.MinItemRows, document.Items.Count);

            float contentHeight =
                tokens.TitleHeight
                + tokens.MetaRowHeight
                + tokens.PartyTitleHeight + 4f * tokens.PartyRowHeight
                + tokens.SectionGap + tokens.CipherLabelHeight
                + tokens.CipherRows * tokens.CipherBoxSize
                + tokens.SectionGap + tokens.ItemHeaderHeight
                + itemRowCount * tokens.ItemRowHeight + tokens.TotalRowHeight
                + tokens.SectionGap + tokens.AmountRowHeight
                + tokens.SectionGap + tokens.RemarkHeight + tokens.FooterHeight
                + roles.Count * tokens.CopyFieldRowHeight;

            float copyHeight = Math.Max(tokens.MinCopyHeight, ticket.Margin * 2f + contentHeight);
            float totalHeight = copyHeight * roles.Count;
            SKRect bounds = new SKRect(0f, 0f, ticket.Width, totalHeight);

            List<CopyDef> copyDefs = new List<CopyDef>(roles.Count);
            for (int i = 0; i < roles.Count; i++)
            {
                VatCopyField extra = document.CopyField(roles[i]);
                copyDefs.Add(new CopyDef(VatInvoiceDocument.RoleKey(roles[i]),
                    VatInvoiceDocument.RoleLabel(roles[i]), copyHeight,
                    CommonFieldKeys, new[] { extra.Key }));
            }

            CollateSpec collate = new CollateSpec(copyDefs, tokens.Perforation, CollateDirection.VerticalStack);
            if (collate.DifferentiatingFields.Count != roles.Count)
            {
                throw new InvalidOperationException(
                    "联次差异字段数 " + collate.DifferentiatingFields.Count
                    + " 与联数 " + roles.Count + " 不一致。");
            }

            List<VatCopyLayout> copies = new List<VatCopyLayout>(roles.Count);
            for (int i = 0; i < roles.Count; i++)
            {
                copies.Add(BuildCopy(i, roles, collate, bounds, document, tokens, fonts, family, itemRowCount));
            }

            return new VatInvoiceLayout(bounds, collate, copies, copyHeight, itemRowCount,
                string.IsNullOrEmpty(document.FormTitle) ? tokens.Title : document.FormTitle!,
                document.AmountTotal, document.TaxTotal, document.GrandTotal, document.UppercaseAmount,
                document.InvoiceCode, document.InvoiceNumber, document.InvoiceDateText, document.CipherText,
                document.Remark, document.Payee, document.Reviewer, document.Drawer);
        }

        private static VatCopyLayout BuildCopy(int index, IReadOnlyList<VatCopyRole> roles, CollateSpec collate,
            SKRect bounds, VatInvoiceDocument document, VatInvoiceStyle tokens, IFontResolver fonts,
            string? family, int itemRowCount)
        {
            TicketStyle ticket = tokens.Ticket;
            CopyDef copy = collate.Copies[index];
            SKRect rect = collate.CopyRect(index, bounds);
            SKRect inner = new SKRect(rect.Left + ticket.Margin, rect.Top + ticket.Margin,
                rect.Right - ticket.Margin, rect.Bottom - ticket.Margin);

            float y = inner.Top;
            SKRect titleRect = NextRow(inner.Left, inner.Right, ref y, tokens.TitleHeight);
            SKRect metaRect = NextRow(inner.Left, inner.Right, ref y, tokens.MetaRowHeight);

            float[] metaBounds = CellGeometry.ColumnBounds(inner.Left, inner.Right,
                new[] { 3.0d, 2.2d, 3.0d });
            SKRect codeRect = new SKRect(metaBounds[0], metaRect.Top, metaBounds[1], metaRect.Bottom);
            SKRect numberRect = new SKRect(metaBounds[1], metaRect.Top, metaBounds[2], metaRect.Bottom);
            SKRect dateRect = new SKRect(metaBounds[2], metaRect.Top, metaBounds[3], metaRect.Bottom);

            FontKey metaLabelKey = new FontKey(family, tokens.MetaLabelSize);
            float numberLabelWidth = Primitives.MeasureText(fonts, "发票号码：", metaLabelKey) + 2f;
            SKRect numberLabelRect = new SKRect(numberRect.Left, numberRect.Top,
                numberRect.Left + numberLabelWidth, numberRect.Bottom);

            DigitBox numberBox = new DigitBox(8, tokens.InvoiceNumberBoxWidthFactor, 0f);
            float numberSize = tokens.InvoiceNumberBoxSize;
            float numberWidth = numberBox.RowWidthOf(numberSize);
            SKPoint numberTopLeft = new SKPoint(
                Math.Max(numberRect.Left + numberLabelWidth + tokens.CellPadding,
                    numberRect.Right - numberWidth - tokens.CellPadding),
                numberRect.Top + (numberRect.Height - numberSize) * 0.5f);
            SKRect numberGridRect = numberBox.RowRectFromLeft(numberTopLeft, numberSize);
            List<SKRect> numberCells = new List<SKRect>(8);
            for (int i = 0; i < 8; i++)
            {
                numberCells.Add(numberBox.BoxRectFromLeft(numberTopLeft, numberSize, i));
            }

            float partyTop = y;
            float partyColumnGap = tokens.PartyColumnGap;
            float partyColumnWidth = (inner.Width - partyColumnGap) * 0.5f;
            SKRect buyerTitleRect = new SKRect(inner.Left, partyTop,
                inner.Left + partyColumnWidth, partyTop + tokens.PartyTitleHeight);
            SKRect sellerTitleRect = new SKRect(inner.Left + partyColumnWidth + partyColumnGap, partyTop,
                inner.Right, partyTop + tokens.PartyTitleHeight);

            float buyerRowsTop = partyTop + tokens.PartyTitleHeight;
            List<VatPartyRowLayout> buyerRows = BuildPartyRows(inner.Left, inner.Left + partyColumnWidth,
                buyerRowsTop, tokens, new[] { "名称", "纳税人识别号", "地址、电话", "开户行及账号" },
                new[]
                {
                    document.BuyerName, document.BuyerTaxId, document.BuyerAddressPhone, document.BuyerBankAccount,
                });
            List<VatPartyRowLayout> sellerRows = BuildPartyRows(
                inner.Left + partyColumnWidth + partyColumnGap, inner.Right, buyerRowsTop, tokens,
                new[] { "名称", "纳税人识别号", "地址、电话", "开户行及账号" },
                new[]
                {
                    document.SellerName, document.SellerTaxId, document.SellerAddressPhone,
                    document.SellerBankAccount,
                });

            float partyBottom = buyerRowsTop + 4f * tokens.PartyRowHeight;
            SKRect partyRect = new SKRect(inner.Left, partyTop, inner.Right, partyBottom);
            y = partyBottom;

            y += tokens.SectionGap;
            SKRect cipherLabelRect = NextRow(inner.Left, inner.Right, ref y, tokens.CipherLabelHeight);

            DigitBox cipherRow = new DigitBox(tokens.CipherColumns, tokens.CipherBoxWidthFactor, 0f);
            List<SKRect> cipherCells = new List<SKRect>(tokens.CipherColumns * tokens.CipherRows);
            for (int r = 0; r < tokens.CipherRows; r++)
            {
                SKPoint topLeft = new SKPoint(inner.Left, y + r * tokens.CipherBoxSize);
                for (int c = 0; c < tokens.CipherColumns; c++)
                {
                    cipherCells.Add(cipherRow.BoxRectFromLeft(topLeft, tokens.CipherBoxSize, c));
                }
            }
            y += tokens.CipherRows * tokens.CipherBoxSize;

            y += tokens.SectionGap;
            SKRect itemHeaderRect = NextRow(inner.Left, inner.Right, ref y, tokens.ItemHeaderHeight);
            float[] itemBounds = CellGeometry.ColumnBounds(inner.Left, inner.Right, tokens.ItemColumnWeights);
            List<SKRect> itemHeaderCells = new List<SKRect>(8);
            for (int c = 0; c < 8; c++)
            {
                itemHeaderCells.Add(new SKRect(itemBounds[c], itemHeaderRect.Top,
                    itemBounds[c + 1], itemHeaderRect.Bottom));
            }

            List<VatItemRowLayout> itemRows = new List<VatItemRowLayout>(itemRowCount);
            for (int r = 0; r < itemRowCount; r++)
            {
                SKRect rowRect = NextRow(inner.Left, inner.Right, ref y, tokens.ItemRowHeight);
                VatInvoiceItem? item = r < document.Items.Count ? document.Items[r] : null;
                List<SKRect> cells = new List<SKRect>(8);
                for (int c = 0; c < 8; c++)
                {
                    cells.Add(new SKRect(itemBounds[c], rowRect.Top, itemBounds[c + 1], rowRect.Bottom));
                }
                itemRows.Add(new VatItemRowLayout(r, rowRect, cells, ItemTexts(item), item));
            }

            SKRect totalRowRect = NextRow(inner.Left, inner.Right, ref y, tokens.TotalRowHeight);
            SKRect totalAmountCell = new SKRect(itemBounds[5], totalRowRect.Top,
                itemBounds[6], totalRowRect.Bottom);
            SKRect totalTaxCell = new SKRect(itemBounds[7], totalRowRect.Top,
                itemBounds[8], totalRowRect.Bottom);

            y += tokens.SectionGap;
            SKRect amountRowRect = NextRow(inner.Left, inner.Right, ref y, tokens.AmountRowHeight);
            SKRect amountLabelRect = new SKRect(amountRowRect.Left, amountRowRect.Top,
                amountRowRect.Left + tokens.AmountLabelWidth, amountRowRect.Bottom);
            SKRect lowerAmountRect = new SKRect(amountRowRect.Right - tokens.AmountLowerWidth,
                amountRowRect.Top, amountRowRect.Right, amountRowRect.Bottom);
            SKRect amountGutterRect = new SKRect(lowerAmountRect.Left - tokens.AmountGutter,
                amountRowRect.Top, lowerAmountRect.Left, amountRowRect.Bottom);
            SKRect upperAmountRect = new SKRect(amountLabelRect.Right, amountRowRect.Top,
                amountGutterRect.Left, amountRowRect.Bottom);

            FontKey pressedBase = new FontKey(family, tokens.PressedBaseSize, tokens.PressedBold);
            PressedAmount pressed = PressedAmount.Layout(document.UppercaseAmount, upperAmountRect,
                fonts, pressedBase, tokens.PressedMinSize, tokens.AmountPaddingX, tokens.AmountPaddingY);

            y += tokens.SectionGap;
            SKRect remarkRect = NextRow(inner.Left, inner.Right, ref y, tokens.RemarkHeight);
            SKRect footerRect = NextRow(inner.Left, inner.Right, ref y, tokens.FooterHeight);
            float[] footerBounds = CellGeometry.UniformBounds(footerRect.Left, footerRect.Right,
                tokens.FooterLabels.Count);
            List<SKRect> footerCells = new List<SKRect>(tokens.FooterLabels.Count);
            for (int i = 0; i < tokens.FooterLabels.Count; i++)
            {
                footerCells.Add(new SKRect(footerBounds[i], footerRect.Top,
                    footerBounds[i + 1], footerRect.Bottom));
            }

            List<VatCopyFieldRowLayout> copyFieldRows = new List<VatCopyFieldRowLayout>(roles.Count);
            for (int i = 0; i < roles.Count; i++)
            {
                VatCopyField field = document.CopyField(roles[i]);
                SKRect rowRect = NextRow(inner.Left, inner.Right, ref y, tokens.CopyFieldRowHeight);
                SKRect labelRect = new SKRect(rowRect.Left, rowRect.Top,
                    rowRect.Left + tokens.PartyLabelWidth, rowRect.Bottom);
                SKRect valueRect = new SKRect(labelRect.Right, rowRect.Top, rowRect.Right, rowRect.Bottom);
                copyFieldRows.Add(new VatCopyFieldRowLayout(field.Key, field.Label, field.Value,
                    rowRect, labelRect, valueRect, copy.ShowsField(field.Key)));
            }

            return new VatCopyLayout(index, roles[index], copy.Key, copy.Label, rect, inner,
                titleRect, metaRect, codeRect, numberRect, numberLabelRect, numberCells, numberGridRect, dateRect,
                partyRect, buyerTitleRect, sellerTitleRect, buyerRows, sellerRows,
                cipherLabelRect, cipherCells, itemHeaderRect, itemHeaderCells,
                itemRows, totalRowRect, totalAmountCell, totalTaxCell,
                amountRowRect, amountLabelRect, upperAmountRect, amountGutterRect, lowerAmountRect, pressed,
                remarkRect, footerRect, footerCells, copyFieldRows);
        }

        private static List<VatPartyRowLayout> BuildPartyRows(float left, float right, float top,
            VatInvoiceStyle tokens, string[] labels, string?[] values)
        {
            List<VatPartyRowLayout> rows = new List<VatPartyRowLayout>(labels.Length);
            float y = top;
            for (int i = 0; i < labels.Length; i++)
            {
                SKRect row = new SKRect(left, y, right, y + tokens.PartyRowHeight);
                SKRect labelRect = new SKRect(row.Left, row.Top, row.Left + tokens.PartyLabelWidth, row.Bottom);
                SKRect valueRect = new SKRect(labelRect.Right, row.Top, row.Right, row.Bottom);
                rows.Add(new VatPartyRowLayout(labels[i], values[i], row, labelRect, valueRect));
                y += tokens.PartyRowHeight;
            }
            return rows;
        }

        private static IReadOnlyList<string> ItemTexts(VatInvoiceItem? item)
        {
            if (item == null)
            {
                return new[] { string.Empty, string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty, string.Empty, string.Empty };
            }
            return new[]
            {
                item.Name,
                item.Specification,
                item.Unit,
                item.Quantity.ToString("0.##", CultureInfo.InvariantCulture),
                VatInvoiceItem.Money(item.UnitPrice),
                VatInvoiceItem.Money(item.Amount),
                VatInvoiceItem.Percent(item.TaxRate),
                VatInvoiceItem.Money(item.TaxAmount),
            };
        }

        private static SKRect NextRow(float left, float right, ref float y, float height)
        {
            SKRect rect = new SKRect(left, y, right, y + height);
            y += height;
            return rect;
        }

        // ------------------------------------------------------------------ 绘制

        /// <summary>把整张多联发票画到画布上。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="layout">版面。</param>
        /// <param name="tokens">发票令牌。</param>
        public static void Draw(Painter painter, VatInvoiceLayout layout, VatInvoiceStyle tokens)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            VatText text = VatText.Of(painter, tokens);
            painter.FillRect(layout.Bounds, tokens.Palette.PageBackground);

            foreach (VatCopyLayout copy in layout.Copies)
            {
                DrawCopy(painter, layout, copy, tokens, text);
            }

            for (int seam = 1; seam < layout.Collate.Count; seam++)
            {
                float? y = layout.Collate.PerforationAt(seam, layout.Bounds);
                if (y.HasValue)
                {
                    painter.DashedLine(layout.Bounds.Left + tokens.Ticket.Margin, y.Value,
                        layout.Bounds.Right - tokens.Ticket.Margin, y.Value,
                        tokens.Palette.Perforation, tokens.PerforationStroke);
                }
            }
        }

        private static void DrawCopy(Painter painter, VatInvoiceLayout layout, VatCopyLayout copy,
            VatInvoiceStyle tokens, VatText text)
        {
            TicketStyle ticket = tokens.Ticket;
            TicketPalette palette = tokens.Palette;
            float pad = tokens.CellPadding;

            painter.Frame(copy.Rect, palette.Ink, ticket.BorderStroke);
            painter.TextInBox(layout.Title, copy.TitleRect, text.Title, palette.TitleText,
                CellAlign.Center, CellAlign.Center, 0f);
            painter.TextInBox(copy.CopyLabel, copy.TitleRect, text.Usage, palette.MutedText,
                CellAlign.Far, CellAlign.Center, pad);

            // ---- 代码 / 号码 / 日期 ----
            painter.TextInBox("发票代码：" + (layout.InvoiceCode ?? string.Empty), copy.CodeRect,
                text.MetaLabel, palette.Text, CellAlign.Near, CellAlign.Center, pad);
            painter.TextInBox("发票号码：", copy.NumberLabelRect, text.MetaLabel, palette.Text,
                CellAlign.Near, CellAlign.Center, pad);

            DigitBox numberBox = new DigitBox(copy.NumberCells.Count, tokens.InvoiceNumberBoxWidthFactor, 0f);
            numberBox.Draw(painter, new SKPoint(copy.NumberGridRect.MidX, copy.NumberGridRect.MidY),
                tokens.InvoiceNumberBoxSize, palette.Ink);
            string number = layout.InvoiceNumber ?? string.Empty;
            for (int i = 0; i < copy.NumberCells.Count && i < number.Length; i++)
            {
                painter.TextInBox(char.ToString(number[i]), copy.NumberCells[i], text.MetaValue,
                    palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            painter.TextInBox("开票日期：" + (layout.InvoiceDateText ?? string.Empty), copy.DateRect,
                text.MetaLabel, palette.Text, CellAlign.Far, CellAlign.Center, pad);
            painter.Line(copy.MetaRect.Left, copy.MetaRect.Bottom, copy.MetaRect.Right,
                copy.MetaRect.Bottom, palette.Grid, ticket.GridStroke);

            // ---- 购买方 / 销售方 ----
            painter.TextInBox("购买方", copy.BuyerTitleRect, text.ItemHeader, palette.TitleText,
                CellAlign.Near, CellAlign.Center, pad);
            painter.TextInBox("销售方", copy.SellerTitleRect, text.ItemHeader, palette.TitleText,
                CellAlign.Near, CellAlign.Center, pad);

            float partySeparatorX = copy.BuyerTitleRect.Right + tokens.PartyColumnGap * 0.5f;
            painter.Line(partySeparatorX, copy.PartyRect.Top, partySeparatorX, copy.PartyRect.Bottom,
                palette.Grid, ticket.GridStroke);

            DrawPartyRows(painter, copy.BuyerRows, tokens, text);
            DrawPartyRows(painter, copy.SellerRows, tokens, text);
            painter.Line(copy.PartyRect.Left, copy.PartyRect.Bottom, copy.PartyRect.Right,
                copy.PartyRect.Bottom, palette.Grid, ticket.GridStroke);

            // ---- 密码区 ----
            painter.TextInBox("密码区", copy.CipherLabelRect, text.ItemHeader, palette.TitleText,
                CellAlign.Near, CellAlign.Center, pad);
            painter.TextInBox(tokens.CipherColumns + " 列 × " + tokens.CipherRows + " 行",
                copy.CipherLabelRect, text.Usage, palette.MutedText, CellAlign.Far, CellAlign.Center, pad);

            DigitBox cipherRow = new DigitBox(tokens.CipherColumns, tokens.CipherBoxWidthFactor, 0f);
            for (int r = 0; r < tokens.CipherRows; r++)
            {
                SKRect first = copy.CipherCells[r * tokens.CipherColumns];
                float centerY = first.Top + tokens.CipherBoxSize * 0.5f;
                float centerX = first.Left + cipherRow.RowWidthOf(tokens.CipherBoxSize) * 0.5f;
                cipherRow.Draw(painter, new SKPoint(centerX, centerY), tokens.CipherBoxSize, palette.Ink);
            }

            string cipher = layout.CipherText ?? string.Empty;
            int cipherCount = Math.Min(cipher.Length, copy.CipherCells.Count);
            for (int i = 0; i < cipherCount; i++)
            {
                painter.TextInBox(char.ToString(cipher[i]), copy.CipherCells[i], text.Cipher,
                    palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            // ---- 明细表 ----
            DrawItemTable(painter, layout, copy, tokens, text);

            // ---- 价税合计 ----
            painter.Rect(copy.AmountLabelRect, palette.Grid, ticket.GridStroke);
            painter.Rect(copy.UpperAmountRect, palette.Grid, ticket.GridStroke);
            painter.Rect(copy.LowerAmountRect, palette.Grid, ticket.GridStroke);
            painter.TextInBox("价税合计", copy.AmountLabelRect, text.AmountLabel, palette.Text,
                CellAlign.Center, CellAlign.Center, pad);
            copy.Pressed.Draw(painter, palette.Text);
            painter.TextInBox("（小写）￥" + VatInvoiceItem.Money(layout.GrandTotal), copy.LowerAmountRect,
                text.LowerAmount, palette.Text, CellAlign.Center, CellAlign.Center, pad);

            // ---- 备注 / 签署 / 联次字段 ----
            painter.TextInBox("备注：" + (layout.Remark ?? string.Empty), copy.RemarkRect,
                text.Footer, palette.Text, CellAlign.Near, CellAlign.Center, pad);
            painter.Line(copy.RemarkRect.Left, copy.RemarkRect.Bottom, copy.RemarkRect.Right,
                copy.RemarkRect.Bottom, palette.Grid, ticket.GridStroke);

            string[] footerValues = { layout.Payee ?? string.Empty, layout.Reviewer ?? string.Empty,
                layout.Drawer ?? string.Empty };
            for (int i = 0; i < copy.FooterCells.Count && i < tokens.FooterLabels.Count; i++)
            {
                SKRect cell = copy.FooterCells[i];
                painter.TextInBox(tokens.FooterLabels[i] + "：" + footerValues[i], cell, text.Footer,
                    palette.Text, CellAlign.Near, CellAlign.Center, pad);
                if (i > 0)
                {
                    painter.Line(cell.Left, cell.Top, cell.Left, cell.Bottom, palette.Grid, ticket.GridStroke);
                }
            }
            painter.Line(copy.FooterRect.Left, copy.FooterRect.Bottom, copy.FooterRect.Right,
                copy.FooterRect.Bottom, palette.Grid, ticket.GridStroke);

            foreach (VatCopyFieldRowLayout row in copy.CopyFieldRows)
            {
                if (row.Shown)
                {
                    painter.TextInBox(row.Label + "：", row.LabelRect, text.Footer, palette.Text,
                        CellAlign.Near, CellAlign.Center, pad);
                    if (!string.IsNullOrEmpty(row.Value))
                    {
                        painter.TextInBox(row.Value!, row.ValueRect, text.Footer, palette.Text,
                            CellAlign.Near, CellAlign.Center, pad);
                    }
                }
                painter.Line(row.Row.Left, row.Row.Bottom, row.Row.Right, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
            }
        }

        private static void DrawPartyRows(Painter painter, IReadOnlyList<VatPartyRowLayout> rows,
            VatInvoiceStyle tokens, VatText text)
        {
            foreach (VatPartyRowLayout row in rows)
            {
                painter.TextInBox(row.Label + "：", row.LabelRect, text.PartyLabel, tokens.Palette.Text,
                    CellAlign.Near, CellAlign.Center, tokens.CellPadding);
                if (!string.IsNullOrEmpty(row.Value))
                {
                    painter.TextInBox(row.Value!, row.ValueRect, text.PartyValue, tokens.Palette.Text,
                        CellAlign.Near, CellAlign.Center, tokens.CellPadding);
                }
                painter.Line(row.Row.Left, row.Row.Bottom, row.Row.Right, row.Row.Bottom,
                    tokens.Palette.Grid, tokens.Ticket.GridStroke);
            }
        }

        private static void DrawItemTable(Painter painter, VatInvoiceLayout layout, VatCopyLayout copy,
            VatInvoiceStyle tokens, VatText text)
        {
            TicketStyle ticket = tokens.Ticket;
            TicketPalette palette = tokens.Palette;
            float pad = tokens.CellPadding;

            for (int c = 0; c < copy.ItemHeaderCells.Count && c < tokens.ItemHeaders.Count; c++)
            {
                painter.TextInBox(tokens.ItemHeaders[c], copy.ItemHeaderCells[c], text.ItemHeader,
                    palette.TitleText, CellAlign.Center, CellAlign.Center, pad);
            }

            float[] boundaries = new float[copy.ItemHeaderCells.Count + 1];
            boundaries[0] = copy.ItemHeaderRect.Left;
            for (int c = 0; c < copy.ItemHeaderCells.Count; c++)
            {
                boundaries[c + 1] = copy.ItemHeaderCells[c].Right;
            }

            // 表头 / 明细 / 合计共用的竖分隔线。
            for (int c = 1; c < boundaries.Length - 1; c++)
            {
                painter.Line(boundaries[c], copy.ItemHeaderRect.Top, boundaries[c],
                    copy.TotalRowRect.Bottom, palette.Grid, ticket.GridStroke);
            }

            painter.Line(copy.ItemHeaderRect.Left, copy.ItemHeaderRect.Bottom,
                copy.ItemHeaderRect.Right, copy.ItemHeaderRect.Bottom, palette.Grid, ticket.GridStroke);

            foreach (VatItemRowLayout row in copy.ItemRows)
            {
                for (int c = 0; c < row.Cells.Count && c < row.Texts.Count; c++)
                {
                    if (row.Texts[c].Length == 0)
                    {
                        continue;
                    }
                    CellAlign align = c == 0 ? CellAlign.Near : (c >= 3 ? CellAlign.Far : CellAlign.Center);
                    painter.TextInBox(row.Texts[c], row.Cells[c], text.ItemValue, palette.Text,
                        align, CellAlign.Center, pad);
                }
                painter.Line(row.Row.Left, row.Row.Bottom, row.Row.Right, row.Row.Bottom,
                    palette.Grid, ticket.GridStroke);
            }

            painter.TextInBox("合计", copy.TotalRowRect, text.ItemHeader, palette.Text,
                CellAlign.Near, CellAlign.Center, 0f);
            painter.TextInBox(VatInvoiceItem.Money(layout.AmountTotal), copy.TotalAmountCell,
                text.ItemValue, palette.Text, CellAlign.Far, CellAlign.Center, pad);
            painter.TextInBox(VatInvoiceItem.Money(layout.TaxTotal), copy.TotalTaxCell,
                text.ItemValue, palette.Text, CellAlign.Far, CellAlign.Center, pad);
            painter.Line(copy.TotalRowRect.Left, copy.TotalRowRect.Bottom, copy.TotalRowRect.Right,
                copy.TotalRowRect.Bottom, palette.Grid, ticket.GridStroke);
        }

        private sealed class VatText
        {
            private VatText(FontKey title, FontKey usage, FontKey metaLabel, FontKey metaValue,
                FontKey partyLabel, FontKey partyValue, FontKey itemHeader, FontKey itemValue,
                FontKey cipher, FontKey amountLabel, FontKey lowerAmount, FontKey footer)
            {
                Title = title;
                Usage = usage;
                MetaLabel = metaLabel;
                MetaValue = metaValue;
                PartyLabel = partyLabel;
                PartyValue = partyValue;
                ItemHeader = itemHeader;
                ItemValue = itemValue;
                Cipher = cipher;
                AmountLabel = amountLabel;
                LowerAmount = lowerAmount;
                Footer = footer;
            }

            public FontKey Title { get; }
            public FontKey Usage { get; }
            public FontKey MetaLabel { get; }
            public FontKey MetaValue { get; }
            public FontKey PartyLabel { get; }
            public FontKey PartyValue { get; }
            public FontKey ItemHeader { get; }
            public FontKey ItemValue { get; }
            public FontKey Cipher { get; }
            public FontKey AmountLabel { get; }
            public FontKey LowerAmount { get; }
            public FontKey Footer { get; }

            public static VatText Of(Painter painter, VatInvoiceStyle tokens)
            {
                string? family = painter.Style.Text.Family;
                return new VatText(
                    new FontKey(family, tokens.TitleSize, true),
                    new FontKey(family, tokens.UsageSize),
                    new FontKey(family, tokens.MetaLabelSize),
                    new FontKey(family, tokens.MetaValueSize),
                    new FontKey(family, tokens.PartySize),
                    new FontKey(family, tokens.PartySize),
                    new FontKey(family, tokens.ItemSize, true),
                    new FontKey(family, tokens.ItemSize),
                    new FontKey(family, tokens.CipherCharSize),
                    new FontKey(family, tokens.AmountLabelSize),
                    new FontKey(family, tokens.LowerAmountSize, true),
                    new FontKey(family, tokens.FooterSize));
            }
        }
    }
}
