using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms.DeathCertificate
{
    /// <summary>四联死亡证明的求解结果。</summary>
    public sealed class DeathCertificateLayout
    {
        private readonly DeathCertificateStyle _tokens;

        internal DeathCertificateLayout(SKRect bounds, SKRect header, SKRect body, SKRect footer,
            CollateSpec spec, IReadOnlyList<SKRect> copyRects, DeathCertificateStyle tokens)
        {
            Bounds = bounds;
            HeaderRect = header;
            BodyRect = body;
            FooterRect = footer;
            Spec = spec;
            CopyRects = copyRects;
            _tokens = tokens;
        }

        public SKRect Bounds { get; }
        public SKRect HeaderRect { get; }
        public SKRect BodyRect { get; }
        public SKRect FooterRect { get; }

        /// <summary>四联规格（联宽之和即版面宽）。</summary>
        public CollateSpec Spec { get; }

        public IReadOnlyList<SKRect> CopyRects { get; }

        public int CopyCount => CopyRects.Count;

        /// <summary>第 index 联的矩形（等价于 <see cref="CollateSpec.CopyRect"/>）。</summary>
        public SKRect CopyRect(int index)
        {
            if (index < 0 || index >= CopyRects.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return CopyRects[index];
        }

        /// <summary>第 index 联的竖排联名条（联内左缘）。</summary>
        public SKRect CopyLabelRect(int index)
        {
            SKRect copy = CopyRect(index);
            return new SKRect(copy.Left, copy.Top, copy.Left + _tokens.CopyLabelStripWidth, copy.Bottom);
        }

        /// <summary>第 index 联的行政区划代码栅格行（左上角锚点版本）。</summary>
        public SKRect AdminRowRect(int index)
        {
            SKRect copy = CopyRect(index);
            float left = copy.Left + _tokens.CopyLabelStripWidth + _tokens.CopyPadding;
            float top = copy.Top + _tokens.CopyPadding;
            return new SKRect(left, top, copy.Right - _tokens.CopyPadding, top + _tokens.AdminBoxSize);
        }

        /// <summary>第 index 联的编号栅格行。</summary>
        public SKRect SerialRowRect(int index)
        {
            SKRect admin = AdminRowRect(index);
            return new SKRect(admin.Left, admin.Bottom + _tokens.BoxRowGap,
                admin.Right, admin.Bottom + _tokens.BoxRowGap + _tokens.SerialBoxSize);
        }

        /// <summary>普通字段行矩形（隐藏字段也返回它"本应在"的位置，供像素对照）。</summary>
        public SKRect FieldRect(int index, string fieldKey)
        {
            int row = IndexIn(DeathCertificateFields.Ordered, fieldKey);
            if (row < 0)
            {
                throw new KeyNotFoundException("不是普通字段键：" + fieldKey);
            }
            return RowRect(index, row);
        }

        /// <summary>死因链 I.(a)–(d) 的行矩形。</summary>
        public SKRect CauseRect(int index, string causeKey)
        {
            int row = IndexIn(DeathCertificateFields.Causes, causeKey);
            if (row < 0)
            {
                throw new KeyNotFoundException("不是死因链字段键：" + causeKey);
            }
            SKRect serial = SerialRowRect(index);
            float top = serial.Bottom + _tokens.BoxBlockGap
                        + DeathCertificateFields.Ordered.Count * _tokens.RowHeight
                        + _tokens.CauseGap;
            SKRect copy = CopyRect(index);
            float left = serial.Left;
            return new SKRect(left, top + row * _tokens.RowHeight, copy.Right - _tokens.CopyPadding,
                top + (row + 1) * _tokens.RowHeight);
        }

        /// <summary>任一字段（普通或死因链）的行矩形。</summary>
        public SKRect AnyFieldRect(int index, string fieldKey)
        {
            if (IndexIn(DeathCertificateFields.Causes, fieldKey) >= 0)
            {
                return CauseRect(index, fieldKey);
            }
            return FieldRect(index, fieldKey);
        }

        private SKRect RowRect(int index, int row)
        {
            SKRect serial = SerialRowRect(index);
            SKRect copy = CopyRect(index);
            float top = serial.Bottom + _tokens.BoxBlockGap + row * _tokens.RowHeight;
            return new SKRect(serial.Left, top, copy.Right - _tokens.CopyPadding, top + _tokens.RowHeight);
        }

        private static int IndexIn(IReadOnlyList<string> keys, string key)
        {
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.Equals(keys[i], key, StringComparison.Ordinal)) return i;
            }
            return -1;
        }
    }

    /// <summary>
    /// 居民死亡医学证明（四联）medical.death-certificate。
    /// 四联由 <see cref="CollateSpec"/> 横向切分，每联只画自己可见的字段；
    /// 行政区划代码与编号分别用 6 格 / 17 格 <see cref="DigitBox"/>。
    /// </summary>
    public sealed class DeathCertificateTemplate : IChartTemplate
    {
        public string Key => "medical.death-certificate";
        public string DisplayName => "居民死亡医学证明（四联）";

        /// <summary>表单专属令牌；改令牌即改版面。</summary>
        public DeathCertificateStyle Tokens { get; set; } = new DeathCertificateStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            DeathCertificateDocument doc = Cast(document);
            DeathCertificateStyle t = Tokens;
            if (doc.Copies.Count == 0)
            {
                throw new InvalidOperationException("死亡证明至少要有一联。");
            }
            if (doc.Copies.Count > CollateSpec.MaxCopies)
            {
                throw new InvalidOperationException("联数 " + doc.Copies.Count + " 超过上限 "
                                                    + CollateSpec.MaxCopies + "。");
            }

            float width = t.CopyWidth * doc.Copies.Count;

            float y = 0f;
            float hospitalBottom = y + t.HospitalRowHeight;
            float titleBottom = hospitalBottom + t.TitleRowHeight;
            float headerBottom = titleBottom + t.NoteRowHeight;
            SKRect header = new SKRect(0f, 0f, width, headerBottom);
            y = headerBottom;

            float bodyHeight = t.CopyPadding * 2f
                               + t.AdminBoxSize + t.BoxRowGap + t.SerialBoxSize + t.BoxBlockGap
                               + DeathCertificateFields.Ordered.Count * t.RowHeight
                               + t.CauseGap
                               + DeathCertificateFields.Causes.Count * t.RowHeight;
            SKRect body = new SKRect(0f, y, width, y + bodyHeight);
            y = body.Bottom;

            SKRect footer = new SKRect(0f, y, width, y + t.FooterHeight);

            List<CopyDef> defs = new List<CopyDef>();
            foreach (DeathCopy copy in doc.Copies)
            {
                defs.Add(new CopyDef(copy.Key, copy.Label, t.CopyWidth,
                    copy.VisibleFields, copy.ExtraFields));
            }

            CollateSpec spec = new CollateSpec(defs, PerforationStyle.DashedCut,
                CollateDirection.HorizontalStrip);

            List<SKRect> copyRects = new List<SKRect>();
            for (int i = 0; i < spec.Count; i++)
            {
                copyRects.Add(spec.CopyRect(i, body));
            }

            DeathCertificateLayout layout = new DeathCertificateLayout(
                new SKRect(0f, 0f, width, footer.Bottom), header, body, footer, spec, copyRects, t);

            return ChartGeometry.ForLayout(layout.Bounds, body, layout);
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            DeathCertificateDocument doc = Cast(document);
            DeathCertificateLayout layout = geometry.LayoutOf<DeathCertificateLayout>()
                ?? throw new InvalidOperationException("几何里没有 DeathCertificateLayout。");

            ChartStyle style = painter.Style;
            DeathCertificateStyle t = Tokens;
            SKColor line = style.Colors.Grid;

            DrawHeader(painter, layout, doc, style, t);
            for (int i = 0; i < layout.CopyCount; i++)
            {
                DrawCopy(painter, layout, doc, style, t, i, line);
            }
            DrawSeams(painter, layout, style, t);

            painter.Line(0f, layout.BodyRect.Bottom, layout.Bounds.Right, layout.BodyRect.Bottom,
                line, style.Grid.BorderStroke);
            painter.Line(0f, layout.Bounds.Top, 0f, layout.Bounds.Bottom, line, style.Grid.BorderStroke);
            painter.Line(layout.Bounds.Right, layout.Bounds.Top, layout.Bounds.Right, layout.Bounds.Bottom,
                line, style.Grid.BorderStroke);
            painter.Line(0f, layout.Bounds.Bottom, layout.Bounds.Right, layout.Bounds.Bottom,
                line, style.Grid.BorderStroke);

            painter.TextInBox("本联与相邻联的字段差异由 visibleFields 控制；虚线为裁切线。",
                layout.FooterRect, new FontKey(style.Text.Family, style.Text.AnnotationSize),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 2f);
        }

        private static void DrawHeader(Painter p, DeathCertificateLayout layout, DeathCertificateDocument doc,
            ChartStyle style, DeathCertificateStyle t)
        {
            float hospitalBottom = layout.HeaderRect.Top + t.HospitalRowHeight;
            float titleBottom = hospitalBottom + t.TitleRowHeight;
            p.TextInBox(doc.HospitalName ?? string.Empty,
                new SKRect(0f, layout.HeaderRect.Top, layout.Bounds.Right, hospitalBottom),
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox(doc.FormTitle ?? "居民死亡医学证明",
                new SKRect(0f, hospitalBottom, layout.Bounds.Right, titleBottom),
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.HeaderRightNote))
            {
                p.TextInBox(doc.HeaderRightNote!,
                    new SKRect(0f, hospitalBottom, layout.Bounds.Right, titleBottom),
                    new FontKey(style.Text.Family, style.Text.HeaderSize),
                    style.Colors.Text, CellAlign.Far, CellAlign.Center, 6f);
            }

            p.TextInBox("四联：单位存根 / 公安 / 近亲属 / 殡仪馆",
                new SKRect(0f, titleBottom, layout.Bounds.Right, layout.HeaderRect.Bottom),
                new FontKey(style.Text.Family, style.Text.AnnotationSize),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);
        }

        private void DrawCopy(Painter p, DeathCertificateLayout layout, DeathCertificateDocument doc,
            ChartStyle style, DeathCertificateStyle t, int index, SKColor line)
        {
            SKRect copyRect = layout.CopyRect(index);
            DeathCopy def = doc.Copies[index];

            p.Rect(copyRect, line, style.Grid.MinorStroke);
            p.VerticalText(def.Label, layout.CopyLabelRect(index).MidX, layout.CopyLabelRect(index).Top + 6f,
                new FontKey(style.Text.Family, style.Text.CellLabelSize), style.Colors.Text);

            DrawDigitRow(p, layout.AdminRowRect(index), doc.AdminCode, t.AdminBoxCount, t.AdminBoxSize,
                "行政区划代码", style, line);
            DrawDigitRow(p, layout.SerialRowRect(index), doc.SerialNumber, t.SerialBoxCount, t.SerialBoxSize,
                "编号", style, line);

            FontKey valueKey = new FontKey(style.Text.Family, style.Text.CellValueSize);
            foreach (string key in DeathCertificateFields.Ordered)
            {
                if (!def.ShowsField(key))
                {
                    continue;
                }
                SKRect rect = layout.FieldRect(index, key);
                p.TextInBox(DeathCertificateFields.LabelOf(key) + "：",
                    new SKRect(rect.Left, rect.Top, rect.Left + t.FieldLabelWidth, rect.Bottom),
                    valueKey, style.Colors.Text, CellAlign.Near, CellAlign.Center, 2f);
                p.TextInBox(doc.Get(key),
                    new SKRect(rect.Left + t.FieldLabelWidth, rect.Top, rect.Right, rect.Bottom),
                    valueKey, style.Colors.CellValue, CellAlign.Near, CellAlign.Center, 2f);
            }

            foreach (string key in DeathCertificateFields.Causes)
            {
                if (!def.ShowsField(key))
                {
                    continue;
                }
                SKRect rect = layout.CauseRect(index, key);
                p.TextInBox(DeathCertificateFields.CauseMarkOf(key),
                    new SKRect(rect.Left, rect.Top, rect.Left + t.CauseLabelWidth, rect.Bottom),
                    new FontKey(style.Text.Family, style.Text.CellLabelSize),
                    style.Colors.Text, CellAlign.Near, CellAlign.Center, 1f);
                p.TextInBox(doc.Get(key),
                    new SKRect(rect.Left + t.CauseLabelWidth, rect.Top, rect.Right, rect.Bottom),
                    new FontKey(style.Text.Family, style.Text.CellValueSize),
                    style.Colors.CellValue, CellAlign.Near, CellAlign.Center, 1f);
            }
        }

        private static void DrawDigitRow(Painter p, SKRect row, string value, int boxCount, float size,
            string label, ChartStyle style, SKColor line)
        {
            DigitBox box = new DigitBox(boxCount, 0.62f, 0f);
            SKPoint topLeft = new SKPoint(row.Left, row.Top);
            box.Draw(p, new SKPoint(row.Left + box.RowWidthOf(size) / 2f, row.MidY), size, line);

            string digits = Normalize(value, boxCount);
            FontKey key = new FontKey(style.Text.Family, size * 0.62f);
            for (int i = 0; i < boxCount; i++)
            {
                SKRect cell = box.BoxRectFromLeft(topLeft, size, i);
                p.TextInBox(digits.Substring(i, 1), cell, key, style.Colors.Text,
                    CellAlign.Center, CellAlign.Center, 0f);
            }

            p.TextInBox(label,
                new SKRect(topLeft.X + box.RowWidthOf(size) + 4f, row.Top, row.Right, row.Bottom),
                new FontKey(style.Text.Family, style.Text.AnnotationSize),
                style.Colors.Text, CellAlign.Near, CellAlign.Center, 1f);
        }

        private static string Normalize(string value, int length)
        {
            string text = value ?? string.Empty;
            if (text.Length > length)
            {
                return text.Substring(text.Length - length);
            }
            if (text.Length < length)
            {
                return text.PadLeft(length, '0');
            }
            return text;
        }

        private static void DrawSeams(Painter p, DeathCertificateLayout layout, ChartStyle style,
            DeathCertificateStyle t)
        {
            for (int boundary = 1; boundary < layout.CopyCount; boundary++)
            {
                float? x = layout.Spec.PerforationAt(boundary, layout.BodyRect);
                if (x == null)
                {
                    continue;
                }
                p.DashedLine(x.Value, layout.BodyRect.Top, x.Value, layout.BodyRect.Bottom,
                    t.SeamColor, style.Grid.MinorStroke);
            }
        }

        private static DeathCertificateDocument Cast(ChartDocument document)
        {
            DeathCertificateDocument? typed = document as DeathCertificateDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(DeathCertificateDocument) + "。",
                    nameof(document));
            }
            return typed;
        }
    }
}
