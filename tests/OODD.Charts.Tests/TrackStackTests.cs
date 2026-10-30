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
    /// T216 多轨堆叠与深度分页验收（docs/07 T216 行、docs/06 §3.5 与 §4.4）。
    /// <para>
    /// 四条判据逐个取证：
    /// <list type="bullet">
    /// <item><description>倒置域轴（<c>AxisDirection.VerticalDown</c>）下深度 0 落在道体顶边，且深度增大单调向下；</description></item>
    /// <item><description>续页时每一页的图头像素位置完全一致（纯几何与设备像素两侧都钉）；</description></item>
    /// <item><description>续页接缝值连续、像素级相接（<c>SplitByDomain</c> 与 <c>DepthPager</c> 两条切分路都测）；</description></item>
    /// <item><description>线性道与对数道混排时量程互不污染，改一道量程不动另一道。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 版面常量全部取整数：96dpi 下 <c>Chart.RenderImage</c> 的设备缩放是 1，
    /// 逻辑像素 == 设备像素，"笔触以坐标为中心、宽 2 像素" 覆盖的就是 x-1 与 x 两列，
    /// 因此实测段中心恒为 <c>坐标 - 0.5</c>。这条换算在下面每个像素断言里都写明了来源，
    /// 期望值一律由 <see cref="TrackStack"/> 自己求解出来的矩形复算，不是拍脑袋的数。
    /// </para>
    /// <para>
    /// 与文档的口径差异（以源码为准，详见各用例注释）：docs/07 把 <c>SplitByDomain</c> 记在
    /// <c>DepthPager</c> 名下，源码里它是 <see cref="TrackStack"/> 的实例方法（<c>DepthPager</c>
    /// 只有 <c>Split</c>/<c>SplitWithOverlap</c>/<c>IndexRangeOf</c>）；docs/06 §3.5 的
    /// <c>TrackStack</c> 构造签名比源码少三个参数。两条都不修，只在此登记。
    /// </para>
    /// </summary>
    public sealed class TrackStackTests
    {
        // ------------------------------------------------------------ 版面常量（整数，便于设备像素对账）

        /// <summary>栈最左缘像素 x。</summary>
        private const float StackLeft = 20f;

        /// <summary>每道像素宽（三道用例里另有单独指定的宽度）。</summary>
        private const float TrackWidth = 100f;

        /// <summary>道间间隙像素。</summary>
        private const float TrackGap = 20f;

        /// <summary>图头高（像素）。</summary>
        private const float HeaderHeight = 24f;

        /// <summary>倒置深度轴的像素起点 = 页顶（深度 0 处）。</summary>
        private const float TopY = 60f;

        /// <summary>倒置深度轴的像素终点 = 页底（最大深度处）。</summary>
        private const float BottomY = 360f;

        /// <summary>页宽（逻辑像素 = 96dpi 设备像素）。</summary>
        private const float PageWidth = 260f;

        /// <summary>页高。</summary>
        private const float PageHeight = 420f;

        // 道的横向边界：由 Left + 宽 + 间隙 复算，用来与 TrackStack 的求解结果对账。
        private const float RtLeft = StackLeft;                 // 20
        private const float RtRight = RtLeft + TrackWidth;      // 120
        private const float SpLeft = RtRight + TrackGap;        // 140
        private const float SpRight = SpLeft + TrackWidth;      // 240

        /// <summary>整段深度区间（页顶 0m，页底 1000m）。</summary>
        private const double DepthTop = 0d;
        private const double DepthBottom = 1000d;

        /// <summary>每页覆盖的深度跨度。</summary>
        private const double PageDepth = 250d;

        /// <summary>线性道（视电阻率）量程。</summary>
        private const double RtMin = 0d;
        private const double RtMax = 400d;

        /// <summary>对数道（自然电位）量程。</summary>
        private const double SpMin = 1d;
        private const double SpMax = 1000d;

        /// <summary>
        /// 取样深度步长（米）：整版 0~1000m 共 21 个点，每页 250m 共 6 个点。
        /// 页内取样点落在同样的相对位置上，所以各页曲线的 y 完全一致，
        /// 而取值按"绝对深度"查表 → 不同页的 x 不同，正好用来做反向对照。
        /// </summary>
        private const double SampleStep = 50d;

        /// <summary>
        /// 线性道取值表（下标 = 深度/50）：含 -500（远小于量程下限）与端点值 0/400，
        /// 用来证明"越界值被自己的值轴夹紧后仍留在自己道内"。
        /// 表里没有相邻的同侧极值，免得曲线沿道边竖着爬出一段假"分隔线"。
        /// </summary>
        private static readonly double[] RtByDepthIndex =
        {
            0d, 400d, -500d, 200d, 400d, 100d, 0d, 300d, 5000d, 150d, 400d,
            20d, 0d, 250d, -500d, 380d, 400d, 90d, 0d, 200d, 400d,
        };

        /// <summary>对数道取值表（下标 = 深度/50）：含 0.001（小于下限）与 5000（超上限）。</summary>
        private static readonly double[] SpByDepthIndex =
        {
            1d, 1000d, 0.001d, 10d, 1000d, 100d, 1d, 300d, 5000d, 3d, 1000d,
            1d, 0.001d, 30d, 200d, 1d, 1000d, 50d, 1d, 500d, 1000d,
        };

        /// <summary>线粗令牌：图头线与道体分隔线用 2 像素，段中心即 坐标-0.5（见类注释推导）。</summary>
        private const float SeparatorStroke = 2f;

        /// <summary>曲线线粗（像素）：与分隔线同粗，越界判据的窗口余量按它算。</summary>
        private const float CurveStroke = 2f;

        // ------------------------------------------------------------ 判据①：倒置域轴

        [Fact]
        public void 道栈_倒置深度轴零深度落在道体顶边且单调向下()
        {
            // 倒置深度轴的公开定义：From=页顶、To=页底、值向下增大。
            NumericAxis depth = new NumericAxis("depth", DepthTop, DepthBottom, TopY, BottomY,
                                                AxisDirection.VerticalDown);
            Assert.Equal((double)TopY, (double)depth.PositionOf(0d), 4);
            Assert.Equal((double)BottomY, (double)depth.PositionOf(DepthBottom), 4);

            TrackStack stack = MakeStack(DepthTop, DepthBottom, Rt(), Sp());

            // 深度 0 的像素 == 道体矩形的上边界（"深度 0 在页顶"的字面含义）。
            Assert.True(Math.Abs(stack.DomainPositionOf(0d) - stack.RectOf(0).Top) < 1e-4,
                        "深度 0 的像素 " + stack.DomainPositionOf(0d) + " 不等于道体上边界 "
                        + stack.RectOf(0).Top + "，第 0 道矩形 " + Describe(stack.RectOf(0)));
            Assert.True(Math.Abs(stack.DomainPositionOf(DepthBottom) - stack.RectOf(0).Bottom) < 1e-4,
                        "最大深度的像素 " + stack.DomainPositionOf(DepthBottom)
                        + " 不等于道体下边界 " + stack.RectOf(0).Bottom);

            // 每 100m 占 30px：(BottomY-TopY)/(DepthBottom-DepthTop)*100 = 300/1000*100 = 30（可由公开属性复算）。
            Assert.Equal(30d, (double)(stack.DomainPositionOf(100d) - stack.DomainPositionOf(0d)), 4);
            Assert.Equal(300d, (double)(stack.DomainPositionOf(1000d) - stack.DomainPositionOf(0d)), 4);

            // 单调：深度增大 → 像素 y 增大（向下），逐 100m 核验。
            double previous = stack.DomainPositionOf(0d);
            for (double d = 100d; d <= DepthBottom; d += 100d)
            {
                double current = stack.DomainPositionOf(d);
                Assert.True(current > previous,
                            "倒置深度轴必须自上而下递增：深度 " + d + " 的像素 " + current
                            + " 没有比前一深度 " + (d - 100d) + " 的像素 " + previous + " 更靠下");
                previous = current;
            }

            // 夹紧：页外深度不越出道体（负深度停在页顶）。
            Assert.True(Math.Abs(stack.DomainPositionOf(-500d) - TopY) < 1e-4,
                        "负深度应夹在页顶 " + TopY + "，实到 " + stack.DomainPositionOf(-500d));
            Assert.True(Math.Abs(stack.DomainPositionOf(1200d) - BottomY) < 1e-4,
                        "超范围深度应夹在页底 " + BottomY + "，实到 " + stack.DomainPositionOf(1200d));

            // 与正向轴（VerticalUp，From=页底）方向相反 —— docs/06 §4.4 断言①要求显式测这一条。
            NumericAxis upward = new NumericAxis("time", DepthTop, DepthBottom, BottomY, TopY,
                                                 AxisDirection.VerticalUp);
            Assert.True(Math.Abs(upward.PositionOf(0d) - BottomY) < 1e-4,
                        "正向轴 0 值应在像素终点 " + BottomY + "，实到 " + upward.PositionOf(0d));
            Assert.True(depth.PositionOf(300d) < upward.PositionOf(300d),
                        "同一深度 300m：倒置轴 " + depth.PositionOf(300d) + " 必须比正向轴 "
                        + upward.PositionOf(300d) + " 更靠上");

            // 共享域轴与各道的值轴互不干扰：不同道、不同取值，同一深度的 y 必须完全相同。
            SKPoint inLinear = stack.PointOf(0, 400d, 250d);
            SKPoint inLog = stack.PointOf(1, 400d, 2d);
            Assert.True(inLinear.Y == inLog.Y,
                        "同一深度 400m 在两道的 y 不一致：" + inLinear.Y + " vs " + inLog.Y);
            Assert.True(Math.Abs(inLinear.Y - stack.DomainPositionOf(400d)) < 1e-4,
                        "PointOf 的 y 必须走共享域轴，实到 " + inLinear.Y
                        + "，域轴给 " + stack.DomainPositionOf(400d));
        }

        // ------------------------------------------------------------ 判据②：每页图头像素位置一致

        [Fact]
        public void 道栈_图头在道体之上且续页逐页像素位置一致()
        {
            TrackStack whole = MakeStack(DepthTop, DepthBottom, Rt(), Sp());

            // 图头矩形 = 道体左右边界 + 上边界往上 HeaderHeight。数值可由公开常量复算：
            // (20, 60-24, 120, 60) 与 (140, 36, 240, 60)。
            SKRect head0 = whole.HeaderRectOf(0);
            Assert.True(head0 == new SKRect(RtLeft, TopY - HeaderHeight, RtRight, TopY),
                        "第 0 道图头矩形应为 (20,36,120,60)，实到 " + Describe(head0));
            SKRect head1 = whole.HeaderRectOf(1);
            Assert.True(head1 == new SKRect(SpLeft, TopY - HeaderHeight, SpRight, TopY),
                        "第 1 道图头矩形应为 (140,36,240,60)，实到 " + Describe(head1));

            IReadOnlyList<TrackSegment> pages = whole.SplitByDomain(PageDepth);
            Assert.Equal(4, pages.Count);

            List<string> measured = new List<string>();
            foreach (TrackSegment page in pages)
            {
                // 每页一个栈：像素版面（TopY/BottomY/HeaderHeight）不变，只有域值区间换段。
                TrackStack perPage = MakePageStack(page);
                for (int i = 0; i < whole.Count; i++)
                {
                    SKRect want = whole.HeaderRectOf(i);
                    SKRect got = perPage.HeaderRectOf(i);
                    measured.Add("第" + page.Index + "页" + i + "道 y[" + got.Top + "," + got.Bottom + "] x["
                                 + got.Left + "," + got.Right + "]");
                    Assert.True(want == got,
                                "第 " + page.Index + " 页（深度 " + page.DomainStart + "~" + page.DomainEnd
                                + "）第 " + i + " 道图头与整版不一致：期望 " + Describe(want)
                                + "，实到 " + Describe(got) + "；已测各页图头 " + string.Join(" | ", measured.ToArray()));

                    // 图头文字与量程说明也必须逐页原样重复（续页页头的内容一致性）。
                    Assert.Equal(whole.Headers[i].Label, perPage.Headers[i].Label);
                    Assert.Equal(whole.Headers[i].ScaleCaption, perPage.Headers[i].ScaleCaption);
                    Assert.Equal(whole.Headers[i].Key, perPage.Headers[i].Key);
                }

                // 该页的域端点仍精确收在页顶/页底：末段（短页）也不例外。
                Assert.True(Math.Abs(perPage.DomainPositionOf(page.DomainStart) - TopY) < 1e-4,
                            "第 " + page.Index + " 页首深度 " + page.DomainStart + " 的像素应为页顶 " + TopY
                            + "，实到 " + perPage.DomainPositionOf(page.DomainStart));
                Assert.True(Math.Abs(perPage.DomainPositionOf(page.DomainEnd) - BottomY) < 1e-4,
                            "第 " + page.Index + " 页末深度 " + page.DomainEnd + " 的像素应为页底 " + BottomY
                            + "，实到 " + perPage.DomainPositionOf(page.DomainEnd));
            }

            // 末段是短页（1000-750=250 恰好整除；这里另造一条不整除的续页场景验证图头不受页长影响）。
            IReadOnlyList<TrackSegment> uneven = whole.SplitByDomain(300d);
            TrackStack lastPage = MakePageStack(uneven[uneven.Count - 1]);
            for (int i = 0; i < whole.Count; i++)
            {
                Assert.True(whole.HeaderRectOf(i) == lastPage.HeaderRectOf(i),
                            "末段短页（深度 " + uneven[uneven.Count - 1].DomainStart + "~"
                            + uneven[uneven.Count - 1].DomainEnd + "）图头位置漂移：期望 "
                            + Describe(whole.HeaderRectOf(i)) + "，实到 " + Describe(lastPage.HeaderRectOf(i)));
            }

            // HeaderHeight = 0 时图头是空矩形（渲染层据此跳过图头行）。
            TrackLayoutOptions noHeader = MakeOptions();
            noHeader.HeaderHeight = 0f;
            TrackStack bare = new TrackStack("well-log", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                                             new[] { Rt(), Sp() }, noHeader);
            Assert.True(bare.HeaderRectOf(0) == SKRect.Empty,
                        "无图头时图头矩形应为空，实到 " + Describe(bare.HeaderRectOf(0)));
        }

        // ------------------------------------------------------------ 判据③：续页接缝

        [Fact]
        public void 道栈_续页接缝域值与像素都精确相接()
        {
            TrackStack stack = MakeStack(DepthTop, DepthBottom, Rt(), Sp());
            IReadOnlyList<TrackSegment> pages = stack.SplitByDomain(PageDepth);

            Assert.Equal(4, pages.Count);
            Assert.Equal(0, pages[0].Index);
            Assert.Equal(3, pages[3].Index);
            Assert.Equal(DepthTop, pages[0].DomainStart, 9);
            Assert.Equal(DepthBottom, pages[pages.Count - 1].DomainEnd, 9);

            for (int i = 0; i + 1 < pages.Count; i++)
            {
                TrackSegment current = pages[i];
                TrackSegment next = pages[i + 1];

                // 域值接缝：源码把两段边界都算成 DomainMin + (i+1)*segmentLength，
                // 同一条表达式 → 逐位相同，所以这里用精确相等而不是容差。
                Assert.True(current.DomainEnd == next.DomainStart,
                            "接缝域值不连续：第 " + i + " 页末 " + current.DomainEnd
                            + " != 第 " + (i + 1) + " 页首 " + next.DomainStart);

                // 像素接缝：同一个域值过同一个映射，float 结果也必须逐位相同。
                Assert.True(current.PixelTo == next.PixelFrom,
                            "接缝像素不连续：第 " + i + " 页末像素 " + current.PixelTo
                            + " != 第 " + (i + 1) + " 页首像素 " + next.PixelFrom);

                // 页首间距 == pageDepth：0.25/250 都是二进制精确量，减法无舍入。
                Assert.True(next.DomainStart - current.DomainStart == PageDepth,
                            "相邻页首深度差应等于每页跨度 " + PageDepth + "，实到 "
                            + (next.DomainStart - current.DomainStart));
            }

            // 接缝像素与整版域轴一致：不能切完自己再算一套。
            for (int i = 0; i < pages.Count; i++)
            {
                Assert.True(pages[i].PixelFrom == stack.DomainPositionOf(pages[i].DomainStart),
                            "第 " + i + " 页起始像素 " + pages[i].PixelFrom + " 与整版域轴 "
                            + stack.DomainPositionOf(pages[i].DomainStart) + " 不一致");
            }

            Assert.Throws<ArgumentException>(() => stack.SplitByDomain(0d));
            Assert.Throws<ArgumentException>(() => stack.SplitByDomain(-250d));
        }

        [Fact]
        public void 道栈_计划里的接缝判据与源码口径不同按源码现状记录()
        {
            // docs/07 T216 的字面判据是"第 N 页末深度 + pageDepth == 第 N+1 页首深度"。
            // 源码的 SplitByDomain 给的是首尾重合的连续段（第 N 页末 == 第 N+1 页首），
            // 按字面判据必然不成立。这里如实记录当前行为：接缝是"共边界"而不是"相差一页"，
            // 与 docs/06 §4.4 断言④"第 N 页末深度与 N+1 页首深度差 == pageDepth"（差为 0）同样对不上。
            TrackStack stack = MakeStack(DepthTop, DepthBottom, Rt(), Sp());
            IReadOnlyList<TrackSegment> pages = stack.SplitByDomain(PageDepth);

            Assert.True(pages[0].DomainEnd == pages[1].DomainStart,
                        "现状：相邻段共边界（第0页末 " + pages[0].DomainEnd + " / 第1页首 "
                        + pages[1].DomainStart + "）");
            Assert.False(pages[0].DomainEnd + PageDepth == pages[1].DomainStart,
                        "计划判据" + "「第 N 页末深度 + pageDepth == 第 N+1 页首深度」在源码下成立？"
                        + "实测 " + pages[0].DomainEnd + " + " + PageDepth + " = "
                        + (pages[0].DomainEnd + PageDepth) + "，第 1 页首 = " + pages[1].DomainStart);
            Assert.True(pages[0].DomainEnd + PageDepth == pages[2].DomainStart,
                        "隔一页才等于计划口径的字面式：" + (pages[0].DomainEnd + PageDepth)
                        + " vs 第 2 页首 " + pages[2].DomainStart);
        }

        [Fact]
        public void 道栈_非整除分段长度接缝仍逐位相同()
        {
            // 0.1/0.3 这类小数是"逐段累加必漂移"的经典量（0.1 累加十次 = 0.9999999999999999）。
            // 源码注释声明一律用 DomainMin + i*segmentLength 的乘法式，接缝因此逐位相同。
            TrackStack fine = MakeStack(0d, 3d, Rt(), Sp());
            IReadOnlyList<TrackSegment> pages = fine.SplitByDomain(0.3);

            // (3-0)/0.3 在 double 上是 10.00000000000000x，源码先减 1e-9 容差再 ceil → 10 段。
            Assert.Equal(10, pages.Count);

            for (int i = 0; i < pages.Count; i++)
            {
                // 期望值按源码同一乘法式复算（不写死十进制字面量：i*0.3 本身就带二进制尾数）。
                Assert.True(pages[i].DomainStart == i * 0.3,
                            "第 " + i + " 段起点应等于 " + i + "*0.3 = " + (i * 0.3) + "，实到 "
                            + pages[i].DomainStart);

                double wantEnd = i == pages.Count - 1 ? 3d : (i + 1) * 0.3;
                Assert.True(pages[i].DomainEnd == wantEnd,
                            "第 " + i + " 段终点应为 " + wantEnd + "，实到 " + pages[i].DomainEnd);

                if (i + 1 < pages.Count)
                {
                    Assert.True(pages[i].DomainEnd == pages[i + 1].DomainStart,
                                "小数分段接缝断裂：第 " + i + " 段末 " + pages[i].DomainEnd
                                + " != 第 " + (i + 1) + " 段首 " + pages[i + 1].DomainStart);
                    Assert.True(pages[i].PixelTo == pages[i + 1].PixelFrom,
                                "小数分段像素接缝断裂：第 " + i + " 段末像素 " + pages[i].PixelTo
                                + " != 第 " + (i + 1) + " 段首像素 " + pages[i + 1].PixelFrom);
                }
            }

            Assert.Equal(3d, pages[pages.Count - 1].DomainEnd, 9);
        }

        [Fact]
        public void 深度分页_Split接缝与样本索引首尾相接()
        {
            // DepthPager 是另一条切分路（docs/07 把 SplitByDomain 记在它名下，源码里它只有 Split）。
            List<DomainRange> pages = DepthPager.Split(DepthTop, DepthBottom, PageDepth);
            Assert.Equal(4, pages.Count);
            Assert.Equal("[0,250]", pages[0].ToString());
            Assert.Equal("[750,1000]", pages[3].ToString());
            for (int i = 0; i + 1 < pages.Count; i++)
            {
                Assert.True(pages[i].End == pages[i + 1].Start,
                            "分页接缝域值断裂：" + pages[i] + " 与 " + pages[i + 1]);
                Assert.Equal(i, pages[i].Index);
                Assert.True(pages[i].IsLast == (i == pages.Count - 1),
                            "IsLast 只该落在末段，第 " + i + " 段实到 " + pages[i].IsLast);
            }

            // 外扩：接缝处两端各让 overlap，页间取样区间重叠而不是留缝（续页曲线不断）。
            List<DomainRange> overlapPages = DepthPager.SplitWithOverlap(DepthTop, DepthBottom, PageDepth, 20d);
            Assert.Equal(4, overlapPages.Count);
            Assert.Equal(0d, overlapPages[0].Start, 9);           // 首段左端被 from 截住
            Assert.Equal(270d, overlapPages[0].End, 9);
            Assert.Equal(230d, overlapPages[1].Start, 9);
            Assert.Equal(1000d, overlapPages[3].End, 9);          // 末段右端被 to 截住
            for (int i = 0; i + 1 < overlapPages.Count; i++)
            {
                Assert.True(overlapPages[i].End > overlapPages[i + 1].Start,
                            "外扩后相邻页必须重叠，实到 " + overlapPages[i] + " / " + overlapPages[i + 1]);
            }

            // 数据侧接缝：每 50m 一个取样点，落在接缝上的点被两页共用 → 没有缺点也没有留缝。
            List<double> depths = new List<double>();
            for (double d = DepthTop; d <= DepthBottom; d += 50d)
            {
                depths.Add(d);
            }

            int first0, last0, first1, last1;
            DepthPager.IndexRangeOf(depths, pages[0], out first0, out last0);
            DepthPager.IndexRangeOf(depths, pages[1], out first1, out last1);
            Assert.Equal(0, first0);
            Assert.Equal(5, last0);      // 0,50,100,150,200,250
            Assert.Equal(5, first1);     // 250 是接缝上的共用点
            Assert.Equal(10, last1);     // 到 500
            Assert.True(first1 == last0, "接缝样本点应被相邻两页共用，实到 " + last0 + " → " + first1);

            // 空序列是常态而不是异常：返回 (0,-1)。
            int emptyFirst, emptyLast;
            DepthPager.IndexRangeOf(new double[0], pages[0], out emptyFirst, out emptyLast);
            Assert.Equal(0, emptyFirst);
            Assert.Equal(-1, emptyLast);

            Assert.Throws<ArgumentException>(() => DepthPager.Split(DepthTop, DepthBottom, 0d));
            Assert.Throws<ArgumentException>(() => DepthPager.Split(DepthBottom, DepthTop, PageDepth));
            Assert.Throws<ArgumentException>(() => DepthPager.SplitWithOverlap(0d, 100d, 10d, -1d));
        }

        // ------------------------------------------------------------ 判据④：线性道与对数道混排

        [Fact]
        public void 道栈_线性道与对数道混排各自量程只在各自道内生效()
        {
            TrackStack stack = MakeStack(DepthTop, DepthBottom, Rt(), Sp());
            SKRect rtRect = stack.RectOf(0);
            SKRect spRect = stack.RectOf(1);

            // 道矩形由 Left/Width/TrackGap 复算：20..120 与 140..240。
            Assert.True(rtRect == new SKRect(RtLeft, TopY, RtRight, BottomY),
                        "线性道矩形应为 (20,60,120,360)，实到 " + Describe(rtRect));
            Assert.True(spRect == new SKRect(SpLeft, TopY, SpRight, BottomY),
                        "对数道矩形应为 (140,60,240,360)，实到 " + Describe(spRect));

            // 各自的最小/最大域值映射后仍落在各自区间端点上。
            Assert.True(Math.Abs(stack[0].PositionOf(RtMin) - rtRect.Left) < 1e-4,
                        "线性道下限 " + RtMin + " 应落在道左缘 " + rtRect.Left + "，实到 "
                        + stack[0].PositionOf(RtMin));
            Assert.True(Math.Abs(stack[0].PositionOf(RtMax) - rtRect.Right) < 1e-4,
                        "线性道上限 " + RtMax + " 应落在道右缘 " + rtRect.Right + "，实到 "
                        + stack[0].PositionOf(RtMax));
            Assert.True(Math.Abs(stack[1].PositionOf(SpMin) - spRect.Left) < 1e-4,
                        "对数道下限 " + SpMin + " 应落在道左缘 " + spRect.Left + "，实到 "
                        + stack[1].PositionOf(SpMin));
            Assert.True(Math.Abs(stack[1].PositionOf(SpMax) - spRect.Right) < 1e-4,
                        "对数道上限 " + SpMax + " 应落在道右缘 " + spRect.Right + "，实到 "
                        + stack[1].PositionOf(SpMax));

            // 中点在各自标度空间里：线性 200 → 像素中点 70；对数 √(1×1000) → 像素中点 190。
            Assert.True(Math.Abs(stack[0].PositionOf(200d) - (rtRect.Left + rtRect.Right) / 2f) < 1e-4,
                        "线性道中点偏了，实到 " + stack[0].PositionOf(200d));
            double logMiddle = Math.Sqrt(SpMin * SpMax);
            Assert.True(Math.Abs(stack[1].PositionOf(logMiddle) - (spRect.Left + spRect.Right) / 2f) < 1e-3,
                        "对数道几何中点 " + logMiddle + " 应落在像素中点，实到 " + stack[1].PositionOf(logMiddle));

            // 对数不变量（十年等距）只在对数道成立，线性道等差而不是等比。
            double decadeA = stack[1].PositionOf(10d) - stack[1].PositionOf(1d);
            double decadeB = stack[1].PositionOf(100d) - stack[1].PositionOf(10d);
            double decadeC = stack[1].PositionOf(1000d) - stack[1].PositionOf(100d);
            Assert.True(Math.Abs(decadeA - decadeB) < 1e-3 && Math.Abs(decadeB - decadeC) < 1e-3,
                        "对数道十年不等距：" + decadeA + " / " + decadeB + " / " + decadeC);
            Assert.True(Math.Abs((stack[0].PositionOf(100d) - stack[0].PositionOf(0d))
                                 - (stack[0].PositionOf(200d) - stack[0].PositionOf(100d))) < 1e-3,
                        "线性道应等距（每 100 一个格），实到 "
                        + (stack[0].PositionOf(200d) - stack[0].PositionOf(100d)));

            // 越界值被夹紧在自己的道内，绝不落进邻道：线性道取值扫到 ±∞ 也只到 20/120。
            foreach (double v in new[] { -1e6, -500d, 5000d, 1e6 })
            {
                float p = stack[0].PositionOf(v);
                Assert.True(p >= rtRect.Left - 1e-4 && p <= rtRect.Right + 1e-4,
                            "线性道越界值 " + v + " 跑出道外：" + p + " 不在 [" + rtRect.Left
                            + "," + rtRect.Right + "]");
            }
            foreach (double v in new[] { 1e-9, 0.001, 1e9 })
            {
                float p = stack[1].PositionOf(v);
                Assert.True(p >= spRect.Left - 1e-4 && p <= spRect.Right + 1e-4,
                            "对数道越界值 " + v + " 跑出道外：" + p + " 不在 [" + spRect.Left
                            + "," + spRect.Right + "]");
            }

            // 道类型标记不因混排被改写：线性道是 Curve，对数道是 LogCurve。
            Assert.Equal(TrackKind.Curve, stack[0].Kind);
            Assert.Equal(TrackKind.LogCurve, stack[1].Kind);
        }

        [Fact]
        public void 道栈_改一道量程不动另一道位置()
        {
            // 两栈各自新建轨道实例（同一参数、不同对象），排除"共享同一 NumericAxis"造成的假通过。
            TrackStack a = MakeStack(DepthTop, DepthBottom, Rt(), Sp());
            TrackStack b = MakeStack(DepthTop, DepthBottom, Rt(), Sp(0.2, 2000d));

            Assert.True(a.RectOf(0) == b.RectOf(0), "对数道改量程后线性道矩形动了：" + Describe(a.RectOf(0))
                                                   + " vs " + Describe(b.RectOf(0)));
            Assert.True(a.RectOf(1) == b.RectOf(1), "对数道改量程后自己的道矩形也不该动：" + Describe(a.RectOf(1))
                                                   + " vs " + Describe(b.RectOf(1)));

            // 线性道所有取样点的像素逐位不变。
            foreach (double v in RtByDepthIndex)
            {
                Assert.True(a[0].PositionOf(v) == b[0].PositionOf(v),
                            "线性道取值 " + v + " 的像素被邻道量程污染：" + a[0].PositionOf(v) + " vs "
                            + b[0].PositionOf(v));
            }
            // 对数道自己的映射确实换了（否则这条用例什么都没测）。
            Assert.True(a[1].PositionOf(1d) != b[1].PositionOf(1d),
                        "对数道量程从 [1,1000] 改成 [0.2,2000] 后 1 的像素没变，量程改动没生效："
                        + a[1].PositionOf(1d));

            // 反向：改线性道量程，对数道不动。
            TrackStack c = MakeStack(DepthTop, DepthBottom, Rt(0d, 100d), Sp());
            foreach (double v in SpByDepthIndex)
            {
                Assert.True(a[1].PositionOf(v) == c[1].PositionOf(v),
                            "对数道取值 " + v + " 的像素被邻道量程污染：" + a[1].PositionOf(v) + " vs "
                            + c[1].PositionOf(v));
            }
            Assert.True(a[0].PositionOf(200d) != c[0].PositionOf(200d),
                        "线性道量程 [0,400] 改成 [0,100] 后 200 的像素没变");

            // 共享域轴与两道量程无关：改量程不动任何深度的像素。
            foreach (double d in new[] { 0d, 125d, 400d, 733d, 1000d })
            {
                Assert.True(a.DomainPositionOf(d) == b.DomainPositionOf(d)
                            && a.DomainPositionOf(d) == c.DomainPositionOf(d),
                            "深度 " + d + " 的像素被道量程改动污染：" + a.DomainPositionOf(d) + " / "
                            + b.DomainPositionOf(d) + " / " + c.DomainPositionOf(d));
            }
        }

        // ------------------------------------------------------------ 图头量程说明与索引

        [Fact]
        public void 道栈_图头量程说明按标度生成且用不变文化()
        {
            TrackStack stack = MakeStack(DepthTop, DepthBottom, Rt(), Sp(), Lit());
            Assert.Equal("0~400", stack.Headers[0].ScaleCaption);
            Assert.Equal("1~1000（对数）", stack.Headers[1].ScaleCaption);

            // 小数端点的道：0.001~0.999，小数点必须是点而不是中文环境的逗号。
            // 值轴像素区间照旧由调用方给，但栈现在要求它落在自己道矩形内（缺陷⑤的护栏），
            // 所以这里用道自己的左缘与宽度，而不是随手写一对数。
            TrackStack withDecimals = MakeStack(DepthTop, DepthBottom,
                MakeTrack("dec", "小数道", RtLeft, TrackWidth, 0.001d, 0.999d, AxisScale.Logarithmic, TrackKind.Curve));
            Assert.Equal("0.001~0.999（对数）", withDecimals.Headers[0].ScaleCaption);

            Assert.Equal("视电阻率", stack.Headers[0].Label);
            Assert.Equal("rt", stack.Headers[0].Key);
            Assert.Equal("岩性", stack.Headers[2].Label);
            Assert.Equal(TrackKind.PatternColumn, stack[2].Kind);
        }

        [Fact]
        public void 道栈_布局按宽度与间隙累计且按键取道()
        {
            // 三道宽 100/60/40、间隙 20、Left=20 → [20,120] [140,200] [220,260]，总宽 240 = 100+60+40+2*20。
            Track a = MakeTrack("a", "甲", 20f, 100f, 0d, 100d, AxisScale.Linear, TrackKind.Curve);
            Track b = MakeTrack("b", "乙", 140f, 60f, 0d, 10d, AxisScale.Linear, TrackKind.BarColumn);
            Track c = MakeTrack("c", "丙", 220f, 40f, 1d, 100d, AxisScale.Logarithmic, TrackKind.LogCurve);
            TrackStack stack = MakeStack(DepthTop, DepthBottom, a, b, c);

            Assert.Equal(3, stack.Count);
            Assert.Equal((double)20f, (double)stack.RectOf(0).Left, 4);
            Assert.Equal((double)120f, (double)stack.RectOf(0).Right, 4);
            Assert.Equal((double)140f, (double)stack.RectOf(1).Left, 4);
            Assert.Equal((double)200f, (double)stack.RectOf(1).Right, 4);
            Assert.Equal((double)220f, (double)stack.RectOf(2).Left, 4);
            Assert.Equal((double)260f, (double)stack.Right, 4);
            Assert.Equal((double)240f, (double)stack.Width, 4);
            Assert.Equal((double)TrackGap, (double)(stack.RectOf(1).Left - stack.RectOf(0).Right), 4);
            Assert.Equal((double)40f, (double)stack.RectOf(2).Width, 4);

            // 键查找：RectOf(key) / this[key] / IndexOf 三条入口一致，缺键异常消息带键名。
            Assert.Equal(1, stack.IndexOf("b"));
            Assert.Equal("乙", stack["b"].HeaderLabel);
            Assert.True(stack.RectOf("b") == stack.RectOf(1), "按键取矩形与按下标不一致："
                        + Describe(stack.RectOf("b")) + " vs " + Describe(stack.RectOf(1)));
            KeyNotFoundException missing = Assert.Throws<KeyNotFoundException>(() => stack.RectOf("zz"));
            Assert.True(missing.Message.Contains("zz"), "异常消息该带键名，实到：" + missing.Message);
            Assert.Throws<KeyNotFoundException>(() => stack["zz"]);

            // 间隙为 0 时相邻道共边；空道集不抛（栈宽为 0，Headers 为空）。
            // 注意 b 要按"零间隙后的道位置"重建：道从 120 起，值轴也必须跟着挪，
            // 否则触发栈的值轴越界护栏（缺陷⑤）。
            TrackLayoutOptions tight = MakeOptions();
            tight.TrackGap = 0f;
            Track bTight = MakeTrack("b", "乙", 120f, 60f, 0d, 10d, AxisScale.Linear, TrackKind.BarColumn);
            TrackStack tightStack = new TrackStack("tight", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                                                   new[] { a, bTight }, tight);
            Assert.True(tightStack.RectOf(0).Right == tightStack.RectOf(1).Left,
                        "零间隙时两道应共边，实到 " + Describe(tightStack.RectOf(0)) + " / "
                        + Describe(tightStack.RectOf(1)));

            TrackStack empty = MakeStack(DepthTop, DepthBottom);
            Assert.Equal(0, empty.Count);
            Assert.Equal((double)StackLeft, (double)empty.Right, 4);
            Assert.True(empty.Headers.Count == 0, "空道集不该有图头，实到 " + empty.Headers.Count + " 条");
        }

        // ------------------------------------------------------------ 构造校验（护栏）

        [Fact]
        public void 道栈_非法参数一律拒绝()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new TrackStack("s", StackLeft, TopY, BottomY, DepthTop, DepthBottom, null!, MakeOptions()));
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("s", StackLeft, TopY, BottomY, 500d, 500d, new[] { Rt() }, MakeOptions()));
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("s", StackLeft, TopY, BottomY, 500d, 100d, new[] { Rt() }, MakeOptions()));

            // 道自己的校验齐全：空键、非正宽度、空值轴都拒。
            Assert.Throws<ArgumentException>(() => new Track("", "甲", 10f, Rt().ValueAxis, TrackKind.Curve));
            Assert.Throws<ArgumentException>(() => new Track("k", "甲", 0f, Rt().ValueAxis, TrackKind.Curve));
            Assert.Throws<ArgumentException>(() => new Track("k", "甲", -1f, Rt().ValueAxis, TrackKind.Curve));
            Assert.Throws<ArgumentNullException>(() => new Track("k", "甲", 10f, null!, TrackKind.Curve));

            // 缺陷④（已修）：栈的 key 与道键以前不校验，与 Track 的键校验不对称。
            Assert.Throws<ArgumentException>(() =>
                new TrackStack(null!, StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { Rt() }, MakeOptions()));
            Assert.Throws<ArgumentException>(() =>
                new TrackStack(string.Empty, StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { Rt() }, MakeOptions()));
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("dup", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { Rt(), Rt() }, MakeOptions()));
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("hole", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new Track[] { null! }, MakeOptions()));

            // 缺陷⑥（已修）：负间隙会让道矩形互相重叠、负图头高被当成"无图头"静默忽略。
            TrackLayoutOptions negative = MakeOptions();
            negative.TrackGap = -10f;
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("overlap", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { Rt(), Sp() }, negative));

            TrackLayoutOptions negativeHeader = MakeOptions();
            negativeHeader.HeaderHeight = -8f;
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("headless", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { Rt() }, negativeHeader));

            TrackLayoutOptions nanGap = MakeOptions();
            nanGap.TrackGap = float.NaN;
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("nan", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { Rt() }, nanGap));

            // 缺陷⑤（已修）：值轴的像素区间必须落在自己的道矩形里。
            // 以前量程写错（轴 0~300 而道只有 20~120）投点直接飞进邻道，构造阶段毫无提示。
            NumericAxis loose = new NumericAxis("rt", RtMin, RtMax, 0f, 300f, AxisDirection.HorizontalRight);
            Track escaped = new Track("rt", "视电阻率", TrackWidth, loose, TrackKind.Curve);
            Assert.Throws<ArgumentException>(() =>
                new TrackStack("escape", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                               new[] { escaped }, MakeOptions()));

            // 内缩留白仍然合法：轴区间是道矩形的子集时不报错（护栏只挡越界，不逼你把轴顶到边）。
            NumericAxis inset = new NumericAxis("rt", RtMin, RtMax,
                                                StackLeft + 4f, StackLeft + TrackWidth - 4f,
                                                AxisDirection.HorizontalRight);
            TrackStack padded = new TrackStack("inset", StackLeft, TopY, BottomY, DepthTop, DepthBottom,
                                               new[] { new Track("rt", "视电阻率", TrackWidth, inset, TrackKind.Curve) },
                                               MakeOptions());
            Assert.Equal(1, padded.Count);
        }

        // ------------------------------------------------------------ 判据⑤：像素管线

        [Fact]
        public void 道栈_像素管线_图头线与道体分隔线落墨且与几何一致()
        {
            TrackStack stack = MakeStack(DepthTop, DepthBottom, Rt(), Sp());

            // 期望位置由几何求解本身给出：2 像素笔触以坐标为中心 → 覆盖设备像素 x-1 与 x，
            // 段中心 = 坐标 - 0.5（96dpi 下逻辑像素即设备像素，无需再乘缩放）。
            double[] wantColumns =
            {
                stack.RectOf(0).Left - 0.5d, stack.RectOf(0).Right - 0.5d,
                stack.RectOf(1).Left - 0.5d, stack.RectOf(1).Right - 0.5d,
            };
            double[] wantRows =
            {
                stack.HeaderRectOf(0).Top - 0.5d, stack.HeaderRectOf(0).Bottom - 0.5d,
            };

            using (ImageProbe probe = RenderPage(stack))
            {
                Assert.True(probe.Width == 260 && probe.Height == 420,
                            "页尺寸应为 260x420，实到 " + probe.Width + "x" + probe.Height);

                IReadOnlyList<double> columns = probe.StrongVerticalLines(0d, PageWidth, TopY + 10d,
                                                                         BottomY - 10d, 0.9);
                Assert.True(columns.Count == wantColumns.Length,
                            "道体分隔线该有 4 条（两道各左右两边），实到 " + columns.Count + "：" + Join(columns));
                AssertPositions(columns, wantColumns, "道体分隔线");

                IReadOnlyList<double> rows = probe.StrongHorizontalLines(20d, TopY + 15d, RtLeft + 4d,
                                                                        RtRight - 2d, 0.95);
                Assert.True(rows.Count == wantRows.Length,
                            "图头该有上下两条横线（线性道窗口内），实到 " + rows.Count + "：" + Join(rows));
                AssertPositions(rows, wantRows, "图头线");

                // 道内确实有墨（曲线本身），否则"没有污染"只是因为什么都没画。
                Assert.True(BlackInkCount(probe, 24, 70, 116, 350) > 20,
                            "线性道体内几乎没墨，用例失去意义："
                            + BlackInkCount(probe, 24, 70, 116, 350));
                Assert.True(probe.HasRedInBox(145, 70, 235, 350), "对数道体内没有红色曲线落墨");

                // 混排不污染：红色（对数道）绝不进线性道体内，黑墨绝不进对数道体内。
                Assert.True(!probe.HasRedInBox(24, 70, 116, 350),
                            "对数道曲线越进了线性道（含越界取样值 0.001 夹紧后仍应留在 140~240）");
                Assert.True(BlackInkCount(probe, 145, 70, 235, 350) == 0,
                            "线性道曲线越进了对数道体内，黑墨 "
                            + BlackInkCount(probe, 145, 70, 235, 350) + " 个像素");

                // 道间间隙（120~140 的中段）干净：两道之间没有互相粘连的墨。
                Assert.True(!probe.HasInkInBox(126, 70, 134, 350), "道间间隙 126~133 列出现墨迹");
            }
        }

        [Fact]
        public void 道栈_像素管线_续页两页图头带设备像素逐点相同()
        {
            TrackStack whole = MakeStack(DepthTop, DepthBottom, Rt(), Sp());
            IReadOnlyList<TrackSegment> pages = whole.SplitByDomain(PageDepth);

            // 取第 0 页（深度 0~250）与第 2 页（深度 500~750）：不同页长、不同数据段。
            using (ImageProbe first = RenderPage(MakePageStack(pages[0])))
            using (ImageProbe third = RenderPage(MakePageStack(pages[2])))
            {
                double[] wantRows =
                {
                    whole.HeaderRectOf(0).Top - 0.5d, whole.HeaderRectOf(0).Bottom - 0.5d,
                };
                double[] wantColumns =
                {
                    whole.RectOf(0).Left - 0.5d, whole.RectOf(0).Right - 0.5d,
                    whole.RectOf(1).Left - 0.5d, whole.RectOf(1).Right - 0.5d,
                };

                IReadOnlyList<double> rowsFirst = HeaderLines(first);
                IReadOnlyList<double> rowsThird = HeaderLines(third);
                AssertPositions(rowsFirst, wantRows, "第 0 页图头线");
                Assert.True(SequenceEqual(rowsFirst, rowsThird),
                            "两页图头线的设备像素位置不一致：第0页 " + Join(rowsFirst)
                            + "，第2页 " + Join(rowsThird));

                IReadOnlyList<double> columnsFirst = probeColumns(first);
                IReadOnlyList<double> columnsThird = probeColumns(third);
                AssertPositions(columnsFirst, wantColumns, "第 0 页道体分隔线");
                Assert.True(SequenceEqual(columnsFirst, columnsThird),
                            "两页道体分隔线的设备像素位置不一致：第0页 " + Join(columnsFirst)
                            + "，第2页 " + Join(columnsThird));

                // 更强：整个图头带（行 0..57，避开图头下边线所在的 59/60 行与曲线首点）
                // 两页必须逐像素相同——文字、图头框线、道头位置一个都不能差。
                string diff = HeaderBandDiff(first, third, (int)TopY - 2);
                Assert.True(diff.Length == 0, "第 0 页与第 2 页图头带像素不同：" + diff);

                // 反向对照：图头带相同不代表整页相同（续页画的是不同深度段的数据）。
                Assert.True(BodyDiffers(first, third, 145, (int)TopY + 20, 235, (int)BottomY - 20),
                            "两页对数道体内的曲线一模一样，说明数据没随深度段推进");
            }
        }

        // ------------------------------------------------------------ 支撑：版面构造

        private static TrackLayoutOptions MakeOptions()
        {
            TrackLayoutOptions options = new TrackLayoutOptions();
            options.TrackGap = TrackGap;
            options.HeaderHeight = HeaderHeight;
            options.SeparatorWidth = SeparatorStroke;
            options.DrawSeparators = true;
            return options;
        }

        private static TrackStack MakeStack(double domainMin, double domainMax, params Track[] tracks)
        {
            return new TrackStack("well-log", StackLeft, TopY, BottomY, domainMin, domainMax,
                                  tracks, MakeOptions());
        }

        /// <summary>把一段续页区间做成一页的栈：像素版面不变，只换共享域轴的取值区间。</summary>
        private static TrackStack MakePageStack(TrackSegment page)
        {
            return MakeStack(page.DomainStart, page.DomainEnd, Rt(), Sp());
        }

        private static Track Rt(double min = RtMin, double max = RtMax)
        {
            return MakeTrack("rt", "视电阻率", RtLeft, TrackWidth, min, max, AxisScale.Linear, TrackKind.Curve);
        }

        private static Track Sp(double min = SpMin, double max = SpMax)
        {
            return MakeTrack("sp", "自然电位", SpLeft, TrackWidth, min, max, AxisScale.Logarithmic,
                             TrackKind.LogCurve);
        }

        private static Track Lit()
        {
            return MakeTrack("lit", "岩性", SpRight + TrackGap, TrackWidth, 0d, 1d, AxisScale.Linear,
                             TrackKind.PatternColumn);
        }

        /// <summary>
        /// 造一道：值轴的像素区间按"道左缘 → 道右缘"公开定义预先设好（源码要求调用方预设）。
        /// </summary>
        private static Track MakeTrack(string key, string label, float left, float width,
                                       double min, double max, AxisScale scale, TrackKind kind)
        {
            NumericAxis axis = new NumericAxis(key, min, max, left, left + width,
                                               AxisDirection.HorizontalRight, scale);
            return new Track(key, label, width, axis, kind);
        }

        private static ImageProbe RenderPage(TrackStack stack)
        {
            using (Chart chart = new Chart(new ChartDocument(),
                                          new TrackStackPageTemplate(stack),
                                          new ChartStyle()))
            {
                return ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100));
            }
        }

        // ------------------------------------------------------------ 支撑：像素读数

        private static IReadOnlyList<double> HeaderLines(ImageProbe probe)
        {
            return probe.StrongHorizontalLines(20d, TopY + 15d, RtLeft + 4d, RtRight - 2d, 0.95);
        }

        private static IReadOnlyList<double> probeColumns(ImageProbe probe)
        {
            return probe.StrongVerticalLines(0d, PageWidth, TopY + 10d, BottomY - 10d, 0.9);
        }

        /// <summary>
        /// 窗口内"黑墨"像素数：三通道都低于 100 才算。
        /// 红色 (255,0,0) 抗锯齿后是 (255,g,g)，红通道始终高位，不会被误计成黑墨。
        /// </summary>
        private static int BlackInkCount(ImageProbe probe, int left, int top, int right, int bottom)
        {
            int count = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (!probe.IsInk(x, y))
                    {
                        continue;
                    }
                    SKColor color = probe.Pixel(x, y);
                    if (color.Red < 100 && color.Green < 100 && color.Blue < 100)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        /// <summary>图头带（第 0..bottom-1 行、整页宽）逐像素比对；无差异返回空串。</summary>
        private static string HeaderBandDiff(ImageProbe a, ImageProbe b, int bottom)
        {
            List<string> diffs = new List<string>();
            for (int y = 0; y < bottom; y++)
            {
                for (int x = 0; x < Math.Min(a.Width, b.Width); x++)
                {
                    if (a.IsInk(x, y) != b.IsInk(x, y) && diffs.Count < 12)
                    {
                        diffs.Add("(" + x + "," + y + ")=" + a.IsInk(x, y) + "/" + b.IsInk(x, y));
                    }
                }
            }
            return diffs.Count == 0 ? string.Empty : string.Join(" ", diffs.ToArray());
        }

        /// <summary>窗口内是否存在"一侧有墨、另一侧全白"的差集（续页数据真的换了）。</summary>
        private static bool BodyDiffers(ImageProbe a, ImageProbe b, int left, int top, int right, int bottom)
        {
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (a.IsInk(x, y) != b.IsInk(x, y))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool SequenceEqual(IReadOnlyList<double> a, IReadOnlyList<double> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 逐条比对实测线位与几何复算值。容差 0.6 像素：整数坐标 + 偶数线粗时理论上是 0 偏差，
        /// 留半格是因为笔触边界落在像素边界上时，抗锯齿给出的两半覆盖档位在 247 阈值两侧。
        /// </summary>
        private static void AssertPositions(IReadOnlyList<double> actual, IReadOnlyList<double> expected,
                                            string label)
        {
            Assert.True(actual.Count == expected.Count,
                        label + "：条数 " + actual.Count + "，期望 " + expected.Count + "：" + Join(actual));
            for (int i = 0; i < expected.Count; i++)
            {
                double delta = Math.Abs(actual[i] - expected[i]);
                Assert.True(delta <= 0.6, label + "：第 " + i + " 条实测 " + actual[i] + "，几何复算 "
                                + expected[i] + "（偏差 " + delta.ToString("0.###") + " 设备像素）");
            }
        }

        private static string Join(IReadOnlyList<double> values)
        {
            List<string> texts = new List<string>();
            foreach (double v in values)
            {
                texts.Add(v.ToString("0.##"));
            }
            return "[" + string.Join(", ", texts.ToArray()) + "]";
        }

        private static string Describe(SKRect rect)
        {
            return "(" + rect.Left + "," + rect.Top + "," + rect.Right + "," + rect.Bottom + ")";
        }

        // ------------------------------------------------------------ 走管线用的小模板

        /// <summary>
        /// 最小模板：把一个 <see cref="TrackStack"/> 的图头线与道体分隔线、
        /// 以及两道各自的曲线接进 Chart 光栅管线。坐标全部取自栈的求解结果，模板内不写字面量坐标。
        /// </summary>
        private sealed class TrackStackPageTemplate : IChartTemplate
        {
            private readonly TrackStack _stack;

            public TrackStackPageTemplate(TrackStack stack)
            {
                _stack = stack;
            }

            public string Key
            {
                get { return "test.track-stack-page"; }
            }

            public string DisplayName
            {
                get { return "多轨堆叠试版"; }
            }

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                return new ChartGeometry(
                    new SKRect(0f, 0f, PageWidth, PageHeight),
                    new SlotAxis(StackLeft, TrackWidth, 1, 1),
                    // 域轴 downward=false：与 TrackStack 的 VerticalDown 口径一致（值向下增大）。
                    new LinearAxis("depth", DepthTop, DepthBottom, TopY, BottomY, 10d, 100d, false),
                    new LinearAxis[0],
                    new Band("header", new SKRect(0f, TopY - HeaderHeight, PageWidth, TopY), new BandRow[0]),
                    new Band("date", new SKRect(0f, TopY, PageWidth, TopY + 1f), new BandRow[0]),
                    new SKRect(StackLeft, TopY, SpRight, BottomY),
                    new LabelColumn[0],
                    new Band("cells", new SKRect(0f, BottomY, PageWidth, BottomY + 20f), new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, PageHeight - 20f, PageWidth, PageHeight), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                SKColor grid = painter.Style.Colors.Grid;
                float width = _stack.Options.SeparatorWidth;

                for (int i = 0; i < _stack.Count; i++)
                {
                    // 道体分隔线：栈求解的矩形左右两边各一条竖线。
                    SKRect body = _stack.RectOf(i);
                    painter.Line(body.Left, body.Top, body.Left, body.Bottom, grid, width);
                    painter.Line(body.Right, body.Top, body.Right, body.Bottom, grid, width);

                    // 图头：上下两条横线 + 标题与量程说明。
                    SKRect head = _stack.HeaderRectOf(i);
                    painter.Line(head.Left, head.Top, head.Right, head.Top, grid, width);
                    painter.Line(head.Left, head.Bottom, head.Right, head.Bottom, grid, width);
                    painter.TextInBox(_stack.Headers[i].Label + " " + _stack.Headers[i].ScaleCaption, head,
                                      new FontKey(null, 9f), painter.Style.Colors.Text,
                                      CellAlign.Center, CellAlign.Center, 1f);
                }

                // 线性道黑色、对数道红色：颜色只用来把"谁越了界"读出来。
                DrawCurve(painter, 0, RtByDepthIndex, grid);
                DrawCurve(painter, 1, SpByDepthIndex, SKColors.Red);
            }

            /// <summary>
            /// 按绝对深度每 50 米（<c>SampleStep</c>）取一点画折线：
            /// 取值由深度查表，所以同一份表在任何一页上都成立——续页之间曲线的形状与位置都跟着深度走，
            /// 而页顶/页底的 y 恒为 TopY/BottomY（栈按本页自己的域区间归一）。
            /// </summary>
            private void DrawCurve(Painter painter, int track, double[] valueByDepthIndex, SKColor color)
            {
                List<SKPoint> points = new List<SKPoint>();
                double span = _stack.DomainMax - _stack.DomainMin;
                int steps = (int)Math.Round(span / SampleStep);
                if (steps < 1)
                {
                    steps = 1;
                }
                for (int i = 0; i <= steps; i++)
                {
                    double depth = _stack.DomainMin + span * i / steps;
                    int index = (int)Math.Round(depth / SampleStep);
                    if (index < 0)
                    {
                        index = 0;
                    }
                    else if (index > valueByDepthIndex.Length - 1)
                    {
                        index = valueByDepthIndex.Length - 1;
                    }
                    points.Add(_stack.PointOf(track, depth, valueByDepthIndex[index]));
                }
                painter.Polyline(points, color, CurveStroke, false);
            }
        }
    }
}
