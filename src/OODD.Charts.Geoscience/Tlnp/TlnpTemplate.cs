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

namespace OODD.Charts.Geoscience.Tlnp
{
    /// <summary>
    /// T-lnP 温度对数压力图（模板键 <c>geoscience.tlnp</c>），同时承载 T262 的探空层结：
    /// 两条 T/td 曲线、特征层标注与某一层的斜线填图都是本模板的文档字段，不新增模板键。
    /// <para>
    /// 坐标纪律（docs/06 §4.9）：
    /// 纵轴为对数气压，等压线因此严格等距；横轴坐标 x = T + 倾斜量·log10(P₀/p)，
    /// 等温线在 (x, log p) 域里是直线，等压线与等温线的交点可解析求出。
    /// 五族线全部由 <see cref="LineFamilyGenerator"/> 的纯函数采样，绘制走 <see cref="Painter.Polyline"/>。
    /// </para>
    /// <para>
    /// <b>未校准</b>：幅面、倾斜量与线族密度是演示值，未与 QX/T 123-2011 或任何业务底图逐项对齐；
    /// 底部温度读数按 1000 hPa 处的等温线温度标注，这一约定也写在页脚（docs/06 D21/D22）。
    /// </para>
    /// </summary>
    public sealed class TlnpTemplate : IChartTemplate
    {
        public const string FamilyIsobar = "isobar";
        public const string FamilyIsotherm = "isotherm";
        public const string FamilyDryAdiabat = "dry-adiabat";
        public const string FamilyMoistAdiabat = "moist-adiabat";
        public const string FamilyMixingRatio = "mixing-ratio";

        public string Key => "geoscience.tlnp";

        public string DisplayName => "T-lnP 温度对数压力图";

        public TlnpStyle Tokens { get; set; } = new TlnpStyle();

        // ------------------------------------------------------------------ 版面

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            SoundingDocument doc = Cast(document);
            if (fonts == null)
            {
                throw new ArgumentNullException(nameof(fonts));
            }

            TlnpStyle tokens = Tokens;
            GeoscienceStyle paper = tokens.Base;
            SoundingConstants constants = doc.Constants ?? throw new InvalidOperationException("文档缺少热力学常数。");
            if (!(constants.ReferencePressureHpa > 0d))
            {
                throw new InvalidOperationException("参考气压必须为正，实到 " + constants.ReferencePressureHpa);
            }
            if (!(tokens.PressureMaxHpa > tokens.PressureMinHpa) || tokens.PressureMinHpa <= 0d)
            {
                throw new InvalidOperationException("气压轴区间无效：[" + tokens.PressureMinHpa + ","
                                                    + tokens.PressureMaxHpa + "]");
            }

            SKRect bounds = new SKRect(0f, 0f, paper.Width, paper.Height);
            float left = paper.Margin;
            float right = paper.Width - paper.RightPad;
            SKRect titleRect = new SKRect(left, paper.Margin, right, paper.Margin + paper.TitleHeight);
            float footerTop = paper.Height - paper.Margin - paper.FooterHeight;
            SKRect footerRect = new SKRect(left, footerTop, right, footerTop + paper.FooterHeight);

            // 顶部纵向分三条带，自下而上：绘图区 / 顶端刻度（100 的半个字高伸到绘图区上缘外）/
            // 气压轴标题。标题带高度由刻度半高 + 间隙 + 标题字高推出来，刻度带与标题带因此不可能相交。
            float pressureTickHalf = tokens.AxisLabelSize * tokens.AxisLabelHeightFactor * 0.5f;
            float pressureTitleHeight = tokens.AxisTitleSize * tokens.AxisLabelHeightFactor;
            float pressureTitleBand = pressureTickHalf + tokens.AxisTitleGap + pressureTitleHeight;

