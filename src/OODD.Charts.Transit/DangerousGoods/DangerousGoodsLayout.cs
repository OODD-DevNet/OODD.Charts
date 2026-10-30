using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Transit.DangerousGoods
{
    /// <summary>
    /// 危货联单版面：主/副菱形标志牌 + 头部字段表 + 多联单 + 罐体标志牌位置示意。
    /// <para>
    /// 菱形几何一律取自 Core 的 <see cref="DiamondPlacard"/>（顶点、上下图案槽、副牌偏移），
    /// 模板只负责落笔与写类项号；UN 官方象形图由注入的 <see cref="SKPath"/> 提供。
    /// </para>
    /// </summary>
    public sealed class DangerousGoodsLayout
    {
        /// <summary>联单字段的固定顺序（行槽位按它定，联间增删不挪行）。</summary>
        public static readonly string[] AllFields =
        {
            DangerousGoodsDocument.FUnNumber,
            DangerousGoodsDocument.FName,
            DangerousGoodsDocument.FClass,
            DangerousGoodsDocument.FDivision,
            DangerousGoodsDocument.FPackingGroup,
            DangerousGoodsDocument.FTunnel,
            DangerousGoodsDocument.FConsignor,
            DangerousGoodsDocument.FCarrier,
            DangerousGoodsDocument.FConsignee,
            DangerousGoodsDocument.FEmergency,
        };

        private static readonly DiamondPlacard Geometry = new DiamondPlacard();

        private readonly CollateSpec _collate;
        private readonly SKRect _collateRect;
        private readonly List<string> _headerFields;

        private DangerousGoodsLayout(DangerousGoodsDocument document, DangerousGoodsStyle tokens, SKRect bounds,
                                     SKRect titleRect, SKRect noteRect, SKPoint placardAnchor, float placardSize,
                                     SKRect headerFieldsRect, CollateSpec collate, SKRect collateRect,
                                     SKRect tankBand, SKRect tankBody, SKRect tankPlacard, SKRect footerRect)
        {
            Document = document;
            Tokens = tokens;
            Bounds = bounds;
            TitleRect = titleRect;
            NoteRect = noteRect;
            PlacardAnchor = placardAnchor;
            PlacardSize = placardSize;
            HeaderFieldsRect = headerFieldsRect;
            _collate = collate;
            _collateRect = collateRect;
            TankBand = tankBand;
            TankBodyRect = tankBody;
            TankPlacardRect = tankPlacard;
            FooterRect = footerRect;

            _headerFields = new List<string>
            {
                DangerousGoodsDocument.FUnNumber,
                DangerousGoodsDocument.FName,
                DangerousGoodsDocument.FClass,
                DangerousGoodsDocument.FDivision,
                DangerousGoodsDocument.FPackingGroup,
                DangerousGoodsDocument.FTunnel,
                DangerousGoodsDocument.FConsignor,
                DangerousGoodsDocument.FConsignee,
            };
            SubPlacardSize = placardSize * tokens.SubPlacardSizeFactor;
            SubPlacardAnchor = SubAnchorOf(placardAnchor, placardSize, SubPlacardSize, tokens.SubPlacardGapFactor);
        }

        /// <summary>副标志锚点：主牌右侧偏移后再把尺寸差折算成同心，和 <see cref="SubBounds"/> 同一口径。</summary>
        private static SKPoint SubAnchorOf(SKPoint anchor, float size, float subSize, float gapFactor)
        {
            return new SKPoint(
                anchor.X + DiamondPlacard.SubPlacardOffset(size, gapFactor).X + (subSize - size) * 0.5f,
                anchor.Y);
        }

        public DangerousGoodsDocument Document { get; }
        public DangerousGoodsStyle Tokens { get; }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect NoteRect { get; }
        public SKRect FooterRect { get; }

        /// <summary>主标志中心。</summary>
        public SKPoint PlacardAnchor { get; }

        public float PlacardSize { get; }

        /// <summary>副标志中心与尺寸（无副标志时仍给出几何，模板按需绘制）。</summary>
        public SKPoint SubPlacardAnchor { get; }

        public float SubPlacardSize { get; }

        /// <summary>头部字段表区域（2 列）。</summary>
        public SKRect HeaderFieldsRect { get; }

        public IReadOnlyList<string> HeaderFields => _headerFields;

        /// <summary>多联规格（联宽之和 == 版面宽，误差 0）。</summary>
        public CollateSpec Collate => _collate;

        public SKRect CollateRect => _collateRect;

        public SKRect TankBand { get; }
        public SKRect TankBodyRect { get; }
        public SKRect TankPlacardRect { get; }

        public static DangerousGoodsLayout Compute(DangerousGoodsDocument document, DangerousGoodsStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (document.Copies.Count == 0)
            {
                throw new InvalidOperationException("联单至少要有一联（Copies 为空）。");
            }
            if (!(tokens.PlacardSize > 0f))
            {
                throw new InvalidOperationException("菱形牌尺寸必须为正，实到 " + tokens.PlacardSize);
            }

            TransitStyle frame = tokens.Frame;
            float left = frame.Margin + frame.BindingMargin;
            float right = frame.Width - frame.Margin;
            SKRect bounds = new SKRect(0f, 0f, frame.Width, frame.Height);
            SKRect titleRect = new SKRect(left, frame.Margin, right, frame.Margin + frame.TitleHeight);
            SKRect noteRect = new SKRect(left, titleRect.Bottom, right, titleRect.Bottom + frame.SubtitleSize * 2.4f);
            SKRect footerRect = new SKRect(left, frame.Height - frame.Margin - frame.FooterHeight,
                right, frame.Height - frame.Margin);

            float headerTop = noteRect.Bottom + 4f;
            float size = tokens.PlacardSize;
            SKPoint anchor = new SKPoint(left + size * 0.5f + 4f, headerTop + size * 0.5f);

            // 头部字段区要让开主牌与副牌两者的右缘：只避主牌时，默认 150px 牌副标志会压住「类别/包装类别/托运人」。
            float subSize = size * tokens.SubPlacardSizeFactor;
            SKPoint subAnchor = SubAnchorOf(anchor, size, subSize, tokens.SubPlacardGapFactor);
            float placardsRight = Math.Max(anchor.X + size * 0.5f, subAnchor.X + subSize * 0.5f);
            float fieldsLeft = placardsRight + 34f;
            float fieldRowHeight = tokens.HeaderFieldRowHeight;
            int fieldRows = (_headerFieldsCount + 1) / 2;
            SKRect headerFields = new SKRect(fieldsLeft, headerTop, right,
                headerTop + fieldRows * fieldRowHeight);

            float copiesTop = Math.Max(anchor.Y + size * 0.5f + 24f, headerFields.Bottom + 14f) + 8f;
            SKRect collateRect = new SKRect(left, copiesTop, right, copiesTop + tokens.CopyHeight);

            float tankTop = collateRect.Bottom + 8f;
            SKRect tankBand = new SKRect(left, tankTop, right, tankTop + tokens.TankBandHeight);
            SKRect tankBody = new SKRect(left + 46f, tankBand.Top + (tokens.TankBandHeight - tokens.TankBodyHeight) * 0.5f + 8f,
                left + 46f + tokens.TankBodyWidth,
                tankBand.Top + (tokens.TankBandHeight - tokens.TankBodyHeight) * 0.5f + 8f + tokens.TankBodyHeight);
            SKRect tankPlacard = new SKRect(tankBody.Left + tokens.TankBodyWidth * 0.42f, tankBody.Top - 2f,
                tankBody.Left + tokens.TankBodyWidth * 0.42f + tokens.TankPlacardWidth,
                tankBody.Top - 2f + tokens.TankPlacardHeight);

            if (!(collateRect.Bottom < footerRect.Top))
            {
                throw new InvalidOperationException(
                    "联单区压到页脚：联单底 " + collateRect.Bottom.ToString("0.#", CultureInfo.InvariantCulture)
                    + "，页脚顶 " + footerRect.Top.ToString("0.#", CultureInfo.InvariantCulture));
            }
            if (!(tankBand.Bottom < footerRect.Top))
            {
                throw new InvalidOperationException("罐体示意区压到页脚。");
            }

            float available = right - left;
            double shareSum = 0d;
            for (int i = 0; i < document.Copies.Count; i++)
            {
                shareSum += document.Copies[i].Share;
            }
            if (!(shareSum > 0d))
            {
                throw new InvalidOperationException("联占比之和必须为正。");
            }

            List<CopyDef> defs = new List<CopyDef>(document.Copies.Count);
            float assigned = 0f;
            for (int i = 0; i < document.Copies.Count; i++)
            {
                DangerousGoodsCopy copy = document.Copies[i];
                float width = i == document.Copies.Count - 1
                    ? available - assigned
                    : (float)(available * copy.Share / shareSum);
                if (!(width > 0.01f))
                {
                    throw new InvalidOperationException("第 " + (i + 1) + " 联宽度非正：" + width.ToString("0.###", CultureInfo.InvariantCulture));
                }
                assigned += width;
                defs.Add(new CopyDef(copy.Key, copy.Label, width, copy.VisibleFields, copy.ExtraFields));
            }

            CollateSpec collate = new CollateSpec(defs, PerforationStyle.DashedCut, CollateDirection.HorizontalStrip);

            return new DangerousGoodsLayout(document, tokens, bounds, titleRect, noteRect, anchor, size,
                headerFields, collate, collateRect, tankBand, tankBody, tankPlacard, footerRect);
        }

        private const int _headerFieldsCount = 8;

        /// <summary>头部字段第 <paramref name="index"/> 项的矩形（2 列排布）。</summary>
        public SKRect HeaderFieldRect(int index)
        {
            if (index < 0 || index >= _headerFields.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            SKRect area = HeaderFieldsRect;
            float cellWidth = area.Width * 0.5f;
            int column = index % 2;
            int row = index / 2;
            float x = area.Left + column * cellWidth;
            float y = area.Top + row * Tokens.HeaderFieldRowHeight;
            return new SKRect(x, y, x + cellWidth, y + Tokens.HeaderFieldRowHeight);
        }

        /// <summary>第 <paramref name="copyIndex"/> 联的矩形。</summary>
        public SKRect CopyRect(int copyIndex)
        {
            return _collate.CopyRect(copyIndex, _collateRect);
        }

        /// <summary>联名表头矩形。</summary>
        public SKRect CopyLabelRect(int copyIndex)
        {
            SKRect copy = CopyRect(copyIndex);
            return new SKRect(copy.Left, copy.Top, copy.Right, copy.Top + Tokens.CopyHeaderHeight);
        }

        /// <summary>第 <paramref name="copyIndex"/> 联中某字段的行槽矩形（按固定字段顺序定位）。</summary>
        public SKRect CopyFieldRect(int copyIndex, string field)
        {
            int index = Array.IndexOf(AllFields, field);
            if (index < 0)
            {
                throw new ArgumentException("未知字段键：" + field, nameof(field));
            }
            SKRect copy = CopyRect(copyIndex);
            float top = copy.Top + Tokens.CopyHeaderHeight + index * Tokens.CopyRowHeight;
            return new SKRect(copy.Left + 2f, top, copy.Right - 2f, top + Tokens.CopyRowHeight);
        }

        /// <summary>第 <paramref name="copyIndex"/> 联要画的字段（固定顺序，只含该联显示的字段）。</summary>
        public IReadOnlyList<string> FieldsOf(int copyIndex)
        {
            CopyDef copy = _collate.Copies[copyIndex];
            List<string> fields = new List<string>(AllFields.Length);
            for (int i = 0; i < AllFields.Length; i++)
            {
                if (copy.ShowsField(AllFields[i]))
                {
                    fields.Add(AllFields[i]);
                }
            }
            return fields;
        }

        /// <summary>联内小标志牌中心。</summary>
        public SKPoint CopyPlacardAnchor(int copyIndex)
        {
            SKRect copy = CopyRect(copyIndex);
            float half = Tokens.CopyPlacardSize * 0.5f;
            return new SKPoint(copy.Right - half - 6f, copy.Top + Tokens.CopyHeaderHeight + half + 4f);
        }

        /// <summary>主标志四顶点（顶、右、底、左）。</summary>
        public SKPoint[] MainVertices()
        {
            return Geometry.VerticesOf(PlacardAnchor, PlacardSize);
        }

        public SKRect MainBounds()
        {
            return Geometry.BoundsOf(PlacardAnchor, PlacardSize);
        }

        /// <summary>主标志下半角类项号槽。</summary>
        public SKRect NumberSlot()
        {
            return Geometry.NumberSlotOf(PlacardAnchor, PlacardSize);
        }

        /// <summary>主标志上半角图案槽（用户注入的象形图会被符号缩放进这个矩形）。</summary>
        public SKRect SymbolSlot()
        {
            return Geometry.SymbolSlotOf(PlacardAnchor, PlacardSize);
        }

        public SKRect SubBounds()
        {
            return Geometry.BoundsOf(SubPlacardAnchor, SubPlacardSize);
        }

        public SKRect SubNumberSlot()
        {
            return Geometry.NumberSlotOf(SubPlacardAnchor, SubPlacardSize);
        }

        /// <summary>联间骑缝线（第 <paramref name="boundaryIndex"/> 条）的 x；无则 null。</summary>
        public float? PerforationX(int boundaryIndex)
        {
            return _collate.PerforationAt(boundaryIndex, _collateRect);
        }
    }
}
