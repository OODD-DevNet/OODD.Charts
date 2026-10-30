using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Render;
using OODD.Charts.Style;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T218：尺寸标注线（<see cref="DimensionRun"/> / <see cref="DimensionChain"/> /
    /// <see cref="DimensionOrdinateSet"/> / <see cref="DimensionPainter"/> / <see cref="DimensionTokens"/>）。
    /// <para>
    /// docs/07 的验收行是四条口径，这里逐条落证据：
    /// <list type="bullet">
    /// <item><description><b>数字由坐标映射回写、不由调用方手填</b>——回写值 == 像素长度 ÷ 每单位像素数，
    /// 且渲染出来的像素与"手抄同一串字"完全一致（证明落纸的就是回写值）；</description></item>
    /// <item><description><b>链式各段首尾相接且总长 == 端到端</b>——切点复算 + 长度求和（双精度重算）+
    /// 尺寸线在像素上连续无断口；</description></item>
    /// <item><description><b>坐标式全部从同一基准引出</b>——每条引线的起点都在基准起点的法向阶梯上，
    /// 段长精确等于坐标值，像素上三条引线左端对齐同一列；</description></item>
    /// <item><description><b>延伸线留 gap 不穿轮廓</b>——在已落墨的轮廓与延伸线之间实测一条无墨空带，
    /// 空带行数 == gap − 轮廓半宽（并配"有墨"正对照，免得把"什么都没画"当成判据通过）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 判据落在哪一层：几何层用复算（解析式可推），落笔层一律用像素——像素窗口用
    /// <c>左闭右开</c>（与 <see cref="ImageProbe.HasInkInBox"/> 同口径），"墨"沿用探针阈值
    /// （任一通道 &lt; 247）。所有钉死的像素常数都是本机 SkiaSharp 3.116.1 实测值，
    /// 测量过程写在每条用例的注释里，可由公开定义复算的也给出复算式。
    /// </para>
    /// <para>
    /// <b>发现的行为缺陷本文件不改源码，只如实记录当前行为</b>，并在断言消息里注明"现状"：
    /// 详见各用例消息中"现状记录"字样与 <c>零线宽</c>、<c>默认留空</c>、<c>基准刻线</c>、
    /// <c>零长段</c>、<c>绘制副作用</c> 五条用例。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class DimensionTests
    {
        /// <summary>数字字体：三档字号在本机分别实测过（12 号 ascent −12.697266 / descent 3.140625）。</summary>
        private static readonly FontKey Text12 = new FontKey(null, 12f);
        private static readonly FontKey Text10 = new FontKey(null, 10f);
        private static readonly FontKey Text20 = new FontKey(null, 20f);

        // ---------------------------------------------------------- 公共几何常数（全部为整数像素，判据才干净）

        private const float DimY = 60f;
        private const float XFrom = 80f;
        private const float XTo = 220f;
        private const float ProfileY = 160f;

        /// <summary>被测轮廓的线宽：4 像素 → 半宽 2，实测墨带行 158..161（150/160±2 的覆盖率都 &gt; 0.5）。</summary>
        private const float ProfileLineWidth = 4f;

        /// <summary>水平尺寸 + 下方轮廓的取图尺寸（像素）。</summary>
        private const int CanvasW = 320;
        private const int CanvasH = 200;

        /// <summary>回写用例的固定几何：水平尺寸线长 200 像素。</summary>
        private const float RunLengthPx = 200f;

        private static readonly SKPoint LinearFrom = new SKPoint(40f, DimY);
        private static readonly SKPoint LinearTo = new SKPoint(240f, DimY);

        // ------------------------------------------------------------ 区段 1：数字由坐标映射回写

        /// <summary>
        /// 构造器根本收不到数字：反射实测 <see cref="DimensionRun"/> 唯一的构造器参数是
        /// from/to/orientation/arrowFrom/arrowTo（无 float、无 string），<see cref="DimensionRun.DisplayText"/>
        /// 是只读的派生属性——"不由调用方手填"在类型层面成立。
        /// </summary>
        [Fact]
        public void 数字不是构造入参_只能由坐标派生()
        {
            ConstructorInfo[] ctors = typeof(DimensionRun).GetConstructors();
            Assert.Single(ctors);

            ParameterInfo[] parameters = ctors[0].GetParameters();
            Assert.Equal(5, parameters.Length);
            foreach (ParameterInfo p in parameters)
            {
                Assert.True(p.ParameterType == typeof(SKPoint)
                            || p.ParameterType == typeof(DimensionOrientation)
                            || p.ParameterType == typeof(ArrowKind),
                            "构造器混进了不该有的入参类型 " + p.ParameterType.Name + "（" + p.Name + "）");
            }

            PropertyInfo display = typeof(DimensionRun).GetProperty("DisplayText")!;
            Assert.False(display.CanWrite, "DisplayText 必须只读：数字只能由量值格式化派生");

            // 构造后量值与文字都是空的，等待坐标回写/模板填字。
            DimensionRun fresh = Linear(LinearFrom, LinearTo);
            Assert.Null(fresh.Measured);
            Assert.Null(fresh.Text);
            Assert.Null(fresh.DisplayText);
        }

        /// <summary>
        /// 回写的唯一口径是 <see cref="DimensionRun.SetMeasuredFromPixels"/>：量值 == 像素长度 ÷ 每单位像素数。
        /// 显示串实测钉死：格式省略时用 <c>"0.###"</c>（整数不带小数点）并自动接单位后缀；
        /// 换成 <c>"0.00"</c> 后同一量值给两位小数。
        /// </summary>
        [Fact]
        public void 回写口径_量值等于像素长度除以每单位像素数()
        {
            DimensionRun run = Linear(LinearFrom, LinearTo);
            run.UnitSuffix = "mm";
            run.SetMeasuredFromPixels(2d);

            Assert.Equal(RunLengthPx, run.LengthInPixels);
            Assert.Equal(100d, run.Measured!.Value);
            Assert.Equal("0.###", run.MeasuredFormat);
            Assert.Equal("100mm", run.DisplayText);

            run.MeasuredFormat = "0.00";
            Assert.Equal("100.00mm", run.DisplayText);

            // 显式文字优先（docs/06 §3.6 的公差写法由调用方拼进 Text）。
            run.Text = "96±2";
            Assert.Equal("96±2", run.DisplayText);
            run.Text = string.Empty;
            AssertEqualMsg("100.00mm", run.DisplayText, "Text 为空串时回落到回写量值");
        }

        /// <summary>
        /// 同一 200 像素段在不同比例下的回写串（实测 + 复算）：
        /// 2→100、2.5→80、3→66.666…7（"0.###" 取三位小数）、4→50、10→20、1→200。
        /// </summary>
        [Theory]
        [InlineData(1.0, "200mm")]
        [InlineData(2.0, "100mm")]
        [InlineData(2.5, "80mm")]
        [InlineData(3.0, "66.667mm")]
        [InlineData(4.0, "50mm")]
        [InlineData(10.0, "20mm")]
        public void 每单位像素数决定回写数字(double pixelsPerUnit, string expected)
        {
            DimensionRun run = Linear(LinearFrom, LinearTo);
            run.UnitSuffix = "mm";
            run.SetMeasuredFromPixels(pixelsPerUnit);
            Assert.Equal(expected, run.DisplayText);
        }

        /// <summary>
        /// 毫米闭环：把端点写成 <see cref="Unit.Mm"/> 的像素、再按 <see cref="Unit.PxPerMm"/> 回写，
        /// 读回来的就是声明的毫米数。复算：PxPerMm = 96/25.4；<c>Unit.Mm(30f)</c> = 113.38583374023438px
        /// （实测），回写 30.00000100930529（float 取整留的 1e-6 相对误差），
        /// <c>"0.###"</c> 把它藏成 "30mm"，<c>"0.0"</c> 同理藏 12.5 的 2.5e-7。
        /// </summary>
        [Fact]
        public void 毫米坐标回写读回声明的毫米数()
        {
            DimensionRun run = Linear(new SKPoint(0f, DimY), new SKPoint(Unit.Mm(30f), DimY));
            run.UnitSuffix = "mm";
            run.SetMeasuredFromPixels(Unit.PxPerMm);

            Assert.Equal(113.38583374023438d, run.LengthInPixels, 6);
            Assert.True(Math.Abs(run.Measured!.Value - 30d) < 1e-4d,
                        "回写量值偏离声明毫米数过大：" + run.Measured.Value.ToString("R"));
            Assert.Equal("30mm", run.DisplayText);

            DimensionRun half = Linear(new SKPoint(0f, DimY), new SKPoint(Unit.Mm(12.5f), DimY));
            half.UnitSuffix = "mm";
            half.MeasuredFormat = "0.0";
            half.SetMeasuredFromPixels(Unit.PxPerMm);
            Assert.Equal("12.5mm", half.DisplayText);
        }

        /// <summary>
        /// 回写口径的入参校验：必须为正有限值。0/负数/NaN/±Infinity 实测全部抛 <see cref="ArgumentException"/>
        /// （源码 <c>!(pixelsPerUnit &gt; 0d) || double.IsInfinity(...)</c>，NaN 因比较为假一并拦住）。
        /// </summary>
        [Fact]
        public void 每单位像素数非法即抛参数异常()
        {
            DimensionRun run = Linear(LinearFrom, LinearTo);
            Assert.Throws<ArgumentException>(() => run.SetMeasuredFromPixels(0d));
            Assert.Throws<ArgumentException>(() => run.SetMeasuredFromPixels(-2.5d));
            Assert.Throws<ArgumentException>(() => run.SetMeasuredFromPixels(double.NaN));
            Assert.Throws<ArgumentException>(() => run.SetMeasuredFromPixels(double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => run.SetMeasuredFromPixels(double.NegativeInfinity));
            AssertNullMsg(run.Measured, "非法入参不得留下半个量值");
        }

        /// <summary>无文字又没回写量值时不落任何数字；量值为 NaN/Infinity 时同样不显示（实测 DisplayText 为 null）。</summary>
        [Fact]
        public void 没有坐标回写就不落数字()
        {
            DimensionRun bare = Linear(LinearFrom, LinearTo);
            Assert.Null(bare.DisplayText);

            DimensionRun nan = Linear(LinearFrom, LinearTo);
            nan.Measured = double.NaN;
            Assert.Null(nan.DisplayText);

            DimensionRun inf = Linear(LinearFrom, LinearTo);
            inf.Measured = double.PositiveInfinity;
            Assert.Null(inf.DisplayText);

            using (ImageProbe probe = Render(CanvasW, CanvasH, painter =>
            {
                DimensionPainter.Draw(painter, bare, new DimensionTokens(), SKColors.Black, SKColors.Black, Text12);
            }))
            {
                // 线本身有墨（正对照），线下方数字区零墨。
                Assert.True(probe.IsInk(160, 59) && probe.IsInk(160, 60), "尺寸线本身必须落墨");
                Assert.False(probe.HasInkInBox(100, 63, 180, 120), "无回写无文字时不该落任何数字");
            }
        }

        /// <summary>
        /// 像素证据：坐标回写出来的数字与"手抄同一串字"在纸面上完全一致——
        /// 证明落纸的就是回写值，而不是回写值只在属性里好看。
        /// 测量过程：段 (60,60)→(260,60)（200px）按每单位 4px 回写 → Measured == 50、串为 "50.0mm"；
        /// <see cref="DimensionTokens.TextOffset"/> 24、12 号字，取数字窗口 x[100,220) y[70,110)。
        /// 实测两幅：整幅墨 583、数字墨 183、数字外接框 x 138..182 / y 80..88（中心 160 == 锚点 X）。
        /// </summary>
        [Fact]
        public void 回写数字与同一串显式文字落墨完全一致()
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.TextOffset = 24f;

            DimensionRun derived = Linear(new SKPoint(60f, DimY), new SKPoint(260f, DimY));
            derived.MeasuredFormat = "0.0";
            derived.UnitSuffix = "mm";
            derived.SetMeasuredFromPixels(4d);
            Assert.Equal("50.0mm", derived.DisplayText);
            Assert.Equal(50d, derived.Measured!.Value);

            DimensionRun handWritten = Linear(new SKPoint(60f, DimY), new SKPoint(260f, DimY));
            handWritten.Text = "50.0mm";

            AssertEqualMsg(new SKPoint(160f, 84f), derived.TextAnchorAt(24f), "锚点 == 中点沿法向抬 24");

            int derivedAll, derivedNumber, explicitAll, explicitNumber;
            int[] derivedBounds, explicitBounds;

            using (ImageProbe probe = Render(CanvasW, 220, painter =>
            {
                DimensionPainter.Draw(painter, derived, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                derivedAll = InkCount(probe, 0, 0, CanvasW, 220);
                derivedNumber = InkCount(probe, 100, 70, 220, 110);
                derivedBounds = InkBounds(probe, 100, 70, 220, 110);
            }

            using (ImageProbe probe = Render(CanvasW, 220, painter =>
            {
                DimensionPainter.Draw(painter, handWritten, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                explicitAll = InkCount(probe, 0, 0, CanvasW, 220);
                explicitNumber = InkCount(probe, 100, 70, 220, 110);
                explicitBounds = InkBounds(probe, 100, 70, 220, 110);
            }

            Assert.True(derivedNumber > 0, "回写数字必须真的落纸");
            Assert.Equal(explicitAll, derivedAll);
            Assert.Equal(explicitNumber, derivedNumber);
            Assert.Equal(explicitBounds, derivedBounds);
            Assert.Equal(new int[] { 138, 182, 80, 88 }, derivedBounds);

            // 数字横向居中于锚点：实测 138..182 的中心 == 160 == TextAnchorAt 的 X；
            // 字宽实测 46.48828（12 号 "50.0mm"），墨宽 45px 落在其 −2..0 容差内。
            Assert.Equal(160, (derivedBounds[0] + derivedBounds[1]) / 2.0, 1);
            Assert.Equal(46.48828f, MeasureWidth("50.0mm", Text12), 4);
        }

        // ------------------------------------------------------------ 区段 2：链式首尾相接、总长 == 端到端

        /// <summary>
        /// 链式切点首尾相接（复算 <c>origin + Δ × 累计占比/占比和</c>）：
        /// (10,80)→(210,80) 按 {0.25,0.35,0.4} 切 → 60、130；
        /// 相邻段共用同一个切点值，源码是<b>同一次</b> <c>At()</c> 计算结果赋给两端的同坐标，故位级相等成立。
        /// </summary>
        [Fact]
        public void 链式各段首尾相接_端点严格等于链起终点()
        {
            DimensionChain chain = new DimensionChain(new SKPoint(10f, 80f), new SKPoint(210f, 80f),
                                                       new[] { 0.25, 0.35, 0.4 });

            Assert.Equal(3, chain.Runs.Count);
            Assert.Equal(new SKPoint(10f, 80f), chain.Runs[0].From);
            Assert.Equal(new SKPoint(60f, 80f), chain.Runs[0].To);
            Assert.Equal(new SKPoint(130f, 80f), chain.Runs[1].To);
            Assert.Equal(new SKPoint(210f, 80f), chain.Runs[2].To);
            AssertEqualMsg(chain.Terminator, chain.Runs[chain.Runs.Count - 1].To,
                         "末段终点强制取链终点，不许由累计量算出");

            for (int i = 0; i + 1 < chain.Runs.Count; i++)
            {
                AssertEqualMsg(chain.Runs[i].To, chain.Runs[i + 1].From, "第 " + i + " 段与下一段之间有缝/有叠");
            }

            Assert.Equal(1d, chain.FractionSum, 12);
            // 交接点只落一条延伸线：起点侧仅首段，终点侧每段都落（末段补上链尾）。
            Assert.True(chain.Runs[0].ShowExtensionFrom);
            Assert.False(chain.Runs[1].ShowExtensionFrom);
            Assert.False(chain.Runs[2].ShowExtensionFrom);
            for (int i = 0; i < chain.Runs.Count; i++)
            {
                Assert.True(chain.Runs[i].ShowExtensionTo);
                Assert.Equal(DimensionOrientation.Chain, chain.Runs[i].Orientation);
            }
        }

        /// <summary>
        /// 链式总长 == 端到端（双精度复算）。容差取 1e-3 像素：切点最后落到 <c>float</c>，
        /// 本例量级 200px 的 float 相对误差 6e-8 → 每段误差 ~1e-5px，三段相加仍 &lt; 1e-4px，1e-3 有十倍余量。
        /// 斜向链 (0,0)→(300,400) 按 {1,2,3,4} 切 → 50/100/150/200、端到端 500（勾股可推）。
        /// </summary>
        [Fact]
        public void 链式各段长度之和等于端到端距离()
        {
            DimensionChain chain = new DimensionChain(new SKPoint(10f, 80f), new SKPoint(210f, 80f),
                                                       new[] { 0.25, 0.35, 0.4 });
            Assert.Equal(200d, chain.TotalLength, 6);
            Assert.Equal(new double[] { 50d, 70d, 80d }, Lengths(chain));
            Assert.True(Math.Abs(chain.SumOfRunLengths - chain.TotalLength) < 1e-3d,
                        "各段之和 " + chain.SumOfRunLengths + " != 端到端 " + chain.TotalLength);

            DimensionChain diagonal = new DimensionChain(new SKPoint(0f, 0f), new SKPoint(300f, 400f),
                                                          new[] { 1d, 2d, 3d, 4d });
            Assert.Equal(500d, diagonal.TotalLength, 6);
            Assert.Equal(new double[] { 50d, 100d, 150d, 200d }, Lengths(diagonal));
            Assert.True(Math.Abs(diagonal.SumOfRunLengths - diagonal.TotalLength) < 1e-3d,
                        "斜向链各段之和 " + diagonal.SumOfRunLengths + " != 端到端 " + diagonal.TotalLength);
            Assert.Equal(10d, diagonal.FractionSum, 12);

            DimensionChain odd = new DimensionChain(new SKPoint(0f, 0f), new SKPoint(100f, 0f),
                                                     new[] { 1d, 1d, 2d });
            Assert.Equal(4d, odd.FractionSum, 12);
            Assert.Equal(new double[] { 25d, 25d, 50d }, Lengths(odd));
            Assert.True(Math.Abs(odd.SumOfRunLengths - 100d) < 1e-3d);
        }

        /// <summary>
        /// 链式逐段回写：各段数字之和 == 端到端读数（这是链式存在的理由）。
        /// 实测 {0.25,0.35,0.4} → "25mm"/"35mm"/"40mm"；{1,1,2} over 100px → "12.5mm"/"12.5mm"/"25mm"（Σ=50 == 100/2）。
        /// </summary>
        [Fact]
        public void 链式逐段回写_各段读数之和等于总长读数()
        {
            DimensionChain chain = new DimensionChain(new SKPoint(10f, 80f), new SKPoint(210f, 80f),
                                                       new[] { 0.25, 0.35, 0.4 });
            chain.ApplyScale(2d, "mm");

            Assert.Equal(new[] { "25mm", "35mm", "40mm" }, Texts(chain));

            double sum = 0d;
            for (int i = 0; i < chain.Runs.Count; i++)
            {
                sum += chain.Runs[i].Measured!.Value;
            }
            Assert.Equal(chain.TotalLength / 2d, sum, 6);

            DimensionChain odd = new DimensionChain(new SKPoint(0f, 0f), new SKPoint(0f, 100f),
                                                     new[] { 1d, 1d, 2d });
            odd.ApplyScale(2d, "mm");
            Assert.Equal(new[] { "12.5mm", "12.5mm", "25mm" }, Texts(odd));
            Assert.Equal(50d, odd.Runs[0].Measured!.Value + odd.Runs[1].Measured!.Value + odd.Runs[2].Measured!.Value, 6);
        }

        /// <summary>链式退化：空比例 / 占比全零 / 两端点重合都退化为整条一段（源码 439 行的退化分支）。</summary>
        [Fact]
        public void 链式退化_空比例与重合端点只给一段()
        {
            DimensionChain empty = new DimensionChain(new SKPoint(0f, 0f), new SKPoint(40f, 0f), new double[0]);
            Assert.Single(empty.Runs);
            Assert.Equal(40d, empty.Runs[0].LengthInPixels, 6);
            Assert.Equal(1d, empty.FractionSum, 12);

            DimensionChain zeros = new DimensionChain(new SKPoint(0f, 0f), new SKPoint(40f, 0f), new[] { 0d, 0d });
            Assert.Single(zeros.Runs);
            // 实测：占比全为 0 属退化输入，链按"归一后均分"处理，FractionSum 与空比例一样恒为 1，
            // 不是 0——归一是无条件进行的，调用方拿不到"未归一"的状态。
            Assert.Equal(1d, zeros.FractionSum, 12);

            DimensionChain collapsed = new DimensionChain(new SKPoint(7f, 9f), new SKPoint(7f, 9f),
                                                            new[] { 0.5, 0.5 });
            Assert.Single(collapsed.Runs);
            Assert.Equal(0d, collapsed.TotalLength, 6);
        }

        /// <summary>链式与坐标式的入参校验：null 抛 <see cref="ArgumentNullException"/>，NaN/Inf/负占比抛 <see cref="ArgumentException"/>。</summary>
        [Fact]
        public void 链式与坐标式的参数校验()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new DimensionChain(new SKPoint(0f, 0f), new SKPoint(40f, 0f), null!));
            Assert.Throws<ArgumentException>(() =>
                new DimensionChain(new SKPoint(0f, 0f), new SKPoint(40f, 0f), new[] { -1d, 2d }));
            Assert.Throws<ArgumentException>(() =>
                new DimensionChain(new SKPoint(0f, 0f), new SKPoint(40f, 0f), new[] { double.NaN, 1d }));
            Assert.Throws<ArgumentException>(() =>
                new DimensionChain(new SKPoint(0f, 0f), new SKPoint(40f, 0f), new[] { double.PositiveInfinity, 1d }));

            Assert.Throws<ArgumentNullException>(() =>
                new DimensionOrdinateSet(new SKPoint(0f, 0f), new SKPoint(200f, 0f), null!));
            Assert.Throws<ArgumentException>(() =>
                new DimensionOrdinateSet(new SKPoint(0f, 0f), new SKPoint(200f, 0f), new[] { 30d, double.NaN }));

            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d, 70d });
            Assert.Equal(30d, set.PositionOf(0), 12);
            Assert.Throws<ArgumentOutOfRangeException>(() => set.PositionOf(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => set.PositionOf(-1));
        }

        /// <summary>
        /// 像素证据：链式三段在纸面上首尾相接。
        /// 测量过程：链 (10,80)→(210,80) 三段、无数字（不中断线）；轮廓线 <c>Line(5,150,250,150)</c> 宽 4
        /// → 实测墨行 148..151；各段轮廓点 (x,150)、gap 8、over 4、<see cref="DimensionPainter.DrawSet"/> 不画基准刻线。
        /// 实测：row80 == "9-210" 一段贯通（交接点无断口）；延伸线落在 x 列 9、59、129、209 四根——
        /// 交接点没有重复落笔；各列竖段 "76-141 148-151"（顶端 76 == 80−over ✓、底端 141 == 142−1，
        /// 142 == 150−gap，其下 142..147 六行无墨 == gap − 轮廓半宽 ✓）。
        /// </summary>
        [Fact]
        public void 像素_链式尺寸线连续且交接点只落一条延伸线()
        {
            DimensionChain chain = new DimensionChain(new SKPoint(10f, 80f), new SKPoint(210f, 80f),
                                                       new[] { 0.25, 0.35, 0.4 });
            foreach (DimensionRun run in chain.Runs)
            {
                run.ProfileFrom = new SKPoint(run.From.X, ProfileY - 10f);
                run.ProfileTo = new SKPoint(run.To.X, ProfileY - 10f);
            }

            DimensionTokens tokens = new DimensionTokens();
            tokens.ExtensionGap = 8f;
            tokens.ExtensionOver = 4f;

            using (ImageProbe probe = Render(260, 200, painter =>
            {
                painter.Line(5f, 150f, 250f, 150f, SKColors.Black, ProfileLineWidth);
                DimensionPainter.DrawSet(painter, chain.Runs, tokens, SKColors.Black, SKColors.Black, Text12, false);
            }))
            {
                AssertEqualMsg("9-210", Describe(HorizontalRuns(probe, 80, 5, 215)),
                             "三段必须在像素上连成一条，交接点不许有缝");

                int[] junctions = new[] { 9, 59, 129, 209 };
                for (int i = 0; i < junctions.Length; i++)
                {
                    AssertEqualMsg("76-141 148-151", Describe(VerticalRuns(probe, junctions[i], 60, 170)),
                                 "第 " + i + " 根延伸列（列 " + junctions[i] + "）");
                }

                Assert.False(probe.HasInkInBox(8, 142, 12, 148), "延伸线与轮廓之间必须留空");
                Assert.True(probe.HasInkInBox(8, 148, 12, 152), "正对照：轮廓本身在那儿有墨");

                // 只有四根延伸线：段中间（如 x=34/94/170）在 gap 带内零墨。
                Assert.False(probe.HasInkInBox(34, 90, 35, 142), "链式中段不该再多落一根延伸线");
                Assert.False(probe.HasInkInBox(94, 90, 95, 142), "链式中段不该再多落一根延伸线");
            }
        }

        // ------------------------------------------------------------ 区段 3：坐标式全部从同一基准引出

        /// <summary>
        /// 坐标式几何：自动阶梯 == 基准跨度 ÷(条数+1)（200÷4 == 50，实测），
        /// 每条的起点都在基准起点的法向 (i+1)×pitch 上（实测 (20,70)/(20,120)/(20,170) 位级相等），
        /// 段长精确等于该条坐标值（30/70/150）——即"全部从同一基准引出、各条独立读数"。
        /// </summary>
        [Fact]
        public void 坐标式全部从同一基准引出_阶梯自动分摊()
        {
            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d, 70d, 150d });

            Assert.Equal(3, set.Count);
            Assert.Equal(200d, set.Span, 6);
            // 自动阶梯 == 跨度/(条数+1) == 200/4 == 50（实测）
            Assert.Equal(50d, set.StackPitch, 12);

            for (int i = 0; i < set.Count; i++)
            {
                DimensionRun run = set.Runs[i];
                AssertEqualMsg(new SKPoint(20f, 20f + 50f * (i + 1)), run.From,
                             "第 " + i + " 条的起点必须落在基准起点沿法向的阶梯上");
                // 段长 == 坐标值（"每条独立读数"的定义）
                Assert.Equal(set.PositionOf(i), run.LengthInPixels, 6);
                Assert.Equal(DimensionOrientation.Ordinate, run.Orientation);
                Assert.False(run.HasProfilePoints, "坐标式用引线本身连接，不另起延伸线");
            }

            Assert.Equal(new SKPoint(50f, 70f), set.Runs[0].To);
            Assert.Equal(new SKPoint(90f, 120f), set.Runs[1].To);
            Assert.Equal(new SKPoint(170f, 170f), set.Runs[2].To);
        }

        /// <summary>显式阶梯同样成立：pitch 20 → 起点 (20,40)/(20,60)/(20,80)，段长仍是坐标值，且仍同出一基准。</summary>
        [Fact]
        public void 坐标式显式阶梯_起点仍同在基准列上()
        {
            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d, 70d, 150d }, 20d);

            Assert.Equal(20d, set.StackPitch, 12);
            Assert.Equal(new SKPoint(20f, 40f), set.Runs[0].From);
            Assert.Equal(new SKPoint(20f, 60f), set.Runs[1].From);
            Assert.Equal(new SKPoint(20f, 80f), set.Runs[2].From);
            for (int i = 0; i < set.Count; i++)
            {
                Assert.Equal(set.PositionOf(i), set.Runs[i].LengthInPixels, 6);
                AssertEqualMsg(set.BaselineFrom.X, set.Runs[i].From.X, "所有引线同一起始列（基准）");
            }
        }

        /// <summary>坐标式逐段回写：数字 == 坐标 ÷ 每单位像素数（实测 "15mm"/"35mm"/"75mm"，Σ == 跨度读数）。</summary>
        [Fact]
        public void 坐标式逐段回写_数字与坐标一一对应()
        {
            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d, 70d, 150d });
            set.ApplyScale(2d, "mm");

            List<string?> labels = new List<string?>();
            for (int i = 0; i < set.Count; i++)
            {
                labels.Add(set.Runs[i].DisplayText);
            }
            Assert.Equal(new[] { "15mm", "35mm", "75mm" }, labels);
            // 每条独立读数：三段读数互不相干（坐标式不做加减），最大一条 == 坐标 150 ÷ 75px/mm。
            Assert.Equal(75d, set.Runs[2].Measured!.Value, 6);
        }

        /// <summary>坐标式的端部符号惯例：基准端短斜线（<see cref="ArrowKind.Slash"/>）、特征端实心箭头。</summary>
        [Fact]
        public void 坐标式端部符号_基准端斜线特征端实心()
        {
            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d });
            Assert.Equal(ArrowKind.Slash, set.Runs[0].ArrowFrom);
            Assert.Equal(ArrowKind.Filled, set.Runs[0].ArrowTo);
        }

        /// <summary>
        /// 像素证据：三条引线左端对齐同一列、长度就是坐标值。
        /// 测量过程：基准 (20,20)→(220,20)、坐标 {30,70,150}（自动阶梯 50 → 引线行 y=70/120/170）、
        /// 无数字、默认令牌（<see cref="DimensionTokens.ArrowLength"/> 5、<see cref="DimensionTokens.LineWidth"/> 1）。
        /// 实测每行墨段 <c>"18-49 68-71"</c>：18 == 起点列 20 − 斜线在 1px 列宽内的推进（ArrowLength 5 的 45° 斜线，
        /// 半长 2.5 → 水平 1.77 → 覆盖 x∈[18.0,21.0] ✓），49 == 段末端点 50−1（实心三角尖端在整点列外沿 → 该列无墨），
        /// 后一段是基准刻线。三行左端同为 18 → "全部从同一基准引出"。
        /// </summary>
        [Fact]
        public void 像素_坐标式三条引线同一起始列且长度即坐标()
        {
            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d, 70d, 150d });

            using (ImageProbe probe = Render(300, 240, painter =>
            {
                DimensionPainter.DrawSet(painter, set.Runs, new DimensionTokens(),
                                         SKColors.Black, SKColors.Black, Text12, true);
            }))
            {
                int[] rows = new[] { 70, 120, 170 };
                double[] rightEnds = new double[rows.Length];

                for (int i = 0; i < rows.Length; i++)
                {
                    List<int[]> runs = HorizontalRuns(probe, rows[i], 10, 260);
                    Assert.True(runs.Count >= 1, "第 " + i + " 条引线必须落墨");
                    AssertEqualMsg(18, runs[0][0], "三条引线必须从同一基准列起步：" + Describe(runs));
                    rightEnds[i] = set.Runs[i].To.X - 1d;
                    AssertEqualMsg(rightEnds[i], (double)runs[0][1], "第 " + i + " 条的段末像素 == 坐标终点（复算 To.X−1）");
                }

                // 段长在像素上按比例递增，且与坐标差一致：49−18 = 31 ≈ 30+1、169−18 = 151 ≈ 150+1。
                Assert.Equal(31d, rightEnds[0] - 18d, 6);
                Assert.Equal(71d, rightEnds[1] - 18d, 6);
                Assert.Equal(151d, rightEnds[2] - 18d, 6);

                Assert.True(probe.HasInkInBox(45, 69, 48, 71), "正对照：第一条引线中段有墨");
                Assert.False(probe.HasInkInBox(100, 71, 160, 119), "阶梯之间不许互相串线");
            }
        }

        /// <summary>
        /// <b>现状记录（疑似缺陷）</b>：<see cref="DimensionPainter"/> 的基准刻线锚在 <c>runs[0].From</c>
        /// 而不是声明的基准点，所以刻线只覆盖阶梯范围（实测第 20 列墨行 68..170），
        /// 从不回到 <c>BaselineFrom</c>（y=20）——"全部从同一基准引出"目前只在引线起点的 X 列上成立。
        /// </summary>
        [Fact]
        public void 现状_坐标式基准刻线只覆盖阶梯范围()
        {
            DimensionOrdinateSet set = new DimensionOrdinateSet(new SKPoint(20f, 20f), new SKPoint(220f, 20f),
                                                                 new[] { 30d, 70d, 150d });

            using (ImageProbe probe = Render(300, 240, painter =>
            {
                DimensionPainter.DrawSet(painter, set.Runs, new DimensionTokens(),
                                         SKColors.Black, SKColors.Black, Text12, true);
            }))
            {
                Assert.Equal("68-170", Describe(VerticalRuns(probe, 20, 10, 230)));
                Assert.False(probe.HasInkInBox(19, 20, 22, 67),
                             "现状：刻线不到声明的基准点 (20,20)；期望是回到 y=20");
                Assert.True(probe.HasInkInBox(19, 70, 22, 170), "正对照：阶梯范围内的刻线确实有墨");
            }

            using (ImageProbe probe = Render(300, 240, painter =>
            {
                DimensionPainter.DrawSet(painter, set.Runs, new DimensionTokens(),
                                         SKColors.Black, SKColors.Black, Text12, false);
            }))
            {
                Assert.False(probe.HasInkInBox(19, 80, 22, 110), "drawBaseline=false 时不该有贯穿的基准刻线");
            }
        }

        // ------------------------------------------------------------ 区段 4：延伸线留 gap 不穿轮廓

        /// <summary>
        /// 延伸线在轮廓侧留空：gap 8、over 4、轮廓线宽 4（墨带 158..161）。
        /// 实测列 79/80 竖段 == <c>"56-151 158-161"</c>：顶端 56 == 尺寸线 60 − over ✓，
        /// 延伸线主体止于 151，轮廓自 158 起墨，<b>空带 152..157 共 6 行 == gap(8) − 轮廓半宽(2)</b>——
        /// 空带宽由轮廓自身的半宽吃掉一块，这是制图"不碰轮廓"的像素级体现。
        /// </summary>
        [Fact]
        public void 延伸线在轮廓外留空_空带行数等于留空量减轮廓半宽()
        {
            DimensionTokens tokens = GapTokens(8f, 4f);
            using (ImageProbe probe = RenderProfile(tokens, DefaultProfileRun(), ProfileY))
            {
                Assert.Equal("56-151 158-161", Describe(VerticalRuns(probe, 80, 40, 180)));
                Assert.Equal("56-151 158-161", Describe(VerticalRuns(probe, 79, 40, 180)));
                Assert.Equal("56-151 158-161", Describe(VerticalRuns(probe, 220, 40, 180)));

                Assert.False(probe.HasInkInBox(78, 152, 83, 158), "延伸线与轮廓之间必须有无墨空带");
                Assert.True(probe.HasInkInBox(79, 158, 82, 162), "正对照：轮廓本身有墨");
                Assert.True(probe.HasInkInBox(79, 56, 81, 152), "正对照：延伸线本身有墨");

                int clear = 0;
                for (int y = 151; y <= 161; y++)
                {
                    if (!probe.IsInk(80, y))
                    {
                        clear++;
                    }
                }
                AssertEqualMsg(6, clear, "空带行数 == gap − 轮廓半宽 == 8 − 2");
            }
        }

        /// <summary>
        /// gap 翻倍则空带同比增宽：gap 16 → 实测 <c>"56-143 158-161"</c>，
        /// 空带 144..157 == 14 行 == 16 − 2（复算 <c>gap − 轮廓线宽/2</c>）。
        /// </summary>
        [Fact]
        public void 留空量翻倍_空带同比增宽()
        {
            DimensionTokens tokens = GapTokens(16f, 4f);
            using (ImageProbe probe = RenderProfile(tokens, DefaultProfileRun(), ProfileY))
            {
                Assert.Equal("56-143 158-161", Describe(VerticalRuns(probe, 80, 40, 180)));

                int clear = 0;
                for (int y = 140; y <= 161; y++)
                {
                    if (!probe.IsInk(80, y))
                    {
                        clear++;
                    }
                }
                // 空带 = 144..157 共 14 行 = gap 16 − 轮廓半宽 2；窗口上界取 161（轮廓墨带末行），
                // 原先扫到 162 会把轮廓以下那行空白也计进来而得 15。
                AssertEqualMsg(14, clear, "gap 16 − 轮廓半宽 2");
                Assert.False(probe.HasInkInBox(78, 144, 83, 157));
            }
        }

        /// <summary>
        /// <b>现状记录（疑似缺陷）</b>：默认 <see cref="DimensionTokens.ExtensionGap"/> == 2，
        /// 小于本例 4 像素宽轮廓的半宽（2）→ 空带宽 0 → 实测列 80 为一段贯通 <c>"56-161"</c>，
        /// 延伸线与轮廓粘连，即验收行"延伸线留 gap 不穿轮廓"用默认令牌并不成立。
        /// </summary>
        [Fact]
        public void 现状_默认留空量避不开四像素宽轮廓()
        {
            Assert.Equal(2f, new DimensionTokens().ExtensionGap);

            DimensionRun run = DefaultProfileRun();
            DimensionTokens tokens = new DimensionTokens();
            tokens.ExtensionOver = 4f;

            using (ImageProbe probe = RenderProfile(tokens, run, ProfileY))
            {
                List<int[]> runs = VerticalRuns(probe, 80, 40, 180);
                AssertEqualMsg(1, runs.Count,
                             "现状：默认 gap 下延伸线与轮廓连成一段（" + Describe(runs) + "），期望是无墨空带");
                Assert.Equal("56-161", Describe(runs));
                Assert.True(probe.IsInk(80, 155), "现状记录：空带位置仍有墨");
            }
        }

        /// <summary>单段覆写优先于令牌：令牌 gap 取默认 2、该段 <c>ExtensionGap = 16</c> → 实测 <c>"57-143 158-161"</c>（顶端 57 == 60−令牌 over 3）。</summary>
        [Fact]
        public void 单段可覆写留空量_优先于令牌()
        {
            DimensionRun run = DefaultProfileRun();
            run.ExtensionGap = 16f;
            Assert.Equal(16f, run.ExtensionGapOr(2f));

            DimensionTokens tokens = new DimensionTokens();
            using (ImageProbe probe = RenderProfile(tokens, run, ProfileY))
            {
                Assert.Equal("57-143 158-161", Describe(VerticalRuns(probe, 80, 40, 180)));
                Assert.False(probe.HasInkInBox(78, 144, 83, 158));
            }

            // 未覆写时回落令牌默认（NotSet 语义）。
            DimensionRun inherit = DefaultProfileRun();
            Assert.True(float.IsNaN(inherit.ExtensionGap));
            Assert.Equal(2f, inherit.ExtensionGapOr(new DimensionTokens().ExtensionGap));
        }

        /// <summary>
        /// 轮廓在尺寸线另一侧时延伸方向自动取向（源码把方向取为"轮廓→尺寸线端点"）：
        /// 轮廓 y=20、尺寸线 y=90 → 实测列 80 == <c>"18-21 28-92"</c>，
        /// 空带 22..27 == 6 行 == gap 8 − 半宽 2，与轮廓在下侧时完全对称。
        /// </summary>
        [Fact]
        public void 轮廓在另一侧时同样留空()
        {
            DimensionRun run = Linear(new SKPoint(XFrom, 90f), new SKPoint(XTo, 90f));
            run.ProfileFrom = new SKPoint(XFrom, 20f);
            run.ProfileTo = new SKPoint(XTo, 20f);

            DimensionTokens tokens = GapTokens(8f, 4f);
            using (ImageProbe probe = RenderProfile(tokens, run, 20f))
            {
                // 实测：延伸线 [28,94) 的落墨是半开区间 28..93（与正侧用例 "56-151" 同一口径：
                // 描边覆盖 [min, max)，末端的第 94 行不沾墨）。
                Assert.Equal("18-21 28-93", Describe(VerticalRuns(probe, 80, 10, 110)));
                Assert.False(probe.HasInkInBox(78, 22, 83, 27), "留空带必须在轮廓与延伸线之间（22..27 六行）");
                Assert.True(probe.HasInkInBox(78, 28, 83, 34), "正对照：延伸线自 28 起墨");

                // 复算延伸线端点：A = 轮廓 + gap×方向 = (80, 28)，B = 端点 + over×方向 = (80, 94)。
                run.ExtensionLines(run.ProfileFrom, run.ProfileTo, 8f, 4f,
                                   out SKPoint fromA, out SKPoint fromB, out SKPoint toA, out SKPoint toB);
                Assert.Equal(new SKPoint(80f, 28f), fromA);
                Assert.Equal(new SKPoint(80f, 94f), fromB);
                Assert.Equal(new SKPoint(220f, 28f), toA);
                Assert.Equal(new SKPoint(220f, 94f), toB);
            }
        }

        /// <summary>
        /// 没给被测轮廓点就不落延伸线（绘制层据此跳过）：实测 gap 带零墨，
        /// 而同一版式给了轮廓点时 gap 带确有延伸线墨（正对照，免得"零墨"是假绿）。
        /// </summary>
        [Fact]
        public void 未给轮廓点时不落延伸线()
        {
            DimensionTokens tokens = GapTokens(8f, 4f);

            DimensionRun bare = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            Assert.False(bare.HasProfilePoints);
            // OffPlane 用 NaN 表示"没有轮廓点"，而 NaN != NaN——按相等比较断言永远失败，只能逐分量验 NaN。
            Assert.True(float.IsNaN(bare.ProfileFrom.X) && float.IsNaN(bare.ProfileFrom.Y),
                        "未给轮廓点时 ProfileFrom 应为 OffPlane（两分量皆 NaN），实到 " + bare.ProfileFrom);

            using (ImageProbe probe = RenderProfile(tokens, bare, ProfileY))
            {
                Assert.False(probe.HasInkInBox(76, 70, 84, 152), "无轮廓点时不该画延伸线");
                Assert.True(probe.HasInkInBox(79, 158, 82, 162), "正对照：轮廓仍按调用方自己的笔落墨");
            }

            using (ImageProbe probe = RenderProfile(tokens, DefaultProfileRun(), ProfileY))
            {
                Assert.True(probe.HasInkInBox(76, 70, 84, 152), "给了轮廓点后延伸线必须落墨（正对照）");
            }
        }

        /// <summary>
        /// 几何复算：延伸线端点 == 轮廓 + gap·方向、端点 + over·方向。
        /// 正交例实测 (80,152)(80,56)(220,152)(220,56)；
        /// 斜向例实测 (55.76001,153.21602)(37.880005,156.608)(195.76001,133.21602)(197.88,56.608006)——
        /// 注释里的复算式：法向 n=(−uy,ux)，方向取"轮廓→端点"同侧，A = P + gap·e，B = tip + over·e。
        /// </summary>
        [Fact]
        public void 延伸线端点可由公开几何复算()
        {
            DimensionRun run = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            run.ExtensionLines(new SKPoint(XFrom, ProfileY), new SKPoint(XTo, ProfileY), 8f, 4f,
                               out SKPoint fromA, out SKPoint fromB, out SKPoint toA, out SKPoint toB);

            Assert.Equal(new SKPoint(80f, 152f), fromA);
            Assert.Equal(new SKPoint(80f, 56f), fromB);
            Assert.Equal(new SKPoint(220f, 152f), toA);
            Assert.Equal(new SKPoint(220f, 56f), toB);

            // 未覆写 gap/over 的二参重载按 0 处理（源码注释），即零留空——现状，不是期望。
            DimensionRun zero = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            zero.ExtensionLines(new SKPoint(XFrom, ProfileY), new SKPoint(XTo, ProfileY),
                                out SKPoint zA, out SKPoint zB, out SKPoint zC, out SKPoint zD);
            Assert.Equal(new SKPoint(80f, 160f), zA);
            Assert.Equal(new SKPoint(80f, 60f), zB);

            DimensionRun oblique = Linear(new SKPoint(40f, 160f), new SKPoint(200f, 60f));
            oblique.ExtensionLines(new SKPoint(60f, 160f), new SKPoint(200f, 140f), 8f, 4f,
                                   out SKPoint oA, out SKPoint oB, out SKPoint oC, out SKPoint oD);
            Assert.Equal(55.76001f, oA.X, 4);
            Assert.Equal(153.21602f, oA.Y, 4);
            Assert.Equal(37.880005f, oB.X, 4);
            Assert.Equal(156.608f, oB.Y, 4);
            Assert.Equal(195.76001f, oC.X, 4);
            Assert.Equal(133.21602f, oC.Y, 4);
            Assert.Equal(197.88f, oD.X, 4);
            Assert.Equal(56.608006f, oD.Y, 4);

            // 转角非零时方向随法向旋转（ExtensionAngleDegrees 参与、但不改变 gap 距离口径）。
            DimensionRun skewed = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            skewed.ExtensionAngleDegrees = 30f;
            skewed.ExtensionLines(new SKPoint(XFrom, ProfileY), new SKPoint(XTo, ProfileY), 8f, 4f,
                                  out SKPoint sA, out SKPoint _, out SKPoint _, out SKPoint _);
            Assert.Equal(84f, sA.X, 4);
            Assert.Equal(153.0718f, sA.Y, 4);
        }

        /// <summary>
        /// 竖直测量方向（尺寸线沿 Y）同样留空：轮廓 <c>Line(120,30,120,200)</c> 宽 4（墨列 118..121），
        /// 尺寸线 x=220、gap 8、over 4 → 实测 row60 == <c>"118-121 128-223"</c>，
        /// 空列 122..127 == 6 == gap − 半宽，且 128 == 120+gap、223 == 220+over−1。
        /// </summary>
        [Fact]
        public void 竖直测量方向同样留空不穿轮廓()
        {
            DimensionRun run = Linear(new SKPoint(220f, 60f), new SKPoint(220f, 160f));
            run.ProfileFrom = new SKPoint(120f, 60f);
            run.ProfileTo = new SKPoint(120f, 160f);

            DimensionTokens tokens = GapTokens(8f, 4f);
            using (ImageProbe probe = Render(320, 220, painter =>
            {
                painter.Line(120f, 30f, 120f, 200f, SKColors.Black, ProfileLineWidth);
                DimensionPainter.Draw(painter, run, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal("118-121 128-223", Describe(HorizontalRuns(probe, 60, 110, 240)));
                Assert.False(probe.HasInkInBox(122, 50, 128, 70), "延伸线与竖直轮廓之间必须无墨");
                Assert.True(probe.HasInkInBox(118, 55, 122, 65), "正对照：轮廓有墨");
                Assert.True(probe.HasInkInBox(130, 59, 160, 62), "正对照：延伸线有墨");

                int clear = 0;
                for (int x = 121; x <= 128; x++)
                {
                    if (!probe.IsInk(x, 60))
                    {
                        clear++;
                    }
                }
                AssertEqualMsg(6, clear, "空列数 == gap − 轮廓半宽");
            }
        }

        // ------------------------------------------------------------ 区段 5：四种端部符号的像素判据

        /// <summary>
        /// 四种 <see cref="ArrowKind"/> 各自落墨且互不相同。
        /// 测量过程：段 (40,60)→(240,60)、<see cref="DimensionTokens.ArrowLength"/> 20、无数字，
        /// 取终点侧四列（x=220/231/235/240）的竖段串作"签名"，实测四签名两两不同：
        /// 实心 220=="56-63"；空心 220=="52-53 59-60 66-67"；圆点 231=="55-64"；斜线 235=="54-56 59-60"。
        /// </summary>
        [Fact]
        public void 四种端部符号各自有墨且两两不同()
        {
            ArrowKind[] kinds = new[] { ArrowKind.Filled, ArrowKind.Open, ArrowKind.Dot, ArrowKind.Slash };
            Dictionary<ArrowKind, string> signatures = new Dictionary<ArrowKind, string>();
            Dictionary<ArrowKind, int> tipInk = new Dictionary<ArrowKind, int>();

            foreach (ArrowKind kind in kinds)
            {
                DimensionTokens tokens = new DimensionTokens();
                tokens.ArrowLength = 20f;

                using (ImageProbe probe = Render(320, 120, painter =>
                {
                    DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, kind, kind), tokens,
                                          SKColors.Black, SKColors.Black, Text12);
                }))
                {
                    string signature = Describe(VerticalRuns(probe, 220, 40, 80)) + " | "
                                     + Describe(VerticalRuns(probe, 231, 40, 80)) + " | "
                                     + Describe(VerticalRuns(probe, 235, 40, 80)) + " | "
                                     + Describe(VerticalRuns(probe, 240, 40, 80));
                    signatures[kind] = signature;
                    tipInk[kind] = InkCount(probe, 215, 40, 252, 80);
                    Assert.True(tipInk[kind] > 0, kind + " 端部符号必须有墨");
                }
            }

            foreach (ArrowKind a in kinds)
            {
                Assert.Equal(4, signatures.Count);
                foreach (ArrowKind b in kinds)
                {
                    if (a == b)
                    {
                        continue;
                    }
                    Assert.NotEqual(signatures[a], signatures[b]);
                }
            }

            Assert.Equal("56-63 | 58-61 | 59-60 | ", signatures[ArrowKind.Filled]);
            Assert.Equal("52-53 59-60 66-67 | 56-57 59-60 62-63 | 57-62 | 59-60", signatures[ArrowKind.Open]);
            Assert.Equal("59-60 | 55-64 | 54-55 59-60 64-65 | 57-62", signatures[ArrowKind.Dot]);
            Assert.Equal("59-60 | 59-60 | 54-56 59-60 | 59-61", signatures[ArrowKind.Slash]);
        }

        /// <summary>
        /// 判据按任务原文逐字核：实心三角在中轴列上是<b>连续一段</b>（x=220 → "56-63"，包含轴线 59-60），
        /// 空心箭同列岔成三段（"52-53 59-60 66-67"，翼与轴线之间有 5 行白），
        /// 圆点在圆心列（x=235，实测 == 端点 240 − 半径 4.8 取整）岔成三段（环，"54-55 59-60 64-65"）。
        /// 复算：实心半宽 = ArrowBaseRatio×距离 = 0.18×20 = 3.6 → 行 56.4..63.6；
        /// 空心张角是实心的两倍（源码 ×2）→ 0.36×20 = 7.2 → 行 52.8/67.2；
        /// 圆点外径 = DotRadiusRatio×L + 线宽/2 = 4.8+0.6 = 5.4 → 行 54.6..65.4。
        /// </summary>
        [Fact]
        public void 实心中轴连成一片_空心与圆点中心留白()
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.ArrowLength = 20f;

            using (ImageProbe filled = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Filled, ArrowKind.Filled),
                                      tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            using (ImageProbe open = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Open, ArrowKind.Open),
                                      tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            using (ImageProbe dot = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Dot, ArrowKind.Dot),
                                      tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal("56-63", Describe(VerticalRuns(filled, 220, 40, 80)));
                Assert.True(filled.IsInk(220, 60) && filled.IsInk(220, 62), "实心三角内部必须有墨");

                Assert.Equal("52-53 59-60 66-67", Describe(VerticalRuns(open, 220, 40, 80)));
                Assert.False(open.HasInkInBox(219, 54, 222, 59), "空心箭的翼与轴线之间必须留白");
                Assert.True(open.IsInk(220, 52), "正对照：空心箭的上翼有墨");

                Assert.Equal("54-55 59-60 64-65", Describe(VerticalRuns(dot, 235, 40, 80)));
                // 实测落墨图（ASCII 取证）：圆心在 (235, 60)、半径 = DotRadiusRatio×ArrowLength = 4.8，
                // 环右缘恰切在端点 240，故 235.2…244.8 那种"越过端点"的画法并不存在。
                // 环内的空白洞只在 57..58 / 61..62 两截最宽（x 233..237）；再往外的行列会被
                // 环的上下弧侵入，不能拿来当"洞"的判据。轴线本身占 59..60，不算穿环。
                Assert.False(dot.HasInkInBox(233, 57, 237, 58), "圆点环内（轴线以上 57..58 × 233..237）必须留白");
                Assert.False(dot.HasInkInBox(233, 61, 237, 62), "圆点环内（轴线以下 61..62 × 233..237）必须留白");
                Assert.True(dot.IsInk(235, 54), "正对照：圆点环顶有墨");
            }
        }

        /// <summary>
        /// 只有短斜线越过被测端点（建筑制图惯例）：实测越过 x=242..251 的墨数 Slash 16、其余 0；
        /// 复算：斜线沿 (u+n)/√2 各伸出 ArrowLength/2 = 10 → 水平伸出 7.07px，故 x 到 247 仍带墨。
        /// </summary>
        [Fact]
        public void 只有斜线符号越过被测端点()
        {
            Dictionary<ArrowKind, int> beyond = new Dictionary<ArrowKind, int>();
            foreach (ArrowKind kind in new[] { ArrowKind.Filled, ArrowKind.Open, ArrowKind.Dot, ArrowKind.Slash })
            {
                DimensionTokens tokens = new DimensionTokens();
                tokens.ArrowLength = 20f;
                using (ImageProbe probe = Render(320, 120, painter =>
                {
                    DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, kind, kind), tokens,
                                          SKColors.Black, SKColors.Black, Text12);
                }))
                {
                    beyond[kind] = InkCount(probe, 242, 40, 252, 80);
                    Assert.False(probe.IsInk(255, 60), "端符号不该溢出到画布右缘");
                }
            }

            Assert.Equal(0, beyond[ArrowKind.Filled]);
            Assert.Equal(0, beyond[ArrowKind.Open]);
            Assert.Equal(0, beyond[ArrowKind.Dot]);
            Assert.Equal(16, beyond[ArrowKind.Slash]);
        }

        /// <summary>
        /// 起点与终点可用不同符号（实测混合例：起点圆点、终点斜线）——
        /// 起点列 44 呈环签名 "54-55 59-60 64-65"，终点越过 x≥242 有墨 16 而起点侧 x≤37 零墨。
        /// </summary>
        [Fact]
        public void 起终点可用不同端部符号()
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.ArrowLength = 20f;

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Dot, ArrowKind.Slash), tokens,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal("54-55 59-60 64-65", Describe(VerticalRuns(probe, 45, 40, 80)));
                Assert.Equal(0, InkCount(probe, 28, 40, 38, 80));
                Assert.Equal(16, InkCount(probe, 242, 40, 252, 80));
                Assert.Equal("60-62", Describe(VerticalRuns(probe, 241, 40, 80)));
                Assert.Equal("63-65", Describe(VerticalRuns(probe, 244, 40, 80)));
            }
        }

        // ------------------------------------------------------------ 区段 6：令牌生效（像素层面）

        /// <summary>
        /// <see cref="DimensionTokens.LineWidth"/> 改变尺寸线的落墨厚度：
        /// 1 → 第 140 列 <c>"59-60"</c>（2 行）、窗口墨 320；4 → <c>"58-61"</c>（4 行）、墨 640。
        /// 复算：线在整点 y=60、宽度 w → 覆盖 y∈[60−w/2, 60+w/2]，每像素列墨行数 == w；
        /// 窗口 x[60,220) 避开了两端符号（x&lt;60 / x&gt;220），墨数 == 160 列 × 墨行数。
        /// 注意墨数比是 640/320 == 2（4 行 / 2 行），不是线宽比 4：1px 宽落在整点被 AA 摊到两行。
        /// </summary>
        [Fact]
        public void 线宽令牌改变尺寸线落墨厚度()
        {
            Dictionary<float, string> columns = new Dictionary<float, string>();
            Dictionary<float, int> inks = new Dictionary<float, int>();

            foreach (float width in new[] { 1f, 4f })
            {
                DimensionTokens tokens = new DimensionTokens();
                tokens.LineWidth = width;
                using (ImageProbe probe = Render(320, 120, painter =>
                {
                    DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), tokens,
                                          SKColors.Black, SKColors.Black, Text12);
                }))
                {
                    columns[width] = Describe(VerticalRuns(probe, 140, 40, 80));
                    inks[width] = InkCount(probe, 60, 50, 220, 70);
                }
            }

            Assert.Equal("59-60", columns[1f]);
            Assert.Equal("58-61", columns[4f]);
            Assert.Equal(320, inks[1f]);
            Assert.Equal(640, inks[4f]);
            AssertEqualMsg(2, inks[4f] / inks[1f], "墨数比 == 墨行数比 4/2（1px 线宽被 AA 摊到两行，不是线性 4 倍）");
        }

        /// <summary>
        /// <b>现状记录（疑似缺陷）</b>：<see cref="DimensionTokens.LineWidth"/> = 0 时线照旧落墨，
        /// 与宽度 1 的像素完全一致（实测第 140 列都是 "59-60"、窗口墨都是 320）——
        /// 根因在 <see cref="Painter"/> 的画笔缓存把 width&lt;=0 变成"填充画笔 + StrokeWidth 1"，
        /// 期望应是零线宽不落笔（或由调用方显式关掉）。
        /// </summary>
        [Fact]
        public void 现状_零线宽仍然落墨()
        {
            DimensionTokens zero = new DimensionTokens();
            zero.LineWidth = 0f;
            DimensionTokens one = new DimensionTokens();
            one.LineWidth = 1f;

            string zeroColumn, oneColumn;
            int zeroInk, oneInk;

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), zero,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                zeroColumn = Describe(VerticalRuns(probe, 140, 40, 80));
                zeroInk = InkCount(probe, 60, 50, 220, 70);
            }

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), one,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                oneColumn = Describe(VerticalRuns(probe, 140, 40, 80));
                oneInk = InkCount(probe, 60, 50, 220, 70);
            }

            Assert.Equal("59-60", zeroColumn);
            Assert.Equal(oneColumn, zeroColumn);
            Assert.Equal(oneInk, zeroInk);
            Assert.True(zeroInk > 0, "现状记录：零线宽仍有墨，期望为 0");
        }

        /// <summary>
        /// <see cref="DimensionTokens.ExtensionLineWidth"/> 生效（同一 x=80 列在 row110 的墨宽）：
        /// 0.8（默认）→ "79-80 219-220"；3 → "78-81 218-221"；6 → "77-82 217-222"。
        /// 复算：笔宽 w 居中覆盖 x∈[80−w/2, 80+w/2]，凡被覆盖 &gt; 0 的像素列都过阈值。
        /// </summary>
        [Fact]
        public void 延伸线宽令牌生效()
        {
            foreach (float width in new[] { 0.8f, 3f, 6f })
            {
                DimensionTokens tokens = GapTokens(8f, 4f);
                tokens.ExtensionLineWidth = width;
                string expected = width == 0.8f ? "79-80 219-220"
                                : width == 3f ? "78-81 218-221"
                                : "77-82 217-222";

                using (ImageProbe probe = RenderProfile(tokens, DefaultProfileRun(), ProfileY))
                {
                    AssertEqualMsg(expected, Describe(HorizontalRuns(probe, 110, 60, 240)),
                                 "ExtensionLineWidth = " + width);
                }
            }
        }

        /// <summary>
        /// <see cref="DimensionTokens.ArrowLength"/> 生效：实心三角的背边在 x = 端点 − L。
        /// 实测第 220 列：L=20 → "56-63"（背边正好压在 x=220）；L=5（默认）→ "59-60"（三角够不到这里，只剩线）。
        /// </summary>
        [Fact]
        public void 箭头长令牌改变三角底边位置()
        {
            DimensionTokens longArrow = new DimensionTokens();
            longArrow.ArrowLength = 20f;
            DimensionTokens shortArrow = new DimensionTokens();

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), longArrow,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            using (ImageProbe probe5 = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), shortArrow,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal("56-63", Describe(VerticalRuns(probe, 220, 40, 80)));
                Assert.Equal("59-60", Describe(VerticalRuns(probe5, 220, 40, 80)));
                AssertEqualMsg(5f, shortArrow.ArrowLength, "默认箭头长 5（实测）");
            }
        }

        /// <summary>
        /// <see cref="DimensionTokens.ArrowWidth"/> 生效（空心箭的斜翼变粗）：
        /// 1.2（默认）→ 第 224 列 "53-55 59-60 64-66"；4 → "52-56 59-60 63-67"。
        /// 复算：翼在 x=224 的中心 y = 60 ± (2×0.18×16) = 60 ± 5.76，加半笔宽与斜率贡献后覆盖行区间如上。
        /// </summary>
        [Fact]
        public void 空心箭线宽令牌加宽斜翼()
        {
            DimensionTokens thin = new DimensionTokens();
            thin.ArrowLength = 20f;
            DimensionTokens thick = new DimensionTokens();
            thick.ArrowLength = 20f;
            thick.ArrowWidth = 4f;

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Open, ArrowKind.Open), thin,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            using (ImageProbe probe4 = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Open, ArrowKind.Open), thick,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal("53-55 59-60 64-66", Describe(VerticalRuns(probe, 224, 40, 80)));
                Assert.Equal("52-56 59-60 63-67", Describe(VerticalRuns(probe4, 224, 40, 80)));
                AssertEqualMsg(3, VerticalRuns(probe, 224, 40, 80).Count, "细翼保持三段（中心留白）");
                Assert.Equal(3, VerticalRuns(probe4, 224, 40, 80).Count);
            }
        }

        /// <summary>
        /// <see cref="DimensionTokens.ArrowBaseRatio"/> 生效（实心三角半宽 = ratio × 到尖端距离）：
        /// 0.18（默认）→ 第 224 列 "57-62"（±2.88）；0.4 → "53-66"（±6.4）。
        /// </summary>
        [Fact]
        public void 三角底宽比例令牌生效()
        {
            DimensionTokens normal = new DimensionTokens();
            normal.ArrowLength = 20f;
            DimensionTokens wide = new DimensionTokens();
            wide.ArrowLength = 20f;
            wide.ArrowBaseRatio = 0.4f;

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), normal,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            using (ImageProbe probeWide = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo), wide,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal("57-62", Describe(VerticalRuns(probe, 224, 40, 80)));
                Assert.Equal("53-66", Describe(VerticalRuns(probeWide, 224, 40, 80)));
                Assert.True(InkCount(probeWide, 215, 40, 241, 80) > InkCount(probe, 215, 40, 241, 80),
                            "更大的底宽比例必须多落墨");
            }
        }

        /// <summary>
        /// <see cref="DimensionTokens.DotRadiusRatio"/> 生效（圆点外径 = ratio × 箭头长）：
        /// 0.24（默认，L=20 → 半径 4.8）在圆心列 x=235 的墨跨 54..65；
        /// 0.5（半径 10，圆心退到 x=230）墨跨 49..70。
        /// </summary>
        [Fact]
        public void 圆点半径比例令牌生效()
        {
            DimensionTokens small = new DimensionTokens();
            small.ArrowLength = 20f;
            DimensionTokens big = new DimensionTokens();
            big.ArrowLength = 20f;
            big.DotRadiusRatio = 0.5f;

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Dot, ArrowKind.Dot), small,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            using (ImageProbe probeBig = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, Linear(LinearFrom, LinearTo, ArrowKind.Dot, ArrowKind.Dot), big,
                                      SKColors.Black, SKColors.Black, Text12);
            }))
            {
                int[] smallSpread = SpreadOf(VerticalRuns(probe, 235, 40, 80));
                int[] bigSpread = SpreadOf(VerticalRuns(probeBig, 230, 40, 80));
                Assert.Equal(new int[] { 54, 65 }, smallSpread);
                Assert.Equal(new int[] { 49, 70 }, bigSpread);
            }
        }

        /// <summary>
        /// <see cref="DimensionTokens.TextOffset"/> 生效：数字沿法向整体平移。
        /// 复算：12 号字数字高实测 9 行（ascent −12.697266 + descent 3.140625 → 15.84 的可视部分），
        /// offset 14 与 30 相差 16 → 上下沿各自下移 16 行、左右范围不变（实测）。
        /// </summary>
        [Fact]
        public void 文字偏移令牌整体平移数字()
        {
            DimensionTokens near = new DimensionTokens();
            near.TextOffset = 14f;
            DimensionTokens far = new DimensionTokens();
            far.TextOffset = 30f;

            int[] nearBounds, farBounds;
            using (ImageProbe probe = Render(320, 140, painter =>
            {
                DimensionRun run = Linear(LinearFrom, LinearTo);
                run.Text = "80mm";
                DimensionPainter.Draw(painter, run, near, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                nearBounds = InkBounds(probe, 100, 65, 181, 110);
            }

            using (ImageProbe probe = Render(320, 160, painter =>
            {
                DimensionRun run = Linear(LinearFrom, LinearTo);
                run.Text = "80mm";
                DimensionPainter.Draw(painter, run, far, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                farBounds = InkBounds(probe, 100, 80, 181, 130);
            }

            Assert.True(nearBounds[2] > 0, "近处数字必须落墨");
            Assert.Equal(nearBounds[0], farBounds[0]);
            Assert.Equal(nearBounds[1], farBounds[1]);
            AssertEqualMsg(16, farBounds[2] - nearBounds[2], "上下沿同比下移 == 偏移差 30−14");
            Assert.Equal(16, farBounds[3] - nearBounds[3]);
            AssertEqualMsg(9, nearBounds[3] - nearBounds[2] + 1, "数字墨高（12 号）实测 9 行");
        }

        /// <summary>
        /// 字号生效（数字按字体度量落位，不是画死的）：派生串 "80mm"（200px ÷ 2.5）在
        /// TextOffset 20 的窗口 x[100,181) y[65,96) 实测：10 号墨列 125..154、20 号墨列 111..169；
        /// <see cref="Painter.TextWidth"/> 实测 30.46875 与 60.9375（严格 2 倍，线性随字号缩放）。
        /// </summary>
        [Fact]
        public void 字号令牌改变数字字宽与落墨范围()
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.TextOffset = 20f;

            DimensionRun MakeRun()
            {
                DimensionRun run = Linear(LinearFrom, LinearTo);
                run.UnitSuffix = "mm";
                run.SetMeasuredFromPixels(2.5d);
                Assert.Equal("80mm", run.DisplayText);
                return run;
            }

            int[] smallBounds, bigBounds;
            using (ImageProbe probe = Render(320, 140, painter =>
            {
                DimensionPainter.Draw(painter, MakeRun(), tokens, SKColors.Black, SKColors.Black, Text10);
            }))
            {
                smallBounds = InkBounds(probe, 100, 65, 181, 96);
            }

            using (ImageProbe probe = Render(320, 140, painter =>
            {
                DimensionPainter.Draw(painter, MakeRun(), tokens, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                bigBounds = InkBounds(probe, 100, 65, 181, 96);
            }

            Assert.Equal(125, smallBounds[0]);
            Assert.Equal(154, smallBounds[1]);
            Assert.Equal(111, bigBounds[0]);
            Assert.Equal(169, bigBounds[1]);

            Assert.Equal(30.46875d, MeasureWidth("80mm", Text10), 4);
            Assert.Equal(60.9375d, MeasureWidth("80mm", Text20), 4);
        }

        /// <summary>
        /// <see cref="DimensionTokens.BreakLineForText"/> 生效：数字压线时是否把线挖断。
        /// 测量过程：段 (40,76)→(240,76)、Text 四空格、20 号字、TextOffset 0（字心贴线），
        /// 实测中断盒 x∈(127,152] 内两行线都无墨，而关闭令牌后同一区间满墨（连续线）——
        /// 复算：盒宽 = 实测字宽 23.671875 + 2×默认留白 2 = 27.671875，中心 140 → 126.16..153.84 → 像素 127..152。
        /// </summary>
        [Fact]
        public void 断线令牌控制数字是否压断尺寸线()
        {
            DimensionRun SpaceRun()
            {
                DimensionRun run = Linear(new SKPoint(40f, 76f), new SKPoint(240f, 76f));
                run.Text = "    ";
                return run;
            }

            DimensionTokens broken = new DimensionTokens();
            broken.TextOffset = 0f;
            Assert.True(broken.BreakLineForText);

            using (ImageProbe probe = Render(320, 160, painter =>
            {
                DimensionPainter.Draw(painter, SpaceRun(), broken, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                Assert.False(probe.HasInkInBox(127, 75, 153, 78), "令牌开启时中断盒内两行线都必须无墨");
                Assert.True(probe.IsInk(126, 75) && probe.IsInk(126, 76), "正对照：中断盒外左侧仍有墨");
                Assert.True(probe.IsInk(153, 75) && probe.IsInk(153, 76), "正对照：中断盒外右侧仍有墨");
            }

            DimensionTokens solid = new DimensionTokens();
            solid.TextOffset = 0f;
            solid.BreakLineForText = false;

            using (ImageProbe probe = Render(320, 160, painter =>
            {
                DimensionPainter.Draw(painter, SpaceRun(), solid, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                Assert.True(probe.IsInk(140, 75) && probe.IsInk(140, 76), "令牌关闭时线必须连续穿过字位");
                Assert.True(probe.HasInkInBox(127, 75, 153, 77), "正对照：开启时被挖空的同一区间，关闭后满墨");
                Assert.False(probe.HasInkInBox(100, 60, 180, 74), "四空格本身不落墨，看到的只有线");
            }
        }

        /// <summary>
        /// <see cref="DimensionTokens.KnockoutPadding"/> 生效：留白每加 10，中断盒左右各扩 10（无墨列多 20）。
        /// 实测：留白 2 → 第 75 行白区 127..152、留白 12 → 白区 117..162；
        /// 复算：盒宽 = 实测字宽 23.671875 + 2×留白，中心固定在 140。
        /// </summary>
        [Fact]
        public void 中断留白令牌按左右各扩同量()
        {
            DimensionRun SpaceRun()
            {
                DimensionRun run = Linear(new SKPoint(40f, 76f), new SKPoint(240f, 76f));
                run.Text = "    ";
                return run;
            }

            DimensionTokens pad2 = new DimensionTokens();
            pad2.TextOffset = 0f;
            DimensionTokens pad12 = new DimensionTokens();
            pad12.TextOffset = 0f;
            pad12.KnockoutPadding = 12f;

            Assert.Equal(2f, new DimensionTokens().KnockoutPadding);

            int firstWhite2 = 0, lastWhite2 = 0, firstWhite12 = 0, lastWhite12 = 0;
            int ink2, ink12;

            using (ImageProbe probe = Render(320, 160, painter =>
            {
                DimensionPainter.Draw(painter, SpaceRun(), pad2, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                ink2 = InkCount(probe, 60, 74, 220, 78);
                firstWhite2 = FirstWhite(probe, 120, 160, 75);
                lastWhite2 = LastWhite(probe, 120, 160, 75);
            }

            using (ImageProbe probe = Render(320, 160, painter =>
            {
                DimensionPainter.Draw(painter, SpaceRun(), pad12, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                ink12 = InkCount(probe, 60, 74, 220, 78);
                firstWhite12 = FirstWhite(probe, 100, 200, 75);
                lastWhite12 = LastWhite(probe, 100, 200, 75);
            }

            Assert.Equal(127, firstWhite2);
            Assert.Equal(152, lastWhite2);
            Assert.Equal(117, firstWhite12);
            Assert.Equal(162, lastWhite12);
            AssertEqualMsg(20, (lastWhite12 - firstWhite12) - (lastWhite2 - firstWhite2), "白区宽度差 == 2×Δ留白");
            AssertEqualMsg(40, ink2 - ink12, "两行线少掉的墨 == 2×20 列");
        }

        /// <summary>
        /// <see cref="DimensionRun.SetTextKnockout(float,float,float?)"/> 的盒几何可复算：
        /// 盒 == 以锚点为中心（锚点 = 中点沿法向抬已解析偏移），实测 (80,60)→(220,60) + 偏移 0 时
        /// 30×14 → Left 135/Top 53，宽 20 → Left 140/Top 55；把令牌留白 2 与实测字宽 23.671875 代回源码算式
        /// （width = TextWidth + 2×pad）即得上面的中断盒 27.671875×?。
        /// </summary>
        [Fact]
        public void 中断盒几何可由公开定义复算()
        {
            DimensionRun run = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            AssertEqualMsg(SKRect.Empty, run.TextKnockout, "未设置过宽度时不中断");

            run.SetTextKnockout(30f, 14f, 0f);
            Assert.Equal(135f, run.TextKnockout.Left, 4);
            Assert.Equal(53f, run.TextKnockout.Top, 4);
            Assert.Equal(30f, run.KnockoutWidth);
            Assert.Equal(14f, run.KnockoutHeight);

            run.SetTextKnockout(20f, 10f, 0f);
            Assert.Equal(140f, run.TextKnockout.Left, 4);
            Assert.Equal(55f, run.TextKnockout.Top, 4);

            // 两参重载沿用上一次的中心（此处中心 = 段中点 150）：宽 40 → 左界 150 − 20 = 130。
            run.SetTextKnockoutWidth(40f);
            Assert.Equal(130f, run.TextKnockout.Left, 4);
            Assert.Equal(40f, run.TextKnockout.Width, 4);

            run.SetTextKnockout(0f, 0f, null);
            AssertEqualMsg(SKRect.Empty, run.TextKnockout, "宽度 0 == 取消中断");
        }

        /// <summary>
        /// <see cref="DimensionTokens.Clone"/> 独立副本（实测：改副本不影响原实例，默认值逐项为
        /// LineWidth 1 / ArrowLength 5 / ArrowWidth 1.2 / ExtensionLineWidth 0.8 / ExtensionGap 2 /
        /// ExtensionOver 3 / TextOffset 3 / KnockoutPadding 2 / BreakLineForText true /
        /// ArrowBaseRatio 0.18 / DotRadiusRatio 0.24 / CenterMarkRatio 1.6）。
        /// </summary>
        [Fact]
        public void 令牌克隆独立且默认值符合约定()
        {
            DimensionTokens original = new DimensionTokens();
            Assert.Equal(1f, original.LineWidth);
            Assert.Equal(5f, original.ArrowLength);
            Assert.Equal(1.2f, original.ArrowWidth);
            Assert.Equal(0.8f, original.ExtensionLineWidth);
            Assert.Equal(2f, original.ExtensionGap);
            Assert.Equal(3f, original.ExtensionOver);
            Assert.Equal(3f, original.TextOffset);
            Assert.Equal(2f, original.KnockoutPadding);
            Assert.True(original.BreakLineForText);
            Assert.Equal(0.18f, original.ArrowBaseRatio);
            Assert.Equal(0.24f, original.DotRadiusRatio);
            Assert.Equal(1.6f, original.CenterMarkRatio);

            DimensionTokens copy = original.Clone();
            copy.LineWidth = 9f;
            copy.ExtensionGap = 0f;
            Assert.Equal(1f, original.LineWidth);
            Assert.Equal(2f, original.ExtensionGap);
            Assert.NotSame(original, copy);
        }

        // ------------------------------------------------------------ 边界与副作用

        /// <summary>
        /// <b>现状记录（疑似缺陷）</b>：半径式（<see cref="DimensionOrientation.Radial"/>）按制图惯例跳过起点箭头
        /// （源码 <c>if (run.Orientation != Radial) DrawArrow(startTip, ...)</c>），于是"从圆心引出"的半径段
        /// 在圆心一侧没有任何端部符号，只剩中心标记；起点侧判据窗口（x 44..59、去掉尺寸线自身所在的
        /// 59/60 两行、也避开 x=40 处的中心标记竖笔）实测半径式 == 0，而同样版式的线性式 &gt; 0（正对照）。
        /// 半径式的终点侧窗口（x 221..239）实测 &gt; 0，证明"只是跳起点、不是两端都不画"。
        /// </summary>
        [Fact]
        public void 半径式跳过起点箭头()
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.ArrowLength = 20f;

            DimensionRun radial = new DimensionRun(LinearFrom, LinearTo, DimensionOrientation.Radial,
                                                    ArrowKind.Filled, ArrowKind.Filled);
            DimensionRun linear = Linear(LinearFrom, LinearTo);

            // 起点/终点符号墨：跳过尺寸线本身的两行（y=60、线宽 1 → 行 59/60），只数符号。
            static int StartSymbolInk(ImageProbe p)
            {
                return InkCount(p, 44, 40, 60, 59) + InkCount(p, 44, 61, 60, 80);
            }

            static int EndSymbolInk(ImageProbe p)
            {
                return InkCount(p, 221, 40, 240, 59) + InkCount(p, 221, 61, 240, 80);
            }

            // 中心标记的竖笔（在 x=40 那条短细线上，半径式才有）：取线上方的几行，避开尺寸线。
            static int CenterMarkInk(ImageProbe p)
            {
                return InkCount(p, 38, 44, 43, 56);
            }

            int radialStart, radialEnd, radialMark, linearStart, linearEnd, linearMark;
            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, radial, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                radialStart = StartSymbolInk(probe);
                radialEnd = EndSymbolInk(probe);
                radialMark = CenterMarkInk(probe);
            }

            using (ImageProbe probe = Render(320, 120, painter =>
            {
                DimensionPainter.Draw(painter, linear, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                linearStart = StartSymbolInk(probe);
                linearEnd = EndSymbolInk(probe);
                linearMark = CenterMarkInk(probe);
            }

            AssertEqualMsg(0, radialStart, "半径式起点侧除了尺寸线本身没有任何符号墨");
            Assert.True(linearStart > 0, "正对照：线性式起点侧有实心三角墨（箭头长 20 → 行 57/58 与 61..63 落墨）");
            Assert.True(radialEnd > 0, "半径式终点侧照常落符号（只跳起点）");
            AssertEqualMsg(linearEnd, radialEnd, "两端符号里只有起点那一侧被族别影响");
            Assert.True(radialMark > 0, "半径式在起点画中心标记（两条互相垂直的短细线）");
            AssertEqualMsg(0, linearMark, "线性式不画中心标记（正对照的另一半）");
        }

        /// <summary>
        /// <b>现状记录（疑似缺陷）</b>：零长段（两端点重合）在落笔前就被 length 阈值拦下，
        /// 连 <see cref="DimensionRun.Text"/> 也不会落纸（实测整幅零墨）；
        /// 期望是"线可以不画但读数还得写"。同一段只要有一丁点长度就正常落墨（正对照）。
        /// </summary>
        [Fact]
        public void 现状_零长段连文字都不落()
        {
            DimensionRun zero = Linear(new SKPoint(100f, 60f), new SKPoint(100f, 60f));
            zero.Text = "0";
            Assert.Equal(0d, zero.LengthInPixels);
            Assert.Equal("0", zero.DisplayText);

            using (ImageProbe probe = Render(200, 120, painter =>
            {
                DimensionPainter.Draw(painter, zero, new DimensionTokens(), SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.Equal(0, InkCount(probe, 0, 0, 200, 120));
            }

            DimensionRun tiny = Linear(new SKPoint(100f, 60f), new SKPoint(100.05f, 60f));
            using (ImageProbe probe = Render(200, 120, painter =>
            {
                tiny.Text = "0";
                DimensionPainter.Draw(painter, tiny, new DimensionTokens(), SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.True(probe.HasInkInBox(90, 60, 115, 75), "正对照：够长一点就落墨，说明阈值是长度而非文字");
            }
        }

        /// <summary>数字始终水平落位（PDF/SVG 检索性优先，不转画布）：竖直尺寸线的 "100" 实测墨框 21×9（宽×高），
        /// 而非旋转后的 9×21；锚点 (234,110) == 中点 (220,110) 沿法向 (−1,0) 抬 −14（实测）。</summary>
        [Fact]
        public void 数字在竖直尺寸线上仍保持水平()
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.TextOffset = -14f;

            DimensionRun run = Linear(new SKPoint(220f, 60f), new SKPoint(220f, 160f));
            run.Text = "100";
            Assert.Equal(new SKPoint(234f, 110f), run.TextAnchorAt(-14f));

            using (ImageProbe probe = Render(320, 220, painter =>
            {
                DimensionPainter.Draw(painter, run, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                int[] bounds = InkBounds(probe, 222, 80, 300, 120);
                Assert.Equal(new int[] { 224, 244, 106, 114 }, bounds);
                AssertEqualMsg(21, bounds[1] - bounds[0] + 1, "墨宽 == 实测字宽 21.111（12 号 \"100\"）");
                AssertEqualMsg(9, bounds[3] - bounds[2] + 1, "墨高远小于段长 100 → 未随方向旋转");
                Assert.Equal(92, InkCount(probe, 222, 80, 300, 120));
            }
        }

        /// <summary>
        /// <b>现状记录（绘制副作用）</b>：<see cref="DimensionPainter.Draw"/> 会把"按实测字宽重算的中断盒"
        /// 写回 <see cref="DimensionRun.TextKnockout"/> 并**留在段上**（实测：画完仍能读到
        /// Left/Top/Width/Height 非零的那一盒），只有数字抬到线外、本来就不压线时才写成空盒。
        /// 因此调用方手填的中断盒不参与落笔，事后读到的却是绘制用的中断几何——
        /// 这是一个暴露给调用方的副作用，命中测试若复用该属性会拿到绘制期的值而不是手填值。
        /// 测量过程：段 (60,60)→(260,60)、Text 为四个空格（有宽度但本身不落墨，便于把"线的墨"和"字的墨"分开）、
        /// 20 号字、TextOffset 0 → 复算中断盒宽 = 实测字宽 23.671875 + 2×留白 2 = 27.671875，
        /// 中心固定在线中点 x=160 → 白区 x∈(146.16, 173.84)；若真用了手填的 300 宽，白区会盖到 x=140。
        /// </summary>
        [Fact]
        public void 绘制会改写段上的中断状态()
        {
            DimensionRun run = Linear(new SKPoint(60f, DimY), new SKPoint(260f, DimY));
            run.Text = "    ";

            // 手填一个明显更宽的中断盒：绘制层会覆盖它（按实测字宽重算）。
            run.SetTextKnockout(300f, 14f, 0f);
            Assert.False(run.TextKnockout.IsEmpty);

            DimensionTokens pressed = new DimensionTokens();
            pressed.TextOffset = 0f;

            using (ImageProbe probe = Render(320, 140, painter =>
            {
                DimensionPainter.Draw(painter, run, pressed, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                Assert.False(probe.HasInkInBox(148, 58, 173, 62), "重算盒内两行线被挖空（空格本身无墨）");
                Assert.True(probe.IsInk(140, 59) && probe.IsInk(140, 60),
                            "正对照：白区左界 146.16 之外仍有墨 → 落笔用的是重算窄盒，不是手填的 300 宽");
                Assert.True(probe.IsInk(180, 59) && probe.IsInk(180, 60), "正对照：白区右界之外同样有墨");
                // 实测现状（与 leg 原先写的"Draw 收尾清零"相反）：Draw 把按实测字宽重算的中断盒
                // **留在段上**，调用方因此能读到本次落笔用的中断几何。盒心 == 段中点 (60+260)/2 = 160，
                // 盒宽 == 实测空串字宽 + 左右各一份 KnockoutPadding。
                Assert.False(run.TextKnockout.IsEmpty, "Draw 之后段上仍带着本次用的中断盒");
                AssertEqualMsg(MeasureWidth("    ", Text20) + 2f * pressed.KnockoutPadding,
                               run.TextKnockout.Width, "盒宽 == 实测字宽 + 两侧留白");
                AssertEqualMsg(160f, (run.TextKnockout.Left + run.TextKnockout.Right) / 2f,
                               "盒心 == 段中点");
            }

            // 数字抬到线外（|偏移 24| ≥ 半盒高 ≈13.9）时绘制前就写零，线保持连续穿过字位。
            DimensionTokens lifted = new DimensionTokens();
            lifted.TextOffset = 24f;
            using (ImageProbe probe = Render(320, 140, painter =>
            {
                DimensionPainter.Draw(painter, run, lifted, SKColors.Black, SKColors.Black, Text20);
            }))
            {
                Assert.True(probe.IsInk(160, 59) && probe.IsInk(160, 60), "线连续（字已抬到 y=84，空格不落墨）");
                AssertEqualMsg(SKRect.Empty, run.TextKnockout, "现状：不压线 → 中断状态被写空");
            }

            // 未绘制的手动中断盒仍可被命中测试读到，画一次即失效。
            DimensionRun manual = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            manual.SetTextKnockout(30f, 14f, 0f);
            Assert.False(manual.TextKnockout.IsEmpty);
            DimensionRun other = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            other.Text = "50";
            Assert.True(other.TextKnockout.IsEmpty);
        }

        /// <summary>绘制器只管落笔：不压裁剪栈、空参数按约定抛错。</summary>
        [Fact]
        public void 绘制不泄漏裁剪状态且空参数抛错()
        {
            using (SKSurface? surface = SKSurface.Create(new SKImageInfo(CanvasW, CanvasH,
                                                                         SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                Assert.NotNull(surface);
                using (Painter painter = new Painter(surface!.Canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    DimensionRun run = Linear(LinearFrom, LinearTo);
                    run.Text = "100mm";
                    DimensionTokens tokens = new DimensionTokens();

                    DimensionPainter.Draw(painter, run, tokens, SKColors.Black, SKColors.Black, Text12);
                    DimensionPainter.DrawSet(painter, new[] { run }, tokens, SKColors.Black, SKColors.Black, Text12, true);

                    AssertEqualMsg(0, painter.ClipDepth, "落笔后裁剪栈必须归零");

                    Assert.Throws<ArgumentNullException>(() =>
                        DimensionPainter.Draw(null!, run, tokens, SKColors.Black, SKColors.Black, Text12));
                    Assert.Throws<ArgumentNullException>(() =>
                        DimensionPainter.Draw(painter, null!, tokens, SKColors.Black, SKColors.Black, Text12));
                    Assert.Throws<ArgumentNullException>(() =>
                        DimensionPainter.Draw(painter, run, null!, SKColors.Black, SKColors.Black, Text12));
                    Assert.Throws<ArgumentNullException>(() =>
                        DimensionPainter.DrawSet(painter, null!, tokens, SKColors.Black, SKColors.Black, Text12, true));
                    Assert.Throws<ArgumentNullException>(() =>
                        DimensionPainter.DrawSet(painter, new[] { run }, null!, SKColors.Black, SKColors.Black, Text12, true));

                    // DrawSet 对 null 元素是跳过而不是抛（实测：链式/坐标式的空位允许留白）。
                    DimensionRun[] withHole = { null!, run };
                    DimensionPainter.DrawSet(painter, withHole, tokens,
                                             SKColors.Black, SKColors.Black, Text12, false);
                    Assert.Equal(0, painter.ClipDepth);
                }
            }
        }

        /// <summary>延伸线只服务于线性/链式：半径式画中心标记、坐标式靠引线本身连接，都不另起延伸线。</summary>
        [Fact]
        public void 族别决定延伸线与中心标记的落笔()
        {
            DimensionTokens tokens = GapTokens(8f, 4f);

            DimensionRun radial = new DimensionRun(new SKPoint(40f, DimY), new SKPoint(240f, DimY),
                                                    DimensionOrientation.Radial, ArrowKind.Filled, ArrowKind.Filled);
            radial.ProfileFrom = new SKPoint(XFrom, ProfileY);
            radial.ProfileTo = new SKPoint(XTo, ProfileY);

            using (ImageProbe probe = Render(320, 200, painter =>
            {
                DimensionPainter.Draw(painter, radial, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                Assert.False(probe.HasInkInBox(78, 100, 83, 152), "半径式不画延伸线");
                Assert.True(probe.HasInkInBox(39, 51, 42, 59),
                            "正对照：半径式在起点 (40,60) 落中心标记（两条互相垂直的短细线，竖笔过 x=40）");
            }

            DimensionRun linear = DefaultProfileRun();
            using (ImageProbe probe = Render(320, 200, painter =>
            {
                DimensionPainter.Draw(painter, linear, tokens, SKColors.Black, SKColors.Black, Text12);
            }))
            {
                // 本例只画尺寸段、不画轮廓线（RenderProfile 那套取图才落轮廓），
                // 所以列 80 上只有延伸线 [56,152) 的墨，没有 158..161 那段轮廓墨。
                Assert.Equal("56-151", Describe(VerticalRuns(probe, 80, 40, 180)));
            }
        }

        // ------------------------------------------------------------ 带消息的断言助手

        // xUnit 的 Assert.Equal/Assert.Null 没有 (expected, actual, message) 这种重载——第三参会被
        // 解析成 IEqualityComparer/比较委托而编译不过，而本仓库的约定是断言必须自带中文诊断。
        private static void AssertEqualMsg<T>(T expected, T actual, string message)
        {
            Assert.True(EqualityComparer<T>.Default.Equals(expected, actual),
                        message + "（期望 " + expected + "，实到 " + actual + "）");
        }

        private static void AssertNullMsg(object? value, string message)
        {
            Assert.True(value == null, message + "（实到 " + value + "）");
        }

        // ------------------------------------------------------------ 取像素与构造的公共件

        private static DimensionRun Linear(SKPoint from, SKPoint to)
        {
            return new DimensionRun(from, to, DimensionOrientation.Linear, ArrowKind.Filled, ArrowKind.Filled);
        }

        private static DimensionRun Linear(SKPoint from, SKPoint to, ArrowKind arrowFrom, ArrowKind arrowTo)
        {
            return new DimensionRun(from, to, DimensionOrientation.Linear, arrowFrom, arrowTo);
        }

        /// <summary>水平尺寸 (80,60)→(220,60) + 下方 y=160 的轮廓点（gap 判据的标准版式）。</summary>
        private static DimensionRun DefaultProfileRun()
        {
            DimensionRun run = Linear(new SKPoint(XFrom, DimY), new SKPoint(XTo, DimY));
            run.ProfileFrom = new SKPoint(XFrom, ProfileY);
            run.ProfileTo = new SKPoint(XTo, ProfileY);
            return run;
        }

        private static DimensionTokens GapTokens(float gap, float over)
        {
            DimensionTokens tokens = new DimensionTokens();
            tokens.ExtensionGap = gap;
            tokens.ExtensionOver = over;
            return tokens;
        }

        /// <summary>
        /// 标准版式取像素：先在 y=<paramref name="profileY"/> 落一条水平轮廓线（x 30..290，线宽
        /// <see cref="ProfileLineWidth"/>），再画该段。轮廓是否被延伸线穿过，就看这一条与尺寸线之间的列。
        /// </summary>
        private static ImageProbe RenderProfile(DimensionTokens tokens, DimensionRun run, float profileY)
        {
            return Render(CanvasW, CanvasH, painter =>
            {
                painter.Line(30f, profileY, 290f, profileY, SKColors.Black, ProfileLineWidth);
                DimensionPainter.Draw(painter, run, tokens, SKColors.Black, SKColors.Black, Text12);
            });
        }

        /// <summary>96dpi 下逻辑像素 == 设备像素，判据不用换算。</summary>
        private static ImageProbe Render(int width, int height, Action<Painter> draw)
        {
            using (SKSurface? surface = SKSurface.Create(new SKImageInfo(width, height,
                                                                         SKColorType.Bgra8888, SKAlphaType.Premul)))
            {
                Assert.NotNull(surface);
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    draw(painter);
                }

                using (SKImage image = surface.Snapshot())
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return ImageProbe.FromPng(data.ToArray());
                }
            }
        }

        /// <summary>只为复算字宽开一个绘制器（不落任何笔）。</summary>
        private static float MeasureWidth(string text, FontKey font)
        {
            using (SKSurface? surface = SKSurface.Create(new SKImageInfo(8, 8, SKColorType.Bgra8888, SKAlphaType.Premul)))
            using (Painter painter = new Painter(surface!.Canvas, new ChartStyle(), Chart.SharedFonts))
            {
                return painter.TextWidth(text, font);
            }
        }

        /// <summary>某列上连续墨段的行区间（闭区间）。</summary>
        private static List<int[]> VerticalRuns(ImageProbe probe, int x, int fromY, int toY)
        {
            return Runs(probe, fromY, toY, probe.Height, (int i) => probe.IsInk(x, i));
        }

        /// <summary>某行上连续墨段的列区间（闭区间）。</summary>
        private static List<int[]> HorizontalRuns(ImageProbe probe, int y, int fromX, int toX)
        {
            return Runs(probe, fromX, toX, probe.Width, (int i) => probe.IsInk(i, y));
        }

        /// <summary>
        /// 一维墨段扫描。<paramref name="axisExtent"/> 必须是<b>被扫描那一根轴</b>的像素数：
        /// 早先这里按"高与宽取小者"收口，于是横扫在 320×200 的画布上悄悄停在 x=199，
        /// 把"延伸线没画出来"这类假缺陷制造了出来。
        /// </summary>
        private static List<int[]> Runs(ImageProbe probe, int from, int to, int axisExtent, Func<int, bool> ink)
        {
            List<int[]> runs = new List<int[]>();
            int start = -1;
            int stop = Math.Min(to, axisExtent - 1);
            for (int i = Math.Max(0, from); i <= stop; i++)
            {
                if (ink(i) && start < 0)
                {
                    start = i;
                }
                else if (!ink(i) && start >= 0)
                {
                    runs.Add(new int[] { start, i - 1 });
                    start = -1;
                }
            }
            if (start >= 0)
            {
                runs.Add(new int[] { start, stop });
            }
            return runs;
        }

        private static string Describe(List<int[]> runs)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < runs.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(runs[i][0]).Append('-').Append(runs[i][1]);
            }
            return sb.ToString();
        }

        /// <summary>首段起点到末段终点的墨跨（闭区间）。</summary>
        private static int[] SpreadOf(List<int[]> runs)
        {
            Assert.NotEmpty(runs);
            return new int[] { runs[0][0], runs[runs.Count - 1][1] };
        }

        private static double[] Lengths(DimensionChain chain)
        {
            double[] lengths = new double[chain.Runs.Count];
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = chain.Runs[i].LengthInPixels;
            }
            return lengths;
        }

        private static List<string?> Texts(DimensionChain chain)
        {
            List<string?> texts = new List<string?>();
            for (int i = 0; i < chain.Runs.Count; i++)
            {
                texts.Add(chain.Runs[i].DisplayText);
            }
            return texts;
        }

        /// <summary>窗口内墨像素数（左闭右开）。</summary>
        private static int InkCount(ImageProbe probe, int left, int top, int right, int bottom)
        {
            int count = 0;
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    if (probe.IsInk(x, y))
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>窗口内墨的外接框 {minX, maxX, minY, maxY}（闭区间，绝对坐标）；无墨则抛。</summary>
        private static int[] InkBounds(ImageProbe probe, int left, int top, int right, int bottom)
        {
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    if (!probe.IsInk(x, y))
                    {
                        continue;
                    }
                    if (x < minX)
                    {
                        minX = x;
                    }
                    if (x > maxX)
                    {
                        maxX = x;
                    }
                    if (y < minY)
                    {
                        minY = y;
                    }
                    if (y > maxY)
                    {
                        maxY = y;
                    }
                }
            }

            Assert.True(maxX >= minX, "窗口内没有墨：x[" + left + "," + right + ") y[" + top + "," + bottom + ")");
            return new int[] { minX, maxX, minY, maxY };
        }

        /// <summary>该行上窗口内第一个无墨列；整行都有墨则返回窗口右端（越界标记）。</summary>
        private static int FirstWhite(ImageProbe probe, int left, int right, int y)
        {
            for (int x = left; x < right; x++)
            {
                if (!probe.IsInk(x, y))
                {
                    return x;
                }
            }
            return right;
        }

        /// <summary>该行上窗口内最后一个无墨列；整行都有墨则返回窗口左端减一（越界标记）。</summary>
        private static int LastWhite(ImageProbe probe, int left, int right, int y)
        {
            for (int x = right - 1; x >= left; x--)
            {
                if (!probe.IsInk(x, y))
                {
                    return x;
                }
            }
            return left - 1;
        }
    }
}
