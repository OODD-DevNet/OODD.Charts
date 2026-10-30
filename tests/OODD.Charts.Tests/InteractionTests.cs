using System;
using System.Linq;
using System.Threading;
using OODD.Charts.Demo;
using OODD.Charts.Interaction;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;
using OODD.Charts.Style;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 交互与动画验收：视口换算（贴合居中 / 原尺寸 DPI）、点击选中、悬停高亮、
    /// 扫描线按耗时推进并归一、半圆盘不命中扇区外目标、非雷达表单安全返回 null，
    /// 以及 <see cref="ChartInteractionHost"/> 的挂载与指针转发。
    /// </summary>
    public sealed class InteractionTests
    {
        // ------------------------------------------------------------ 视口

        [Fact]
        public void 视口_贴合模式等比居中且坐标可往返()
        {
            ChartViewport viewport = ChartViewport.Create(
                new SKSize(200f, 100f), 400f, 400f, ChartFit.Fit, 1d, 96d);

            Assert.False(viewport.IsEmpty);
            Assert.Equal(2f, viewport.Scale, 3);
            Assert.Equal(0f, viewport.OffsetX, 3);
            Assert.Equal(100f, viewport.OffsetY, 3);

            SKPoint chart = viewport.ToChart(new SKPoint(100f, 200f));
            Assert.Equal(50f, chart.X, 3);
            Assert.Equal(50f, chart.Y, 3);

            SKPoint view = viewport.ToView(chart);
            Assert.Equal(100f, view.X, 3);
            Assert.Equal(200f, view.Y, 3);

            Assert.True(viewport.ContainsChart(new SKPoint(199f, 99f)));
            Assert.False(viewport.ContainsChart(new SKPoint(201f, 50f)));
            Assert.False(viewport.ContainsChart(new SKPoint(50f, -1f)));
        }

        [Fact]
        public void 视口_原尺寸按DPI与缩放系数从原点起画()
        {
            ChartViewport viewport = ChartViewport.Create(
                new SKSize(100f, 80f), 500f, 500f, ChartFit.Actual, 2d, 144d);

            Assert.Equal(3f, viewport.Scale, 3);
            Assert.Equal(0f, viewport.OffsetX, 3);
            Assert.Equal(0f, viewport.OffsetY, 3);

            SKPoint chart = viewport.ToChart(new SKPoint(300f, 300f));
            Assert.Equal(100f, chart.X, 3);
            Assert.Equal(100f, chart.Y, 3);
        }

        [Fact]
        public void 视口_无效输入退化为空视口且不换算()
        {
            Assert.True(ChartViewport.Empty.IsEmpty);
            Assert.True(ChartViewport.Create(new SKSize(0f, 10f), 100f, 100f, ChartFit.Fit).IsEmpty);
            Assert.True(ChartViewport.Create(new SKSize(10f, 10f), 0f, 100f, ChartFit.Fit).IsEmpty);
            Assert.True(ChartViewport.FromPlacement(new SKSize(10f, 10f), 0f, 0f, 0f).IsEmpty);

            SKPoint point = new SKPoint(12f, 34f);
            SKPoint unchanged = ChartViewport.Empty.ToChart(point);
            Assert.Equal(point.X, unchanged.X, 3);
            Assert.Equal(point.Y, unchanged.Y, 3);
        }

        // ------------------------------------------------------------ 点击 / 悬停

        [Fact]
        public void 雷达交互_点击目标选中点盘内空白取消点留白不响应()
        {
            RadarDocument doc = TwoTargetDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                RadarInteraction interaction = new RadarInteraction();
                Assert.True(interaction.CanAttach(chart));
                interaction.Attach(chart);

                RadarLayout layout = template.Layout(doc);
                RadarTarget target = doc.Targets[1];
                SKPoint at = layout.PointOf(target.Bearing, target.Distance);

                interaction.PointerDown(at);
                Assert.Equal(target.Id, doc.SelectedTargetId);
                Assert.Same(target, interaction.SelectedTarget);

                // 盘面内空白：取消选中。
                interaction.PointerDown(new SKPoint(layout.Center.X + 2f, layout.Center.Y + 2f));
                Assert.Null(doc.SelectedTargetId);

                // 版面外留白：不响应，保留当前选中。
                interaction.Select(target.Id);
                interaction.PointerDown(new SKPoint(-40f, -40f));
                Assert.Equal(target.Id, doc.SelectedTargetId);
            }
        }

        [Fact]
        public void 雷达交互_悬停写回编号离开时清除()
        {
            RadarDocument doc = TwoTargetDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                RadarInteraction interaction = new RadarInteraction();
                interaction.Attach(chart);
                RadarLayout layout = template.Layout(doc);
                RadarTarget target = doc.Targets[0];
                SKPoint at = layout.PointOf(target.Bearing, target.Distance);

                int hoverChanges = 0;
                interaction.HoverChanged += (sender, args) => hoverChanges++;

                interaction.PointerMove(at);
                Assert.Equal(target.Id, doc.HoverTargetId);
                Assert.Same(target, interaction.HoveredTarget);
                Assert.True(interaction.IsPointerOverInteractive);
                Assert.Equal(1, hoverChanges);

                interaction.PointerMove(at);
                Assert.Equal(1, hoverChanges);

                interaction.PointerMove(new SKPoint(layout.Center.X + 2f, layout.Center.Y + 2f));
                Assert.Null(doc.HoverTargetId);
                Assert.False(interaction.IsPointerOverInteractive);

                interaction.PointerMove(at);
                interaction.PointerLeave();
                Assert.Null(doc.HoverTargetId);
                Assert.False(interaction.IsPointerOverInteractive);
                Assert.Equal(4, hoverChanges);
            }
        }

        [Fact]
        public void 雷达交互_选中变化触发事件并可外部联动()
        {
            RadarDocument doc = TwoTargetDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                RadarInteraction interaction = new RadarInteraction();
                interaction.Attach(chart);

                int selectionChanges = 0;
                interaction.SelectionChanged += (sender, args) => selectionChanges++;

                interaction.Select("B");
                Assert.Equal("B", doc.SelectedTargetId);
                Assert.Equal(1, selectionChanges);

                interaction.Select("B");
                Assert.Equal(1, selectionChanges);

                interaction.Select(null);
                Assert.Null(doc.SelectedTargetId);
                Assert.Equal(2, selectionChanges);
            }
        }

        [Fact]
        public void 雷达交互_半圆盘不命中扇区外目标()
        {
            RadarDocument doc = FullDocument();
            doc.Scope = RadarScope.Half;
            doc.HalfStartBearing = 270d;
            doc.Targets.Add(new RadarTarget("N") { Bearing = 0d, Distance = 10d });
            doc.Targets.Add(new RadarTarget("S") { Bearing = 180d, Distance = 10d });

            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            SKPoint north = layout.PointOf(0d, 10d);
            SKPoint south = layout.PointOf(180d, 10d);

            Assert.True(layout.Contains(north));
            Assert.False(layout.Contains(south));
            Assert.Null(layout.HitTest(doc, south, 12f));
            Assert.Equal("N", layout.HitTest(doc, north, 12f)?.Id);

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                RadarInteraction interaction = new RadarInteraction();
                interaction.Attach(chart);
                interaction.PointerDown(south);
                Assert.Null(doc.SelectedTargetId);
            }
        }

        // ------------------------------------------------------------ 动画

        [Fact]
        public void 雷达交互_扫描线按耗时推进并归一到0到360()
        {
            RadarDocument doc = TwoTargetDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                RadarInteraction interaction = new RadarInteraction
                {
                    SweepSpeedDegreesPerSecond = 60d,
                };
                interaction.Attach(chart);

                int redraws = 0;
                interaction.RedrawRequested += (sender, args) => redraws++;

                doc.SweepAngle = 350d;
                interaction.Tick(1000d);
                Assert.Equal(50d, doc.SweepAngle, 3);
                Assert.Equal(1, redraws);

                interaction.Tick(250d);
                Assert.Equal(65d, doc.SweepAngle, 3);

                Assert.True(interaction.WantsAnimation);
                interaction.IsAnimating = false;
                Assert.False(interaction.WantsAnimation);

                double frozen = doc.SweepAngle;
                interaction.Tick(1000d);
                Assert.Equal(frozen, doc.SweepAngle, 3);
                Assert.Equal(2, redraws);
            }
        }

        [Fact]
        public void 雷达交互_扫描推进后位图确实变化()
        {
            RadarDocument doc = RadarSample.BuildFull();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                RadarInteraction interaction = new RadarInteraction();
                interaction.Attach(chart);

                byte[] before = chart.Encode(ChartImageFormat.Png, 96f, 100);
                interaction.Tick(500d);
                byte[] after = chart.Encode(ChartImageFormat.Png, 96f, 100);

                Assert.False(before.SequenceEqual(after), "推进扫描线后位图没有变化");
            }
        }

        [Fact]
        public void 雷达交互_悬停目标画出可见高亮()
        {
            RadarDocument doc = TwoTargetDocument();
            doc.SelectedTargetId = null;
            RadarTarget target = doc.Targets[0];
            RadarChartTemplate template = new RadarChartTemplate();
            RadarLayout layout = template.Layout(doc);
            SKPoint at = layout.PointOf(target.Bearing, target.Distance);
            SKRect box = new SKRect(at.X - 14f, at.Y - 14f, at.X + 14f, at.Y + 14f);

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ImageProbe plain = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            using (ImageProbe hovered = ImageProbe.FromPng(
                       EncodeWithHover(chart, doc, target.Id)))
            {
                Assert.True(CountDifferent(plain, hovered, box) > 20, "悬停目标没有画出高亮 ring");
            }
        }

        // ------------------------------------------------------------ 宿主胶水

        [Fact]
        public void 交互宿主_按视口转发指针并自动挂载换表单()
        {
            RadarDocument doc = TwoTargetDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                int redraws = 0;
                int animationChanges = 0;
                using (ChartInteractionHost host = new ChartInteractionHost(
                           () => redraws++, () => animationChanges++))
                {
                    host.Chart = chart;
                    host.Interaction = new RadarInteraction();
                    Assert.True(animationChanges > 0, "挂载交互没有通知宿主");
                    Assert.True(host.WantsAnimation);

                    RadarTarget target = doc.Targets[0];

                    // 首帧还没画过，视口是空的：此时来的指针必须整个丢掉，
                    // 否则视图坐标会被当成逻辑坐标，凭空选中一个看不见的目标。
                    host.PointerDown(new SKPoint(120f, 90f));
                    Assert.Null(doc.SelectedTargetId);

                    host.Viewport = ChartViewport.Create(
                        chart.MeasureSize(), 1600f, 1200f, ChartFit.Fit);

                    SKPoint viewPoint = host.Viewport.ToView(
                        template.Layout(doc).PointOf(target.Bearing, target.Distance));

                    host.PointerDown(viewPoint);
                    Assert.Equal(target.Id, doc.SelectedTargetId);
                    Assert.True(redraws > 0, "选中后没有请求重绘");

                    host.PointerMove(viewPoint);
                    Assert.Equal(target.Id, doc.HoverTargetId);
                    Assert.True(host.IsPointerOverInteractive);

                    host.PointerLeave();
                    Assert.Null(doc.HoverTargetId);

                    // 换表单：交互自动卸下，动画状态随之通知宿主。
                    int beforeDetach = animationChanges;
                    host.Chart = null;
                    Assert.True(animationChanges > beforeDetach, "卸下交互没有通知宿主");
                    Assert.False(host.WantsAnimation);
                }
            }
        }

        [Fact]
        public void 交互宿主_动画帧由真实耗时推进()
        {
            RadarDocument doc = TwoTargetDocument();
            RadarChartTemplate template = new RadarChartTemplate();
            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            using (ChartInteractionHost host = new ChartInteractionHost(() => { }))
            {
                RadarInteraction interaction = new RadarInteraction
                {
                    SweepSpeedDegreesPerSecond = 60d,
                };
                host.Chart = chart;
                host.Interaction = interaction;

                double before = doc.SweepAngle;
                Thread.Sleep(30);
                host.Tick();
                Assert.NotEqual(before, doc.SweepAngle);

                interaction.IsAnimating = false;
                double frozen = doc.SweepAngle;
                Thread.Sleep(10);
                host.Tick();
                Assert.Equal(frozen, doc.SweepAngle, 3);
            }
        }

        [Fact]
        public void 雷达交互_非雷达表单返回null且拒绝挂载()
        {
            using (Chart chart = DemoCatalog.All[0].Create())
            {
                Assert.Null(RadarInteraction.TryCreate(chart));

                RadarInteraction interaction = new RadarInteraction();
                Assert.False(interaction.CanAttach(chart));
                Assert.Throws<ArgumentException>(() => interaction.Attach(chart));
            }
        }

        // ------------------------------------------------------------ 文档

        private static RadarDocument FullDocument()
        {
            RadarDocument doc = new RadarDocument
            {
                FormTitle = "交互测试",
                Scope = RadarScope.Full,
                MaxRange = 40d,
                RangeUnit = "km",
                RingCount = 4,
                ShowSweep = false,
                ShowTrails = false,
            };
            return doc;
        }

        private static RadarDocument TwoTargetDocument()
        {
            RadarDocument doc = FullDocument();
            doc.Targets.Add(new RadarTarget("A") { Label = "甲", Bearing = 60d, Distance = 20d });
            doc.Targets.Add(new RadarTarget("B") { Label = "乙", Bearing = 210d, Distance = 12d });
            return doc;
        }

        // ------------------------------------------------------------ 像素工具

        private static int CountDifferent(ImageProbe before, ImageProbe after, SKRect box)
        {
            int left = Math.Max(0, (int)Math.Floor(box.Left));
            int top = Math.Max(0, (int)Math.Floor(box.Top));
            int right = Math.Min(before.Width, (int)Math.Ceiling(box.Right));
            int bottom = Math.Min(before.Height, (int)Math.Ceiling(box.Bottom));

            int count = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (before.Pixel(x, y) != after.Pixel(x, y))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>把悬停编号写进文档后重新编码一帧，返回 PNG 字节。</summary>
        private static byte[] EncodeWithHover(Chart chart, RadarDocument doc, string targetId)
        {
            doc.HoverTargetId = targetId;
            try
            {
                return chart.Encode(ChartImageFormat.Png, 96f, 100);
            }
            finally
            {
                doc.HoverTargetId = null;
            }
        }
    }
}
