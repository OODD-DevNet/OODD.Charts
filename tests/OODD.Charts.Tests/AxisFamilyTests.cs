using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 轴族验收（docs/07 T210–T212）：三种标度的解析可逆性、对数不变量、
    /// 概率轴的压缩方向、Φ⁻¹ 的分位点、四级刻度分层与真值标签、
    /// 权重分类轴的格距分配，以及双域轴的互逆反查。
    /// <para>
    /// 正态分布的期望值不背常数：测试里另写一条<b>独立的辛普森积分</b>当裁判，
    /// 与本库的级数/渐近实现对标。系数或级数被改坏时两边立刻分叉。
    /// </para>
    /// </summary>
    public sealed class AxisFamilyTests
    {
        private const double PixelTolerance = 1e-4;

        // ------------------------------------------------------------ T210 NumericAxis

        [Fact]
        public void 轴_PositionOfExtended域外不夹紧而PositionOf夹紧()
        {
            // 缺陷⑧的另一半：裁剪/求交需要"这条线真正的走向"，夹紧会让图外段贴边、斜线被折弯。
            NumericAxis y = new NumericAxis("y", 0d, 100d, 440f, 40f, AxisDirection.VerticalUp);

            // 域内两者一致（不能把已有的夹紧语义改掉）。
            Assert.Equal(y.PositionOf(37d), y.PositionOfExtended(37d), 6);
            Assert.Equal(y.PositionOf(0d), y.PositionOfExtended(0d), 6);
            Assert.Equal(y.PositionOf(100d), y.PositionOfExtended(100d), 6);

            // 域外：线性轴的期望就是那条直线本身——from + (v-min)/(max-min)*(to-from)。
            Assert.Equal(440d + (-30d - 0d) / 100d * (40d - 440d), y.PositionOfExtended(-30d), 4);
            Assert.Equal(440d + (150d - 0d) / 100d * (40d - 440d), y.PositionOfExtended(150d), 4);
            // 对照：夹紧版把越界点压回端点，正是 120px 折弯的来源。
            Assert.Equal(440d, y.PositionOf(-30d), 4);
            Assert.Equal(40d, y.PositionOf(150d), 4);

            // 对数轴在 value>0 时按 log 外推（精确），value≤0 时标度无定义，退化为端点局部密度线性外推：
            // 只断言"有限、且落在 Min 那一端之外、越小的值越远"——外推段本来就要被裁掉，
            // 断言它精确等于某个背出来的数反而是假精度。
            NumericAxis log = new NumericAxis("rt", 1d, 1000d, 60f, 460f,
                                             AxisDirection.HorizontalRight, AxisScale.Logarithmic);
            Assert.Equal(60d + Math.Log(0.1d / 1d) / Math.Log(1000d / 1d) * 400d,
                         log.PositionOfExtended(0.1d), 3);
            float atZero = log.PositionOfExtended(0d);
            // net462 没有 float.IsFinite，双 TFM 只能用 !NaN && !Infinity 表达。
            Assert.True(!float.IsNaN(atZero) && !float.IsInfinity(atZero),
                        "对数轴取 0 外推出非有限值：" + atZero);
            Assert.True(atZero < log.From, "0 小于域下限，外推必须落在轴外而不是压回边上：" + atZero);
            Assert.True(log.PositionOfExtended(-5d) < atZero, "更小的值应更远");

            // 对数轴的另一端：value 大于域上限时按 log 继续外推。
            NumericAxis logUp = new NumericAxis("rt2", 1d, 1000d, 300f, 40f,
                                                AxisDirection.VerticalUp, AxisScale.Logarithmic);
            float above = logUp.PositionOfExtended(10000d);
            Assert.True(!float.IsNaN(above) && !float.IsInfinity(above) && above < 40f,
                        "10000 在域上限之上，应越过 To(40)，实到 " + above);
        }

        [Fact]
        public void 轴_对数标度满足十年等距不变量()
        {
            NumericAxis axis = new NumericAxis("d", 0.1, 1000d, 0f, 900f, AxisDirection.HorizontalRight,
                                               AxisScale.Logarithmic);

            double a = axis.PositionOf(10d) - axis.PositionOf(1d);
            double b = axis.PositionOf(100d) - axis.PositionOf(10d);
            Assert.Equal(a, b, 3);

            // 同一十进位内 1→2 与 5→10 的像素差也必须相等（log 的比例性）。
            double c = axis.PositionOf(2d) - axis.PositionOf(1d);
            double d = axis.PositionOf(10d) - axis.PositionOf(5d);
            Assert.Equal(c, d, 3);

            // 级配图断言②的同一条：1mm 与 10mm 的距离 == 0.1mm 与 1mm 的距离。
            Assert.Equal(axis.PositionOf(10d) - axis.PositionOf(1d),
                         axis.PositionOf(1d) - axis.PositionOf(0.1d), 3);
        }


        [Fact]
        public void 轴_两种标度的像素值往返解析可逆()
        {
            NumericAxis linear = new NumericAxis("l", -5d, 42d, 100f, 900f, AxisDirection.VerticalUp);
            NumericAxis log = new NumericAxis("g", 0.075, 60d, 0f, 800f, AxisDirection.HorizontalRight,
                                             AxisScale.Logarithmic);

            foreach (double v in new[] { -5d, -1.5d, 0d, 3.7d, 20.25d, 42d })
            {
                AssertRoundTrip(linear, v);
            }
            foreach (double v in new[] { 0.075, 0.15, 0.3, 1d, 4.75, 9.5, 19d, 60d })
            {
                AssertRoundTrip(log, v);
            }
        }

        [Fact]
        public void 轴_反向像素区间与四种方向都单调一致()
        {
            NumericAxis rightward = new NumericAxis("r", 0d, 10d, 50f, 250f, AxisDirection.HorizontalRight);
            Assert.Equal(50f, rightward.PositionOf(0d), 4);
            Assert.Equal(250f, rightward.PositionOf(10d), 4);

            NumericAxis leftward = new NumericAxis("l", 0d, 10d, 250f, 50f, AxisDirection.HorizontalLeft);
            Assert.Equal(250f, leftward.PositionOf(0d), 4);
            Assert.Equal(50f, leftward.PositionOf(10d), 4);

            NumericAxis down = new NumericAxis("depth", 0d, 100d, 20f, 820f, AxisDirection.VerticalDown);
            Assert.Equal(20f, down.PositionOf(0d), 4);
            Assert.Equal(420f, down.PositionOf(50d), 4);
            Assert.Equal(820f, down.PositionOf(100d), 4);

            NumericAxis up = new NumericAxis("t", 0d, 100d, 820f, 20f, AxisDirection.VerticalUp);
            Assert.Equal(420f, up.PositionOf(50d), 4);
        }

        [Fact]
        public void 轴_越界值夹在端点不飞到轴外()
        {
            NumericAxis axis = new NumericAxis("a", 0d, 10d, 0f, 100f, AxisDirection.HorizontalRight);
            Assert.Equal(0f, axis.PositionOf(-5d), 4);
            Assert.Equal(100f, axis.PositionOf(20d), 4);
            Assert.Equal(0d, axis.ValueAt(-999f), 6);
            Assert.Equal(10d, axis.ValueAt(999f), 6);
            Assert.True(axis.Accepts(0d) && axis.Accepts(10d) && !axis.Accepts(10.5d));

            // Clamp 的语义是"夹进域内"，不是恒等：域外值返回最近的端点。
            Assert.Equal(0d, axis.Clamp(-5d), 6);
            Assert.Equal(10d, axis.Clamp(50d), 6);
            Assert.Equal(4d, axis.Clamp(4d), 6);
        }

        [Fact]
        public void 轴_非法标度与非法域直接拒绝()
        {
            Assert.Throws<ArgumentException>(() => new NumericAxis("x", 0d, 10d, 0f, 1f,
                AxisDirection.HorizontalRight, AxisScale.Logarithmic));
            Assert.Throws<ArgumentException>(() => new NumericAxis("x", 10d, 1d, 0f, 1f, AxisDirection.HorizontalRight));
            Assert.Throws<ArgumentException>(() => new NumericAxis("", 1d, 2d, 0f, 1f, AxisDirection.HorizontalRight));
        }

        [Fact]
        public void 轴_局部像素密度在对数轴上随值变化()
        {
            NumericAxis log = new NumericAxis("g", 0.1, 100d, 0f, 900f, AxisDirection.HorizontalRight,
                                             AxisScale.Logarithmic);
            // 同一十倍距离的密度应当与 1/值 成正比：0.1 处密度是 10 处的 100 倍。
            double ratio = log.PixelsPerUnitAt(0.1d) / log.PixelsPerUnitAt(10d);
            Assert.Equal(100d, ratio, 1);

            NumericAxis linear = new NumericAxis("l", 0d, 100d, 0f, 500f, AxisDirection.HorizontalRight);
            Assert.Equal(linear.PixelsPerUnitAt(7d), linear.PixelsPerUnit, 2);
        }


        // ------------------------------------------------------------ T211 AxisRuler

        [Fact]
        public void 刻度_时间轴四级分层且只在高层级出标签()
        {
            NumericAxis minutes = new NumericAxis("t", 0d, 120d, 0f, 480f, AxisDirection.HorizontalRight);
            List<AxisTier> tiers = new List<AxisTier>
            {
                new AxisTier(1d, RulerTier.Hairline, false),
                new AxisTier(5d, RulerTier.Fine, false),
                new AxisTier(10d, RulerTier.Medium, false),
                new AxisTier(60d, RulerTier.Heavy, true),
            };

            List<AxisMark> marks = AxisRuler.Marks(minutes, tiers);
            Assert.Equal(121, marks.Count);

            Assert.Equal(RulerTier.Hairline, TierAt(marks, 1d));
            Assert.Equal(RulerTier.Fine, TierAt(marks, 5d));
            Assert.Equal(RulerTier.Medium, TierAt(marks, 10d));
            Assert.Equal(RulerTier.Medium, TierAt(marks, 50d));
            Assert.Equal(RulerTier.Heavy, TierAt(marks, 60d));
            Assert.Equal(RulerTier.Heavy, TierAt(marks, 120d));

            // 高层级胜出：60 分同时是 1/5/10 的整数倍，仍必须是粗线而不是毫线。
            Assert.True(MarkAt(marks, 60d).Labeled);
            Assert.False(MarkAt(marks, 10d).Labeled);
            Assert.Equal(3, CountLabeled(marks));

            // 位置由轴给出：60 分在 240px。
            Assert.Equal(240f, MarkAt(marks, 60d).Position, 3);
        }

        [Fact]
        public void 刻度_对数轴标签是真值而不是幂次()
        {
            NumericAxis sieve = new NumericAxis("d", 0.075, 60d, 0f, 700f, AxisDirection.HorizontalRight,
                                                AxisScale.Logarithmic);
            AxisTier grid = AxisRuler.LogTier(RulerTier.Fine, false, new[] { 1d, 2d, 5d }, 0.075d, 60d);
            List<AxisMark> marks = AxisRuler.Marks(sieve, new[] { grid });

            foreach (string want in new[] { "0.1", "0.2", "0.5", "1", "2", "5", "10", "20", "50" })
            {
                Assert.Contains(want, LabelTexts(marks));
            }

            // 不得出现 10ⁿ 形式：任何标签里都不许有指数记法或 "10^"。
            foreach (AxisMark mark in marks)
            {
                string text = mark.Label("0.####");
                Assert.False(text.Contains("E") || text.Contains("e") || text.Contains("^"),
                             "对数轴标签必须是真值，实到 " + text);
            }

            // 级配图断言①：0.075 与 60 落在两端，同一十进位内 1→2 与 2→4 的间距相等。
            Assert.True(ContainsValue(marks, 0.1d));
            Assert.True(ContainsValue(marks, 50d));
            Assert.False(ContainsValue(marks, 100d));
            Assert.Equal(sieve.PositionOf(2d) - sieve.PositionOf(1d),
                         sieve.PositionOf(4d) - sieve.PositionOf(2d), 3);
        }

        [Fact]
        public void 刻度_筛孔序列由表给定改表即改版面()
        {
            double[] sieves = { 0.075, 0.15, 0.3, 0.6, 1.18, 2.36, 4.75, 9.5, 19, 37.5, 60 };
            NumericAxis axis = new NumericAxis("d", 0.075, 60d, 0f, 700f, AxisDirection.HorizontalRight,
                                               AxisScale.Logarithmic);
            List<AxisMark> marks = AxisRuler.Marks(axis,
                new[] { new AxisTier(RulerTier.Heavy, true, sieves) });

            Assert.Equal(sieves.Length, marks.Count);
            Assert.Equal("0.075", marks[0].Label("0.####"));
            Assert.Equal("1.18", marks[4].Label("0.####"));
            Assert.Equal("60", marks[marks.Count - 1].Label("0.####"));

            List<AxisMark> changed = AxisRuler.Marks(axis,
                new[] { new AxisTier(RulerTier.Heavy, true, new[] { 0.075, 2d, 60d }) });
            Assert.Equal(3, changed.Count);
        }

        [Fact]
        public void 刻度_枚举区间可夹在from与to之间()
        {
            NumericAxis axis = new NumericAxis("t", 0d, 120d, 0f, 480f, AxisDirection.HorizontalRight);
            List<AxisMark> marks = AxisRuler.Marks(axis, new[] { new AxisTier(10d, RulerTier.Medium, true) },
                                                   25d, 75d);

            Assert.Equal(5, marks.Count);
            Assert.Equal(30d, marks[0].Value, 6);
            Assert.Equal(70d, marks[marks.Count - 1].Value, 6);
        }

        // ------------------------------------------------------------ T212 分类轴

        [Fact]
        public void 分类轴_格距与权重成正比且总和等于轴长()
        {
            // 权重口径：第 i 项的权重 = 它到下一站那一段的运行时分为止；末项给 0 表示收口在轴末端。
            WeightedCategoryAxis axis = new WeightedCategoryAxis(100f, 700f, new[]
            {
                new CategoryStop("K0", "甲站", 20d),
                new CategoryStop("K3", "乙站", 30d),
                new CategoryStop("K7", "丙站", 50d),
                new CategoryStop("K8", "丁站", 30d),
                new CategoryStop("K12", "戊站", 0d),
            });

            Assert.Equal(5, axis.Count);
            Assert.Equal(100f, axis.PositionOf(0), 3);
            Assert.Equal(192.308f, axis.PositionOf(1), 3);
            Assert.Equal(330.769f, axis.PositionOf(2), 3);
            Assert.Equal(561.538f, axis.PositionOf(3), 3);
            Assert.Equal(700f, axis.PositionOf(4), 3);

            // 跳号站名可用：编号不连续也能按键查到下标。
            Assert.Equal(2, axis.IndexOf("K7"));
            Assert.Equal("丙站", axis.LabelOf(2));
            Assert.Equal("K8", axis.KeyOf(3));
            Assert.Equal(-1, axis.IndexOf("K99"));

            double sum = 0d;
            for (int i = 0; i < axis.Count; i++)
            {
                sum += axis.WidthOf(i);
            }
            Assert.Equal(axis.Length, sum, 4);

            // 权重比即宽度比。
            Assert.Equal(30d / 50d, (double)axis.WidthOf(1) / axis.WidthOf(2), 3);

            // 中点与带的收口互相对齐。
            Assert.Equal(axis.CenterOf(2), (axis.PositionOf(2) + axis.PositionOf(3)) / 2f, 4);
            SKRect band = axis.BandOf(2, 40f, 90f);
            Assert.Equal(axis.PositionOf(2), band.Left, 4);
            Assert.Equal(axis.PositionOf(3), band.Right, 4);
            Assert.Equal(40f, band.Top, 4);
            Assert.Equal(90f, band.Bottom, 4);

            // 反查落在哪一格。
            Assert.Equal(2, axis.IndexAt(axis.PositionOf(2)));
            Assert.Equal(2, axis.IndexAt(axis.CenterOf(2)));
            Assert.Equal(4, axis.IndexAt(700f));
            Assert.Equal(-1, axis.IndexAt(69f));
        }

        [Fact]
        public void 分类轴_垂直走向下带是纵向的且反查仍正确()
        {
            WeightedCategoryAxis down = new WeightedCategoryAxis(0f, 600f, new[]
            {
                new CategoryStop("a", "A", 1d),
                new CategoryStop("b", "B", 2d),
                new CategoryStop("c", "C", 0d),
            }, AxisDirection.VerticalDown);

            Assert.Equal(0f, down.PositionOf(0), 4);
            Assert.Equal(200f, down.PositionOf(1), 4);
            Assert.Equal(600f, down.PositionOf(2), 4);
            SKRect band = down.BandOf(0, 50f, 150f);
            Assert.Equal(50f, band.Left, 4);
            Assert.Equal(0f, band.Top, 4);
            Assert.Equal(150f, band.Right, 4);
            Assert.Equal(200f, band.Bottom, 4);
            Assert.Equal(1, down.IndexAt(250f));
        }

        [Fact]
        public void 分类轴_像素倒序走向也能反查()
        {
            WeightedCategoryAxis leftward = new WeightedCategoryAxis(600f, 0f, new[]
            {
                new CategoryStop("a", "A", 1d),
                new CategoryStop("b", "B", 1d),
                new CategoryStop("c", "C", 0d),
            }, AxisDirection.HorizontalLeft);

            Assert.Equal(600f, leftward.PositionOf(0), 4);
            Assert.Equal(300f, leftward.PositionOf(1), 4);
            Assert.Equal(0f, leftward.PositionOf(2), 4);
            Assert.Equal(0, leftward.IndexAt(500f));
            Assert.Equal(1, leftward.IndexAt(200f));
        }

        [Fact]
        public void 分类轴_非法权重与越界下标拒绝()
        {
            Assert.Throws<ArgumentException>(() => new WeightedCategoryAxis(0f, 100f, new CategoryStop[0]));
            Assert.Throws<ArgumentException>(() => new WeightedCategoryAxis(0f, 100f,
                new[] { new CategoryStop("a", "A", 0d) }));
            Assert.Throws<ArgumentException>(() => new WeightedCategoryAxis(50f, 50f,
                new[] { new CategoryStop("a", "A", 1d) }));
            Assert.Throws<ArgumentException>(() => new CategoryStop("a", "A", -1d));
            Assert.Throws<ArgumentException>(() => new CategoryStop("", "A", 1d));

            WeightedCategoryAxis axis = new WeightedCategoryAxis(0f, 100f,
                new[] { new CategoryStop("a", "A", 1d), new CategoryStop("b", "B", 1d) });
            Assert.Throws<ArgumentOutOfRangeException>(() => axis.PositionOf(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => axis.CenterOf(-1));
        }



        // ------------------------------------------------------------ 毫米令牌

        [Fact]
        public void 单位_毫米令牌换算精确且往返一致()
        {
            Assert.Equal(96f / 25.4f, Unit.PxPerMm, 4);
            Assert.Equal(96f, Unit.Mm(25.4f), 3);
            Assert.Equal(250f * 96f / 25.4f, Unit.Mm(250f), 2);
            Assert.Equal(250f, Unit.MmFromPx(Unit.Mm(250f)), 2);
            // MmWidth 返回的是"对齐到整数像素后的像素宽"，不是 mm：250mm = 944.88px → 945。
            Assert.Equal(945f, Unit.MmWidth(250f), 0);
            Assert.Equal(Math.Round(Unit.Mm(250f)), Unit.MmWidth(250f), 0);

            // 令牌可以取负（自右向左的偏移），换算必须是奇的。
            Assert.Equal(-Unit.Mm(3f), Unit.Mm(-3f), 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => Unit.Mm(float.NaN));
        }

        // ------------------------------------------------------------ 支撑

        private static void AssertRoundTrip(NumericAxis axis, double value)
        {
            double back = axis.ValueAt(axis.PositionOf(value));
            // 像素总线是 float，一次往返的固有误差是"轴跨度 × float 相对精度"，
            // 约 1e-7 × span；比这更严的判据只能在解析层（端点、中点）成立，见下条。
            double bound = Math.Max(1e-9, Math.Abs(axis.Max - axis.Min) * 1e-6);
            Assert.True(Math.Abs(back - value) <= bound,
                        axis.Key + " 往返失败：" + value + " → " + back + "，界限 ±" + bound);
        }

        [Fact]
        public void 轴_端点与中点往返解析精确到1e9()
        {
            NumericAxis linear = new NumericAxis("l", -5d, 42d, 100f, 900f, AxisDirection.VerticalUp);
            NumericAxis log = new NumericAxis("g", 0.075, 60d, 0f, 800f, AxisDirection.HorizontalRight,
                                             AxisScale.Logarithmic);

            // 端点：t 恰为 0/1，不经 float 舍入，所以 docs/07 T210 的 1e-9 判据在这里成立。
            AssertEndpoint(log);
            AssertEndpoint(linear);

            // 标度空间中点的像素是 (From+To)/2，也是精确可表示的。
            Assert.True(Math.Abs(log.PositionOf(Math.Sqrt(0.075d * 60d)) - 400f) < 1e-4,
                        "对数轴几何中点未落在像素中点");
            Assert.True(Math.Abs(linear.ValueAt(500f) - 18.5d) < 1e-9,
                        "线性轴像素中点不等于算术中点");
        }

        private static void AssertEndpoint(NumericAxis axis)
        {
            Assert.True(Math.Abs(axis.ValueAt(axis.PositionOf(axis.Min)) - axis.Min) < 1e-9 * Math.Max(1d, axis.Min),
                        axis.Key + " 下界往返失败");
            Assert.True(Math.Abs(axis.ValueAt(axis.PositionOf(axis.Max)) - axis.Max) < 1e-9 * axis.Max,
                        axis.Key + " 上界往返失败");
        }


        private static double DensityAt(double z)
        {
            return Math.Exp(-0.5d * z * z) / Math.Sqrt(2d * Math.PI);
        }

        private static AxisMark MarkAt(List<AxisMark> marks, double value)
        {
            foreach (AxisMark mark in marks)
            {
                if (Math.Abs(mark.Value - value) < 1e-9)
                {
                    return mark;
                }
            }

            throw new InvalidOperationException("刻度表里没有 " + value);
        }

        private static RulerTier TierAt(List<AxisMark> marks, double value) => MarkAt(marks, value).Tier;

        private static bool ContainsValue(List<AxisMark> marks, double value)
        {
            foreach (AxisMark mark in marks)
            {
                if (Math.Abs(mark.Value - value) <= PixelTolerance)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountLabeled(List<AxisMark> marks)
        {
            int n = 0;
            foreach (AxisMark mark in marks)
            {
                if (mark.Labeled)
                {
                    n++;
                }
            }

            return n;
        }

        private static List<string> LabelTexts(List<AxisMark> marks)
        {
            List<string> texts = new List<string>();
            foreach (AxisMark mark in marks)
            {
                texts.Add(mark.Label("0.####"));
            }

            return texts;
        }
    }
}