            SKRect plotRect = new SKRect(
                left + tokens.PlotLeftGutter,
                titleRect.Bottom + tokens.PlotTopInset + pressureTitleBand,
                right - tokens.PlotRightInset,
                footerTop - tokens.XAxisHeight);
            float pressureTitleBottom = plotRect.Top - pressureTickHalf - tokens.AxisTitleGap;
            SKRect pressureAxisTitleRect = new SKRect(
                left, pressureTitleBottom - pressureTitleHeight,
                left + tokens.PlotLeftGutter, pressureTitleBottom);
            if (!(plotRect.Width > 0f) || !(plotRect.Height > 0f))
            {
                throw new InvalidOperationException("绘图区尺寸无效：" + plotRect.Width + "×" + plotRect.Height);
            }
            SKRect xAxisRect = new SKRect(plotRect.Left, plotRect.Bottom, plotRect.Right, footerTop);

            NumericAxis temperatureAxis = new NumericAxis(
                "temperature", tokens.TemperatureMinC, tokens.TemperatureMaxC,
                plotRect.Left, plotRect.Right, AxisDirection.HorizontalRight);

            NumericAxis pressureAxis = new NumericAxis(
                "pressure", tokens.PressureMinHpa, tokens.PressureMaxHpa,
                plotRect.Top, plotRect.Bottom, AxisDirection.VerticalDown, AxisScale.Logarithmic);

            double skew = tokens.SkewDegreesPerDecade;
            double reference = constants.ReferencePressureHpa;
            double pMin = tokens.PressureMinHpa;
            double pMax = tokens.PressureMaxHpa;
            int samples = Math.Max(8, tokens.CurveSamples);

            List<LineFamily> families = new List<LineFamily>
            {
                new LineFamily(FamilyIsobar, "等压线", tokens.Isobars,
                    p => IsobarSamples(tokens.TemperatureMinC, tokens.TemperatureMaxC, p)),
                new LineFamily(FamilyIsotherm, "等温线", tokens.IsothermMembers(),
                    t => IsothermSamples(t, pMin, pMax, skew, reference)),
                new LineFamily(FamilyDryAdiabat, "干绝热线", tokens.DryAdiabatsC,
                    theta => CurveSamples(pMin, pMax, samples,
                        p => TlnpThermodynamics.TemperatureAtPotentialTemperatureC(theta, p, constants),
                        skew, reference)),
                new LineFamily(FamilyMoistAdiabat, "湿绝热线", tokens.MoistAdiabatsC,
                    thetaW => CurveSamples(pMin, pMax, samples,
                        p => TlnpThermodynamics.MoistAdiabatTemperatureC(thetaW, p, constants, 32),
                        skew, reference)),
                new LineFamily(FamilyMixingRatio, "等饱和比湿线", tokens.MixingRatiosGPerKg,
                    w => CurveSamples(pMin, pMax, samples,
                        p => TlnpThermodynamics.TemperatureOfMixingRatio(w, p, constants),
                        skew, reference)),
            };

            LineFamilyGenerator generator = new LineFamilyGenerator(
                temperatureAxis, pressureAxis, families, samples + 8);

            IReadOnlyList<SoundingLevel> ordered = doc.OrderedLevels();
            List<SKPoint> temperatureCurve = new List<SKPoint>(ordered.Count);
            List<SKPoint> dewPointCurve = new List<SKPoint>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                SoundingLevel level = ordered[i];
                temperatureCurve.Add(ExtendedPoint(temperatureAxis, pressureAxis,
                    level.TemperatureC, level.PressureHpa, skew, reference));
                if (doc.ShowDewPoint)
                {
                    dewPointCurve.Add(ExtendedPoint(temperatureAxis, pressureAxis,
                        level.DewPointC, level.PressureHpa, skew, reference));
                }
            }

