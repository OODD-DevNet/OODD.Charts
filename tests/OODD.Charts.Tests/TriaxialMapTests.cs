using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T217：<see cref="TriaxialMap"/> 三角坐标映射（重心定位 + 逆算）的验收测试。
    /// <para>
    /// 判据逐字对照 docs/07 T217 行：三顶点 (1,0,0)/(0,1,0)/(0,0,1) 落几何顶点；
    /// (1/3,1/3,1/3) 落重心；<c>ComponentsAt(PointOf(a,b,c))</c> 往返误差小于 1e-6；
    /// 非归一输入按比例归一后位置不变。
    /// </para>
    /// <para>
    /// 容差都能由参与运算的数值量级解释：
    /// <list type="bullet">
    /// <item><description>顶点/分量比较用 float 单 ulp 一档：坐标量级 ≤128 时
    /// float ulp = 2^-23 × 2^6 = 7.63e-6，半 ulp = 3.82e-6；
    /// 重心/顶点这类"期望值本身可精确表示"的用例直接断言严格相等。</description></item>
    /// <item><description>往返 1e-6 来自源码契约（TriaxialMap.ComponentsAt 的 XML 注释）。
    /// 误差主项是 PointOf 输出的 float 量化（≤3.8e-6 逻辑像素，坐标 ≤96 一档），
    /// 换算到分量空间要乘 |另一边|/|叉积|：三个测试三角形的该系数分别约
    /// 0.023、0.017、0.013（分量每像素），即约 1e-7 一档，1e-6 留了 8 倍以上余量。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// docs/06 §3.5 把 <c>ComponentsAt</c> 的返回写成元组 (double A, double B, double C)，
    /// 源码实际返回 <see cref="TriaxialComponents"/> 结构体——按任务口径以源码为准。
    /// </para>
    /// </summary>
    public sealed class TriaxialMapTests
    {
        private readonly ITestOutputHelper _output;

        public TriaxialMapTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // ------------------------------------------------------------ 测试三角形
        //
        // T1：重心 (40,30) 为整数像素，像素用例也用它。
        // T2：A-B-C 在屏幕坐标系里顺时针（叉积为负），覆盖"旋向无关"的声明。
        // T3：小数坐标 + 钝角，面积|ABxAC|=6657、最小夹角正弦约 0.92，非扁三角形。

        private static readonly SKPoint[] T1Vertices =
        {
            new SKPoint(10f, 10f), new SKPoint(70f, 10f), new SKPoint(40f, 70f),
        };

        private static readonly SKPoint[] T2Vertices =
        {
            new SKPoint(0f, 0f), new SKPoint(0f, 60f), new SKPoint(60f, 0f),
        };

        private static readonly SKPoint[] T3Vertices =
        {
            new SKPoint(12.5f, 7.25f), new SKPoint(96f, 18.5f), new SKPoint(33.75f, 89f),
        };

        private static TriaxialMap MakeMap(SKPoint[] vertices)
        {
            return new TriaxialMap(vertices[0], vertices[1], vertices[2]);
        }

        private static TriaxialMap[] AllMaps()
        {
            return new[] { MakeMap(T1Vertices), MakeMap(T2Vertices), MakeMap(T3Vertices) };
        }

        // ------------------------------------------------------------ 判据 1：三顶点落位

        [Fact]
        public void 三顶点分量各自落在对应几何顶点上()
        {
            foreach (TriaxialMap map in AllMaps())
            {
                // 严格相等成立的原因：权重 1/0/0 时分子是 1×顶点坐标（float→double 精确），
                // 除以 sum=1 不改变值，转回 float 仍是原值。
                Assert.True(map.PointOf(1d, 0d, 0d) == map.VertexA, "a=(1,0,0) 应落 VertexA：" + map.VertexA);
                Assert.True(map.PointOf(0d, 1d, 0d) == map.VertexB, "b=(0,1,0) 应落 VertexB：" + map.VertexB);
                Assert.True(map.PointOf(0d, 0d, 1d) == map.VertexC, "c=(0,0,1) 应落 VertexC：" + map.VertexC);

                // VertexOf 的大小写不敏感别名与三个公开顶点属性一致。
                Assert.Equal(map.VertexA, map.VertexOf('A'));
                Assert.Equal(map.VertexB, map.VertexOf('b'));
                Assert.Equal(map.VertexC, map.VertexOf('C'));
            }

            Assert.Throws<ArgumentException>(() => MakeMap(T1Vertices).VertexOf('D'));
            Assert.Throws<ArgumentException>(() => MakeMap(T1Vertices).VertexOf('1'));
        }

        // ------------------------------------------------------------ 判据 2：重心

        [Fact]
        public void 三分量相等时落在解析复算的几何重心上()
        {
            foreach (SKPoint[] vertices in new[] { T1Vertices, T2Vertices, T3Vertices })
            {
                TriaxialMap map = MakeMap(vertices);

                // 解析式复算重心：G = ((Ax+Bx+Cx)/3, (Ay+By+Cy)/3)，double 域内求值。
                double gx = ((double)vertices[0].X + vertices[1].X + vertices[2].X) / 3d;
                double gy = ((double)vertices[0].Y + vertices[1].Y + vertices[2].Y) / 3d;

                // 容差 1e-5：坐标量级 &lt;128，float 单 ulp 至多 7.63e-6，允许一次 float 量化。
                double[] weights = { 1d, 2d, 1d / 3d };  // (1,1,1)、(2,2,2)、(1/3,1/3,1/3) 三种写法
                foreach (double w in weights)
                {
                    SKPoint p = map.PointOf(w, w, w);
                    Assert.True(Math.Abs(p.X - gx) <= 1e-5f && Math.Abs(p.Y - gy) <= 1e-5f,
                                "权重 " + w + " 落点 " + p + "，解析重心 ("
                                + gx.ToString("0.######", CultureInfo.InvariantCulture) + ", "
                                + gy.ToString("0.######", CultureInfo.InvariantCulture) + ")");
                }

                // T1 重心是整数像素 (40,30)，(1,1,1) 路径分子分母都是精确整数商，直接钉死。
                if (vertices == T1Vertices)
                {
                    Assert.Equal(new SKPoint(40f, 30f), map.PointOf(1d, 1d, 1d));
                }
            }
        }

        // ------------------------------------------------------------ 判据 3：往返一致性

        [Fact]
        public void 分量与像素往返后逐分量误差小于百万分之一()
        {
            // 代表性分量组：顶点、重心、三条边中点、边上非中点、近顶点、退化到某分量为 0、
            // 未归一（和≠1）的正数组。
            double[][] samples =
            {
                new[] { 1d, 0d, 0d },
                new[] { 0d, 1d, 0d },
                new[] { 0d, 0d, 1d },
                new[] { 1d / 3d, 1d / 3d, 1d / 3d },
                new[] { 0.5d, 0.5d, 0d },
                new[] { 0d, 0.5d, 0.5d },
                new[] { 0.5d, 0d, 0.5d },
                new[] { 0.3d, 0.4d, 0.3d },
                new[] { 0.1d, 0.1d, 0.8d },
                new[] { 0.25d, 0.25d, 0.5d },
                new[] { 0.02d, 0.49d, 0.49d },
                new[] { 0.98d, 0.01d, 0.01d },
                new[] { 0.9999d, 5e-5d, 5e-5d },   // 极近顶点：b、c 分量约 5e-5
                new[] { 2d, 4d, 6d },
                new[] { 30d, 40d, 30d },
                new[] { 0.001d, 0.002d, 0.003d },
            };

            double maxComponentError = 0d;
            double maxSumError = 0d;
            string worst = string.Empty;

            foreach (TriaxialMap map in AllMaps())
            {
                foreach (double[] abc in samples)
                {
                    double sum = abc[0] + abc[1] + abc[2];
                    SKPoint p = map.PointOf(abc[0], abc[1], abc[2]);
                    TriaxialComponents back = map.ComponentsAt(p);

                    double ea = Math.Abs(back.A - abc[0] / sum);
                    double eb = Math.Abs(back.B - abc[1] / sum);
                    double ec = Math.Abs(back.C - abc[2] / sum);
                    double e = Math.Max(ea, Math.Max(eb, ec));
                    if (e > maxComponentError)
                    {
                        maxComponentError = e;
                        worst = "[" + map.VertexA + "|" + map.VertexB + "|" + map.VertexC + "] "
                              + abc[0] + "/" + abc[1] + "/" + abc[2]
                              + " -> " + p + " -> " + back;
                    }

                    double sumError = Math.Abs(back.A + back.B + back.C - 1d);
                    maxSumError = Math.Max(maxSumError, sumError);

                    Assert.True(e < 1e-6, "往返误差超限：" + e + "，用例 " + worst);
                    // 逆算契约：三分量之和为 1。量级 1、双精度三次除法+加法，误差不超过几个 ulp(1)。
                    Assert.True(sumError <= 1e-14, "分量和偏离 1：" + sumError + "，用例 " + back);
                }
            }

            // 实测最大往返误差（报告引用；日志由 `dotnet test -l "console;verbosity=detailed"` 读出）。
            _output.WriteLine("往返最大逐分量误差 = "
                             + maxComponentError.ToString("0.############E-0", CultureInfo.InvariantCulture)
                             + "，最大 |sum-1| = "
                             + maxSumError.ToString("0.############E-0", CultureInfo.InvariantCulture)
                             + "，来自 " + worst);
            Assert.True(maxComponentError < 1e-6, "往返最大误差 " + maxComponentError + " 违反 1e-6 契约");
        }

        // ------------------------------------------------------------ 判据 4：非归一输入按比例归一

        [Fact]
        public void 非归一输入成比例缩放后落同一像素()
        {
            foreach (TriaxialMap map in AllMaps())
            {
                // 源码 XML 的原例：(30,40,30) 与 (0.3,0.4,0.3) 同点。两式 double 中间值差约 1e-14 相对，
                // 远小于坐标量级(&lt;128)的 float ulp 7.63e-6，转 float 后逐位一致，可断严格相等。
                Assert.Equal(map.PointOf(0.3d, 0.4d, 0.3d), map.PointOf(30d, 40d, 30d));
                Assert.Equal(map.PointOf(1d, 2d, 3d), map.PointOf(2d, 4d, 6d));
                Assert.Equal(map.PointOf(1d, 2d, 3d), map.PointOf(1e8d, 2e8d, 3e8d));
                Assert.Equal(map.PointOf(1d, 1d, 1d), map.PointOf(2d, 2d, 2d));
                Assert.Equal(map.PointOf(0d, 1d, 0d), map.PointOf(0d, 3d, 0d));
                Assert.Equal(map.PointOf(1d / 3d, 1d / 3d, 1d / 3d), map.PointOf(1d, 1d, 1d));
            }
        }

        // ------------------------------------------------------------ 非法输入与现状边界

        [Fact]
        public void 非法输入按源码约定拒绝并记录极小正和现状()
        {
            TriaxialMap map = MakeMap(T1Vertices);

            Assert.Throws<ArgumentException>(() => map.PointOf(0d, 0d, 0d));                    // 全 0
            Assert.Throws<ArgumentException>(() => map.PointOf(-1d, -2d, -3d));                 // 和为负
            Assert.Throws<ArgumentException>(() => map.PointOf(1d, -1d, 0d));                   // 和恰为 0
            Assert.Throws<ArgumentException>(() => map.PointOf(double.NaN, 1d, 1d));
            Assert.Throws<ArgumentException>(() => map.PointOf(1d, double.NaN, 1d));
            Assert.Throws<ArgumentException>(() => map.PointOf(1d, 1d, double.NaN));
            Assert.Throws<ArgumentException>(() => map.PointOf(double.PositiveInfinity, 1d, 1d));
            Assert.Throws<ArgumentException>(() => map.PointOf(double.NegativeInfinity, 1d, 1d));

            // 现状记录（非期望值改动）：和为正但 <= Epsilon(1e-9) 同样抛 ArgumentException。
            // 源码 PointOf 的 XML 注释把"数值上退化为 0 的极小正值"列为有意行为；
            // 副作用是整体缩小 1e-10 倍的合法比例输入(1e-10,1e-10,1e-10)会被拒——
            // 与"按比例归一后位置不变"在极端缩放下不成立。断言的是当前行为。
            Assert.Throws<ArgumentException>(() => map.PointOf(1e-10d, 0d, 0d));
            Assert.Throws<ArgumentException>(() => map.PointOf(1e-10d, 1e-10d, 1e-10d));

            // ComponentsAt 对非有限像素点的防御分支：抛 ArgumentException（源码 236-241 行）。
            Assert.Throws<ArgumentException>(() => map.ComponentsAt(new SKPoint(float.NaN, 0f)));
            Assert.Throws<ArgumentException>(() => map.ComponentsAt(new SKPoint(0f, float.PositiveInfinity)));
        }

        [Fact]
        public void 混合符号且和为正时不拒绝但落在三角域外()
        {
            TriaxialMap map = MakeMap(T1Vertices);

            // 源码声明：PointOf 不校验符号。(2,-1,0)：x=(2·10-1·70+0)/1=-50，y=(2·10-1·10)/1=10，
            // 期望值由 T1 顶点解析复算，整数中间值全部精确，可钉严格相等。
            Assert.Equal(new SKPoint(-50f, 10f), map.PointOf(2d, -1d, 0d));
            Assert.False(map.IsInside(2d, -1d, 0d));

            // 逆算三角形外部的点返回越界分量（源码 XML：外部落点分量可为负或大于 1）。
            // P=(100,100) 的重心坐标可由 T1 顶点解析解出：60b+30c=90 且 60c=90 →
            // c=1.5、b=0.75、a=1-b-c=-1.25。中间值均为二进制可精确表示的数，逐分量钉死。
            TriaxialComponents back = map.ComponentsAt(new SKPoint(100f, 100f));
            Assert.True(Math.Abs(back.A - (-1.25d)) <= 1e-12d, "外部点 a 应为 -1.25，实得 " + back.A);
            Assert.True(Math.Abs(back.B - 0.75d) <= 1e-12d, "外部点 b 应为 0.75，实得 " + back.B);
            Assert.True(Math.Abs(back.C - 1.5d) <= 1e-12d, "外部点 c 应为 1.5，实得 " + back.C);
        }

        [Fact]
        public void IsInside_按_Epsilon_容差判定三角域归属()
        {
            TriaxialMap map = MakeMap(T1Vertices);

            Assert.True(map.IsInside(1d / 3d, 1d / 3d, 1d / 3d));
            Assert.True(map.IsInside(0d, 0d, 1d));            // 顶点合法（源码：0 与 1 是合法组分）
            Assert.True(map.IsInside(1d, 0d, 0d));
            Assert.True(map.IsInside(0.5d, 0.5d, 0d));        // 边上点
            Assert.True(map.IsInside(-1e-10d, 0.5d, 0.5d));   // 0 的容差边界内（-1e-10 >= -1e-9）
            Assert.True(map.IsInside(1d + 1e-10d, 0d, 0d));   // 1 的容差边界内
            Assert.False(map.IsInside(-1e-8d, 0.5d, 0.5d));   // 越界一个数量级
            Assert.False(map.IsInside(1.000001d, 0d, 0d));
            Assert.False(map.IsInside(0d, 0d, 0d));           // 和不为正
            Assert.False(map.IsInside(1e-10d, 1e-10d, 1e-10d)); // 和 <= Epsilon，与 PointOf 抛异常一致
            Assert.False(map.IsInside(double.NaN, 0.5d, 0.5d));
        }

        // ------------------------------------------------------------ 构造期退化拦截

        [Fact]
        public void 共线_重合或非有限顶点的构造被拒绝()
        {
            // 三点共线（含水平/垂直/斜线三种姿势）。
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(0f, 0f), new SKPoint(10f, 10f), new SKPoint(20f, 20f)));
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(0f, 5f), new SKPoint(10f, 5f), new SKPoint(20f, 5f)));
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(5f, 0f), new SKPoint(5f, 10f), new SKPoint(5f, 20f)));

            // 两点重合 / 三点重合：叉积与 crossScale 同为 0，源码注释确认 0 <= eps·0 成立。
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(5f, 5f), new SKPoint(5f, 5f), new SKPoint(10f, 0f)));
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(5f, 5f), new SKPoint(0f, 9f), new SKPoint(5f, 5f)));
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(7f, 7f), new SKPoint(7f, 7f), new SKPoint(7f, 7f)));

            // 非有限顶点。
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(float.NaN, 0f), new SKPoint(10f, 0f), new SKPoint(0f, 10f)));
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(0f, 0f), new SKPoint(float.PositiveInfinity, 0f), new SKPoint(0f, 10f)));
        }

        [Fact]
        public void 近共线判定用夹角正弦而非绝对偏移()
        {
            // 大三角形：A(0,0) B(100,0) C(200,1e-7)，sin≈det/(|AB||AC|)=5.0e-10 <= Epsilon(1e-9) → 拒绝。
            Assert.Throws<ArgumentException>(() =>
                new TriaxialMap(new SKPoint(0f, 0f), new SKPoint(100f, 0f), new SKPoint(200f, 1e-7f)));

            // 小三角形取更大的绝对偏移 2e-7，但 sin≈1.0e-8 > Epsilon → 接受。
            // 两条并置证明判据与图纸绝对尺寸无关（源码 XML 的设计声明）。
            TriaxialMap flat = new TriaxialMap(new SKPoint(0f, 0f), new SKPoint(10f, 0f), new SKPoint(20f, 2e-7f));
            Assert.Equal(new SKPoint(10f, 0f), flat.PointOf(0d, 1d, 0d));

            // 更"胖"一档的近共线同样接受；只验顶点落位，不验往返——
            // 源码自述精度按 1/sin(最小夹角) 放大，极扁三角形不在 1e-6 契约的适用范围内（现状如实记录）。
            TriaxialMap wider = new TriaxialMap(new SKPoint(0f, 0f), new SKPoint(100f, 0f), new SKPoint(200f, 1e-5f));
            Assert.Equal(new SKPoint(0f, 0f), wider.PointOf(1d, 0d, 0d));
            Assert.Equal(new SKPoint(200f, 1e-5f), wider.PointOf(0d, 0d, 1d));
        }

        // ------------------------------------------------------------ 像素用例（真实管线）

        [Fact]
        public void 经Chart管线三顶点与重心落在预期像素上()
        {
            // 换算口径：版面 bounds 是 96dpi 逻辑像素 (0,0,100,100)，Encode(dpi=96) 时设备缩放 1:1，
            // 逻辑像素号 = 设备像素号，无需再乘系数；若换 dpi，坐标与容差都按 dpi/96 同步放大。
            // 容差 0.5 设备像素的理由：墨迹段中心按整像素段取中点，圆心落在整像素上时
            // 抗锯齿只会在圆周边界 ±1 像素内增减灰边，且四侧对称、中心不偏移；0.5 已容住
            // 单侧一个像素的不对称。
            using (Chart chart = new Chart(new ChartDocument(), new TriaxialTriangleTemplate(), new ChartStyle()))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.Equal(100, probe.Width);
                Assert.Equal(100, probe.Height);

                // 三个顶点分量按源码映射落笔（期望像素即模板声明的几何顶点，可由公开定义直接复算）。
                Assert.True(probe.IsInk(10, 10), "VertexA(10,10) 圆心应有墨");
                Assert.True(probe.IsInk(70, 10), "VertexB(70,10) 圆心应有墨");
                Assert.True(probe.IsInk(40, 70), "VertexC(40,70) 圆心应有墨");

                // 实心圆（半径 4）的墨迹段中心：行 y=10 上有 A、B 两个圆，段中心应分别钉在 10 和 70。
                IReadOnlyList<double> row10 = probe.VerticalRulesAt(10, 0, 100);
                Assert.True(row10.Count == 2, "y=10 行应只有 A、B 两段墨，实到 " + row10.Count + "：" + Join(row10));
                Assert.True(Math.Abs(row10[0] - 10d) <= 0.5, "A 圆段中心 " + row10[0] + "，期望 10");
                Assert.True(Math.Abs(row10[1] - 70d) <= 0.5, "B 圆段中心 " + row10[1] + "，期望 70");

                IReadOnlyList<double> col10 = probe.HorizontalRulesAt(10, 0, 100);
                Assert.True(col10.Count == 1, "x=10 列应只有 A 圆一段墨，实到 " + col10.Count + "：" + Join(col10));
                Assert.True(Math.Abs(col10[0] - 10d) <= 0.5, "A 圆段中心 " + col10[0] + "，期望 10");

                IReadOnlyList<double> row70 = probe.VerticalRulesAt(70, 0, 100);
                Assert.True(row70.Count == 1, "y=70 行应只有 C 圆一段墨，实到 " + row70.Count + "：" + Join(row70));
                Assert.True(Math.Abs(row70[0] - 40d) <= 0.5, "C 圆段中心 " + row70[0] + "，期望 40");

                // 重心：Marker(Ring, r=6, stroke=2) 画空心圈，圈心 (40,30) 必须留白，
                // 环带在过心行/列上的两段中心 (34,46)、(24,36) 的中点即圈心，且间距为 2r。
                Assert.False(probe.IsInk(40, 30), "空心环的圈心不该有墨");

                IReadOnlyList<double> ringRow = probe.VerticalRulesAt(30, 20, 60);
                Assert.True(ringRow.Count == 2, "过圈心行应读到环带左右两段，实到 " + ringRow.Count + "：" + Join(ringRow));
                Assert.True(Math.Abs((ringRow[0] + ringRow[1]) / 2d - 40d) <= 0.5,
                            "环带段中点应为圈心 x=40，实得 " + Join(ringRow));
                Assert.True(Math.Abs((ringRow[1] - ringRow[0]) - 12d) <= 1.5,
                            "环带内外缘中点间距应约 2r=12，实得 " + (ringRow[1] - ringRow[0]));

                IReadOnlyList<double> ringCol = probe.HorizontalRulesAt(40, 20, 40);
                Assert.True(ringCol.Count == 2, "过圈心列应读到环带上下两段，实到 " + ringCol.Count + "：" + Join(ringCol));
                Assert.True(Math.Abs((ringCol[0] + ringCol[1]) / 2d - 30d) <= 0.5,
                            "环带段中点应为圈心 y=30，实得 " + Join(ringCol));

                // 版面角落没有杂散墨（映射之外没落笔）。
                Assert.False(probe.HasInkInBox(0, 0, 5, 5), "左上角不该有墨");
                Assert.False(probe.HasInkInBox(50, 50, 70, 90), "右下空白区不该有墨");
            }
        }

        private static string Join(IReadOnlyList<double> values)
        {
            return string.Join(", ", System.Linq.Enumerable.Select(values, v => v.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        // ------------------------------------------------------------ 走管线用的小模板

        /// <summary>
        /// 最小模板：把 <see cref="TriaxialMap"/> 的三个顶点分量与等分量点经
        /// <c>Chart.Record → RenderImage → Encode</c> 真实链路落到 PNG 上，不做版面含义。
        /// </summary>
        private sealed class TriaxialTriangleTemplate : IChartTemplate
        {
            private static readonly SKPoint VertexA = new SKPoint(10f, 10f);
            private static readonly SKPoint VertexB = new SKPoint(70f, 10f);
            private static readonly SKPoint VertexC = new SKPoint(40f, 70f);

            private const float CanvasWidth = 100f;
            private const float CanvasHeight = 100f;

            public string Key => "test.triaxial-triangle";

            public string DisplayName => "三角坐标落像素试版";

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                SKRect canvas = new SKRect(0f, 0f, CanvasWidth, CanvasHeight);
                return new ChartGeometry(
                    canvas,
                    new SlotAxis(0f, 10f, 1, 1),
                    new LinearAxis("rule", 0d, 10d, 0f, CanvasHeight, 1d, 5d),
                    new LinearAxis[0],
                    new Band("header", new SKRect(0f, 0f, CanvasWidth, 2f), new BandRow[0]),
                    new Band("date", new SKRect(0f, 2f, CanvasWidth, 4f), new BandRow[0]),
                    canvas,
                    new LabelColumn[0],
                    new Band("cells", canvas, new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, CanvasHeight - 2f, CanvasWidth, CanvasHeight), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                TriaxialMap map = new TriaxialMap(VertexA, VertexB, VertexC);

                // 三个顶点分量：半径 4 的实心圆，圆心正好压在整像素 (10,10)/(70,10)/(40,70)。
                painter.FillCircle(map.PointOf(1d, 0d, 0d), 4f, SKColors.Black);
                painter.FillCircle(map.PointOf(0d, 1d, 0d), 4f, SKColors.Black);
                painter.FillCircle(map.PointOf(0d, 0d, 1d), 4f, SKColors.Black);

                // 等分量点（重心）：空心环，半径 6、线粗 2，圈心 (40,30) 留白。
                painter.Marker(MarkerSymbol.Ring, map.PointOf(1d / 3d, 1d / 3d, 1d / 3d), 6f, SKColors.Black, 2f);
            }
        }
    }
}
