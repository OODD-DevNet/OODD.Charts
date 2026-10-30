using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OODD.Charts.Demo;
using OODD.Charts.Radar;
using OODD.Charts.Style;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 雷达域验收：极坐标约定（0° 正北）、量程映射、360°/180° 盘面、
    /// 右上角南北向罗盘、扫描线开关与角度、轨迹保留点数、选中高亮、
    /// 手动配置的信息面板，以及位图/矢量导出。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class RadarTests
    {
        // ------------------------------------------------------------ 几何约定

        [Fact]
        public void 雷达_方位0在正上方方位90在正右方且量程映射到半径()
        {
            RadarDocument doc = FullDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);

            SKPoint north = layout.PointOf(0d, doc.MaxRange);
            SKPoint east = layout.PointOf(90d, doc.MaxRange);
            SKPoint south = layout.PointOf(180d, doc.MaxRange);
            SKPoint west = layout.PointOf(270d, doc.MaxRange);

            Assert.Equal(layout.Center.X, north.X, 3);
            Assert.Equal(layout.Center.Y - layout.Radius, north.Y, 3);
            Assert.Equal(layout.Center.X + layout.Radius, east.X, 3);
            Assert.Equal(layout.Center.Y, east.Y, 3);
            Assert.Equal(layout.Center.Y + layout.Radius, south.Y, 3);
            Assert.Equal(layout.Center.X - layout.Radius, west.X, 3);

            Assert.Equal(layout.Radius / 2f, layout.RadiusOf(doc.MaxRange / 2d), 3);
            Assert.Equal(layout.Radius, layout.RadiusOf(doc.MaxRange * 2d), 3);
            Assert.Equal(doc.MaxRange / 2d, layout.DistanceOf(layout.PointOf(37d, doc.MaxRange / 2d)), 3);

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                Assert.Equal(layout.Radius, chart.Geometry.GridRect.Width / 2f, 3);
                Assert.Equal(layout.Radius, chart.Geometry.GridRect.Height / 2f, 3);
            }
        }

        [Fact]
        public void 雷达_180度扇区只覆盖上半盘且盘面下方是底色()
        {
            RadarDocument doc = FullDocument();
            doc.Scope = RadarScope.Half;
            doc.HalfStartBearing = 270d;
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);

            Assert.Equal(layout.Radius, layout.ScopeRect.Height, 3);
            Assert.True(layout.Contains(layout.PointOf(0d, 10d)), "正北不在扇区内");
            Assert.True(layout.Contains(layout.PointOf(88d, 10d)), "接近东端的方位不在扇区内");
            Assert.False(layout.Contains(layout.PointOf(180d, 10d)), "正南被误判在扇区内");
            Assert.False(layout.Contains(layout.PointOf(92d, 10d)), "扇区外的东偏南方向被误判");

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                SKColor below = probe.Pixel(
                    (int)Math.Round(layout.Center.X + layout.Radius * 0.4f),
                    (int)Math.Round(layout.Center.Y + 4f));
                AssertColorNear(template.Palette.PageBackground, below, 2, "半圆雷达直径下方不应有盘面填充");

                SKPoint inside = layout.PointOf(20d, doc.MaxRange * 0.55d);
                AssertColorNear(template.Palette.ScopeFill, probe.Pixel((int)inside.X, (int)inside.Y), 3,
                    "半圆雷达扇区内没有盘面填充");
            }
        }

        [Fact]
        public void 雷达_点击命中容差内选中最近目标()
        {
            RadarDocument doc = FullDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            RadarTarget target = doc.Targets[0];

            SKPoint at = layout.PointOf(target.Bearing, target.Distance);
            Assert.Same(target, layout.HitTest(doc, new SKPoint(at.X + 5f, at.Y - 4f), 12f));
            Assert.Null(layout.HitTest(doc, new SKPoint(at.X + 60f, at.Y + 60f), 12f));
        }

        // ------------------------------------------------------------ 罗盘 / 扫描

        [Fact]
        public void 雷达_右上角画南北向罗盘且可关闭()
        {
            RadarDocument doc = FullDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            int cx = (int)layout.CompassCenter.X;
            int cy = (int)layout.CompassCenter.Y;

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(probe.HasRedInBox(cx - 24, cy - 24, cx + 24, cy + 24),
                    "右上角罗盘没有指北红针");
                Assert.False(probe.HasRedInBox(20, 20, cx - 80, cy + 24),
                    "罗盘之外出现了红色墨");
                Assert.True(cx > (int)(layout.Bounds.Width * 0.8f) && cy < (int)(layout.Bounds.Height * 0.2f),
                    "罗盘不在右上角");
            }

            doc.ShowNorthIndicator = false;
            using (Chart chart = new Chart(doc, new RadarChartTemplate(), new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.False(probe.HasRedInBox(cx - 24, cy - 24, cx + 24, cy + 24),
                    "关闭后仍画了罗盘");
            }
        }

        [Fact]
        public void 雷达_扫描线可开关且角度可配()
        {
            RadarDocument doc = EmptyDocument();
            doc.ShowSweep = true;
            doc.SweepAngle = 60d;
            doc.SweepBeamWidth = 30d;
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);

            SKRect box60 = BoxAround(layout.PointOf(60d, doc.MaxRange * 0.6d), 20f, 20f);
            SKRect box210 = BoxAround(layout.PointOf(210d, doc.MaxRange * 0.6d), 20f, 20f);

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(CountSweep(probe, box60) > 25, "扫描线 060° 处没有亮扫");
                Assert.True(CountSweep(probe, box210) < 5, "060° 之外的方位也出现了亮扫");
            }

            doc.SweepAngle = 210d;
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(CountSweep(probe, box210) > 25, "改到 210° 后没有亮扫");
                Assert.True(CountSweep(probe, box60) < 5, "改到 210° 后 060° 处仍有亮扫");
            }

            doc.ShowSweep = false;
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(CountSweep(probe, box60) < 5, "关闭扫描线后 060° 处仍有亮扫");
                Assert.True(CountSweep(probe, box210) < 5, "关闭扫描线后 210° 处仍有亮扫");
            }
        }

        // ------------------------------------------------------------ 轨迹 / 选中

        [Fact]
        public void 雷达_轨迹只保留配置的点数()
        {
            RadarTarget target = new RadarTarget("T") { Bearing = 90d, Distance = 5d };
            for (int i = 0; i < 9; i++)
            {
                target.Track.Add(new RadarTrackPoint(i * 10d, i + 1d));
            }

            IReadOnlyList<RadarTrackPoint> four = RadarChartTemplate.RetainedTrack(target, 4);
            Assert.Equal(4, four.Count);
            Assert.Equal(50d, four[0].Bearing, 3);
            Assert.Equal(80d, four[3].Bearing, 3);
            Assert.Empty(RadarChartTemplate.RetainedTrack(target, 0));
            Assert.Equal(9, RadarChartTemplate.RetainedTrack(target, 99).Count);

            target.MoveTo(120d, 6d);
            Assert.Equal(10, target.Track.Count);
            Assert.Equal(90d, target.Track[9].Bearing, 3);
            Assert.Equal(120d, target.Bearing, 3);
        }

        [Fact]
        public void 雷达_轨迹线可开关且老点可见()
        {
            RadarDocument doc = FullDocument();
            doc.Targets.Clear();
            doc.SelectedTargetId = null;
            doc.ShowTrails = true;
            doc.TrailLength = 6;
            doc.RingCount = 5;

            RadarTarget target = new RadarTarget("T") { Label = "轨迹目标", Bearing = 90d, Distance = 20d };
            double[] bearings = { 130d, 122d, 114d, 106d, 98d, 90d };
            foreach (double bearing in bearings)
            {
                target.Track.Add(new RadarTrackPoint(bearing, 20d));
            }
            doc.Targets.Add(target);

            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            SKRect oldBox = BoxAround(layout.PointOf(110d, 20d), 7f, 7f);

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(CountTrail(probe, oldBox) > 5, "轨迹开启时老位置没有轨迹线");
            }

            doc.ShowTrails = false;
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(CountTrail(probe, oldBox) < 2, "轨迹关闭后仍画了老位置");
            }
        }

        [Fact]
        public void 雷达_选中目标画高亮圈且可切换()
        {
            RadarDocument doc = FullDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            RadarTarget selected = doc.Targets[0];
            SKRect box = BoxAround(layout.PointOf(selected.Bearing, selected.Distance), 15f, 15f);

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.True(probe.HasRedInBox((int)box.Left, (int)box.Top, (int)box.Right, (int)box.Bottom),
                    "选中目标没有高亮圈");
            }

            doc.SelectedTargetId = null;
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.False(probe.HasRedInBox((int)box.Left, (int)box.Top, (int)box.Right, (int)box.Bottom),
                    "取消选中后仍有高亮圈");
            }
        }

        // ------------------------------------------------------------ 信息面板

        [Fact]
        public void 雷达_信息面板字段由用户手动配置()
        {
            RadarDocument doc = new RadarDocument { MaxRange = 40d, RangeUnit = "nmi", SpeedUnit = "kn" };
            RadarTarget target = new RadarTarget("T9")
            {
                Label = "目标甲",
                Bearing = -15d,
                Distance = 12.34d,
                Speed = 9d,
                Course = 405d,
            };
            target.Extra["type"] = "快艇";
            doc.Targets.Add(target);

            RadarInfoPanel panel = new RadarInfoPanel();
            panel.Fields.Clear();
            panel.Fields.Add(new RadarInfoField("id"));
            panel.Fields.Add(new RadarInfoField("label", "船名"));
            panel.Fields.Add(new RadarInfoField("bearing"));
            panel.Fields.Add(new RadarInfoField("distance", null, "0"));
            panel.Fields.Add(new RadarInfoField("speed"));
            panel.Fields.Add(new RadarInfoField("course"));
            panel.Fields.Add(new RadarInfoField("status"));
            panel.Fields.Add(new RadarInfoField("extra.type", "类型"));
            panel.Fields.Add(new RadarInfoField("extra.missing"));
            doc.InfoPanel = panel;

            IReadOnlyList<KeyValuePair<string, string>> pairs = panel.Resolve(target, doc);
            Assert.Equal(8, pairs.Count);
            Assert.Equal("编号", pairs[0].Key);
            Assert.Equal("T9", pairs[0].Value);
            Assert.Equal("船名", pairs[1].Key);
            Assert.Equal("目标甲", pairs[1].Value);
            Assert.Equal("345°", pairs[2].Value);
            Assert.Equal("12 nmi", pairs[3].Value);
            Assert.Equal("9.0 kn", pairs[4].Value);
            Assert.Equal("045°", pairs[5].Value);
            Assert.Equal("跟踪中", pairs[6].Value);
            Assert.Equal("类型", pairs[7].Key);
            Assert.Equal("快艇", pairs[7].Value);

            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            Assert.True(layout.HasPanel);

            SKPoint panelProbe = new SKPoint(layout.PanelRect.Left + 5f, layout.PanelRect.Bottom - 5f);
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                AssertColorNear(template.Palette.PanelFill,
                    probe.Pixel((int)panelProbe.X, (int)panelProbe.Y), 3, "信息面板没有画出来");
            }

            panel.Show = false;
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                AssertColorNear(template.Palette.PageBackground,
                    probe.Pixel((int)panelProbe.X, (int)panelProbe.Y), 3, "关闭信息面板后仍留有面板底色");
            }
        }

        // ------------------------------------------------------------ 导出

        [Fact]
        public void 雷达_两种盘面都能导出位图与矢量()
        {
            foreach (RadarDocument doc in new[] { FullDocument(), RadarSample.BuildHalf() })
            {
                using (Chart chart = new Chart(doc, new RadarChartTemplate(), new ChartStyle()))
                {
                    SKSize first = chart.MeasureSize();
                    Assert.Equal(first.Width, chart.MeasureSize().Width, 3);
                    Assert.Equal(first.Height, chart.MeasureSize().Height, 3);

                    byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                    Assert.True(png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50,
                        "雷达 PNG 产物过小或魔数不对");

                    byte[] pdf;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        chart.WritePdf(stream);
                        pdf = stream.ToArray();
                    }
                    Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf, 0, 5));

                    string svg = ChartExport.ToSvgString(chart);
                    Assert.True(svg.Contains("<path") && svg.Contains("<text"), "雷达 SVG 缺矢量线条或文字");
                    Assert.False(svg.Contains("<image"), "雷达 SVG 内嵌了位图");
                }
            }
        }

        // ------------------------------------------------------------ 文档

        private static RadarDocument FullDocument()
        {
            RadarDocument doc = new RadarDocument
            {
                FormTitle = "测试雷达",
                Scope = RadarScope.Full,
                MaxRange = 40d,
                RangeUnit = "km",
                RingCount = 4,
                ShowSweep = false,
                ShowTrails = false,
                ShowNorthIndicator = true,
                SelectedTargetId = "A",
            };

            doc.Targets.Add(new RadarTarget("A") { Label = "甲", Bearing = 60d, Distance = 20d });
            doc.Targets.Add(new RadarTarget("B") { Label = "乙", Bearing = 210d, Distance = 10d });
            return doc;
        }

        private static RadarDocument EmptyDocument()
        {
            RadarDocument doc = FullDocument();
            doc.Targets.Clear();
            doc.SelectedTargetId = null;
            return doc;
        }

        // ------------------------------------------------------------ 像素工具

        private static SKRect BoxAround(SKPoint point, float halfWidth, float halfHeight)
        {
            return new SKRect(point.X - halfWidth, point.Y - halfHeight,
                point.X + halfWidth, point.Y + halfHeight);
        }

        private static int CountSweep(ImageProbe probe, SKRect rect)
        {
            return CountPixels(probe, rect,
                c => c.Green >= 200 && c.Green - c.Red >= 80 && c.Green - c.Blue >= 50);
        }

        private static int CountTrail(ImageProbe probe, SKRect rect)
        {
            return CountPixels(probe, rect,
                c => c.Green >= 70 && c.Green - c.Red >= 30 && c.Green - c.Blue >= 20);
        }

        private static int CountPixels(ImageProbe probe, SKRect rect, Func<SKColor, bool> match)
        {
            int left = Math.Max(0, (int)Math.Floor(rect.Left));
            int top = Math.Max(0, (int)Math.Floor(rect.Top));
            int right = Math.Min(probe.Width, (int)Math.Ceiling(rect.Right));
            int bottom = Math.Min(probe.Height, (int)Math.Ceiling(rect.Bottom));

            int count = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (match(probe.Pixel(x, y)))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static void AssertColorNear(SKColor expected, SKColor actual, int tolerance, string message)
        {
            Assert.True(
                Math.Abs(expected.Red - actual.Red) <= tolerance
                && Math.Abs(expected.Green - actual.Green) <= tolerance
                && Math.Abs(expected.Blue - actual.Blue) <= tolerance,
                message + "：期望 " + expected.Red + "," + expected.Green + "," + expected.Blue
                + "，实际 " + actual.Red + "," + actual.Green + "," + actual.Blue);
        }
    }
}
