using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 一族线：由"给定族参数 → 一串域坐标采样点"的方程，加上该族参数的取值列表构成。
    ///
    /// 气象探空 T-lnP 图上的五族线（等温线、等压线、干绝热线、湿绝热线、等饱和比湿线）
    /// 就是五个 <see cref="LineFamily"/>。每个采样点是 <c>{ x, y }</c> 两点（长度必须为 2），
    /// 语义由使用该族的具体图决定（T-lnP 里是 { 温度℃, 气压 hPa }）。
    ///
    /// 采样密度是调用方的责任，库里不做插值补点，也不做任何数值求解。
    /// </summary>
    public sealed class LineFamily
    {
        /// <summary>单个采样点的坐标分量个数：{ x, y }。</summary>
        private const int SampleDimension = 2;

        private readonly Func<double, IReadOnlyList<double[]>> _resolver;

        /// <summary>构造一族线，并在构造时就校验全部族参数对应的采样点形状。</summary>
        /// <param name="key">族标识，如 "isobar"、"isotherm"。同一生成器内必须唯一。</param>
        /// <param name="name">族中文名，供图例，如 "等压线"。</param>
        /// <param name="memberValues">族参数列表，如 [1000, 850, 700, 500, 300]。</param>
        /// <param name="resolver">对某个族参数，给出该线在域坐标上的一串采样点（T-lnP：x=温度℃，y=气压 hPa）。
        /// 采样密度由调用方决定，库里不做插值。必须是纯函数：构造时会针对每个族参数试用一次，
        /// 之后每次取点都会重新调用。</param>
        /// <exception cref="ArgumentException">key/name 为空、族参数列表为空，或某个采样点数组长度不是
        /// <see cref="SampleDimension"/>、含非有限值时抛出（消息含族 key 与下标）。</exception>
        public LineFamily(string key, string name, IReadOnlyList<double> memberValues,
                          Func<double, IReadOnlyList<double[]>> resolver)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("线族标识不能为空。", nameof(key));
            }
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("线族名称不能为空：" + key, nameof(name));
            }
            if (memberValues == null)
            {
                throw new ArgumentNullException(nameof(memberValues));
            }
            if (memberValues.Count == 0)
            {
                throw new ArgumentException("线族至少要有一个族参数：" + key, nameof(memberValues));
            }
            if (resolver == null)
            {
                throw new ArgumentNullException(nameof(resolver));
            }

            Key = key;
            Name = name;

            List<double> values = new List<double>(memberValues.Count);
            for (int i = 0; i < memberValues.Count; i++)
            {
                double value = memberValues[i];
                if (double.IsNaN(value) || double.IsInfinity(value))
                {
                    throw new ArgumentException(
                        "线族 " + key + " 的第 " + i + " 个族参数不是有限值：" + Num(value), nameof(memberValues));
                }
                values.Add(value);
            }

            MemberValues = values;
            _resolver = resolver;

            // 构造期试用：让"采样点形状不合法"在装配阶段就暴露，而不是拖到绘制时。
            for (int i = 0; i < values.Count; i++)
            {
                Validate(values[i], _resolver(values[i]), nameof(resolver));
            }
        }

        /// <summary>族标识，供 <see cref="LineFamilyGenerator"/> 按键取族。</summary>
        public string Key { get; }

        /// <summary>族中文名，供图例。</summary>
        public string Name { get; }

        /// <summary>族参数个数。</summary>
        public int MemberCount => MemberValues.Count;

        /// <summary>族参数列表（构造时给的顺序）。</summary>
        public IReadOnlyList<double> MemberValues { get; }

        /// <summary>
        /// 某个族参数对应的域坐标采样点。每次调用都会重新执行 <c>resolver</c>，本类型不缓存。
        /// </summary>
        /// <param name="memberValue">族参数值。</param>
        /// <exception cref="ArgumentException">resolver 返回空引用，或某个采样点数组不是
        /// { x, y } 两点、含非有限值时抛出（消息含族 key 与下标）。</exception>
        public IReadOnlyList<double[]> SamplesOf(double memberValue)
        {
            IReadOnlyList<double[]> samples = _resolver(memberValue);
            Validate(memberValue, samples, nameof(memberValue));
            return samples;
        }

        private void Validate(double memberValue, IReadOnlyList<double[]> samples, string paramName)
        {
            if (samples == null)
            {
                throw new ArgumentException(
                    "线族 " + Key + " 在族参数 " + Num(memberValue) + " 处的方程返回空引用。", paramName);
            }

            for (int i = 0; i < samples.Count; i++)
            {
                double[] sample = samples[i];
                string where = "线族 " + Key + " 族参数 " + Num(memberValue) + " 的第 " + i + " 个采样点";

                if (sample == null)
                {
                    throw new ArgumentException(where + " 为 null，必须是 { x, y } 两点。", paramName);
                }
                if (sample.Length != SampleDimension)
                {
                    throw new ArgumentException(
                        where + " 长度为 " + sample.Length + "，必须是 { x, y } 两点。", paramName);
                }
                if (double.IsNaN(sample[0]) || double.IsInfinity(sample[0])
                    || double.IsNaN(sample[1]) || double.IsInfinity(sample[1]))
                {
                    throw new ArgumentException(
                        where + " 含非有限值：{ " + Num(sample[0]) + ", " + Num(sample[1]) + " }。", paramName);
                }
            }
        }

        private static string Num(double value)
        {
            return value.ToString("0.#####", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 把若干 <see cref="LineFamily"/> 落到像素：域坐标由两条 <see cref="NumericAxis"/> 给定，
    /// 因此"族与族斜交"是映射的自然结果，不需要任何旋转。
    ///
    /// 约定 domainX 是横向轴、domainY 是纵向轴；
    /// 两轴的 <c>Direction</c> 只影响值→像素的方向，不参与裁剪矩形计算。
    /// 除 <see cref="DrawFamily"/> 之外全部是纯函数，不缓存任何中间结果。
    /// </summary>
    public sealed class LineFamilyGenerator
    {
        /// <summary>
        /// 浮点比较容差（并行/退化判定）。所有判等都用显式容差而不是 ==，
        /// 以保证 net462 与 net8.0 上交点判定结果逐字节一致。
        /// </summary>
        private const double Epsilon = 1e-9;

        /// <summary>裁剪与求交包含边界用的像素容差。</summary>
        private const double PixelTolerance = 1e-6;

        private readonly NumericAxis _domainX;
        private readonly NumericAxis _domainY;
        private readonly List<LineFamily> _families;
        private readonly Dictionary<string, LineFamily> _byKey;
        private readonly int _maxSamplesPerLine;

        /// <summary>建立线族像素生成器。</summary>
        /// <param name="domainX">域横轴（T-lnP：温度轴）。</param>
        /// <param name="domainY">域纵轴（T-lnP：气压轴，对数刻度使等压线等距）。</param>
        /// <param name="families">参与绘制的线族集合，族标识不得重复。</param>
        /// <param name="maxSamplesPerLine">单条线的采样点上限，仅做保护性截断（超出取前 N 个丢弃其余）。
        /// 采样密度是调用方的责任，库不做插值补点。</param>
        /// <exception cref="ArgumentException">families 为空引用/为空集合/族标识重复时抛出。</exception>
        /// <exception cref="ArgumentOutOfRangeException">maxSamplesPerLine 非正时抛出。</exception>
        public LineFamilyGenerator(NumericAxis domainX, NumericAxis domainY, IEnumerable<LineFamily> families,
                                   int maxSamplesPerLine)
        {
            _domainX = domainX ?? throw new ArgumentNullException(nameof(domainX));
            _domainY = domainY ?? throw new ArgumentNullException(nameof(domainY));
            if (families == null)
            {
                throw new ArgumentNullException(nameof(families));
            }
            if (maxSamplesPerLine <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxSamplesPerLine), "单条线的采样点上限必须为正：" + maxSamplesPerLine);
            }

            _maxSamplesPerLine = maxSamplesPerLine;
            _families = new List<LineFamily>();
            _byKey = new Dictionary<string, LineFamily>(StringComparer.Ordinal);

            foreach (LineFamily family in families)
            {
                if (family == null)
                {
                    throw new ArgumentException("线族集合中存在 null 元素。", nameof(families));
                }
                if (_byKey.ContainsKey(family.Key))
                {
                    throw new ArgumentException("线族标识重复：" + family.Key, nameof(families));
                }
                _families.Add(family);
                _byKey[family.Key] = family;
            }

            if (_families.Count == 0)
            {
                throw new ArgumentException("至少需要一族线才能建立线族生成器。", nameof(families));
            }
        }

        /// <summary>参与绘制的线族，按构造给定顺序。</summary>
        public IReadOnlyList<LineFamily> Families => _families;

        /// <summary>绘图区矩形：两轴像素区间（<c>From</c>..<c>To</c>）的交叠，与轴方向无关。</summary>
        public SKRect ClipRect
        {
            get
            {
                float left = Math.Min(_domainX.From, _domainX.To);
                float right = Math.Max(_domainX.From, _domainX.To);
                float top = Math.Min(_domainY.From, _domainY.To);
                float bottom = Math.Max(_domainY.From, _domainY.To);
                return new SKRect(left, top, right, bottom);
            }
        }

        /// <summary>按族标识取族，不存在抛 <see cref="KeyNotFoundException"/>。</summary>
        public LineFamily FamilyOf(string familyKey)
        {
            LineFamily? family;
            if (familyKey != null && _byKey.TryGetValue(familyKey, out family))
            {
                return family;
            }
            throw new KeyNotFoundException("未登记的线族：" + familyKey);
        }

        /// <summary>按族标识取族，不存在返回 false。</summary>
        public bool TryFamily(string familyKey, out LineFamily family)
        {
            LineFamily? found;
            if (familyKey != null && _byKey.TryGetValue(familyKey, out found))
            {
                family = found;
                return true;
            }
            family = null!;
            return false;
        }

        /// <summary>
        /// 某族某参数的线的像素点列：先按域轴映射成像素（<see cref="NumericAxis.PositionOfExtended"/>，
        /// <b>不夹紧</b>），再逐段与 <see cref="ClipRect"/> 求交（Liang–Barsky 参数裁剪），
        /// 越界部分被切掉而不是把线画到轴外。
        /// 返回空列表表示该线完全在图外；只含一个点表示截断后仅剩一个落在图内的采样点
        /// （画不出线，<see cref="DrawFamily"/> 会跳过）。
        /// <para>
        /// 用不夹紧的映射是有原因的：轴的 <c>PositionOf</c> 会把域外值压回轴边，
        /// 那样"完全在图外"永远裁不出空列表，斜线还会在边界处被折弯成贴边线段。
        /// </para>
        ///
        /// 一条线若多次进出绘图区，会输出多段可见点列并首尾相接平铺；
        /// 由于裁剪矩形是凸的，拼接弦也落在矩形内，不会把线画到轴外，
        /// 只会多出一段图内连线。需要严格断开时请自行按段调用 <see cref="Painter.Polyline"/>。
        /// </summary>
        /// <param name="familyKey">族标识。</param>
        /// <param name="memberValue">族参数值。任意有限值都接受：一族线由 resolver（族参数→点列的纯函数）
        /// 定义，画一条没登记过的等值线是合法用法；<see cref="DrawFamily"/> 走的是构造时给的
        /// <see cref="LineFamily.MemberValues"/>，正常路径不会画到没登记的族参数。</param>
        /// <exception cref="KeyNotFoundException">族标识未登记时抛出。</exception>
        public IReadOnlyList<SKPoint> PixelsOf(string familyKey, double memberValue)
        {
            LineFamily family = FamilyOf(familyKey);
            IReadOnlyList<double[]> samples = family.SamplesOf(memberValue);

            // 保护性截断：取前 N 个采样点，丢弃其余。库不补点。
            int count = samples.Count < _maxSamplesPerLine ? samples.Count : _maxSamplesPerLine;
            List<SKPoint> result = new List<SKPoint>(count * 2);
            if (count == 0)
            {
                return result;
            }

            double[] xs = new double[count];
            double[] ys = new double[count];
            for (int i = 0; i < count; i++)
            {
                xs[i] = _domainX.PositionOfExtended(samples[i][0]);
                ys[i] = _domainY.PositionOfExtended(samples[i][1]);
            }

            SKRect clip = ClipRect;

            if (count == 1)
            {
                if (IsInsideRect(xs[0], ys[0], clip))
                {
                    result.Add(new SKPoint((float)xs[0], (float)ys[0]));
                }
                return result;
            }

            bool hasLast = false;
            double lastX = 0d;
            double lastY = 0d;

            for (int i = 0; i + 1 < count; i++)
            {
                double x0 = xs[i];
                double y0 = ys[i];
                double x1 = xs[i + 1];
                double y1 = ys[i + 1];

                double t0;
                double t1;
                if (!TryClipSegment(x0, y0, x1, y1, clip, out t0, out t1))
                {
                    continue;
                }

                double startX = x0 + t0 * (x1 - x0);
                double startY = y0 + t0 * (y1 - y0);
                double endX = x0 + t1 * (x1 - x0);
                double endY = y0 + t1 * (y1 - y0);

                // 新可见段：起点与上一段终点不重合时补一个起笔点。
                if (!hasLast || Math.Abs(startX - lastX) > PixelTolerance || Math.Abs(startY - lastY) > PixelTolerance)
                {
                    result.Add(new SKPoint((float)startX, (float)startY));
                }

                if (Math.Abs(endX - startX) > PixelTolerance || Math.Abs(endY - startY) > PixelTolerance)
                {
                    result.Add(new SKPoint((float)endX, (float)endY));
                    lastX = endX;
                    lastY = endY;
                }
                else
                {
                    lastX = startX;
                    lastY = startY;
                }
                hasLast = true;
            }

            return result;
        }

        /// <summary>
        /// 两族两条线的交点像素：对各自裁剪后的像素折线逐段线性求交，取第一个交点
        /// （按 A 的段序号、再按 B 的段序号）。无交点返回 null；平行或共线/重合段也返回 null，不抛异常。
        ///
        /// 用途是 T-lnP 的解析校验：等压线与等温线的交点必须落在 (温度, 气压) 的解析解上，
        /// 从而让绘图落点可被断言而不是目视。因为求交用的是裁剪后的点列，图外交点不会被返回。
        /// </summary>
        /// <param name="familyKeyA">第一条线的族标识。</param>
        /// <param name="memberA">第一条线的族参数。</param>
        /// <param name="familyKeyB">第二条线的族标识。</param>
        /// <param name="memberB">第二条线的族参数。</param>
        /// <exception cref="KeyNotFoundException">任一族标识未登记时抛出。</exception>
        public SKPoint? Intersect(string familyKeyA, double memberA, string familyKeyB, double memberB)
        {
            IReadOnlyList<SKPoint> lineA = PixelsOf(familyKeyA, memberA);
            IReadOnlyList<SKPoint> lineB = PixelsOf(familyKeyB, memberB);

            for (int i = 0; i + 1 < lineA.Count; i++)
            {
                for (int j = 0; j + 1 < lineB.Count; j++)
                {
                    double x;
                    double y;
                    if (TryCross(lineA[i], lineA[i + 1], lineB[j], lineB[j + 1], out x, out y))
                    {
                        return new SKPoint((float)x, (float)y);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 把某族全部线画出来。dashed 为真用虚线（湿绝热线常用虚线）。完全在图外的线跳过。
        /// </summary>
        /// <param name="painter">落笔的绘制器。</param>
        /// <param name="familyKey">族标识。</param>
        /// <param name="color">线色。</param>
        /// <param name="width">线宽（像素）。</param>
        /// <param name="dashed">是否虚线。</param>
        /// <exception cref="ArgumentNullException">painter 为空引用时抛出。</exception>
        /// <exception cref="KeyNotFoundException">族标识未登记时抛出。</exception>
        public void DrawFamily(Painter painter, string familyKey, SKColor color, float width, bool dashed)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }

            LineFamily family = FamilyOf(familyKey);
            for (int i = 0; i < family.MemberValues.Count; i++)
            {
                IReadOnlyList<SKPoint> pixels = PixelsOf(family.Key, family.MemberValues[i]);
                if (pixels.Count >= 2)
                {
                    painter.Polyline(pixels, color, width, dashed);
                }
            }
        }

        // ---------------------------------------------------------------- 裁剪与求交

        /// <summary>点是否落在裁剪矩形内（含边界，用 <see cref="PixelTolerance"/> 容差）。</summary>
        private static bool IsInsideRect(double x, double y, SKRect rect)
        {
            return x >= rect.Left - PixelTolerance && x <= rect.Right + PixelTolerance
                && y >= rect.Top - PixelTolerance && y <= rect.Bottom + PixelTolerance;
        }

        /// <summary>
        /// Liang–Barsky：线段与矩形的可见参数区间 [t0,t1]（相对 p1→p2）。
        /// 与边平行且在该半平面外时返回 false；恰好贴在边界上的线段判为可见。
        /// </summary>
        private static bool TryClipSegment(double x0, double y0, double x1, double y1, SKRect rect,
                                           out double t0, out double t1)
        {
            t0 = 0d;
            t1 = 1d;

            double dx = x1 - x0;
            double dy = y1 - y0;

            if (!Slab(-dx, x0 - rect.Left, ref t0, ref t1)) return false;
            if (!Slab(dx, rect.Right - x0, ref t0, ref t1)) return false;
            if (!Slab(-dy, y0 - rect.Top, ref t0, ref t1)) return false;
            if (!Slab(dy, rect.Bottom - y0, ref t0, ref t1)) return false;

            return t0 <= t1 + PixelTolerance;
        }

        /// <summary>单个半平面的裁剪区间收窄。</summary>
        private static bool Slab(double p, double q, ref double t0, ref double t1)
        {
            if (Math.Abs(p) <= Epsilon)
            {
                // 与边界平行：在本侧内则整段保留，否则整段丢弃。q 为 0 表示贴边，判为在内。
                return q >= -PixelTolerance;
            }

            double r = q / p;
            if (p < 0d)
            {
                if (r > t1 + PixelTolerance)
                {
                    return false;
                }
                if (r > t0)
                {
                    t0 = r;
                }
            }
            else
            {
                if (r < t0 - PixelTolerance)
                {
                    return false;
                }
                if (r < t1)
                {
                    t1 = r;
                }
            }

            return true;
        }

        /// <summary>
        /// 两线段是否相交。用叉积行列式判平行/共线：行列式绝对值 ≤ <see cref="Epsilon"/> 时
        /// 视为平行或共线，按契约返回 false（重合段不抛异常、不返回"整段"）。
        /// </summary>
        private static bool TryCross(SKPoint a0, SKPoint a1, SKPoint b0, SKPoint b1, out double x, out double y)
        {
            x = 0d;
            y = 0d;

            double p = a1.X - a0.X;
            double q = a1.Y - a0.Y;
            double r = b1.X - b0.X;
            double s = b1.Y - b0.Y;

            double determinant = p * s - q * r;
            if (Math.Abs(determinant) <= Epsilon)
            {
                return false;
            }

            double dx = b0.X - a0.X;
            double dy = b0.Y - a0.Y;

            double t = (dx * s - dy * r) / determinant;
            double u = (dx * q - dy * p) / determinant;

            if (t < -PixelTolerance || t > 1d + PixelTolerance
                || u < -PixelTolerance || u > 1d + PixelTolerance)
            {
                return false;
            }

            x = a0.X + t * p;
            y = a0.Y + t * q;
            return true;
        }
    }
}
