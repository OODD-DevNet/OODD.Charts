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

namespace OODD.Charts.Transit.Stowage
{
    /// <summary>
    /// 集装箱积载图（Bay/Row/Tier 双视图）。
    /// <para>
    /// 左视图按 bay × tier 画纵向剖面，右视图按 row × tier 画横剖面；
    /// 箱块内部文字一律走 <see cref="Painter.Text"/>（保持可检索），
    /// 40 尺箱左对齐跨两个 20 尺 bay 位，tier 自底向上、row 自舷中向两舷非等距扩展。
    /// </para>
    /// <para>
    /// 版面数值全部走 <see cref="StowageStyle"/>，默认值<b>未校准</b>（D22）；
    /// 字段顺序已做成令牌，未与任何船公司图样对齐。
    /// </para>
    /// </summary>
    public sealed class StowageTemplate : IChartTemplate
    {
        public string Key => "transit.bay-plan";

        public string DisplayName => "集装箱积载图";

        /// <summary>版面令牌。</summary>
        public StowageStyle Tokens { get; set; } = new StowageStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            StowageDocument doc = Cast(document);
            StowageLayout layout = StowageLayout.Compute(doc, Tokens);
            return ChartGeometry.ForLayout(layout.Bounds, layout.ProfilePanel, layout);
        }