            List<SKPoint> hatchPolygon = new List<SKPoint>();
            int hatchIndex = doc.HatchedLevelIndex;
            if (doc.ShowDewPoint && hatchIndex >= 0 && hatchIndex + 1 < ordered.Count)
            {
                SoundingLevel lower = ordered[hatchIndex];
                SoundingLevel upper = ordered[hatchIndex + 1];
                hatchPolygon.Add(ExtendedPoint(temperatureAxis, pressureAxis,
                    lower.TemperatureC, lower.PressureHpa, skew, reference));
                hatchPolygon.Add(ExtendedPoint(temperatureAxis, pressureAxis,
                    upper.TemperatureC, upper.PressureHpa, skew, reference));
                hatchPolygon.Add(ExtendedPoint(temperatureAxis, pressureAxis,
                    upper.DewPointC, upper.PressureHpa, skew, reference));
                hatchPolygon.Add(ExtendedPoint(temperatureAxis, pressureAxis,
                    lower.DewPointC, lower.PressureHpa, skew, reference));
            }

            List<TlnpCharacteristicLabel> labels = PlaceCharacteristicLabels(
                doc, ordered, temperatureAxis, pressureAxis, plotRect,
                skew, reference, temperatureCurve, dewPointCurve, tokens, fonts, style);

            TlnpLayout layout = new TlnpLayout(
                bounds, titleRect, footerRect, plotRect, xAxisRect, pressureAxisTitleRect,
                temperatureAxis, pressureAxis, generator, skew, reference,
                temperatureCurve, dewPointCurve, labels, hatchPolygon);

