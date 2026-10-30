using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geoscience.Surface;
using OODD.Charts.Geoscience.Tlnp;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>
    /// 地学域 2 张表单的演示样例与结构断言（docs/07 T290）。
    /// <para>
    /// 两张表是地面天气图与 T-lnP 温度对数压力图。洪水频率、水位流量定线图与
    /// 土壤质地三角图只是普通曲线/三元图，通用图表控件加预变换即可实现，按库定位剔除。
    /// 全部数值均为<b>示例数据，非真实机型/标准值</b>：站点与探空廓线都是为了让版面要素
    /// 齐全而编造的演示值，不对应任何真实测站或探空记录。
    /// </para>
    /// <para>
    /// <see cref="Verify"/> 只读版面对象（<c>Chart.Geometry.LayoutOf&lt;T&gt;()</c>）与
    /// 96dpi PNG 字节数，绘制与断言同源；未知样例名返回 0，便于样例工具按名分发。
    /// </para>
    /// </summary>
    public static class GeoscienceSamples
    {
        /// <summary>2 张地学表单的样例登记表（名称逐字对齐模板键后缀）。</summary>
        public static IReadOnlyList<DemoSample> All { get; } = new List<DemoSample>
        {
            new DemoSample("surface-chart", "地面天气图填图",
                () => new Chart(BuildSurface(), new SurfaceChartTemplate(), new ChartStyle())),
            new DemoSample("tlnp", "T-lnP 温度对数压力图",
                () => new Chart(BuildSounding(), new TlnpTemplate(), new ChartStyle())),
        };

        // ================================================================== 样例数据

        /// <summary>
        /// 地面天气图：7 个站，覆盖静风、偏北/偏东/偏南/偏西风、不同风速与云量，
        /// 温度露点成对给出。<b>示例数据，非真实机型/标准值</b>。
        /// </summary>
        private static SurfacePlotDocument BuildSurface()
        {
            SurfacePlotDocument document = new SurfacePlotDocument
            {
                FormTitle = "地面天气图填图",
                ValidTimeText = "2026-03-01 08 时（示例数据，非真实机型/标准值）",
            };

            SurfaceLevelPlot level = document.Levels[0];
            level.Stations.Add(new SurfaceStation("哈尔滨 50953", 0.16d, 0.20d)
            {
                TemperatureC = -13.6d,
                DewPointC = -19.2d,
                PressureHpa = 1028.6d,
                PressureTendencyHpa = 1.8d,
                WindDirectionDegrees = 0d,
                WindSpeedMps = 9d,
                CloudOktas = 7,
                WeatherCode = "SN",
            });
            level.Stations.Add(new SurfaceStation("乌鲁木齐 51463", 0.06d, 0.42d)
            {
                TemperatureC = -6.4d,
                DewPointC = -15.1d,
                PressureHpa = 1023.2d,
                PressureTendencyHpa = 2.1d,
                WindDirectionDegrees = 315d,
                WindSpeedMps = 14d,
                CloudOktas = 2,
            });
            level.Stations.Add(new SurfaceStation("北京 54511", 0.40d, 0.32d)
            {
                TemperatureC = 1.8d,
                DewPointC = -7.4d,
                PressureHpa = 1016.8d,
                PressureTendencyHpa = -0.7d,
                WindDirectionDegrees = 45d,
                WindSpeedMps = 12d,
                CloudOktas = 4,
            });
            level.Stations.Add(new SurfaceStation("成都 56294", 0.24d, 0.62d)
            {
                TemperatureC = 11.6d,
                DewPointC = 9.8d,
                PressureHpa = 1009.4d,
                PressureTendencyHpa = 0.2d,
                WindDirectionDegrees = 0d,
                WindSpeedMps = 0d,
                CloudOktas = 8,
                WeatherCode = "BR",
            });
            level.Stations.Add(new SurfaceStation("武汉 57494", 0.52d, 0.58d)
            {
                TemperatureC = 16.9d,
                DewPointC = 13.2d,
                PressureHpa = 1004.1d,
                PressureTendencyHpa = -0.4d,
                WindDirectionDegrees = 210d,
                WindSpeedMps = 7d,
                CloudOktas = 6,
                WeatherCode = "RA",
            });
            level.Stations.Add(new SurfaceStation("上海 58362", 0.74d, 0.50d)
            {
                TemperatureC = 15.2d,
                DewPointC = 11.6d,
                PressureHpa = 1007.0d,
                PressureTendencyHpa = -1.2d,
                WindDirectionDegrees = 135d,
                WindSpeedMps = 18d,
                CloudOktas = 8,
                WeatherCode = "RA",
            });
            level.Stations.Add(new SurfaceStation("广州 59287", 0.84d, 0.80d)
            {
                TemperatureC = 25.3d,
                DewPointC = 22.6d,
                PressureHpa = 1001.6d,
                PressureTendencyHpa = -0.9d,
                WindDirectionDegrees = 160d,
                WindSpeedMps = 11d,
                CloudOktas = 7,
                WeatherCode = "TSRA",
            });

            return document;
        }

        /// <summary>
        /// T-lnP：一条 11 层探空廓线，含 T 与 td 两条曲线、4 个特征层与一层 T—td 填图。
        /// <b>示例数据，非真实机型/标准值</b>。
        /// </summary>
        private static SoundingDocument BuildSounding()
        {
            SoundingDocument document = new SoundingDocument
            {
                StationName = "示例探空站 54511（示例数据）",
                ValidTimeText = "2026-03-01 08 时（示例）",
                HatchedLevelIndex = 2,
                FooterNote = "示例数据，非真实机型/标准值；底图倾斜量与线族密度为可调令牌，未按 QX/T 123 底图校准",
            };

            document.Levels.Add(new SoundingLevel(1000d, 22.4d, 15.1d));
            document.Levels.Add(new SoundingLevel(925d, 17.6d, 12.0d));
            document.Levels.Add(new SoundingLevel(850d, 11.8d, 6.4d) { Note = "湿层顶" });
            document.Levels.Add(new SoundingLevel(700d, 3.1d, -4.2d));
            document.Levels.Add(new SoundingLevel(500d, -9.8d, -21.0d));
            document.Levels.Add(new SoundingLevel(400d, -21.6d, -33.2d));
            document.Levels.Add(new SoundingLevel(300d, -39.8d, -51.6d));
            document.Levels.Add(new SoundingLevel(250d, -51.2d, -61.0d));
            document.Levels.Add(new SoundingLevel(200d, -56.4d, -66.8d));
            document.Levels.Add(new SoundingLevel(150d, -62.3d, -73.5d));
            document.Levels.Add(new SoundingLevel(100d, -68.0d, -79.0d));

            document.CharacteristicLevels.Add(new SoundingCharacteristicLevel(925d, "逆温层顶") { PreferLeft = true });
            document.CharacteristicLevels.Add(new SoundingCharacteristicLevel(850d, "湿层顶") { PreferLeft = false });
            document.CharacteristicLevels.Add(new SoundingCharacteristicLevel(600d, "0℃ 层") { PreferLeft = true });
            document.CharacteristicLevels.Add(new SoundingCharacteristicLevel(200d, "对流层顶") { PreferLeft = false });

            return document;
        }




        // ================================================================== 断言

        /// <summary>
        /// 按样例名执行结构断言，返回失败条数；未知名称返回 0。
        /// 每条断言打印 <c>[name] 通过/失败: 说明，实到 值</c>。
        /// </summary>
        public static int Verify(string name, Chart chart)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            switch (name)
            {
                case "surface-chart":
                    return VerifySurfaceChart(chart);
                case "tlnp":
                    return VerifyTlnp(chart);
                default:
                    return 0;
            }
        }

        private static int VerifySurfaceChart(Chart chart)
        {
            const string name = "surface-chart";
            SurfacePlotDocument? document = chart.Document as SurfacePlotDocument;
            SurfaceChartTemplate? template = chart.Template as SurfaceChartTemplate;
            SurfaceChartLayout? layout = chart.Geometry.LayoutOf<SurfaceChartLayout>();
            if (document == null || template == null || layout == null || layout.Panels.Count == 0)
            {
                return Fail(name, "版面/文档类型", "SurfacePlotDocument + SurfaceChartLayout",
                    layout == null ? "版面 null" : "面板 " + layout.Panels.Count);
            }

            int failures = 0;
            SurfaceLevelPlot level = document.Levels[0];
            int stationCount = level.Stations.Count;
            failures += Assert(name, "站数在 6–8 之间", stationCount >= 6 && stationCount <= 8,
                stationCount.ToString(CultureInfo.InvariantCulture) + " 站");

            SKPoint north = WindBarb.DirectionVectorOf(0f);
            SKPoint east = WindBarb.DirectionVectorOf(90f);
            bool windOk = Math.Abs(north.X) < 1e-5f && Math.Abs(north.Y + 1f) < 1e-5f
                          && Math.Abs(east.X - 1f) < 1e-5f && Math.Abs(east.Y) < 1e-5f;
            failures += Assert(name, "风向 0° 朝上、90° 朝右", windOk,
                "0°=(" + F(north.X) + "," + F(north.Y) + ") 90°=(" + F(east.X) + "," + F(east.Y) + ")");

            SurfacePanel panel = layout.Panels[0];
            int inside = 0;
            int quadrantOk = 0;
            foreach (SurfaceStation station in level.Stations)
            {
                SKPoint center = layout.CenterOf(panel, station);
                if (center.X >= panel.Content.Left - 0.01f && center.X <= panel.Content.Right + 0.01f
                    && center.Y >= panel.Content.Top - 0.01f && center.Y <= panel.Content.Bottom + 0.01f)
                {
                    inside++;
                }

                SurfaceElementBoxes boxes = SurfaceChartTemplate.ElementBoxesOf(
                    station, center, template.Tokens.StationSize, chart.Fonts, chart.Style.Text.Family,
                    template.Tokens);
                bool ok = boxes.Temperature.Right <= center.X + 0.01f
                          && boxes.Temperature.Bottom <= center.Y + 0.01f
                          && boxes.DewPoint.Left >= center.X - 0.01f
                          && boxes.DewPoint.Bottom <= center.Y + 0.01f
                          && boxes.Pressure.Left >= center.X - 0.01f
                          && boxes.Pressure.Top >= center.Y - 0.01f
                          && boxes.WindBarb.Right <= center.X + 0.01f
                          && boxes.WindBarb.Top >= center.Y - 0.01f;
                if (ok)
                {
                    quadrantOk++;
                }
            }

            failures += Assert(name, "站点全部落在层面板内容区内", inside == stationCount,
                inside.ToString(CultureInfo.InvariantCulture) + "/"
                + stationCount.ToString(CultureInfo.InvariantCulture) + " 站");
            failures += Assert(name, "气温左上、露点右上、海压右下、风杆左下", quadrantOk == stationCount,
                quadrantOk.ToString(CultureInfo.InvariantCulture) + "/"
                + stationCount.ToString(CultureInfo.InvariantCulture) + " 站四象限正确");

            failures += AssertPng(name, chart);
            return failures;
        }

        private static int VerifyTlnp(Chart chart)
        {
            const string name = "tlnp";
            SoundingDocument? document = chart.Document as SoundingDocument;
            TlnpLayout? layout = chart.Geometry.LayoutOf<TlnpLayout>();
            if (document == null || layout == null)
            {
                return Fail(name, "版面/文档类型", "SoundingDocument + TlnpLayout",
                    layout == null ? "版面 null" : "文档类型不符");
            }

            int failures = 0;
            double gap1000To500 = Math.Abs(layout.YOf(1000d) - layout.YOf(500d));
            double gap500To250 = Math.Abs(layout.YOf(500d) - layout.YOf(250d));
            failures += Assert(name, "等压线在对数气压轴上等距（1000→500 = 500→250）",
                Math.Abs(gap1000To500 - gap500To250) < 0.01d,
                F(gap1000To500) + " px vs " + F(gap500To250) + " px");

            int levelCount = document.Levels.Count;
            failures += Assert(name, "T 与 td 两条曲线点数等于层次数",
                layout.TemperatureCurve.Count == levelCount && layout.DewPointCurve.Count == levelCount,
                "T " + layout.TemperatureCurve.Count.ToString(CultureInfo.InvariantCulture)
                + " 点 / td " + layout.DewPointCurve.Count.ToString(CultureInfo.InvariantCulture)
                + " 点 / 层次 " + levelCount.ToString(CultureInfo.InvariantCulture));

            double minSeparation = double.MaxValue;
            for (int i = 0; i < layout.TemperatureCurve.Count && i < layout.DewPointCurve.Count; i++)
            {
                double separation = (double)layout.TemperatureCurve[i].X - layout.DewPointCurve[i].X;
                if (separation < minSeparation)
                {
                    minSeparation = separation;
                }
            }
            failures += Assert(name, "每层温度曲线在露点曲线右侧", minSeparation > 0d,
                "最小间距 " + F(minSeparation) + " px");

            int labelsInside = 0;
            foreach (TlnpCharacteristicLabel label in layout.CharacteristicLabels)
            {
                SKRect box = label.Box;
                if (box.Left >= layout.PlotRect.Left - 0.01f && box.Right <= layout.PlotRect.Right + 0.01f
                    && box.Top >= layout.PlotRect.Top - 0.01f && box.Bottom <= layout.PlotRect.Bottom + 0.01f)
                {
                    labelsInside++;
                }
            }
            failures += Assert(name, "特征层标签数正确且全部落在绘图区内",
                layout.CharacteristicLabels.Count == document.CharacteristicLevels.Count
                && labelsInside == document.CharacteristicLevels.Count,
                labelsInside.ToString(CultureInfo.InvariantCulture) + "/"
                + document.CharacteristicLevels.Count.ToString(CultureInfo.InvariantCulture) + " 个标签在区内");

            failures += AssertPng(name, chart);
            return failures;
        }




        // ================================================================== 工具

        private static int AssertPng(string name, Chart chart)
        {
            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            return Assert(name, "96dpi PNG 导出非空（> 5000 B）", png.Length > 5000,
                png.Length.ToString(CultureInfo.InvariantCulture) + " B");
        }

        private static int Fail(string name, string what, string expected, string actual)
        {
            return Assert(name, what, false, actual + "（期望 " + expected + "）");
        }

        private static int Assert(string name, string what, bool ok, string actual)
        {
            Console.WriteLine("[" + name + "] " + (ok ? "通过" : "失败") + ": " + what + "，实到 " + actual);
            return ok ? 0 : 1;
        }

        private static double Distance(SKPoint a, SKPoint b)
        {
            double dx = (double)a.X - b.X;
            double dy = (double)a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static string F(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
