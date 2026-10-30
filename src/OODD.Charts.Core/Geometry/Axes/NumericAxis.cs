using System;
using System.Globalization;

namespace OODD.Charts.Geometry
{
    /// <summary>轴的方向：域值增大时像素往哪一端走。</summary>
    public enum AxisDirection
    {
        /// <summary>水平，值向右增大。</summary>
        HorizontalRight = 0,

        /// <summary>垂直，值向上增大（体温、起重量）。</summary>
        VerticalUp = 1,

        /// <summary>水平，值向左增大。</summary>
        HorizontalLeft = 2,

        /// <summary>垂直，值向下增大（测井深度、沉降量）。倒置域轴就是这一种。</summary>
        VerticalDown = 3,
    }

    /// <summary>值到像素的标度。两种都是解析可逆的。</summary>
    public enum AxisScale
    {
        /// <summary>等距。</summary>
        Linear = 0,

        /// <summary>对数。要求域值全为正，刻度标签写真实值而不是幂次。</summary>
        Logarithmic = 1,
    }

    /// <summary>
    /// 通用数值轴：双向 + 可换标度。
    /// <para>
    /// 与既有 <see cref="LinearAxis"/> 的分工是"新旧并行、旧的冻结"：
    /// <see cref="LinearAxis"/> 继续服务三测单与产程图（版面已被实测断言钉住），
    /// 本类型服务新增表单，因为它要支持横向与对数坐标。
    /// </para>
    /// </summary>
    public sealed class NumericAxis
    {
        private readonly double _forwardMin;
        private readonly double _forwardMax;

        /// <param name="key">轴标识。</param>
        /// <param name="min">域值下限。</param>
        /// <param name="max">域值上限，必须大于 min。</param>
        /// <param name="from">像素起点，不要求小于 to。</param>
        /// <param name="to">像素终点。</param>
        /// <param name="direction">值增大方向；与 from/to 的先后无关，只是语义标注，
        /// 真正的映射由标度函数的单调性与 from→to 决定。</param>
        /// <param name="scale">标度。</param>
        public NumericAxis(string key, double min, double max, float from, float to,
                           AxisDirection direction, AxisScale scale = AxisScale.Linear)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("轴必须有 key。", nameof(key));
            }
            if (!(max > min))
            {
                throw new ArgumentException("轴上限必须大于下限：" + key + " [" + min + "," + max + "]", nameof(max));
            }
            if (scale == AxisScale.Logarithmic && min <= 0d)
            {
                throw new ArgumentException("对数轴的域值必须全为正：" + key + " min=" + min, nameof(min));
            }

            Key = key;
            Min = min;
            Max = max;
            From = from;
            To = to;
            Direction = direction;
            Scale = scale;

