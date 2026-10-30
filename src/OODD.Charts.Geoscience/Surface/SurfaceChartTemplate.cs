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

namespace OODD.Charts.Geoscience.Surface
{
    /// <summary>
    /// 站点符号位里四个要素的文字框。四个象限分别对应 <see cref="ElementAnchorSlot"/>，
    /// 风杆只给"标称槽位"矩形——风杆绕站圈圆心旋转，其真实墨迹范围随风向变化，
    /// 槽位矩形表达的是"这个要素占哪一象限"，不与风向绑定。
    /// </summary>
    public readonly struct SurfaceElementBoxes
    {
        internal SurfaceElementBoxes(SKRect temperature, SKRect dewPoint, SKRect pressure, SKRect windBarb)
        {
            Temperature = temperature;
            DewPoint = dewPoint;
            Pressure = pressure;
            WindBarb = windBarb;
        }

        public SKRect Temperature { get; }
        public SKRect DewPoint { get; }
        public SKRect Pressure { get; }
        public SKRect WindBarb { get; }
    }

    /// <summary>
    /// 地面天气图填图（模板键 <c>geoscience.surface-chart</c>），含高空等压面多层堆叠开关。
    /// <para>
    /// 版面：每个层次一个面板；站圈为中心，左上气温、右上露点、右下海压、左下风杆
    /// （象限由 <see cref="ElementAnchorSlot"/> 令牌选，距离由 <c>ElementGap</c> 给）。
    /// 台风杆的羽旗全部是几何，经 <see cref="Painter.RotateAround"/> 旋转；符号内不落文字，
    /// 因此 SVG 导出后站旁文字仍是可检索的文本节点。
    /// </para>
    /// <para>
    /// <b>未校准</b>：站位尺寸/要素间距/网格密度是演示值，未与 MH/T 4016.8-2008、
    /// QX/T 804-2025、WMO No.306 FM-12 的填图图样逐项对齐（docs/06 D22）。
    /// </para>
    /// </summary>
    public sealed class SurfaceChartTemplate : IChartTemplate
    {
        public string Key => "geoscience.surface-chart";

        public string DisplayName => "地面天气图填图";

        public SurfaceChartStyle Tokens { get; set; } = new SurfaceChartStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            SurfacePlotDocument doc = Cast(document);
            SurfaceChartStyle tokens = Tokens;
            GeoscienceStyle paper = tokens.Base;

            float left = paper.Margin;
            float right = paper.Width - paper.RightPad;
            SKRect bounds = new SKRect(0f, 0f, paper.Width, paper.Height);
            SKRect titleRect = new SKRect(left, paper.Margin, right, paper.Margin + paper.TitleHeight);
            float footerTop = paper.Height - paper.Margin - paper.FooterHeight;
            SKRect footerRect = new SKRect(left, footerTop, right, footerTop + paper.FooterHeight);

            int count = PanelCountOf(doc);
            float top = titleRect.Bottom;
            float bottom = footerTop;
            float gap = tokens.PanelGap;
            float panelHeight = (bottom - top - gap * (count - 1)) / count;

            List<SurfacePanel> panels = new List<SurfacePanel>(count);
            for (int i = 0; i < count; i++)
            {
                float panelTop = top + i * (panelHeight + gap);
                SKRect rect = new SKRect(left, panelTop, right, panelTop + panelHeight);
                SKRect content = new SKRect(
                    rect.Left + tokens.PanelInset,
                    rect.Top + tokens.PanelInset,
                    rect.Right - tokens.PanelInset,
                    rect.Bottom - tokens.PanelInset);
                SurfaceLevelPlot level = doc.Levels[i];
                panels.Add(new SurfacePanel(rect, content, level.PressureHpa, level.Label));
            }

            SurfaceChartLayout layout = new SurfaceChartLayout(bounds, titleRect, footerRect, panels);
            return ChartGeometry.ForLayout(bounds, layout.PlotRect, layout);
        }

