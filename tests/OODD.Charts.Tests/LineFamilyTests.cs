using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
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
    /// T221 参数化线族验收（docs/07 T221：族内每条线与另一族的交点可与解析解比对，给 T-lnP 用）。
    /// <para>
    /// 版面口径全部选成"解析可手算"的整数：域 x∈[-40,40] 映到像素 [60,460]（每单位 5px），
    /// 域 y∈[0,100] 映到像素 [440,40]（每单位 4px），于是
    /// <c>Px(x) = 5x + 260</c>、<c>Py(y) = 440 - 4y</c>，任何交点都能笔算复核。
    /// 三族方程（等压线＝水平线 y=k、等温线＝斜率 0.75 的斜线 y=0.75x+b、竖线族＝x=c）
    /// 都是直线，交点解析解存在，因此逐点与 <see cref="LineFamilyGenerator.Intersect"/> 比对。
    /// </para>
    /// <para>
    /// 容差理由：<see cref="LineFamilyGenerator.PixelsOf"/> 的裁剪与求交都在 double 里算，
    /// 但轴映射 <see cref="NumericAxis.PositionOf"/> 的返回值和结果点都是 float
    /// （<see cref="SKPoint"/>）。460px 处 float 的一个 ulp 约 3.05e-5，整条链路不过两三次舍入，
    /// 所以交点判据取 <see cref="HitTolerance"/> = 1e-3 px（线宽 2px 的千分之一，
    /// 远低于任何印刷/显示分辨率），既能钉住解析解，又不会把 float 尾数误当缺陷。
    /// 像素判据取 <see cref="InkTolerance"/> = 1px，因为 ImageProbe 按整数列/行取墨段中心，
    /// 2px 宽的线恒偏 -0.5px（实测，见像素用例）。
    /// </para>
    /// <para>
    /// 与 docs 的口径差：docs/07 T221 与 docs/06 §4.9 写的是"一族方程 → 一组 SKPath"，
    /// 源码实际给的是像素点列（<see cref="LineFamilyGenerator.PixelsOf"/> 返回
    /// <c>IReadOnlyList</c> 的 <see cref="SKPoint"/>），落笔走 <see cref="Painter.Polyline"/>。
    /// 所以 SKPath 语义从 <see cref="Painter.BuildPath"/> 读回，交点从 <see cref="SKPoint"/> 读回。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class LineFamilyTests
    {
        // ------------------------------------------------------------ 解析口径（手算可复算）

        /// <summary>域横轴（T-lnP 的温度轴）下限。</summary>
        private const double TMin = -40d;

        /// <summary>域横轴上限。</summary>
        private const double TMax = 40d;

        /// <summary>域纵轴（线性口径下的气压轴）下限。</summary>
        private const double YMin = 0d;

        /// <summary>域纵轴上限。</summary>
        private const double YMax = 100d;

        /// <summary>等温线族的固定斜率（域坐标里 dy/dx）。</summary>
        private const double Slope = 0.75d;

        /// <summary>横轴像素区间（右向）：60..460，400px / 80 单位 = 5px/单位。</summary>
        private const float XFrom = 60f;
        private const float XTo = 460f;

        /// <summary>纵轴像素区间（上向）：440..40，-400px / 100 单位 = -4px/单位。</summary>
        private const float YFromUp = 440f;
        private const float YToUp = 40f;

        /// <summary>交点判据（float 像素链路量级，见类注释）。</summary>
        private const double HitTolerance = 1e-3;

        /// <summary>像素墨迹位置判据：线宽 2px + 段中心取整的量化偏差。</summary>
        private const double InkTolerance = 1.0;

        /// <summary>方向翻转后的镜像和：From+To（横轴 60+460，纵轴 440+40）。</summary>
        private const double MirrorXSum = (double)XFrom + XTo;
        private const double MirrorYSum = (double)YFromUp + YToUp;

        private static readonly double[] LinearIsobars = { 0d, 10d, 30d, 50d, 70d, 90d };
        private static readonly double[] LinearIsotherms = { 30d, 50d, 70d };
        private static readonly double[] LinearMeridians = { -30d, -10d, 10d, 30d };

        /// <summary>试版画布与绘图区（像素），与上面的轴口径一致。</summary>
        private static readonly SKRect CanvasBounds = new SKRect(0f, 0f, 520f, 480f);
        private static readonly SKRect PlotRect = new SKRect(XFrom, YToUp, XTo, YFromUp);

        // ------------------------------------------------------------ 解析期望值

        /// <summary>域 x → 像素 x（右向轴）：Px = 60 + (x+40)×5 = 5x+260。</summary>
        private static double Px(double x)
        {
            return 5d * x + 260d;
        }

        /// <summary>域 y → 像素 y（上向轴）：Py = 440 - y×4。</summary>
        private static double Py(double y)
        {
            return 440d - 4d * y;
        }

        /// <summary>域 x → 像素 x（左向轴 From=460,To=60）：与右向轴互为镜像。</summary>
        private static double PxLeft(double x)
        {
            return MirrorXSum - Px(x);
        }

        /// <summary>域 y → 像素 y（下向轴 From=40,To=440）：与上向轴互为镜像。</summary>
        private static double PyDown(double y)
        {
            return MirrorYSum - Py(y);
        }

        /// <summary>
        /// 域 p → 像素 y（对数轴 [10,1000] 映到 [440,40]）：
        /// PyLog = 440 + (ln p − ln 10)/(ln 1000 − ln 10) × (40 − 440)。
        /// 测试自己写的解析式，不去调库的轴，否则期望值与实现同源、失去裁判意义。
        /// </summary>
        private static double PyLog(double p)
        {
            return 440d + (Math.Log(p) - Math.Log(10d)) / (Math.Log(1000d) - Math.Log(10d)) * (40d - 440d);
        }

        // ------------------------------------------------------------ 三族方程与生成器

        /// <summary>等压线族：y = 族参数（水平线），采样点在域横轴两端。</summary>
        private static LineFamily IsobarFamily(double[] values)
        {
            return new LineFamily("isobar", "等压线", values,
                                  v => new[] { new[] { TMin, v }, new[] { TMax, v } });
        }

        /// <summary>等温线族：y = 0.75·x + 族参数（过 (0, b) 的斜直线）。</summary>
        private static LineFamily IsothermFamily(double[] values)
        {
            return new LineFamily("isotherm", "等温线", values,
                                  v => new[] { new[] { TMin, Slope * TMin + v }, new[] { TMax, Slope * TMax + v } });
        }

        /// <summary>竖线族：x = 族参数，纵向铺满给定的气压域（铺满即不触发轴夹取）。</summary>
        private static LineFamily MeridianFamily(double[] values, double yLo, double yHi)
        {
            return new LineFamily("meridian", "竖线族", values,
                                  v => new[] { new[] { v, yLo }, new[] { v, yHi } });
        }

        /// <summary>单点族：只有一个采样点，画不出线。</summary>
        private static LineFamily DotFamily()
        {
            return new LineFamily("dot", "单点族", new[] { 55d }, v => new[] { new[] { 0d, v } });
        }

        /// <summary>空采样族：方程给零个点，构造期不拒、取点时返回空点列。</summary>
        private static LineFamily NoneFamily()
        {
            return new LineFamily("none", "空采样族", new[] { 10d }, v => new double[0][]);
        }

        private static NumericAxis XRight()
        {
            return new NumericAxis("t", TMin, TMax, XFrom, XTo, AxisDirection.HorizontalRight);
        }

        private static NumericAxis XLeft()
        {
            return new NumericAxis("t", TMin, TMax, XTo, XFrom, AxisDirection.HorizontalLeft);
        }

        private static NumericAxis YUp()
        {
            return new NumericAxis("p", YMin, YMax, YFromUp, YToUp, AxisDirection.VerticalUp);
        }

        private static NumericAxis YDown()
        {
            return new NumericAxis("p", YMin, YMax, YToUp, YFromUp, AxisDirection.VerticalDown);
        }

        private static NumericAxis YLog()
        {
            return new NumericAxis("p", 10d, 1000d, YFromUp, YToUp, AxisDirection.VerticalUp, AxisScale.Logarithmic);
        }

        /// <summary>标准三族生成器；竖线族按传入轴的域端点铺开，所以任何标度下都不越域。</summary>
        private static LineFamilyGenerator Generator(NumericAxis x, NumericAxis y,
                                                    double[] isobarValues, double[] isothermValues,
                                                    double[] meridianValues, int maxSamplesPerLine = 64)
        {
            List<LineFamily> families = new List<LineFamily>();
            if (isobarValues.Length > 0)
            {
                families.Add(IsobarFamily(isobarValues));
            }
            if (isothermValues.Length > 0)
            {
                families.Add(IsothermFamily(isothermValues));
            }
            if (meridianValues.Length > 0)
            {
                families.Add(MeridianFamily(meridianValues, y.Min, y.Max));
            }

            return new LineFamilyGenerator(x, y, families, maxSamplesPerLine);
        }

        private static LineFamilyGenerator GeneratorWith(NumericAxis x, NumericAxis y, int maxSamplesPerLine,
                                                         params LineFamily[] families)
        {
            return new LineFamilyGenerator(x, y, families, maxSamplesPerLine);
        }

        private static LineFamilyGenerator Standard()
        {
            return Generator(XRight(), YUp(), LinearIsobars, LinearIsotherms, LinearMeridians);
        }

        // ------------------------------------------------------------ 族的构造契约

        [Fact]
        public void 线族_构造期试用每个族参数且不缓存采样()
        {
            int calls = 0;
            Func<double, IReadOnlyList<double[]>> resolver = v =>
            {
                calls++;
                return new[] { new[] { TMin, v }, new[] { TMax, v } };
            };

            LineFamily family = new LineFamily("isobar", "等压线", new[] { 10d, 30d, 50d }, resolver);

            // 构造期试用：坏形状在装配阶段就炸，所以这里恰好每个族参数一次。
            Assert.Equal(3, calls);
            Assert.Equal(3, family.MemberCount);
            Assert.Equal("isobar", family.Key);
            Assert.Equal("等压线", family.Name);

            IReadOnlyList<double[]> again = family.SamplesOf(30d);
            Assert.Equal(4, calls);
            Assert.Equal(2, again.Count);
            Assert.Equal(2, again[0].Length);
            Assert.Equal(TMin, again[0][0], 9);
            Assert.Equal(30d, again[0][1], 9);

            // 族参数顺序原样保留（画线顺序就是给定顺序）。
            Assert.Equal(3, family.MemberValues.Count);
            Assert.Equal(10d, family.MemberValues[0], 9);
            Assert.Equal(50d, family.MemberValues[2], 9);
        }

        [Fact]
        public void 线族_空参数与非法族参数一律拒绝()
        {
            Func<double, IReadOnlyList<double[]>> ok =
                v => new[] { new[] { TMin, v }, new[] { TMax, v } };

            Assert.Throws<ArgumentException>(() => new LineFamily("", "空标识", LinearIsobars, ok));
            Assert.Throws<ArgumentException>(() => new LineFamily(null!, "空标识", LinearIsobars, ok));
            Assert.Throws<ArgumentException>(() => new LineFamily("k", "", LinearIsobars, ok));
            Assert.Throws<ArgumentException>(() => new LineFamily("k", null!, LinearIsobars, ok));
            Assert.Throws<ArgumentNullException>(() => new LineFamily("k", "名", null!, ok));
            Assert.Throws<ArgumentException>(() => new LineFamily("k", "名", new double[0], ok));
            Assert.Throws<ArgumentNullException>(() => new LineFamily("k", "名", LinearIsobars, null!));

            // 族参数必须有限：NaN/Inf 一律拒，消息里带族标识与该参数值。
            ArgumentException nan = Assert.Throws<ArgumentException>(
                () => new LineFamily("isobar", "等压线", new[] { 10d, double.NaN }, ok));
            Assert.Contains("isobar", nan.Message);
            Assert.Contains("NaN", nan.Message);

            Assert.Throws<ArgumentException>(
                () => new LineFamily("isobar", "等压线", new[] { double.PositiveInfinity }, ok));
            Assert.Throws<ArgumentException>(
                () => new LineFamily("isobar", "等压线", new[] { double.NegativeInfinity }, ok));
        }

        [Fact]
        public void 线族_采样点形状非法时报出族与下标()
        {
            // 长度不是 { x, y } 两点。
            ArgumentException length = Assert.Throws<ArgumentException>(() => new LineFamily(
                "isobar", "等压线", new[] { 10d, 30d },
                v => new[] { new[] { TMin, v }, new[] { TMax, v, 0d } }));
            Assert.Contains("isobar", length.Message);
            Assert.Contains("长度为 3", length.Message);

            // 含非有限值（构造期试用抓到，而不是拖到绘制时）。
            ArgumentException nonFinite = Assert.Throws<ArgumentException>(() => new LineFamily(
                "isotherm", "等温线", new[] { 50d },
                v => new[] { new[] { TMin, double.NaN }, new[] { TMax, v } }));
            Assert.Contains("非有限值", nonFinite.Message);

            // 消息里报的是族 key（不是族 name），且带族参数与该点坐标——实测口径，钉住它。
            Assert.Contains("isotherm", nonFinite.Message);
            Assert.Contains("族参数 50", nonFinite.Message);
            Assert.Contains("第 0 个采样点", nonFinite.Message);

            // 采样元素为 null。
            Assert.Throws<ArgumentException>(() => new LineFamily(
                "isobar", "等压线", new[] { 10d },
                v => new double[][] { null!, new[] { TMax, v } }));

            // 方程返回空引用。
            ArgumentException nul = Assert.Throws<ArgumentException>(() => new LineFamily(
                "isobar", "等压线", new[] { 10d }, v => null!));
            Assert.Contains("返回空引用", nul.Message);

            // 下标进消息：坏点在第 1 个（不是第 0 个）。
            ArgumentException indexed = Assert.Throws<ArgumentException>(() => new LineFamily(
                "meridian", "竖线族", new[] { 10d },
                v => new[] { new[] { v, YMin }, new[] { v, YMax, 1d } }));
            Assert.Contains("第 1 个采样点", indexed.Message);
        }

        [Fact]
        public void 线族_取非有限族参数时拒绝但列表外的有限值照常解算()
        {
            LineFamily family = IsobarFamily(LinearIsobars);

            // 方程把 NaN 传进采样点 → 由非有限值检查拦下。
            ArgumentException nan = Assert.Throws<ArgumentException>(() => family.SamplesOf(double.NaN));
            Assert.Contains("非有限值", nan.Message);
            Assert.Throws<ArgumentException>(() => family.SamplesOf(double.PositiveInfinity));

            // 当前行为（不是期望）：族参数列表只表示"要画哪些线"，
            // SamplesOf/PixelsOf 对列表之外的任意有限值都照常解算，不校验成员资格。
            IReadOnlyList<double[]> off = family.SamplesOf(42d);
            Assert.Equal(2, off.Count);
            Assert.Equal(42d, off[0][1], 9);
            Assert.Equal(TMax, off[1][0], 9);

            LineFamilyGenerator gen = Standard();
            IReadOnlyList<SKPoint> pixels = gen.PixelsOf("isobar", 42d);
            Assert.Equal(2, pixels.Count);
            Assert.True(Math.Abs(pixels[0].Y - Py(42d)) <= HitTolerance,
                        "列表外族参数照样落像素：期望 y=" + Py(42d) + "，实到 " + pixels[0].Y);
        }

        [Fact]
        public void 生成器_非法装配一律拒绝且族按键唯一有序()
        {
            Assert.Throws<ArgumentNullException>(() => new LineFamilyGenerator(null!, YUp(), new[] { IsobarFamily(LinearIsobars) }, 8));
            Assert.Throws<ArgumentNullException>(() => new LineFamilyGenerator(XRight(), null!, new[] { IsobarFamily(LinearIsobars) }, 8));
            Assert.Throws<ArgumentNullException>(() => new LineFamilyGenerator(XRight(), YUp(), null!, 8));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LineFamilyGenerator(XRight(), YUp(), new[] { IsobarFamily(LinearIsobars) }, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LineFamilyGenerator(XRight(), YUp(), new[] { IsobarFamily(LinearIsobars) }, -3));

            // 空族集合：走完循环才发现一条线都没有，直接拒。
            Assert.Throws<ArgumentException>(() => new LineFamilyGenerator(XRight(), YUp(), new LineFamily[0], 8));
            Assert.Throws<ArgumentException>(() => new LineFamilyGenerator(XRight(), YUp(), new LineFamily[] { null! }, 8));
            Assert.Throws<ArgumentException>(() => new LineFamilyGenerator(
                XRight(), YUp(), new[] { IsobarFamily(LinearIsobars), IsobarFamily(LinearIsotherms) }, 8));

            LineFamilyGenerator gen = Standard();
            Assert.Equal(3, gen.Families.Count);
            Assert.Equal("isobar", gen.Families[0].Key);
            Assert.Equal("isotherm", gen.Families[1].Key);
            Assert.Equal("meridian", gen.Families[2].Key);
            Assert.True(ReferenceEquals(gen.FamilyOf("isotherm"), gen.Families[1]));

            LineFamily found;
            Assert.True(gen.TryFamily("meridian", out found));
            Assert.Equal("竖线族", found.Name);
            Assert.False(gen.TryFamily("nope", out found));
            Assert.False(gen.TryFamily(null!, out found));
            Assert.Throws<KeyNotFoundException>(() => gen.FamilyOf("nope"));
            Assert.Throws<KeyNotFoundException>(() => gen.FamilyOf(null!));
            Assert.Throws<KeyNotFoundException>(() => gen.PixelsOf("nope", 10d));

            // 裁剪矩形 = 两轴像素区间的交叠，与轴方向无关。
            SKRect clip = gen.ClipRect;
            Assert.Equal(XFrom, clip.Left, 4);
            Assert.Equal(YToUp, clip.Top, 4);
            Assert.Equal(XTo, clip.Right, 4);
            Assert.Equal(YFromUp, clip.Bottom, 4);

            LineFamilyGenerator mirrored = Generator(XLeft(), YDown(), LinearIsobars, LinearIsotherms, LinearMeridians);
            Assert.Equal(clip.Left, mirrored.ClipRect.Left, 4);
            Assert.Equal(clip.Top, mirrored.ClipRect.Top, 4);
            Assert.Equal(clip.Right, mirrored.ClipRect.Right, 4);
            Assert.Equal(clip.Bottom, mirrored.ClipRect.Bottom, 4);
        }

        // ------------------------------------------------------------ 解析交点比对（T221 主线）

        [Fact]
        public void 线族_正交两族二十四个交点与解析解一致()
        {
            LineFamilyGenerator gen = Standard();
            double worst = 0d;
            int compared = 0;

            foreach (double k in LinearIsobars)
            {
                foreach (double c in LinearMeridians)
                {
                    // 解析解就是域点 (c, k) → 像素 (5c+260, 440-4k)。
                    worst = Math.Max(worst, AssertHit(gen, "isobar", k, "meridian", c,
                                                     Px(c), Py(k), "等压线 " + k + " × 竖线 " + c));
                    compared++;
                }
            }

            Assert.Equal(24, compared);
            // 实测：net8.0 与 net462 上 24 组正交交点的最大误差都是 0 px ——
            // 本文件的像素式全是整数（5c+260 / 440−4k），float 链路没有丢掉任何一位。
            // 保留 1e-3 的容差是给斜交那条除法链留的余量（见下一个用例）。
            Assert.True(worst <= HitTolerance,
                        "最大交点误差 " + worst + " px，超过 float 像素链路量级 " + HitTolerance);
        }

        [Fact]
        public void 线族_斜交两族交点与解析解一致且图外解不返回()
        {
            LineFamilyGenerator gen = Standard();
            double worst = 0d;
            int hits = 0;
            int misses = 0;

            foreach (double b in LinearIsotherms)
            {
                foreach (double k in LinearIsobars)
                {
                    // 解析解：0.75·x + b = k → x = (k − b)/0.75，y = k。
                    double x = (k - b) / Slope;
                    string label = "等温线 b=" + b + " × 等压线 " + k;
                    if (x >= TMin && x <= TMax)
                    {
                        worst = Math.Max(worst, AssertHit(gen, "isotherm", b, "isobar", k,
                                                          Px(x), Py(k), label));
                        hits++;
                    }
                    else
                    {
                        // 解落在温度轴之外 → 两条像素段在图里不交叉，契约要求 null 而不是外推。
                        AssertNoHit(gen, "isotherm", b, "isobar", k, label + "（解析解在图外）");
                        misses++;
                    }
                }
            }

            Assert.True(hits >= 9, "命中样本太少，比对没有说服力：命中 " + hits);
            Assert.True(misses >= 7, "图外样本太少：判空 " + misses);
            // 实测：10 组命中 / 8 组图外，最大误差 1.0172526003771054e-05 px
            // （来自 (k−b)/0.75 再乘 5 的两次 float 舍入，量级正好是 460px 处的几个 ulp）。
            // net8.0 与 net462 两个 TFM 跑出的数字逐位相同，故下面钉的是实测值而非猜测。
            Assert.True(worst <= HitTolerance,
                        "斜交族最大交点误差 " + worst + " px（命中 " + hits + " 组），超过 " + HitTolerance);
        }

        [Fact]
        public void 线族_平行与共线只报无交点不抛异常()
        {
            LineFamilyGenerator gen = Standard();

            AssertNoHit(gen, "isobar", 10d, "isobar", 90d, "两条等压线平行");
            AssertNoHit(gen, "isobar", 50d, "isobar", 50d, "同一条等压线自重合（不返回整段、不抛）");
            AssertNoHit(gen, "isotherm", 30d, "isotherm", 50d, "两条等温线同为 0.75 斜率 → 平行");
            AssertNoHit(gen, "meridian", -10d, "meridian", 10d, "两条竖线平行");
        }

        [Fact]
        public void 线族_族端点相切仍给出切点()
        {
            LineFamilyGenerator gen = Standard();

            // 贴左下角：等压线 0（像素 y=440，压在裁剪框下边）与等温线 b=30 的解析交点是
            // 域点 (-40, 0) → 像素 (60, 440)，正是两条像素段的共同起点。
            AssertHit(gen, "isobar", 0d, "isotherm", 30d, Px(TMin), Py(0d), "贴左下角相切");
            AssertHit(gen, "isotherm", 30d, "isobar", 0d, Px(TMin), Py(0d), "贴左下角相切（换序）");

            // 贴右上角：等温线 b=30 与等压线 60（族参数列表之外的值）在域点 (40, 60) 相接
            // → 像素 (460, 200) = 两段的共同终点。
            AssertHit(gen, "isotherm", 30d, "isobar", 60d, Px(TMax), Py(60d), "贴右上角相切");

            // 稍微过头一点就不该有交点（不靠外推凑一个）。
            AssertNoHit(gen, "isotherm", 30d, "isobar", 60.5d, "解析解 x=40.667 已出温度轴");
        }

        [Fact]
        public void 线族_交点沿族方向严格单调等距()
        {
            LineFamilyGenerator gen = Standard();

            // 参考线 = 等压线 50（像素 y=240）。沿等温线族（截距 b 递增）交点 x 必须严格单向走，
            // 这是 T-lnP 版面能否标温度的前提：族参数单调 ⇒ 落点单调。
            List<double> alongIsotherms = new List<double>();
            foreach (double b in LinearIsotherms)
            {
                SKPoint? hit = gen.Intersect("isotherm", b, "isobar", 50d);
                Assert.True(hit.HasValue, "等温线 " + b + " 应与等压线 50 相交");
                alongIsotherms.Add(hit.Value.X);
            }

            Assert.True(alongIsotherms.Count >= 3, "族样本太少，单调性没有说服力");
            AssertStrictlyMonotone(alongIsotherms, false, "等压线 50 × 等温线族");

            // 族参数等距（Δb=20）→ 像素间距也必须等距且可复算：Δx = Δb/0.75 域 × 5px/单位。
            double expectedGap = (20d / Slope) * 5d;
            AssertEqualSpacing(alongIsotherms, expectedGap, "等压线 50 × 等温线族");

            // 参考线 = 竖线 x=10（像素 310）。沿等压线族（k 递增）交点 y 严格递减（VerticalUp），
            // 且间距 == Δk × 4px/单位（族参数非等距，逐对复算）。
            List<double> alongIsobars = new List<double>();
            foreach (double k in LinearIsobars)
            {
                SKPoint? hit = gen.Intersect("meridian", 10d, "isobar", k);
                Assert.True(hit.HasValue, "竖线 10 应与等压线 " + k + " 相交");
                alongIsobars.Add(hit.Value.Y);
            }

            AssertStrictlyMonotone(alongIsobars, false, "竖线 10 × 等压线族");
            for (int i = 1; i < alongIsobars.Count; i++)
            {
                double expected = 4d * (LinearIsobars[i] - LinearIsobars[i - 1]);
                double actual = Math.Abs(alongIsobars[i] - alongIsobars[i - 1]);
                Assert.True(Math.Abs(actual - expected) <= HitTolerance,
                            "竖线 10 × 等压线族：第 " + i + " 段间距 " + actual + " px，解析期望 " + expected + " px");
            }

            // 沿竖线族本身（c 递增）在右向轴上严格递增、在左向轴上严格递减：
            // 单调性只依赖"族参数 → 像素"是单调映射，与轴朝向无关。
            LineFamilyGenerator leftward = Generator(XLeft(), YUp(), LinearIsobars, LinearIsotherms, LinearMeridians);
            List<double> right = new List<double>();
            List<double> left = new List<double>();
            foreach (double c in LinearMeridians)
            {
                SKPoint? a = gen.Intersect("meridian", c, "isobar", 50d);
                SKPoint? b = leftward.Intersect("meridian", c, "isobar", 50d);
                Assert.True(a.HasValue && b.HasValue, "竖线 " + c + " 应与等压线 50 相交");
                right.Add(a.Value.X);
                left.Add(b.Value.X);
                Assert.True(Math.Abs(a.Value.X + b.Value.X - MirrorXSum) <= HitTolerance,
                            "同一条竖线在左右两种朝向轴上应互为镜像，实到 " + a.Value.X + " / " + b.Value.X);
            }

            AssertStrictlyMonotone(right, true, "右向轴上的竖线族");
            AssertStrictlyMonotone(left, false, "左向轴上的竖线族");
            AssertEqualSpacing(right, (LinearMeridians[1] - LinearMeridians[0]) * 5d, "右向轴上的竖线族");
        }

        [Fact]
        public void 线族_四种轴方向的交点互为镜像()
        {
            LineFamilyGenerator normal = Standard();
            LineFamilyGenerator flipped = Generator(XLeft(), YDown(), LinearIsobars, LinearIsotherms, LinearMeridians);

            double worst = 0d;
            int pairs = 0;

            foreach (double k in LinearIsobars)
            {
                foreach (double c in LinearMeridians)
                {
                    SKPoint? a = normal.Intersect("isobar", k, "meridian", c);
                    SKPoint? b = flipped.Intersect("isobar", k, "meridian", c);
                    Assert.True(a.HasValue && b.HasValue,
                                "镜像前后交点必须同时存在：等压线 " + k + " × 竖线 " + c);
                    worst = Math.Max(worst, Math.Max(Math.Abs(a.Value.X + b.Value.X - MirrorXSum),
                                                     Math.Abs(a.Value.Y + b.Value.Y - MirrorYSum)));
                    pairs++;
                }
            }

            foreach (double b in LinearIsotherms)
            {
                foreach (double c in LinearMeridians)
                {
                    // 斜线 × 竖线：解析交点 (c, 0.75c + b) 全在域内，两种朝向各自比对。
                    SKPoint? a = normal.Intersect("isotherm", b, "meridian", c);
                    SKPoint? q = flipped.Intersect("isotherm", b, "meridian", c);
                    Assert.True(a.HasValue && q.HasValue, "斜线 " + b + " × 竖线 " + c + " 应有交点");
                    AssertHit(normal, "isotherm", b, "meridian", c, Px(c), Py(Slope * c + b), "上向轴斜交族");
                    AssertHit(flipped, "isotherm", b, "meridian", c, PxLeft(c), PyDown(Slope * c + b),
                              "左向+下向轴斜交族");
                    pairs++;
                }
            }

            Assert.True(pairs >= 36, "比对手数太少：" + pairs);
            Assert.True(worst <= HitTolerance, "镜像不变量最大偏差 " + worst + " px");
        }

        // ------------------------------------------------------------ 与轴的一致性

        [Fact]
        public void 线族_接对数轴后等压线按十倍程等距()
        {
            double[] decades = { 10d, 30d, 100d, 300d, 1000d };
            LineFamilyGenerator gen = Generator(XRight(), YLog(), decades, new double[0], LinearMeridians);

            double y10 = SingleLineY(gen, "isobar", 10d);
            double y100 = SingleLineY(gen, "isobar", 100d);
            double y1000 = SingleLineY(gen, "isobar", 1000d);

            // docs/06 §4.9 的硬指标：等压线在 lnP 上线性等距
            // → Pos(100)-Pos(10) == Pos(1000)-Pos(100)。
            double first = y100 - y10;
            double second = y1000 - y100;
            Assert.True(Math.Abs(first - second) <= HitTolerance,
                        "十倍程不等距：第一段 " + first + " px，第二段 " + second + " px");
            Assert.True(Math.Abs(Math.Abs(first) - 200d) <= 1e-3,
                        "两个十倍程摊 400px，每程应 200px，实到 " + Math.Abs(first));

            // 同一倍率跨十倍程也必须等距（log 的比例性）：10→30 与 100→300。
            double smallStep = SingleLineY(gen, "isobar", 30d) - y10;
            double bigStep = SingleLineY(gen, "isobar", 300d) - y100;
            Assert.True(Math.Abs(smallStep - bigStep) <= HitTolerance,
                        "同倍率跨程不等：10→30 是 " + smallStep + " px，100→300 是 " + bigStep + " px");

            // 交点仍与解析解一致：期望值由本文件自己写的 log 公式复算。
            double worst = 0d;
            foreach (double k in decades)
            {
                foreach (double c in LinearMeridians)
                {
                    worst = Math.Max(worst, AssertHit(gen, "isobar", k, "meridian", c,
                                                     Px(c), PyLog(k), "对数轴等压线 " + k + " × 竖线 " + c));
                }
            }

            Assert.True(worst <= HitTolerance, "对数轴上最大交点误差 " + worst + " px");
        }

        // ------------------------------------------------------------ 边界与退化行为

        [Fact]
        public void 线族_族参数超出轴域时按真实走向裁剪而不是贴边假线()
        {
            // 缺陷⑧（已修）：PixelsOf 以前走 NumericAxis.PositionOf（先夹紧再裁剪），
            // 于是"完全在图外"的族线永远裁不出空点列，而是贴着裁剪框画一条假线；
            // 斜线越界时折点被压到边界，交点偏离解析解 120px（越界 30 单位 × 4px/单位）。
            // 现在走 PositionOfExtended：域外值按真实标度落在轴外，再由 Liang–Barsky 裁掉。
            // 注意：族参数列表只登记"要画哪些线"，PixelsOf/Intersect 对列表外的值照常解算，
            // 所以这里用只含 150 的等压线族也能取到 y=10 那条参考线。
            LineFamilyGenerator gen = GeneratorWith(XRight(), YUp(), 64,
                IsobarFamily(new double[] { 150d }), OutOfDomainSlanted(),
                MeridianFamily(new double[] { -10d, 10d, 30d }, YMin, YMax));

            // 整条线在图外（y=150 > 域上限 100）：空点列，而不是贴上边界的一条横线。
            Assert.Empty(gen.PixelsOf("isobar", 150d));
            AssertNoHit(gen, "isobar", 150d, "meridian", 10d, "图外等压线与竖线");

            // 越界斜线 y = x + 10：采样点 (-40, -30) 在域下限之下、(40, 50) 在域内。
            // 解析交点（与等压线 10）是域点 (0, 10) → 像素 (260, 400)。
            // 不夹紧后，越界端点落在 (60, 560)，进入裁剪框的点由线段几何给出：
            // y=440 处 x=60+(560-440)/(560-240)×400=210 → 可见段 (210,440)→(460,240)。
            IReadOnlyList<SKPoint> clipped = gen.PixelsOf("offdomain", 0d);
            Assert.Equal(2, clipped.Count);
            Assert.True(Math.Abs(clipped[0].X - 210f) <= HitTolerance && Math.Abs(clipped[0].Y - YFromUp) <= HitTolerance,
                        "越界端点应按真实走向裁在边界 (210, 440)，实到 (" + clipped[0].X + ", " + clipped[0].Y + ")");
            Assert.True(Math.Abs(clipped[1].X - XTo) <= HitTolerance && Math.Abs(clipped[1].Y - Py(50d)) <= HitTolerance,
                        "域内端点应照解析落在 (460, 240)，实到 (" + clipped[1].X + ", " + clipped[1].Y + ")");

            // 关键判据：交点回到解析解，而不是夹取造成的 140。
            SKPoint? hit = gen.Intersect("offdomain", 0d, "isobar", 10d);
            Assert.True(hit.HasValue, "越界斜线与等压线 10 的交点必须还在");
            Assert.True(Math.Abs(hit.Value.X - Px(0d)) <= HitTolerance,
                        "交点应等于解析解 x=" + Px(0d) + "，实到 " + hit.Value.X
                        + "（夹紧时代码会偏到 140，差 120px）");
        }

        [Fact]
        public void 线族_采样点上限只做截断不补点()
        {
            LineFamily poly = PolyFamily();

            // 五点线（方程 y = x + 50）：像素 (60,400)(160,320)(260,240)(360,160)(460,80)。
            LineFamilyGenerator full = GeneratorWith(XRight(), YUp(), 64, poly, IsobarFamily(LinearIsobars));
            IReadOnlyList<SKPoint> all = full.PixelsOf("poly", 0d);
            Assert.Equal(5, all.Count);
            Assert.True(Math.Abs(all[0].X - 60f) <= HitTolerance && Math.Abs(all[0].Y - 400f) <= HitTolerance,
                        "首点应为 (60, 400)，实到 (" + all[0].X + ", " + all[0].Y + ")");
            Assert.True(Math.Abs(all[4].X - 460f) <= HitTolerance && Math.Abs(all[4].Y - 80f) <= HitTolerance,
                        "末点应为 (460, 80)，实到 (" + all[4].X + ", " + all[4].Y + ")");

            // 上限 3：取前三个采样点丢弃其余（库不插值补点），线被"截短"而不是重采样。
            LineFamilyGenerator cut = GeneratorWith(XRight(), YUp(), 3, poly, IsobarFamily(LinearIsobars));
            IReadOnlyList<SKPoint> first3 = cut.PixelsOf("poly", 0d);
            Assert.Equal(3, first3.Count);
            Assert.True(Math.Abs(first3[2].X - 260f) <= HitTolerance && Math.Abs(first3[2].Y - 240f) <= HitTolerance,
                        "截断后末点应是第 3 个采样点 (260, 240)，实到 (" + first3[2].X + ", " + first3[2].Y + ")");

            // 截断的后果可测：与等压线 90（像素 80）的交点在完整线上存在，在截断线上消失。
            SKPoint? complete = full.Intersect("poly", 0d, "isobar", 90d);
            Assert.True(complete.HasValue, "完整五点线应与等压线 90 交于 (460, 80)");
            Assert.True(Math.Abs(complete.Value.X - 460d) <= HitTolerance && Math.Abs(complete.Value.Y - 80d) <= HitTolerance,
                        "交点应为 (460, 80)，实到 (" + complete.Value.X + ", " + complete.Value.Y + ")");
            AssertNoHit(cut, "poly", 0d, "isobar", 90d, "上限 3 时尾段被丢弃");

            // 没被丢弃的部分仍与解析解一致。
            AssertHit(cut, "poly", 0d, "isobar", 50d, Px(0d), Py(50d), "截断线上的交点");
            AssertHit(full, "poly", 0d, "isobar", 10d, Px(-40d), Py(10d), "完整线上的交点");
        }

        [Fact]
        public void 线族_单点与空采样画不出线也不抛()
        {
            LineFamilyGenerator gen = GeneratorWith(XRight(), YUp(), 64,
                DotFamily(), NoneFamily(), IsobarFamily(LinearIsobars));

            // 单点且落在图内：返回 1 个点，按契约画不出线。
            SKPoint only = Assert.Single(gen.PixelsOf("dot", 55d));
            Assert.True(Math.Abs(only.X - Px(0d)) <= HitTolerance && Math.Abs(only.Y - Py(55d)) <= HitTolerance,
                        "单点应落在 (260, 220)，实到 (" + only.X + ", " + only.Y + ")");

            // 单点在域外：不夹紧后它落在裁剪框外，直接为空（缺陷⑧的同一处修复）；
            // 以前会被轴夹到边界上、假装还有一个点。
            Assert.Empty(gen.PixelsOf("dot", 500d));

            // 空采样列表：构造期不拒（零个点没有坏形状可报），取点时返回空列表。
            Assert.Empty(gen.PixelsOf("none", 10d));

            // 没有可画段的线求交：无段可比 → null。
            AssertNoHit(gen, "dot", 55d, "isobar", 55d, "单点族与水平线");
            AssertNoHit(gen, "none", 10d, "isobar", 10d, "空采样族");

            // 落笔实证：这两族一条线都画不出，画布必须干净；同一画笔随后画完整族必须有墨。
            using (SKSurface? surface = SKSurface.Create(
                       new SKImageInfo(520, 480, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                Assert.NotNull(surface);
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    gen.DrawFamily(painter, "dot", SKColors.Black, 2f, false);
                    gen.DrawFamily(painter, "none", SKColors.Black, 2f, false);
                    Assert.Equal(0, painter.ClipDepth);

                    using (ImageProbe bare = Snapshot(surface))
                    {
                        Assert.False(bare.HasInkInBox(0, 0, bare.Width, bare.Height),
                                     "单点/空采样族不该落任何墨（落墨就是猜方向）");
                    }

                    gen.DrawFamily(painter, "isobar", SKColors.Black, 2f, false);
                    using (ImageProbe inked = Snapshot(surface))
                    {
                        IReadOnlyList<double> rows = inked.StrongHorizontalLines(39d, 441d, 65d, 455d, 0.9d);

                        // 族内画出的线条数 == 族参数个数（线宽 2px 跨两行，按段合并后正好 6 条）。
                        Assert.True(rows.Count == LinearIsobars.Length,
                                    "等压线族 " + LinearIsobars.Length + " 个参数应画出同样多条横线，实到 "
                                    + rows.Count + "：" + Join(rows));
                        double worstRow = 0d;
                        for (int i = 0; i < LinearIsobars.Length; i++)
                        {
                            // 扫描器按 y 从小到大（画布顶→底）报线，VerticalUp 轴上等于族参数从大到小，
                            // 所以倒序比对。2px 线覆盖整数行 [y-1, y]，段中心恒偏 -0.5px（实测）。
                            double want = Py(LinearIsobars[LinearIsobars.Length - 1 - i]) - 0.5d;
                            worstRow = Math.Max(worstRow, Math.Abs(rows[i] - want));
                        }

                        Assert.True(worstRow <= InkTolerance,
                                    "横线像素位置最大偏差 " + worstRow + "px（判据 " + InkTolerance + "px）");
                    }
                }
            }
        }

        [Fact]
        public void 路径_每条族线只有起笔与一笔画()
        {
            LineFamilyGenerator gen = GeneratorWith(XRight(), YUp(), 64,
                IsobarFamily(LinearIsobars), IsothermFamily(LinearIsotherms),
                MeridianFamily(new double[] { 10d }, YMin, YMax), DotFamily(), NoneFamily());

            using (SKSurface? surface = SKSurface.Create(
                       new SKImageInfo(520, 480, SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                Assert.NotNull(surface);
                using (Painter painter = new Painter(surface!.Canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    // 直线族（两个采样点）→ 像素点列 2 点 → SKPath 只有 MoveTo + LineTo。
                    using (SKPath line = painter.BuildPath(gen.PixelsOf("isobar", 30d), false))
                    {
                        Assert.False(line.IsEmpty);
                        Assert.Equal(2, line.PointCount);
                        Assert.Equal(2, line.VerbCount);
                    }

                    using (SKPath vertical = painter.BuildPath(gen.PixelsOf("meridian", 10d), false))
                    {
                        Assert.Equal(2, vertical.PointCount);
                    }

                    using (SKPath slanted = painter.BuildPath(gen.PixelsOf("isotherm", 50d), false))
                    {
                        Assert.Equal(2, slanted.PointCount);
                        Assert.Equal(2, slanted.VerbCount);
                    }

                    // 五点族不补点：路径点数 = 采样点数，动词数与点数同数（起笔 + 逐段连线）。
                    using (SKPath poly = painter.BuildPath(PolyFamily5(), false))
                    {
                        Assert.Equal(5, poly.PointCount);
                        Assert.Equal(5, poly.VerbCount);
                    }

                    // 单点 → 只有起笔一笔；空点列 → 空路径。两者都连不出一条线。
                    using (SKPath dot = painter.BuildPath(gen.PixelsOf("dot", 55d), false))
                    {
                        Assert.Equal(1, dot.PointCount);
                        Assert.Equal(1, dot.VerbCount);
                    }

                    using (SKPath empty = painter.BuildPath(gen.PixelsOf("none", 10d), false))
                    {
                        Assert.True(empty.IsEmpty);
                        Assert.Equal(0, empty.PointCount);
                    }
                }
            }
        }

        // ------------------------------------------------------------ 真实管线像素

        [Fact]
        public void 像素_管线里竖线族条数位置正确且交点有墨()
        {
            double[] verticals = { -30d, -15d, 0d, 15d, 30d };   // 像素 110/185/260/335/410，间隔 75
            double[] horizontals = { 25d, 50d, 75d };            // 像素 340/240/140，间隔 100

            LineFamilyGenerator gen = Generator(XRight(), YUp(), horizontals, new double[0], verticals);
            LineFamilyTemplate template = new LineFamilyTemplate(gen, "meridian", new[] { "isobar" }, 2f);

            byte[] png;
            using (Chart chart = new Chart(new ChartDocument(), template, new ChartStyle()))
            {
                png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            }

            Assert.NotNull(png);
            Assert.True(png.Length > 0, "PNG 导出为空字节，管线没录到任何绘制指令");

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                Assert.Equal(520, probe.Width);
                Assert.Equal(480, probe.Height);

                // ① 竖线族条数 == 族参数个数（走 BuildPath + StrokePath 那条路径）。
                IReadOnlyList<double> strong = probe.StrongVerticalLines(55d, 465d, 45d, 435d, 0.9d);
                Assert.True(strong.Count == verticals.Length,
                            "竖线族应画出 " + verticals.Length + " 条贯通线，实到 " + strong.Count + "：" + Join(strong));

                double worst = 0d;
                for (int i = 0; i < verticals.Length; i++)
                {
                    worst = Math.Max(worst, Math.Abs(strong[i] - (Px(verticals[i]) - 0.5d)));
                }
                Assert.True(worst <= InkTolerance,
                            "竖线像素位置最大偏差 " + worst + "px（判据 " + InkTolerance + "px）");
                AssertEqualSpacing(strong, 75d, "管线里的竖线族");

                // ② 单行扫描交叉核对（取不与任何横线同行的 y=190）。
                IReadOnlyList<double> row = probe.VerticalRulesAt(190, 55d, 465d);
                Assert.True(row.Count == verticals.Length,
                            "y=190 行上应有 " + verticals.Length + " 段竖墨，实到 " + row.Count + "：" + Join(row));
                for (int i = 0; i < verticals.Length; i++)
                {
                    Assert.True(Math.Abs(row[i] - (Px(verticals[i]) - 0.5d)) <= InkTolerance,
                                "第 " + i + " 条竖线应在 x=" + Px(verticals[i]) + "，实到 " + row[i]);
                }

                // ③ 横线族条数（走 DrawFamily → Polyline 那条路径）。
                IReadOnlyList<double> cols = probe.HorizontalRulesAt(120, 45d, 435d);
                Assert.True(cols.Count == horizontals.Length,
                            "x=120 列上应有 " + horizontals.Length + " 段横墨，实到 " + cols.Count + "：" + Join(cols));
                for (int i = 0; i < horizontals.Length; i++)
                {
                    // 横线按 y 升序报出（VerticalUp → 族参数降序），所以倒序比对。
                    double want = Py(horizontals[horizontals.Length - 1 - i]) - 0.5d;
                    Assert.True(Math.Abs(cols[i] - want) <= InkTolerance,
                                "第 " + i + " 条横线应在 y=" + want + "，实到 " + cols[i] + "：" + Join(cols));
                }

                // ④ 交点处确有墨：15 个解析交点 (Px(c), Py(k)) 全部落墨，且线宽覆盖区连着。
                int crossings = 0;
                foreach (double c in verticals)
                {
                    foreach (double k in horizontals)
                    {
                        int x = (int)Math.Round(Px(c));
                        int y = (int)Math.Round(Py(k));
                        Assert.True(probe.IsInk(x, y), "解析交点 (" + x + ", " + y + ") 处不该没墨");
                        Assert.True(probe.IsInk(x - 1, y - 1),
                                    "交点 (" + x + ", " + y + ") 的 2px 线宽覆盖区应连着墨");
                        crossings++;
                    }
                }
                Assert.Equal(15, crossings);

                // ⑤ 反证：族线之间空白处无墨，越出裁剪框的地方无墨（说明裁剪真的在起作用）。
                Assert.False(probe.IsInk(150, 200), "两条族线之间的空白不该有墨");
                Assert.False(probe.IsInk(300, 300), "两条族线之间的空白不该有墨");
                Assert.False(probe.HasInkInBox(0, 0, 58, probe.Height), "裁剪框左侧（x<58）不该有墨");
                Assert.False(probe.HasInkInBox(462, 0, probe.Width, probe.Height), "裁剪框右侧（x>462）不该有墨");
                Assert.False(probe.HasInkInBox(0, 0, probe.Width, 37), "裁剪框上方（y<37）不该有墨");
                Assert.False(probe.HasInkInBox(0, 443, probe.Width, probe.Height), "裁剪框下方（y>443）不该有墨");

                // ⑥ 墨迹总量下限：5 竖（约 400×2×5）+ 3 横（约 400×2×3）≈ 6400 像素，
                //    这里只要求"确实成图"；空实现（只登记族不产几何）会被这条当场拦下。
                int ink = 0;
                for (int y = 40; y < 441; y++)
                {
                    for (int x = 60; x < 461; x++)
                    {
                        if (probe.IsInk(x, y))
                        {
                            ink++;
                        }
                    }
                }
                Assert.True(ink > 2500, "绘图区墨像素只有 " + ink + "，远小于 5 竖 + 3 横的量级");
            }
        }

        [Fact]
        public void 像素_越界族参数在管线上一个墨点都不留()
        {
            // 缺陷⑧（已修）：族参数 150（域上限 100）以前被轴夹到裁剪框上沿，
            // 管线上因此多出一条"贴边假等压线"。现在它整条线落在裁剪框外，裁成空点列，
            // 管线不该有任何落墨。
            LineFamilyGenerator gen = Generator(XRight(), YUp(), new[] { 150d }, new double[0], new double[0]);
            byte[] png;
            using (Chart chart = new Chart(new ChartDocument(),
                       new LineFamilyTemplate(gen, "isobar", new string[0], 2f), new ChartStyle()))
            {
                png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            }

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                Assert.False(probe.HasInkInBox(40, 34, 480, 448),
                             "越界族参数在绘图区（含上边界那一行）留下了墨迹");
                Assert.True(probe.StrongHorizontalLines(36d, 44d, 65d, 455d, 0.9d).Count == 0,
                            "上边界一带不应有贴边线，实到 "
                            + Join(probe.StrongHorizontalLines(36d, 44d, 65d, 455d, 0.9d)));
            }

            // 正对照：同一个模板、同一个族换一个域内参数（50 → 像素 y=240）必须有墨，
            // 否则上面那两条"没有墨"的断言就是零墨。
            LineFamilyGenerator drawn = Generator(XRight(), YUp(), new[] { 50d }, new double[0], new double[0]);
            byte[] png2;
            using (Chart chart = new Chart(new ChartDocument(),
                       new LineFamilyTemplate(drawn, "isobar", new string[0], 2f), new ChartStyle()))
            {
                png2 = chart.Encode(ChartImageFormat.Png, 96f, 100);
            }

            using (ImageProbe probe = ImageProbe.FromPng(png2))
            {
                IReadOnlyList<double> lines = probe.StrongHorizontalLines(236d, 244d, 65d, 455d, 0.9d);
                Assert.True(lines.Count == 1, "域内等压线 50 应画出 1 条横线，实到 " + lines.Count + "：" + Join(lines));
                Assert.True(Math.Abs(lines[0] - (Py(50d) - 0.5d)) <= InkTolerance,
                            "域内线应在像素 y=240 附近（段中心 239.5），实到 " + lines[0]);
            }
        }

        // ------------------------------------------------------------ 支撑

        /// <summary>五点斜线族（方程 y = x + 50，采样 x∈{-40,-20,0,20,40}）。</summary>
        private static LineFamily PolyFamily()
        {
            return new LineFamily("poly", "五点斜线", new[] { 0d },
                                  v => new[]
                                  {
                                      new[] { -40d, 10d }, new[] { -20d, 30d }, new[] { 0d, 50d },
                                      new[] { 20d, 70d }, new[] { 40d, 90d },
                                  });
        }

        private static IReadOnlyList<SKPoint> PolyFamily5()
        {
            return GeneratorWith(XRight(), YUp(), 64, PolyFamily()).PixelsOf("poly", 0d);
        }

        /// <summary>越界斜线族：y = x + 10，左端采样点 -30 落在域下限之外。</summary>
        private static LineFamily OutOfDomainSlanted()
        {
            return new LineFamily("offdomain", "越界斜线", new[] { 0d },
                                  v => new[] { new[] { TMin, -30d + v }, new[] { TMax, 50d + v } });
        }

        /// <summary>
        /// 比对一个交点，返回误差（px）。命中点由 <see cref="LineFamilyGenerator.Intersect"/> 给，
        /// 期望点由本文件的解析式给，两边都是像素。
        /// </summary>
        private static double AssertHit(LineFamilyGenerator gen, string keyA, double memberA,
                                       string keyB, double memberB, double wantX, double wantY, string label)
        {
            SKPoint? hit = gen.Intersect(keyA, memberA, keyB, memberB);
            Assert.True(hit.HasValue, label + "：应有解析交点 (" + wantX + ", " + wantY + ")，实到 null");
            double error = Math.Max(Math.Abs(hit.Value.X - wantX), Math.Abs(hit.Value.Y - wantY));
            Assert.True(error <= HitTolerance,
                        label + "：交点 (" + hit.Value.X + ", " + hit.Value.Y + ") 偏离解析解 ("
                        + wantX + ", " + wantY + ") 共 " + error + " px");
            return error;
        }

        private static void AssertNoHit(LineFamilyGenerator gen, string keyA, double memberA,
                                        string keyB, double memberB, string label)
        {
            SKPoint? hit = gen.Intersect(keyA, memberA, keyB, memberB);
            if (hit.HasValue)
            {
                Assert.Fail(label + "：按契约应无交点，实到 (" + hit.Value.X + ", " + hit.Value.Y + ")");
            }
        }

        /// <summary>族线是水平线时读它的像素 y；顺带证明该线只有两个采样点、两点同高。</summary>
        private static double SingleLineY(LineFamilyGenerator gen, string key, double member)
        {
            IReadOnlyList<SKPoint> pixels = gen.PixelsOf(key, member);
            Assert.True(pixels.Count == 2, key + " 族参数 " + member + " 应有 2 个像素点，实到 " + pixels.Count);
            Assert.True(Math.Abs(pixels[0].Y - pixels[1].Y) <= HitTolerance,
                        key + " 族参数 " + member + " 不是水平线：" + pixels[0].Y + " / " + pixels[1].Y);
            return pixels[0].Y;
        }

        /// <summary>相邻比较证明严格单调（wantUp=false 要求严格递减），不接受"大体单调"。</summary>
        private static void AssertStrictlyMonotone(List<double> values, bool wantUp, string label)
        {
            for (int i = 1; i < values.Count; i++)
            {
                double delta = values[i] - values[i - 1];
                Assert.True(Math.Abs(delta) > 1e-6,
                            label + "：第 " + i + " 与第 " + (i - 1) + " 个交点重合（差 " + delta
                            + "），族方向上不可排版：" + Join(values));
                Assert.True((delta > 0d) == wantUp,
                            label + "：应严格" + (wantUp ? "递增" : "递减") + "，第 " + i + " 步实际 "
                            + delta + " px：" + Join(values));
            }
        }

        /// <summary>族参数等距时像素间距必须也等距（线性映射的直接推论，可复算）。</summary>
        private static void AssertEqualSpacing(IReadOnlyList<double> positions, double expectedGap, string label)
        {
            for (int i = 1; i < positions.Count; i++)
            {
                double gap = Math.Abs(positions[i] - positions[i - 1]);
                Assert.True(Math.Abs(gap - expectedGap) <= HitTolerance,
                            label + "：第 " + i + " 段间距 " + gap + " px，解析期望 " + expectedGap + " px："
                            + Join(positions));
            }
        }

        private static string Join(IReadOnlyList<double> values)
        {
            List<string> texts = new List<string>();
            for (int i = 0; i < values.Count; i++)
            {
                texts.Add(values[i].ToString("0.###"));
            }

            return "[" + string.Join(", ", texts) + "]";
        }

        private static ImageProbe Snapshot(SKSurface surface)
        {
            using (SKImage image = surface.Snapshot())
            using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                return ImageProbe.FromPng(data.ToArray());
            }
        }

        // ------------------------------------------------------------ 走管线用的小模板

        /// <summary>
        /// 最小模板：把线族接进 <c>Chart.Record → RenderImage → PNG</c> 这条真实链路。
        /// 一族走 <see cref="Painter.BuildPath"/> + <see cref="Painter.StrokePath"/>（SKPath 口径），
        /// 其余族走 <see cref="LineFamilyGenerator.DrawFamily"/>（折线口径），两条路径分别取证。
        /// </summary>
        private sealed class LineFamilyTemplate : IChartTemplate
        {
            private readonly LineFamilyGenerator _generator;
            private readonly string? _pathKey;
            private readonly string[] _polylineKeys;
            private readonly float _width;

            public LineFamilyTemplate(LineFamilyGenerator generator, string? pathKey, string[] polylineKeys, float width)
            {
                _generator = generator;
                _pathKey = pathKey;
                _polylineKeys = polylineKeys;
                _width = width;
            }

            public string Key => "test.line-family";

            public string DisplayName => "线族试版";

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                return new ChartGeometry(
                    CanvasBounds,
                    new SlotAxis(0f, 10f, 1, 1),
                    new LinearAxis("rule", 0d, 10d, 0f, CanvasBounds.Height, 1d, 5d),
                    new LinearAxis[0],
                    new Band("header", new SKRect(0f, 0f, CanvasBounds.Width, 20f), new BandRow[0]),
                    new Band("date", new SKRect(0f, 20f, CanvasBounds.Width, YToUp), new BandRow[0]),
                    PlotRect,
                    new LabelColumn[0],
                    new Band("cells", PlotRect, new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, YFromUp, CanvasBounds.Width, CanvasBounds.Height), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                if (_pathKey != null)
                {
                    LineFamily family = _generator.FamilyOf(_pathKey);
                    for (int i = 0; i < family.MemberValues.Count; i++)
                    {
                        IReadOnlyList<SKPoint> pixels = _generator.PixelsOf(family.Key, family.MemberValues[i]);
                        if (pixels.Count >= 2)
                        {
                            using (SKPath path = painter.BuildPath(pixels, false))
                            {
                                painter.StrokePath(path, SKColors.Black, _width);
                            }
                        }
                    }
                }

                for (int i = 0; i < _polylineKeys.Length; i++)
                {
                    _generator.DrawFamily(painter, _polylineKeys[i], SKColors.Black, _width, false);
                }
            }
        }
    }
}
