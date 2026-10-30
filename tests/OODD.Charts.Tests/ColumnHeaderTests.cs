using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
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
    /// T220「深层分组表头」验收测试（docs/07 行 T220，设计口径 docs/06 §3.8）。
    /// <para>
    /// 被测面只有 <c>Geometry/ColumnHeaderNode.cs</c> 一个文件里的公开类型：
    /// <list type="bullet">
    /// <item><description><see cref="ColumnHeaderNode"/>：任意深度表头树（纯结构，不碰像素）；</description></item>
    /// <item><description><see cref="HeaderTableLayout"/>：把树落到像素矩形上（源码提供的唯一求矩形口径）；</description></item>
    /// <item><description><see cref="DiagonalDirection"/> 与 <see cref="ColumnHeaderNode.DiagonalEnds"/>：斜线格的几何；</description></item>
    /// <item><description><see cref="SlotAxis"/>：回归（本轮不得改动其存量行为）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// T220 原条目里的两处交付缺口已在补库轮关闭（docs/07 "波次 3 回报的 Core 缺陷处置"之后）：
    /// ① <c>DiagonalCellMark</c> 独立类型没做，改为 <see cref="ColumnHeaderNode.Diagonal"/> 标志 +
    /// 库内消费者 <c>Render/HeaderRenderer</c>——"斜线格内不画文字"从调用侧约定变成库的行为，
    /// 本文件的探针模板因此不再自己判断；
    /// ② 列边界数学抽进 <c>Geometry/CellGeometry</c>（权重列 + 等宽列两种入口，<see cref="HeaderTableLayout"/>
    /// 与合并格共用一条口径）。docs/07 原文写的路径是 <c>Render/CellGeometry.cs</c>，实际落在
    /// <c>Geometry/</c>：它被 <see cref="HeaderTableLayout"/> 调用，放 Render 会让几何层反向依赖绘制层。
    /// </para>
    /// </summary>
    public sealed class ColumnHeaderTests
    {
        /// <summary>
        /// 矩形边界比较容差 = 1e-3 逻辑像素。
        /// 理由：<c>HeaderTableLayout</c> 的列分界是 <c>area.Left + available × (累加权重/总权重)</c> 用 double
        /// 算完再单次转 float（ColumnHeaderNode.cs:300-315），float32 在 600 量级上的分辨率约 6e-5，
        /// 一次减法最多带 2 ulp（约 1.2e-4）；而"错一列"的量级是 34 像素。1e-3 比浮点噪声高一个数量级、
        /// 比真实错配低四个数量级，两头都不会误判。
        /// </summary>
        private const float RectEps = 1e-3f;

        // ------------------------------------------------------ 出图探针的版面常量（逻辑像素，96dpi）

        private const float ProbeLeft = 20f;
        private const float ProbeTop = 20f;
        private const int ProbeLeafCount = 8;
        private const float ProbeLeafWidth = 60f;
        private const float ProbeLevelHeight = 30f;
        private const int ProbeLevelCount = 4;
        private const float ProbeAreaRight = ProbeLeft + ProbeLeafCount * ProbeLeafWidth;
        private const float ProbeAreaBottom = ProbeTop + ProbeLevelCount * ProbeLevelHeight;
        private const float ProbeDpi = 96f;

        /// <summary>斜线墨的垂距豁免带（逻辑像素）：1px 线粗居中落墨 + 抗锯齿边，实测不超过约 1.2px。</summary>
        private const double DiagonalBand = 2.5;

        /// <summary>取格内墨时离边框的设备像素余量，避开格线自身的墨。</summary>
        private const int CellMarginPx = 4;

        // ============================================================================ LeafSpan 守恒

        [Fact]
        public void 四层表头逐层LeafSpan之和等于叶子列总数()
        {
            ColumnHeaderNode root = UniformTree();
            int total = root.Leaves().Count;
            Assert.Equal(18, total);
            Assert.Equal(18, root.LeafSpan);
            Assert.Equal(3, root.Depth);
            Assert.Equal(3, root.MaxLevel);

            // 逐层求和，不是只看根：level=0 是根自己，level=Depth 是全部叶子。
            for (int level = 0; level <= root.Depth; level++)
            {
                IReadOnlyList<ColumnHeaderNode> nodes = root.NodesAtLevel(level);
                int sum = SumLeafSpan(nodes);
                Assert.True(sum == total,
                            "第 " + level + " 层 LeafSpan 之和 " + sum + "，叶子列总数 " + total
                            + "（该层节点数 " + nodes.Count + "）");
                Assert.Equal(ExpectedNodeCountAtLevel(level), nodes.Count);
            }

            // 越界层按源码返回空列表而不抛（ColumnHeaderNode.cs:194-214 的注释口径）。
            Assert.True(root.NodesAtLevel(4).Count == 0, "越界层应返回空列表，实到 " + root.NodesAtLevel(4).Count + " 个");
            Assert.True(root.NodesAtLevel(99).Count == 0, "越界层应返回空列表，实到 " + root.NodesAtLevel(99).Count + " 个");
        }

        [Fact]
        public void 每个父节点的LeafSpan等于其子节点之和()
        {
            ColumnHeaderNode root = UniformTree();
            AssertSpanConservation(root);

            // 深度优先展开全部叶子：叶子数 == 根 LeafSpan，且顺序即列序（每个节点拿到的都是连续列段）。
            IReadOnlyList<ColumnHeaderNode> leaves = root.Leaves();
            Assert.Equal(root.LeafSpan, leaves.Count);
            for (int i = 0; i < leaves.Count; i++)
            {
                Assert.True(leaves[i].IsLeaf, "Leaves() 第 " + i + " 项不是叶子：" + leaves[i].Key);
                Assert.Equal(1, leaves[i].LeafSpan);
                Assert.Equal(0, leaves[i].Depth);
            }
        }

        [Fact]
        public void 非均匀深度树的逐层求和按实测降级()
        {
            // 现状记录（不是期望）：NodesAtLevel 的 LeafSpan 求和只在"树均匀到底"时等于叶子列总数。
            // 一旦某条分支提前收成叶子，它在更深的层里就不再出现，该层之和随之变小——
            // 护理记录单正是这种非均匀树（心率一层、实入量三层），模板若按"逐层求和==总列数"
            // 自证版面守恒，必须先用 NodesAtLevel 的层数上限，而不是根的 Depth。
            ColumnHeaderNode root = RaggedTree();
            int total = root.Leaves().Count;
            Assert.Equal(6, total);
            Assert.Equal(6, root.LeafSpan);
            Assert.Equal(3, root.Depth);

            int[] expected = { 6, 6, 5, 2 };
            for (int level = 0; level <= root.Depth; level++)
            {
                int sum = SumLeafSpan(root.NodesAtLevel(level));
                Assert.True(sum == expected[level],
                            "非均匀树第 " + level + " 层实测 LeafSpan 之和 " + sum
                            + "（登记值 " + expected[level] + "，叶子列总数 " + total + "）："
                            + "提前收叶的分支不再出现在更深层，故逐层求和自 level=2 起小于总列数。这是源码现状。");
            }
        }

        // ============================================================================ 求矩形（深度 ≥3）

        [Fact]
        public void 四层表头求矩形同层互不重叠且铺满区域()
        {
            ColumnHeaderNode root = UniformTree();
            SKRect area = new SKRect(6f, 10f, 618f, 130f);      // 18 叶 × 34px、4 层 × 30px
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);

            Assert.Equal(4, layout.LevelCount);
            Assert.Equal(18, layout.LeafCount);
            Assert.Equal(120f, layout.RequiredHeight);
            Assert.True(layout.Area.Left == area.Left && layout.Area.Top == area.Top
                        && layout.Area.Right == area.Right && layout.Area.Bottom == area.Bottom,
                        "Area 应原样回传，实测 " + layout.Area);
            Assert.False(layout.LeavesToBottom, "默认不沉底（ColumnHeaderNode.cs:261 的 leavesToBottom 默认 false）");

            for (int level = 0; level <= root.Depth; level++)
            {
                SKRect row = layout.LevelRect(level);
                IReadOnlyList<ColumnHeaderNode> nodes = root.NodesAtLevel(level);

                for (int i = 0; i < nodes.Count; i++)
                {
                    SKRect rect = layout.RectOf(nodes[i]);
                    Assert.True(Math.Abs(rect.Top - row.Top) <= RectEps && Math.Abs(rect.Bottom - row.Bottom) <= RectEps,
                                "第 " + level + " 层节点 " + nodes[i].Key + " 的行高与 LevelRect 不符：["
                                + rect.Top + "," + rect.Bottom + "] vs [" + row.Top + "," + row.Bottom + "]");
                    Assert.True(rect.Left >= area.Left - RectEps && rect.Right <= area.Right + RectEps,
                                "节点 " + nodes[i].Key + " 越出表头区域：" + rect.Left + ".." + rect.Right);

                    if (i > 0)
                    {
                        SKRect prev = layout.RectOf(nodes[i - 1]);
                        Assert.True(rect.Left >= prev.Right - RectEps,
                                    "同层重叠：节点 " + nodes[i].Key + " 左缘 " + rect.Left + " 小于前节点 "
                                    + nodes[i - 1].Key + " 右缘 " + prev.Right);
                        Assert.True(Math.Abs(rect.Left - prev.Right) <= RectEps,
                                    "同层应当共边无缝：前节点右缘 " + prev.Right + "，本节点左缘 " + rect.Left);
                    }
                }

                Assert.True(Math.Abs(layout.RectOf(nodes[0]).Left - area.Left) <= RectEps, "该层首格应从区域左缘起");
                Assert.True(Math.Abs(layout.RectOf(nodes[nodes.Count - 1]).Right - area.Right) <= RectEps,
                            "该层末格应铺到区域右缘，实测 " + layout.RectOf(nodes[nodes.Count - 1]).Right);
            }
        }

        [Fact]
        public void 四层表头求矩形任意两格不重叠()
        {
            ColumnHeaderNode root = UniformTree();
            SKRect area = new SKRect(6f, 10f, 618f, 130f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);

            List<ColumnHeaderNode> all = new List<ColumnHeaderNode>();
            CollectAll(root, all);
            Assert.Equal(28, all.Count);        // 1 根 + 3 组 + 6 子组 + 18 叶

            for (int i = 0; i < all.Count; i++)
            {
                for (int j = i + 1; j < all.Count; j++)
                {
                    SKRect a = layout.RectOf(all[i]);
                    SKRect b = layout.RectOf(all[j]);
                    bool xOverlap = a.Left < b.Right - RectEps && b.Left < a.Right - RectEps;
                    bool yOverlap = a.Top < b.Bottom - RectEps && b.Top < a.Bottom - RectEps;
                    Assert.False(xOverlap && yOverlap,
                                 "两格重叠：" + all[i].Key + " [" + a.Left + "," + a.Top + "," + a.Right + "," + a.Bottom
                                 + "] 与 " + all[j].Key + " [" + b.Left + "," + b.Top + "," + b.Right + "," + b.Bottom + "]");
                }
            }
        }

        [Fact]
        public void 父矩形等于其子矩形的并()
        {
            // "并"按源码口径逐项核对（左右上下边界分别比）：父格自己占一行，纵向不可能与子格矩形重合，
            // 所以成立的是三条——水平边界等于子格边界的最小/最大值、父格下缘等于子格上缘的最小值
            // （紧邻其上）、父格宽度等于子格宽度之和（同层无缝共边 ⇒ 并集无缝）。
            ColumnHeaderNode root = UniformTree();
            SKRect area = new SKRect(6f, 10f, 618f, 130f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);

            List<ColumnHeaderNode> all = new List<ColumnHeaderNode>();
            CollectAll(root, all);
            int checkedParents = 0;
            for (int n = 0; n < all.Count; n++)
            {
                ColumnHeaderNode node = all[n];
                if (node.IsLeaf) continue;
                checkedParents++;

                SKRect parent = layout.RectOf(node);
                float minLeft = float.MaxValue, maxRight = float.MinValue;
                float minChildTop = float.MaxValue;
                float childWidthSum = 0f;
                for (int i = 0; i < node.Children.Count; i++)
                {
                    SKRect child = layout.RectOf(node.Children[i]);
                    if (child.Left < minLeft) minLeft = child.Left;
                    if (child.Right > maxRight) maxRight = child.Right;
                    if (child.Top < minChildTop) minChildTop = child.Top;
                    childWidthSum += child.Width;
                }

                Assert.True(Math.Abs(parent.Left - minLeft) <= RectEps,
                            node.Key + " 父格左缘 " + parent.Left + " != 子格并集左缘 " + minLeft);
                Assert.True(Math.Abs(parent.Right - maxRight) <= RectEps,
                            node.Key + " 父格右缘 " + parent.Right + " != 子格并集右缘 " + maxRight);
                Assert.True(Math.Abs(parent.Bottom - minChildTop) <= RectEps,
                            node.Key + " 父格下缘 " + parent.Bottom + " != 子格上缘最小值 " + minChildTop
                            + "（父行应紧邻子行之上）");
                Assert.True(Math.Abs(parent.Top - (minChildTop - layout.LevelHeight)) <= RectEps,
                            node.Key + " 父格上缘应比子行上缘高一个行高，实测 " + parent.Top);
                Assert.True(Math.Abs(parent.Width - childWidthSum) <= 1e-2f,
                            node.Key + " 父格宽 " + parent.Width + " != 子格宽之和 " + childWidthSum);
            }
            Assert.Equal(10, checkedParents);
        }

        [Fact]
        public void 中间层矩形跨度等于其叶子跨度()
        {
            ColumnHeaderNode root = UniformTree();
            SKRect area = new SKRect(6f, 10f, 618f, 130f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);
            IReadOnlyList<ColumnHeaderNode> allLeaves = root.Leaves();
            IReadOnlyList<float> bounds = layout.LeafBounds;

            List<ColumnHeaderNode> all = new List<ColumnHeaderNode>();
            CollectAll(root, all);
            for (int n = 0; n < all.Count; n++)
            {
                ColumnHeaderNode node = all[n];
                IReadOnlyList<ColumnHeaderNode> own = node.Leaves();
                int first = IndexByReference(allLeaves, own[0]);
                int last = IndexByReference(allLeaves, own[own.Count - 1]);

                Assert.True(last - first + 1 == node.LeafSpan,
                            node.Key + " 的叶子在列序上不连续：首 " + first + " 末 " + last
                            + "，LeafSpan " + node.LeafSpan);

                SKRect rect = layout.RectOf(node);
                Assert.True(Math.Abs(rect.Left - bounds[first]) <= RectEps,
                            node.Key + " 左缘 " + rect.Left + " != LeafBounds[" + first + "]=" + bounds[first]);
                Assert.True(Math.Abs(rect.Right - bounds[last + 1]) <= RectEps,
                            node.Key + " 右缘 " + rect.Right + " != LeafBounds[" + (last + 1) + "]=" + bounds[last + 1]);

                // 等权重时跨度还等于 LeafSpan × 单列宽（612/18 = 34）。
                double expected = node.LeafSpan * ((double)area.Width / allLeaves.Count);
                Assert.True(Math.Abs((double)rect.Width - expected) <= 1e-2,
                            node.Key + " 宽 " + rect.Width + "，期望 LeafSpan(" + node.LeafSpan + ")×列宽 = " + expected);

                // 中间层（非根非叶）尤其要满足"矩形跨度 == 叶子跨度 == 子格跨度的并"。
                if (!node.IsLeaf && !ReferenceEquals(node, root))
                {
                    Assert.Equal(node.LeafSpan, SumLeafSpan(node.Children));
                    SKRect childFirst = layout.RectOf(node.Children[0]);
                    SKRect childLast = layout.RectOf(node.Children[node.Children.Count - 1]);
                    Assert.True(Math.Abs(rect.Left - childFirst.Left) <= RectEps,
                                "中间层节点 " + node.Key + " 的左缘不等于首个子格左缘");
                    Assert.True(Math.Abs(rect.Right - childLast.Right) <= RectEps,
                                "中间层节点 " + node.Key + " 的右缘不等于末个子格右缘");
                }

                if (node.IsLeaf)
                {
                    Assert.Equal(first, layout.LeafIndexOf(node));
                    SKRect column = layout.ColumnRect(first, rect.Top, rect.Bottom);
                    Assert.True(Math.Abs(column.Left - rect.Left) <= RectEps
                                && Math.Abs(column.Right - rect.Right) <= RectEps,
                                "叶子 " + node.Key + " 的格与列网格不一致：" + column + " vs " + rect);
                }
            }
        }

        [Fact]
        public void 叶子沉底模式把浅层叶子压到末行并留下行号错配()
        {
            ColumnHeaderNode root = RaggedTree();
            SKRect area = new SKRect(0f, 0f, 240f, 120f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f, true);

            Assert.Equal(4, layout.LevelCount);
            Assert.True(layout.LeavesToBottom);

            ColumnHeaderNode? shallow = root.Find("b");
            Assert.NotNull(shallow);
            Assert.True(shallow!.IsLeaf);

            SKRect rect = layout.RectOf(shallow);
            SKRect bottomRow = layout.LevelRect(layout.LevelCount - 1);
            Assert.True(Math.Abs(rect.Top - bottomRow.Top) <= RectEps && Math.Abs(rect.Bottom - bottomRow.Bottom) <= RectEps,
                        "沉底模式下浅叶 " + shallow.Key + " 应落到末行 [" + bottomRow.Top + "," + bottomRow.Bottom
                        + "]，实测 [" + rect.Top + "," + rect.Bottom + "]");

            // 现状记录：沉底后"节点的层号"与"它实际占据的行号"不再相等——
            // b 在 NodesAtLevel(1) 里，矩形却画在第 3 行。按 NodesAtLevel 排行高的绘制层会把字写错行，
            // 拿矩形只能走 RectOf。源码没有提供"行号"公开查询，故此处只登记错配事实。
            IReadOnlyList<ColumnHeaderNode> levelOne = root.NodesAtLevel(1);
            Assert.Equal(3, levelOne.Count);
            Assert.True(ReferenceEquals(levelOne[1], shallow), "b 仍是第 1 层节点（NodesAtLevel 按树深而非行深）");
            Assert.True(Math.Abs(rect.Top - layout.LevelRect(1).Top) > 1f,
                        "实测行位置与层号不符（现状，非期望）");

            // 末行只放叶子：三组叶子共 6 列无缝铺满，且不越出区域。
            IReadOnlyList<ColumnHeaderNode> leaves = root.Leaves();
            Assert.Equal(6, leaves.Count);
            for (int i = 0; i < leaves.Count; i++)
            {
                SKRect leaf = layout.RectOf(leaves[i]);
                Assert.True(Math.Abs(leaf.Top - bottomRow.Top) <= RectEps,
                            "沉底后叶子 " + leaves[i].Key + " 不在末行，实测 top=" + leaf.Top);
                if (i > 0)
                {
                    Assert.True(Math.Abs(leaf.Left - layout.RectOf(leaves[i - 1]).Right) <= RectEps,
                                "末行列网格断裂于 " + leaves[i].Key);
                }
            }
            Assert.True(Math.Abs(layout.RectOf(leaves[leaves.Count - 1]).Right - area.Right) <= RectEps);
        }

        [Fact]
        public void 叶子列边界数组可直接与数据行共用列网格()
        {
            ColumnHeaderNode root = UniformTree();
            SKRect area = new SKRect(6f, 10f, 618f, 130f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);
            IReadOnlyList<float> bounds = layout.LeafBounds;

            Assert.Equal(layout.LeafCount + 1, bounds.Count);
            Assert.Equal(area.Left, bounds[0]);
            Assert.True(Math.Abs(bounds[bounds.Count - 1] - area.Right) <= RectEps,
                        "末边界按源码直接取区域右缘（ColumnHeaderNode.cs:307-310，防累加漂移），实测 "
                        + bounds[bounds.Count - 1]);
            for (int i = 0; i < layout.LeafCount; i++)
            {
                Assert.True(bounds[i + 1] >= bounds[i], "列边界必须单调不减，第 " + i + " 处违例");
                Assert.True(Math.Abs((bounds[i + 1] - bounds[i]) - 34f) <= 1e-3f,
                            "等权重下第 " + i + " 列宽应为 612/18=34，实测 " + (bounds[i + 1] - bounds[i]));
            }
        }

        [Fact]
        public void 不等权重按权重分列宽且末列右缘取区域右缘()
        {
            ColumnHeaderNode a = new ColumnHeaderNode("a", "甲");
            ColumnHeaderNode b = new ColumnHeaderNode("b", "乙");
            ColumnHeaderNode c = new ColumnHeaderNode("c", "丙");
            ColumnHeaderNode d = new ColumnHeaderNode("d", "丁");
            a.LeafWeight = 1d;
            b.LeafWeight = 2d;
            c.LeafWeight = 1d;
            d.LeafWeight = 1d;
            ColumnHeaderNode root = new ColumnHeaderNode("hdr", "表头", new[] { a, b, c, d });

            SKRect area = new SKRect(0f, 0f, 100f, 30f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);

            // 权重和 5，可用宽 100 ⇒ 分界 0, 20, 60, 80, 100（最后一条取区域右缘）。
            float[] expected = { 0f, 20f, 60f, 80f, 100f };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(Math.Abs(layout.LeafBounds[i] - expected[i]) <= 1e-4f,
                            "第 " + i + " 条列界 " + layout.LeafBounds[i] + "，期望 " + expected[i]);
            }

            SKRect rectB = layout.RectOf(b);
            Assert.True(Math.Abs(rectB.Left - 20f) <= 1e-4f && Math.Abs(rectB.Right - 60f) <= 1e-4f,
                        "2 权重列 " + b.Key + " 应占 20px，实测 " + rectB.Width);
            Assert.Equal(4, root.LeafSpan);        // 权重不影响 LeafSpan
            Assert.Equal(1, root.Depth);           // 也不影响树深

            // 非叶子节点上的 LeafWeight 不参与求解（源码注释 ColumnHeaderNode.cs:138-139）。
            ColumnHeaderNode deep = UniformTree();
            SKRect wideArea = new SKRect(0f, 0f, 612f, 120f);
            HeaderTableLayout baseline = new HeaderTableLayout(deep, wideArea, 30f);
            deep.LeafWeight = 9d;                  // 挂在根（非叶子）上
            HeaderTableLayout after = new HeaderTableLayout(deep, wideArea, 30f);
            for (int level = 0; level <= deep.Depth; level++)
            {
                IReadOnlyList<ColumnHeaderNode> nodes = deep.NodesAtLevel(level);
                for (int i = 0; i < nodes.Count; i++)
                {
                    Assert.True(RectEquals(baseline.RectOf(nodes[i]), after.RectOf(nodes[i]), RectEps),
                                "非叶子 LeafWeight 竟参与了列宽求解：" + nodes[i].Key);
                }
            }
        }

        // ============================================================================ CellGeometry（合并格共用的列边界数学）

        [Fact]
        public void CellGeometry_列边界与合并格矩形按解析值给出并守住末缘直取右缘()
        {
            float[] uniform = CellGeometry.UniformBounds(10f, 130f, 4);
            Assert.Equal(5, uniform.Length);
            Assert.Equal(10f, uniform[0]);
            Assert.Equal(40f, uniform[1]);
            Assert.Equal(70f, uniform[2]);
            Assert.Equal(100f, uniform[3]);
            Assert.Equal(130f, uniform[4]);

            // 权重列：[0,120] 按 1:2:1 分 → 30 / 90 / 120（解析值：120×¼、120×¾，不是实测回填）。
            float[] weighted = CellGeometry.ColumnBounds(0f, 120f, new double[] { 1d, 2d, 1d });
            Assert.Equal(30f, weighted[1], 4);
            Assert.Equal(90f, weighted[2], 4);
            Assert.Equal(120f, weighted[3]);

            // 末缘必须<b>恰好</b>等于区域右缘：0.1 这类宽度逐列累加会把误差传到末列。
            float[] thirds = CellGeometry.ColumnBounds(0f, 0.3f, new double[] { 1d, 1d, 1d });
            Assert.Equal(0.3f, thirds[3]);

            // 合并格与单列格同一个入口：span=1 就是单列。
            SKRect merged = CellGeometry.RectOf(weighted, 0, 2, 5f, 15f);
            Assert.True(merged.Left == 0f && merged.Right == 90f && merged.Top == 5f && merged.Bottom == 15f,
                        "跨 0~1 两列的合并格应为 [0,90]×[5,15]，实到 " + merged);
            SKRect single = CellGeometry.RectOf(weighted, 2, 1, 5f, 15f);
            Assert.True(single.Left == 90f && single.Right == 120f, "第 3 列应为 [90,120]，实到 " + single);

            // 抽取前后必须同一条口径：等权表头树的 LeafBounds 与 UniformBounds 逐元素相同。
            ColumnHeaderNode root = UniformTree();
            SKRect area = new SKRect(0f, 0f, 612f, 120f);
            HeaderTableLayout layout = new HeaderTableLayout(root, area, 30f);
            float[] reference = CellGeometry.UniformBounds(0f, 612f, layout.LeafCount);
            Assert.Equal(reference.Length, layout.LeafBounds.Count);
            for (int i = 0; i < reference.Length; i++)
            {
                Assert.True(reference[i] == layout.LeafBounds[i],
                            "第 " + i + " 条列界与 CellGeometry 口径不一致：" + layout.LeafBounds[i]
                            + " vs " + reference[i]);
            }

            // 入参护栏。
            Assert.Throws<ArgumentNullException>(() => CellGeometry.ColumnBounds(0f, 100f, null!));
            Assert.Throws<ArgumentException>(() => CellGeometry.ColumnBounds(0f, 100f, new double[0]));
            Assert.Throws<ArgumentException>(() =>
                CellGeometry.ColumnBounds(0f, 100f, new double[] { 1d, -0.5d }));
            Assert.Throws<ArgumentException>(() =>
                CellGeometry.ColumnBounds(0f, 100f, new double[] { double.NaN, 1d }));
            Assert.Throws<ArgumentException>(() =>
                CellGeometry.ColumnBounds(0f, 100f, new double[] { double.PositiveInfinity }));
            Assert.Throws<ArgumentException>(() => CellGeometry.ColumnBounds(0f, 100f, new double[] { 0d, 0d }));
            Assert.Throws<ArgumentException>(() => CellGeometry.ColumnBounds(100f, 100f, new double[] { 1d }));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellGeometry.UniformBounds(0f, 100f, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellGeometry.RectOf(weighted, 2, 2, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellGeometry.RectOf(weighted, -1, 1, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => CellGeometry.RectOf(weighted, 0, 0, 0f, 1f));
            Assert.Throws<ArgumentNullException>(() => CellGeometry.RectOf(null!, 0, 1, 0f, 1f));
        }

        // ============================================================================ 斜线格（真实出图管线）

        [Fact]
        public void 斜线格走真实管线格内有斜线墨但格内不落字()
        {
            DiagonalHeaderTemplate semantic = new DiagonalHeaderTemplate(false);
            DiagonalHeaderTemplate control = new DiagonalHeaderTemplate(true);

            // 探针版面自证：四层树 / 八叶 / 每叶 60px 宽、每层 30px 高，格窗口才够放下 12px 字号的字。
            Assert.Equal(ProbeLevelCount, semantic.Layout.LevelCount);
            Assert.Equal(ProbeLeafCount, semantic.Layout.LeafCount);
            Assert.Equal(8, semantic.Root.LeafSpan);
            SKRect probeLeafRect = semantic.Layout.RectOf(semantic.Root.Find("f1")!);
            Assert.True(Math.Abs(probeLeafRect.Width - ProbeLeafWidth) <= RectEps,
                        "探针叶子格宽应为 480/8=60，实测 " + probeLeafRect.Width);

            using (ImageProbe plain = RenderThroughPipeline(semantic))
            using (ImageProbe withText = RenderThroughPipeline(control))
            {
                Assert.Equal(520, plain.Width);
                Assert.Equal(160, plain.Height);

                // ---- ① 斜线墨：沿声明方向逐段采样，落点必须在解析直线上
                ColumnHeaderNode leafUp = semantic.Root.Find("f3")!;   // 叶子格，左下→右上
                ColumnHeaderNode leafDown = semantic.Root.Find("s1")!; // 叶子格，左上→右下
                ColumnHeaderNode groupUp = semantic.Root.Find("blood")!; // 分组格，左下→右上
                Assert.True(leafUp.Diagonal && leafDown.Diagonal && groupUp.Diagonal,
                            "构造前置条件：三个格都该被标记为斜线格");
                Assert.Equal(DiagonalDirection.LowerLeftToUpperRight, leafUp.DiagonalDirection);
                Assert.Equal(DiagonalDirection.UpperLeftToLowerRight, leafDown.DiagonalDirection);

                AssertDiagonalInkAlongDeclaredDirection(plain, semantic.Layout.RectOf(leafUp),
                                                        leafUp.DiagonalDirection, "叶子格 f3");
                AssertDiagonalInkAlongDeclaredDirection(plain, semantic.Layout.RectOf(leafDown),
                                                        leafDown.DiagonalDirection, "叶子格 s1");
                AssertDiagonalInkAlongDeclaredDirection(plain, semantic.Layout.RectOf(groupUp),
                                                        groupUp.DiagonalDirection, "分组格 blood");

                // ---- ② 格内不落字：去掉斜线自身的墨带后，格内应一个墨像素都不剩
                Assert.True(OffDiagonalInk(plain, semantic.Layout.RectOf(leafUp),
                                           leafUp.DiagonalDirection) == 0, "斜线格 f3 内除斜线外还有墨＝落字了");
                Assert.True(OffDiagonalInk(plain, semantic.Layout.RectOf(leafDown),
                                           leafDown.DiagonalDirection) == 0, "斜线格 s1 内除斜线外还有墨＝落字了");
                Assert.True(OffDiagonalInk(plain, semantic.Layout.RectOf(groupUp),
                                           groupUp.DiagonalDirection) == 0, "斜线分组格 blood 内除斜线外还有墨＝落字了");

                // ---- ③ 判据不空转：同尺寸的非斜线格（模板照常写字）在同一个窗口里必须读到墨
                ColumnHeaderNode textLeaf = semantic.Root.Find("b1")!;
                ColumnHeaderNode textGroup = semantic.Root.Find("machine")!;
                int leafGlyphs = InteriorInk(plain, semantic.Layout.RectOf(textLeaf));
                int groupGlyphs = InteriorInk(plain, semantic.Layout.RectOf(textGroup));
                Assert.True(leafGlyphs > 5, "对照叶子格 b1 格内墨像素 " + leafGlyphs + "＝取样窗口没对准文字，用例失去意义");
                Assert.True(groupGlyphs > 5, "对照分组格 machine 格内墨像素 " + groupGlyphs + "＝取样窗口没对准文字");

                // ---- ④ 反向对照：同模板强制在斜线格里写字，同一个窗口必须读到一大片墨
                // 实测取证（96dpi，net8.0）：语义模式下 f3 / s1 / blood 三格的"斜线外墨像素"都是 0，
                // 格内总墨（含斜线本身与抗锯齿）f3=88、blood=176；对照非斜线格 b1 / machine 的格内墨
                // 是 156 / 232；把同一份模板改成"斜线格也写字"后，同一窗口的斜线外墨变成
                // 222 / 203 / 275。所以 0 与 200+ 之间差两个数量级，下面 5 这个阈值有 40 倍余量。
                int controlF3 = OffDiagonalInk(withText, control.Layout.RectOf(control.Root.Find("f3")!),
                                               leafUp.DiagonalDirection);
                int controlBlood = OffDiagonalInk(withText, control.Layout.RectOf(control.Root.Find("blood")!),
                                                  groupUp.DiagonalDirection);
                Assert.True(controlF3 > 5,
                            "写字对照模式下 f3 的斜线外墨像素只 " + controlF3 + "，说明上一条 ==0 的断言本来就无法证伪");
                Assert.True(controlBlood > 5,
                            "写字对照模式下 blood 的斜线外墨像素只 " + controlBlood + "，判据失去意义");

                // ---- ⑤ 中心点本应放字处：斜线恰好穿过格中心，故用"中心邻域内除线带外无墨"另证一遍
                Assert.True(OffDiagonalInkInBox(plain, semantic.Layout.RectOf(leafUp), leafUp.DiagonalDirection,
                                                new SKRect(semantic.Layout.RectOf(leafUp).MidX - 12f,
                                                           semantic.Layout.RectOf(leafUp).MidY - 6f,
                                                           semantic.Layout.RectOf(leafUp).MidX + 12f,
                                                           semantic.Layout.RectOf(leafUp).MidY + 6f)) == 0,
                            "f3 格中心 24×12 的放字区里，除斜线墨带外不该有任何墨");
            }
        }

        // ============================================================================ 非法与退化

        [Fact]
        public void 单节点与空子节点集退化为一片叶子()
        {
            ColumnHeaderNode single = new ColumnHeaderNode("only", "唯一列");
            Assert.True(single.IsLeaf);
            Assert.Equal(1, single.LeafSpan);
            Assert.Equal(0, single.Depth);
            Assert.True(single.MaxLevel == 0, "MaxLevel 实测 " + single.MaxLevel);
            Assert.True(single.Children.Count == 0, "叶子不该有子节点，实到 " + single.Children.Count);
            Assert.Null(single.Parent);
            Assert.True(single.IndexInParent == 0, "未挂树的节点 IndexInParent 实测 " + single.IndexInParent);
            Assert.True(single.Leaves().Count == 1, "单节点 Leaves() 实到 " + single.Leaves().Count + " 项");
            Assert.True(single.NodesAtLevel(0).Count == 1, "根层应只有它自己，实到 " + single.NodesAtLevel(0).Count);
            Assert.True(single.NodesAtLevel(1).Count == 0, "第 1 层应为空，实到 " + single.NodesAtLevel(1).Count);
            Assert.True(ReferenceEquals(single, single.Find("only")));
            Assert.Null(single.Find("别的"));
            Assert.Null(single.Find(null!));
            Assert.Null(single.Find(string.Empty));

            // 显式空集合是叶子；null 集合是非法输入（缺陷③的处置：静默退化成叶子会把"这层还没想好"写进版面）。
            ColumnHeaderNode emptyKids = new ColumnHeaderNode("e", "空子集", new ColumnHeaderNode[0]);
            Assert.True(emptyKids.IsLeaf && emptyKids.LeafSpan == 1);
            Assert.Throws<ArgumentNullException>(() =>
            {
                ColumnHeaderNode nullKids = new ColumnHeaderNode("n", "null 子集", null!);
                Assert.True(!nullKids.IsLeaf, "不该走到这里：null children 必须被拒");
            });

            // 单节点表落到像素：一列一层，矩形就是区域本身。
            SKRect area = new SKRect(5f, 7f, 155f, 37f);
            HeaderTableLayout layout = new HeaderTableLayout(single, area, 30f);
            Assert.Equal(1, layout.LevelCount);
            Assert.Equal(1, layout.LeafCount);
            Assert.Equal(30f, layout.RequiredHeight);
            Assert.Equal(2, layout.LeafBounds.Count);
            SKRect rect = layout.RectOf(single);
            Assert.True(Math.Abs(rect.Left - 5f) <= RectEps && Math.Abs(rect.Right - 155f) <= RectEps,
                        "单列表头应铺满区域宽，实测 " + rect);
            Assert.True(Math.Abs(rect.Bottom - 37f) <= RectEps,
                        "区域高等于行高时末层下缘取区域下缘（_snapBottom），实测 " + rect.Bottom);
            Assert.Equal(0, layout.LeafIndexOf(single));
        }

        [Fact]
        public void LeafSpan没有公开写入口且恒不小于1()
        {
            // docs/07 那句"LeafSpan 为 0 或负"在公开面上构造不出来：LeafSpan/Depth 是只读属性 +
            // 构造期算定并存的 readonly 字段（ColumnHeaderNode.cs:27-28、77-94），最小值天然是 1（叶子）。
            PropertyInfo? span = typeof(ColumnHeaderNode).GetProperty("LeafSpan", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo? depth = typeof(ColumnHeaderNode).GetProperty("Depth", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(span);
            Assert.NotNull(depth);
            Assert.True(span!.GetSetMethod() == null, "LeafSpan 出现了公开 setter，与源码现状不符");
            Assert.True(depth!.GetSetMethod() == null, "Depth 出现了公开 setter，与源码现状不符");

            ColumnHeaderNode root = RaggedTree();
            List<ColumnHeaderNode> all = new List<ColumnHeaderNode>();
            CollectAll(root, all);
            for (int i = 0; i < all.Count; i++)
            {
                Assert.True(all[i].LeafSpan >= 1, all[i].Key + " 的 LeafSpan = " + all[i].LeafSpan);
                Assert.True(all[i].Depth >= 0, all[i].Key + " 的 Depth = " + all[i].Depth);
                Assert.True(all[i].IsLeaf == (all[i].LeafSpan == 1 && all[i].Depth == 0) || !all[i].IsLeaf,
                            "叶子 " + all[i].Key + " 的 LeafSpan/Depth 与 IsLeaf 不自洽");
            }

            // 能走到的"最非法"入口是权重全 0（列宽无从分配），按源码抛 ArgumentException。
            ColumnHeaderNode w1 = new ColumnHeaderNode("w1", "一");
            ColumnHeaderNode w2 = new ColumnHeaderNode("w2", "二");
            w1.LeafWeight = 0d;
            w2.LeafWeight = 0d;
            ColumnHeaderNode zeroRoot = new ColumnHeaderNode("z", "零权重", new[] { w1, w2 });
            Assert.Throws<ArgumentException>(() =>
                new HeaderTableLayout(zeroRoot, new SKRect(0f, 0f, 200f, 30f), 30f));

            // 单列权重 0 是合法降级：那一列宽塌成 0，不报错（现状记录）。
            ColumnHeaderNode k1 = new ColumnHeaderNode("k1", "一");
            ColumnHeaderNode k2 = new ColumnHeaderNode("k2", "二");
            ColumnHeaderNode k3 = new ColumnHeaderNode("k3", "三");
            k1.LeafWeight = 1d;
            k2.LeafWeight = 0d;
            k3.LeafWeight = 1d;
            ColumnHeaderNode mixed = new ColumnHeaderNode("m", "混合权重", new[] { k1, k2, k3 });
            HeaderTableLayout mixedLayout = new HeaderTableLayout(mixed, new SKRect(0f, 0f, 100f, 30f), 30f);
            Assert.True(Math.Abs(mixedLayout.RectOf(k2).Width) <= RectEps,
                        "零权重列应塌成 0 宽，实测 " + mixedLayout.RectOf(k2).Width);
            Assert.True(Math.Abs(mixedLayout.RectOf(k1).Width - 50f) <= 1e-3f);
            Assert.True(Math.Abs(mixedLayout.RectOf(k3).Right - 100f) <= RectEps);

            // 权重自身的非法值：负数、NaN、无穷都挡。
            Assert.Throws<ArgumentOutOfRangeException>(() => k1.LeafWeight = -1d);
            Assert.Throws<ArgumentOutOfRangeException>(() => k1.LeafWeight = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => k1.LeafWeight = double.PositiveInfinity);
            Assert.Throws<ArgumentOutOfRangeException>(() => k1.LeafWeight = double.NegativeInfinity);
            k1.LeafWeight = 0d;   // 0 合法（塌列）
            Assert.Equal(0d, k1.LeafWeight);
        }

        [Fact]
        public void 父子引用环不可构造重复挂接被拒()
        {
            ColumnHeaderNode child = new ColumnHeaderNode("c", "子");
            ColumnHeaderNode parentOne = new ColumnHeaderNode("p1", "父一", new[] { child });
            Assert.True(ReferenceEquals(child.Parent, parentOne));
            Assert.Equal(0, child.IndexInParent);

            // 已挂接的节点不能再挂到第二个父节点 ⇒ 想造环必须复用同一实例，此路被封（ColumnHeaderNode.cs:51-56）。
            Assert.Throws<ArgumentException>(() =>
                new ColumnHeaderNode("p2", "父二", new[] { child }));
            Assert.True(ReferenceEquals(child.Parent, parentOne), "拒绝后原父引用不该被改写");

            // 同一实例在子集合里出现两次：先被挂上正在构造的父，再由重复 key 检查挡下。
            ColumnHeaderNode twice = new ColumnHeaderNode("t", "两次");
            Assert.Throws<ArgumentException>(() => new ColumnHeaderNode("q", "自引用尝试", new[] { twice, twice }));

            // 子集合只能是构造期一次性给定的快照：Children 是只读列表，环无法事后闭环。
            PropertyInfo? children = typeof(ColumnHeaderNode).GetProperty("Children", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(children);
            Assert.True(children!.GetSetMethod() == null, "Children 出现了公开 setter");

            // 五层链照常展开，不无限递归（能造环的话这里就会挂死）。
            ColumnHeaderNode tree = FiveLevelChain();
            Assert.Equal(4, tree.Depth);
            Assert.Equal(2, tree.LeafSpan);
            Assert.Equal(2, tree.Leaves().Count);
            List<ColumnHeaderNode> all = new List<ColumnHeaderNode>();
            CollectAll(tree, all);
            Assert.Equal(6, all.Count);
        }

        [Fact]
        public void 子节点集合按引用快照外部改动不影响树()
        {
            List<ColumnHeaderNode> kids = new List<ColumnHeaderNode>();
            kids.Add(new ColumnHeaderNode("x", "甲"));
            ColumnHeaderNode node = new ColumnHeaderNode("y", "父", kids);
            Assert.Equal(1, node.LeafSpan);

            kids.Add(new ColumnHeaderNode("z", "乙"));
            kids.RemoveAt(0);
            Assert.True(node.Children.Count == 1, "构造后外部增删不该改树，Children 实到 " + node.Children.Count);
            Assert.Equal(1, node.LeafSpan);
            Assert.Equal("x", node.Children[0].Key);
            Assert.True(node.LeafSpan != 2, "子集合被外部列表改动污染了");
        }

        [Fact]
        public void 构造期非法参数按源码拒绝()
        {
            Assert.Throws<ArgumentNullException>(() => new ColumnHeaderNode(null!, "标签"));
            Assert.Throws<ArgumentException>(() => new ColumnHeaderNode(string.Empty, "标签"));
            Assert.Throws<ArgumentException>(() =>
                new ColumnHeaderNode("p", "父", new ColumnHeaderNode[] { null! }));
            Assert.Throws<ArgumentException>(() =>
                new ColumnHeaderNode("p", "父", new[] { new ColumnHeaderNode("dup", "一"), new ColumnHeaderNode("dup", "二") }));

            // label 传 null 降级为空串（ColumnHeaderNode.cs:40），不是报错。
            ColumnHeaderNode nullLabel = new ColumnHeaderNode("nl", null!);
            Assert.Equal(string.Empty, nullLabel.Label);

            // 同 key 允许出现在不同分组下（源码注释 ColumnHeaderNode.cs:108），只有同层重复才拒。
            ColumnHeaderNode left = new ColumnHeaderNode("L", "左组", new[] { new ColumnHeaderNode("same", "左内") });
            ColumnHeaderNode right = new ColumnHeaderNode("R", "右组", new[] { new ColumnHeaderNode("same", "右内") });
            ColumnHeaderNode both = new ColumnHeaderNode("T", "顶层", new[] { left, right });
            Assert.Equal(2, both.LeafSpan);

            // 深度优先先序 ⇒ 重名取第一个（左组的那个）。
            ColumnHeaderNode? found = both.Find("same");
            Assert.NotNull(found);
            Assert.Equal("左内", found!.Label);
            Assert.True(ReferenceEquals(found, left.Children[0]));
            Assert.False(ReferenceEquals(found, right.Children[0]));

            SKRect area = new SKRect(0f, 0f, 100f, 60f);
            HeaderTableLayout layout = new HeaderTableLayout(both, area, 30f);
            Assert.True(Math.Abs(layout.RectOf("same").Left - 0f) <= RectEps,
                        "按 key 取矩形取到的不是深度优先第一个");
            Assert.Throws<KeyNotFoundException>(() => layout.RectOf("不存在"));
            Assert.Throws<KeyNotFoundException>(() => layout.FindNode("不存在"));
        }

        [Fact]
        public void 布局求解期非法参数按源码拒绝()
        {
            ColumnHeaderNode root = UniformTree();
            SKRect goodArea = new SKRect(0f, 0f, 612f, 120f);

            Assert.Throws<ArgumentNullException>(() => new HeaderTableLayout(null!, goodArea, 30f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderTableLayout(root, goodArea, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderTableLayout(root, goodArea, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HeaderTableLayout(root, goodArea, float.NaN));

            // 缺陷②（已修）：正无穷行高以前畅通无阻——RequiredHeight 变成无穷，
            // 且 row=0 的节点上缘算出 0×无穷 = NaN，整棵树的矩形一起坏掉。现在和 0/负/NaN 一样被拒。
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HeaderTableLayout(root, goodArea, float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new HeaderTableLayout(root, goodArea, float.NegativeInfinity));

            // 区域宽：!(Width > eps) 同时挡 0/负数/NaN（ColumnHeaderNode.cs:269-275）。
            Assert.Throws<ArgumentException>(() => new HeaderTableLayout(root, new SKRect(0f, 0f, 0f, 120f), 30f));
            Assert.Throws<ArgumentException>(() => new HeaderTableLayout(root, new SKRect(100f, 0f, 50f, 120f), 30f));
            Assert.Throws<ArgumentException>(() =>
                new HeaderTableLayout(root, new SKRect(float.NaN, 0f, 612f, 120f), 30f));

            // 区域高与 RequiredHeight 不符只是不 snap，不报错（现状记录）：末层下缘按行高算，可能不贴区域下缘。
            HeaderTableLayout mismatch = new HeaderTableLayout(root, new SKRect(0f, 0f, 612f, 100f), 30f);
            Assert.Equal(120f, mismatch.RequiredHeight);
            SKRect lastRow = mismatch.LevelRect(3);
            Assert.True(Math.Abs(lastRow.Bottom - 120f) <= RectEps,
                        "区域高 100 不等于所需 120 时末行仍按行高铺开（实测下缘 " + lastRow.Bottom
                        + "）：源码不裁也不抛，越界由高由调用方自己核对 RequiredHeight。这是现状。");
            Assert.True(lastRow.Bottom > mismatch.Area.Bottom);
        }

        [Fact]
        public void 越界与外来节点按源码抛对应异常()
        {
            ColumnHeaderNode root = UniformTree();
            HeaderTableLayout layout = new HeaderTableLayout(root, new SKRect(0f, 0f, 612f, 120f), 30f);

            Assert.Throws<ArgumentOutOfRangeException>(() => layout.LevelRect(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.LevelRect(layout.LevelCount));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.ColumnRect(-1, 0f, 30f));
            Assert.Throws<ArgumentOutOfRangeException>(() => layout.ColumnRect(layout.LeafCount, 0f, 30f));

            Assert.Throws<ArgumentNullException>(() => layout.RectOf((ColumnHeaderNode)null!));
            Assert.Throws<ArgumentNullException>(() => layout.LeafIndexOf(null!));

            ColumnHeaderNode foreign = new ColumnHeaderNode("foreign", "外来的");
            Assert.Throws<KeyNotFoundException>(() => layout.RectOf(foreign));
            Assert.Throws<KeyNotFoundException>(() => layout.LeafIndexOf(foreign));

            ColumnHeaderNode? inner = root.Find("g0-s0");
            Assert.NotNull(inner);
            Assert.Throws<KeyNotFoundException>(() => layout.LeafIndexOf(inner!));   // 非叶子
            Assert.Throws<KeyNotFoundException>(() => layout.LeafIndexOf(root));      // 根也不是叶子

            // ColumnRect 的纵向完全由调用方给，源码不做 top/bottom 合法性检查（现状记录）。
            SKRect inverted = layout.ColumnRect(0, 90f, 10f);
            Assert.True(inverted.Top > inverted.Bottom, "现状：ColumnRect 不校验上下缘顺序");
            Assert.True(Math.Abs(inverted.Width - 34f) <= 1e-3f);
        }

        [Fact]
        public void DiagonalEnds按方向给出对角端点()
        {
            SKRect rect = new SKRect(10f, 20f, 50f, 60f);

            SKPoint a, b;
            ColumnHeaderNode.DiagonalEnds(rect, DiagonalDirection.LowerLeftToUpperRight, out a, out b);
            Assert.Equal(10f, a.X);
            Assert.Equal(60f, a.Y);
            Assert.Equal(50f, b.X);
            Assert.Equal(20f, b.Y);

            ColumnHeaderNode.DiagonalEnds(rect, DiagonalDirection.UpperLeftToLowerRight, out a, out b);
            Assert.Equal(10f, a.X);
            Assert.Equal(20f, a.Y);
            Assert.Equal(50f, b.X);
            Assert.Equal(60f, b.Y);

            // 未知枚举值走 else 分支＝左上到右下（源码没有 default 抛，现状记录）。
            ColumnHeaderNode.DiagonalEnds(rect, (DiagonalDirection)99, out a, out b);
            Assert.Equal(20f, a.Y);
            Assert.Equal(60f, b.Y);

            // 零宽/零高矩形不校验，端点退化但仍返回（现状记录：绘制层要自己防 0 尺寸格）。
            ColumnHeaderNode.DiagonalEnds(new SKRect(5f, 5f, 5f, 40f), DiagonalDirection.LowerLeftToUpperRight, out a, out b);
            Assert.Equal(a.X, b.X);
            ColumnHeaderNode.DiagonalEnds(SKRect.Empty, DiagonalDirection.UpperLeftToLowerRight, out a, out b);
            Assert.Equal(0f, a.X);
            Assert.Equal(0f, b.X);

            // 标志位只是数据：不画任何东西、也不自动置方向（现状记录）。
            ColumnHeaderNode node = new ColumnHeaderNode("d", "划掉");
            Assert.False(node.Diagonal);
            Assert.Equal(DiagonalDirection.LowerLeftToUpperRight, node.DiagonalDirection);
            node.Diagonal = true;
            Assert.Equal(DiagonalDirection.LowerLeftToUpperRight, node.DiagonalDirection);
        }

        // ============================================================================ SlotAxis 回归

        [Fact]
        public void SlotAxis存量行为零变化按解析式复算()
        {
            // 口径逐条抄自 Geometry/SlotAxis.cs 的公开定义（本轮 T220 没有动这个文件，
            // 把解析式写死在这里就是"改动前后一致"的回归钉）：
            //   TotalSlots      = GroupCount × SlotsPerGroup                       (SlotAxis.cs:34)
            //   GroupWidth      = SlotWidth × SlotsPerGroup                        (SlotAxis.cs:35)
            //   Right           = Left + TotalSlots × SlotWidth                    (SlotAxis.cs:36)
            //   Width           = Right − Left                                     (SlotAxis.cs:37)
            //   SlotX(i)        = Left + i × SlotWidth                             (SlotAxis.cs:40)
            //   GroupX(g)       = Left + g × GroupWidth                            (SlotAxis.cs:43)
            //   GroupOfSlot(i)  = i / SlotsPerGroup（整除）                        (SlotAxis.cs:45)
            //   SlotInGroup(i)  = i % SlotsPerGroup                                (SlotAxis.cs:47)
            //   SlotCenterX(i)  = SlotX(i) + SlotWidth/2 = Left + (i + 0.5)×SlotWidth   (SlotAxis.cs:49)
            //   GroupCenterX(g) = GroupX(g) + GroupWidth/2 = Left + (g + 0.5)×GroupWidth (SlotAxis.cs:51)
            //   SlotRect(i)     = [SlotX(i), SlotX(i+1)]                           (SlotAxis.cs:54-57)
            //   GroupRect(g)    = [GroupX(g), GroupX(g+1)]                         (SlotAxis.cs:60-63)
            //   SlotRangeRect(f,l) = [SlotX(f), SlotX(l+1)]                        (SlotAxis.cs:66-69)
            const float left = 12f;
            const float slotWidth = 15f;      // 全部取二进制精确值，等式可以按位比
            const int slotsPerGroup = 6;
            const int groupCount = 3;
            SlotAxis axis = new SlotAxis(left, slotWidth, slotsPerGroup, groupCount);

            Assert.Equal(left, axis.Left);
            Assert.Equal(slotWidth, axis.SlotWidth);
            Assert.Equal(slotsPerGroup, axis.SlotsPerGroup);
            Assert.Equal(groupCount, axis.GroupCount);

            Assert.Equal(18, axis.TotalSlots);                 // 3 × 6
            Assert.Equal(90f, axis.GroupWidth);                // 15 × 6
            Assert.Equal(282f, axis.Right);                    // 12 + 18 × 15
            Assert.Equal(270f, axis.Width);                    // 282 − 12

            // 边界槽：0 与最后一个。
            Assert.Equal(12f, axis.SlotX(0));
            Assert.Equal(19.5f, axis.SlotCenterX(0));          // 12 + 0.5 × 15
            Assert.Equal(267f, axis.SlotX(17));                // 12 + 17 × 15
            Assert.Equal(274.5f, axis.SlotCenterX(17));        // 12 + 17.5 × 15
            Assert.Equal(282f, axis.SlotX(18));                // 末槽右缘 == Right

            for (int slot = 0; slot < axis.TotalSlots; slot++)
            {
                Assert.Equal(left + slot * slotWidth, axis.SlotX(slot));
                Assert.Equal(left + (slot + 0.5f) * slotWidth, axis.SlotCenterX(slot));
                Assert.Equal(slot / slotsPerGroup, axis.GroupOfSlot(slot));
                Assert.Equal(slot % slotsPerGroup, axis.SlotInGroup(slot));

                SKRect slotRect = axis.SlotRect(slot, 100f, 130f);
                Assert.True(slotRect.Left == axis.SlotX(slot) && slotRect.Right == axis.SlotX(slot + 1),
                            "SlotRect 第 " + slot + " 槽与 SlotX 不符：" + slotRect);
                Assert.Equal(100f, slotRect.Top);
                Assert.Equal(130f, slotRect.Bottom);
                Assert.Equal(15f, slotRect.Width);
            }

            // 边界组：0 与最后一个。
            Assert.Equal(12f, axis.GroupX(0));
            Assert.Equal(57f, axis.GroupCenterX(0));           // 12 + 0.5 × 90
            Assert.Equal(192f, axis.GroupX(2));                // 12 + 2 × 90
            Assert.Equal(237f, axis.GroupCenterX(2));          // 12 + 2.5 × 90
            for (int group = 0; group < groupCount; group++)
            {
                Assert.Equal(left + group * axis.GroupWidth, axis.GroupX(group));
                Assert.Equal(left + (group + 0.5f) * axis.GroupWidth, axis.GroupCenterX(group));
                SKRect rect = axis.GroupRect(group, 10f, 20f);
                Assert.Equal(axis.SlotX(group * slotsPerGroup), rect.Left);
                Assert.Equal(axis.SlotX((group + 1) * slotsPerGroup), rect.Right);
                Assert.Equal(90f, rect.Width);
            }

            // 跨槽合并矩形（合并表头用的就是它）：槽 3..5 与"每槽单独"的并一致。
            SKRect range = axis.SlotRangeRect(3, 5, 0f, 10f);
            Assert.Equal(57f, range.Left);                     // 12 + 3 × 15
            Assert.Equal(102f, range.Right);                   // 12 + 6 × 15
            Assert.Equal(45f, range.Width);
            Assert.True(range.Left == axis.SlotRect(3, 0f, 10f).Left
                        && range.Right == axis.SlotRect(5, 0f, 10f).Right,
                        "SlotRangeRect 与单槽矩形不一致");

            // 浮点不可精确表示的槽宽：钉"一次乘法"的解析式，容差 1e-6。
            // 说明：float32 下逐槽累加与直接乘法的差别只有 ~1e-7，这个容差区分不了两者，
            // 所以真钉住"存量口径"的是上面 15f/6/3 那组按位相等，这里只验不把误差放大。
            SlotAxis fine = new SlotAxis(0f, 0.1f, 3, 4);
            Assert.Equal(12, fine.TotalSlots);
            Assert.True(Math.Abs(fine.Right - 1.2f) <= 1e-6f, "0.1 槽宽的 Right 漂了：" + fine.Right);
            Assert.True(Math.Abs(fine.SlotCenterX(11) - 1.15f) <= 1e-6f, "末槽中心漂了：" + fine.SlotCenterX(11));

            // 与表头树并存：两者互不知晓，SlotAxis 不因新类型而改变任何求值。
            Assert.Equal(282f, axis.Right);
        }

        [Fact]
        public void SlotAxis非法参数与越界槽位按源码现状()
        {
            // 构造函数只校验两个计数（SlotAxis.cs:14-21）。
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotAxis(0f, 15f, 0, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotAxis(0f, 15f, -1, 3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotAxis(0f, 15f, 6, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SlotAxis(0f, 15f, 6, -3));

            // 现状记录（非期望）：slotWidth 与 left 都不校验——负槽宽能构造出 Right 小于 Left 的轴，
            // 索引也不越界检查，负数/超大槽号直接算出轴外矩形。
            SlotAxis negative = new SlotAxis(10f, -5f, 4, 2);
            Assert.True(negative.Right < negative.Left,
                        "负槽宽未被拒绝（Right=" + negative.Right + "，Left=" + negative.Left + "）：源码不校验 slotWidth，这是现状而非期望");
            Assert.Equal(-2.5f, negative.SlotCenterX(2));      // 10 + 2.5 × (−5)
            Assert.Equal(-5f, negative.SlotX(3));              // 10 + 3 × (−5)
            Assert.Equal(-30f, negative.Right);                // 10 + 8 × (−5)

            SlotAxis axis = new SlotAxis(12f, 15f, 6, 3);
            Assert.True(Math.Abs(axis.SlotCenterX(999) - (12f + 999.5f * 15f)) <= 1e-3f,
                        "越界槽号未抛（现状）：SlotCenterX(999)=" + axis.SlotCenterX(999));
            Assert.Equal(12f - 15f, axis.SlotX(-1));           // 负槽号同样直接算，不越界检查
            Assert.Equal(259.5f, axis.SlotCenterX(16));        // 12 + 16.5 × 15
        }

        // ============================================================================ 存量版面零变化

        [Fact]
        public void 存量三测单版面零变化PNG哈希等于既有基准()
        {
            // 基准值直接引用 CrossTfmTests 里钉住的那个常量（它是 private const，按字段引用取，
            // 不复制字面量，避免两处基准各改各的）。取证过程见该文件第 18-22 行注释：
            // 由 net8.0 与 net462 两轮实测同一个 SHA-256。
            FieldInfo? field = typeof(CrossTfmTests).GetField("PngSha256", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.True(field != null, "找不到 CrossTfmTests.PngSha256，基准常量被改名或删除");
            string? baseline = (string?)field!.GetValue(null);
            Assert.True(!string.IsNullOrEmpty(baseline), "CrossTfmTests.PngSha256 为空，基准尚未录制");

            byte[] png;
            using (Chart chart = TestDocuments.Temperature())
            {
                png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            }
            string actual = Sha256Hex(png);

            Assert.True(string.Equals(actual, baseline, StringComparison.OrdinalIgnoreCase),
                        "表头改动扰动了存量三测单版面：实测 SHA-256 " + actual + "，基准 " + baseline);
        }

        // ============================================================================ 断言助手

        private static void AssertSpanConservation(ColumnHeaderNode node)
        {
            if (node.IsLeaf)
            {
                Assert.Equal(1, node.LeafSpan);
                return;
            }

            int sum = 0;
            int maxChildDepth = 0;
            for (int i = 0; i < node.Children.Count; i++)
            {
                ColumnHeaderNode child = node.Children[i];
                AssertSpanConservation(child);
                sum += child.LeafSpan;
                Assert.True(ReferenceEquals(child.Parent, node), child.Key + " 的 Parent 没指回 " + node.Key);
                Assert.Equal(i, child.IndexInParent);
                if (child.Depth > maxChildDepth) maxChildDepth = child.Depth;
            }
            Assert.Equal(sum, node.LeafSpan);
            Assert.Equal(maxChildDepth + 1, node.Depth);
        }

        /// <summary>声明方向的斜线是否真的落到像素上：沿解析直线逐段采样要求附近有墨，
        /// 并沿"另一条对角线"同位置采样要求无墨。采样刻意只取格宽两端各 20% 的区段——
        /// 两条对角线只在格心重合，越靠中心两者越近（30px 高的格在中点附近只差几像素），
        /// 会被斜线自身的抗锯齿尾巴污染，故 f 落在 0.18..0.32 与 0.68..0.82，
        /// 使两条线的竖向间距恒大于 0.36×格高（约 10.8px），"无墨"判据才不是采样半径太宽的假象。</summary>
        private static void AssertDiagonalInkAlongDeclaredDirection(ImageProbe probe, SKRect cell,
                                                                   DiagonalDirection direction, string label)
        {
            SKPoint a, b;
            ColumnHeaderNode.DiagonalEnds(cell, direction, out a, out b);
            SKPoint c, d;
            ColumnHeaderNode.DiagonalEnds(cell, OtherDirection(direction), out c, out d);

            double[] fractions = { 0.18d, 0.215d, 0.25d, 0.285d, 0.32d,
                                   0.68d, 0.715d, 0.75d, 0.785d, 0.82d };
            int samples = 0;
            for (int i = 0; i < fractions.Length; i++)
            {
                double f = fractions[i];
                double onX = a.X + (b.X - a.X) * f;
                double onY = a.Y + (b.Y - a.Y) * f;
                double antiY = c.Y + (d.Y - c.Y) * ((onX - c.X) / (double)(d.X - c.X));
                samples++;

                int inkX, inkY;
                Assert.True(InkNearDevicePixel(probe, onX, onY, 2, out inkX, out inkY),
                            label + " 在声明对角方向上第 " + samples + " 个采样点 ("
                            + onX.ToString("0.##", CultureInfo.InvariantCulture) + ","
                            + onY.ToString("0.##", CultureInfo.InvariantCulture) + ") 没有墨：斜线没落进格内");
                Assert.False(InkNearDevicePixel(probe, onX, antiY, 1.5, out inkX, out inkY),
                             label + " 在反向对角线 (" + onX.ToString("0.##", CultureInfo.InvariantCulture) + ","
                             + antiY.ToString("0.##", CultureInfo.InvariantCulture) + ") 附近有墨，落点 ("
                             + inkX + "," + inkY + ")＝划的不是声明的那个方向");
            }
            Assert.Equal(10, samples);

            Assert.True(InteriorInk(probe, cell) > 0, label + " 格内完全没墨，斜线与边框都没落地");
        }

        private static DiagonalDirection OtherDirection(DiagonalDirection direction)
        {
            return direction == DiagonalDirection.LowerLeftToUpperRight
                ? DiagonalDirection.UpperLeftToLowerRight
                : DiagonalDirection.LowerLeftToUpperRight;
        }

        /// <summary>设备像素坐标即逻辑像素（96dpi，缩放 1.0），故这里直接按逻辑值取整。
        /// 命中时回传第一个墨像素（inkX/inkY），便于失败消息定位。</summary>
        private static bool InkNearDevicePixel(ImageProbe probe, double x, double y, double radius,
                                               out int inkX, out int inkY)
        {
            int x0 = (int)Math.Floor(x - radius);
            int x1 = (int)Math.Ceiling(x + radius);
            int y0 = (int)Math.Floor(y - radius);
            int y1 = (int)Math.Ceiling(y + radius);
            inkX = -1;
            inkY = -1;
            for (int py = Math.Max(0, y0); py <= Math.Min(probe.Height - 1, y1); py++)
            {
                for (int px = Math.Max(0, x0); px <= Math.Min(probe.Width - 1, x1); px++)
                {
                    if (probe.IsInk(px, py))
                    {
                        inkX = px;
                        inkY = py;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>格内（离边框留 <see cref="CellMarginPx"/> 余量）的全部墨像素数。</summary>
        private static int InteriorInk(ImageProbe probe, SKRect cell)
        {
            return InkInWindow(probe, cell, null, null, 0d, false);
        }

        /// <summary>格内离声明对角线垂距超过 <see cref="DiagonalBand"/> 的墨像素数＝"除斜线以外的墨"。</summary>
        private static int OffDiagonalInk(ImageProbe probe, SKRect cell, DiagonalDirection direction)
        {
            SKPoint a, b;
            ColumnHeaderNode.DiagonalEnds(cell, direction, out a, out b);
            return InkInWindow(probe, cell, a, b, DiagonalBand, true);
        }

        /// <summary>格内某个具体窗口（逻辑坐标，相对格矩形给）里的斜线外墨像素数。</summary>
        private static int OffDiagonalInkInBox(ImageProbe probe, SKRect cell, DiagonalDirection direction, SKRect window)
        {
            SKPoint a, b;
            ColumnHeaderNode.DiagonalEnds(cell, direction, out a, out b);
            int left = Math.Max((int)Math.Ceiling(window.Left), (int)Math.Ceiling(cell.Left) + CellMarginPx);
            int right = Math.Min((int)Math.Floor(window.Right), (int)Math.Floor(cell.Right) - CellMarginPx);
            int top = Math.Max((int)Math.Ceiling(window.Top), (int)Math.Ceiling(cell.Top) + CellMarginPx);
            int bottom = Math.Min((int)Math.Floor(window.Bottom), (int)Math.Floor(cell.Bottom) - CellMarginPx);

            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            int ink = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (!probe.IsInk(x, y)) continue;
                    double px = x + 0.5;
                    double py = y + 0.5;
                    double distance = Math.Abs((px - a.X) * dy - (py - a.Y) * dx) / length;
                    if (distance > DiagonalBand) ink++;
                }
            }
            return ink;
        }

        private static int InkInWindow(ImageProbe probe, SKRect cell, SKPoint? a, SKPoint? b,
                                       double perpendicularLimit, bool excludeBand)
        {
            int left = (int)Math.Ceiling(cell.Left) + CellMarginPx;
            int right = (int)Math.Floor(cell.Right) - CellMarginPx;
            int top = (int)Math.Ceiling(cell.Top) + CellMarginPx;
            int bottom = (int)Math.Floor(cell.Bottom) - CellMarginPx;

            double dx = 0d, dy = 0d, length = 1d, ax = 0d, ay = 0d;
            if (excludeBand && a != null && b != null)
            {
                ax = a.Value.X;
                ay = a.Value.Y;
                dx = b.Value.X - ax;
                dy = b.Value.Y - ay;
                length = Math.Sqrt(dx * dx + dy * dy);
            }

            int ink = 0;
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    if (!probe.IsInk(x, y)) continue;
                    if (excludeBand)
                    {
                        double px = x + 0.5;
                        double py = y + 0.5;
                        double distance = Math.Abs((px - ax) * dy - (py - ay) * dx) / length;
                        if (distance <= perpendicularLimit) continue;
                    }
                    ink++;
                }
            }
            return ink;
        }

        private static ImageProbe RenderThroughPipeline(IChartTemplate template)
        {
            using (Chart chart = new Chart(new ChartDocument(), template, new ChartStyle()))
            {
                return ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, ProbeDpi, 100));
            }
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }

        private static int SumLeafSpan(IReadOnlyList<ColumnHeaderNode> nodes)
        {
            int sum = 0;
            for (int i = 0; i < nodes.Count; i++) sum += nodes[i].LeafSpan;
            return sum;
        }

        private static bool RectEquals(SKRect a, SKRect b, float eps)
        {
            return Math.Abs(a.Left - b.Left) <= eps && Math.Abs(a.Top - b.Top) <= eps
                && Math.Abs(a.Right - b.Right) <= eps && Math.Abs(a.Bottom - b.Bottom) <= eps;
        }

        private static int ExpectedNodeCountAtLevel(int level)
        {
            switch (level)
            {
                case 0: return 1;
                case 1: return 3;
                case 2: return 6;
                default: return 18;
            }
        }

        private static int IndexByReference(IReadOnlyList<ColumnHeaderNode> list, ColumnHeaderNode node)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], node)) return i;
            }
            return -1;
        }

        private static void CollectAll(ColumnHeaderNode node, List<ColumnHeaderNode> into)
        {
            into.Add(node);
            for (int i = 0; i < node.Children.Count; i++) CollectAll(node.Children[i], into);
        }

        private static string N(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        // ============================================================================ 树构造

        /// <summary>四层均匀树：1 根 / 3 组 / 6 子组 / 18 叶（每组 LeafSpan 6，每子组 3）。</summary>
        private static ColumnHeaderNode UniformTree()
        {
            List<ColumnHeaderNode> groups = new List<ColumnHeaderNode>();
            for (int g = 0; g < 3; g++)
            {
                string gk = "g" + N(g);
                List<ColumnHeaderNode> subs = new List<ColumnHeaderNode>();
                for (int s = 0; s < 2; s++)
                {
                    List<ColumnHeaderNode> leaves = new List<ColumnHeaderNode>();
                    for (int l = 0; l < 3; l++)
                    {
                        leaves.Add(new ColumnHeaderNode(gk + "-s" + N(s) + "-l" + N(l),
                                                        "列" + gk + N(s) + N(l)));
                    }
                    subs.Add(new ColumnHeaderNode(gk + "-s" + N(s), "子组" + N(s), leaves));
                }
                groups.Add(new ColumnHeaderNode(gk, "组" + N(g), subs));
            }
            return new ColumnHeaderNode("hdr", "ICU 记录单表头", groups);
        }

        /// <summary>
        /// 非均匀深度树：根 LeafSpan 6、Depth 3；分支 a 两层、分支 b 一层（浅叶）、分支 c 三层。
        /// 逐层 LeafSpan 之和实测 6 / 6 / 5 / 2。
        /// 对应"心率只一层、实入量三层"的真实护理记录单形态。
        /// </summary>
        private static ColumnHeaderNode RaggedTree()
        {
            ColumnHeaderNode a1 = new ColumnHeaderNode("a1", "甲一");
            ColumnHeaderNode a2 = new ColumnHeaderNode("a2", "甲二");
            ColumnHeaderNode a = new ColumnHeaderNode("a", "甲组", new[] { a1, a2 });
            ColumnHeaderNode b = new ColumnHeaderNode("b", "浅叶");

            ColumnHeaderNode c1x = new ColumnHeaderNode("c1x", "丙一一");
            ColumnHeaderNode c1y = new ColumnHeaderNode("c1y", "丙一二");
            ColumnHeaderNode c1 = new ColumnHeaderNode("c1", "丙一", new[] { c1x, c1y });
            ColumnHeaderNode c2 = new ColumnHeaderNode("c2", "丙二");
            ColumnHeaderNode c = new ColumnHeaderNode("c", "丙组", new[] { c1, c2 });

            return new ColumnHeaderNode("r", "混合深度", new[] { a, b, c });
        }

        /// <summary>五层单链 + 末端两片叶子：Depth 4，节点共 6 个。</summary>
        private static ColumnHeaderNode FiveLevelChain()
        {
            ColumnHeaderNode leaf1 = new ColumnHeaderNode("l1", "叶一");
            ColumnHeaderNode leaf2 = new ColumnHeaderNode("l2", "叶二");
            ColumnHeaderNode level3 = new ColumnHeaderNode("n3", "第四层", new[] { leaf1, leaf2 });
            ColumnHeaderNode level2 = new ColumnHeaderNode("n2", "第三层", new[] { level3 });
            ColumnHeaderNode level1 = new ColumnHeaderNode("n1", "第二层", new[] { level2 });
            return new ColumnHeaderNode("n0", "第一层", new[] { level1 });
        }

        // ============================================================================ 出图模板

        /// <summary>
        /// 最小表头模板：只为把 <see cref="ColumnHeaderNode.Diagonal"/> +
        /// <see cref="HeaderTableLayout"/> + <see cref="ColumnHeaderNode.DiagonalEnds"/>
        /// 接进 <c>Chart.Record → RenderImage → PNG</c> 这条真实链路。
        /// <para>
        /// 版面是四层均匀表头（根 / 两组 / 四子组 / 八叶）。三个格被标成斜线格：叶子 f3（左下→右上）、
        /// 叶子 s1（左上→右下）、分组 blood（左下→右上）。
        /// 构造参数 <c>drawTextInDiagonalCell</c>=false 时按源码语义"斜线格不再写字"，
        /// =true 时故意违反该语义写字，用来证明 false 那一侧的"格内无墨"判据可以被证伪。
        /// </para>
        /// </summary>
        private sealed class DiagonalHeaderTemplate : IChartTemplate
        {
            private static readonly FontKey LabelFont = new FontKey(null, 12f);
            private readonly bool _drawTextInDiagonalCell;
            private readonly ColumnHeaderNode _root;
            private readonly HeaderTableLayout _layout;
            private readonly SKRect _area;

            public DiagonalHeaderTemplate(bool drawTextInDiagonalCell)
            {
                _drawTextInDiagonalCell = drawTextInDiagonalCell;
                _area = new SKRect(ProbeLeft, ProbeTop, ProbeAreaRight, ProbeAreaBottom);
                _root = BuildProbeTree();
                _layout = new HeaderTableLayout(_root, _area, ProbeLevelHeight);
            }

            /// <summary>探针用表头树（斜线标志已置位）。</summary>
            public ColumnHeaderNode Root { get { return _root; } }

            /// <summary>探针用版面（求矩形口径全部来自被测源码）。</summary>
            public HeaderTableLayout Layout { get { return _layout; } }

            public string Key { get { return "test.column-header-diagonal"; } }

            public string DisplayName { get { return "深层分组表头斜线格试版"; } }

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                return new ChartGeometry(
                    new SKRect(0f, 0f, 520f, 160f),
                    new SlotAxis(ProbeLeft, ProbeLeafWidth, ProbeLeafCount, 1),
                    new LinearAxis("rule", 0d, 4d, ProbeTop, ProbeAreaBottom, 1d, 1d),
                    new LinearAxis[0],
                    new Band("header", _area, new BandRow[0]),
                    new Band("date", new SKRect(ProbeLeft, ProbeAreaBottom, ProbeAreaRight, ProbeAreaBottom + 6f), new BandRow[0]),
                    new SKRect(ProbeLeft, ProbeAreaBottom + 6f, ProbeAreaRight, 150f),
                    new LabelColumn[0],
                    new Band("cells", new SKRect(ProbeLeft, ProbeAreaBottom + 6f, ProbeAreaRight, 150f), new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, 150f, 520f, 160f), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                // 落笔整体交给库内的 HeaderRenderer：斜线端点取自 ColumnHeaderNode.DiagonalEnds，
                // "斜线格内不写字"由库保证，测试不再自己复刻这套判断。
                HeaderRenderer.Draw(painter, _layout, LabelFont, SKColors.Black, SKColors.Black,
                                    gridWidth: 1f, diagonalWidth: 1f);

                if (_drawTextInDiagonalCell)
                {
                    // 对照组：绕开库的抑制，把标签硬写进划掉的格子（叶子与分组格都要写，
                    // 否则"斜线外应有墨"的判据自己先失去意义）。
                    for (int level = 0; level <= _root.Depth; level++)
                    {
                        IReadOnlyList<ColumnHeaderNode> nodes = _root.NodesAtLevel(level);
                        for (int i = 0; i < nodes.Count; i++)
                        {
                            ColumnHeaderNode node = nodes[i];
                            if (!node.Diagonal)
                            {
                                continue;
                            }
                            painter.TextInBox(node.Label, _layout.RectOf(node), LabelFont,
                                              SKColors.Black, CellAlign.Center, CellAlign.Center, 1f);
                        }
                    }
                }
            }

            private ColumnHeaderNode BuildProbeTree()
            {
                ColumnHeaderNode f1 = new ColumnHeaderNode("f1", "1");
                ColumnHeaderNode f2 = new ColumnHeaderNode("f2", "2");
                ColumnHeaderNode f3 = new ColumnHeaderNode("f3", "液体种类");
                ColumnHeaderNode fluid = new ColumnHeaderNode("fluid", "液", new[] { f1, f2, f3 });

                ColumnHeaderNode b1 = new ColumnHeaderNode("b1", "输注");
                ColumnHeaderNode b2 = new ColumnHeaderNode("b2", "出血");
                ColumnHeaderNode blood = new ColumnHeaderNode("blood", "红细胞血浆", new[] { b1, b2 });

                ColumnHeaderNode m1 = new ColumnHeaderNode("m1", "ml");
                ColumnHeaderNode machine = new ColumnHeaderNode("machine", "呼吸机", new[] { m1 });

                ColumnHeaderNode s1 = new ColumnHeaderNode("s1", "自主次数");
                ColumnHeaderNode s2 = new ColumnHeaderNode("s2", "次");
                ColumnHeaderNode self = new ColumnHeaderNode("self", "自主", new[] { s1, s2 });

                ColumnHeaderNode g1 = new ColumnHeaderNode("g1", "实入量", new[] { fluid, blood });
                ColumnHeaderNode g2 = new ColumnHeaderNode("g2", "呼吸频率", new[] { machine, self });
                ColumnHeaderNode root = new ColumnHeaderNode("hdr", "ICU 三级表头", new[] { g1, g2 });

                f3.Diagonal = true;                                   // 默认方向＝左下到右上
                s1.Diagonal = true;
                s1.DiagonalDirection = DiagonalDirection.UpperLeftToLowerRight;
                blood.Diagonal = true;
                blood.DiagonalDirection = DiagonalDirection.LowerLeftToUpperRight;
                return root;
            }
        }
    }
}