            _forwardMin = ForwardRaw(min);
            _forwardMax = ForwardRaw(max);
        }

        public string Key { get; }
        public double Min { get; }
        public double Max { get; }
        public float From { get; }
        public float To { get; }
        public AxisDirection Direction { get; }
        public AxisScale Scale { get; }

        /// <summary>像素长度（恒为非负）。</summary>
        public float Length => Math.Abs(To - From);

        /// <summary>值→像素。值先夹到 [Min,Max]，所以图外的点不会飞到轴外面去。</summary>
        public float PositionOf(double value)
        {
            double t = Normalized(value);
            return (float)(From + t * (To - From));
        }

        /// <summary>
        /// 值→像素，<b>不夹紧</b>：域外的值落在轴的像素区间之外，位置忠实于标度。
        /// <para>
        /// <see cref="PositionOf"/> 的夹紧是"投点不许飞出轴外"的语义，适合画一个数据点；
        /// 但裁剪/求交这类几何运算需要的是"这条线真正的走向"，夹紧会让图外的线段贴在轴边上，
        /// 斜线因此在边界处被折弯（<c>LineFamilyGenerator.PixelsOf</c> 的"完全图外→空列表"
        /// 承诺就依赖这个方法）。
        /// </para>
        /// <para>
        /// 标度在该值处<b>没有定义</b>时（对数轴的 ≤0、概率轴的 (0,1) 之外）无法忠实外推，
        /// 退化为按该端点处的局部像素密度线性外推：越界多远就按比例飞多远，方向与轴一致，
        /// 不会再被压回边上。截距的精度只影响轴外那一段，而轴外本来就要被裁掉。
        /// </para>
        /// </summary>
        public float PositionOfExtended(double value)
        {
            if (value >= Min && value <= Max)
            {
                return PositionOf(value);
            }

            double span = _forwardMax - _forwardMin;
            if (Math.Abs(span) >= 1e-15 && IsScaleDefined(value))
            {
                double t = (ForwardRaw(value) - _forwardMin) / span;
                return (float)(From + t * (To - From));
            }

            // 端点处的局部密度：用域内相邻两点做差商。必须在 double 空间算完再落回 float——
            // 像素是 float，300 附近的分辨率只有 3e-5，先用 PositionOf 相减会把差商吃成 0。
            bool below = value < Min;
            double edge = below ? Min : Max;
            double step = Math.Max(1e-9, Math.Abs(edge) * 1e-6);
            double inside = below ? edge + step : edge - step;
            double spanPixels = (double)To - From;
            double pixelEdge = (double)From + Normalized(edge) * spanPixels;
            double pixelInside = (double)From + Normalized(inside) * spanPixels;
            double slope = (pixelEdge - pixelInside) / (edge - inside);
            return (float)(pixelEdge + slope * (value - edge));
        }

        /// <summary>标度函数在该值处是否有定义（对数轴要正数）。</summary>
        private bool IsScaleDefined(double value)
        {
            switch (Scale)
            {
                case AxisScale.Logarithmic:
                    return value > 0d;
                default:
                    return true;
            }
        }

        /// <summary>像素→值，<see cref="PositionOf"/> 的解析逆。</summary>
        public double ValueAt(float position)
        {
            double span = To - From;
            double t = Math.Abs(span) < 1e-9 ? 0d : (position - From) / span;
            if (t < 0d)
            {
                t = 0d;
            }
            else if (t > 1d)
            {
                t = 1d;
            }

            return Inverse(t);
        }

        /// <summary>该值处的局部像素密度：每单位值占多少像素。对数轴与概率轴上处处不同，
        /// 所以任何"按轴长度换算字号/线宽"的代码都必须问这个而不是问 <see cref="PixelsPerUnit"/>。</summary>
        public float PixelsPerUnitAt(double value)
        {
            // 差商必须在 double 空间算完再落回 float。像素是 float：900px 的轴上
            // 两点像素差只有 3e-5，先用 PositionOf 相减会被 float 精度吃成 0，
            // 密度于是报 0、调用方换算出 ∞——本条真实踩过。
            double span = (double)To - From;
            double delta = Math.Max(1e-9, Math.Abs(value) * 1e-6);
            double lo = Clamp(value - delta);
            double hi = Clamp(value + delta);
            double dValue = hi - lo;
            if (dValue <= 0d)
            {
                return 0f;
            }

            double dPosition = (Normalized(hi) - Normalized(lo)) * span;
            return (float)(Math.Abs(dPosition) / dValue);
        }

        /// <summary>均匀标度下的像素密度（对数/概率轴请用 <see cref="PixelsPerUnitAt"/>）。</summary>
        public float PixelsPerUnit => (Max - Min) <= 0d ? 0f : Length / (float)(Max - Min);

        /// <summary>值是否落在轴的可画范围内（含端点）。</summary>
        public bool Accepts(double value)
        {
            return value >= Min - Tolerance && value <= Max + Tolerance;
        }

        /// <summary>把值夹进域内。</summary>
        public double Clamp(double value)
        {
            return value < Min ? Min : value > Max ? Max : value;
        }

        /// <summary>数值格式化统一走不变文化：中文区域会把小数点变成逗号，
        /// 那会让 net462 与 net8.0 的导出内容不再逐字节一致。</summary>
        public static string Format(double value, string format = "0.###")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private const double Tolerance = 1e-9;

        private double Normalized(double value)
        {
            double clamped = Clamp(value);
            double f = ForwardRaw(clamped);
            double span = _forwardMax - _forwardMin;
            return Math.Abs(span) < 1e-15 ? 0d : (f - _forwardMin) / span;
        }

        private double ForwardRaw(double value)
        {
            switch (Scale)
            {
                case AxisScale.Logarithmic:
                    return Math.Log(value);
                default:
                    return value;
            }
        }

        private double Inverse(double t)
        {
            double f = _forwardMin + t * (_forwardMax - _forwardMin);
            switch (Scale)
            {
                case AxisScale.Logarithmic:
                    return Math.Exp(f);
                default:
                    return f;
            }
        }
    }
}
