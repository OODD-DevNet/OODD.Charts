using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>对角斜线的方向（表头格里划掉"本专科不适用"的那一列时用）。</summary>
    public enum DiagonalDirection
    {
        /// <summary>左下 → 右上。</summary>
        LowerLeftToUpperRight,

        /// <summary>左上 → 右下。</summary>
        UpperLeftToLowerRight,
    }

    /// <summary>列表头树的一个节点。叶子 = 一列；父节点横跨其全部后代叶子。</summary>
    /// <remarks>
    /// ICU 护理记录单这类表头是任意深度的嵌套分组（"实入量" → "1~5 液体种类"；
    /// "呼吸频率" → "呼吸机"/"自主"），本库既有的 <see cref="SlotAxis"/> 只能表达"分组 → 子格"一层，
    /// 故新建本类型而不是改 <see cref="SlotAxis"/>（已验收版面冻结）。
    /// 本类型是纯结构：不碰像素、不画任何东西。
    /// </remarks>
    public sealed class ColumnHeaderNode
    {
        private readonly int _depth;
        private readonly int _leafSpan;

        /// <summary>构造一个节点。子节点在此刻被挂到本节点上（写 <see cref="Parent"/> 与 <see cref="IndexInParent"/>）。</summary>
        /// <param name="key">稳定标识，供数据行按 key 取值。</param>
        /// <param name="label">表头显示文字（可能很长，绘制层负责换行/缩字号）。</param>
        /// <param name="children">子节点；空集合即叶子。<b>null 不被当作空集合</b>——
        /// 传 null 通常是把"这一层还没想好"写进了代码，静默退化成叶子会让表头少一整层。</param>
        public ColumnHeaderNode(string key, string label, IEnumerable<ColumnHeaderNode> children)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0) throw new ArgumentException("节点 key 不能为空。", nameof(key));
            if (children == null) throw new ArgumentNullException(nameof(children),
                "节点 \"" + key + "\" 的 children 传了 null。要做叶子请传空集合或用一个不带 children 的重载。");

            Key = key;
            Label = label ?? string.Empty;

            List<ColumnHeaderNode> list = new List<ColumnHeaderNode>();
            foreach (ColumnHeaderNode child in children)
            {
                if (child == null)
                {
                    throw new ArgumentException("节点 \"" + key + "\" 的子节点集合里有 null 元素。", nameof(children));
                }
                if (child._parent != null && !ReferenceEquals(child._parent, this))
                {
                    throw new ArgumentException(
                        "子节点 \"" + child.Key + "\" 已挂在父节点 \"" + child._parent.Key + "\" 下，不能同时属于两个父节点。",
                        nameof(children));
                }

                // 同一父节点下 key 必须唯一：数据行是按 key 取值的，重复了就只能取到第一个。
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Key, child.Key, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "父节点 \"" + key + "\" 下出现重复的子节点 key \"" + child.Key + "\"（第 " + i + " 与第 "
                            + list.Count + " 个子节点）。", nameof(children));
                    }
                }

                child._parent = this;
                child._indexInParent = list.Count;
                list.Add(child);
            }

            Children = list.AsReadOnly();

            // 树自底向上构造，子节点的 Depth/LeafSpan 此处已定型，可以直接算并存下来。
            if (list.Count == 0)
            {
                _depth = 0;
                _leafSpan = 1;
            }
            else
            {
                int maxChildDepth = 0;
                int span = 0;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i]._depth > maxChildDepth) maxChildDepth = list[i]._depth;
                    span += list[i]._leafSpan;
                }
                _depth = maxChildDepth + 1;
                _leafSpan = span;
            }
        }

        /// <summary>构造一个叶子（无子节点）。</summary>
        /// <param name="key">稳定标识。</param>
        /// <param name="label">表头显示文字。</param>
        public ColumnHeaderNode(string key, string label)
            : this(key, label, new ColumnHeaderNode[0])
        {
        }

        private ColumnHeaderNode? _parent;
        private int _indexInParent;

        /// <summary>稳定标识（同一父节点下 Ordinal 唯一；不同分组下允许重名）。</summary>
        public string Key { get; }

        /// <summary>表头显示文字。</summary>
        public string Label { get; }

        /// <summary>子节点，从左到右；空即叶子。</summary>
        public IReadOnlyList<ColumnHeaderNode> Children { get; }

        /// <summary>无子节点即叶子（= 一列）。</summary>
        public bool IsLeaf { get { return Children.Count == 0; } }

        /// <summary>深度：叶子为 0，父节点 = 子节点最大深度 + 1。</summary>
        public int Depth { get { return _depth; } }

        /// <summary>该节点覆盖的叶子列数。叶子为 1。</summary>
        public int LeafSpan { get { return _leafSpan; } }

        /// <summary>父节点引用（未挂进树时为 null）。</summary>
        public ColumnHeaderNode? Parent { get { return _parent; } }

        /// <summary>同一父节点下的序号（未挂进树时为 0）。</summary>
        public int IndexInParent { get { return _indexInParent; } }

        /// <summary>该节点是否用对角斜线划掉（"本专科不适用"）。仅是标志位，画不画由绘制层决定。</summary>
        public bool Diagonal { get; set; }

        /// <summary>斜线方向。</summary>
        public DiagonalDirection DiagonalDirection { get; set; }

        /// <summary>叶子列宽权重（默认 1）；不等宽列靠它。非叶子节点上的该值不参与版面求解。</summary>
        public double LeafWeight
        {
            get { return _leafWeight; }
            set
            {
                // !(value >= 0) 同时挡住负数与 NaN。
                if (!(value >= 0d) || double.IsInfinity(value))
                {
                    throw new ArgumentOutOfRangeException(nameof(LeafWeight),
                        "节点 \"" + Key + "\" 的列宽权重必须是不小于 0 的有限数，实际为 "
                        + value.ToString(CultureInfo.InvariantCulture) + "。");
                }
                _leafWeight = value;
            }
        }

        private double _leafWeight = 1d;

        /// <summary>树的最大层数（与 <see cref="Depth"/> 同值，供按层试探的模板用）。</summary>
        public int MaxLevel { get { return _depth; } }

        /// <summary>按 key 在子树里找节点（Ordinal，深度优先先序），找不到返回 null。</summary>
        public ColumnHeaderNode? Find(string key)
        {
            if (key == null || key.Length == 0) return null;
            if (string.Equals(Key, key, StringComparison.Ordinal)) return this;
            for (int i = 0; i < Children.Count; i++)
            {
                ColumnHeaderNode? found = Children[i].Find(key);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>深度优先展开全部叶子，顺序即列序。</summary>
        public IReadOnlyList<ColumnHeaderNode> Leaves()
        {
            List<ColumnHeaderNode> leaves = new List<ColumnHeaderNode>(_leafSpan);
            CollectLeaves(leaves);
            return leaves.AsReadOnly();
        }

        private void CollectLeaves(List<ColumnHeaderNode> into)
        {
            if (Children.Count == 0)
            {
                into.Add(this);
                return;
            }
            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].CollectLeaves(into);
            }
        }

        /// <summary>
        /// 某一层（level=0 为本节点所在层，即"根层"）的全部节点，从左到右。
        /// 越界的 level 返回空列表而不抛——模板会按最大深度试探。
        /// <para>
        /// <b>层号不等于行号</b>：当 <see cref="HeaderTableLayout.LeavesToBottom"/> 为 true 时，
        /// 叶子一律沉到最后一行，所以第 1 层的叶子其矩形落在第 <c>LevelCount-1</c> 行。
        /// 按层遍历（本方法 + <see cref="HeaderTableLayout.LevelRect"/>）只适合画层间分隔线；
        /// 要格子位置一律问 <c>HeaderTableLayout.RectOf</c>。
        /// </para>
        /// </summary>
        public IReadOnlyList<ColumnHeaderNode> NodesAtLevel(int level)
        {
            List<ColumnHeaderNode> current = new List<ColumnHeaderNode>();
            current.Add(this);
            for (int i = 0; i < level; i++)
            {
                List<ColumnHeaderNode> next = new List<ColumnHeaderNode>();
                for (int j = 0; j < current.Count; j++)
                {
                    IReadOnlyList<ColumnHeaderNode> kids = current[j].Children;
                    for (int k = 0; k < kids.Count; k++) next.Add(kids[k]);
                }
                current = next;
                if (current.Count == 0) break;
            }
            return current.AsReadOnly();
        }

        /// <summary>
        /// 对角斜线在矩形上的两个端点（纯几何，不画）。绘制层拿到端点后自行 <c>Painter.Line</c>。
        /// </summary>
        /// <param name="rect">要划掉的格子矩形。</param>
        /// <param name="direction">斜线方向。</param>
        /// <param name="a">端点 A。</param>
        /// <param name="b">端点 B。</param>
        public static void DiagonalEnds(SKRect rect, DiagonalDirection direction, out SKPoint a, out SKPoint b)
        {
            if (direction == DiagonalDirection.LowerLeftToUpperRight)
            {
                a = new SKPoint(rect.Left, rect.Bottom);
                b = new SKPoint(rect.Right, rect.Top);
            }
            else
            {
                a = new SKPoint(rect.Left, rect.Top);
                b = new SKPoint(rect.Right, rect.Bottom);
            }
        }
    }

    /// <summary>把一棵 <see cref="ColumnHeaderNode"/> 树落到具体像素矩形上。</summary>
    public sealed class HeaderTableLayout
    {
        /// <summary>浮点比较容差（浮点比较一律用显式容差，不用 ==）。</summary>
        private const float PixelEpsilon = 0.0001f;

        private const double WeightEpsilon = 1e-9;

        private readonly Dictionary<ColumnHeaderNode, SKRect> _rects;
        private readonly Dictionary<ColumnHeaderNode, int> _firstLeaf;
        private readonly float[] _bounds;

        /// <summary>区域高与各层行之和相符时末层下边界取区域下缘（见构造函数注释）。</summary>
        private readonly bool _snapBottom;

        /// <summary>把表头树铺到给定区域：按叶子权重分列宽，按层分表头行高，一次算好全部节点矩形。</summary>
        /// <param name="root">虚拟根（其 LeafSpan 即总列数）。</param>
        /// <param name="area">表头区域矩形（宽=全部列宽，高=各层行之和）。</param>
        /// <param name="levelHeight">每层表头行高。</param>
        /// <param name="leavesToBottom">
        /// true = 叶子一律沉到最底层（分组表里"心率"这种单层叶子与"呼吸机/自主"这种二层叶子同高，
        /// 这是护理记录单的常见版面）；false = 节点就按自己距根层的行数摆放。层数少的组会腾出下方空白，由绘制层合并。
        /// </param>
        public HeaderTableLayout(ColumnHeaderNode root, SKRect area, float levelHeight, bool leavesToBottom = false)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            // !(x > eps) 挡 NaN，Infinity 要单独挡：∞ 行高会让 RequiredHeight=∞，
            // 而第 0 行的 top = Area.Top + 0×∞ = NaN，整棵树的矩形一起变成 NaN。
            if (!(levelHeight > PixelEpsilon) || float.IsInfinity(levelHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(levelHeight),
                    "表头行高必须是大于 0 的有限数，实际为 " + levelHeight.ToString(CultureInfo.InvariantCulture) + "。");
            }
            // !(x > eps) 同时挡住 NaN。
            if (!(area.Width > PixelEpsilon))
            {
                throw new ArgumentException(
                    "表头区域宽度必须大于 0（树共 " + root.LeafSpan + " 列），实际为 "
                    + area.Width.ToString(CultureInfo.InvariantCulture) + "。", nameof(area));
            }

            Root = root;
            Area = area;
            LevelHeight = levelHeight;
            LeavesToBottom = leavesToBottom;

            List<ColumnHeaderNode> leaves = new List<ColumnHeaderNode>(root.Leaves());
            LeafCount = leaves.Count;
            LevelCount = root.Depth + 1;
            RequiredHeight = (float)(LevelCount * (double)levelHeight);

            // 列边界交给 CellGeometry：与"数据行跨列合并格"共用同一条 double 累加 + 末缘直取右缘的口径。
            double totalWeight = 0d;
            for (int i = 0; i < leaves.Count; i++) totalWeight += leaves[i].LeafWeight;
            if (!(totalWeight > WeightEpsilon))
            {
                throw new ArgumentException(
                    "表头 \"" + root.Key + "\" 的全部叶子列宽权重之和必须大于 0，实际为 "
                    + totalWeight.ToString(CultureInfo.InvariantCulture) + "。");
            }

            double[] weights = new double[LeafCount];
            for (int i = 0; i < LeafCount; i++) weights[i] = leaves[i].LeafWeight;
            _bounds = CellGeometry.ColumnBounds(area.Left, area.Right, weights);

            // 区域高恰好等于各层行之和时，末层下边界同样直接取区域下缘（同上，防累加漂移）。
            _snapBottom = Math.Abs((double)area.Height - (double)RequiredHeight) <= PixelEpsilon;

            // 键是节点实例本身：同一 key 允许出现在不同分组下，只能按引用区分。
            // ColumnHeaderNode 不重写 Equals/GetHashCode，故默认比较器就是引用相等。
            _rects = new Dictionary<ColumnHeaderNode, SKRect>();
            _firstLeaf = new Dictionary<ColumnHeaderNode, int>();
            Assign(root, 0, 0);

            LeafBounds = _bounds;
        }

        private int Assign(ColumnHeaderNode node, int level, int firstLeaf)
        {
            int leafIndex = firstLeaf;
            for (int i = 0; i < node.Children.Count; i++)
            {
                leafIndex = Assign(node.Children[i], level + 1, leafIndex);
            }

            _firstLeaf[node] = firstLeaf;

            int row = (LeavesToBottom && node.IsLeaf && !ReferenceEquals(node, Root)) ? LevelCount - 1 : level;
            float top = (float)((double)Area.Top + row * (double)LevelHeight);
            float bottom = (_snapBottom && row == LevelCount - 1)
                ? Area.Bottom
                : (float)((double)top + (double)LevelHeight);
            _rects[node] = CellGeometry.RectOf(_bounds, firstLeaf, node.LeafSpan, top, bottom);

            return firstLeaf + node.LeafSpan;
        }

        /// <summary>虚拟根。</summary>
        public ColumnHeaderNode Root { get; }

        /// <summary>表头区域矩形（= 构造时传入的 area）。</summary>
        public SKRect Area { get; }

        /// <summary>表头层数（= 根深度 + 1）。</summary>
        public int LevelCount { get; }

        /// <summary>每层行高。</summary>
        public float LevelHeight { get; }

        /// <summary>叶子是否沉到最底层。</summary>
        public bool LeavesToBottom { get; }

        /// <summary>各层行之和（<see cref="LevelCount"/> × <see cref="LevelHeight"/>），供版面求解器核对区域高。</summary>
        public float RequiredHeight { get; }

        /// <summary>叶子列数。</summary>
        public int LeafCount { get; }

        /// <summary>叶子列的分界 x 数组，长度 = 叶子数 + 1；用于与数据行共用列网格。</summary>
        public IReadOnlyList<float> LeafBounds { get; }

        /// <summary>某节点的矩形（跨其全部叶子列）。不在树内的节点抛 KeyNotFoundException（消息含 key）。</summary>
        public SKRect RectOf(ColumnHeaderNode node)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            SKRect rect;
            if (_rects.TryGetValue(node, out rect)) return rect;
            throw new KeyNotFoundException("表头节点 " + node.Key + " 不属于本布局的表头树。");
        }

        /// <summary>按 key 取节点矩形（深度优先第一个匹配）。找不到抛 KeyNotFoundException（消息含 key）。</summary>
        public SKRect RectOf(string key)
        {
            return RectOf(FindNode(key));
        }

        /// <summary>按 key 取节点本身；找不到抛 KeyNotFoundException（消息含 key）。</summary>
        public ColumnHeaderNode FindNode(string key)
        {
            ColumnHeaderNode? node = Root.Find(key);
            if (node == null)
            {
                throw new KeyNotFoundException("表头树 " + Root.Key + " 内不存在节点 " + key + "。");
            }
            return node;
        }

        /// <summary>第 level 层的行矩形（横跨全宽，供画层间分隔线）。</summary>
        public SKRect LevelRect(int level)
        {
            if (level < 0 || level >= LevelCount)
            {
                throw new ArgumentOutOfRangeException(nameof(level),
                    "层序号 " + level + " 越界（共 " + LevelCount + " 层）。");
            }
            float top = (float)((double)Area.Top + level * (double)LevelHeight);
            float bottom = (_snapBottom && level == LevelCount - 1)
                ? Area.Bottom
                : (float)((double)top + (double)LevelHeight);
            return new SKRect(Area.Left, top, Area.Right, bottom);
        }

        /// <summary>第 index 列（叶子序号）的矩形，纵向给 <paramref name="top"/>/<paramref name="bottom"/>。</summary>
        public SKRect ColumnRect(int index, float top, float bottom)
        {
            if (index < 0 || index >= LeafCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "列序号 " + index + " 越界（共 " + LeafCount + " 列）。");
            }
            return new SKRect(_bounds[index], top, _bounds[index + 1], bottom);
        }

        /// <summary>某叶子在列网格中的列序号；节点不是本树内的叶子时抛 KeyNotFoundException。</summary>
        public int LeafIndexOf(ColumnHeaderNode leaf)
        {
            if (leaf == null) throw new ArgumentNullException(nameof(leaf));
            int index;
            if (_firstLeaf.TryGetValue(leaf, out index) && leaf.IsLeaf) return index;
            throw new KeyNotFoundException("节点 " + leaf.Key + " 不是本布局的叶子列。");
        }
    }

    /// <summary>主表旁挂一张行距独立的子表（ICU 记录单右侧的"交班小结"）。</summary>
    public sealed class SideAttachedTable
    {
        /// <summary>浮点比较容差（浮点比较一律用显式容差，不用 ==）。</summary>
        private const float PixelEpsilon = 0.0001f;

        private float _width;

        /// <summary>构造一张行距独立的旁挂子表。</summary>
        /// <param name="key">标识。</param>
        /// <param name="rowLabels">行标签，顺序即行序。</param>
        /// <param name="rowHeight">该子表自己的行高——与主表行高无关，独立行距就是它存在的理由。</param>
        /// <param name="gap">与主表之间的间隔。</param>
        public SideAttachedTable(string key, IEnumerable<string> rowLabels, float rowHeight, float gap)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0) throw new ArgumentException("子表 key 不能为空。", nameof(key));
            if (rowLabels == null) throw new ArgumentNullException(nameof(rowLabels));
            if (!(rowHeight > PixelEpsilon))
            {
                throw new ArgumentOutOfRangeException(nameof(rowHeight),
                    "子表 \"" + key + "\" 的行高必须大于 0，实际为 " + rowHeight.ToString(CultureInfo.InvariantCulture) + "。");
            }
            if (!(gap >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(gap),
                    "子表 \"" + key + "\" 与主表的间隔不能为负，实际为 " + gap.ToString(CultureInfo.InvariantCulture) + "。");
            }

            Key = key;
            RowHeight = rowHeight;
            Gap = gap;

            List<string> labels = new List<string>();
            foreach (string label in rowLabels)
            {
                // null 标签当作空串行处理：留白行是合法版面。
                labels.Add(label ?? string.Empty);
            }
            RowLabels = labels.AsReadOnly();
            RowCount = labels.Count;
            Height = (float)(RowCount * (double)rowHeight);
        }

        /// <summary>子表标识。</summary>
        public string Key { get; }

        /// <summary>行标签，顺序即行序。</summary>
        public IReadOnlyList<string> RowLabels { get; }

        /// <summary>本子表自己的行高（与主表行高无关）。</summary>
        public float RowHeight { get; }

        /// <summary>与主表的间隔。</summary>
        public float Gap { get; }

        /// <summary>行数（= RowLabels.Count）。</summary>
        public int RowCount { get; }

        /// <summary>子表总高（= RowCount × RowHeight，不含 gap）。</summary>
        public float Height { get; }

        /// <summary>子表宽，由版面求解后回填；未回填（小于等于 0）时 <see cref="PlaceAt"/> 无法工作。</summary>
        public float Width
        {
            get { return _width; }
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(Width),
                        "子表 \"" + Key + "\" 的宽必须是不小于 0 的有限数，实际为 "
                        + value.ToString(CultureInfo.InvariantCulture) + "。");
                }
                _width = value;
            }
        }

        /// <summary>贴到主表右缘：<paramref name="mainRight"/> 是主表右边界 x，<paramref name="top"/> 是起始 y。</summary>
        public SKRect PlaceAt(float mainRight, float top)
        {
            if (!(Width > PixelEpsilon))
            {
                throw new InvalidOperationException(
                    "子表 \"" + Key + "\" 宽未定，先求解版面并回填 Width 再定位。");
            }
            float left = mainRight + Gap;
            return new SKRect(left, top, left + Width, top + Height);
        }

        /// <summary>
        /// 第 index 行在已定位矩形 <paramref name="placed"/> 内的行矩形。
        /// 行 y 用 double 累加后转 float；<b>最末一行的下边界直接取 <paramref name="placed"/> 的下缘</b>，
        /// 否则 0.1 这类行高逐行累加的误差会传导到末行，让最下一行与子表下缘差半个像素。
        /// </summary>
        public SKRect RowRect(int index, SKRect placed)
        {
            if (RowCount == 0)
            {
                throw new InvalidOperationException("子表 \"" + Key + "\" 没有行，无从取行矩形。");
            }
            if (index < 0 || index >= RowCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "子表 \"" + Key + "\" 行序号 " + index + " 越界（共 " + RowCount + " 行）。");
            }

            double top = (double)placed.Top + index * (double)RowHeight;
            double bottom = index == RowCount - 1
                ? (double)placed.Bottom
                : top + (double)RowHeight;
            return new SKRect(placed.Left, (float)top, placed.Right, (float)bottom);
        }

        /// <summary>某行右侧再挂的小计列矩形（交班小结常有"合计"列）。列宽在行内等分。</summary>
        public SKRect RowCellRect(int index, SKRect placed, int columnIndex, int columnCount)
        {
            if (columnCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(columnCount),
                    "子表 \"" + Key + "\" 的行内列数必须大于 0，实际为 " + columnCount + "。");
            }
            SKRect row = RowRect(index, placed);
            if (columnIndex < 0 || columnIndex >= columnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(columnIndex),
                    "子表 \"" + Key + "\" 的行内列序号 " + columnIndex + " 越界（共 " + columnCount + " 列）。");
            }

            // 同 RowRect 的规则：double 按比例求边界，最右一列的右边界直接取行右缘。
            double width = (double)row.Width;
            double left = (double)row.Left + width * ((double)columnIndex / columnCount);
            double right = columnIndex == columnCount - 1
                ? (double)row.Right
                : (double)row.Left + width * ((double)(columnIndex + 1) / columnCount);
            return new SKRect((float)left, row.Top, (float)right, row.Bottom);
        }
    }

    /// <summary>派生单元格链：格与格之间存在固定推导关系（血透记录单的 8 格体重链：
    /// 上次透后体重→透前体重→增加量→干体重→较干体重增加量→净脱水量→透后体重→本次下降量）。
    /// 注意：本类型只固化"谁是谁的来源"这一版面关系，不做任何计算——数值一律由文档传入。</summary>
    public sealed class DerivedCellChain
    {
        private readonly Dictionary<string, DerivedCell> _byKey;

        /// <summary>构造一条派生格链（只登记顺序与引用关系，不做计算）。</summary>
        /// <param name="key">链标识。</param>
        /// <param name="cells">链内各格，顺序即链序（绘制层按序画引用箭头）。</param>
        public DerivedCellChain(string key, IEnumerable<DerivedCell> cells)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0) throw new ArgumentException("派生链 key 不能为空。", nameof(key));
            if (cells == null) throw new ArgumentNullException(nameof(cells));

            Key = key;
            List<DerivedCell> list = new List<DerivedCell>();
            foreach (DerivedCell cell in cells)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (string.Equals(list[i].Key, cell.Key, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "派生链 \"" + key + "\" 内出现重复的格 key \"" + cell.Key + "\"。", nameof(cells));
                    }
                }
                list.Add(cell);
            }

            Cells = list.AsReadOnly();
            _byKey = new Dictionary<string, DerivedCell>(StringComparer.Ordinal);
            for (int i = 0; i < list.Count; i++) _byKey[list[i].Key] = list[i];
        }

        /// <summary>链标识。</summary>
        public string Key { get; }

        /// <summary>链内各格，顺序即链序。</summary>
        public IReadOnlyList<DerivedCell> Cells { get; }

        /// <summary>格数。</summary>
        public int Count { get { return Cells.Count; } }

        /// <summary>按格 key 取格；不存在返回 null。</summary>
        public DerivedCell? Find(string cellKey)
        {
            if (cellKey == null || cellKey.Length == 0) return null;
            DerivedCell found;
            return _byKey.TryGetValue(cellKey, out found) ? found : (DerivedCell?)null;
        }

        /// <summary>按格 key 取格序号（供绘制层排箭头）；不存在返回 -1。</summary>
        public int IndexOf(string cellKey)
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                if (string.Equals(Cells[i].Key, cellKey, StringComparison.Ordinal)) return i;
            }
            return -1;
        }
    }

    /// <summary>派生链里的一格。</summary>
    public readonly struct DerivedCell
    {
        private static readonly IReadOnlyList<string> EmptySources = new List<string>().AsReadOnly();

        /// <summary>构造链中的一格。</summary>
        /// <param name="key">本格标识。</param>
        /// <param name="label">格标签。</param>
        /// <param name="sourceKeys">版面上标注的取值来源格（可空数组=无来源）。绘制时画引用箭头用。</param>
        /// <param name="unit">单位后缀，可为空串。</param>
        public DerivedCell(string key, string label, IReadOnlyList<string> sourceKeys, string unit)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0) throw new ArgumentException("派生格 key 不能为空。", nameof(key));

            Key = key;
            Label = label ?? string.Empty;
            Unit = unit ?? string.Empty;

            if (sourceKeys == null)
            {
                SourceKeys = EmptySources;
                return;
            }
            List<string> sources = new List<string>(sourceKeys.Count);
            for (int i = 0; i < sourceKeys.Count; i++)
            {
                string source = sourceKeys[i];
                if (source == null)
                {
                    throw new ArgumentException(
                        "派生格 \"" + key + "\" 的 sourceKeys 第 " + i + " 项为 null。", nameof(sourceKeys));
                }
                if (source.Length == 0)
                {
                    throw new ArgumentException(
                        "派生格 \"" + key + "\" 的 sourceKeys 第 " + i + " 项为空串。", nameof(sourceKeys));
                }
                sources.Add(source);
            }
            SourceKeys = sources.AsReadOnly();
        }

        /// <summary>本格标识。</summary>
        public string Key { get; }

        /// <summary>格标签。</summary>
        public string Label { get; }

        /// <summary>版面上标注的取值来源格（空即无来源）。</summary>
        public IReadOnlyList<string> SourceKeys { get; }

        /// <summary>单位后缀，可为空串。</summary>
        public string Unit { get; }

        /// <summary>来源引用是否全部指向链内已有的格（版面自检：链尾指向不存在的格就是数据错了）。</summary>
        public bool HasAllSourcesIn(DerivedCellChain chain)
        {
            if (chain == null) return SourceKeys.Count == 0;
            for (int i = 0; i < SourceKeys.Count; i++)
            {
                if (chain.Find(SourceKeys[i]) == null) return false;
            }
            return true;
        }
    }
}