        private static StowageDocument Cast(ChartDocument document)
        {
            StowageDocument? typed = document as StowageDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(StowageDocument) + "。", nameof(document));
            }
            return typed;
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            StowageDocument doc = Cast(document);
            StowageLayout layout = geometry.LayoutOf<StowageLayout>()
                                   ?? StowageLayout.Compute(doc, Tokens);
            TransitPalette palette = layout.Tokens.Palette;

            painter.FillRect(layout.Bounds, palette.PageBackground);

            DrawTitle(painter, layout, doc);
            DrawPanels(painter, layout, doc);
            DrawContainerText(painter, layout, doc, true);
            DrawContainerText(painter, layout, doc, false);
            DrawLegend(painter, layout, doc);
            DrawFrame(painter, layout);
        }

        private static void DrawTitle(Painter p, StowageLayout layout, StowageDocument doc)
        {
            StowageStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            TransitPalette palette = tokens.Palette;

            string title = string.IsNullOrEmpty(doc.FormTitle) ? "集装箱积载图" : doc.FormTitle!;
            p.TextInBox(title, layout.TitleRect, new FontKey(p.Style.Text.Family, frame.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(doc.VesselId))
            {
                parts.Add(doc.VesselId!);
            }
            if (!string.IsNullOrEmpty(doc.VoyageId))
            {
                parts.Add("航次 " + doc.VoyageId);
            }
            parts.Add("Bay " + doc.Bays.Count.ToString(CultureInfo.InvariantCulture)
                      + " 个／箱 " + doc.Containers.Count.ToString(CultureInfo.InvariantCulture) + " 只");
            parts.Add("字段顺序与分色未与船公司图样对齐（未校准）");

            p.TextInBox(string.Join("　", parts.ToArray()), layout.NoteRect,
                new FontKey(p.Style.Text.Family, frame.SubtitleSize),
                palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawPanels(Painter p, StowageLayout layout, StowageDocument doc)
        {
            StowageStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey titleKey = new FontKey(p.Style.Text.Family, tokens.PanelTitleSize, true);

            p.Rect(layout.ProfilePanel, palette.PanelBorder, 0.8f);
            p.Rect(layout.SectionPanel, palette.PanelBorder, 0.8f);

            string profileTitle = "纵向剖面（Bay "
                + layout.Axis.BayNumbers[0].ToString("00", CultureInfo.InvariantCulture)
                + "–" + layout.Axis.BayNumbers[layout.Axis.BayNumbers.Count - 1].ToString("00", CultureInfo.InvariantCulture) + "）";
            p.TextInBox(profileTitle,
                new SKRect(layout.ProfilePanel.Left, layout.ProfilePanel.Top,
                    layout.ProfilePanel.Right, layout.ProfilePanel.Top + tokens.PanelTitleSize * 2f),
                titleKey, palette.SubtitleText, CellAlign.Near, CellAlign.Center, 4f);

            p.TextInBox("横剖面（Bay " + doc.CrossSectionBay.ToString("00", CultureInfo.InvariantCulture) + "）",
                new SKRect(layout.SectionPanel.Left, layout.SectionPanel.Top,
                    layout.SectionPanel.Right, layout.SectionPanel.Top + tokens.PanelTitleSize * 2f),
                titleKey, palette.SubtitleText, CellAlign.Near, CellAlign.Center, 4f);

            DrawBays(p, layout);
            DrawTiers(p, layout);
            DrawSectionFrame(p, layout, doc);

            foreach (StowageContainer c in doc.Containers)
            {
                if (layout.ProfileVisible(c))
                {
                    DrawContainer(p, layout, layout.ProfileRectOf(c), c);
                }
                if (layout.SectionVisible(c))
                {
                    DrawContainer(p, layout, layout.SectionRectOf(c), c);
                }
            }
        }

        private static void DrawBays(Painter p, StowageLayout layout)
        {
            StowageStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.LabelSize);
            SKRect plot = layout.Axis.ProfilePlot;

            for (int i = 0; i < layout.Axis.BayAxis.Count; i++)
            {
                float left = layout.Axis.BayAxis.PositionOf(i);
                float width = layout.Axis.BayAxis.WidthOf(i);
                p.Line(left, plot.Top, left, plot.Bottom, palette.Grid, 0.5f);
                p.TextInBox(layout.Axis.BayAxis.LabelOf(i),
                    new SKRect(left, plot.Bottom + 1f, left + width, plot.Bottom + tokens.LabelSize * 2f),
                    key, palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }
            p.Line(plot.Right, plot.Top, plot.Right, plot.Bottom, palette.Grid, 0.5f);
            p.Line(plot.Left, plot.Bottom, plot.Right, plot.Bottom, palette.Ink, 1f);
        }

        private static void DrawTiers(Painter p, StowageLayout layout)
        {
            StowageStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.LabelSize);
            SKRect plot = layout.Axis.ProfilePlot;

            for (int tier = 1; tier <= layout.Axis.TierCount; tier++)
            {
                SKRect band = layout.Axis.TierBand(tier, layout.ProfilePanel.Left, plot.Right);
                p.Line(plot.Left, band.Bottom, plot.Right, band.Bottom, palette.Grid, 0.4f);

                p.TextInBox(tier.ToString("00", CultureInfo.InvariantCulture),
                    new SKRect(layout.ProfilePanel.Left + 2f, band.Top, plot.Left - 3f, band.Bottom),
                    key, palette.Text, CellAlign.Far, CellAlign.Center, 0f);
            }
        }

        private static void DrawSectionFrame(Painter p, StowageLayout layout, StowageDocument doc)
        {
            StowageStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.LabelSize);
            SKRect plot = layout.Axis.SectionPlot;
            StowageBay? bay = doc.BayOf(doc.CrossSectionBay);

            p.Line(plot.Left, plot.Bottom, plot.Right, plot.Bottom, palette.Ink, 1f);
            p.DashedLine(layout.Axis.CenterlineX, plot.Top, layout.Axis.CenterlineX, plot.Bottom,
                palette.Maintenance, tokens.CenterlineStroke);

            for (int tier = 1; tier <= layout.Axis.TierCount; tier++)
            {
                SKRect band = layout.Axis.TierBand(tier, layout.SectionPanel.Left, plot.Right);
                p.Line(plot.Left, band.Bottom, plot.Right, band.Bottom, palette.Grid, 0.4f);
                p.TextInBox(tier.ToString("00", CultureInfo.InvariantCulture),
                    new SKRect(layout.SectionPanel.Left + 2f, band.Top, plot.Left - 3f, band.Bottom),
                    key, palette.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            if (bay == null)
            {
                return;
            }

            for (int row = 1; row <= bay.PortRows * 2; row += 2)
            {
                DrawRowLabel(p, layout, row, key);
            }
            for (int row = 2; row <= bay.StarboardRows * 2; row += 2)
            {
                DrawRowLabel(p, layout, row, key);
            }

            p.TextInBox("舷中", new SKRect(layout.Axis.CenterlineX - 16f, plot.Bottom + tokens.LabelSize,
                    layout.Axis.CenterlineX + 16f, plot.Bottom + tokens.LabelSize * 2.2f),
                key, palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawRowLabel(Painter p, StowageLayout layout, int row, FontKey key)
        {
            float center = layout.Axis.RowCenter(row);
            p.TextInBox(row.ToString("00", CultureInfo.InvariantCulture),
                new SKRect(center - 14f, layout.Axis.SectionPlot.Bottom + 1f,
                    center + 14f, layout.Axis.SectionPlot.Bottom + layout.Tokens.LabelSize * 2f),
                key, layout.Tokens.Palette.Text, CellAlign.Center, CellAlign.Center, 0f);
        }

        private static void DrawContainer(Painter p, StowageLayout layout, SKRect rect, StowageContainer c)
        {
            StowageStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;

            p.FillRect(rect, layout.PortColorOf(c.PortCode));
            p.Rect(rect, palette.Ink, tokens.ContainerStroke);

            float mark = tokens.CornerMarkSize;
            if (c.OverHeight)
            {
                using (SKPath triangle = p.BuildPath(new[]
                {
                    new SKPoint(rect.Left + 1.5f, rect.Top + 1.5f + mark),
                    new SKPoint(rect.Left + 1.5f + mark, rect.Top + 1.5f),
                    new SKPoint(rect.Left + 1.5f, rect.Top + 1.5f),
                }, true))
                {
                    p.FillPath(triangle, palette.Accent);
                }
            }
            if (c.IsHeavy)
            {
                p.FillRect(new SKRect(rect.Right - 1.5f - mark, rect.Top + 1.5f, rect.Right - 1.5f,
                    rect.Top + 1.5f + mark), palette.Secondary);
            }
        }

        /// <summary>箱块文字单独重绘一遍：保证在任何底色上都是最后一层，也是可检索文本。</summary>
        private static void DrawContainerText(Painter p, StowageLayout layout, StowageDocument doc, bool profile)
        {
            StowageStyle tokens = layout.Tokens;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.ContainerTextSize);

            for (int i = 0; i < doc.Containers.Count; i++)
            {
                StowageContainer c = doc.Containers[i];
                if (profile ? !layout.ProfileVisible(c) : !layout.SectionVisible(c))
                {
                    continue;
                }

                SKRect rect = profile ? layout.ProfileRectOf(c) : layout.SectionRectOf(c);
                p.TextInBox(layout.TextOf(c), new SKRect(rect.Left + 1f, rect.Top + 1f, rect.Right - 1f, rect.Bottom - 1f),
                    key, tokens.Palette.Ink, CellAlign.Center, CellAlign.Center, 1f);
            }
        }

        private static void DrawLegend(Painter p, StowageLayout layout, StowageDocument doc)
        {
            StowageStyle tokens = layout.Tokens;
            TransitPalette palette = tokens.Palette;
            FontKey key = new FontKey(p.Style.Text.Family, tokens.LegendSize);
            SKRect footer = layout.FooterRect;

            float x = footer.Left + 2f;
            p.Text("目的港：", x, footer.MidY + 3f, key, palette.MutedText);
            x += p.TextWidth("目的港：", key) + 4f;

            for (int i = 0; i < layout.Ports.Count; i++)
            {
                string port = layout.Ports[i];
                SKRect swatch = new SKRect(x, footer.MidY - 4f, x + 10f, footer.MidY + 4f);
                p.FillRect(swatch, layout.PortColorOf(port));
                p.Rect(swatch, palette.PanelBorder, 0.5f);
                x += 13f;
                p.Text(port, x, footer.MidY + 3f, key, palette.Text);
                x += p.TextWidth(port, key) + 10f;
            }

            p.Text("左上角▲ = 超限高箱，右上角■ = 重箱（≥24t 示例阈值）", x, footer.MidY + 3f, key, palette.MutedText);
        }

        private static void DrawFrame(Painter p, StowageLayout layout)
        {
            StowageStyle tokens = layout.Tokens;
            TransitStyle frame = tokens.Frame;
            SKRect rect = new SKRect(frame.Margin, frame.Margin, frame.Width - frame.Margin,
                frame.Height - frame.Margin);
            p.Frame(rect, tokens.Palette.Ink, frame.BorderStroke);
        }
    }
}
