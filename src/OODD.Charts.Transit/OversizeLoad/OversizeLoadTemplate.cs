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

namespace OODD.Charts.Transit.OversizeLoad
{
    /// <summary>
    /// 超限运输许可附图：车货侧面外廓 + 轴距链 + 总高尺寸 + 关键高度坐标式 + 轴荷带。
    /// <para>
    /// 尺寸数字一律由 <see cref="DimensionRun.Measured"/>（像素回写）生成，模板不填数字；
    /// 链式各段共用地面切点、延伸线留 gap，轴荷条高与轴荷成比例。
    /// </para>
    /// <para>
    /// 版面数值全部走 <see cref="OversizeLoadStyle"/>，默认值<b>未校准</b>（D22）。
    /// </para>
    /// </summary>
    public sealed class OversizeLoadTemplate : IChartTemplate
    {
        public string Key => "transit.ogami-profile";

        public string DisplayName => "超限运输许可附图";

        public OversizeLoadStyle Tokens { get; set; } = new OversizeLoadStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            OversizeLoadDocument doc = Cast(document);
            OversizeLoadLayout layout = OversizeLoadLayout.Compute(doc, Tokens);
            return ChartGeometry.ForLayout(layout.Bounds, layout.DrawingRect, layout);
        }

        private static OversizeLoadDocument Cast(ChartDocument document)
        {
            OversizeLoadDocument? typed = document as OversizeLoadDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(OversizeLoadDocument) + "。", nameof(document));
            }
            return typed;
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            OversizeLoadDocument doc = Cast(document);
            OversizeLoadLayout layout = geometry.LayoutOf<OversizeLoadLayout>()
                                         ?? OversizeLoadLayout.Compute(doc, Tokens);
            TransitPalette palette = layout.Tokens.Palette;

            painter.FillRect(layout.Bounds, palette.PageBackground);

            DrawTitle(painter, layout, doc);
            DrawProfile(painter, layout, doc);
            DrawAxles(painter, layout, doc);
            DrawDimensions(painter, layout, doc);
            DrawFooter(painter, layout, doc);
            DrawFrame(painter, layout);
        }

        private static void DrawTitle(Painter p, OversizeLoadLayout layout, OversizeLoadDocument doc)
        {
            OversizeLoadStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            TransitPalette palette = tokens.Palette;

            string title = string.IsNullOrEmpty(doc.FormTitle) ? "超限运输许可附图" : doc.FormTitle!;
            p.TextInBox(title, layout.TitleRect, new FontKey(p.Style.Text.Family, frame.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(doc.PermitNumber))
            {
                parts.Add("许可证 " + doc.PermitNumber);
            }
            if (!string.IsNullOrEmpty(doc.VehiclePlate))
            {
                parts.Add("车牌 " + doc.VehiclePlate);
            }
            if (!string.IsNullOrEmpty(doc.CargoName))
            {
                parts.Add("货物 " + doc.CargoName);
            }
            parts.Add("总长 " + doc.LengthMeters.ToString("0.00", CultureInfo.InvariantCulture)
                      + " m × 总宽 " + doc.WidthMeters.ToString("0.00", CultureInfo.InvariantCulture)
                      + " m × 总高 " + doc.HeightMeters.ToString("0.00", CultureInfo.InvariantCulture) + " m");
            parts.Add("轴 " + doc.Axles.Count.ToString(CultureInfo.InvariantCulture)
                      + " 位／总轴荷 " + doc.TotalLoadTonnes().ToString("0.0", CultureInfo.InvariantCulture) + " t");

            p.TextInBox(string.Join("　", parts.ToArray()), layout.NoteRect,
                new FontKey(p.Style.Text.Family, frame.SubtitleSize),
                palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawProfile(Painter p, OversizeLoadLayout layout, OversizeLoadDocument doc)
        {
            OversizeLoadStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;

            List<SKPoint> points = new List<SKPoint>(doc.Profile.Count);
            for (int i = 0; i < doc.Profile.Count; i++)
            {
                points.Add(layout.PointOf(doc.Profile[i].XMeters, doc.Profile[i].YMeters));
            }

            using (SKPath path = p.BuildPath(points, true))
            {
                p.FillPath(path, palette.PageBackground);
                p.StrokePath(path, palette.Ink, tokens.ProfileStroke);
            }

            float left = layout.PointOf(0d, 0d).X;
            float right = layout.PointOf(doc.LengthMeters, 0d).X;
            p.Line(left - 18f, layout.GroundY, right + 18f, layout.GroundY, palette.Ink, tokens.GroundStroke);

            // 轴荷条：高度与轴荷成比例（轴荷带的读法就是"条越高轴越重"）。
            for (int i = 0; i < doc.Axles.Count; i++)
            {
                SKRect bar = layout.AxleBarRect(doc.Axles[i]);
                p.FillRect(bar, palette.Secondary);
                p.Rect(bar, palette.Ink, 0.5f);
                p.DashedLine(layout.PointOf(doc.Axles[i].PositionMeters, 0d).X, layout.GroundY + 2f,
                    layout.PointOf(doc.Axles[i].PositionMeters, 0d).X, bar.Top, palette.Maintenance, 0.5f);
            }
        }

        private static void DrawAxles(Painter p, OversizeLoadLayout layout, OversizeLoadDocument doc)
        {
            OversizeLoadStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.AxleLabelSize);

            for (int i = 0; i < doc.Axles.Count; i++)
            {
                OversizeAxle axle = doc.Axles[i];
                SKPoint center = layout.WheelCenterOf(axle);
                float radius = layout.WheelRadiusPixels;
                p.FillCircle(center, radius, palette.PageBackground);
                p.Circle(center, radius, palette.Ink, 1.2f);
                p.FillCircle(center, radius * 0.3f, palette.Ink);

                SKRect bar = layout.AxleBarRect(axle);
                p.TextInBox(axle.LoadTonnes.ToString("0.0", CultureInfo.InvariantCulture) + "t",
                    new SKRect(bar.Left - 12f, bar.Bottom + 1f, bar.Right + 12f,
                        bar.Bottom + 1f + tokens.AxleLabelSize * 1.6f),
                    key, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }
        }

        private static void DrawDimensions(Painter p, OversizeLoadLayout layout, OversizeLoadDocument doc)
        {
            OversizeLoadStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            DimensionTokens dimension = new DimensionTokens();
            dimension.TextOffset = tokens.DimensionTextOffset;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.DimensionTextSize);

            DimensionPainter.DrawSet(p, layout.Chain.Runs, dimension, palette.Ink, palette.Text, key, false);
            DimensionPainter.Draw(p, layout.HeightRun, dimension, palette.Ink, palette.Text, key);
            DimensionPainter.DrawSet(p, layout.HeightOrdinates.Runs, dimension, palette.Ink, palette.Text, key, true);

            p.TextInBox("总高", new SKRect(layout.DimensionX + 6f, layout.TopY - 14f,
                    layout.DimensionX + 46f, layout.TopY),
                new FontKey(p.Style.Text.Family, tokens.NoteSize), palette.MutedText, CellAlign.Near,
                CellAlign.Center, 0f);

            p.TextInBox("允许偏差 ±" + doc.ToleranceMillimeters.ToString("0", CultureInfo.InvariantCulture) + " mm",
                new SKRect(layout.OriginX, layout.DrawingRect.Top, layout.OriginX + 240f,
                    layout.DrawingRect.Top + tokens.NoteSize * 2f),
                new FontKey(p.Style.Text.Family, tokens.NoteSize), palette.MutedText, CellAlign.Near,
                CellAlign.Center, 0f);
        }

        private static void DrawFooter(Painter p, OversizeLoadLayout layout, OversizeLoadDocument doc)
        {
            OversizeLoadStyle tokens = layout.Tokens;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.Frame.FooterSize);
            string text = "尺寸数字由坐标回写（非手填）；轴距链各段首尾相接、总和 = 端到端；延伸线留 gap 不穿轮廓；"
                          + "外廓折点为示例数据，非真实车型；版面未校准。";
            p.TextInBox(text, layout.FooterRect, key, tokens.Palette.MutedText, CellAlign.Near, CellAlign.Center, 2f);
        }

        private static void DrawFrame(Painter p, OversizeLoadLayout layout)
        {
            OversizeLoadStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            SKRect rect = new SKRect(frame.Margin, frame.Margin, frame.Width - frame.Margin,
                frame.Height - frame.Margin);
            p.Frame(rect, tokens.Palette.Ink, frame.BorderStroke);
        }
    }
}
