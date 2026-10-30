using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using OODD.Charts.Geoscience.Surface;
using OODD.Charts.Geoscience.Tlnp;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 波次 6 / 腿 C：地学域 T260–T262 的验收测试。
    /// <para>
    /// 每个验收条目至少一条 Fact，判据全部落在"版面对象里的数值 / PNG 像素 / SVG 文本节点"上，
    /// 不靠目视。改令牌必须改落点的 A18 断言每张表单各一条。
    /// </para>
    /// <para>
    /// 本文件是腿 C 唯一新增的测试文件；运行方式见工单的隔离工程说明，
    /// 不依赖（也不触碰）其它腿的测试文件。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class GeoscienceFormsTests
    {
        // ================================================================== 公共工具

        private static byte[] Render(ChartDocument document, IChartTemplate template, out ChartGeometry geometry)
        {
            using (Chart chart = new Chart(document, template, new ChartStyle()))
            {
                geometry = chart.Geometry;
                return chart.Encode(ChartImageFormat.Png, 96f, 100);
            }
        }

        private static byte[] Render(ChartDocument document, IChartTemplate template)
        {
            ChartGeometry ignored;
            return Render(document, template, out ignored);
        }

        private static T RequireLayout<T>(ChartGeometry geometry) where T : class
        {
            T? layout = geometry.LayoutOf<T>();
            if (layout == null)
            {
                throw new InvalidOperationException(
                    "版面对象缺失：期望 " + typeof(T).Name + "，实到 "
                    + (geometry.Layout == null ? "null" : geometry.Layout.GetType().Name));
            }
            return layout;
        }

        private static string F(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static string Describe(SKRect rect)
        {
            return "(" + F(rect.Left) + "," + F(rect.Top) + ")-(" + F(rect.Right) + "," + F(rect.Bottom) + ")";
        }

        private static void AssertInk(ImageProbe probe, SKRect rect, string what)
        {
            Assert.True(rect.Width > 0f && rect.Height > 0f,
                what + " 的矩形为空：" + Describe(rect));
            bool ink = probe.HasInkInBox((int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
                (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom));
            Assert.True(ink, what + " 的矩形内没有墨：" + Describe(rect)
                             + "，图幅 " + probe.Width + "×" + probe.Height);
        }

        private static double Distance(SKPoint a, SKPoint b)
        {
            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static bool BoxesOverlap(SKRect a, SKRect b)
        {
            return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
        }

        private static List<string> SvgTexts(string svg)
        {
            List<string> texts = new List<string>();
            MatchCollection matches = new Regex("<text[^>]*>([^<]*)</text>").Matches(svg);
            for (int i = 0; i < matches.Count; i++)
            {
                string text = matches[i].Groups[1].Value.Trim();
                if (text.Length > 0)
                {
                    texts.Add(text);
                }
            }
            return texts;
        }

        private static bool ContainsText(List<string> texts, string expected)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                if (texts[i].IndexOf(expected, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static string JoinHead(List<string> texts, int count)
        {
            int take = Math.Min(count, texts.Count);
            string joined = string.Empty;
            for (int i = 0; i < take; i++)
            {
                joined += "[" + texts[i] + "]";
            }
            return joined.Length == 0 ? "（无文本节点）" : joined;
        }

        // ================================================================== 模板键

        [Fact]
        public void 两张表单的模板键与T262并入Tlnp的口径一致()
        {
            IChartTemplate[] templates =
            {
                new SurfaceChartTemplate(),
                new TlnpTemplate(),
            };
            string[] expected =
            {
                "geoscience.surface-chart",
                "geoscience.tlnp",
            };

            for (int i = 0; i < templates.Length; i++)
            {
                Assert.True(string.Equals(templates[i].Key, expected[i], StringComparison.Ordinal),
                    "第 " + (i + 1) + " 个模板键不符：期望 " + expected[i] + "，实到 " + templates[i].Key);
            }

            // T262 不新增键：特征层 / T/td 双曲线 / 层次填图都是 geoscience.tlnp 的文档字段。
            SoundingDocument sounding = new SoundingDocument();
            sounding.HatchedLevelIndex = 0;
            sounding.ShowDewPoint = false;
            sounding.ShowCharacteristicLabels = false;
            Assert.True(string.Equals(new TlnpTemplate().Key, "geoscience.tlnp", StringComparison.Ordinal)
                        && sounding.HatchedLevelIndex == 0 && !sounding.ShowDewPoint && !sounding.ShowCharacteristicLabels,
                "T262 应作为 geoscience.tlnp 的字段存在：键=" + new TlnpTemplate().Key
                + "，HatchedLevelIndex=" + sounding.HatchedLevelIndex
                + "，ShowDewPoint=" + sounding.ShowDewPoint
                + "，ShowCharacteristicLabels=" + sounding.ShowCharacteristicLabels);
        }

        // ================================================================== T260 地面天气图

        private static SurfacePlotDocument BuildSurfaceDocument()
        {
            SurfacePlotDocument document = new SurfacePlotDocument();
            document.ValidTimeText = "2026-03-01 08 时（示例）";
            SurfaceLevelPlot level = document.Levels[0];

            level.Stations.Add(new SurfaceStation("北京 54511", 0.28d, 0.30d)
            {
                TemperatureC = -3.5d,
                DewPointC = -8.2d,
                PressureHpa = 1012.4d,
                PressureTendencyHpa = 1.2d,
                WindDirectionDegrees = 0d,
                WindSpeedMps = 10d,
                CloudOktas = 6,
                WeatherCode = "SN",
            });
            level.Stations.Add(new SurfaceStation("上海 58362", 0.72d, 0.66d)
            {
                TemperatureC = 9.8d,
                DewPointC = 6.1d,
                PressureHpa = 1008.7d,
                PressureTendencyHpa = -0.6d,
                WindDirectionDegrees = 90d,
                WindSpeedMps = 25d,
                CloudOktas = 3,
            });
            return document;
        }

        [Fact]
        public void 地面天气图风向零度朝上九十度朝右且顺时针()
        {
            SKPoint north = WindBarb.DirectionVectorOf(0f);
            SKPoint east = WindBarb.DirectionVectorOf(90f);
            SKPoint south = WindBarb.DirectionVectorOf(180f);
            SKPoint west = WindBarb.DirectionVectorOf(270f);

            Assert.True(Distance(north, new SKPoint(0f, -1f)) < 1e-6,
                "0° 应指向屏幕上方 (0,-1)，实到 (" + F(north.X) + "," + F(north.Y) + ")");
            Assert.True(Distance(east, new SKPoint(1f, 0f)) < 1e-6,
                "90° 应指向屏幕右方 (1,0)，实到 (" + F(east.X) + "," + F(east.Y) + ")");
            Assert.True(Distance(south, new SKPoint(0f, 1f)) < 1e-6,
                "180° 应指向屏幕下方 (0,1)，实到 (" + F(south.X) + "," + F(south.Y) + ")");
            Assert.True(Distance(west, new SKPoint(-1f, 0f)) < 1e-6,
                "270° 应指向屏幕左方 (-1,0)，实到 (" + F(west.X) + "," + F(west.Y) + ")");

            // 正向对照：0° 与 90° 的杆端确实落在圆的上下方/右方，而不是只验静态函数。
            SurfacePlotDocument document = BuildSurfaceDocument();
            SurfaceChartTemplate template = new SurfaceChartTemplate();
            ChartGeometry geometry;
            byte[] png = Render(document, template, out geometry);
            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                SurfaceChartLayout layout = RequireLayout<SurfaceChartLayout>(geometry);
                SurfacePanel panel = layout.Panels[0];
                SurfaceStation northStation = document.Levels[0].Stations[0];
                SurfaceStation eastStation = document.Levels[0].Stations[1];
                SKPoint northCenter = layout.CenterOf(panel, northStation);
                SKPoint eastCenter = layout.CenterOf(panel, eastStation);

                WindBarb northBarb = new WindBarb((float)northStation.WindDirectionDegrees,
                    SurfaceChartTemplate.KnotsOf(northStation, template.Tokens),
                    WindBarb.BarbRule.WmoStandard)
                {
                    StationRadiusFactor = template.Tokens.StationRadiusFactor,
                    BarbsOnLeft = template.Tokens.BarbsOnLeft,
                };
                WindBarb eastBarb = new WindBarb((float)eastStation.WindDirectionDegrees,
                    SurfaceChartTemplate.KnotsOf(eastStation, template.Tokens),
                    WindBarb.BarbRule.WmoStandard)
                {
                    StationRadiusFactor = template.Tokens.StationRadiusFactor,
                    BarbsOnLeft = template.Tokens.BarbsOnLeft,
                };

                SKPoint northTip = northBarb.TipOf(northCenter, template.Tokens.StationSize);
                SKPoint eastTip = eastBarb.TipOf(eastCenter, template.Tokens.StationSize);
                Assert.True(northTip.Y < northCenter.Y - template.Tokens.StationSize,
                    "0° 风杆末端应在站圈上方：中心 y=" + F(northCenter.Y) + "，末端 y=" + F(northTip.Y));
                Assert.True(eastTip.X > eastCenter.X + template.Tokens.StationSize,
                    "90° 风杆末端应在站圈右方：中心 x=" + F(eastCenter.X) + "，末端 x=" + F(eastTip.X));

                AssertInk(probe, new SKRect(northTip.X - 12f, northTip.Y - 6f, northTip.X + 12f, northTip.Y + 12f),
                    "0° 风杆末端");
                AssertInk(probe, new SKRect(eastTip.X - 6f, eastTip.Y - 12f, eastTip.X + 12f, eastTip.Y + 12f),
                    "90° 风杆末端");
            }
        }

        [Fact]
        public void 地面天气图半旗数量等于风速编码()
        {
            SurfaceChartTemplate template = new SurfaceChartTemplate();
            double[] speeds = { 2d, 5d, 10d, 25d };

            for (int i = 0; i < speeds.Length; i++)
            {
                SurfaceStation station = new SurfaceStation("站" + i, 0.5d, 0.5d) { WindSpeedMps = speeds[i] };
                int knots = SurfaceChartTemplate.KnotsOf(station, template.Tokens);
                WindBarb barb = new WindBarb(0f, knots, WindBarb.BarbRule.WmoStandard);
                WindBarb.BarbCounts counts = barb.Counts;

                int expectedHalves = (knots % 10) / 5;
                int expectedFulls = knots % 50 / 10;
                int expectedPennants = knots / 50;

                Assert.True(counts.Halves == expectedHalves,
                    "风速 " + F(speeds[i]) + " m/s → " + knots + " kn 的半旗数应为 " + expectedHalves
                    + "，实到 " + counts.Halves + "（编码 " + counts + "）");
                Assert.True(counts.Fulls == expectedFulls,
                    "风速 " + F(speeds[i]) + " m/s → " + knots + " kn 的全旗数应为 " + expectedFulls
                    + "，实到 " + counts.Fulls + "（编码 " + counts + "）");
                Assert.True(counts.Pennants == expectedPennants,
                    "风速 " + F(speeds[i]) + " m/s → " + knots + " kn 的三角旗数应为 " + expectedPennants
                    + "，实到 " + counts.Pennants + "（编码 " + counts + "）");
            }

            // 半旗是"向下取整到 5 节"：10 m/s→19 kn 留 1 面半旗，9 m/s→17 kn 也只留 1 面。
            SurfaceStation lower = new SurfaceStation("站A", 0.5d, 0.5d) { WindSpeedMps = 9d };
            int lowerKnots = SurfaceChartTemplate.KnotsOf(lower, template.Tokens);
            WindBarb lowerBarb = new WindBarb(0f, lowerKnots, WindBarb.BarbRule.WmoStandard);
            Assert.True(lowerBarb.Counts.Halves == 1,
                "9 m/s → " + lowerKnots + " kn 应编码 1 面半旗（45 节量级），实到 " + lowerBarb.Counts);
        }

        [Fact]
        public void 地面天气图四个要素锚点几何不相交()
        {
            SurfacePlotDocument document = BuildSurfaceDocument();
            SurfaceChartTemplate template = new SurfaceChartTemplate();
            ChartGeometry geometry;
            Render(document, template, out geometry);
            SurfaceChartLayout layout = RequireLayout<SurfaceChartLayout>(geometry);
            SurfacePanel panel = layout.Panels[0];

            foreach (SurfaceStation station in document.Levels[0].Stations)
            {
                SKPoint center = layout.CenterOf(panel, station);
                SurfaceElementBoxes boxes = SurfaceChartTemplate.ElementBoxesOf(
                    station, center, template.Tokens.StationSize, Chart.SharedFonts, null, template.Tokens);

                SKRect[] all = { boxes.Temperature, boxes.DewPoint, boxes.Pressure, boxes.WindBarb };
                string[] names = { "气温", "露点", "海压", "风杆槽" };
                for (int i = 0; i < all.Length; i++)
                {
                    Assert.True(all[i].Width > 0f && all[i].Height > 0f,
                        station.Name + " 的 " + names[i] + " 框为空：" + Describe(all[i]));
                    for (int j = i + 1; j < all.Length; j++)
                    {
                        Assert.True(!BoxesOverlap(all[i], all[j]),
                            station.Name + " 的 " + names[i] + " " + Describe(all[i]) + " 与 "
                            + names[j] + " " + Describe(all[j]) + " 相交");
                    }
                }
            }
        }

        [Fact]
        public void 地面天气图SVG文字可检索且没有空文本节点()
        {
            SurfacePlotDocument document = BuildSurfaceDocument();
            SurfaceChartTemplate template = new SurfaceChartTemplate();
            string svg;
            using (Chart chart = new Chart(document, template, new ChartStyle()))
            {
                svg = ChartExport.ToSvgString(chart);
            }

            List<string> texts = SvgTexts(svg);
            Assert.True(texts.Count > 0, "SVG 里一个非空 <text> 节点都没有，长度 " + svg.Length);
            Assert.True(ContainsText(texts, "地面天气图"),
                "SVG 文本节点里找不到标题“地面天气图”，实到前几个 " + JoinHead(texts, 8));
            Assert.True(ContainsText(texts, "北京 54511"),
                "SVG 文本节点里找不到站名“北京 54511”，实到前几个 " + JoinHead(texts, 12));
            Assert.True(ContainsText(texts, "2026-03-01"),
                "SVG 文本节点里找不到时次“2026-03-01”，实到前几个 " + JoinHead(texts, 12));

            bool emptyNode = Regex.IsMatch(svg, "<text[^>]*>\\s*</text>");
            Assert.True(!emptyNode, "SVG 里出现内容为空的 <text> 节点（文字被路径化或丢字），非空节点数=" + texts.Count);
        }

        [Fact]
        public void 地面天气图A18改要素间距后气温落点与像素一起移动()
        {
            // 专用文档：站点不写站名、无云量/天气码，避免站名文字的降部落进对照带。
            SurfacePlotDocument document = new SurfacePlotDocument();
            SurfaceStation station = new SurfaceStation(string.Empty, 0.5d, 0.5d)
            {
                TemperatureC = -3.5d,
                DewPointC = -8.2d,
                PressureHpa = 1012.4d,
            };
            document.Levels[0].Stations.Add(station);

            SurfaceChartStyle narrowTokens = new SurfaceChartStyle();
            SurfaceChartTemplate narrowTemplate = new SurfaceChartTemplate { Tokens = narrowTokens };
            ChartGeometry narrowGeometry;
            byte[] narrowPng = Render(document, narrowTemplate, out narrowGeometry);
            SurfaceChartLayout narrowLayout = RequireLayout<SurfaceChartLayout>(narrowGeometry);
            SKPoint center = narrowLayout.CenterOf(narrowLayout.Panels[0], station);
            SurfaceElementBoxes narrowBoxes = SurfaceChartTemplate.ElementBoxesOf(
                station, center, narrowTokens.StationSize, Chart.SharedFonts, null, narrowTokens);

            SurfaceChartStyle wideTokens = new SurfaceChartStyle { ElementGap = 24f };
            SurfaceChartTemplate wideTemplate = new SurfaceChartTemplate { Tokens = wideTokens };
            ChartGeometry wideGeometry;
            byte[] widePng = Render(document, wideTemplate, out wideGeometry);
            SurfaceChartLayout wideLayout = RequireLayout<SurfaceChartLayout>(wideGeometry);
            SKPoint wideCenter = wideLayout.CenterOf(wideLayout.Panels[0], station);
            SurfaceElementBoxes wideBoxes = SurfaceChartTemplate.ElementBoxesOf(
                station, wideCenter, wideTokens.StationSize, Chart.SharedFonts, null, wideTokens);

            double shift = (double)narrowBoxes.Temperature.Right - wideBoxes.Temperature.Right;
            double expected = wideTokens.ElementGap - narrowTokens.ElementGap;
            Assert.True(Math.Abs(shift - expected) < 1e-3,
                "ElementGap 从 " + F(narrowTokens.ElementGap) + " 改到 " + F(wideTokens.ElementGap)
                + "，气温框右缘应左移 " + F(expected) + " px，实到 " + F(shift) + " px");

            using (ImageProbe narrowProbe = ImageProbe.FromPng(narrowPng))
            {
                AssertInk(narrowProbe, narrowBoxes.Temperature, "默认间距下的气温文字");
            }

            // 默认间距独有的一条竖带：宽间距下这里应该没有气温文字（外扩 2px 避开抗锯齿边缘，
            // 右端收在站圈外缘，避免把站圈描边算成文字）。
            float vacatedRight = Math.Min(narrowBoxes.Temperature.Right,
                center.X - narrowTokens.StationSize * narrowTokens.StationRadiusFactor - 2f);
            SKRect vacated = new SKRect(wideBoxes.Temperature.Right + 2f, wideBoxes.Temperature.Top,
                vacatedRight, wideBoxes.Temperature.Bottom);
            using (ImageProbe wideProbe = ImageProbe.FromPng(widePng))
            {
                AssertInk(wideProbe, wideBoxes.Temperature, "加宽间距后的气温文字");
                Assert.True(!wideProbe.HasInkInBox((int)Math.Floor(vacated.Left), (int)Math.Floor(vacated.Top),
                        (int)Math.Ceiling(vacated.Right), (int)Math.Ceiling(vacated.Bottom)),
                    "ElementGap 加大后，气温文字应离开旧位置，但带 " + Describe(vacated) + " 里仍有墨");
            }
        }

        [Fact]
        public void 地面天气图站名让开向上风杆()
        {
            SurfacePlotDocument document = new SurfacePlotDocument();
            document.StackPressureLevels = false;
            SurfaceStation north = new SurfaceStation("北京 54511", 0.5d, 0.5d)
            {
                TemperatureC = -3.5d,
                DewPointC = -8.2d,
                PressureHpa = 1012.4d,
                PressureTendencyHpa = 1.2d,
                WindDirectionDegrees = 0d,
                WindSpeedMps = 25d,
                CloudOktas = 0,
            };
            document.Levels[0].Stations.Add(north);

            SurfaceChartStyle tokens = new SurfaceChartStyle();
            SurfaceChartTemplate template = new SurfaceChartTemplate { Tokens = tokens };
            ChartGeometry geometry;
            byte[] png = Render(document, template, out geometry);
            SurfaceChartLayout layout = RequireLayout<SurfaceChartLayout>(geometry);
            SKPoint center = layout.CenterOf(layout.Panels[0], north);

            SKRect nameBox = SurfaceChartTemplate.StationNameBoxOf(north, center, tokens.StationSize, tokens);
            WindBarb barb = SurfaceChartTemplate.BarbOf(north, tokens);
            SKPoint tip = barb.TipOf(center, tokens.StationSize);
            float flagReach = tokens.StationSize * barb.FullBarbLengthFactor;

            Assert.True(nameBox.Bottom <= tip.Y - flagReach + 0.01f,
                "朝上风杆的羽旗伸到 y=" + F(tip.Y - flagReach) + "，站名框下缘 " + F(nameBox.Bottom)
                + " 应在它之上（默认站名位 y=" + F(center.Y - tokens.StationSize * tokens.StationNameOffsetFactor)
                + " 会被杆压掉）");

            SKRect barbInk = new SKRect(center.X - flagReach, tip.Y - 1f,
                center.X + flagReach, center.Y - tokens.StationSize * tokens.StationRadiusFactor);
            Assert.True(!BoxesOverlap(nameBox, barbInk),
                "站名框 " + Describe(nameBox) + " 与风杆墨迹带 " + Describe(barbInk) + " 相交");

            // 正对照：风杆不朝上时站名留在默认位置，修正不能把所有站名都顶高。
            SurfaceStation calm = new SurfaceStation("静风站", 0.5d, 0.5d) { WindSpeedMps = 0d };
            SKRect calmBox = SurfaceChartTemplate.StationNameBoxOf(calm, center, tokens.StationSize, tokens);
            float defaultMidY = center.Y - tokens.StationSize * tokens.StationNameOffsetFactor;
            Assert.True(Math.Abs(calmBox.MidY - defaultMidY) < 0.01f,
                "静风站名应留在默认位 y=" + F(defaultMidY) + "，实到 " + F(calmBox.MidY));

            SurfaceStation east = new SurfaceStation("东风站", 0.5d, 0.5d)
            {
                WindDirectionDegrees = 90d,
                WindSpeedMps = 10d,
            };
            SKRect eastBox = SurfaceChartTemplate.StationNameBoxOf(east, center, tokens.StationSize, tokens);
            Assert.True(Math.Abs(eastBox.MidY - defaultMidY) < 0.01f,
                "东风（杆朝屏幕右方）不占站名带，站名不应移动，实到 " + F(eastBox.MidY));

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                // 单独 HasInk 不够：面板网格线会穿过站名框，旧位置也能蹭出墨来。
                // 收窄到站名占位中心，要求文字密度远高于一条网格线。
                SKRect nameCore = new SKRect(center.X - 25f, nameBox.Top + 1f,
                    center.X + 25f, nameBox.Bottom - 1f);
                double nameInk = probe.NonWhiteRatio(nameCore);
                Assert.True(nameInk > 0.08d,
                    "让位后的站名应落在 " + Describe(nameCore) + "，非白占比应 >8%，实到 " + F(nameInk));
                AssertInk(probe, new SKRect(tip.X - 3f, tip.Y - 3f, tip.X + 3f, tip.Y + 3f), "朝上风杆末端");
            }
        }

        [Fact]
        public void 地面天气图整幅渲染有墨且图幅正确()
        {
            byte[] png = Render(BuildSurfaceDocument(), new SurfaceChartTemplate());
            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                Assert.True(probe.Width == 820 && probe.Height == 1060,
                    "地面天气图应按令牌 820×1060 出图，实到 " + probe.Width + "×" + probe.Height);
                AssertInk(probe, new SKRect(18f, 18f, 798f, 68f), "标题带");
                AssertInk(probe, new SKRect(18f, 70f, 798f, 1016f), "层次面板");
            }
        }

        // ================================================================== T261/T262 T-lnP

        private static SoundingDocument BuildSoundingDocument()
        {
            SoundingDocument document = new SoundingDocument();
            document.StationName = "示例探空站 54511";
            document.ValidTimeText = "2026-03-01 08 时";
            document.Levels.Add(new SoundingLevel(1000d, 20d, 12d));
            document.Levels.Add(new SoundingLevel(925d, 16d, 10d));
            document.Levels.Add(new SoundingLevel(850d, 10d, 4d));
            document.Levels.Add(new SoundingLevel(700d, 2d, -6d));
            document.Levels.Add(new SoundingLevel(500d, -12d, -22d));
            document.Levels.Add(new SoundingLevel(400d, -24d, -34d));
            document.Levels.Add(new SoundingLevel(300d, -42d, -52d));
            document.CharacteristicLevels.Add(new SoundingCharacteristicLevel(850d, "逆温层顶"));
            document.CharacteristicLevels.Add(new SoundingCharacteristicLevel(500d, "冻结层"));
            document.HatchedLevelIndex = 1;
            return document;
        }

        [Fact]
        public void TlnP等压线在对数气压轴上按同一比例等距()
        {
            SoundingDocument document = BuildSoundingDocument();
            TlnpTemplate template = new TlnpTemplate();
            ChartGeometry geometry;
            Render(document, template, out geometry);
            TlnpLayout layout = RequireLayout<TlnpLayout>(geometry);

            double y1000 = layout.YOf(1000d);
            double y500 = layout.YOf(500d);
            double y250 = layout.YOf(250d);
            double gap1 = Math.Abs(y1000 - y500);
            double gap2 = Math.Abs(y500 - y250);

            Assert.True(Math.Abs(gap1 - gap2) < 0.01d,
                "1000→500 与 500→250 的气压比同为 2，像素间距应相等；实到 " + F(gap1) + " px vs " + F(gap2) + " px");

            // 解析式：y = plot.Top + H·ln(p/pMin)/ln(pMax/pMin)（对数轴，等比例才等距）。
            double pMin = template.Tokens.PressureMinHpa;
            double pMax = template.Tokens.PressureMaxHpa;
            double h = layout.PlotRect.Height;
            for (int i = 0; i < template.Tokens.Isobars.Length; i++)
            {
                double p = template.Tokens.Isobars[i];
                double expected = layout.PlotRect.Top + h * Math.Log(p / pMin) / Math.Log(pMax / pMin);
                double actual = layout.YOf(p);
                Assert.True(Math.Abs(actual - expected) < 0.01d,
                    p + " hPa 的等压线 y 应为 " + F(expected) + "（对数解析式），实到 " + F(actual));
            }

            IReadOnlyList<SKPoint> line = layout.Families.PixelsOf(TlnpTemplate.FamilyIsobar, 500d);
            Assert.True(line.Count >= 2, "500 hPa 等压线裁剪后应至少 2 个像素点，实到 " + line.Count);
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            for (int i = 0; i < line.Count; i++)
            {
                Assert.True(Math.Abs(line[i].Y - y500) < 0.01d,
                    "等压线第 " + i + " 点 y=" + F(line[i].Y) + " 应等于轴上的 " + F(y500));
                minX = Math.Min(minX, line[i].X);
                maxX = Math.Max(maxX, line[i].X);
            }
            Assert.True(Math.Abs(minX - layout.PlotRect.Left) < 0.02d
                        && Math.Abs(maxX - layout.PlotRect.Right) < 0.02d,
                "500 hPa 等压线应横贯绘图区 [" + F(layout.PlotRect.Left) + "," + F(layout.PlotRect.Right)
                + "]，实到 [" + F(minX) + "," + F(maxX) + "]");
        }

        [Fact]
        public void TlnP等温线是斜直线且斜率等于解析值()
        {
            SoundingDocument document = BuildSoundingDocument();
            TlnpTemplate template = new TlnpTemplate();
            ChartGeometry geometry;
            Render(document, template, out geometry);
            TlnpLayout layout = RequireLayout<TlnpLayout>(geometry);

            IReadOnlyList<SKPoint> line = layout.Families.PixelsOf(TlnpTemplate.FamilyIsotherm, 20d);
            Assert.True(line.Count >= 2, "20℃ 等温线裁剪后应至少 2 个像素点，实到 " + line.Count);

            SKPoint a = line[0];
            SKPoint b = line[line.Count - 1];
            double measured = ((double)b.X - a.X) / ((double)b.Y - a.Y);

            double pMin = template.Tokens.PressureMinHpa;
            double pMax = template.Tokens.PressureMaxHpa;
            double xScale = layout.PlotRect.Width / (template.Tokens.TemperatureMaxC - template.Tokens.TemperatureMinC);
            // 纵轴向下为正：p 增大时 y 增大而等温线 x 减小，故像素斜率为负。
            double expected = -template.Tokens.SkewDegreesPerDecade
                            * Math.Log(pMax / pMin) / (Math.Log(10d) * layout.PlotRect.Height) * xScale;

            Assert.True(Math.Abs(measured - expected) < 1e-3,
                "等温线像素斜率应为 " + F(expected) + "（含温度轴比例 " + F(xScale) + "），实到 " + F(measured)
                + "；端点 (" + F(a.X) + "," + F(a.Y) + ")-(" + F(b.X) + "," + F(b.Y) + ")");

            // 三个交点须共线：同一族等温线在任意两个等压线之间的斜率一致。
            double firstSlope = double.NaN;
            double[] pressures = { 850d, 700d, 500d };
            SKPoint? previous = null;
            for (int i = 0; i < pressures.Length; i++)
            {
                SKPoint? hit = layout.Families.Intersect(TlnpTemplate.FamilyIsobar, pressures[i],
                    TlnpTemplate.FamilyIsotherm, 20d);
                Assert.True(hit != null, pressures[i] + " hPa 与 20℃ 等温线应有交点，实到 null");
                if (previous != null)
                {
                    double slope = ((double)hit!.Value.X - previous.Value.X) / ((double)hit.Value.Y - previous.Value.Y);
                    Assert.True(Math.Abs(slope - expected) < 2e-3,
                        pressures[i] + " hPa 与前一交点连线的斜率应为 " + F(expected) + "，实到 " + F(slope));
                    if (double.IsNaN(firstSlope))
                    {
                        firstSlope = slope;
                    }
                }
                previous = hit;
            }
            Assert.True(!double.IsNaN(firstSlope), "交点不足两个，无法验证等温线的直线性");
        }

        [Fact]
        public void TlnP等压线与等温线交点等于解析解()
        {
            SoundingDocument document = BuildSoundingDocument();
            TlnpTemplate template = new TlnpTemplate();
            ChartGeometry geometry;
            Render(document, template, out geometry);
            TlnpLayout layout = RequireLayout<TlnpLayout>(geometry);

            double pMin = template.Tokens.PressureMinHpa;
            double pMax = template.Tokens.PressureMaxHpa;
            double skew = template.Tokens.SkewDegreesPerDecade;
            double reference = layout.ReferencePressureHpa;
            double xScale = layout.PlotRect.Width / (template.Tokens.TemperatureMaxC - template.Tokens.TemperatureMinC);
            double[] pressures = { 850d, 700d, 500d };

            for (int i = 0; i < pressures.Length; i++)
            {
                double p = pressures[i];
                SKPoint? hit = layout.Families.Intersect(TlnpTemplate.FamilyIsobar, p,
                    TlnpTemplate.FamilyIsotherm, 20d);
                Assert.True(hit != null, p + " hPa 等压线与 20℃ 等温线应有交点，实到 null");

                double coordinate = 20d + skew * Math.Log10(reference / p);
                double expectedX = layout.PlotRect.Left
                                 + (coordinate - template.Tokens.TemperatureMinC) * xScale;
                double expectedY = layout.PlotRect.Top
                                 + layout.PlotRect.Height * Math.Log(p / pMin) / Math.Log(pMax / pMin);

                Assert.True(Math.Abs(hit!.Value.X - expectedX) < 0.02d,
                    p + " hPa 交点的 x 应为 " + F(expectedX) + "（x=T+" + F(skew) + "·log10(P0/p)），实到 "
                    + F(hit.Value.X));
                Assert.True(Math.Abs(hit.Value.Y - expectedY) < 0.02d,
                    p + " hPa 交点的 y 应为 " + F(expectedY) + "（对数轴），实到 " + F(hit.Value.Y));
            }
        }

        [Fact]
        public void T262特征层标签在绘图区内且不压温度和露点曲线()
        {
            SoundingDocument document = BuildSoundingDocument();
            TlnpTemplate template = new TlnpTemplate();
            ChartGeometry geometry;
            Render(document, template, out geometry);
            TlnpLayout layout = RequireLayout<TlnpLayout>(geometry);

            Assert.True(layout.CharacteristicLabels.Count == document.CharacteristicLevels.Count,
                "特征层标签数应等于文档给的 " + document.CharacteristicLevels.Count
                + "，实到 " + layout.CharacteristicLabels.Count);

            SKRect plot = layout.PlotRect;
            for (int k = 0; k < layout.CharacteristicLabels.Count; k++)
            {
                TlnpCharacteristicLabel label = layout.CharacteristicLabels[k];
                Assert.True(label.Box.Left >= plot.Left - 0.01f && label.Box.Right <= plot.Right + 0.01f
                            && label.Box.Top >= plot.Top - 0.01f && label.Box.Bottom <= plot.Bottom + 0.01f,
                    "标签“" + label.Text + "”越出绘图区：框 " + Describe(label.Box) + "，绘图区 " + Describe(plot));

                for (int i = 0; i + 1 < layout.TemperatureCurve.Count; i++)
                {
                    Assert.True(!TlnpTemplate.SegmentHitsRect(layout.TemperatureCurve[i],
                            layout.TemperatureCurve[i + 1], label.Box),
                        "标签“" + label.Text + "”压到温度曲线第 " + i + " 段，框 " + Describe(label.Box));
                }
                for (int i = 0; i + 1 < layout.DewPointCurve.Count; i++)
                {
                    Assert.True(!TlnpTemplate.SegmentHitsRect(layout.DewPointCurve[i],
                            layout.DewPointCurve[i + 1], label.Box),
                        "标签“" + label.Text + "”压到露点曲线第 " + i + " 段，框 " + Describe(label.Box));
                }

                for (int j = 0; j < k; j++)
                {
                    Assert.True(!BoxesOverlap(label.Box, layout.CharacteristicLabels[j].Box),
                        "标签“" + label.Text + "”与“" + layout.CharacteristicLabels[j].Text + "”相交："
                        + Describe(label.Box) + " vs " + Describe(layout.CharacteristicLabels[j].Box));
                }
            }

            // 正向对照：T/td 曲线本身确有墨，避免“曲线为空所以不压”这种假通过。
            byte[] png = Render(document, template);
            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                AssertInk(probe, new SKRect(layout.TemperatureCurve[2].X - 4f, layout.TemperatureCurve[2].Y - 4f,
                    layout.TemperatureCurve[2].X + 4f, layout.TemperatureCurve[2].Y + 4f), "温度曲线层次点");
                AssertInk(probe, new SKRect(layout.DewPointCurve[4].X - 4f, layout.DewPointCurve[4].Y - 4f,
                    layout.DewPointCurve[4].X + 4f, layout.DewPointCurve[4].Y + 4f), "露点曲线层次点");
            }
        }

        [Fact]
        public void TlnP图A18改倾斜量后等温线落点改变()
        {
            SoundingDocument document = BuildSoundingDocument();

            TlnpStyle defaultTokens = new TlnpStyle();
            TlnpTemplate defaultTemplate = new TlnpTemplate { Tokens = defaultTokens };
            ChartGeometry defaultGeometry;
            Render(document, defaultTemplate, out defaultGeometry);
            TlnpLayout defaultLayout = RequireLayout<TlnpLayout>(defaultGeometry);
            SKPoint? defaultHit = defaultLayout.Families.Intersect(TlnpTemplate.FamilyIsobar, 500d,
                TlnpTemplate.FamilyIsotherm, 20d);

            TlnpStyle skewedTokens = new TlnpStyle { SkewDegreesPerDecade = 80d };
            TlnpTemplate skewedTemplate = new TlnpTemplate { Tokens = skewedTokens };
            ChartGeometry skewedGeometry;
            Render(document, skewedTemplate, out skewedGeometry);
            TlnpLayout skewedLayout = RequireLayout<TlnpLayout>(skewedGeometry);
            SKPoint? skewedHit = skewedLayout.Families.Intersect(TlnpTemplate.FamilyIsobar, 500d,
                TlnpTemplate.FamilyIsotherm, 20d);

            Assert.True(defaultHit != null && skewedHit != null,
                "两种倾斜量下 500 hPa 与 20℃ 等温线都应相交：默认 " + (defaultHit == null ? "null" : "有")
                + "，改后 " + (skewedHit == null ? "null" : "有"));

            double xScale = defaultLayout.PlotRect.Width
                          / (defaultTokens.TemperatureMaxC - defaultTokens.TemperatureMinC);
            double expected = (defaultTokens.SkewDegreesPerDecade - skewedTokens.SkewDegreesPerDecade)
                            * Math.Log10(defaultLayout.ReferencePressureHpa / 500d) * xScale;
            double measured = (double)defaultHit!.Value.X - skewedHit!.Value.X;
            Assert.True(Math.Abs(measured - expected) < 0.02d,
                "SkewDegreesPerDecade 从 " + F(defaultTokens.SkewDegreesPerDecade) + " 改到 "
                + F(skewedTokens.SkewDegreesPerDecade) + " 后，500 hPa 处交点应右移 " + F(expected)
                + " px，实到 " + F(measured) + " px");
        }

        [Fact]
        public void TlnP整幅渲染有墨且图幅正确()
        {
            byte[] png = Render(BuildSoundingDocument(), new TlnpTemplate());
            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                Assert.True(probe.Width == 820 && probe.Height == 1060,
                    "T-lnP 应按令牌 820×1060 出图，实到 " + probe.Width + "×" + probe.Height);
                AssertInk(probe, new SKRect(70f, 78f, 790f, 992f), "坐标纸绘图区");
                AssertInk(probe, new SKRect(18f, 18f, 798f, 68f), "标题带");
            }
        }

        [Fact]
        public void TlnP气压轴标题与顶端刻度各占一带()
        {
            SoundingDocument document = BuildSoundingDocument();
            TlnpTemplate template = new TlnpTemplate();
            TlnpStyle tokens = template.Tokens;
            ChartGeometry geometry;
            byte[] png = Render(document, template, out geometry);
            TlnpLayout layout = RequireLayout<TlnpLayout>(geometry);

            float y = layout.PressureAxis.PositionOf(tokens.PressureMinHpa);
            Assert.True(Math.Abs(y - layout.PlotRect.Top) < 0.01f,
                "顶端 " + F(tokens.PressureMinHpa) + " hPa 应落在绘图区上缘 " + F(layout.PlotRect.Top)
                + "，实到 y=" + F(y));

            float tickHeight = tokens.AxisLabelSize * tokens.AxisLabelHeightFactor;
            SKRect tickBox = new SKRect(
                layout.PlotRect.Left - tokens.PlotLeftGutter + tokens.AxisLabelPadding,
                y - tickHeight / 2f,
                layout.PlotRect.Left - tokens.GridTickLength - tokens.AxisLabelPadding,
                y + tickHeight / 2f);

            Assert.True(layout.PressureAxisTitleRect.Bottom <= tickBox.Top + 0.01f,
                "气压轴标题带下缘 " + F(layout.PressureAxisTitleRect.Bottom)
                + " 应在顶端刻度带上缘 " + F(tickBox.Top) + " 之上，否则标题下半截会压住 100");
            Assert.True(layout.PressureAxisTitleRect.Top >= layout.TitleRect.Bottom - 0.01f,
                "气压轴标题带应落在页标题之下：标题带上缘 " + F(layout.PressureAxisTitleRect.Top)
                + "，页标题下缘 " + F(layout.TitleRect.Bottom));
            Assert.True(layout.PressureAxisTitleRect.Bottom <= layout.PlotRect.Top,
                "气压轴标题带不应伸进绘图区：带下缘 " + F(layout.PressureAxisTitleRect.Bottom)
                + "，绘图区上缘 " + F(layout.PlotRect.Top));

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                AssertInk(probe, layout.PressureAxisTitleRect, "气压轴标题带");
                AssertInk(probe, tickBox, "顶端 100 刻度");

                // 只验带内有墨不够：旧实现把标题画进刻度带，字的上半截仍会落进标题带。
                // 取标题文字左段（避开右对齐的 "100"），带下到绘图区上缘之间必须干净。
                SKRect belowBand = new SKRect(layout.PressureAxisTitleRect.Left,
                    layout.PressureAxisTitleRect.Bottom + 1f,
                    layout.PressureAxisTitleRect.Left + tokens.PlotLeftGutter * 0.6f,
                    layout.PlotRect.Top - 1f);
                Assert.True(!probe.HasInkInBox((int)Math.Floor(belowBand.Left), (int)Math.Floor(belowBand.Top),
                        (int)Math.Ceiling(belowBand.Right), (int)Math.Ceiling(belowBand.Bottom)),
                    "标题带下缘到绘图区上缘之间不应有墨（旧实现标题下半截伸到这里）：" + Describe(belowBand));
            }
        }

    }
}
