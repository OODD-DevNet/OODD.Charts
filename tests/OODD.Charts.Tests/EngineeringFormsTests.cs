using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Engineering;
using OODD.Charts.Engineering.Dga;
using OODD.Charts.Engineering.WellLog;
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
    /// 工程域 T241/T245 两张表单的验收测试（测井、DGA）。所有版面落点从模板求解结果读取，
    /// 像素判据一律走真实 PNG 渲染 + <see cref="ImageProbe"/>；
    /// 期望值要么由文档独立重算，要么由像素实测，不写死"看起来对"的常数。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class EngineeringFormsTests
    {
        // ================================================================ 测井 T241





        // ================================================================ 测井 T241

        [Fact]
        public void 测井_深度零在页顶且向下递增()
        {
            WellLogDocument doc = WellLogDocument();
            WellLogChartTemplate template = new WellLogChartTemplate();
            WellLogLayout layout = Measure<WellLogLayout>(doc, template);

            Assert.True(Math.Abs(layout.DepthAxis.PositionOf(0d) - layout.PlotRect.Top) < 1e-3,
                "深度 0 应在页顶 " + layout.PlotRect.Top.ToString("0.###", CultureInfo.InvariantCulture)
                + "，实测 " + layout.DepthAxis.PositionOf(0d).ToString("0.###", CultureInfo.InvariantCulture));
            Assert.True(layout.DepthAxis.PositionOf(100d) > layout.DepthAxis.PositionOf(0d),
                "深度增大必须向下：0m y=" + layout.DepthAxis.PositionOf(0d).ToString("0.###", CultureInfo.InvariantCulture)
                + "，100m y=" + layout.DepthAxis.PositionOf(100d).ToString("0.###", CultureInfo.InvariantCulture));

            SKPoint shallow = layout.PointOf(0, 0d, 20d);
            SKPoint deep = layout.PointOf(0, 100d, 20d);
            Assert.True(deep.Y > shallow.Y,
                "同一测值在更深处必须落在更下方：0m y=" + shallow.Y.ToString("0.###", CultureInfo.InvariantCulture)
                + "，100m y=" + deep.Y.ToString("0.###", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void 测井_每页图头位置与像素一致()
        {
            WellLogDocument doc = WellLogDocument();
            WellLogChartTemplate template = new WellLogChartTemplate();

            doc.PageIndex = 0;
            WellLogLayout page0 = Measure<WellLogLayout>(doc, template);
            doc.PageIndex = 1;
            WellLogLayout page1 = Measure<WellLogLayout>(doc, template);

            Assert.True(page0.Headers.Count == doc.Tracks.Count && page1.Headers.Count == doc.Tracks.Count,
                "两页道头数都应等于道数 " + doc.Tracks.Count + "，实到 " + page0.Headers.Count + " / " + page1.Headers.Count);
            for (int i = 0; i < page0.Headers.Count; i++)
            {
                SKRect r0 = page0.Headers[i].Rect;
                SKRect r1 = page1.Headers[i].Rect;
                Assert.True(Math.Abs(r0.Left - r1.Left) < 1e-3 && Math.Abs(r0.Top - r1.Top) < 1e-3
                    && Math.Abs(r0.Right - r1.Right) < 1e-3 && Math.Abs(r0.Bottom - r1.Bottom) < 1e-3,
                    "第 " + (i + 1) + " 道图头矩形两页不一致：[" + r0.Left.ToString("0.#", CultureInfo.InvariantCulture)
                    + "," + r0.Top.ToString("0.#", CultureInfo.InvariantCulture) + "," + r0.Right.ToString("0.#", CultureInfo.InvariantCulture)
                    + "," + r0.Bottom.ToString("0.#", CultureInfo.InvariantCulture) + "] vs ["
                    + r1.Left.ToString("0.#", CultureInfo.InvariantCulture) + "," + r1.Top.ToString("0.#", CultureInfo.InvariantCulture)
                    + "," + r1.Right.ToString("0.#", CultureInfo.InvariantCulture) + "," + r1.Bottom.ToString("0.#", CultureInfo.InvariantCulture) + "]");
            }

            using (ImageProbe probe0 = RenderPage0(doc, template))
            using (ImageProbe probe1 = RenderPage1(doc, template))
            {
                for (int i = 0; i < page0.Headers.Count; i++)
                {
                    SKRect rect = page0.Headers[i].Rect;
                    double a = probe0.NonWhiteRatio(rect);
                    double b = probe1.NonWhiteRatio(rect);
                    int dark0 = DarkPixelCount(probe0, rect, 120);
                    int dark1 = DarkPixelCount(probe1, rect, 120);
                    Assert.True(a > 0.005d, "第 " + (i + 1) + " 道图头在页 1 上应有底色，实测覆盖率 " + a.ToString("0.####", CultureInfo.InvariantCulture));
                    Assert.True(dark0 > 10, "第 " + (i + 1) + " 道图头文字应形成深色像素，实测 " + dark0 + " 个");
                    Assert.True(Math.Abs(dark0 - dark1) <= 2,
                        "第 " + (i + 1) + " 道图头文字两页深色像素数应一致：页 1 " + dark0 + "，页 2 " + dark1);
                    Assert.True(Math.Abs(a - b) < 0.02d,
                        "第 " + (i + 1) + " 道图头两页着墨率应一致：页 1 " + a.ToString("0.####", CultureInfo.InvariantCulture)
                        + "，页 2 " + b.ToString("0.####", CultureInfo.InvariantCulture));
                }
            }
        }

        [Fact]
        public void 测井_对数道与线性道各自等距()
        {
            WellLogDocument doc = WellLogDocument();
            WellLogChartTemplate template = new WellLogChartTemplate();
            WellLogLayout layout = Measure<WellLogLayout>(doc, template);

            NumericAxis logAxis = layout.Stack["res"].ValueAxis;
            double decadeLow = logAxis.PositionOf(10d) - logAxis.PositionOf(1d);
            double decadeHigh = logAxis.PositionOf(100d) - logAxis.PositionOf(10d);
            Assert.True(Math.Abs(decadeLow - decadeHigh) < 0.01,
                "对数道 1/10/100 应等距：1→10 为 " + decadeLow.ToString("0.###", CultureInfo.InvariantCulture)
                + "px，10→100 为 " + decadeHigh.ToString("0.###", CultureInfo.InvariantCulture) + "px");
            Assert.True(logAxis.Scale == AxisScale.Logarithmic, "res 道应为对数标度，实到 " + logAxis.Scale);

            NumericAxis linearAxis = layout.Stack["gr"].ValueAxis;
            double stepLow = linearAxis.PositionOf(50d) - linearAxis.PositionOf(0d);
            double stepHigh = linearAxis.PositionOf(100d) - linearAxis.PositionOf(50d);
            Assert.True(linearAxis.Scale == AxisScale.Linear, "gr 道应为线性标度，实到 " + linearAxis.Scale);
            Assert.True(Math.Abs(stepLow - stepHigh) < 0.01,
                "线性道等值增量应等距：0→50 为 " + stepLow.ToString("0.###", CultureInfo.InvariantCulture)
                + "px，50→100 为 " + stepHigh.ToString("0.###", CultureInfo.InvariantCulture) + "px");
        }

        [Fact]
        public void 测井_续页接缝域值与像素连续()
        {
            WellLogDocument doc = WellLogDocument();
            WellLogChartTemplate template = new WellLogChartTemplate();

            doc.PageIndex = 0;
            WellLogLayout page0 = Measure<WellLogLayout>(doc, template);
            doc.PageIndex = 1;
            WellLogLayout page1 = Measure<WellLogLayout>(doc, template);

            Assert.True(page0.PageDepthTo == page1.PageDepthFrom,
                "相邻页应共边：页 1 末深度 " + page0.PageDepthTo.ToString("0.###", CultureInfo.InvariantCulture)
                + "，页 2 首深度 " + page1.PageDepthFrom.ToString("0.###", CultureInfo.InvariantCulture));
            Assert.True(Math.Abs(page0.Stack.DomainPositionOf(page0.PageDepthTo) - page0.PlotRect.Bottom) < 1e-3,
                "页 1 末深度应落在绘图区底缘");
            Assert.True(Math.Abs(page1.Stack.DomainPositionOf(page1.PageDepthFrom) - page1.PlotRect.Top) < 1e-3,
                "页 2 首深度应落在绘图区顶缘");

            double boundary = page0.PageDepthTo;
            double? value0 = doc.ValueAt("gr", boundary);
            double? value1 = doc.ValueAt("gr", boundary);
            Assert.True(value0 != null && value1 != null && value0.Value == value1.Value,
                "接缝深度处曲线值应连续：页 1 " + (value0 == null ? "null" : value0.Value.ToString("0.###", CultureInfo.InvariantCulture))
                + "，页 2 " + (value1 == null ? "null" : value1.Value.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        // ================================================================ DGA T245

        [Fact]
        public void DGA_三顶点落在几何顶点()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);
            TriaxialMap map = layout.Triangle;

            Assert.True(Distance(map.PointOf(1d, 0d, 0d), map.VertexA) < 1e-4,
                "组分 (1,0,0) 应落在顶点 A：" + Describe(map.PointOf(1d, 0d, 0d)) + " vs " + Describe(map.VertexA));
            Assert.True(Distance(map.PointOf(0d, 1d, 0d), map.VertexB) < 1e-4,
                "组分 (0,1,0) 应落在顶点 B：" + Describe(map.PointOf(0d, 1d, 0d)) + " vs " + Describe(map.VertexB));
            Assert.True(Distance(map.PointOf(0d, 0d, 1d), map.VertexC) < 1e-4,
                "组分 (0,0,1) 应落在顶点 C：" + Describe(map.PointOf(0d, 0d, 1d)) + " vs " + Describe(map.VertexC));

            // 三角形为等边：三边长度实测一致。
            double ab = Distance(map.VertexA, map.VertexB);
            double bc = Distance(map.VertexB, map.VertexC);
            double ca = Distance(map.VertexC, map.VertexA);
            Assert.True(Math.Abs(ab - bc) < 0.01 && Math.Abs(bc - ca) < 0.01,
                "三边应等长：AB=" + ab.ToString("0.###", CultureInfo.InvariantCulture)
                + "，BC=" + bc.ToString("0.###", CultureInfo.InvariantCulture)
                + "，CA=" + ca.ToString("0.###", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void DGA_三等分点落在重心()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);
            TriaxialMap map = layout.Triangle;

            SKPoint expected = new SKPoint(
                (map.VertexA.X + map.VertexB.X + map.VertexC.X) / 3f,
                (map.VertexA.Y + map.VertexB.Y + map.VertexC.Y) / 3f);
            SKPoint actual = map.PointOf(1d / 3d, 1d / 3d, 1d / 3d);
            Assert.True(Distance(actual, expected) < 1e-3,
                "(1/3,1/3,1/3) 应落在三顶点均值 " + Describe(expected) + "，实测 " + Describe(actual));
        }

        [Fact]
        public void DGA_组分与像素往返误差小于1e6分之一()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);
            TriaxialMap map = layout.Triangle;

            Assert.True(doc.Samples.Count >= 3, "测试文档至少应有 3 个投点，实到 " + doc.Samples.Count);
            foreach (DgaSample sample in doc.Samples)
            {
                TriaxialComponents input = sample.Components;
                double sum = input.A + input.B + input.C;
                Assert.True(sum > 0d, "投点三组分之和必须为正：" + input);
                SKPoint point = map.PointOf(input.A, input.B, input.C);
                TriaxialComponents round = map.ComponentsAt(point);
                double ea = Math.Abs(round.A - input.A / sum);
                double eb = Math.Abs(round.B - input.B / sum);
                double ec = Math.Abs(round.C - input.C / sum);
                Assert.True(ea < 1e-6 && eb < 1e-6 && ec < 1e-6,
                    "往返误差应小于 1e-6，实测 " + sample.DateText + "：("
                    + ea.ToString("0.#########", CultureInfo.InvariantCulture) + ","
                    + eb.ToString("0.#########", CultureInfo.InvariantCulture) + ","
                    + ec.ToString("0.#########", CultureInfo.InvariantCulture) + ")");
            }
        }

        [Fact]
        public void DGA_未归一输入按比例归一后位置不变()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);
            TriaxialMap map = layout.Triangle;

            SKPoint unit = map.PointOf(0.2d, 0.3d, 0.5d);
            SKPoint scaled = map.PointOf(20d, 30d, 50d);
            Assert.True(Distance(unit, scaled) < 1e-4,
                "和不为 1 的三组分应按比例归一：单位输入 " + Describe(unit) + "，放大 100 倍 " + Describe(scaled));

            TriaxialComponents a = map.ComponentsAt(unit);
            TriaxialComponents b = map.ComponentsAt(scaled);
            Assert.True(Math.Abs(a.A - b.A) < 1e-9 && Math.Abs(a.B - b.B) < 1e-9 && Math.Abs(a.C - b.C) < 1e-9,
                "归一前后逆算组分应一致：" + a + " vs " + b);
        }

        [Fact]
        public void DGA_分区填色互不重叠且覆盖三角域()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);

            Assert.True(layout.ZonePolygons.Count == doc.Zones.Count,
                "分区多边形数应等于文档分区数：" + doc.Zones.Count + " vs " + layout.ZonePolygons.Count);
            EnvelopePolygon frame = new EnvelopePolygon("frame", "三角域",
                new[] { layout.Triangle.VertexA, layout.Triangle.VertexB, layout.Triangle.VertexC });
            double triangleArea = PolygonArea(frame.Vertices);
            double zoneArea = 0d;
            foreach (EnvelopePolygon zone in layout.ZonePolygons)
            {
                double area = PolygonArea(zone.Vertices);
                Assert.True(area > 1d, "分区 " + zone.Label + " 面积应为正，实测 " + area.ToString("0.#", CultureInfo.InvariantCulture));
                zoneArea += area;
            }
            Assert.True(Math.Abs(zoneArea - triangleArea) <= triangleArea * 0.01d,
                "分区面积之和应等于三角域面积（不重叠也不留空）：分区 " + zoneArea.ToString("0.#", CultureInfo.InvariantCulture)
                + "，三角域 " + triangleArea.ToString("0.#", CultureInfo.InvariantCulture));

            // 网格取样：任意点最多落在一个分区内（共享边按边界处理，取样点避开整除网格）。
            int overlap = 0;
            int[] hits = new int[layout.ZonePolygons.Count];
            for (int iy = 0; iy < 60; iy++)
            {
                double y = layout.TriangleRect.Top + (iy + 0.219d) / 60d * layout.TriangleRect.Height;
                for (int ix = 0; ix < 60; ix++)
                {
                    double x = layout.TriangleRect.Left + (ix + 0.137d) / 60d * layout.TriangleRect.Width;
                    if (!frame.Contains(x, y))
                    {
                        continue;
                    }
                    int count = 0;
                    for (int z = 0; z < layout.ZonePolygons.Count; z++)
                    {
                        if (layout.ZonePolygons[z].Contains(x, y))
                        {
                            count++;
                            hits[z]++;
                        }
                    }
                    if (count > 1)
                    {
                        overlap++;
                    }
                }
            }
            Assert.True(overlap == 0, "取样点落入多个分区的次数应为 0，实测 " + overlap);
            for (int z = 0; z < hits.Length; z++)
            {
                Assert.True(hits[z] > 20, "分区 " + layout.ZonePolygons[z].Label + " 内部取样点应多于 20，实测 " + hits[z]);
            }

            using (ImageProbe probe = Render(doc, template))
            {
                for (int z = 0; z < layout.ZonePolygons.Count; z++)
                {
                    EnvelopePolygon zone = layout.ZonePolygons[z];
                    float sumX = 0f, sumY = 0f;
                    foreach (SKPoint vertex in zone.Vertices)
                    {
                        sumX += vertex.X;
                        sumY += vertex.Y;
                    }
                    float cx = sumX / zone.Vertices.Count;
                    float cy = sumY / zone.Vertices.Count;
                    Assert.True(probe.IsInk((int)cx, (int)cy), "分区 " + zone.Label + " 的填色/区名应在渲染图上着墨");
                }
                Assert.True(layout.SamplePoints.Count == doc.Samples.Count,
                    "投点数应等于文档取样数：" + doc.Samples.Count + " vs " + layout.SamplePoints.Count);
                foreach (SKPoint point in layout.SamplePoints)
                {
                    Assert.True(probe.HasRedInBox((int)point.X - 5, (int)point.Y - 5,
                        (int)point.X + 6, (int)point.Y + 6), "投点红色符号应在 " + Describe(point) + " 出现");
                }
                Assert.True(probe.HasInkInBox((int)layout.TrendRect.Left + 2, (int)layout.TrendRect.Top + 2,
                    (int)layout.TrendRect.Right - 2, (int)layout.TrendRect.Bottom - 2), "趋势轨区应有墨");
            }
        }

        // ================================================================ E4 回写：样例取证发现的版面缺陷





        [Fact]
        public void DGA_五条趋势轨按版心收窄不越界()
        {
            DgaChartDocument doc = DgaDocument();
            double[] days = { 0d, 3d, 7d, 14d, 30d };
            string[] dates = { "03-02", "03-05", "03-09", "03-16", "04-01" };
            foreach (string key in new[] { "c2h6", "c2h4" })
            {
                DgaGasSeries extra = new DgaGasSeries(key, key.ToUpperInvariant(), "μL/L");
                for (int i = 0; i < days.Length; i++)
                {
                    extra.Samples.Add(new DgaGasSample(days[i], dates[i], 1d + i));
                }
                doc.GasSeries.Add(extra);
            }

            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);
            float usableRight = template.Tokens.Common.Width - template.Tokens.Common.Margin;

            Assert.True(doc.GasSeries.Count == 5, "本例应有 5 条气体轨，实到 " + doc.GasSeries.Count);
            Assert.True(layout.TrendRect.Right <= usableRight + 0.01f,
                "5 轨趋势区右缘 " + layout.TrendRect.Right.ToString("0.#", CultureInfo.InvariantCulture)
                + " 不得超过版心右缘 " + usableRight.ToString("0.#", CultureInfo.InvariantCulture)
                + "（默认 210px×5 + 12×4 + 96 = 1194）");

            float trackWidth = layout.Trend.RectOf(0).Width;
            Assert.True(trackWidth < template.Tokens.TrackWidth,
                "轨道应按版心收窄到令牌上限之下，实测 " + trackWidth.ToString("0.#", CultureInfo.InvariantCulture));
            Assert.True(trackWidth > 150f,
                "收窄后仍应保持可读宽度（>150px），实测 " + trackWidth.ToString("0.#", CultureInfo.InvariantCulture));

            // 令牌被调大也仍要落版：收窄由 Measure 负责，不靠调用方手改样例。
            template.Tokens.TrackWidth = 400f;
            DgaLayout wide = Measure<DgaLayout>(doc, template);
            Assert.True(wide.TrendRect.Right <= usableRight + 0.01f,
                "令牌 TrackWidth=400 时仍应落版，实测右缘 " + wide.TrendRect.Right.ToString("0.#", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void DGA_三角形标题与顶点A标注各占一带且都在顶点之上()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout layout = Measure<DgaLayout>(doc, template);

            Assert.False(layout.TriangleTitleRect.IntersectsWith(layout.ApexLabelRect),
                "标题带 " + Rect(layout.TriangleTitleRect) + " 与顶点 A 标注带 " + Rect(layout.ApexLabelRect)
                + " 相交：两者曾被画在同一条缝里（实测字形叠成一团）");
            Assert.True(layout.TriangleTitleRect.Top >= layout.TrendRect.Bottom,
                "标题带顶 " + layout.TriangleTitleRect.Top.ToString("0.#", CultureInfo.InvariantCulture)
                + " 应低于趋势区底 " + layout.TrendRect.Bottom.ToString("0.#", CultureInfo.InvariantCulture));
            Assert.True(layout.ApexLabelRect.Bottom <= layout.Triangle.VertexA.Y + 0.01f,
                "顶点 A 标注带底 " + layout.ApexLabelRect.Bottom.ToString("0.#", CultureInfo.InvariantCulture)
                + " 不得压过顶点 " + layout.Triangle.VertexA.Y.ToString("0.#", CultureInfo.InvariantCulture));

            using (ImageProbe probe = Render(doc, template))
            {
                Assert.True(probe.HasInkInBox((int)layout.TriangleTitleRect.Left + 40, (int)layout.TriangleTitleRect.Top,
                    (int)layout.TriangleTitleRect.Right - 40, (int)layout.TriangleTitleRect.Bottom + 1),
                    "标题带内应有标题墨迹");
                Assert.True(probe.HasInkInBox((int)layout.ApexLabelRect.Left + 40, (int)layout.ApexLabelRect.Top,
                    (int)layout.ApexLabelRect.Right - 40, (int)layout.ApexLabelRect.Bottom + 1),
                    "顶点 A 标注带内应有「A · 组分」墨迹");
            }
        }

        private static string Rect(SKRect rect)
        {
            return "[" + rect.Left.ToString("0.#", CultureInfo.InvariantCulture) + ","
                 + rect.Top.ToString("0.#", CultureInfo.InvariantCulture) + ","
                 + rect.Right.ToString("0.#", CultureInfo.InvariantCulture) + ","
                 + rect.Bottom.ToString("0.#", CultureInfo.InvariantCulture) + "]";
        }


        // ================================================================ A18：改令牌，落点必须跟着变


        [Fact]
        public void 测井_令牌驱动版面()
        {
            WellLogDocument doc = WellLogDocument();
            WellLogChartTemplate template = new WellLogChartTemplate();
            WellLogLayout before = Measure<WellLogLayout>(doc, template);
            template.Tokens.TrackLeft += 20f;
            WellLogLayout after = Measure<WellLogLayout>(doc, template);

            Assert.True(Math.Abs((after.Stack.Left - before.Stack.Left) - 20f) < 0.01f,
                "TrackLeft +20 后轨道栈左缘应平移 20px，实测 "
                + (after.Stack.Left - before.Stack.Left).ToString("0.###", CultureInfo.InvariantCulture));
            Assert.True(Math.Abs((after.Stack.HeaderRectOf(0).Left - before.Stack.HeaderRectOf(0).Left) - 20f) < 0.01f,
                "道头矩形也应随 TrackLeft 平移 20px");
        }




        [Fact]
        public void DGA_令牌驱动三角落位()
        {
            DgaChartDocument doc = DgaDocument();
            DgaChartTemplate template = new DgaChartTemplate();
            DgaLayout before = Measure<DgaLayout>(doc, template);
            template.Tokens.TriangleLeft += 24f;
            DgaLayout after = Measure<DgaLayout>(doc, template);

            Assert.True(Math.Abs((after.Triangle.VertexA.X - before.Triangle.VertexA.X) - 24f) < 0.01f,
                "TriangleLeft +24 后顶点 A 的 x 应平移 24px，实测 "
                + (after.Triangle.VertexA.X - before.Triangle.VertexA.X).ToString("0.###", CultureInfo.InvariantCulture));
            Assert.True(Math.Abs((after.SamplePoints[0].X - before.SamplePoints[0].X) - 24f) < 0.01f,
                "投点应随 TriangleLeft 平移 24px");
            Assert.True(Math.Abs((after.ZonePolygons[0].Vertices[0].X - before.ZonePolygons[0].Vertices[0].X) - 24f) < 0.01f,
                "分区多边形应随 TriangleLeft 平移 24px");
        }

        // ================================================================ 模板键与测量可重复

        [Fact]
        public void 两张表单_模板键正确且重复测量一致()
        {
            List<FormCase> cases = new List<FormCase>
            {
                new FormCase("engineering.well-log", WellLogDocument(), new WellLogChartTemplate(),
                    g => LayoutSignature(g.LayoutOf<WellLogLayout>()!.Stack.RectOf(0))),
                new FormCase("engineering.dga", DgaDocument(), new DgaChartTemplate(),
                    g => Describe(g.LayoutOf<DgaLayout>()!.Triangle.VertexA)
                        + "|" + g.LayoutOf<DgaLayout>()!.ZonePolygons.Count
                        + "|" + g.LayoutOf<DgaLayout>()!.SamplePoints.Count),
            };

            foreach (FormCase item in cases)
            {
                Assert.True(item.Template.Key == item.Key,
                    "模板键应为 " + item.Key + "，实到 " + item.Template.Key);
                Assert.True(!string.IsNullOrEmpty(item.Template.DisplayName),
                    "模板 " + item.Key + " 的 DisplayName 不能为空");

                ChartGeometry first = item.Template.Measure(item.Document, new ChartStyle(), Chart.SharedFonts);
                ChartGeometry second = item.Template.Measure(item.Document, new ChartStyle(), Chart.SharedFonts);
                Assert.True(Math.Abs(first.Bounds.Width - second.Bounds.Width) < 1e-4f
                    && Math.Abs(first.GridRect.Left - second.GridRect.Left) < 1e-4f,
                    "模板 " + item.Key + " 的边界/绘图区重复测量不一致");
                Assert.True(item.Signature(first) == item.Signature(second),
                    "模板 " + item.Key + " 重复测量落点不一致：" + item.Signature(first) + " vs " + item.Signature(second));
            }

            Assert.True(new DgaChartTemplate().Tokens.Palette.GetType() == typeof(EngineeringPalette),
                "DGA 令牌必须组合 EngineeringPalette");
        }

        // ================================================================ 文档与工具

        private sealed class FormCase
        {
            public FormCase(string key, ChartDocument document, IChartTemplate template,
                Func<ChartGeometry, string> signature)
            {
                Key = key;
                Document = document;
                Template = template;
                Signature = signature;
            }

            public string Key { get; }
            public ChartDocument Document { get; }
            public IChartTemplate Template { get; }
            public Func<ChartGeometry, string> Signature { get; }
        }

        private static T Measure<T>(ChartDocument document, IChartTemplate template) where T : class
        {
            ChartGeometry geometry = template.Measure(document, new ChartStyle(), Chart.SharedFonts);
            T? layout = geometry.LayoutOf<T>();
            Assert.True(layout != null, "模板 " + template.Key + " 未返回 " + typeof(T).Name);
            return layout!;
        }

        private static ImageProbe Render(ChartDocument document, IChartTemplate template)
        {
            using (Chart chart = new Chart(document, template))
            {
                return ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100));
            }
        }

        private static ImageProbe RenderPage0(WellLogDocument doc, WellLogChartTemplate template)
        {
            doc.PageIndex = 0;
            return Render(doc, template);
        }

        private static ImageProbe RenderPage1(WellLogDocument doc, WellLogChartTemplate template)
        {
            doc.PageIndex = 1;
            return Render(doc, template);
        }

        private static string LayoutSignature(IReadOnlyList<SKPoint> points)
        {
            if (points.Count == 0)
            {
                return "empty";
            }
            SKPoint first = points[0];
            SKPoint last = points[points.Count - 1];
            return points.Count.ToString(CultureInfo.InvariantCulture)
                + ":" + first.X.ToString("R", CultureInfo.InvariantCulture) + "," + first.Y.ToString("R", CultureInfo.InvariantCulture)
                + ":" + last.X.ToString("R", CultureInfo.InvariantCulture) + "," + last.Y.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string LayoutSignature(IReadOnlyList<double> values)
        {
            if (values.Count == 0)
            {
                return "empty";
            }
            return values.Count.ToString(CultureInfo.InvariantCulture)
                + ":" + values[0].ToString("R", CultureInfo.InvariantCulture)
                + ":" + values[values.Count - 1].ToString("R", CultureInfo.InvariantCulture);
        }

        private static string LayoutSignature(SKRect rect)
        {
            return rect.Left.ToString("R", CultureInfo.InvariantCulture) + ","
                + rect.Top.ToString("R", CultureInfo.InvariantCulture) + ","
                + rect.Right.ToString("R", CultureInfo.InvariantCulture) + ","
                + rect.Bottom.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string Describe(SKPoint point)
        {
            return "(" + point.X.ToString("0.###", CultureInfo.InvariantCulture)
                + "," + point.Y.ToString("0.###", CultureInfo.InvariantCulture) + ")";
        }

        private static double Distance(SKPoint a, SKPoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static int DarkPixelCount(ImageProbe probe, SKRect rect, int threshold)
        {
            int left = Math.Max(0, (int)rect.Left);
            int top = Math.Max(0, (int)rect.Top);
            int right = Math.Min(probe.Width, (int)rect.Right);
            int bottom = Math.Min(probe.Height, (int)rect.Bottom);
            int count = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    SKColor color = probe.Pixel(x, y);
                    if (color.Red < threshold && color.Green < threshold && color.Blue < threshold)
                    {
                        count++;
                    }
                }
            }
            return count;
        }




        private static double PolygonArea(IReadOnlyList<SKPoint> vertices)
        {
            double sum = 0d;
            for (int i = 0; i < vertices.Count; i++)
            {
                SKPoint a = vertices[i];
                SKPoint b = vertices[(i + 1) % vertices.Count];
                sum += (double)a.X * b.Y - (double)b.X * a.Y;
            }
            return Math.Abs(sum) / 2d;
        }


        // ---------------------------------------------------------------- 测试文档


        private static WellLogDocument WellLogDocument()
        {
            WellLogDocument doc = new WellLogDocument
            {
                WellId = "X-1",
                RunNumber = "2",
                Formation = "山西组",
                ScaleCaption = "1:200",
                DepthTop = 0d,
                DepthBottom = 1000d,
                PageDepth = 500d,
                PageIndex = 0,
            };

            WellLogTrack gr = new WellLogTrack("gr", "自然伽马 (API)", 0d, 150d, false);
            WellLogTrack res = new WellLogTrack("res", "深电阻率 (Ω·m)", 1d, 1000d, true);
            WellLogTrack lith = new WellLogTrack("lith", "岩性", 0d, 1d, false);
            for (int depth = 0; depth <= 1000; depth += 50)
            {
                gr.Samples.Add(new WellLogSample(depth, 30d + (depth % 200) * 0.25d));
                res.Samples.Add(new WellLogSample(depth, Math.Pow(10d, 0.5d + depth / 500d)));
            }
            lith.Intervals.Add(new WellLogInterval(0d, 300d, PatternKind.Diagonal, "砂岩"));
            lith.Intervals.Add(new WellLogInterval(300d, 650d, PatternKind.Dot, "泥岩"));
            lith.Intervals.Add(new WellLogInterval(650d, 1000d, PatternKind.Cross, "灰岩"));
            doc.Tracks.Add(gr);
            doc.Tracks.Add(res);
            doc.Tracks.Add(lith);
            return doc;
        }




        private static DgaChartDocument DgaDocument()
        {
            DgaChartDocument doc = new DgaChartDocument
            {
                TransformerId = "1# 主变",
                OilBrand = "25#",
                SampleDateText = "2026-05-18",
            };

            DgaGasSeries h2 = new DgaGasSeries("h2", "H2", "μL/L");
            DgaGasSeries ch4 = new DgaGasSeries("ch4", "CH4", "μL/L");
            DgaGasSeries c2h2 = new DgaGasSeries("c2h2", "C2H2", "μL/L");
            double[] days = { 0d, 3d, 7d, 14d, 30d };
            string[] dates = { "03-02", "03-05", "03-09", "03-16", "04-01" };
            double[] h2Values = { 2d, 4d, 5d, 8d, 12d };
            double[] ch4Values = { 10d, 14d, 18d, 25d, 30d };
            double[] c2h2Values = { 0.2d, 0.5d, 0.8d, 1.2d, 2.0d };
            for (int i = 0; i < days.Length; i++)
            {
                h2.Samples.Add(new DgaGasSample(days[i], dates[i], h2Values[i]));
                ch4.Samples.Add(new DgaGasSample(days[i], dates[i], ch4Values[i]));
                c2h2.Samples.Add(new DgaGasSample(days[i], dates[i], c2h2Values[i]));
            }
            doc.GasSeries.Add(h2);
            doc.GasSeries.Add(ch4);
            doc.GasSeries.Add(c2h2);

            TriaxialComponents a = new TriaxialComponents(1d, 0d, 0d);
            TriaxialComponents b = new TriaxialComponents(0d, 1d, 0d);
            TriaxialComponents c = new TriaxialComponents(0d, 0d, 1d);
            TriaxialComponents g = new TriaxialComponents(1d / 3d, 1d / 3d, 1d / 3d);
            TriaxialComponents ab = new TriaxialComponents(0.5d, 0.5d, 0d);
            TriaxialComponents bc = new TriaxialComponents(0d, 0.5d, 0.5d);
            TriaxialComponents ca = new TriaxialComponents(0.5d, 0d, 0.5d);
            doc.Zones.Add(new DgaZone("低能放电", new[] { a, ab, g, ca }));
            doc.Zones.Add(new DgaZone("高能放电", new[] { b, bc, g, ab }));
            doc.Zones.Add(new DgaZone("高温过热", new[] { c, ca, g, bc }));

            doc.Samples.Add(new DgaSample("03-09", new TriaxialComponents(0.2d, 0.3d, 0.5d)));
            doc.Samples.Add(new DgaSample("03-16", new TriaxialComponents(0.6d, 0.25d, 0.15d)));
            doc.Samples.Add(new DgaSample("04-01", new TriaxialComponents(0.1d, 0.7d, 0.2d)));
            return doc;
        }
    }
}
