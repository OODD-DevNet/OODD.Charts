using System;
using System.Globalization;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 三角坐标的三条边。起点/终点按 A→B→C 的顺序定义，用于三边各自反向的刻度落位。
    /// </summary>
    public enum TriaxialEdge
    {
        /// <summary>起点顶点 A，终点顶点 B。</summary>
        AB = 0,
        /// <summary>起点顶点 B，终点顶点 C。</summary>
        BC = 1,
        /// <summary>起点顶点 C，终点顶点 A。</summary>
        CA = 2,
    }

    /// <summary>
    /// 三组分。只承载数值，不做归一化也不做区间校验，因此可以直接来自用户输入或文档数据。
    /// </summary>
    public readonly struct TriaxialComponents
    {
        private const string ComponentFormat = "0.###";

        /// <summary>按给定的三个组分值构造。</summary>
        /// <param name="a">组分 a（对应 <see cref="TriaxialMap.VertexA"/>）。</param>
        /// <param name="b">组分 b（对应 <see cref="TriaxialMap.VertexB"/>）。</param>
        /// <param name="c">组分 c（对应 <see cref="TriaxialMap.VertexC"/>）。</param>
        public TriaxialComponents(double a, double b, double c)
        {
            A = a;
            B = b;
            C = c;
        }

        /// <summary>组分 a。</summary>
        public double A { get; }

        /// <summary>组分 b。</summary>
        public double B { get; }

        /// <summary>组分 c。</summary>
        public double C { get; }

        /// <summary>不变文化格式化，形如 "0.3/0.3/0.4"，用于断言与调试输出。</summary>
        public override string ToString()
        {
            return A.ToString(ComponentFormat, CultureInfo.InvariantCulture) + "/"
                 + B.ToString(ComponentFormat, CultureInfo.InvariantCulture) + "/"
                 + C.ToString(ComponentFormat, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 三组分（三角坐标）与页面像素之间的双向映射。顶点顺序 A-B-C 逆时针或顺时针均可，
    /// 内部按同一约定解释 a/b/c，因此调用方只要保持一致即可。
    ///
    /// 定位方式是面积坐标（重心坐标）：P = (a·A + b·B + c·C) / (a+b+c)，
    /// 即把未归一的三组分按比例归一后作为三个顶点的权重做凸组合。
    /// 这是笛卡尔（<see cref="LinearAxis"/>）与极坐标之外的第三类坐标家族，
    /// 被土壤质地三角图与变压器油色谱大卫三角形两个域共用。
    ///
    /// 全程用双精度运算，只在输出 <see cref="SKPoint"/> 时降为 float，
    /// 因此 <see cref="ComponentsAt"/> 与 <see cref="PointOf"/> 的往返误差由 float 的位宽决定，
    /// 与运行时的 TFM 无关（net462 与 net8.0 结果逐字节一致）。
    /// </summary>
    public sealed class TriaxialMap
    {
        /// <summary>
        /// 统一浮点比较容差。所有判等/判零都用它，绝不使用 == 比较浮点，
        /// 以保证退化判定与往返一致性在不同运行时上完全确定。
        /// </summary>
        private const double Epsilon = 1e-9;

        /// <summary>组分格式化串，供异常消息使用（不变文化，保证消息可比对）。</summary>
        private const string NumberFormat = "0.#####";

        private readonly double _ax;
        private readonly double _ay;
        private readonly double _bx;
        private readonly double _by;
        private readonly double _cx;
        private readonly double _cy;

        /// <summary>以 A 为原点的基向量 AB、AC，逆映射的 2×2 系数矩阵。</summary>
        private readonly double _abx;
        private readonly double _aby;
        private readonly double _acx;
        private readonly double _acy;

        /// <summary>叉积 AB × AC。符号只反映顶点旋向，绝对值非零即三角形不退化。</summary>
        private readonly double _determinant;

        /// <summary>|AB|·|AC|，把叉积折算成夹角正弦用的尺度，使退化判定与图纸尺寸无关。</summary>
        private readonly double _crossScale;

        /// <summary>
        /// 用三个几何顶点建立映射。
        /// </summary>
        /// <param name="vertexA">组分 a 的顶点。</param>
        /// <param name="vertexB">组分 b 的顶点。</param>
        /// <param name="vertexC">组分 c 的顶点。</param>
        /// <exception cref="ArgumentException">
        /// 顶点坐标非有限，或三点共线（含两点重合）时抛出。
        /// 共线判据用叉积相对 |AB|·|AC| 的比值（即夹角正弦）与 <see cref="Epsilon"/> 比较，
        /// 因此与三角形的绝对尺寸无关。
        /// </exception>
        public TriaxialMap(SKPoint vertexA, SKPoint vertexB, SKPoint vertexC)
        {
            if (!IsFinite(vertexA) || !IsFinite(vertexB) || !IsFinite(vertexC))
            {
                throw new ArgumentException(
                    "三角坐标的顶点坐标必须是有限值：A(" + Describe(vertexA) + ") B(" + Describe(vertexB)
                    + ") C(" + Describe(vertexC) + ")");
            }

            VertexA = vertexA;
            VertexB = vertexB;
            VertexC = vertexC;

            _ax = vertexA.X;
            _ay = vertexA.Y;
            _bx = vertexB.X;
            _by = vertexB.Y;
            _cx = vertexC.X;
            _cy = vertexC.Y;

            _abx = _bx - _ax;
            _aby = _by - _ay;
            _acx = _cx - _ax;
            _acy = _cy - _ay;

            _determinant = _abx * _acy - _aby * _acx;
            _crossScale = Math.Sqrt(_abx * _abx + _aby * _aby) * Math.Sqrt(_acx * _acx + _acy * _acy);

            // _crossScale 为 0 表示有顶点重合，此时判据 0 <= Epsilon·0 同样成立。
            if (Math.Abs(_determinant) <= Epsilon * _crossScale)
            {
                throw new ArgumentException(
                    "三角坐标的三个顶点共线，无法建立映射：A(" + Describe(vertexA) + ") B("
                    + Describe(vertexB) + ") C(" + Describe(vertexC) + ")");
            }
        }

        /// <summary>组分 a 的几何顶点（96dpi 逻辑像素）。</summary>
        public SKPoint VertexA { get; }

        /// <summary>组分 b 的几何顶点。</summary>
        public SKPoint VertexB { get; }

        /// <summary>组分 c 的几何顶点。</summary>
        public SKPoint VertexC { get; }

        /// <summary>
        /// 组分 a/b/c（大小写均可）对应的顶点，便于模板写标签落位。
        /// </summary>
        /// <param name="component">组分标识 'A'、'B'、'C'。</param>
        /// <exception cref="ArgumentException">不是 A/B/C 时抛出。</exception>
        public SKPoint VertexOf(char component)
        {
            switch (char.ToUpperInvariant(component))
            {
                case 'A': return VertexA;
                case 'B': return VertexB;
                case 'C': return VertexC;
                default:
                    throw new ArgumentException("未知的三组分标识，只接受 A/B/C：" + component, nameof(component));
            }
        }

        /// <summary>
        /// 三组分 → 像素点。输入不要求已归一：按 (a+b+c) 比例归一后定位。
        /// 例如 (30, 40, 30) 与 (0.3, 0.4, 0.3) 落在同一像素点上。
        /// </summary>
        /// <param name="a">组分 a 的份额（非负才有三角域内意义，本方法不校验符号）。</param>
        /// <param name="b">组分 b 的份额。</param>
        /// <param name="c">组分 c 的份额。</param>
        /// <exception cref="ArgumentException">三者和 ≤ 0（含数值上退化为 0 的极小正值、NaN、无穷）时抛出：无法定义重心坐标。</exception>
        public SKPoint PointOf(double a, double b, double c)
        {
            double sum = a + b + c;
            if (!IsFinite(sum) || sum <= Epsilon)
            {
                throw new ArgumentException(
                    "三组分之和必须为正，无法归一化重心坐标：a=" + Num(a) + ", b=" + Num(b) + ", c=" + Num(c));
            }

            return new SKPoint(
                (float)((a * _ax + b * _bx + c * _cx) / sum),
                (float)((a * _ay + b * _by + c * _cy) / sum));
        }

        /// <summary>
        /// 三组分是否都在 [0,1] 且和为正，即点确实落在三角域内。
        /// 边界值按 <see cref="Epsilon"/> 容差算作在内（0 与 1 是合法组分）。
        /// 注意本方法只看组分本身，与具体三角形尺寸无关，故与 <see cref="PointOf"/> 的抛异常条件一致。
        /// </summary>
        public bool IsInside(double a, double b, double c)
        {
            return a >= -Epsilon && a <= 1d + Epsilon
                && b >= -Epsilon && b <= 1d + Epsilon
                && c >= -Epsilon && c <= 1d + Epsilon
                && a + b + c > Epsilon;
        }

        /// <summary>
        /// 像素点 → 三组分（逆算）。以 A 为原点、AB 与 AC 为基向量解 2×2 线性方程组，
        /// 返回值已归一化，三个分量之和为 1。
        ///
        /// 契约：<c>ComponentsAt(PointOf(a, b, c))</c> 的每个分量误差 &lt; 1e-6（三角图投点选点的前提）。
        /// 该精度随三角形"扁"的程度按 1/sin(最小夹角) 放大，极端扁三角形上达不到；
        /// 退化判定已在构造函数中按同一容差完成，正常的三相图顶点不会触发。
        /// </summary>
        /// <param name="point">页面像素点。落在三角形外部时返回的组分可能为负或大于 1。</param>
        /// <exception cref="InvalidOperationException">三个顶点共线（退化三角形）时抛出。构造已拦截该情形，此处为防御性复检。</exception>
        public TriaxialComponents ComponentsAt(SKPoint point)
        {
            if (Math.Abs(_determinant) <= Epsilon * _crossScale)
            {
                throw new InvalidOperationException(
                    "三角坐标的三个顶点共线，无法逆算组分：A(" + Describe(VertexA) + ") B("
                    + Describe(VertexB) + ") C(" + Describe(VertexC) + ")");
            }

            double dx = point.X - _ax;
            double dy = point.Y - _ay;

            // b = 沿 AB 方向的比例，c = 沿 AC 方向的比例，a 由归一条件补出。
            double componentB = (dx * _acy - dy * _acx) / _determinant;
            double componentC = (_abx * dy - _aby * dx) / _determinant;
            double componentA = 1d - componentB - componentC;

            double sum = componentA + componentB + componentC;
            if (!IsFinite(sum) || sum <= Epsilon)
            {
                // 只可能出现在输入点为非有限像素值时，此时重心坐标无定义。
                throw new ArgumentException(
                    "输入像素点无法逆算三组分，坐标必须是有限值：(" + Describe(point) + ")", nameof(point));
            }

            return new TriaxialComponents(componentA / sum, componentB / sum, componentC / sum);
        }

        /// <summary>
        /// 某一条边上的刻度位置：t=0 在该边起点顶点，t=1 在终点顶点。
        /// t 超出 [0,1] 不报错，按同一直线外推（用于把刻度线画到框外的场合）。
        /// </summary>
        /// <param name="edge">三角边。</param>
        /// <param name="t">边上的相对位置。</param>
        /// <exception cref="ArgumentException">edge 不是 <see cref="TriaxialEdge"/> 的合法值时抛出。</exception>
        public SKPoint PointOnEdge(TriaxialEdge edge, double t)
        {
            double startX;
            double startY;
            double endX;
            double endY;

            switch (edge)
            {
                case TriaxialEdge.AB:
                    startX = _ax; startY = _ay; endX = _bx; endY = _by;
                    break;
                case TriaxialEdge.BC:
                    startX = _bx; startY = _by; endX = _cx; endY = _cy;
                    break;
                case TriaxialEdge.CA:
                    startX = _cx; startY = _cy; endX = _ax; endY = _ay;
                    break;
                default:
                    throw new ArgumentException("未知的三角边：" + edge, nameof(edge));
            }

            return new SKPoint((float)(startX + t * (endX - startX)), (float)(startY + t * (endY - startY)));
        }

        /// <summary>
        /// 三角域内一条"等值线"（例如黏粒 30% 的线）的两个端点，用于画分区边界网格。
        /// 该线是"指定组分固定为 value、其余两组分之和为 1-value"的直线段，
        /// 端点即未归一重心坐标 (value, 1-value, 0) 与 (value, 0, 1-value)。
        ///
        /// 端点顺序约定：剩余两个组分中，标识字母较小者先取满 1-value（'A' → 先 (v,1-v,0) 后 (v,0,1-v)）。
        /// value 超出 [0,1] 不报错，端点会落在三角域外（仍按同一重心式计算）。
        /// </summary>
        /// <param name="component">固定值的组分标识 'A'、'B'、'C'（大小写均可）。</param>
        /// <param name="value">该组分的归一化份额，如 0.3 表示 30%。</param>
        /// <returns>长度为 2 的端点数组。</returns>
        /// <exception cref="ArgumentException">component 不是 A/B/C，或 value 非有限时抛出。</exception>
        public SKPoint[] IsoLineOf(char component, double value)
        {
            if (!IsFinite(value))
            {
                throw new ArgumentException("等值线的组分值必须是有限值：" + Num(value), nameof(value));
            }

            double rest = 1d - value;

            switch (char.ToUpperInvariant(component))
            {
                case 'A':
                    return new[] { PointOf(value, rest, 0d), PointOf(value, 0d, rest) };
                case 'B':
                    return new[] { PointOf(rest, value, 0d), PointOf(0d, value, rest) };
                case 'C':
                    return new[] { PointOf(rest, 0d, value), PointOf(0d, rest, value) };
                default:
                    throw new ArgumentException("未知的三组分标识，只接受 A/B/C：" + component, nameof(component));
            }
        }

        // ---------------------------------------------------------------- 内部工具

        private static bool IsFinite(SKPoint point)
        {
            return !float.IsNaN(point.X) && !float.IsInfinity(point.X)
                && !float.IsNaN(point.Y) && !float.IsInfinity(point.Y);
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static string Num(double value)
        {
            return value.ToString(NumberFormat, CultureInfo.InvariantCulture);
        }

        private static string Describe(SKPoint point)
        {
            return Num(point.X) + ", " + Num(point.Y);
        }
    }
}
