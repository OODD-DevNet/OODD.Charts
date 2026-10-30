using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 深层分组表头的落笔器：把 <see cref="HeaderTableLayout"/> 求出的矩形变成图上的格子、
    /// 标签与"划掉"斜线。
    /// <para>
    /// 它是 <see cref="ColumnHeaderNode.Diagonal"/> 这个标志在库内<b>唯一</b>的消费者，
    /// 并把"斜线格内不画文字"从调用侧约定升级为库的行为：划掉的格子里再叠一行标签，
    /// 读表的人分不清那是被划掉的表头还是被划掉的数据。
    /// </para>
    /// </summary>
    public static class HeaderRenderer
    {
        /// <summary>
        /// 绘制整棵表头树（逐层、逐节点）。
        /// </summary>
        /// <param name="painter">落笔器。</param>
        /// <param name="layout">已求解的表头版面（矩形全部来自它，本方法不自算几何）。</param>
        /// <param name="labelFont">标签字号。</param>
        /// <param name="textColor">标签颜色。</param>
        /// <param name="gridColor">格线与斜线颜色。</param>
        /// <param name="gridWidth">格线粗（像素）。</param>
        /// <param name="diagonalWidth">斜线粗（像素）。行业习惯上斜线比格线略粗，单独给一个参数。</param>
        /// <param name="drawFrames">是否画格子边框（只要斜线与文字时传 false）。</param>
        /// <param name="horizontal">标签水平对齐。</param>
        /// <param name="vertical">标签垂直对齐。</param>
        /// <exception cref="ArgumentNullException">painter 或 layout 为 null。</exception>
        public static void Draw(Painter painter, HeaderTableLayout layout, FontKey labelFont,
                                SKColor textColor, SKColor gridColor, float gridWidth = 1f,
                                float diagonalWidth = 1.2f, bool drawFrames = true,
                                CellAlign horizontal = CellAlign.Center, CellAlign vertical = CellAlign.Center)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            ColumnHeaderNode root = layout.Root;
            for (int level = 0; level <= root.Depth; level++)
            {
                IReadOnlyList<ColumnHeaderNode> nodes = root.NodesAtLevel(level);
                for (int i = 0; i < nodes.Count; i++)
                {
                    DrawNode(painter, nodes[i], layout.RectOf(nodes[i]), labelFont, textColor,
                             gridColor, gridWidth, diagonalWidth, drawFrames, horizontal, vertical);
                }
            }
        }

        /// <summary>
        /// 绘制单个格子。<paramref name="node"/> 标了 <see cref="ColumnHeaderNode.Diagonal"/> 时
        /// 只画斜线（必要时先画框），<b>不写标签</b>；其余情况按 <paramref name="node"/> 的
        /// <see cref="ColumnHeaderNode.Label"/> 写字。
        /// </summary>
        public static void DrawNode(Painter painter, ColumnHeaderNode node, SKRect rect, FontKey labelFont,
                                    SKColor textColor, SKColor gridColor, float gridWidth = 1f,
                                    float diagonalWidth = 1.2f, bool drawFrames = true,
                                    CellAlign horizontal = CellAlign.Center, CellAlign vertical = CellAlign.Center)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (drawFrames)
            {
                painter.Frame(rect, gridColor, gridWidth);
            }

            if (node.Diagonal)
            {
                Diagonal(painter, rect, node.DiagonalDirection, gridColor, diagonalWidth);
                return;
            }

            painter.TextInBox(node.Label, rect, labelFont, textColor, horizontal, vertical);
        }

        /// <summary>
        /// 在格子上画"划掉"的斜线。端点取自 <see cref="ColumnHeaderNode.DiagonalEnds"/>，
        /// 与表头树声明的方向同源——绘制层不再自己推端点，否则声明与实际落墨会分叉。
        /// </summary>
        public static void Diagonal(Painter painter, SKRect rect, DiagonalDirection direction,
                                    SKColor color, float width)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }

            SKPoint a;
            SKPoint b;
            ColumnHeaderNode.DiagonalEnds(rect, direction, out a, out b);
            painter.Line(a.X, a.Y, b.X, b.Y, color, width);
        }
    }
}