            return ChartGeometry.ForLayout(bounds, plotRect, layout);
        }

        private static SoundingDocument Cast(ChartDocument document)
        {
            SoundingDocument? typed = document as SoundingDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(SoundingDocument) + "。", nameof(document));
            }
            return typed;
        }

        // ------------------------------------------------------------------ 线族采样

        private static IReadOnlyList<double[]> IsobarSamples(double xMin, double xMax, double pressure)
        {
            return new[]
            {
                new[] { xMin, pressure },
                new[] { xMax, pressure },
            };
        }

        private static IReadOnlyList<double[]> IsothermSamples(double temperature, double pMin, double pMax,
                                                               double skew, double reference)
        {
            return new[]
            {
                new[] { Coordinate(temperature, pMin, skew, reference), pMin },
                new[] { Coordinate(temperature, pMax, skew, reference), pMax },
            };
        }

        private static IReadOnlyList<double[]> CurveSamples(double pMin, double pMax, int count,
            Func<double, double> temperatureAtPressure, double skew, double reference)
        {
            double[] pressures = LogSamples(pMin, pMax, count);
            double[][] samples = new double[pressures.Length][];
            for (int i = 0; i < pressures.Length; i++)
            {
                double p = pressures[i];
                double t = temperatureAtPressure(p);
                samples[i] = new[] { Coordinate(t, p, skew, reference), p };
            }
            return samples;
        }

        /// <summary>对数等分采样：p 从 pMin 到 pMax 按 ln p 等距，共 count 个点。</summary>
        public static double[] LogSamples(double pMin, double pMax, int count)
        {
            if (!(pMin > 0d) || !(pMax > 0d) || !(pMax > pMin))
            {
                throw new ArgumentException("对数采样的气压区间无效：[" + pMin + "," + pMax + "]");
            }
            if (count < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "采样点数至少为 2，实到 " + count);
            }

            double logMin = Math.Log(pMin);
            double logMax = Math.Log(pMax);
            double[] values = new double[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = Math.Exp(logMin + (logMax - logMin) * i / (count - 1));
            }
            return values;
        }

        /// <summary>x = T + 倾斜量·log10(P₀/p)，T-lnP 的横坐标。</summary>
        public static double Coordinate(double temperatureC, double pressureHpa, double skew, double reference)
        {
            if (!(pressureHpa > 0d))
            {
                throw new ArgumentOutOfRangeException(nameof(pressureHpa), "气压必须为正，实到 " + pressureHpa);
            }
            return temperatureC + skew * Math.Log10(reference / pressureHpa);
        }

        private static SKPoint ExtendedPoint(NumericAxis temperatureAxis, NumericAxis pressureAxis,
                                             double temperatureC, double pressureHpa, double skew, double reference)
        {
            return new SKPoint(
                temperatureAxis.PositionOfExtended(Coordinate(temperatureC, pressureHpa, skew, reference)),
                pressureAxis.PositionOfExtended(pressureHpa));
        }

        private static SKPoint Point(NumericAxis temperatureAxis, NumericAxis pressureAxis,
                                     double temperatureC, double pressureHpa, double skew, double reference)
        {
            return new SKPoint(
                temperatureAxis.PositionOf(Coordinate(temperatureC, pressureHpa, skew, reference)),
                pressureAxis.PositionOf(pressureHpa));
        }

        // ------------------------------------------------------------------ 特征层避让

        private static List<TlnpCharacteristicLabel> PlaceCharacteristicLabels(
            SoundingDocument doc, IReadOnlyList<SoundingLevel> ordered,
            NumericAxis temperatureAxis, NumericAxis pressureAxis, SKRect plateRect,
            double skew, double reference,
            List<SKPoint> temperatureCurve, List<SKPoint> dewPointCurve,
            TlnpStyle tokens, IFontResolver fonts, ChartStyle style)
        {
            List<TlnpCharacteristicLabel> labels = new List<TlnpCharacteristicLabel>();
            if (!doc.ShowCharacteristicLabels || ordered.Count == 0)
            {
                return labels;
            }

            FontKey font = new FontKey(style.Text.Family, tokens.CharacteristicLabelSize);
            float boxHeight = tokens.CharacteristicLabelSize * tokens.CharacteristicBoxHeightFactor
                            + 2f * tokens.CharacteristicPadding;
            float step = boxHeight * tokens.CharacteristicNudgeFactor;

            foreach (SoundingCharacteristicLevel characteristic in doc.CharacteristicLevels)
            {
                if (characteristic == null || string.IsNullOrEmpty(characteristic.Label))
                {
                    continue;
                }

                double p = characteristic.PressureHpa;
                if (p < tokens.PressureMinHpa || p > tokens.PressureMaxHpa)
                {
                    continue;
                }

                double t = TlnpThermodynamics.TemperatureAtPressure(ordered, p);
                SKPoint anchor = Point(temperatureAxis, pressureAxis, t, p, skew, reference);
                float width = Primitives.MeasureText(fonts, characteristic.Label, font)
                            + 2f * tokens.CharacteristicPadding;

                SKRect? placed = null;
                int[] sides = characteristic.PreferLeft ? new[] { -1, 1 } : new[] { 1, -1 };
                for (int s = 0; s < sides.Length && placed == null; s++)
                {
                    for (int n = 0; n <= tokens.CharacteristicNudgeSteps && placed == null; n++)
                    {
                        for (int dir = 0; dir < (n == 0 ? 1 : 2) && placed == null; dir++)
                        {
                            float offset = n == 0 ? 0f : (dir == 0 ? 1f : -1f) * step * n;
                            SKRect box = BoxFor(anchor, width, boxHeight, sides[s], offset, tokens.CharacteristicGap);
                            if (IsInside(plateRect, box, 1f)
                                && !HitsPolyline(box, temperatureCurve)
                                && !HitsPolyline(box, dewPointCurve)
                                && !HitsAny(box, labels))
                            {
                                placed = box;
                            }
                        }
                    }
                }

                if (placed == null)
                {
                    // 兜底：放在绘图区左缘的标注带里并竖直夹紧。极端数据下可能压到曲线，
                    // 但不会越出纸面；正常探空数据在前面的避让里已经落位。
                    float x = plateRect.Left + tokens.CharacteristicGap;
                    float y = anchor.Y - boxHeight / 2f;
                    if (y < plateRect.Top)
                    {
                        y = plateRect.Top;
                    }
                    if (y + boxHeight > plateRect.Bottom)
                    {
                        y = plateRect.Bottom - boxHeight;
                    }
                    placed = new SKRect(x, y, x + width, y + boxHeight);
                }

                labels.Add(new TlnpCharacteristicLabel(characteristic, placed.Value, anchor));
            }

            return labels;
        }

        private static SKRect BoxFor(SKPoint anchor, float width, float height, int side, float verticalOffset, float gap)
        {
            float top = anchor.Y - height / 2f + verticalOffset;
            if (side < 0)
            {
                float right = anchor.X - gap;
                return new SKRect(right - width, top, right, top + height);
            }

            float left = anchor.X + gap;
            return new SKRect(left, top, left + width, top + height);
        }

        private static bool IsInside(SKRect outer, SKRect inner, float margin)
        {
            return inner.Left >= outer.Left + margin && inner.Right <= outer.Right - margin
                && inner.Top >= outer.Top + margin && inner.Bottom <= outer.Bottom - margin;
        }

        private static bool HitsAny(SKRect box, List<TlnpCharacteristicLabel> labels)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                if (box.IntersectsWith(labels[i].Box))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HitsPolyline(SKRect box, List<SKPoint> points)
        {
            for (int i = 0; i + 1 < points.Count; i++)
            {
                if (SegmentHitsRect(points[i], points[i + 1], box))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>线段与矩形是否有交（Liang–Barsky 的参数裁剪，贴边算相交）。</summary>
        public static bool SegmentHitsRect(SKPoint a, SKPoint b, SKRect rect)
        {
            double t0 = 0d;
            double t1 = 1d;
            double dx = (double)b.X - a.X;
            double dy = (double)b.Y - a.Y;

            if (!Slab(-dx, a.X - rect.Left, ref t0, ref t1)) { return false; }
            if (!Slab(dx, rect.Right - a.X, ref t0, ref t1)) { return false; }
            if (!Slab(-dy, a.Y - rect.Top, ref t0, ref t1)) { return false; }
            if (!Slab(dy, rect.Bottom - a.Y, ref t0, ref t1)) { return false; }
            return t0 <= t1 + 1e-9;
        }

        private static bool Slab(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) < 1e-12)
            {
                return q >= -1e-9;
            }

            double r = q / p;
            if (p < 0d)
            {
                if (r > t1 + 1e-9) { return false; }
                if (r > t0) { t0 = r; }
            }
            else
            {
                if (r < t0 - 1e-9) { return false; }
                if (r < t1) { t1 = r; }
            }
            return true;
        }

        // ------------------------------------------------------------------ 绘制

        public void DrawContent(Painter p, ChartGeometry geometry, ChartDocument document)
        {
            SoundingDocument doc = Cast(document);
            TlnpLayout? layout = geometry.LayoutOf<TlnpLayout>();
            if (layout == null)
            {
                throw new InvalidOperationException("版面对象缺失：本模板的落点全部来自 TlnpLayout。");
            }

            TlnpStyle tokens = Tokens;
            GeosciencePalette palette = tokens.Palette;
            GeoscienceStyle paper = tokens.Base;
            string? family = p.Style.Text.Family;

            p.FillRect(layout.Bounds, palette.PageBackground);

            p.TextInBox(NullToEmpty(doc.FormTitle), layout.TitleRect,
                new FontKey(family, paper.TitleSize, true),
                palette.TitleText, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.StationName))
            {
                p.TextInBox(doc.StationName!, layout.TitleRect,
                    new FontKey(family, paper.SubtitleSize),
                    palette.SubtitleText, CellAlign.Near, CellAlign.Center, tokens.TitleSidePadding);
            }

            if (!string.IsNullOrEmpty(doc.ValidTimeText))
            {
                p.TextInBox(doc.ValidTimeText!, layout.TitleRect,
                    new FontKey(family, paper.SubtitleSize),
                    palette.SubtitleText, CellAlign.Far, CellAlign.Center, tokens.TitleSidePadding);
            }

            string footer = string.IsNullOrEmpty(doc.FooterNote) ? tokens.FooterNote : doc.FooterNote!;
            p.TextInBox(footer, layout.FooterRect,
                new FontKey(family, paper.FooterSize),
                palette.MutedText, CellAlign.Near, CellAlign.Center, 0f);

            p.Frame(layout.PlotRect, palette.Ink, paper.BorderStroke);

            p.PushClipRect(layout.PlotRect);
            try
            {
                layout.Families.DrawFamily(p, FamilyMixingRatio, tokens.MixingRatioColor,
                    tokens.MixingRatioStroke, true);
                layout.Families.DrawFamily(p, FamilyMoistAdiabat, tokens.MoistAdiabatColor,
                    tokens.MoistAdiabatStroke, true);
                layout.Families.DrawFamily(p, FamilyDryAdiabat, tokens.DryAdiabatColor,
                    tokens.DryAdiabatStroke, false);
                layout.Families.DrawFamily(p, FamilyIsotherm, tokens.IsothermColor,
                    tokens.IsothermStroke, false);
                layout.Families.DrawFamily(p, FamilyIsobar, tokens.IsobarColor,
                    tokens.IsobarStroke, false);

                DrawHatch(p, layout, tokens);

                if (layout.TemperatureCurve.Count >= 2)
                {
                    p.Polyline(layout.TemperatureCurve, palette.Accent, tokens.TemperatureCurveStroke, false);
                }
                if (layout.DewPointCurve.Count >= 2)
                {
                    p.Polyline(layout.DewPointCurve, palette.Secondary, tokens.DewPointCurveStroke, false);
                }

                if (tokens.LevelMarkerRadius > 0f)
                {
                    for (int i = 0; i < layout.TemperatureCurve.Count; i++)
                    {
                        p.FillCircle(layout.TemperatureCurve[i], tokens.LevelMarkerRadius, palette.Accent);
                    }
                    for (int i = 0; i < layout.DewPointCurve.Count; i++)
                    {
                        p.FillCircle(layout.DewPointCurve[i], tokens.LevelMarkerRadius, palette.Secondary);
                    }
                }
            }
            finally
            {
                p.Pop();
            }

            DrawPressureAxis(p, layout, tokens, paper, family);
            DrawTemperatureAxis(p, layout, tokens, paper, family);
            DrawCharacteristicLabels(p, layout, tokens, palette, family);

            p.Rect(layout.Bounds, palette.Ink, paper.BorderStroke);
        }

        private static void DrawHatch(Painter p, TlnpLayout layout, TlnpStyle tokens)
        {
            if (layout.HatchPolygon.Count < 4)
            {
                return;
            }

            using (SKPath path = p.BuildPath(layout.HatchPolygon, true))
            {
                PatternFill pattern = PatternFill.Of(PatternKind.Diagonal,
                    tokens.HatchSpacingMm, tokens.HatchStrokeMm, tokens.Palette.Band);
                p.FillPathWithPattern(path, pattern);
            }
        }

        private static void DrawPressureAxis(Painter p, TlnpLayout layout, TlnpStyle tokens,
                                             GeoscienceStyle paper, string? family)
        {
            FontKey font = new FontKey(family, tokens.AxisLabelSize);
            float height = tokens.AxisLabelSize * tokens.AxisLabelHeightFactor;
            float tick = tokens.GridTickLength;
            float pad = tokens.AxisLabelPadding;

            foreach (double pressure in tokens.Isobars)
            {
                float y = layout.PressureAxis.PositionOf(pressure);
                p.Line(layout.PlotRect.Left - tick, y, layout.PlotRect.Left, y, tokens.Palette.Ink, paper.GridStroke);

                string text = pressure.ToString(tokens.PressureFormat, CultureInfo.InvariantCulture);
                SKRect box = new SKRect(
                    layout.PlotRect.Left - tokens.PlotLeftGutter + pad,
                    y - height / 2f,
                    layout.PlotRect.Left - tick - pad,
                    y + height / 2f);
                p.TextInBox(text, box, font, tokens.Palette.Text, CellAlign.Far, CellAlign.Center, 0f);
            }

            p.TextInBox("气压/hPa", layout.PressureAxisTitleRect,
                new FontKey(family, tokens.AxisTitleSize),
                tokens.Palette.SubtitleText, CellAlign.Near, CellAlign.Far, 0f);
        }

        private static void DrawTemperatureAxis(Painter p, TlnpLayout layout, TlnpStyle tokens,
                                                GeoscienceStyle paper, string? family)
        {
            FontKey font = new FontKey(family, tokens.AxisLabelSize);
            float height = tokens.AxisLabelSize * tokens.AxisLabelHeightFactor;
            float tick = tokens.GridTickLength;
            double pMax = tokens.PressureMaxHpa;

            for (double t = tokens.TemperatureMinC; t <= tokens.TemperatureMaxC + 1e-9; t += tokens.IsothermStepC)
            {
                float x = layout.XOfCoordinate(TlnpTemplate.Coordinate(t, pMax,
                    layout.SkewDegreesPerDecade, layout.ReferencePressureHpa));
                p.Line(x, layout.PlotRect.Bottom, x, layout.PlotRect.Bottom + tick,
                    tokens.Palette.Ink, paper.GridStroke);

                SKRect box = new SKRect(
                    x - tokens.TemperatureLabelHalfWidth,
                    layout.PlotRect.Bottom + tick + tokens.AxisTitleGap,
                    x + tokens.TemperatureLabelHalfWidth,
                    layout.PlotRect.Bottom + tick + tokens.AxisTitleGap + height);
                p.TextInBox(t.ToString(tokens.TemperatureFormat, CultureInfo.InvariantCulture), box,
                    font, tokens.Palette.Text, CellAlign.Center, CellAlign.Center, 0f);
            }

            p.TextInBox("温度/℃（1000 hPa 读数）",
                new SKRect(layout.XAxisRect.Right - tokens.TemperatureAxisTitleWidth,
                           layout.XAxisRect.Top + tokens.AxisLabelSize * tokens.AxisLabelHeightFactor,
                           layout.XAxisRect.Right, layout.XAxisRect.Bottom),
                new FontKey(family, tokens.AxisTitleSize),
                tokens.Palette.SubtitleText, CellAlign.Far, CellAlign.Center, 0f);
        }

        private static void DrawCharacteristicLabels(Painter p, TlnpLayout layout, TlnpStyle tokens,
                                                     GeosciencePalette palette, string? family)
        {
            FontKey font = new FontKey(family, tokens.CharacteristicLabelSize);

            foreach (TlnpCharacteristicLabel label in layout.CharacteristicLabels)
            {
                p.FillRect(label.Box, palette.PanelFill);
                p.Frame(label.Box, palette.PanelBorder, tokens.CharacteristicBoxStroke);
                p.TextInBox(label.Text, label.Box, font, palette.Text,
                    CellAlign.Center, CellAlign.Center, 0f);

                bool left = label.Box.Right <= label.Anchor.X;
                SKPoint from = left
                    ? new SKPoint(label.Box.Right, label.Box.MidY)
                    : new SKPoint(label.Box.Left, label.Box.MidY);
                p.Line(from.X, from.Y, label.Anchor.X, label.Anchor.Y,
                    palette.MutedText, tokens.CharacteristicLeaderStroke);
                p.FillCircle(label.Anchor, tokens.CharacteristicAnchorRadius, palette.Accent);
            }
        }

        private static string NullToEmpty(string? value) => value ?? string.Empty;
    }
}
