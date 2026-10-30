using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Engineering.Dga;
using OODD.Charts.Engineering.WellLog;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>
    /// 工程域 2 张表单的演示样例与冒烟断言（T290）。
    /// <para>
    /// 两张表是测井成果图与变压器油色谱（大卫三角形）。级配曲线、起重特性曲线、
    /// 建筑沉降观测、隧道监控量测四张只是普通折线/曲线图，通用图表控件即可实现，
    /// 按库定位（通用控件做不到的刚性版面）剔除。所有数值均为<b>示例数据，非真实机型/标准值</b>，
    /// 只用于演示版面与数据落位，不得当作工程结论引用。
    /// </para>
    /// </summary>
    public static class EngineeringSamples
    {
        /// <summary>两张工程表单的演示样例；名称与命令行/文件名一一对应。</summary>
        public static IReadOnlyList<DemoSample> All { get; } = new List<DemoSample>
        {
            new DemoSample("well-log", "测井成果图",
                () => new Chart(BuildWellLog(), new WellLogChartTemplate(), new ChartStyle())),
            new DemoSample("dga", "变压器油色谱",
                () => new Chart(BuildDga(), new DgaChartTemplate(), new ChartStyle())),
        };

        // ================================================================ 样例数据


        /// <summary>
        /// 测井成果图：深度 0–1000 m，两页（每页 500 m）；自然伽马为 0–150 线性道，
        /// 深电阻率为 1–1000 对数道，另附岩性花纹道。示例数据，非真实机型/标准值。
        /// </summary>
        public static WellLogDocument BuildWellLog()
        {
            WellLogDocument doc = new WellLogDocument
            {
                WellId = "示例井 X-7",
                RunNumber = "2",
                Formation = "示例层位",
                ScaleCaption = "1:200",
                DepthTop = 0d,
                DepthBottom = 1000d,
                PageDepth = 500d,
                PageIndex = 0,
                FooterNote = "示例数据，非真实机型/标准值；曲线与岩性层段仅用于演示。",
            };

            WellLogTrack gr = new WellLogTrack("gr", "自然伽马 (API)", 0d, 150d, false);
            WellLogTrack res = new WellLogTrack("res", "深电阻率 (Ω·m)", 1d, 1000d, true);
            WellLogTrack lith = new WellLogTrack("lith", "岩性", 0d, 1d, false);
            for (int depth = 0; depth <= 1000; depth += 25)
            {
                gr.Samples.Add(new WellLogSample(depth,
                    42d + 28d * Math.Sin(depth / 90d) + 12d * Math.Cos(depth / 37d)));
                res.Samples.Add(new WellLogSample(depth, Math.Pow(10d, 0.5d + depth / 750d)));
            }

            lith.Intervals.Add(new WellLogInterval(0d, 260d, PatternKind.Diagonal, "砂岩"));
            lith.Intervals.Add(new WellLogInterval(260d, 620d, PatternKind.Dot, "泥岩"));
            lith.Intervals.Add(new WellLogInterval(620d, 1000d, PatternKind.Cross, "灰岩"));

            doc.Tracks.Add(gr);
            doc.Tracks.Add(res);
            doc.Tracks.Add(lith);
            return doc;
        }




        /// <summary>
        /// 变压器油色谱：H2/CH4/C2H6/C2H4/C2H2 五个气体组分的趋势轨 +
        /// 三个故障分区与 3 次取样投点。示例数据，非真实机型/标准值。
        /// </summary>
        public static DgaChartDocument BuildDga()
        {
            DgaChartDocument doc = new DgaChartDocument
            {
                TransformerId = "示例 1# 主变",
                OilBrand = "示例 25#",
                SampleDateText = "2026-05-18",
                FooterNote = "示例数据，非真实机型/标准值；气体浓度与分区顶点仅用于演示。",
            };

            double[] days = { 0d, 4d, 9d, 16d, 30d, 45d };
            string[] dates = { "03-02", "03-06", "03-11", "03-18", "04-01", "04-16" };
            string[] keys = { "h2", "ch4", "c2h6", "c2h4", "c2h2" };
            string[] labels = { "H2", "CH4", "C2H6", "C2H4", "C2H2" };
            double[][] values =
            {
                new[] { 2d, 3d, 5d, 7d, 10d, 13d },
                new[] { 8d, 11d, 15d, 21d, 27d, 32d },
                new[] { 1.2d, 1.6d, 2.1d, 2.8d, 3.4d, 4.0d },
                new[] { 0.8d, 1.1d, 1.5d, 2.2d, 2.9d, 3.5d },
                new[] { 0.1d, 0.2d, 0.4d, 0.7d, 1.1d, 1.5d },
            };
            for (int gas = 0; gas < keys.Length; gas++)
            {
                DgaGasSeries series = new DgaGasSeries(keys[gas], labels[gas], "μL/L");
                for (int i = 0; i < days.Length; i++)
                {
                    series.Samples.Add(new DgaGasSample(days[i], dates[i], values[gas][i]));
                }
                doc.GasSeries.Add(series);
            }

            TriaxialComponents a = new TriaxialComponents(1d, 0d, 0d);
            TriaxialComponents b = new TriaxialComponents(0d, 1d, 0d);
            TriaxialComponents c = new TriaxialComponents(0d, 0d, 1d);
            TriaxialComponents g = new TriaxialComponents(1d / 3d, 1d / 3d, 1d / 3d);
            TriaxialComponents ab = new TriaxialComponents(0.5d, 0.5d, 0d);
            TriaxialComponents bc = new TriaxialComponents(0d, 0.5d, 0.5d);
            TriaxialComponents ca = new TriaxialComponents(0.5d, 0d, 0.5d);
            doc.Zones.Add(new DgaZone("低能放电（示例）", new[] { a, ab, g, ca }));
            doc.Zones.Add(new DgaZone("高能放电（示例）", new[] { b, bc, g, ab }));
            doc.Zones.Add(new DgaZone("高温过热（示例）", new[] { c, ca, g, bc }));

            doc.Samples.Add(new DgaSample("03-11", new TriaxialComponents(0.25d, 0.35d, 0.40d)));
            doc.Samples.Add(new DgaSample("03-18", new TriaxialComponents(0.55d, 0.30d, 0.15d)));
            doc.Samples.Add(new DgaSample("04-16", new TriaxialComponents(0.15d, 0.65d, 0.20d)));
            return doc;
        }

        // ================================================================ 冒烟断言
        /// <summary>
        /// 按样例名做结构冒烟：关键落位 + 96dpi PNG 导出非空。返回失败条数；未知名称返回 0。
        /// 这里只验证"样例 + 版面"能对上，不替代各域单测。
        /// </summary>
        public static int Verify(string name, Chart chart)
        {
            switch (name)
            {
                case "well-log": return VerifyWellLog(name, chart);
                case "dga": return VerifyDga(name, chart);
                default: return 0;
            }
        }


        private static int VerifyWellLog(string name, Chart chart)
        {
            WellLogDocument doc = (WellLogDocument)chart.Document;
            WellLogLayout? layout = chart.Geometry.LayoutOf<WellLogLayout>();
            if (layout == null)
            {
                return Expect(name, false, "版面应为 WellLogLayout", "null");
            }

            int failures = 0;
            float yTop = layout.DepthAxis.PositionOf(0d);
            failures += Expect(name, Math.Abs(yTop - layout.PlotRect.Top) < 1e-3f,
                "深度 0 落在页顶 " + Fmt(layout.PlotRect.Top), Fmt(yTop) + "px");

            float yShallow = layout.DepthAxis.PositionOf(0d);
            float yDeep = layout.DepthAxis.PositionOf(100d);
            failures += Expect(name, yDeep > yShallow,
                "深度向下递增：100 m 在 0 m 之下", Fmt(yDeep) + "px > " + Fmt(yShallow) + "px");

            bool scalesDiffer = layout.Stack["gr"].ValueAxis.Scale == AxisScale.Linear
                && layout.Stack["res"].ValueAxis.Scale == AxisScale.Logarithmic;
            failures += Expect(name, scalesDiffer && layout.Headers.Count == doc.Tracks.Count,
                "两条不同量程道（gr 线性 / res 对数）且道头数等于道数 " + doc.Tracks.Count,
                layout.Stack["gr"].ValueAxis.Scale + " / " + layout.Stack["res"].ValueAxis.Scale
                    + "，道头 " + layout.Headers.Count);

            failures += VerifyExport(name, chart);
            return failures;
        }




        private static int VerifyDga(string name, Chart chart)
        {
            DgaChartDocument doc = (DgaChartDocument)chart.Document;
            DgaLayout? layout = chart.Geometry.LayoutOf<DgaLayout>();
            if (layout == null)
            {
                return Expect(name, false, "版面应为 DgaLayout", "null");
            }

            int failures = 0;
            TriaxialMap map = layout.Triangle;
            double vertexDelta = Math.Max(
                Distance(map.PointOf(1d, 0d, 0d), map.VertexA),
                Math.Max(Distance(map.PointOf(0d, 1d, 0d), map.VertexB),
                         Distance(map.PointOf(0d, 0d, 1d), map.VertexC)));
            failures += Expect(name, vertexDelta < 1e-3d,
                "组分 A/B/C 三个单位顶点落在三角顶点，最大偏差 < 0.001px", Fmt(vertexDelta) + "px");

            failures += Expect(name, layout.SamplePoints.Count == doc.Samples.Count
                    && layout.ZonePolygons.Count == doc.Zones.Count,
                "投点数与分区数等于文档 " + doc.Samples.Count + " / " + doc.Zones.Count,
                layout.SamplePoints.Count + " / " + layout.ZonePolygons.Count);

            failures += Expect(name, doc.GasSeries.Count == 5,
                "五条气体趋势轨（H2/CH4/C2H6/C2H4/C2H2）", doc.GasSeries.Count.ToString(CultureInfo.InvariantCulture));

            failures += VerifyExport(name, chart);
            return failures;
        }

        private static int VerifyExport(string name, Chart chart)
        {
            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            return Expect(name, png.Length > 5000, "96dpi PNG 导出字节数 > 5000", png.Length + " B");
        }

        // ================================================================ 工具

        private static int Expect(string name, bool ok, string what, string actual)
        {
            Console.WriteLine("[" + name + "] " + (ok ? "通过" : "失败") + ": " + what + " 实到 " + actual);
            return ok ? 0 : 1;
        }

        private static string Fmt(float value)
        {
            return double.IsNaN(value) ? "NaN" : value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Fmt(double value)
        {
            return double.IsNaN(value) ? "NaN" : value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static double Distance(SKPoint a, SKPoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
