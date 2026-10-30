using System;
using System.Globalization;
using OODD.Charts.Geometry;
using OODD.Charts.Radar;
using OODD.Charts.Style;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T201 + T202 验收（docs/07 E1 两行，docs/06 §2.1 决策 D15）。
    /// <para>
    /// T201 判据原文："既有 A9/三测单/产程图/FormSheet 测试全绿；新静态方法返回的实例 Bounds 正确、
    /// <c>LayoutOf&lt;T&gt;()</c> 类型命中与未命中各一例"。
    /// </para>
    /// <para>
    /// T202 判据原文："Radar 既有全部测试与导出基线不变（A20）；<c>Measure</c> 与 <c>DrawContent</c>
    /// 共用同一 <c>RadarLayout</c> 实例（用引用相等断言）"。引用相等在这里换成**可观测的等价判据**：
    /// 版面是私有对象，外部拿不到"那一个实例"，但"共用同一份"的唯一后果是——
    /// 改了令牌却不重测版面时，出图必须与改之前逐字节相同（绘制跟着缓存版面走），
    /// 而重测之后出图必须不同（证明那次令牌改动真有可见效果，等式不是空转）。
    /// 两条一起构成双向判据，任何一侧缺失都说明 DrawContent 还在自己重算。
    /// </para>
    /// </summary>
    public sealed class GeometryLayoutTests
    {
        private static readonly SKRect TableBounds = new SKRect(0f, 0f, 400f, 300f);

        /// <summary>领域版面对象的替身：挂点只认"是不是我自己那个对象"。</summary>
        private sealed class ProbeLayout
        {
            public SKRect Scope = new SKRect(1f, 2f, 30f, 40f);
        }

        // ================================================================ T201

        [Fact]
        public void 挂点几何_版面命中与未命中_外接与绘图区如实回填()
        {
            SKRect grid = new SKRect(10f, 20f, 390f, 280f);
            ProbeLayout layout = new ProbeLayout();

            ChartGeometry geo = ChartGeometry.ForLayout(TableBounds, grid, layout);

            Assert.Equal(TableBounds, geo.Bounds);
            Assert.Equal(grid, geo.GridRect);
            Assert.True(ReferenceEquals(layout, geo.Layout),
                        "Layout 必须就是传入的那个实例（副本会让领域版面与几何脱钩）");
            Assert.True(ReferenceEquals(layout, geo.LayoutOf<ProbeLayout>()),
                        "LayoutOf<ProbeLayout>() 应命中同一实例，实到 " + geo.LayoutOf<ProbeLayout>());
            Assert.True(geo.LayoutOf<SlotAxis>() == null,
                        "类型不符时 LayoutOf 必须返回 null 而不是抛，实到 " + geo.LayoutOf<SlotAxis>());
        }

        [Fact]
        public void 表格式构造器的挂点为空_存量版面不受影响()
        {
            ChartGeometry geo = new ChartGeometry(
                TableBounds,
                new SlotAxis(0f, 10f, 1, 1),
                new LinearAxis("rule", 0d, 10d, 0f, 300f, 1d, 5d),
                new LinearAxis[0],
                new Band("header", new SKRect(0f, 0f, 400f, 40f), new BandRow[0]),
                new Band("date", new SKRect(0f, 40f, 400f, 60f), new BandRow[0]),
                new SKRect(0f, 60f, 400f, 260f),
                new LabelColumn[0],
                new Band("cells", new SKRect(0f, 60f, 400f, 260f), new BandRow[0]),
                new CellRow[0],
                new Band("footer", new SKRect(0f, 260f, 400f, 300f), new BandRow[0]));

            Assert.True(geo.Layout == null, "表格式几何没有领域版面，Layout 应为 null，实到 " + geo.Layout);
            Assert.True(geo.LayoutOf<ProbeLayout>() == null, "未挂版面时 LayoutOf 应为 null");
            // 存量的表格式成员照旧可用（加法不改动既有语义）：GridLeft/GridRight 仍来自真列轴，
            // YOf 仍走登记的 rule 轴——期望值取自轴对象自身，不凭记忆写。
            Assert.Equal(0f, geo.GridLeft, 3);
            Assert.Equal(10f, geo.GridRight, 3);
            Assert.Equal(geo.RuleAxis.YOf(3d), geo.YOf("rule", 3d), 6);
            Assert.True(geo.Axis("rule") == geo.RuleAxis, "按键取轴应回到同一个轴实例");
        }

        [Fact]
        public void 挂点几何取用表格式成员时抛带出路的异常()
        {
            // 这条是"消假轴"的正面证据：ForLayout 不再编造 SlotAxis(0,1,1,1) 与假刻度轴，
            // 所以误用表格式成员必须**响亮地失败并指出该用 LayoutOf<T>()**，
            // 而不是留下一个看着合法、实则无意义的 0~1 列轴等着被断言成"版面正确"。
            ChartGeometry geo = ChartGeometry.ForLayout(TableBounds, SKRect.Empty, new ProbeLayout());

            Assert.Throws<InvalidOperationException>(() => { float ignored = geo.GridLeft; });
            Assert.Throws<InvalidOperationException>(() => { float ignored = geo.GridRight; });
            Assert.Throws<InvalidOperationException>(() => { float ignored = geo.SlotCenterX(0); });
            Assert.Throws<InvalidOperationException>(() => { float ignored = geo.GroupCenterX(0); });
            Assert.Throws<InvalidOperationException>(() => { SKRect ignored = geo.CellRect("row", 0); });
            Assert.Throws<InvalidOperationException>(() => { LinearAxis ignored = geo.Axis("rule"); });
            Assert.Throws<InvalidOperationException>(() => { float ignored = geo.YOf("rule", 1d); });

            LinearAxis axis;
            Assert.False(geo.TryAxis("rule", out axis), "未登记任何轴时 TryAxis 应返回 false");
            Assert.True(axis == null, "TryAxis 失败时不应留下半个轴，实到 " + axis);

            InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(
                () => { float ignored = geo.GridLeft; });
            Assert.True(thrown.Message.IndexOf("ForLayout", StringComparison.Ordinal) >= 0,
                        "异常消息必须点名 ForLayout，便于调用方立刻找到出路，实到：" + thrown.Message);
            Assert.True(thrown.Message.IndexOf("ProbeLayout", StringComparison.Ordinal) >= 0,
                        "异常消息应带上实际挂的版面类型名，实到：" + thrown.Message);
        }

        [Fact]
        public void 挂点几何拒绝空版面与非正外接矩形()
        {
            // 版面为 null 就该用表格式构造器——静默接受会把"忘了挂版面"伪装成"挂点可用"。
            Assert.Throws<ArgumentNullException>(() => ChartGeometry.ForLayout(TableBounds, SKRect.Empty, null!));

            Assert.Throws<ArgumentException>(
                () => ChartGeometry.ForLayout(new SKRect(0f, 0f, 0f, 300f), SKRect.Empty, new ProbeLayout()));
            Assert.Throws<ArgumentException>(
                () => ChartGeometry.ForLayout(new SKRect(0f, 0f, 400f, -1f), SKRect.Empty, new ProbeLayout()));
            // !(x > 0) 同时挡住 NaN，不留静默的 NaN 版面。
            Assert.Throws<ArgumentException>(
                () => ChartGeometry.ForLayout(new SKRect(0f, 0f, float.NaN, 300f), SKRect.Empty, new ProbeLayout()));
        }

        // ================================================================ T202

        [Fact]
        public void 雷达版面只有一个来源_改令牌不重测时出图与缓存版面逐字节相同()
        {
            RadarDocument doc = Document();
            RadarChartTemplate template = new RadarChartTemplate();

            float radiusBefore = template.Layout(doc).Radius;
            byte[] before;
            byte[] stale;
            byte[] refreshed;

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                // 首次访问 Geometry 才求解版面，此后 Record/Encode 都复用这份缓存几何。
                chart.MeasureSize();
                before = chart.Encode(ChartImageFormat.Png, 96f, 100);

                template.Tokens.Margin = 64f;

                // 不 Invalidate：缓存的 Geometry 仍是旧版面。DrawContent 若偷偷重算一次版面，
                // 落点就会跑到 Measure 定下的 Bounds 之外，出图必然改变。
                stale = chart.Encode(ChartImageFormat.Png, 96f, 100);

                chart.Invalidate();
                refreshed = chart.Encode(ChartImageFormat.Png, 96f, 100);
            }

            string beforeText = Convert.ToBase64String(before);
            Assert.True(beforeText == Convert.ToBase64String(stale),
                        "改令牌但未重测版面时，出图必须跟着 Measure 缓存的那份版面走");

            float radiusAfter = template.Layout(doc).Radius;
            Assert.True(Math.Abs(radiusBefore - radiusAfter) > 1f,
                        "对照组失效：Margin 18→64 没有改变盘面半径（前 " + Radius(radiusBefore)
                        + "，后 " + Radius(radiusAfter) + "），上面的等式就成了空转");
            Assert.True(beforeText != Convert.ToBase64String(refreshed),
                        "重测版面后出图必须变化，否则无法证明令牌改动确实有可见效果");
        }

        [Fact]
        public void 雷达几何不再塞假轴与假带()
        {
            RadarDocument doc = Document();
            RadarChartTemplate template = new RadarChartTemplate();

            using (Chart chart = new Chart(doc, template, new ChartStyle()))
            {
                ChartGeometry geo = chart.Geometry;
                RadarLayout? layout = geo.LayoutOf<RadarLayout>();

                Assert.True(layout != null, "Radar 的 Measure 必须把 RadarLayout 挂上挂点");
                Assert.Equal(layout.Bounds, geo.Bounds);
                Assert.Equal(layout.ScopeRect, geo.GridRect);
                // A20 基线：盘面外接矩形的半边长就是半径（RadarTests 里同一条断言，这里钉住来源）。
                Assert.Equal(layout.Radius, geo.GridRect.Width / 2f, 3);

                Assert.True(geo.Slots == null, "假 SlotAxis(0,1,1,1) 应已删除，实到 " + geo.Slots);
                Assert.True(geo.RuleAxis == null, "假 rule 轴应已删除，实到 " + geo.RuleAxis);
                Assert.True(geo.Header == null, "假 header 带应已删除");
                Assert.True(geo.DateBand == null, "假 date 带应已删除");
                Assert.True(geo.CellTable == null, "假 cells 带应已删除");
                Assert.True(geo.Footer == null, "假 footer 带应已删除");
                Assert.True(geo.LabelColumns.Count == 0,
                            "假标签列应已随挂点消失，实到 " + geo.LabelColumns.Count + " 列");
                Assert.True(geo.CellRows.Count == 0,
                            "假附属行应已随挂点消失，实到 " + geo.CellRows.Count + " 行");
                LinearAxis axis;
                Assert.False(geo.TryAxis("range", out axis), "range 轴属于伪造，应已随挂点消失");
                Assert.True(geo.Row("panel") == null, "附属行是假值，应已随挂点消失");
            }
        }

        // ================================================================ 构造

        private static RadarDocument Document()
        {
            RadarDocument doc = new RadarDocument
            {
                FormTitle = "挂点验证雷达",
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

        private static string Radius(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
