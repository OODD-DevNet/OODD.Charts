using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Radar;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T215 符号体系验收（docs/07 该行原文判据：每符号一个像素探针用例（非空、色对、尺寸令牌生效）；
    /// 风杆 0°→上、90°→右（顺时针，与雷达角度口径一致）；DigitBox 逐格落字仍是可检索文本）。
    /// <para>
    /// 出图一律走 <c>SKPictureRecorder → SKSurface → canvas.DrawPicture → image.Encode</c>
    /// （<c>SKImage.FromPicture</c> 在 SkiaSharp 3.116.1 上抛不可捕获的 AccessViolationException），
    /// 且固定 96dpi，于是<b>设备像素 == 96dpi 逻辑像素</b>，所有期望值都能由符号公开因子的算术复算：
    /// 每个用例的注释里都写了复算过程，不写"我觉得"。
    /// </para>
    /// <para>
    /// 色对判据用 <see cref="ImageProbe.AverageInkColor"/>：符号几乎都是细线（线粗 0.03–0.1×size），
    /// 描边中心线两侧的半覆盖像素按 <see cref="ImageProbe.IsInk"/> 计入墨量、色值却被白底稀释，
    /// 所以平均色必然比声明色淡。按"半覆盖像素约占墨量 1/4"估算，偏移上界 0.25×255 ≈ 64，
    /// 容差取 70；实心填充的窗口（菱形底色）实测远小于该值。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class GlyphTests
    {
        private static readonly SKColor BlackInk = SKColors.Black;
        private static readonly SKColor BlueInk = new SKColor(0, 0, 255);
        private static readonly SKColor RedInk = new SKColor(255, 0, 0);
        private static readonly SKColor GreenInk = new SKColor(0, 180, 0);

        /// <summary>平均色容差（见类型注释的推导）。</summary>
        private const int ColorTolerance = 70;

        /// <summary>长度断言容差：描边落在非整像素上时，设备像素外接跨度最多偏 1.5 px。</summary>
        private const double SpanTolerance = 1.5;

        // ================================================================ 注册表

        [Fact]
        public void 符号注册表_内置六符号齐备且是最中性样式()
        {
            AssertBuiltIn(Glyphs.PairedEventKey, typeof(PairedEventGlyph));
            AssertBuiltIn(Glyphs.DiamondPlacardKey, typeof(DiamondPlacard));
            AssertBuiltIn(Glyphs.WindBarbKey, typeof(WindBarb));
            AssertBuiltIn(Glyphs.DigitBoxKey, typeof(DigitBox));
            AssertBuiltIn(Glyphs.TriStateBoxKey, typeof(TriStateBox));
            AssertBuiltIn(Glyphs.StampRingKey, typeof(StampRing));

            // 内置集取该族最中性样式（源码 Glyphs 静态构造的注释口径）：空心圆成对 / 正北 10 节 / 6 格 / 未勾选 / 正章不倾。
            PairedEventGlyph paired = (PairedEventGlyph)Glyphs.Find(Glyphs.PairedEventKey)!;
            Assert.Equal(MarkerSymbol.Ring, paired.StartSymbol);
            Assert.Equal(MarkerSymbol.Ring, paired.EndSymbol);
            Assert.True(paired.ConnectWithLine, "内置成对符号默认带连线");

            WindBarb wind = (WindBarb)Glyphs.Find(Glyphs.WindBarbKey)!;
            Assert.Equal(0d, wind.DirectionDegrees, 5);
            Assert.Equal(10, wind.Knots);
            Assert.True(ReferenceEquals(WindBarb.BarbRule.WmoStandard, wind.Rule), "内置风杆用 WMO 常见编码 50/10/5");

            DigitBox digit = (DigitBox)Glyphs.Find(Glyphs.DigitBoxKey)!;
            Assert.Equal(6, digit.BoxCount);
            Assert.False(((TriStateBox)Glyphs.Find(Glyphs.TriStateBoxKey)!).State.Equals(TriState.Checked),
                         "内置三态框默认未勾选");
            Assert.Equal(0d, ((StampRing)Glyphs.Find(Glyphs.StampRingKey)!).RotationDegrees, 5);
        }

        [Fact]
        public void 符号注册表_键校验与自定义覆盖()
        {
            // 键名带 T215 前缀且不覆盖任何内置键：注册表是进程级、测试并行，蹭内置键会污染别的用例。
            const string key = "test.t215.glyph-probe";
            try
            {
                Assert.False(Glyphs.IsRegistered(key), "用例开始前该测试键不该存在");
                Assert.Null(Glyphs.Find(key));

                TriStateBox custom = new TriStateBox(TriState.NotApplicable);
                Glyphs.Register(key, custom);
                Assert.True(Glyphs.IsRegistered(key));
                Assert.True(ReferenceEquals(custom, Glyphs.Find(key)), "Register 同键覆盖后 Find 必须给回同一个实现");

                Assert.True(Glyphs.Unregister(key), "Unregister 应报告确实删掉了");
                Assert.False(Glyphs.Unregister(key), "重复 Unregister 应返回 false");
                Assert.Null(Glyphs.Find(key));
            }
            finally
            {
                Glyphs.Unregister(key);
            }

            Assert.Null(Glyphs.Find(string.Empty));
            Assert.False(Glyphs.IsRegistered(string.Empty));
            Assert.False(Glyphs.IsRegistered(null!));
            Assert.Throws<ArgumentNullException>(() => Glyphs.Register(key, null!));
            Assert.Throws<ArgumentException>(() => Glyphs.Register(string.Empty, new TriStateBox(TriState.Unchecked)));

            Assert.True(Glyphs.Count >= 6, "内置六符号之外还应有内置集，实到 " + Glyphs.Count);
            IReadOnlyList<string> keys = Glyphs.Keys;
            foreach (string builtin in new[]
            {
                Glyphs.PairedEventKey, Glyphs.DiamondPlacardKey, Glyphs.WindBarbKey,
                Glyphs.DigitBoxKey, Glyphs.TriStateBoxKey, Glyphs.StampRingKey,
            })
            {
                Assert.Contains(builtin, keys);
            }
            for (int i = 1; i < keys.Count; i++)
            {
                Assert.True(string.CompareOrdinal(keys[i - 1], keys[i]) < 0,
                            "Keys 需按 Ordinal 升序，第 " + i + " 项 " + keys[i] + " 未大于前一项 " + keys[i - 1]);
            }
        }

        [Fact]
        public void 符号_零负尺寸不落墨且空画师拒绝()
        {
            IGlyph[] builtins =
            {
                Glyphs.Find(Glyphs.PairedEventKey)!, Glyphs.Find(Glyphs.DiamondPlacardKey)!,
                Glyphs.Find(Glyphs.WindBarbKey)!, Glyphs.Find(Glyphs.DigitBoxKey)!,
                Glyphs.Find(Glyphs.TriStateBoxKey)!, Glyphs.Find(Glyphs.StampRingKey)!,
            };

            foreach (IGlyph glyph in builtins)
            {
                string name = glyph.GetType().Name;
                SKPoint anchor = new SKPoint(80f, 60f);

                using (ImageProbe zero = Render(160, 120, p => glyph.Draw(p, anchor, 0f, BlackInk)))
                {
                    Assert.False(zero.HasInkInBox(0, 0, 160, 120), name + "：size=0 不该落任何墨");
                }
                using (ImageProbe negative = Render(160, 120, p => glyph.Draw(p, anchor, -12f, BlackInk)))
                {
                    Assert.False(negative.HasInkInBox(0, 0, 160, 120), name + "：size<0 属非法尺寸，应什么都不画而不是猜一个尺寸");
                }

                Assert.Throws<ArgumentNullException>(() => glyph.Draw(null!, anchor, 20f, BlackInk));
            }
        }

        // ================================================================ PairedEventGlyph

        [Fact]
        public void 成对符号_环心空心_连线让位_间隙干净()
        {
            // 公开因子（PairedEventGlyph 默认）：MarkerRadiusFactor 0.5、PairHalfSpanFactor 1.6、
            // StrokeWidthFactor 0.1、SymbolGapFactor 2.2。size=50、anchor=(130,60) 时逐值复算：
            //   端点中心 = 130 ± 1.6×50 = (50,60) 与 (210,60)
            //   环半径   = 0.5×50 = 25，线粗 = 0.1×50 = 5 → 环墨带 r∈[22.5,27.5]
            //   让位长度 = 2.2×25 = 55 → 连线自 (50+55, 60)=(105,60) 到 (210-55,60)=(155,60)
            //   → 左环外缘墨止于 x=77.5，连线起于 x=105：x∈[80,103] 必须是干净的让位间隙
            PairedEventGlyph glyph = new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, true);
            using (ImageProbe probe = Render(260, 120, p => glyph.Draw(p, new SKPoint(130f, 60f), 50f, BlackInk)))
            {
                Assert.True(probe.NonWhiteRatio(new SKRect(0f, 0f, 260f, 120f)) > 0.005d, "非空判据失败：整幅没有墨");
                Assert.True(probe.IsInk(130, 60), "连线应在两环正中 (130,60) 落墨");
                Assert.True(probe.IsInk(106, 60) && probe.IsInk(154, 60), "连线应正好覆盖让位点 105–155");
                Assert.False(probe.IsInk(50, 60), "左环是空心环 Ring，环心不该有墨");
                Assert.False(probe.IsInk(210, 60), "右环环心不该有墨");
                Assert.True(probe.IsInk(50, 35) && probe.IsInk(50, 85), "左环顶/底墨带应在 y=60±25 附近");
                Assert.False(probe.HasInkInBox(80, 40, 103, 80), "左侧让位间隙 (77.5,105) 必须干净，连线不得穿过环口");
                Assert.False(probe.HasInkInBox(158, 40, 181, 80), "右侧让位间隙 (155,182.5) 必须干净");
                Assert.False(probe.HasInkInBox(105, 20, 155, 50), "连线上方不该有第二种墨");
                Assert.True(RunsInColumn(probe, 50, 0, 119) == 2, "空心环在同一列只应交出上下两段墨，实到 " + RunsInColumn(probe, 50, 0, 119));
            }
        }

        [Fact]
        public void 成对符号_连线开关与端点样式各如其分()
        {
            PairedEventGlyph noLine = new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, false);
            using (ImageProbe probe = Render(260, 120, p => noLine.Draw(p, new SKPoint(130f, 60f), 50f, BlackInk)))
            {
                Assert.False(probe.HasInkInBox(100, 40, 160, 80), "ConnectWithLine=false 时两环之间必须完全干净");
                Assert.True(probe.IsInk(50, 35) && probe.IsInk(210, 35), "两端环仍应落墨");
            }

            // Dot 端点：FillCircle(center, 25) → 环心位置现在是实心墨
            PairedEventGlyph dots = new PairedEventGlyph(MarkerSymbol.Dot, MarkerSymbol.Dot, true);
            using (ImageProbe probe = Render(260, 120, p => dots.Draw(p, new SKPoint(130f, 60f), 50f, BlackInk)))
            {
                Assert.True(probe.IsInk(50, 60) && probe.IsInk(210, 60), "实心点端点应在端点中心落墨");
                Assert.True(probe.IsInk(130, 60), "连线仍应在");
                Assert.True(probe.NonWhiteRatio(new SKRect(25f, 35f, 75f, 85f)) > 0.4d,
                            "左端实心点的填充占比应远高于空心环版本");
            }
        }

        [Fact]
        public void 成对符号_色对()
        {
            PairedEventGlyph glyph = new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, true);
            SKRect window = new SKRect(0f, 0f, 260f, 120f);
            using (ImageProbe black = Render(260, 120, p => glyph.Draw(p, new SKPoint(130f, 60f), 50f, BlackInk)))
            using (ImageProbe red = Render(260, 120, p => glyph.Draw(p, new SKPoint(130f, 60f), 50f, RedInk)))
            {
                AssertAverageInk(black, window, BlackInk, "成对符号黑");
                AssertAverageInk(red, window, RedInk, "成对符号红");
                SKColor a = black.AverageInkColor(window);
                SKColor b = red.AverageInkColor(window);
                Assert.True(Math.Abs(a.Red - b.Red) > 40, "两次不同主色的平均色没拉开：黑 " + Describe(a) + " 红 " + Describe(b));
            }
        }

        [Fact]
        public void 成对符号_尺寸令牌生效()
        {
            // 墨迹跨度由公开因子闭式给出：
            //   横向 = 2×(PairHalfSpanFactor + MarkerRadiusFactor + StrokeWidthFactor/2)×size = 2×2.15×size = 4.3×size
            //   纵向 = 2×(MarkerRadiusFactor + StrokeWidthFactor/2)×size = 1.1×size
            PairedEventGlyph glyph = new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, true);
            Extent small = InkOf(glyph, 40f);
            Extent large = InkOf(glyph, 80f);

            Assert.False(small.IsEmpty, "size=40 没落墨");
            Close(small.Width, 4.3 * 40, 2);
            Close(small.Height, 1.1 * 40, 2);
            Close(large.Width, 4.3 * 80, 2);
            Close(large.Height, 1.1 * 80, 2);
            Assert.True(Math.Abs(large.Width / small.Width - 2d) <= 0.03d,
                        "size 翻倍后横向跨度应翻倍，实到 " + small.Width + " → " + large.Width);
            Assert.True(Math.Abs(large.Height / small.Height - 2d) <= 0.05d,
                        "size 翻倍后纵向跨度应翻倍，实到 " + small.Height + " → " + large.Height);
        }

        private static Extent InkOf(IGlyph glyph, float size)
        {
            using (ImageProbe probe = Render(400, 220, p => glyph.Draw(p, new SKPoint(200f, 110f), size, BlackInk)))
            {
                return MeasureInk(probe, 0, 0, 400, 220);
            }
        }

        // ================================================================ DiamondPlacard

        [Fact]
        public void 菱形牌_顶角朝上_斜边四十五度_四边等长()
        {
            // size=100、中心 (60,60)：顶点 = 中心 ± 半对角线 50 → (60,10)(110,60)(60,110)(10,60)。
            // 菱形边界是 |x-cx| + |y-cy| = 50，所以第 dy 行上的两段墨中心必在 cx ± (50-|dy|)。
            // 这一条同时证明：顶角朝上（跨度在中线最大、向两顶收敛）、斜边 45°（斜率 1:1）、四边等长（上下左右对称）。
            DiamondPlacard glyph = new DiamondPlacard();
            using (ImageProbe probe = Render(130, 130, p => glyph.Draw(p, new SKPoint(60f, 60f), 100f, BlackInk)))
            {
                Assert.True(probe.NonWhiteRatio(new SKRect(0f, 0f, 130f, 130f)) > 0.01d, "非空判据失败");
                Assert.False(probe.IsInk(60, 60), "不填底色时菱形中心必须干净（只有框线）");

                foreach (int dy in new[] { -40, -20, 0, 20, 40 })
                {
                    IReadOnlyList<double> runs = probe.VerticalRulesAt(60 + dy, 0, 129);
                    Assert.True(runs.Count == 2, "y=" + (60 + dy) + " 该行应交出左右两段斜边墨，实到 " + Join(runs));
                    double expectHalf = 50 - Math.Abs(dy);
                    Assert.True(Math.Abs(runs[0] - (60 - expectHalf)) <= 2.5d,
                                "左斜边在该行的落点应距中心 " + expectHalf + "，实到 " + (60 - runs[0]) + "（dy=" + dy + "）");
                    Assert.True(Math.Abs(runs[1] - (60 + expectHalf)) <= 2.5d,
                                "右斜边在该行的落点应距中心 " + expectHalf + "，实到 " + (runs[1] - 60) + "（dy=" + dy + "）");
                }

                IReadOnlyList<double> column = probe.HorizontalRulesAt(60, 0, 129);
                Assert.True(column.Count == 2, "过中心的列应交出顶/底两个角，实到 " + Join(column));
                Assert.True(Math.Abs(column[0] - 10) <= 2.5d, "顶角应落在 y=10（中心上方半对角线 50），实到 " + column[0]);
                Assert.True(Math.Abs(column[1] - 110) <= 2.5d, "底角应落在 y=110，实到 " + column[1]);
            }
        }

        [Fact]
        public void 菱形牌_底色与槽位与注入图案()
        {
            // 槽矩形由公开因子复算（size=100、中心 (60,60)）：半对角线 50，
            // SlotInnerFactor 0.14 → inner=7，SlotOuterFactor 0.62 → outer=31，
            // SlotWidthInsetFactor 0.92 → halfWidth=(50-31)×0.92=17.48
            //   图案槽 = x∈[42.52,77.48]、y∈[29,53]；类项号槽 = x∈[42.52,77.48]、y∈[67,91]
            DiamondPlacard structure = new DiamondPlacard();
            SKPoint anchor = new SKPoint(60f, 60f);
            SKRect symbolSlot = structure.SymbolSlotOf(anchor, 100f);
            SKRect numberSlot = structure.NumberSlotOf(anchor, 100f);
            Assert.True(numberSlot.Top > symbolSlot.Bottom, "类项号槽必须在下半角、图案槽在上半角（JT/T 617 版面口径）");
            // SkiaSharp 3.116.1 的 SKRect 没有 Intersects 实例方法，相交判据自己按闭式写：
            // 两矩形在任一轴上投影不重叠即不相交。
            Assert.False(symbolSlot.Left < numberSlot.Right && numberSlot.Left < symbolSlot.Right
                         && symbolSlot.Top < numberSlot.Bottom && numberSlot.Top < symbolSlot.Bottom,
                         "两槽不得相交（Skia 的 Overlaps 判据手写复算）");
            foreach (SKRect slot in new[] { symbolSlot, numberSlot })
            {
                Assert.True(Math.Abs(slot.Left - 42.52f) <= 0.01f, "槽左缘由公开因子可复算，实到 " + slot.Left);
                Assert.True(Math.Abs(slot.Right - 77.48f) <= 0.01f, "槽右缘由公开因子可复算，实到 " + slot.Right);
                // 四角都须在菱形内：|dx| + |dy| <= 半对角线
                Assert.True(Math.Abs(slot.Left - 60) + Math.Abs(slot.Top - 60) <= 50.001f, "槽角越出菱形外框");
                Assert.True(Math.Abs(slot.Right - 60) + Math.Abs(slot.Bottom - 60) <= 50.001f, "槽角越出菱形外框");
            }

            // 底色与边框分色：填充是 _background，框线是主色
            DiamondPlacard twoTone = new DiamondPlacard(null, true, GreenInk);
            using (ImageProbe probe = Render(130, 130, p => twoTone.Draw(p, anchor, 100f, BlueInk)))
            {
                Assert.True(probe.IsInk(60, 60), "填底色后菱形中心应有底色墨");
                AssertAverageInk(probe, new SKRect(48f, 48f, 72f, 72f), GreenInk, "菱形底色");
                SKColor tip = probe.Pixel(9, 60);
                Assert.True(tip.Blue > tip.Green + 40 && tip.Blue > tip.Red + 40,
                            "左角顶点框线应仍是主色蓝，实到 " + Describe(tip));
            }

            // 注入图案：等比缩放并居中到图案槽（DiamondPlacard.DrawSymbol:238-244）。
            // 原路径界 (2,3)-(12,23) → bounds 宽 10、高 20、中心 (7,13)
            //   scale = min(槽宽 34.96/10, 槽高 24/20) = 1.2
            //   平移后落墨矩形 = 槽中心 (60,41) ± (6,12) → (54,29)-(66,53)，即宽 12、高 24
            using (SKPath mark = new SKPath())
            {
                mark.AddRect(new SKRect(2f, 3f, 12f, 23f));

                // SymbolFill 默认 true → FillPath（源码 245-248 行）：图案整体实心。
                DiamondPlacard withMark = new DiamondPlacard(mark, false, null);
                using (ImageProbe probe = Render(130, 130, p => withMark.Draw(p, anchor, 100f, BlackInk)))
                {
                    Extent ink = MeasureInk(probe, 50, 24, 70, 58);   // 该窗口避开了菱形框线，只剩注入图案
                    Assert.False(ink.IsEmpty, "注入图案没落墨");
                    Close(ink.Width, 12, 2);
                    Close(ink.Height, 24, 2);
                    Assert.True(probe.IsInk(60, 41), "SymbolFill=true 时图案中心 (60,41) 应被填实");
                    Assert.True(RunsInColumn(probe, 60, 24, 58) == 1,
                                "实心图案过中心列应交出一整段墨（不是空心两条边），实到 " + RunsInColumn(probe, 60, 24, 58));
                    Assert.True(CountInk(probe, 55, 30, 65, 52) > 0.6 * 10 * 22,
                                "实心填充的墨量应接近 10×22 的内窗面积，实到 " + CountInk(probe, 55, 30, 65, 52));
                    Assert.False(probe.HasInkInBox(45, 67, 76, 91), "图案只进上半角图案槽，下半角类项号槽必须留给模板落字（符号不写字）");
                }

                // 开关关掉后同一图案变成描边：中心干净、过中心列两段边。
                DiamondPlacard outlined = new DiamondPlacard(mark, false, null);
                outlined.SymbolFill = false;
                using (ImageProbe probe = Render(130, 130, p => outlined.Draw(p, anchor, 100f, BlackInk)))
                {
                    Assert.False(probe.IsInk(60, 41), "SymbolFill=false 时图案只剩描边，中心必须干净");
                    Assert.True(RunsInColumn(probe, 60, 24, 58) == 2,
                                "空心描边图案过中心列应交出上下两条边，实到 " + RunsInColumn(probe, 60, 24, 58));
                }
            }
        }

        [Fact]
        public void 菱形牌_色对与尺寸令牌生效()
        {
            // 外接跨度 = size + 2×(线粗/2)/sin(内角/2) = size + 2×(0.015×size)/sin45 = 1.0424×size
            DiamondPlacard glyph = new DiamondPlacard(null, true, null);
            using (ImageProbe black = Render(260, 260, p => glyph.Draw(p, new SKPoint(130f, 130f), 120f, BlackInk)))
            using (ImageProbe red = Render(260, 260, p => glyph.Draw(p, new SKPoint(130f, 130f), 120f, RedInk)))
            {
                AssertAverageInk(black, new SKRect(0f, 0f, 260f, 260f), BlackInk, "菱形牌黑（实心底色）");
                AssertAverageInk(red, new SKRect(0f, 0f, 260f, 260f), RedInk, "菱形牌红（实心底色）");
            }

            Extent small = PlacardInk(80f);
            Extent large = PlacardInk(160f);
            Assert.False(small.IsEmpty, "size=80 没落墨");
            Close(small.Width, 1.0424 * 80, 2);
            Close(small.Height, 1.0424 * 80, 2);
            Close(large.Width, 1.0424 * 160, 2);
            Close(large.Height, 1.0424 * 160, 2);
            Assert.True(Math.Abs(small.Width - small.Height) <= 1d, "菱形外接框应是正方形（四边等长的直接推论）");
            Assert.True(Math.Abs(large.Width / small.Width - 2d) <= 0.03d,
                        "size 翻倍后外接跨度应翻倍，实到 " + small.Width + " → " + large.Width);
        }

        private static Extent PlacardInk(float size)
        {
            DiamondPlacard glyph = new DiamondPlacard();
            using (ImageProbe probe = Render(260, 260, p => glyph.Draw(p, new SKPoint(130f, 130f), size, BlackInk)))
            {
                return MeasureInk(probe, 0, 0, 260, 260);
            }
        }

        // ================================================================ WindBarb

        [Fact]
        public void 风杆_零度杆身朝上_站圈留给模板()
        {
            // size=100、站圈圆心 (100,200)、10 节（WMO 50/10/5 → 1 全旗）：
            //   杆根 = 200 - 0.28×100 = 172，杆末 = 200 - (0.28+1.0)×100 = 72，线粗 0.07×100 = 7
            //   全旗在杆末端向左手侧（BarbsOnLeft=true → 局部 -x）伸出 0.34×100 = 34 → x=66
            //   → 墨迹跨度：y∈[72-3.5, 172]，x∈[66, 103.5]
            WindBarb glyph = new WindBarb(0f, 10, WindBarb.BarbRule.WmoStandard);
            using (ImageProbe probe = Render(200, 260, p => glyph.Draw(p, new SKPoint(100f, 200f), 100f, BlackInk)))
            {
                Extent ink = MeasureInk(probe, 0, 0, 200, 260);
                Assert.False(ink.IsEmpty, "非静风必须落墨");
                Close(ink.MinY, 200 - 128 - 3.5, SpanTolerance);
                Close(ink.MaxY, 200 - 28, SpanTolerance);
                Close(ink.MinX, 100 - 34, SpanTolerance);
                Close(ink.MaxX, 100 + 3.5, SpanTolerance);

                Assert.False(probe.HasInkInBox(0, 176, 200, 259), "0° 时杆身朝上：圆心一侧（y>杆根）不该有任何墨");
                Assert.False(probe.IsInk(100, 200), "本符号不画站圈，圆心处不该有墨（站圈由模板落）");

                IReadOnlyList<double> shafts = probe.StrongVerticalLines(90, 110, 80, 165, 0.85);
                Assert.True(shafts.Count == 1, "竖直杆身应是一条强线，实到 " + Join(shafts));
                Assert.True(Math.Abs(shafts[0] - 100) <= 2d, "竖直杆身应落在圆心列 x=100，实到 " + shafts[0]);

                Assert.True(glyph.TipOf(new SKPoint(100f, 200f), 100f).Y < 200f - 100f, "公开落位方法与像素同源：TipOf 在圆心上方");
            }
        }

        [Fact]
        public void 风杆_九十度杆身朝右()
        {
            // 顺时针转 90°（与 0° 同一 size/圆心）：杆根 (128,200)、杆末 (228,200)，
            // 羽旗由杆末端向"背向站圈看的左手侧"→ 屏幕上方，伸到 y=200-34=166。
            // 若是逆时针口径，杆末会落在 x=72（左侧），羽旗朝下——这两处正是本用例的裁判点。
            WindBarb glyph = new WindBarb(90f, 10, WindBarb.BarbRule.WmoStandard);
            using (ImageProbe probe = Render(260, 260, p => glyph.Draw(p, new SKPoint(100f, 200f), 100f, BlackInk)))
            {
                Extent ink = MeasureInk(probe, 0, 0, 260, 260);
                Assert.False(ink.IsEmpty, "90° 必须落墨");
                Close(ink.MinX, 128, SpanTolerance);
                Close(ink.MaxX, 228 + 3.5, SpanTolerance);
                Close(ink.MinY, 200 - 34, SpanTolerance);
                Close(ink.MaxY, 200 + 3.5, SpanTolerance);

                Assert.False(probe.HasInkInBox(0, 0, 124, 259), "90° 杆身朝右：圆心左侧（x<杆根）不该有任何墨");
                Assert.False(probe.HasInkInBox(0, 208, 259, 259), "羽旗应在杆的左手侧（屏幕上方），下方不该有墨");

                IReadOnlyList<double> shafts = probe.StrongHorizontalLines(190, 210, 140, 220, 0.85);
                Assert.True(shafts.Count == 1, "水平杆身应是一条强线，实到 " + Join(shafts));
                Assert.True(Math.Abs(shafts[0] - 200) <= 2d, "水平杆身应落在圆心行 y=200，实到 " + shafts[0]);
            }
        }

        [Fact]
        public void 风杆_四基本方位顺时针且与雷达方位角同口径()
        {
            const float Size = 100f;
            SKPoint station = new SKPoint(150f, 150f);
            float reach = Size * 1.28f;   // StationRadiusFactor 0.28 + ShaftLengthFactor 1.0

            foreach (float angle in new[] { 0f, 90f, 180f, 270f })
            {
                double radians = angle * Math.PI / 180d;
                // 0°=正北=屏幕向上、顺时针增大（docs/06 §4.10 ①；与 RadarLayout.PointOf 的 sin/-cos 同一公式）
                SKPoint tip = new SKPoint(station.X + (float)(Math.Sin(radians) * reach),
                                          station.Y - (float)(Math.Cos(radians) * reach));
                SKPoint opposite = new SKPoint(2 * station.X - tip.X, 2 * station.Y - tip.Y);
                WindBarb glyph = new WindBarb(angle, 10, WindBarb.BarbRule.WmoStandard);

                Assert.True(Distance(glyph.TipOf(station, Size), tip) <= 0.01d,
                            angle + "° 的公开 TipOf 与解析杆末端不符");

                using (ImageProbe probe = Render(300, 300, p => glyph.Draw(p, station, Size, BlackInk)))
                {
                    Assert.True(probe.HasInkInBox((int)tip.X - 6, (int)tip.Y - 6, (int)tip.X + 7, (int)tip.Y + 7),
                                angle + "° 的杆末端应在 (" + tip.X + "," + tip.Y + ") 附近有墨（顺时针口径）");
                    Assert.False(probe.HasInkInBox((int)opposite.X - 6, (int)opposite.Y - 6, (int)opposite.X + 7, (int)opposite.Y + 7),
                                 angle + "° 的反方向 " + opposite + " 不该有墨——有墨就是口径镜像了");
                }
            }

            // 跨域口径对账：与雷达方位角的极→直角换算逐角度比单位向量（docs/02 §8.4 同一套约定）
            RadarLayout layout = RadarLayout.Compute(new RadarDocument { MaxRange = 100d }, new RadarStyle());
            foreach (float angle in new[] { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f })
            {
                SKPoint far = layout.PointOf(angle, 100d);
                double dx = far.X - layout.Center.X;
                double dy = far.Y - layout.Center.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);
                Assert.True(length > 1d, "雷达版面半径异常，口径对照失去意义：" + length);
                SKPoint wind = WindBarb.DirectionVectorOf(angle);
                Assert.True(Math.Abs(wind.X - dx / length) <= 1e-3d,
                            angle + "° 的东西分量与雷达口径不同：风杆 " + wind.X + " vs 雷达 " + dx / length);
                Assert.True(Math.Abs(wind.Y - dy / length) <= 1e-3d,
                            angle + "° 的南北分量与雷达口径不同：风杆 " + wind.Y + " vs 雷达 " + dy / length);
            }
        }

        [Fact]
        public void 风杆_角度归一与单位向量按公开定义复算()
        {
            Assert.Equal(0d, WindBarb.NormalizeDegrees(360f), 5);
            Assert.Equal(10d, WindBarb.NormalizeDegrees(370f), 5);
            Assert.Equal(350d, WindBarb.NormalizeDegrees(-10f), 5);
            Assert.Equal(270d, new WindBarb(-90f, 10, WindBarb.BarbRule.WmoStandard).DirectionDegrees, 5);
            Assert.Equal(45d, new WindBarb(405f, 10, WindBarb.BarbRule.WmoStandard).DirectionDegrees, 5);

            // 方向向量：dx = sin(rad)、dy = -cos(rad) → 0°→(0,-1) 上、90°→(1,0) 右、180°→(0,1) 下、270°→(-1,0) 左
            AssertUnitVector(0f, 0d, -1d);
            AssertUnitVector(90f, 1d, 0d);
            AssertUnitVector(180f, 0d, 1d);
            AssertUnitVector(270f, -1d, 0d);
            AssertUnitVector(45f, Math.Sqrt(0.5d), -Math.Sqrt(0.5d));

            Assert.Throws<ArgumentNullException>(() => new WindBarb(0f, 10, null!));
            // 负风速按 0 处理（缺测/静风都是常态，不能画出一根指向别处的杆）
            Assert.Equal(0, new WindBarb(0f, -7, WindBarb.BarbRule.WmoStandard).Knots);
        }

        private static void AssertUnitVector(float degrees, double expectedX, double expectedY)
        {
            SKPoint actual = WindBarb.DirectionVectorOf(degrees);
            Assert.True(Math.Abs(actual.X - expectedX) <= 1e-4d,
                        degrees + "° 的 x 分量应为 " + expectedX + "，实到 " + actual.X);
            Assert.True(Math.Abs(actual.Y - expectedY) <= 1e-4d,
                        degrees + "° 的 y 分量应为 " + expectedY + "，实到 " + actual.Y);
        }

        [Fact]
        public void 风杆_羽旗数量与风速编码一致()
        {
            // size=100、0°、圆心 (100,200) → 杆末 y=72，羽旗自杆末端向站圈依次排（半旗→全旗→三角旗）：
            //   全旗长 0.34×100=34、半旗 0.5×34=17、相邻间距 0.16×100=16、线粗 0.07×100=7
            //   取两列数墨段：x=88（距杆 12，半旗也够得着）→ 段数 = 三角旗 + 全旗 + 半旗
            //                 x=75（距杆 25，只有全旗与三角旗够得着）→ 段数 = 三角旗 + 全旗
            // 编码现算（源码 Encode：自大到小整除、余数向下取整到半旗）：
            //   0→0/0/0（静风不落墨） 3→0/0/0（非静风但零羽旗） 5→0/0/1 10→0/1/0 44→0/4/0 47→0/4/1 50→1/0/0 65→1/1/1
            int[][] cases =
            {
                new[] { 0, 0, 0, 0 },    // knots, pennants, fulls, halves
                new[] { 3, 0, 0, 0 },
                new[] { 5, 0, 0, 1 },
                new[] { 10, 0, 1, 0 },
                new[] { 44, 0, 4, 0 },
                new[] { 47, 0, 4, 1 },
                new[] { 50, 1, 0, 0 },
                new[] { 65, 1, 1, 1 },
            };

            foreach (int[] c in cases)
            {
                int knots = c[0];
                WindBarb glyph = new WindBarb(0f, knots, WindBarb.BarbRule.WmoStandard);
                WindBarb.BarbCounts expected = new WindBarb.BarbCounts(c[1], c[2], c[3]);
                Assert.True(expected.Equals(glyph.Counts),
                            knots + " 节应编码为 " + expected + "，实到 " + glyph.Counts);

                using (ImageProbe probe = Render(200, 260, p => glyph.Draw(p, new SKPoint(100f, 200f), 100f, BlackInk)))
                {
                    if (glyph.IsCalm)
                    {
                        Assert.False(probe.HasInkInBox(0, 0, 200, 259), knots + " 节是静风：杆与羽旗都不该落墨");
                        continue;
                    }

                    int near = RunsInColumn(probe, 88, 60, 175);
                    int far = RunsInColumn(probe, 75, 60, 175);
                    Assert.True(near == c[1] + c[2] + c[3],
                                knots + " 节：距杆 12 的列上羽旗段数应等于 三角旗+全旗+半旗 = " + (c[1] + c[2] + c[3])
                                + "（" + expected + "），实到 " + near);
                    Assert.True(far == c[1] + c[2],
                                knots + " 节：距杆 25 的列上只有全旗与三角旗够得着，段数应为 " + (c[1] + c[2])
                                + "，实到 " + far);
                    Assert.True(RunsInColumn(probe, 100, 60, 190) == 1,
                                knots + " 节：杆身应是一条连续竖墨，实到 " + RunsInColumn(probe, 100, 60, 190) + " 段");
                    Assert.False(probe.HasInkInBox(0, 60, 60, 175), knots + " 节：超过全旗长 34 的位置不该有羽旗墨");
                    Assert.False(probe.HasInkInBox(0, 176, 200, 259), knots + " 节：杆根以下（站圈一侧）不该有墨");
                }
            }
        }

        [Fact]
        public void 风杆_编码规则参数与拒绝非法()
        {
            WindBarb.BarbRule metric = new WindBarb.BarbRule(20, 10, 0);
            Assert.Equal(0, metric.PennantKnots);
            Assert.True(metric.Encode(55).Equals(new WindBarb.BarbCounts(0, 2, 1)), "无三角旗规则下 55 → 2 全 + 1 半，实到 " + metric.Encode(55));

            WindBarb.BarbCounts calm = metric.Encode(0);
            Assert.True(calm.IsEmpty, "0 节分解出的三元组应全零");
            // 向下取整到半旗：规则 (20,10,0) 下 1 节凑不出任何一根羽旗 → 三元组全零（现状，见回报的疑似缺陷）
            Assert.True(metric.Encode(1).IsEmpty, "1 节在 (20,10,0) 规则下应凑不出羽旗，实到 " + metric.Encode(1));
            Assert.True(new WindBarb.BarbCounts(0, 0, 0).IsEmpty, "结构体 IsEmpty 判据：三项全 0 即静风");
            Assert.False(new WindBarb.BarbCounts(0, 0, 1).IsEmpty, "结构体 IsEmpty 判据：有半旗就不算静风");

            int pennants, fulls, halves;
            metric.Encode(30).Deconstruct(out pennants, out fulls, out halves);
            Assert.Equal(0, pennants);
            Assert.Equal(1, fulls);
            Assert.Equal(1, halves);
            Assert.Equal("0/1/1", metric.Encode(30).ToString());

            Assert.Throws<ArgumentOutOfRangeException>(() => new WindBarb.BarbRule(0, 5, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WindBarb.BarbRule(10, 0, 50));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, true, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DigitBox(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DigitBox(6, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DigitBox(6, 0.62f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StampRing(0f, true, true, 0f));
        }

        [Fact]
        public void 风杆_超量风速时羽旗越出杆根压进站圈区()
        {
            // 现状记录（不是期望）：7 面羽旗 × BarbSpacingFactor 0.16 = 1.12×size > ShaftLengthFactor 1.0×size，
            // 最内侧那面三角旗越过杆根（y=172）画到 y=184，即压进模板站圈（半径 0.28×size=28 → y∈[172,228]）的区域。
            // 147 节：pennants=2、fulls=4、halves=1 → 自杆末 72 起每面间隔 16 → 第 7 面在 168–184。
            WindBarb glyph = new WindBarb(0f, 147, WindBarb.BarbRule.WmoStandard);
            Assert.True(glyph.Counts.Equals(new WindBarb.BarbCounts(2, 4, 1)), "147 节分解异常，实到 " + glyph.Counts);

            using (ImageProbe probe = Render(200, 260, p => glyph.Draw(p, new SKPoint(100f, 200f), 100f, BlackInk)))
            {
                Extent ink = MeasureInk(probe, 0, 0, 200, 260);
                Assert.False(probe.HasInkInBox(0, 0, 200, 67), "上半羽旗之内不该有别的墨（口径核对）");
                Assert.True(ink.MaxY > 176,
                            "现状：最内三角旗越出杆根 172（可复算上界 184 = 72 + 7×16），实测墨迹底边 " + ink.MaxY);
                Assert.True(ink.MaxY <= 184 + SpanTolerance, "越界也应止于第 7 面羽旗的下边 184，实到 " + ink.MaxY);
                Assert.True(probe.HasInkInBox(78, 175, 95, 183),
                            "现状断言：杆根与站圈之间确实被羽旗占了墨（源码未做溢出截断）");
            }
        }

        [Fact]
        public void 风杆_色对与尺寸令牌生效()
        {
            WindBarb glyph = new WindBarb(0f, 10, WindBarb.BarbRule.WmoStandard);
            using (ImageProbe black = Render(200, 260, p => glyph.Draw(p, new SKPoint(100f, 200f), 100f, BlackInk)))
            using (ImageProbe blue = Render(200, 260, p => glyph.Draw(p, new SKPoint(100f, 200f), 100f, BlueInk)))
            {
                AssertAverageInk(black, new SKRect(0f, 0f, 200f, 259f), BlackInk, "风杆黑");
                AssertAverageInk(blue, new SKRect(0f, 0f, 200f, 259f), BlueInk, "风杆蓝");
            }

            // 墨迹跨度可闭式复算（0°、1 全旗）：
            //   纵向 = 杆长 size + 端部羽旗的半线粗 0.035×size = 1.035×size
            //   横向 = 全旗长 0.34×size + 杆身半线粗 0.035×size = 0.375×size
            Extent small = WindInk(60f);
            Extent large = WindInk(120f);
            Assert.False(small.IsEmpty, "size=60 没落墨");
            Close(small.Height, 1.035 * 60, 2);
            Close(small.Width, 0.375 * 60, 2);
            Close(large.Height, 1.035 * 120, 2);
            Close(large.Width, 0.375 * 120, 2);
            Assert.True(Math.Abs(large.Height / small.Height - 2d) <= 0.03d,
                        "size 翻倍后杆长应翻倍，实到 " + small.Height + " → " + large.Height);
        }

        private static Extent WindInk(float size)
        {
            WindBarb glyph = new WindBarb(0f, 10, WindBarb.BarbRule.WmoStandard);
            using (ImageProbe probe = Render(280, 260, p => glyph.Draw(p, new SKPoint(140f, 200f), size, BlackInk)))
            {
                return MeasureInk(probe, 0, 0, 280, 260);
            }
        }

        // ================================================================ DigitBox

        [Fact]
        public void 数格框_零间隙法定栅格_七条坚线与空心格()
        {
            // 6 格、BoxWidthFactor 0.62、GapFactor 0、size=100（size 就是格高）→
            //   单格宽 62、整排宽 6×62 = 372、格线粗 0.07×100 = 7
            //   anchor=(200,70) → 整排矩形 x∈[14,386]、y∈[20,120]
            //   竖边界 = 14 + 累计格宽 → 14/76/138/200/262/324/386 共 7 条（相邻格共用边线，故不重复描）
            DigitBox glyph = new DigitBox(6);
            Assert.Equal(372f, glyph.RowWidthOf(100f), 3);
            Assert.Equal(62f, glyph.BoxWidthOf(100f), 3);

            using (ImageProbe probe = Render(400, 140, p => glyph.Draw(p, new SKPoint(200f, 70f), 100f, BlackInk)))
            {
                IReadOnlyList<double> verticals = probe.StrongVerticalLines(5, 398, 30, 110, 0.9);
                Assert.True(verticals.Count == 7, "6 格栅格应有 7 条竖边界，实到 " + Join(verticals));
                double[] expected = { 14, 76, 138, 200, 262, 324, 386 };
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.True(Math.Abs(verticals[i] - expected[i]) <= SpanTolerance,
                                "第 " + i + " 条竖边界应在 x=" + expected[i] + "，实到 " + verticals[i]);
                }
                Assert.True(ImageProbe.Gaps(verticals).Count == 6, "7 条线交出 6 个格距，实到 " + Join(ImageProbe.Gaps(verticals)));
                foreach (double gap in ImageProbe.Gaps(verticals))
                {
                    Assert.True(Math.Abs(gap - 62d) <= SpanTolerance, "格距应等于 BoxWidthFactor×size=62，实到 " + gap);
                }

                IReadOnlyList<double> horizontals = probe.StrongHorizontalLines(10, 130, 25, 375, 0.9);
                Assert.True(horizontals.Count == 2, "上下两条横线，实到 " + Join(horizontals));
                Assert.True(Math.Abs(horizontals[0] - 20) <= SpanTolerance, "上横线在 y=20，实到 " + horizontals[0]);
                Assert.True(Math.Abs(horizontals[1] - 120) <= SpanTolerance, "下横线在 y=120，实到 " + horizontals[1]);

                // 符号只画格、不写字：每格内部必须是干净的（字由模板落）
                for (int i = 0; i < 6; i++)
                {
                    SKRect cell = glyph.BoxRect(new SKPoint(200f, 70f), 100f, i);
                    SKRect inner = new SKRect(cell.Left + 11f, cell.Top + 11f, cell.Right - 11f, cell.Bottom - 11f);
                    Assert.False(probe.HasInkInBox((int)inner.Left, (int)inner.Top, (int)inner.Right, (int)inner.Bottom),
                                 "第 " + i + " 格内部应干净（符号不落文字）");
                }

                Extent ink = MeasureInk(probe, 0, 0, 400, 139);
                Assert.False(ink.IsEmpty, "非空判据失败");
                Close(ink.Width, 372 + 7, 2);
                Close(ink.Height, 100 + 7, 2);
            }
        }

        [Fact]
        public void 数格框_有间隙版逐格描框与末格加宽()
        {
            // 3 格、BoxWidthFactor 0.62、GapFactor 0.2、size=100 → 格宽 62、间隙 20、整排 226
            // anchor=(140,70) → 整排 x∈[27,253]，三格矩形 [27,89]、[109,171]、[191,253]。
            // 间隙>0 时源码逐格 painter.Frame(格矩形)（DigitBox.cs:137-140），而 Frame 把整条描边
            // 收进矩形内（Painter.cs:187 起，内缩线粗一半 3.5），所以栅格墨线落在格边 +3.5 处：
            //   30.5/85.5｜112.5/167.5｜194.5/249.5
            //   → 同格两线间距 = 格宽 - 线粗 = 62-7 = 55
            //   → 邻格两线间距 = 间隙 + 线粗 = 20+7 = 27（墨线各自退让了半个线粗）
            // 实测 55/27 与该式一致（首轮写成 62/20 是按"线居中于格边"误推，已按实测口径改正）。
            DigitBox gapped = new DigitBox(3, 0.62f, 0.2f);
            Assert.Equal(226f, gapped.RowWidthOf(100f), 3);
            using (ImageProbe probe = Render(280, 140, p => gapped.Draw(p, new SKPoint(140f, 70f), 100f, BlackInk)))
            {
                IReadOnlyList<double> verticals = probe.StrongVerticalLines(5, 278, 30, 110, 0.9);
                Assert.True(verticals.Count == 6, "有间隙时逐格描框应交出 6 条竖线，实到 " + Join(verticals));
                double[] gaps = ToArray(ImageProbe.Gaps(verticals));
                Assert.True(Math.Abs(gaps[0] - 55) <= SpanTolerance, "格内两墨线间距 = 格宽 62 - 线粗 7 = 55，实到 " + gaps[0]);
                Assert.True(Math.Abs(gaps[2] - 55) <= SpanTolerance, "第 1 格同样交出 55，实到 " + gaps[2]);
                Assert.True(Math.Abs(gaps[4] - 55) <= SpanTolerance, "第 2 格同样交出 55，实到 " + gaps[4]);
                Assert.True(Math.Abs(gaps[1] - 27) <= SpanTolerance,
                            "邻格墨线间距 = 间隙 20 + 线粗 7 = 27（Frame 内缩所致），实到 " + gaps[1]);
                Assert.True(Math.Abs(gaps[3] - 27) <= SpanTolerance, "第二个间隙同样 27，实到 " + gaps[3]);
                // 两格之间真正干净的空带 = 格边 [89,109]（墨线退让后各让出 3.5 px）
                Assert.False(probe.HasInkInBox(92, 25, 106, 115), "两格之间的间隙带必须干净");
            }

            // 末格加宽（末位放校验位/字母）：3 格 → 62 + 62 + 62×1.5=93，整排 217
            // anchor=(130,60) → 整排 x∈[21.5,238.5]，竖线 21.5/83.5/145.5/238.5 → 间距 62/62/93
            DigitBox lastWider = new DigitBox(3, 0.62f, 0f, true);
            Assert.Equal(93f, lastWider.BoxWidthOf(100f, 2), 3);
            Assert.Equal(217f, lastWider.RowWidthOf(100f), 3);
            using (ImageProbe probe = Render(280, 120, p => lastWider.Draw(p, new SKPoint(130f, 60f), 100f, BlackInk)))
            {
                IReadOnlyList<double> verticals = probe.StrongVerticalLines(5, 278, 20, 100, 0.9);
                Assert.True(verticals.Count == 4, "3 格应有 4 条竖边界，实到 " + Join(verticals));
                double[] gaps = ToArray(ImageProbe.Gaps(verticals));
                Assert.True(Math.Abs(gaps[0] - 62) <= SpanTolerance, "首格宽 62，实到 " + gaps[0]);
                Assert.True(Math.Abs(gaps[1] - 62) <= SpanTolerance, "中格宽 62，实到 " + gaps[1]);
                Assert.True(Math.Abs(gaps[2] - 93) <= SpanTolerance, "末格按 LastBoxWidthFactor 1.5 加宽到 93，实到 " + gaps[2]);
            }
        }

        [Fact]
        public void 数格框_色对与尺寸令牌生效()
        {
            DigitBox glyph = new DigitBox(6);
            SKRect window = new SKRect(0f, 0f, 400f, 139f);
            using (ImageProbe black = Render(400, 140, p => glyph.Draw(p, new SKPoint(200f, 70f), 100f, BlackInk)))
            using (ImageProbe red = Render(400, 140, p => glyph.Draw(p, new SKPoint(200f, 70f), 100f, RedInk)))
            {
                AssertAverageInk(black, window, BlackInk, "数格框黑");
                AssertAverageInk(red, window, RedInk, "数格框红");
            }

            // size 就是格高：整排跨度 = RowWidthOf(size) + 线粗 0.07×size = 3.79×size
            Extent small = DigitInk(50f);
            Extent large = DigitInk(100f);
            Assert.False(small.IsEmpty, "size=50 没落墨");
            Close(small.Width, 3.79 * 50, 2);
            Close(small.Height, 1.07 * 50, 2);
            Close(large.Width, 3.79 * 100, 2);
            Close(large.Height, 1.07 * 100, 2);
            Assert.True(Math.Abs(large.Width / small.Width - 2d) <= 0.03d,
                        "size 翻倍后整排宽应翻倍（格子与字号同步走），实到 " + small.Width + " → " + large.Width);
        }

        private static Extent DigitInk(float size)
        {
            // 画布必须容得下最大档：size=100 时整排墨宽 3.79×100=379，
            // 首轮用 300 宽画布把右半边裁掉了（实测跨度 299 就是画布宽），改成 500×200、中心 (250,100)。
            DigitBox glyph = new DigitBox(6);
            using (ImageProbe probe = Render(500, 200, p => glyph.Draw(p, new SKPoint(250f, 100f), size, BlackInk)))
            {
                return MeasureInk(probe, 0, 0, 500, 200);
            }
        }

        [Fact]
        public void 数格框_越界格序号拒绝()
        {
            DigitBox glyph = new DigitBox(6);
            SKPoint anchor = new SKPoint(200f, 70f);
            Assert.Throws<ArgumentOutOfRangeException>(() => glyph.BoxCenterOf(anchor, 100f, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => glyph.BoxCenterOf(anchor, 100f, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => glyph.BoxRect(anchor, 100f, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => glyph.BoxWidthOf(100f, 6));
            Assert.Throws<ArgumentOutOfRangeException>(() => glyph.BoxCenterFromLeft(new SKPoint(0f, 0f), 100f, 9));

            // 公开取矩形方法与"逐格落字"的落位口径：中心版与左上版必须重合
            SKRect row = glyph.RowRect(anchor, 100f);
            SKPoint topLeft = new SKPoint(row.Left, row.Top);
            for (int i = 0; i < glyph.BoxCount; i++)
            {
                SKRect centered = glyph.BoxRect(anchor, 100f, i);
                SKRect fromLeft = glyph.BoxRectFromLeft(topLeft, 100f, i);
                Assert.True(Math.Abs(centered.Left - fromLeft.Left) <= 1e-4 &&
                            Math.Abs(centered.Top - fromLeft.Top) <= 1e-4,
                            "第 " + i + " 格两种锚点口径给出了不同矩形");
                SKPoint center = glyph.BoxCenterOf(anchor, 100f, i);
                Assert.True(Math.Abs(center.X - fromLeft.MidX) <= 1e-4, "格中心与格矩形不自洽");
            }
        }

        [Fact]
        public void 数格框_逐格落字经真实SVG管线仍是可检索文本()
        {
            const string Code = "110105";
            const float Size = 100f;
            SKPoint anchor = new SKPoint(200f, 70f);
            DigitBox glyph = new DigitBox(Code.Length);

            // 像素侧：带字/不带字各走一次 Chart 光栅管线，格心的墨只能来自字——
            // 排除了"格线本身混进取样窗"造成的假阳性。
            using (ImageProbe withText = RenderViaChart(new DigitRowTemplate(Code, anchor, Size, BlackInk, true)))
            using (ImageProbe gridOnly = RenderViaChart(new DigitRowTemplate(Code, anchor, Size, BlackInk, false)))
            {
                for (int i = 0; i < Code.Length; i++)
                {
                    SKRect cell = glyph.BoxRect(anchor, Size, i);
                    // 内缩 12/14 px 以避开格线墨带（线粗 7 → 每边只占 3.5 px）
                    SKRect inner = new SKRect(cell.Left + 12f, cell.Top + 14f, cell.Right - 12f, cell.Bottom - 14f);
                    int left = (int)inner.Left, top = (int)inner.Top, right = (int)inner.Right, bottom = (int)inner.Bottom;
                    Assert.True(CountInk(gridOnly, left, top, right, bottom) == 0,
                                "第 " + i + " 格的内窗在纯格线版里就该是干净的，否则取样窗没避开格线，实到 "
                                + CountInk(gridOnly, left, top, right, bottom) + " 个墨像素");
                    int ink = CountInk(withText, left, top, right, bottom);
                    Assert.True(ink >= 30, "第 " + i + " 格（字 '" + Code[i] + "'）内窗只读到 " + ink + " 个墨像素，字没落到格里");
                }
            }

            // 文本侧：走真实导出链 Chart.WriteSvg（录 SKPicture → SKSvgCanvas），
            // 数字必须是 <text> 节点内容，而不是被转成路径轮廓。
            using (Chart chart = new Chart(new ChartDocument(),
                                           new DigitRowTemplate(Code, anchor, Size, BlackInk, true), new ChartStyle()))
            using (MemoryStream stream = new MemoryStream())
            {
                chart.WriteSvg(stream);
                string svg = Encoding.UTF8.GetString(stream.ToArray());
                Assert.True(svg.Length > 500, "SVG 产物过小：" + svg.Length);
                Assert.True(svg.Contains("<path"), "格线没走矢量路径，SVG 里缺 path");
                Assert.True(svg.Contains("<text"), "SVG 里没有 text 节点——数字被转成了路径，不可检索（现状见回报的疑似缺陷）");

                MatchCollection matches = new Regex("<text[^>]*?>([^<]*?)</text>").Matches(svg);
                Assert.True(matches.Count == Code.Length,
                            "逐格落字应产出 " + Code.Length + " 个 text 节点，实到 " + matches.Count
                            + "；SVG 片段：" + TextFragment(svg));

                StringBuilder text = new StringBuilder();
                StringBuilder dump = new StringBuilder();
                for (int i = 0; i < matches.Count; i++)
                {
                    // SKSvgCanvas 在 <text> 里前后各排了换行/缩进（实测内容形如 "\n\t\t\t1\n\t"），
                    // 可检索性看的是数字本身，所以按 Trim 后的内容比对；原文一并留在失败消息里，
                    // 万一导出侧丢了字符，消息本身就是取证。
                    string raw = matches[i].Groups[1].Value;
                    dump.Append("[").Append(i).Append("]='").Append(Escape(raw)).Append("' ");
                    text.Append(raw.Trim());
                }
                Assert.True(text.ToString() == Code,
                            "逐格落字的 text 内容串起来应等于 '" + Code + "'，实到 '" + text
                            + "'；各节点原文：" + dump);
            }
        }

        private static string TextFragment(string svg)
        {
            int at = svg.IndexOf("<text", StringComparison.Ordinal);
            if (at < 0)
            {
                return svg.Substring(0, Math.Min(svg.Length, 400));
            }
            return svg.Substring(at, Math.Min(200, svg.Length - at));
        }

        /// <summary>把控制字符写成可见转义，便于在失败消息里看清 text 节点的原文。</summary>
        private static string Escape(string value)
        {
            return value.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        // ================================================================ TriStateBox

        [Fact]
        public void 三态框_勾选与划叉与空格在像素上互不相同()
        {
            // size=60 → 方框边长 60、anchor=(50,50) → 矩形 [20,80]²；线粗 0.09×60 = 5.4
            // 勾选两笔（CheckPoints 公开口径）：起笔 (20+0.16×60, 20+0.52×60)=(29.6,51.2)
            //   → 转折点 (20+0.40×60, 20+0.76×60)=(44,65.6) → 收笔 (80-0.14×60, 20+0.24×60)=(71.6,34.4)
            // 划叉两笔（CrossPoints）= 内缩 0.18×60=10.8 → (30.8,30.8)-(69.2,69.2) 与 (69.2,30.8)-(30.8,69.2)
            // 框心区分度的闭式核对：勾的第二笔直线过 (44,65.6) 与 (71.6,34.4)，单位法向 (0.749,0.663)，
            //   框心 (50,50) 到它的距离 = |(6,-15.6)·n| = 5.84 px，像素 (51,52) 是 3.78 px，
            //   而勾的半线粗只有 2.7（外加不到 0.5 的反锯齿过渡）→ 取 (47,47)-(52,52) 这 5×5 窗：
            //   勾选版必净、划叉版必墨（划叉正正穿过框心）。首轮窗取到 54，把 (52,52) 处 3.03 px 的
            //   勾身边缘也框了进去，属窗取大了，已按上式收窄。
            // 转折点取样窗还要躲开划叉：划叉的反对角线是 x+y=100，勾的转折点 (44,65.6) 在它外侧
            // （x+y=109.6，垂距 6.79 px）。取窗 (43,63)-(48,69) 时窗内最小 x+y = 106 →
            // 离划叉墨带边缘（100+2.7×1.414 = 103.8）仍留 2.2 px，划叉版在此窗必净、勾选版必墨。
            // 首轮用 (41,62) 起窗，其角点垂距仅 2.12 px，被 5.4 线粗的划叉笔蹭到了。
            TriStateBox uncheckedBox = new TriStateBox(TriState.Unchecked);
            TriStateBox checkedBox = new TriStateBox(TriState.Checked);
            TriStateBox notApplicable = new TriStateBox(TriState.NotApplicable);
            SKPoint anchor = new SKPoint(50f, 50f);

            const int elbowLeft = 43, elbowTop = 63, elbowRight = 48, elbowBottom = 69;
            const int centerLeft = 47, centerTop = 47, centerRight = 52, centerBottom = 52;

            using (ImageProbe none = Render(110, 110, p => uncheckedBox.Draw(p, anchor, 60f, BlackInk)))
            using (ImageProbe yes = Render(110, 110, p => checkedBox.Draw(p, anchor, 60f, BlackInk)))
            using (ImageProbe na = Render(110, 110, p => notApplicable.Draw(p, anchor, 60f, BlackInk)))
            {
                foreach (KeyValuePair<string, ImageProbe> pair in new[]
                {
                    new KeyValuePair<string, ImageProbe>("未勾选", none),
                    new KeyValuePair<string, ImageProbe>("已勾选", yes),
                    new KeyValuePair<string, ImageProbe>("不涉及", na),
                })
                {
                    ImageProbe probe = pair.Value;
                    Assert.True(probe.IsInk(50, 22) && probe.IsInk(50, 78), pair.Key + "：方框上/下边线应有墨");
                    Assert.True(probe.IsInk(22, 50) && probe.IsInk(78, 50), pair.Key + "：方框左/右边线应有墨");
                }

                Assert.False(none.HasInkInBox(elbowLeft, elbowTop, elbowRight, elbowBottom), "未勾选：框内不该有任何墨");
                Assert.False(none.HasInkInBox(centerLeft, centerTop, centerRight, centerBottom), "未勾选：框心必须干净");

                Assert.True(yes.HasInkInBox(elbowLeft, elbowTop, elbowRight, elbowBottom),
                            "已勾选：勾的转折点 (44,65.6) 按 CheckPoints 公开口径必有墨");
                Assert.False(yes.HasInkInBox(centerLeft, centerTop, centerRight, centerBottom),
                             "已勾选：勾不过框心（框心处干净才能与划叉区分）");
                Assert.True(yes.IsInk((int)Math.Round(20 + 60 * 0.16), 20 + (int)Math.Round(60 * 0.52)),
                            "已勾选：起笔点 (0.16,0.52) 处应有墨");

                Assert.True(na.HasInkInBox(centerLeft, centerTop, centerRight, centerBottom),
                            "不涉及：两笔对角线交于框心，框心必有墨");
                Assert.False(na.HasInkInBox(elbowLeft, elbowTop, elbowRight, elbowBottom),
                             "不涉及：划叉不经过勾选转折点，该窗必须干净才能与已勾选区分");
                Assert.True(na.IsInk(31, 31) && na.IsInk(69, 69) && na.IsInk(69, 31) && na.IsInk(31, 69),
                            "不涉及：四端点按 CrossPoints 公开口径（内缩 0.18×size）应有墨");

                Assert.True(yes.NonWhiteRatio(new SKRect(26f, 26f, 74f, 74f)) >
                            none.NonWhiteRatio(new SKRect(26f, 26f, 74f, 74f)), "勾选标记应真的往框内加了墨");
                Assert.True(na.NonWhiteRatio(new SKRect(26f, 26f, 74f, 74f)) > yes.NonWhiteRatio(new SKRect(26f, 26f, 74f, 74f)),
                            "两笔对角线在框内的墨量应多于一笔勾（现状断言）");
            }
        }

        [Fact]
        public void 三态框_色对与尺寸令牌生效()
        {
            TriStateBox glyph = new TriStateBox(TriState.Unchecked);
            SKRect window = new SKRect(0f, 0f, 200f, 200f);
            using (ImageProbe blue = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), 120f, BlueInk)))
            using (ImageProbe red = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), 120f, RedInk)))
            {
                AssertAverageInk(blue, window, BlueInk, "三态框蓝");
                AssertAverageInk(red, window, RedInk, "三态框红");
            }

            // Frame 把描边整条收进矩形内（Painter.Frame 按线粗一半内缩），
            // 所以墨迹跨度恰好等于 size：size 取行高时方框正好占满一行。
            Extent small = TriInk(60f);
            Extent large = TriInk(120f);
            Assert.False(small.IsEmpty, "size=60 没落墨");
            Close(small.Width, 60, 2);
            Close(small.Height, 60, 2);
            Close(large.Width, 120, 2);
            Close(large.Height, 120, 2);
            Assert.True(Math.Abs(large.Width / small.Width - 2d) <= 0.03d,
                        "边长令牌应线性生效，实到 " + small.Width + " → " + large.Width);
        }

        private static Extent TriInk(float size)
        {
            TriStateBox glyph = new TriStateBox(TriState.Unchecked);
            using (ImageProbe probe = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), size, BlackInk)))
            {
                return MeasureInk(probe, 0, 0, 200, 200);
            }
        }

        // ================================================================ StampRing

        [Fact]
        public void 圆章_外圈星与横杠落在可复算的位置()
        {
            // size=120、圆心 (100,100)：外圈半径 60、线粗 0.06×120 = 7.2 → 环墨带 r∈[56.4,63.6]
            // 五角星外径 0.17×120 = 20.4、内径 20.4×0.382 = 7.8 → 实心星覆盖圆心
            // 横杠：y = 100 + 0.26×120 = 131.2，半长 0.33×120 = 39.6 → x∈[60.4,139.6]
            StampRing glyph = new StampRing(0f);
            using (ImageProbe probe = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), 120f, RedInk)))
            {
                Assert.True(probe.IsInk(100, 40), "外圈顶部墨带 (100,100-60) 应有墨");
                Assert.True(probe.IsInk(100, 160), "外圈底部墨带应有墨");
                Assert.False(probe.IsInk(100, 30), "外圈外沿 63.6 之外不该有墨");
                Assert.False(probe.IsInk(100, 50), "环内与星之间的空带不该有墨");
                Assert.True(probe.IsInk(100, 100), "章心被实心五角星盖住，应有墨");

                IReadOnlyList<double> bar = probe.StrongHorizontalLines(124, 138, 75, 125, 0.9);
                Assert.True(bar.Count == 1, "章内横杠应是一条强横线，实到 " + Join(bar));
                Assert.True(Math.Abs(bar[0] - 131.2) <= 1.5d, "横杠应在 y = 圆心 + 0.26×size = 131.2，实到 " + bar[0]);

                SKPoint from, to;
                glyph.BarEnds(new SKPoint(100f, 100f), 120f, out from, out to);
                Assert.True(Math.Abs(from.Y - bar[0]) <= 1.5d && Math.Abs(from.X - 60.4f) <= 1.5d &&
                            Math.Abs(to.X - 139.6f) <= 1.5d,
                            "公开的 BarEnds 端点必须与像素上的横杠同源，实到 " + from + " / " + to);

                Assert.False(probe.HasInkInBox(95, 140, 106, 152), "横杠与外圈之间应是干净空带");

                // 外接矩形把描边也算进去：叠在表格上不越出版面的前提
                SKRect bounds = glyph.BoundsOf(new SKPoint(100f, 100f), 120f);
                Assert.True(Math.Abs(bounds.Left - (100 - 63.6)) <= 0.01f, "BoundsOf 应含描边，实到 " + bounds.Left);
                Extent ink = MeasureInk(probe, 0, 0, 200, 200);
                Assert.True(ink.MinX >= (int)Math.Floor(bounds.Left) - 1 && ink.MaxX <= (int)Math.Ceiling(bounds.Right) + 1 &&
                            ink.MinY >= (int)Math.Floor(bounds.Top) - 1 && ink.MaxY <= (int)Math.Ceiling(bounds.Bottom) + 1,
                            "墨迹越出了 BoundsOf：" + ink.MinX + "," + ink.MinY + "," + ink.MaxX + "," + ink.MaxY
                            + " vs " + bounds);
            }

            // 星与横杠是可关掉的构件：只留外圈时章心干净。
            // 干净内腔是半径 60-3.6=56.4 的圆盘，其内接正方形边长 56.4×√2 = 79.8 →
            // 取样窗必须落在 (60.1,139.9) 之内，取整用 (62,62)-(138,138)；
            // 首轮用 (50,50)-(150,150) 把 45° 方向上的环带角 (139.9,139.9) 也框了进去，是窗取大了。
            StampRing ringOnly = new StampRing(0f, false, false);
            using (ImageProbe probe = Render(200, 200, p => ringOnly.Draw(p, new SKPoint(100f, 100f), 120f, RedInk)))
            {
                Assert.False(probe.IsInk(100, 100), "withStar=false 时章心不该有墨");
                Assert.False(probe.HasInkInBox(62, 62, 138, 138), "只画外圈时干净内腔（内接正方形窗）必须全净");
                Assert.True(probe.IsInk(100, 40), "外圈仍在");
            }
        }

        [Fact]
        public void 圆章_倾角只作用于几何_横杠由横转竖而外圈不变()
        {
            // 正章横杠在 y = 圆心 + 0.26×size 上；转 90°（顺时针）后 BarEndsLocal 的两端
            // (±0.33size, +0.26size) 绕圆心转到 (-0.26size, ±0.33size) → 竖杠在 x = 圆心 - 31.2 上。
            const float Size = 120f;
            SKPoint anchor = new SKPoint(100f, 100f);
            StampRing upright = new StampRing(0f);
            StampRing tilted = new StampRing(90f);

            using (ImageProbe a = Render(200, 200, p => upright.Draw(p, anchor, Size, RedInk)))
            using (ImageProbe b = Render(200, 200, p => tilted.Draw(p, anchor, Size, RedInk)))
            {
                Assert.True(a.StrongHorizontalLines(124, 138, 75, 125, 0.9).Count == 1, "正章的横杠应在横向着色，实到 "
                            + a.StrongHorizontalLines(124, 138, 75, 125, 0.9).Count + " 条");
                Assert.True(b.StrongHorizontalLines(124, 138, 75, 125, 0.9).Count == 0, "转 90° 后横向已不该有强线，实到 "
                            + b.StrongHorizontalLines(124, 138, 75, 125, 0.9).Count + " 条");
                IReadOnlyList<double> vertical = b.StrongVerticalLines(60, 80, 75, 125, 0.9);
                Assert.True(vertical.Count == 1, "转 90° 后横杠应变坚线，实到 " + Join(vertical));
                Assert.True(Math.Abs(vertical[0] - (100 - 31.2)) <= 1.5d,
                            "竖杠应在 x = 圆心 - 0.26×size = 68.8，实到 " + vertical[0]);

                SKPoint from, to;
                tilted.BarEnds(anchor, Size, out from, out to);
                Assert.True(Math.Abs(from.X - to.X) <= 0.01f && Math.Abs(from.X - vertical[0]) <= 1.5d,
                            "公开 BarEnds 与像素竖杠不自洽：" + from + " / " + to + " vs " + vertical[0]);

                // 外圈是旋转不变的圆：整幅墨迹外接框在两种倾角下逐值相同
                Extent ia = MeasureInk(a, 0, 0, 200, 200);
                Extent ib = MeasureInk(b, 0, 0, 200, 200);
                Assert.True(string.Join(",", new[] { ia.MinX, ia.MaxX, ia.MinY, ia.MaxY }) ==
                            string.Join(",", new[] { ib.MinX, ib.MaxX, ib.MinY, ib.MaxY }),
                            "旋转污染了外圈：" + ia.MinX + "," + ia.MaxX + "," + ia.MinY + "," + ia.MaxY + " vs "
                            + ib.MinX + "," + ib.MaxX + "," + ib.MinY + "," + ib.MaxY);

                // 落字锚点跟着倾角走，但字本身仍直立（这里比点位，像素侧由下一条用例证明）
                SKPoint uprightText = upright.TextPointAt(anchor, Size, 0f);
                SKPoint tiltedText = tilted.TextPointAt(anchor, Size, 0f);
                float radius = upright.TextArcRadiusOf(Size);
                Assert.True(Math.Abs(radius - Size * 0.37f) <= 1e-3f, "基线圆半径 = 0.5-0.06-0.07 因子 = 0.37×size，实到 " + radius);
                Assert.True(radius < Size * 0.5f - Size * 0.06f / 2f, "基线圆必须让开外圈墨带，否则章名压在外圈上");
                Assert.True(Math.Abs(uprightText.X - 100) <= 1e-3 && Math.Abs(uprightText.Y - (100 - radius)) <= 1e-3,
                            "正章 0° 落字点应在圆心正上方，实到 " + uprightText);
                Assert.True(Math.Abs(tiltedText.X - (100 + radius)) <= 1e-3 && Math.Abs(tiltedText.Y - 100) <= 1e-3,
                            "转 90°（顺时针）后同一角度的落字点应在圆心右侧，实到 " + tiltedText);
            }
        }

        [Fact]
        public void 圆章_章面文字不随章旋转且色对尺寸令牌生效()
        {
            // docs/06 §3.3 RotateAround 验收口径：旋转闭包之后再画同一段文字，
            // 其墨迹外接框必须与完全不旋转时逐值相同。这里让章体倾 30°，字仍由模板直立落位。
            const string StampText = "已执行";
            SKPoint textAt = new SKPoint(190f, 160f);
            FontKey font = new FontKey(null, 24f);
            SKRect window = new SKRect(180f, 135f, 299f, 195f);

            Extent alone;
            using (ImageProbe probe = Render(300, 200, p => p.Text(StampText, textAt.X, textAt.Y, font, BlackInk)))
            {
                alone = MeasureInk(probe, 180, 135, 299, 195);
                Assert.False(alone.IsEmpty, "对照图里一个字都没落到，取样窗没对准文字");
            }

            StampRing tilted = new StampRing(30f);
            Extent withTiltedStamp;
            using (ImageProbe probe = Render(300, 200, p =>
            {
                tilted.Draw(p, new SKPoint(70f, 70f), 100f, BlackInk);
                p.Text(StampText, textAt.X, textAt.Y, font, BlackInk);
            }))
            {
                Assert.True(probe.NonWhiteRatio(new SKRect(10f, 10f, 130f, 130f)) > 0.05d, "章体本身没落墨，用例失去意义");
                withTiltedStamp = MeasureInk(probe, 180, 135, 299, 195);
            }

            Assert.True(string.Join(",", new[] { alone.MinX, alone.MaxX, alone.MinY, alone.MaxY }) ==
                        string.Join(",", new[] { withTiltedStamp.MinX, withTiltedStamp.MaxX, withTiltedStamp.MinY, withTiltedStamp.MaxY }),
                        "章体倾角污染了章面文字：" + alone.MinX + "," + alone.MaxX + "," + alone.MinY + "," + alone.MaxY
                        + " vs " + withTiltedStamp.MinX + "," + withTiltedStamp.MaxX + ","
                        + withTiltedStamp.MinY + "," + withTiltedStamp.MaxY);

            // 红章是这类章的常态：主色由调用方给红色，符号自己不做颜色决定
            StampRing glyph = new StampRing(0f);
            SKRect full = new SKRect(0f, 0f, 200f, 200f);
            using (ImageProbe red = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), 120f, RedInk)))
            using (ImageProbe black = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), 120f, BlackInk)))
            {
                AssertAverageInk(red, full, RedInk, "圆章红");
                AssertAverageInk(black, full, BlackInk, "圆章黑");
            }

            // 墨迹跨度 = size + 线粗 = 1.06×size（外圈描边居中在半径上）
            Extent small = StampInk(100f);
            Extent large = StampInk(160f);
            Assert.False(small.IsEmpty, "size=100 没落墨");
            Close(small.Width, 1.06 * 100, 2);
            Close(small.Height, 1.06 * 100, 2);
            Close(large.Width, 1.06 * 160, 2);
            Close(large.Height, 1.06 * 160, 2);
            Assert.True(Math.Abs(large.Width / small.Width - 1.6d) <= 0.03d,
                        "size 放大 1.6 倍后外圈直径应同步，实到 " + small.Width + " → " + large.Width);
        }

        private static Extent StampInk(float size)
        {
            StampRing glyph = new StampRing(0f);
            using (ImageProbe probe = Render(200, 200, p => glyph.Draw(p, new SKPoint(100f, 100f), size, BlackInk)))
            {
                return MeasureInk(probe, 0, 0, 200, 200);
            }
        }

        // ================================================================ 公共件

        /// <summary>
        /// 符号出图：录 <see cref="SKPicture"/> → <see cref="SKSurface"/> + <c>DrawPicture</c> → PNG。
        /// 96dpi 下设备像素与逻辑像素 1:1，所以期望值可直接由公开因子算出。
        /// </summary>
        private static ImageProbe Render(int widthPx, int heightPx, Action<Painter> draw)
        {
            using (SKPictureRecorder recorder = new SKPictureRecorder())
            {
                SKCanvas recordingCanvas = recorder.BeginRecording(new SKRect(0f, 0f, widthPx, heightPx));
                using (Painter painter = new Painter(recordingCanvas, new ChartStyle(), Chart.SharedFonts))
                {
                    draw(painter);
                }

                using (SKPicture? picture = recorder.EndRecording())
                {
                    Assert.NotNull(picture);
                    SKImageInfo info = new SKImageInfo(widthPx, heightPx, SKColorType.Bgra8888, SKAlphaType.Premul);
                    using (SKSurface? surface = SKSurface.Create(info))
                    {
                        Assert.NotNull(surface);
                        SKCanvas canvas = surface!.Canvas;
                        canvas.Clear(SKColors.White);
                        canvas.DrawPicture(picture!);
                        using (SKImage image = surface.Snapshot())
                        using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                        {
                            return ImageProbe.FromPng(data.ToArray());
                        }
                    }
                }
            }
        }

        private static ImageProbe RenderViaChart(IChartTemplate template)
        {
            using (Chart chart = new Chart(new ChartDocument(), template, new ChartStyle()))
            {
                return ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100));
            }
        }

        private static void AssertBuiltIn(string key, Type expectedType)
        {
            Assert.True(Glyphs.IsRegistered(key), "内置符号键未注册：" + key);
            IGlyph? glyph = Glyphs.Find(key);
            Assert.NotNull(glyph);
            Assert.True(expectedType == glyph!.GetType(),
                        key + " 的实现应是 " + expectedType.Name + "，实到 " + glyph.GetType().Name);
        }

        private static void AssertAverageInk(ImageProbe probe, SKRect window, SKColor expected, string label)
        {
            SKColor actual = probe.AverageInkColor(window);
            bool hasInk = actual.Red > 0 || actual.Green > 0 || actual.Blue > 0;
            Assert.True(hasInk, label + "：窗口内读不到任何墨，平均色无从比较");

            int dr = Math.Abs((int)actual.Red - expected.Red);
            int dg = Math.Abs((int)actual.Green - expected.Green);
            int db = Math.Abs((int)actual.Blue - expected.Blue);
            Assert.True(dr <= ColorTolerance && dg <= ColorTolerance && db <= ColorTolerance,
                        label + "：平均墨色 " + Describe(actual) + " 未落到声明色 " + Describe(expected)
                        + "（各道偏差 " + dr + "/" + dg + "/" + db + "，容差 " + ColorTolerance + "）");
        }

        private static string Describe(SKColor color)
        {
            return "(" + color.Red + "," + color.Green + "," + color.Blue + ")";
        }

        private sealed class Extent
        {
            public int MinX = int.MaxValue;
            public int MaxX = -1;
            public int MinY = int.MaxValue;
            public int MaxY = -1;
            public int Count;

            public bool IsEmpty { get { return Count == 0; } }

            /// <summary>端点跨度（设备像素，非列数）。</summary>
            public double Width { get { return MaxX - MinX; } }

            public double Height { get { return MaxY - MinY; } }
        }

        private static Extent MeasureInk(ImageProbe probe, int left, int top, int right, int bottom)
        {
            Extent extent = new Extent();
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    if (!probe.IsInk(x, y))
                    {
                        continue;
                    }
                    if (x < extent.MinX) { extent.MinX = x; }
                    if (x > extent.MaxX) { extent.MaxX = x; }
                    if (y < extent.MinY) { extent.MinY = y; }
                    if (y > extent.MaxY) { extent.MaxY = y; }
                    extent.Count++;
                }
            }
            return extent;
        }

        private static int CountInk(ImageProbe probe, int left, int top, int right, int bottom)
        {
            return MeasureInk(probe, left, top, right, bottom).Count;
        }

        /// <summary>某列上连续墨迹的段数（数羽旗、数环带都靠它）。</summary>
        private static int RunsInColumn(ImageProbe probe, int x, int fromY, int toY)
        {
            int runs = 0;
            bool inside = false;
            for (int y = Math.Max(0, fromY); y < Math.Min(probe.Height, toY); y++)
            {
                if (probe.IsInk(x, y))
                {
                    if (!inside)
                    {
                        runs++;
                        inside = true;
                    }
                }
                else
                {
                    inside = false;
                }
            }
            return runs;
        }

        private static double Distance(SKPoint a, SKPoint b)
        {
            double dx = a.X - b.X;
            double dy = a.Y - b.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static double[] ToArray(IReadOnlyList<double> values)
        {
            double[] result = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                result[i] = values[i];
            }
            return result;
        }

        private static string Join(IReadOnlyList<double> values)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(values[i].ToString("0.##"));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 跨度断言：实测像素跨度与由符号公开因子闭式算出的期望值之差落在容差内。
        /// 写成私有方法是因为 xUnit 2.9.2 既没有 <c>Assert.Close</c>，也没有带用户消息的
        /// <c>Assert.Equal</c> 重载；失败消息同时给出期望、容差与实到，本身就是测量记录。
        /// </summary>
        /// <param name="actual">实测值（设备像素，96dpi 下等于逻辑像素）。</param>
        /// <param name="expected">由公开因子复算出的期望值。</param>
        /// <param name="tolerance">容差（像素）。</param>
        private static void Close(double actual, double expected, double tolerance)
        {
            Assert.True(Math.Abs(actual - expected) <= tolerance,
                        "期望 " + expected.ToString("0.###") + " ± " + tolerance.ToString("0.###")
                        + "，实到 " + actual.ToString("0.###"));
        }

        // ================================================================ 走真实管线用的最小模板

        /// <summary>
        /// 数格落字模板：符号只画栅格，字符由模板用 <see cref="Painter.TextInBox"/> 逐格直立落位
        /// （<c>size</c> 即字号，与 <see cref="DigitBox"/> 的尺寸口径同一基准），
        /// 不进任何旋转闭包——这正是"逐格落字可检索"的用法本身。
        /// </summary>
        private sealed class DigitRowTemplate : IChartTemplate
        {
            private const float CanvasWidth = 400f;
            private const float CanvasHeight = 140f;

            private readonly string _code;
            private readonly SKPoint _anchor;
            private readonly float _size;
            private readonly SKColor _color;
            private readonly bool _drawDigits;

            public DigitRowTemplate(string code, SKPoint anchor, float size, SKColor color, bool drawDigits)
            {
                _code = code;
                _anchor = anchor;
                _size = size;
                _color = color;
                _drawDigits = drawDigits;
            }

            public string Key { get { return "test.t215.digit-row"; } }

            public string DisplayName { get { return "数格落字试版"; } }

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                SKRect bounds = new SKRect(0f, 0f, CanvasWidth, CanvasHeight);
                return new ChartGeometry(
                    bounds,
                    new SlotAxis(0f, 10f, 1, 1),
                    new LinearAxis("rule", 0d, 10d, 0f, CanvasHeight, 1d, 5d),
                    new LinearAxis[0],
                    new Band("header", new SKRect(0f, 0f, CanvasWidth, 4f), new BandRow[0]),
                    new Band("date", new SKRect(0f, 4f, CanvasWidth, 8f), new BandRow[0]),
                    bounds,
                    new LabelColumn[0],
                    new Band("cells", bounds, new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, CanvasHeight - 4f, CanvasWidth, CanvasHeight), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                DigitBox glyph = new DigitBox(_code.Length);
                glyph.Draw(painter, _anchor, _size, _color);
                if (!_drawDigits)
                {
                    return;
                }

                FontKey font = new FontKey(null, _size);
                for (int i = 0; i < _code.Length; i++)
                {
                    SKRect cell = glyph.BoxRect(_anchor, _size, i);
                    painter.TextInBox(char.ToString(_code[i]), cell, font, _color, CellAlign.Center, CellAlign.Center);
                }
            }
        }
    }
}