        /// <summary>实际画几个面板：堆叠开关关闭时只画第一个层次。</summary>
        public static int PanelCountOf(SurfacePlotDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            return document.StackPressureLevels ? Math.Max(1, document.Levels.Count) : 1;
        }

        private static SurfacePlotDocument Cast(ChartDocument document)
        {
            SurfacePlotDocument? typed = document as SurfacePlotDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(SurfacePlotDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geometry, ChartDocument document)
        {
            SurfacePlotDocument doc = Cast(document);
            SurfaceChartLayout? layout = geometry.LayoutOf<SurfaceChartLayout>();
            if (layout == null)
            {
                throw new InvalidOperationException("版面对象缺失：本模板的落点全部来自 SurfaceChartLayout。");
            }

            SurfaceChartStyle tokens = Tokens;
            GeosciencePalette palette = tokens.Palette;
            GeoscienceStyle paper = tokens.Base;

            p.TextInBox(NullToEmpty(doc.FormTitle), layout.TitleRect,
                new FontKey(p.Style.Text.Family, paper.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.ValidTimeText))
            {
                p.TextInBox(doc.ValidTimeText!, layout.TitleRect,
                    new FontKey(p.Style.Text.Family, paper.SubtitleSize),
                    palette.SubtitleText, CellAlign.Far, CellAlign.Center, 2f);
            }

            p.TextInBox(tokens.FooterNote, layout.FooterRect,
                new FontKey(p.Style.Text.Family, paper.FooterSize),
                palette.MutedText, CellAlign.Near, CellAlign.Center, 0f);

            p.Rect(layout.Bounds, palette.Ink, paper.BorderStroke);

            for (int i = 0; i < layout.Panels.Count; i++)
            {
                SurfacePanel panel = layout.Panels[i];
                SurfaceLevelPlot level = doc.Levels[i];
                DrawPanelFrame(p, panel, tokens, paper);
                foreach (SurfaceStation station in level.Stations)
                {
                    DrawStation(p, layout, panel, station, tokens);
                }
            }
        }

        private static void DrawPanelFrame(Painter p, SurfacePanel panel, SurfaceChartStyle tokens, GeoscienceStyle paper)
        {
            GeosciencePalette palette = tokens.Palette;
            p.Frame(panel.Rect, palette.PanelBorder, paper.BorderStroke);

            int divisions = tokens.GridDivisions;
            for (int i = 1; i < divisions; i++)
            {
                float t = (float)i / divisions;
                float x = panel.Rect.Left + panel.Rect.Width * t;
                float y = panel.Rect.Top + panel.Rect.Height * t;
                p.Line(x, panel.Rect.Top, x, panel.Rect.Bottom, palette.Grid, paper.GridStroke);
                p.Line(panel.Rect.Left, y, panel.Rect.Right, y, palette.Grid, paper.GridStroke);
            }

            p.TextInBox(panel.Label,
                new SKRect(panel.Rect.Left + 4f, panel.Rect.Top + 2f, panel.Rect.Right - 4f, panel.Rect.Top + 2f + paper.SubtitleSize * 1.6f),
                new FontKey(p.Style.Text.Family, paper.SubtitleSize),
                palette.SubtitleText, CellAlign.Near, CellAlign.Center, 0f);
        }

        private void DrawStation(Painter p, SurfaceChartLayout layout, SurfacePanel panel,
                                 SurfaceStation station, SurfaceChartStyle tokens)
        {
            GeosciencePalette palette = tokens.Palette;
            float size = tokens.StationSize;
            float radius = size * tokens.StationRadiusFactor;
            float stroke = size * 0.06f;
            SKPoint center = layout.CenterOf(panel, station);

            p.Circle(center, radius, palette.Ink, stroke);
            DrawCloud(p, center, radius - stroke, station.CloudOktas, palette.MutedText);

            SurfaceElementBoxes boxes = ElementBoxesOf(station, center, size, p.Fonts, p.Style.Text.Family, tokens);
            FontKey elementFont = new FontKey(p.Style.Text.Family, tokens.ElementTextSize);

            DrawElement(p, TemperatureTextOf(station, tokens), boxes.Temperature, tokens.TemperatureSlot,
                elementFont, palette.Text);
            DrawElement(p, DewPointTextOf(station, tokens), boxes.DewPoint, tokens.DewPointSlot,
                elementFont, palette.Accent);
            DrawElement(p, PressureTextOf(station, tokens), boxes.Pressure, tokens.PressureSlot,
                elementFont, palette.Text);

            FontKey nameFont = new FontKey(p.Style.Text.Family, tokens.StationNameSize);
            p.TextInBox(station.Name, StationNameBoxOf(station, center, size, tokens),
                nameFont, palette.SubtitleText, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(station.WeatherCode))
            {
                float codeY = center.Y + size * tokens.WeatherCodeOffsetFactor;
                p.TextInBox(station.WeatherCode!,
                    new SKRect(center.X - size * 1.2f, codeY - tokens.WeatherCodeSize,
                               center.X + size * 1.2f, codeY + tokens.WeatherCodeSize),
                    new FontKey(p.Style.Text.Family, tokens.WeatherCodeSize),
                    palette.MutedText, CellAlign.Center, CellAlign.Center, 0f);
            }

            WindBarb barb = BarbOf(station, tokens);

            if (barb.IsCalm)
            {
                // 静风：画一个小圈表示"风向未定"，不画杆（零长杆落任何墨都是猜方向）。
                p.Circle(center, radius * 0.45f, palette.Ink, stroke);
                return;
            }

            barb.Draw(p, center, size, palette.Ink);
        }

        private static void DrawElement(Painter p, string text, SKRect box, ElementAnchorSlot slot,
                                        FontKey font, SKColor color)
        {
            bool onRight = slot == ElementAnchorSlot.TopRight || slot == ElementAnchorSlot.BottomRight;
            CellAlign horizontal = onRight ? CellAlign.Near : CellAlign.Far;
            p.TextInBox(text, box, font, color, horizontal, CellAlign.Center, 0f);
        }

        private static void DrawCloud(Painter p, SKPoint center, float radius, int oktas, SKColor color)
        {
            int clamped = oktas < 0 ? 0 : oktas > 8 ? 8 : oktas;
            if (clamped == 0 || radius <= 0f)
            {
                return;
            }

            float sweep = 360f * clamped / 8f;
            using (SKPath path = new SKPath())
            {
                path.MoveTo(center);
                path.AddArc(new SKRect(center.X - radius, center.Y - radius,
                                       center.X + radius, center.Y + radius), -90f, sweep);
                path.Close();
                p.FillPath(path, color);
            }
        }

        /// <summary>本模板的风杆实例（羽旗规则与两处令牌同源，绘制与站名避让共用）。</summary>
        public static WindBarb BarbOf(SurfaceStation station, SurfaceChartStyle tokens)
        {
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            return new WindBarb((float)station.WindDirectionDegrees, KnotsOf(station, tokens),
                                WindBarb.BarbRule.WmoStandard)
            {
                StationRadiusFactor = tokens.StationRadiusFactor,
                BarbsOnLeft = tokens.BarbsOnLeft,
            };
        }

        /// <summary>
        /// 站名文字框。默认在站圈正上方；风杆朝上（羽旗伸到站名带）时整体让到羽旗之上，
        /// 否则杆与羽旗会压掉站名（T292 取证发现）。
        /// </summary>
        public static SKRect StationNameBoxOf(SurfaceStation station, SKPoint center, float stationSize,
                                              SurfaceChartStyle tokens)
        {
            float halfHeight = tokens.StationNameSize;
            float nameY = center.Y - stationSize * tokens.StationNameOffsetFactor;

            WindBarb barb = BarbOf(station, tokens);
            if (!barb.IsCalm)
            {
                SKPoint tip = barb.TipOf(center, stationSize);
                float upper = tip.Y - stationSize * barb.FullBarbLengthFactor;
                if (upper < nameY + halfHeight)
                {
                    nameY = upper - halfHeight;
                }
            }

            return new SKRect(center.X - stationSize * 2f, nameY - halfHeight,
                              center.X + stationSize * 2f, nameY + halfHeight);
        }

        // ------------------------------------------------------------------ 要素锚点

        /// <summary>站圈四象限的要素文字框。Measure 与 DrawContent 不用它（Measure 无内容需求），
        /// 但测试与宿主标注必须与绘制同源，所以这里是唯一的锚点实现。</summary>
        public static SurfaceElementBoxes ElementBoxesOf(SurfaceStation station, SKPoint center, float stationSize,
            IFontResolver fonts, string? family, SurfaceChartStyle tokens)
        {
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
            if (fonts == null)
            {
                throw new ArgumentNullException(nameof(fonts));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            float gap = tokens.ElementGap;
            float size = tokens.ElementTextSize;
            float height = size * tokens.ElementBoxHeightFactor;
            FontKey font = new FontKey(family, size);

            SKRect temperature = BoxOf(center, gap,
                Primitives.MeasureText(fonts, TemperatureTextOf(station, tokens), font), height, tokens.TemperatureSlot);
            SKRect dewPoint = BoxOf(center, gap,
                Primitives.MeasureText(fonts, DewPointTextOf(station, tokens), font), height, tokens.DewPointSlot);
            SKRect pressure = BoxOf(center, gap,
                Primitives.MeasureText(fonts, PressureTextOf(station, tokens), font), height, tokens.PressureSlot);

            float side = stationSize * (tokens.StationRadiusFactor + 0.55f);
            SKRect windBarb = BoxOf(center, gap, side, side, tokens.WindBarbSlot);

            return new SurfaceElementBoxes(temperature, dewPoint, pressure, windBarb);
        }

        private static SKRect BoxOf(SKPoint center, float gap, float width, float height, ElementAnchorSlot slot)
        {
            switch (slot)
            {
                case ElementAnchorSlot.TopLeft:
                    return new SKRect(center.X - gap - width, center.Y - gap - height,
                                      center.X - gap, center.Y - gap);
                case ElementAnchorSlot.TopRight:
                    return new SKRect(center.X + gap, center.Y - gap - height,
                                      center.X + gap + width, center.Y - gap);
                case ElementAnchorSlot.BottomRight:
                    return new SKRect(center.X + gap, center.Y + gap,
                                      center.X + gap + width, center.Y + gap + height);
                default:
                    return new SKRect(center.X - gap - width, center.Y + gap,
                                      center.X - gap, center.Y + gap + height);
            }
        }

        // ------------------------------------------------------------------ 数值编码

        /// <summary>风速（m/s）→ 羽旗编码用的整数节数。换算系数走令牌，不写死。</summary>
        public static int KnotsOf(SurfaceStation station, SurfaceChartStyle tokens)
        {
            if (station == null)
            {
                throw new ArgumentNullException(nameof(station));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            double knots = station.WindSpeedMps * tokens.KnotsPerMps;
            if (knots <= 0d)
            {
                return 0;
            }
            return (int)Math.Round(knots);
        }

        public static string TemperatureTextOf(SurfaceStation station, SurfaceChartStyle tokens)
        {
            return station.TemperatureC.ToString(tokens.TemperatureFormat, CultureInfo.InvariantCulture);
        }

        public static string DewPointTextOf(SurfaceStation station, SurfaceChartStyle tokens)
        {
            return station.DewPointC.ToString(tokens.DewPointFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>海平面气压 + 三小时变量，画在同一要素框里（右下象限）。</summary>
        public static string PressureTextOf(SurfaceStation station, SurfaceChartStyle tokens)
        {
            string pressure = station.PressureHpa.ToString(tokens.PressureFormat, CultureInfo.InvariantCulture);
            string tendency = station.PressureTendencyHpa.ToString(tokens.TendencyFormat, CultureInfo.InvariantCulture);
            return pressure + " " + tendency;
        }

        private static string NullToEmpty(string? value) => value ?? string.Empty;
    }
}
