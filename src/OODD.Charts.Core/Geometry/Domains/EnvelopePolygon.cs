using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 闭合多边形约束域（飞机重心包线、互感器误差限值折线、大卫三角形分区）。
    /// <para>
    /// <see cref="Contains(double, double)"/> 的边界语义按 docs/06 §3.2 <b>先定后测</b>：
    /// 顶点和边上的点一律算<b>内</b>。理由是这类判定回答的是"是否合规"，
    /// 边界属于合规侧；把它算成外会让恰好压在包线边界上的配载被判为超限。
    /// </para>
    /// </summary>
    public sealed class EnvelopePolygon
    {
        private readonly double[] _xs;
        private readonly double[] _ys;

        public EnvelopePolygon(string key, string label, IReadOnlyList<SKPoint> vertices, bool closed = true)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("域必须有 key。", nameof(key));
            }
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            // 重复的首尾点（画闭合环时常见）去掉，否则末段退化成零长边，
            // 射线法会在该点上给出错误结论。
            List<SKPoint> list = new List<SKPoint>(vertices);
            if (list.Count > 1 && Same(list[0], list[list.Count - 1]))
            {
                list.RemoveAt(list.Count - 1);
            }

            if (list.Count < 3 && closed)
            {
                throw new ArgumentException("闭合域至少需要 3 个不重复顶点，实到 " + list.Count, nameof(vertices));
            }
            if (list.Count < 2)
            {
                throw new ArgumentException("折线至少需要 2 个顶点。", nameof(vertices));
            }

            Key = key;
            Label = label ?? string.Empty;
            Closed = closed;
            Vertices = list;

            _xs = new double[list.Count];
            _ys = new double[list.Count];
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < list.Count; i++)
            {
                _xs[i] = list[i].X;
                _ys[i] = list[i].Y;
                minX = Math.Min(minX, list[i].X);
                minY = Math.Min(minY, list[i].Y);
                maxX = Math.Max(maxX, list[i].X);
                maxY = Math.Max(maxY, list[i].Y);
            }

            Bounds = new SKRect(minX, minY, maxX, maxY);
        }

        public string Key { get; }
        public string Label { get; }

        /// <summary>false 表示这是一条限值折线而不是域，此时 <see cref="Contains(double, double)"/> 恒为 false。</summary>
        public bool Closed { get; }

        public IReadOnlyList<SKPoint> Vertices { get; }

        /// <summary>顶点外接矩形（数据坐标，不是像素）。</summary>
        public SKRect Bounds { get; }

        public int VertexCount => Vertices.Count;

        /// <summary>顶点和边上的点都算在内（见类型注释）。</summary>
        public bool Contains(double x, double y)
        {
            if (!Closed)
            {
                return false;
            }

            return PolygonHit.Contains(_xs, _ys, x, y);
        }

        public bool Contains(SKPoint p) => Contains(p.X, p.Y);

        private static bool Same(SKPoint a, SKPoint b)
        {
            return Math.Abs(a.X - b.X) < 1e-6f && Math.Abs(a.Y - b.Y) < 1e-6f;
        }
    }

    /// <summary>
    /// 多边形命中判定的共享实现。顶点顺序（顺时针/逆时针）不影响判定结果，
    /// 边上/顶点上的点一律算在内。
    /// </summary>
    internal static class PolygonHit
    {
        /// <summary>先查"在不在边上"，再用射线法查内部。顶点顺序（顺时针/逆时针）不影响结果。</summary>
        internal static bool Contains(double[] xs, double[] ys, double x, double y)
        {
            int n = xs.Length;
            if (n < 3)
            {
                return false;
            }

            double scale = Scale(xs, ys);
            double eps = 1e-9 * scale;

            if (OnBoundary(xs, ys, x, y, eps))
            {
                return true;
            }

            // 标准奇偶射线法：从 (x,y) 向 +x 方向数穿越次数。
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double yi = ys[i], yj = ys[j];
                if ((yi > y) != (yj > y))
                {
                    double xi = xs[i], xj = xs[j];
                    double xAt = xi + (y - yi) / (yj - yi) * (xj - xi);
                    if (x < xAt)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        private static bool OnBoundary(double[] xs, double[] ys, double x, double y, double eps)
        {
            int n = xs.Length;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (OnSegment(xs[j], ys[j], xs[i], ys[i], x, y, eps))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool OnSegment(double ax, double ay, double bx, double by,
                                      double px, double py, double eps)
        {
            double vx = bx - ax, vy = by - ay;
            double wx = px - ax, wy = py - ay;

            double lenSq = vx * vx + vy * vy;
            if (lenSq <= eps * eps)
            {
                return Math.Abs(wx) <= eps && Math.Abs(wy) <= eps;
            }

            double cross = vx * wy - vy * wx;
            if (Math.Abs(cross) > eps * Math.Sqrt(lenSq))
            {
                return false;
            }

            double t = (wx * vx + wy * vy) / lenSq;
            return t >= -eps && t <= 1d + eps;
        }

        private static double Scale(double[] xs, double[] ys)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            for (int i = 0; i < xs.Length; i++)
            {
                if (xs[i] < minX) { minX = xs[i]; }
                if (xs[i] > maxX) { maxX = xs[i]; }
                if (ys[i] < minY) { minY = ys[i]; }
                if (ys[i] > maxY) { maxY = ys[i]; }
            }

            double span = Math.Max(maxX - minX, maxY - minY);
            double largest = Math.Max(Math.Abs(minX), Math.Abs(minY));
            return Math.Max(Math.Max(span, largest), 1d);
        }
    }
}
