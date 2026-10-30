using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Transit.DangerousGoods
{
    /// <summary>
    /// 危险货物道路运输联单（UN 菱形标志牌结构框架 + 多联次 + 罐体牌位置）。
    /// <para>
    /// 菱形由 Core 的 <see cref="DiamondPlacard"/> 提供几何，类项号由模板直立写入下半槽；
    /// 上半槽的 UN 象形图<b>只能注入</b>（<see cref="SymbolPath"/>）——官方象形图是受控图形，
    /// 本库不内置、不重绘、不冒充。联次由 <see cref="OODD.Charts.CollateSpec"/> 横向条排。
    /// </para>
    /// <para>
    /// 版面数值全部走 <see cref="DangerousGoodsStyle"/>，默认值<b>未校准</b>（D22）。
    /// </para>
    /// </summary>
    public sealed class DangerousGoodsTemplate : IChartTemplate
    {
        public string Key => "transit.dg-consignment";

        public string DisplayName => "危险货物道路运输联单";

        /// <summary>版面令牌。</summary>
        public DangerousGoodsStyle Tokens { get; set; } = new DangerousGoodsStyle();

        /// <summary>
        /// UN 象形图注入槽：由用户按 UN 原版提供的矢量路径（引用由调用方持有并释放）。
        /// 未注入时上半角留空——本库不画任何官方象形图。
        /// </summary>
        public SKPath? SymbolPath { get; set; }

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            DangerousGoodsDocument doc = Cast(document);
            DangerousGoodsLayout layout = DangerousGoodsLayout.Compute(doc, Tokens);
            return ChartGeometry.ForLayout(layout.Bounds, layout.CollateRect, layout);
        }

        private static DangerousGoodsDocument Cast(ChartDocument document)
        {
            DangerousGoodsDocument? typed = document as DangerousGoodsDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(DangerousGoodsDocument) + "。", nameof(document));
            }
            return typed;
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            DangerousGoodsDocument doc = Cast(document);
            DangerousGoodsLayout layout = geometry.LayoutOf<DangerousGoodsLayout>()
                                           ?? DangerousGoodsLayout.Compute(doc, Tokens);
            TransitPalette palette = layout.Tokens.Palette;

            painter.FillRect(layout.Bounds, palette.PageBackground);

            DrawTitle(painter, layout, doc);
            DrawPlacards(painter, layout, doc);
            DrawHeaderFields(painter, layout, doc);
            DrawCopies(painter, layout, doc);
            DrawTank(painter, layout, doc);
            DrawFooter(painter, layout, doc);
            DrawFrame(painter, layout);
        }

        private static void DrawTitle(Painter p, DangerousGoodsLayout layout, DangerousGoodsDocument doc)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            TransitPalette palette = tokens.Palette;

            string title = string.IsNullOrEmpty(doc.FormTitle) ? "危险货物道路运输联单" : doc.FormTitle!;
            p.TextInBox(title, layout.TitleRect, new FontKey(p.Style.Text.Family, frame.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            string note = "UN " + (string.IsNullOrEmpty(doc.UnNumber) ? "—" : doc.UnNumber)
                          + "　" + (string.IsNullOrEmpty(doc.ProperShippingName) ? "—" : doc.ProperShippingName)
                          + "　类别 " + ClassText(doc)
                          + "　联次 " + doc.Copies.Count.ToString(CultureInfo.InvariantCulture) + " 联"
                          + "　象形图槽位由用户提供（未校准）";
            p.TextInBox(note, layout.NoteRect, new FontKey(p.Style.Text.Family, frame.SubtitleSize),
                palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);
        }

        private void DrawPlacards(Painter p, DangerousGoodsLayout layout, DangerousGoodsDocument doc)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;

            DiamondPlacard main = new DiamondPlacard(SymbolPath, false, null);
            main.StrokeWidthFactor = tokens.PlacardStrokeFactor;
            main.Draw(p, layout.PlacardAnchor, layout.PlacardSize, palette.PlacardBorder);

            FontKey classKey = new FontKey(p.Style.Text.Family, layout.PlacardSize * tokens.ClassNumberFactor, true);
            p.TextInBox(ClassText(doc), layout.NumberSlot(), classKey, palette.Ink,
                CellAlign.Center, CellAlign.Center, 1f);

            SKRect bounds = layout.MainBounds();
            p.TextInBox("UN " + (string.IsNullOrEmpty(doc.UnNumber) ? "—" : doc.UnNumber),
                new SKRect(bounds.Left - 10f, bounds.Bottom + 2f, bounds.Right + 10f,
                    bounds.Bottom + 2f + tokens.UnCaptionSize * 1.6f),
                new FontKey(p.Style.Text.Family, tokens.UnCaptionSize, true),
                palette.Text, CellAlign.Center, CellAlign.Center, 0f);

            if (string.IsNullOrEmpty(doc.SubClass))
            {
                return;
            }

            DiamondPlacard sub = new DiamondPlacard(null, false, null);
            sub.StrokeWidthFactor = tokens.PlacardStrokeFactor;
            sub.Draw(p, layout.SubPlacardAnchor, layout.SubPlacardSize, palette.PlacardBorder);
            FontKey subKey = new FontKey(p.Style.Text.Family,
                layout.SubPlacardSize * tokens.ClassNumberFactor, true);
            p.TextInBox(doc.SubClass!, layout.SubNumberSlot(), subKey, palette.Ink,
                CellAlign.Center, CellAlign.Center, 1f);
        }

        private static void DrawHeaderFields(Painter p, DangerousGoodsLayout layout, DangerousGoodsDocument doc)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey labelKey = new FontKey(p.Style.Text.Family, tokens.HeaderFieldSize);
            FontKey valueKey = new FontKey(p.Style.Text.Family, tokens.HeaderFieldSize, true);

            IReadOnlyList<string> fields = layout.HeaderFields;
            for (int i = 0; i < fields.Count; i++)
            {
                string field = fields[i];
                SKRect cell = layout.HeaderFieldRect(i);
                p.TextInBox(DangerousGoodsDocument.LabelOf(field),
                    new SKRect(cell.Left, cell.Top, cell.Left + tokens.HeaderLabelWidth, cell.Bottom),
                    labelKey, palette.MutedText, CellAlign.Far, CellAlign.Center, 3f);
                string value = doc.ValueOf(field);
                p.TextInBox(Trim(p, value, cell.Width - tokens.HeaderLabelWidth - 8f, valueKey),
                    new SKRect(cell.Left + tokens.HeaderLabelWidth + 4f, cell.Top, cell.Right, cell.Bottom),
                    valueKey, palette.Text, CellAlign.Near, CellAlign.Center, 1f);
            }
        }

        private static void DrawCopies(Painter p, DangerousGoodsLayout layout, DangerousGoodsDocument doc)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey labelKey = new FontKey(p.Style.Text.Family, tokens.CopyTextSize);
            FontKey valueKey = new FontKey(p.Style.Text.Family, tokens.CopyTextSize, true);
            FontKey headerKey = new FontKey(p.Style.Text.Family, tokens.CopyTextSize, true);

            for (int i = 0; i < doc.Copies.Count; i++)
            {
                SKRect copyRect = layout.CopyRect(i);
                p.Rect(copyRect, palette.PanelBorder, 0.7f);
                SKRect header = layout.CopyLabelRect(i);
                p.FillRect(header, palette.PanelFill);
                p.Line(header.Left, header.Bottom, header.Right, header.Bottom, palette.PanelBorder, 0.6f);
                p.TextInBox(doc.Copies[i].Label, new SKRect(header.Left + 4f, header.Top, header.Right - 4f, header.Bottom),
                    headerKey, palette.Text, CellAlign.Near, CellAlign.Center, 0f);

                IReadOnlyList<string> fields = layout.FieldsOf(i);
                for (int f = 0; f < fields.Count; f++)
                {
                    string field = fields[f];
                    SKRect row = layout.CopyFieldRect(i, field);
                    p.TextInBox(DangerousGoodsDocument.LabelOf(field),
                        new SKRect(row.Left, row.Top, row.Left + tokens.CopyLabelWidth, row.Bottom),
                        labelKey, palette.MutedText, CellAlign.Far, CellAlign.Center, 2f);
                    float valueRight = Math.Max(row.Left + tokens.CopyLabelWidth + 10f,
                        row.Right - tokens.CopyPlacardSize - 10f);
                    string value = doc.ValueOf(field);
                    p.TextInBox(Trim(p, value, valueRight - row.Left - tokens.CopyLabelWidth - 6f, valueKey),
                        new SKRect(row.Left + tokens.CopyLabelWidth + 4f, row.Top, valueRight, row.Bottom),
                        valueKey, palette.Text, CellAlign.Near, CellAlign.Center, 1f);
                }

                // 联内小标志牌：结构同主牌，图案槽留空（受控图形不随联次复制）。
                SKPoint anchor = layout.CopyPlacardAnchor(i);
                DiamondPlacard mini = new DiamondPlacard(null, false, null);
                mini.StrokeWidthFactor = tokens.PlacardStrokeFactor;
                mini.Draw(p, anchor, tokens.CopyPlacardSize, palette.PlacardBorder);
                FontKey miniKey = new FontKey(p.Style.Text.Family,
                    tokens.CopyPlacardSize * tokens.ClassNumberFactor, true);
                p.TextInBox(ClassText(doc),
                    mini.NumberSlotOf(anchor, tokens.CopyPlacardSize), miniKey, palette.Ink,
                    CellAlign.Center, CellAlign.Center, 0.5f);
            }

            for (int boundary = 1; boundary < doc.Copies.Count; boundary++)
            {
                float? x = layout.PerforationX(boundary);
                if (x == null)
                {
                    continue;
                }
                p.DashedLine(x.Value, layout.CollateRect.Top, x.Value, layout.CollateRect.Bottom,
                    palette.Maintenance, tokens.PerforationStroke);
            }
        }

        private static void DrawTank(Painter p, DangerousGoodsLayout layout, DangerousGoodsDocument doc)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.CopyTextSize);

            if (string.IsNullOrEmpty(doc.TankPlacardText))
            {
                return;
            }

            p.TextInBox("罐体标志牌位置示意（轮廓为示意，非车型图）",
                new SKRect(layout.TankBand.Left, layout.TankBand.Top, layout.TankBand.Left + 280f,
                    layout.TankBand.Top + 14f),
                key, palette.MutedText, CellAlign.Near, CellAlign.Center, 2f);

            SKRect body = layout.TankBodyRect;
            p.Rect(body, palette.PanelBorder, 1f);
            p.Ellipse(new SKRect(body.Left - 8f, body.Top, body.Left + 8f, body.Bottom), palette.PanelBorder, 1f);
            p.Ellipse(new SKRect(body.Right - 8f, body.Top, body.Right + 8f, body.Bottom), palette.PanelBorder, 1f);

            SKRect placard = layout.TankPlacardRect;
            p.FillRect(placard, palette.PageBackground);
            p.Rect(placard, palette.PlacardBorder, 1.4f);
            p.TextInBox(ClassText(doc), placard,
                new FontKey(p.Style.Text.Family, tokens.CopyTextSize * 1.2f, true),
                palette.Ink, CellAlign.Center, CellAlign.Center, 0f);

            p.TextInBox(doc.TankPlacardText!,
                new SKRect(placard.Right + 8f, placard.Top, layout.TankBand.Right, placard.Bottom),
                key, palette.Text, CellAlign.Near, CellAlign.Center, 2f);
        }

        private static void DrawFooter(Painter p, DangerousGoodsLayout layout, DangerousGoodsDocument doc)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.Frame.FooterSize);
            p.TextInBox("UN 官方象形图为受控图形：本图只提供结构正确的菱形与类项号，上半槽图案需由用户按其 UN 原版注入；版面未校准。",
                layout.FooterRect, key, tokens.Palette.MutedText, CellAlign.Near, CellAlign.Center, 2f);
        }

        private static void DrawFrame(Painter p, DangerousGoodsLayout layout)
        {
            DangerousGoodsStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            SKRect rect = new SKRect(frame.Margin, frame.Margin, frame.Width - frame.Margin,
                frame.Height - frame.Margin);
            p.Frame(rect, tokens.Palette.Ink, frame.BorderStroke);
        }

        private static string ClassText(DangerousGoodsDocument doc)
        {
            string cls = string.IsNullOrEmpty(doc.Class) ? "—" : doc.Class;
            if (string.IsNullOrEmpty(doc.Division))
            {
                return cls;
            }
            return cls + "." + doc.Division;
        }

        /// <summary>按可用宽度截断文字（超出补省略号），避免压出表格。</summary>
        private static string Trim(Painter p, string text, float width, FontKey key)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f)
            {
                return text ?? string.Empty;
            }
            if (p.TextWidth(text, key) <= width)
            {
                return text;
            }

            string ellipsis = "…";
            float ellipsisWidth = p.TextWidth(ellipsis, key);
            for (int length = text.Length - 1; length > 0; length--)
            {
                string candidate = text.Substring(0, length);
                if (p.TextWidth(candidate, key) + ellipsisWidth <= width)
                {
                    return candidate + ellipsis;
                }
            }
            return ellipsis;
        }
    }
}
