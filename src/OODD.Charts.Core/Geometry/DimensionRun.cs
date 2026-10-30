using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>尺寸线端部符号。GB 机械图用实心箭头，建筑图与测点常用圆点/斜线，
    /// 四种都是"端部怎么收笔"，与量值无关。</summary>
    public enum ArrowKind
    {
        /// <summary>实心闭合箭头（机械制图默认）。</summary>
        Filled = 0,

        /// <summary>空心开口箭头（建筑图常用）。</summary>
        Open = 1,

        /// <summary>小圆点（测量/建筑细部）。</summary>
        Dot = 2,

        /// <summary>45° 斜刻线（建筑总图、链式中间段）。</summary>
        Slash = 3,
    }

    /// <summary>尺寸标注的族别：决定端部符号、延伸线与基准刻线怎么落笔。</summary>
    public enum DimensionOrientation
    {
        /// <summary>线性：两端箭头 + 两端延伸线。总长/总宽/总高这类最常见的测量。</summary>
        Linear = 0,

        /// <summary>半径/直径：起点是被测圆的圆心，落笔时补中心十字线，不画延伸线。</summary>
        Radial = 1,

        /// <summary>链式：画法同线性，但相邻段共用一条延伸线（由 <see cref="DimensionChain"/> 保证首尾相接）。</summary>
        Chain = 2,

        /// <summary>坐标式：各段独立地从同一基准引出，互不扣减；基准端画刻线，不画延伸线。</summary>
        Ordinate = 3,
    }

    /// <summary>
    /// 一段尺寸标注的几何：被测两端点、端部符号、延伸线与数字的相对位置。
    /// 只算几何、不落笔，落笔见 <c>DimensionPainter</c>。
    /// </summary>
    /// <remarks>
    /// 量值 <see cref="Measured"/> 一律由坐标经像素→量值映射<b>回写</b>（见 <see cref="SetMeasuredFromPixels"/>
    /// 与 <see cref="DimensionChain.ApplyScale"/>），构造参数里没有量值——否则调用方可以随手填一个和
    /// 图上量出来的不一样的数，做出"标注写的和图上量的不一致"这种版面谎话。
    /// </remarks>
    public sealed class DimensionRun
    {
        /// <summary>哨兵值：表示该项未单独指定，绘制时沿 <see cref="Render.DimensionTokens"/> 的默认值。
        /// 用 static readonly 而非 const——<see cref="float.NaN"/> 不是编译期常量。</summary>
        public static readonly float NotSet = float.NaN;

        private float _knockoutWidth = NotSet;
        private float _knockoutHeight = NotSet;
        private float _knockoutOffset = NotSet;

        /// <summary>用像素端点新建一段尺寸标注。量值与文字均为空，等待映射回写与模板填字。</summary>
        /// <param name="from">测量方向上的起始端点（像素）。</param>
        /// <param name="to">测量方向上的终止端点（像素）。</param>
        /// <param name="orientation">尺寸线族别。</param>
        /// <param name="arrowFrom">起点端部符号。</param>
        /// <param name="arrowTo">终点端部符号。</param>
        public DimensionRun(SKPoint from, SKPoint to, DimensionOrientation orientation,
                            ArrowKind arrowFrom, ArrowKind arrowTo)
        {
            From = from;
            To = to;
            Orientation = orientation;
            ArrowFrom = arrowFrom;
            ArrowTo = arrowTo;

            UnitSuffix = string.Empty;
            MeasuredFormat = "0.###";
            TextOffset = NotSet;
            ExtensionAngleDegrees = 0f;
            ExtensionGap = NotSet;
            ExtensionOver = NotSet;
            ProfileFrom = OffPlane;
            ProfileTo = OffPlane;
            ShowExtensionFrom = true;
            ShowExtensionTo = true;
        }

        /// <summary>测量方向上的起始端点（像素）。</summary>
        public SKPoint From { get; }

        /// <summary>测量方向上的终止端点（像素）。</summary>
        public SKPoint To { get; }

        /// <summary>尺寸线族别。</summary>
        public DimensionOrientation Orientation { get; }

        /// <summary>起点端部符号。</summary>
        public ArrowKind ArrowFrom { get; }

        /// <summary>终点端部符号。</summary>
        public ArrowKind ArrowTo { get; }

        /// <summary>
        /// 尺寸数字文本（可空：不写数字只画线，链式里的中间段常这样处理）。
        /// 公差写法由调用方拼进文本，如 "96±2"。
        /// </summary>
        public string? Text { get; set; }

        /// <summary>真实量值：像素长度经坐标映射换算得到，由模板设置，不许手填。</summary>
        public double? Measured { get; set; }

        /// <summary>量值单位后缀，如 "mm"；<see cref="Measured"/> 有值时绘制层会自动拼到数字后面。</summary>
        public string UnitSuffix { get; set; }

        /// <summary>量值的数字格式（默认 "0.###"，整数不带小数点；轴荷这类需要一位小数时改成 "0.0"）。</summary>
        public string MeasuredFormat { get; set; }

        /// <summary>数字相对尺寸线的偏移：正=沿 <see cref="Normal"/> 指向的一侧抬出去，负=压在线上留中断；<see cref="NotSet"/> 沿用令牌。</summary>
        public float TextOffset { get; set; }

        /// <summary>延伸线与测量方向的夹角（度）：正交尺寸用 0，斜向轮廓标注可给非 0。</summary>
        public float ExtensionAngleDegrees { get; set; }

        /// <summary>延伸线与被测轮廓之间的留空（gap），制图惯例要求延伸线不得碰到轮廓；<see cref="NotSet"/> 沿用令牌。</summary>
        public float ExtensionGap { get; set; }

        /// <summary>延伸线超出尺寸线端点的量（over）；<see cref="NotSet"/> 沿用令牌。</summary>
        public float ExtensionOver { get; set; }

        /// <summary>被测轮廓上对应 from 的点，即起点延伸线的出发点；默认 <see cref="OffPlane"/> 表示未指定。</summary>
        public SKPoint ProfileFrom { get; set; }

        /// <summary>被测轮廓上对应 to 的点，即终点延伸线的出发点；默认 <see cref="OffPlane"/> 表示未指定。</summary>
        public SKPoint ProfileTo { get; set; }

        /// <summary>是否画起点延伸线（链式的中间交接点由 <see cref="DimensionChain"/> 关掉，避免重复落笔）。</summary>
        public bool ShowExtensionFrom { get; set; }

        /// <summary>是否画终点延伸线。</summary>
        public bool ShowExtensionTo { get; set; }

        /// <summary>未指定轮廓点的哨兵（两坐标皆 NaN）。</summary>
        public static readonly SKPoint OffPlane = new SKPoint(NotSet, NotSet);

        /// <summary>像素长度（双精度累加，只在最后一步落到 float）。</summary>
        public double LengthInPixels
        {
            get
            {
                double dx = (double)To.X - From.X;
                double dy = (double)To.Y - From.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }
        }

        /// <summary>像素长度。</summary>
        public float Length { get { return (float)LengthInPixels; } }

        /// <summary>两点间像素长度，供链式校验总长。</summary>
        public static float Distance(SKPoint a, SKPoint b)
        {
            double dx = (double)b.X - a.X;
            double dy = (double)b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>测量方向单位向量；两端点重合时退化为 +X，保证不产出 NaN 几何。</summary>
        public void Direction(out float ux, out float uy)
        {
            double dx = (double)To.X - From.X;
            double dy = (double)To.Y - From.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (!(len > 0d))
            {
                ux = 1f;
                uy = 0f;
                return;
            }
            ux = (float)(dx / len);
            uy = (float)(dy / len);
        }

        /// <summary>
        /// 法向单位向量（把测量方向按右手系转 90°，用于放延伸线和数字）。
        /// 水平且自左向右的尺寸线，法向指向屏幕下方；要换一侧，把 from/to 对调或改 <see cref="TextOffset"/> 的正负。
        /// </summary>
        public void Normal(out float nx, out float ny)
        {
            Direction(out float ux, out float uy);
            nx = -uy;
            ny = ux;
        }

        /// <summary>起点箭头尖端点（尖端就落在被测端点上，不做内缩）。</summary>
        public SKPoint StartTip { get { return From; } }

        /// <summary>终点箭头尖端点。</summary>
        public SKPoint EndTip { get { return To; } }

        /// <summary>本段是否给出了被测轮廓点（没给出时绘制层跳过延伸线）。</summary>
        public bool HasProfilePoints
        {
            get
            {
                return !float.IsNaN(ProfileFrom.X) && !float.IsNaN(ProfileFrom.Y)
                    && !float.IsNaN(ProfileTo.X) && !float.IsNaN(ProfileTo.Y);
            }
        }

        /// <summary>延伸留空：未单独设置时取令牌默认。</summary>
        public float ExtensionGapOr(float fallback) { return Pick(ExtensionGap, fallback); }

        /// <summary>延伸超出量：未单独设置时取令牌默认。</summary>
        public float ExtensionOverOr(float fallback) { return Pick(ExtensionOver, fallback); }

        /// <summary>数字偏移：未单独设置时取令牌默认。</summary>
        public float TextOffsetOr(float fallback) { return Pick(TextOffset, fallback); }

        private static float Pick(float value, float fallback)
        {
            return float.IsNaN(value) ? fallback : value;
        }

        /// <summary>把本段的量值由像素映射回写（<paramref name="pixelsPerUnit"/> = 每单位量值占多少像素），
        /// 这是 <see cref="Measured"/> 唯一的写入口径，保证数字与图上量出来的严格一致。</summary>
        public void SetMeasuredFromPixels(double pixelsPerUnit)
        {
            if (!(pixelsPerUnit > 0d) || double.IsInfinity(pixelsPerUnit))
            {
                throw new ArgumentException("每单位量值的像素数必须为正有限值：" + pixelsPerUnit);
            }
            Measured = LengthInPixels / pixelsPerUnit;
        }

        /// <summary>延伸线两端点：从被测轮廓点出发（含 gap 留空），穿过尺寸线端点再超出 over。
        /// gap/over 未单独设置时按 0 处理，绘制层用已解析的令牌值走 <see cref="ExtensionLines(SKPoint,SKPoint,float,float,out SKPoint,out SKPoint,out SKPoint,out SKPoint)"/>。</summary>
        /// <param name="profileFrom">被测轮廓上对应起点的点。</param>
        /// <param name="profileTo">被测轮廓上对应终点的点。</param>
        /// <param name="extFromA">起点延伸线的轮廓侧端点。</param>
        /// <param name="extFromB">起点延伸线的超出端端点。</param>
        /// <param name="extToA">终点延伸线的轮廓侧端点。</param>
        /// <param name="extToB">终点延伸线的超出端端点。</param>
        public void ExtensionLines(SKPoint profileFrom, SKPoint profileTo,
                                   out SKPoint extFromA, out SKPoint extFromB,
                                   out SKPoint extToA, out SKPoint extToB)
        {
            ExtensionLines(profileFrom, profileTo, Pick(ExtensionGap, 0f), Pick(ExtensionOver, 0f),
                           out extFromA, out extFromB, out extToA, out extToB);
        }

        /// <summary>用已解析（本段覆写值或令牌默认）的 gap/over 求延伸线两端点。</summary>
        public void ExtensionLines(SKPoint profileFrom, SKPoint profileTo, float gap, float over,
                                   out SKPoint extFromA, out SKPoint extFromB,
                                   out SKPoint extToA, out SKPoint extToB)
        {
            ExtensionDirection(profileFrom, From, out float ax, out float ay);
            ExtensionDirection(profileTo, To, out float bx, out float by);

            extFromA = Move(profileFrom, (double)ax * gap, (double)ay * gap);
            extFromB = Move(From, (double)ax * over, (double)ay * over);
            extToA = Move(profileTo, (double)bx * gap, (double)by * gap);
            extToB = Move(To, (double)bx * over, (double)by * over);
        }

        /// <summary>用已配置的轮廓点求延伸线端点。</summary>
        public void ExtensionLines(out SKPoint extFromA, out SKPoint extFromB,
                                   out SKPoint extToA, out SKPoint extToB)
        {
            ExtensionLines(ProfileFrom, ProfileTo, out extFromA, out extFromB, out extToA, out extToB);
        }

        /// <summary>
        /// 延伸方向 = 法向绕测量方向转 <see cref="ExtensionAngleDegrees"/>，
        /// 并自动取向为"从被测轮廓指向尺寸线端点"，因此调用方不必关心轮廓在尺寸线哪一侧。
        /// </summary>
        private void ExtensionDirection(SKPoint profile, SKPoint tip, out float ex, out float ey)
        {
            Normal(out float nx, out float ny);
            double rad = (double)Pick(ExtensionAngleDegrees, 0f) * Math.PI / 180d;
            double dx = (double)nx * Math.Cos(rad) - (double)ny * Math.Sin(rad);
            double dy = (double)nx * Math.Sin(rad) + (double)ny * Math.Cos(rad);

            double vx = (double)tip.X - profile.X;
            double vy = (double)tip.Y - profile.Y;
            if ((vx * nx + vy * ny) < 0d)
            {
                dx = -dx;
                dy = -dy;
            }

            ex = (float)dx;
            ey = (float)dy;
        }

        /// <summary>数字的几何中心点：尺寸线中点沿法向抬 <see cref="TextOffset"/>（未设置时贴在线上）。</summary>
        public SKPoint TextAnchor { get { return TextAnchorAt(Pick(TextOffset, 0f)); } }

        /// <summary>按指定偏移求数字几何中心，供绘制层用已解析的偏移落字。</summary>
        public SKPoint TextAnchorAt(float offset)
        {
            Normal(out float nx, out float ny);
            double cx = ((double)From.X + To.X) / 2d;
            double cy = ((double)From.Y + To.Y) / 2d;
            return new SKPoint((float)(cx + (double)nx * offset), (float)(cy + (double)ny * offset));
        }

        /// <summary>
        /// 数字压断尺寸线时给线挖出的中断矩形（轴对齐，水平/垂直尺寸线为精确值，斜向为外接框）。
        /// 未设置过宽度、或宽度非法时返回 <see cref="SKRect.Empty"/>，表示"不中断"。
        /// </summary>
        public SKRect TextKnockout
        {
            get
            {
                if (float.IsNaN(_knockoutWidth) || float.IsNaN(_knockoutHeight)
                    || _knockoutWidth <= 0f || _knockoutHeight <= 0f)
                {
                    return SKRect.Empty;
                }

                SKPoint c = TextAnchorAt(Pick(_knockoutOffset, Pick(TextOffset, 0f)));
                return new SKRect(c.X - _knockoutWidth / 2f, c.Y - _knockoutHeight / 2f,
                                  c.X + _knockoutWidth / 2f, c.Y + _knockoutHeight / 2f);
            }
        }

        /// <summary>设置中断宽度（高度按宽度的 0.5 兜底，中心取 <see cref="TextAnchor"/>）；传入 0 或负值即取消中断。</summary>
        public void SetTextKnockoutWidth(float width)
        {
            SetTextKnockout(width, width * 0.5f, null);
        }

        /// <summary>设置中断矩形的宽高（由绘制层用实测文本尺寸 + 令牌留白算出），中心取 <see cref="TextAnchor"/>。</summary>
        public void SetTextKnockout(float width, float height)
        {
            SetTextKnockout(width, height, null);
        }

        /// <summary>
        /// 设置中断矩形并指定数字相对尺寸线的偏移：<paramref name="textOffset"/> 省略时用 <see cref="TextOffset"/>
        /// 自身（未设置则贴线）。绘制层必须把"已解析的令牌偏移"传进来，否则中断位置与数字位置会错开。
        /// </summary>
        public void SetTextKnockout(float width, float height, float? textOffset)
        {
            _knockoutWidth = width;
            _knockoutHeight = height;
            _knockoutOffset = textOffset ?? NotSet;
        }

        /// <summary>中断矩形的宽（未启用时为 0），供绘制层与命中测试复用。</summary>
        public float KnockoutWidth { get { return float.IsNaN(_knockoutWidth) ? 0f : _knockoutWidth; } }

        /// <summary>中断矩形的高（未启用时为 0）。</summary>
        public float KnockoutHeight { get { return float.IsNaN(_knockoutHeight) ? 0f : _knockoutHeight; } }

        /// <summary>
        /// 最终落纸的文本：<see cref="Text"/> 原样优先；未写文字但已回写 <see cref="Measured"/> 时按
        /// <see cref="MeasuredFormat"/> + <see cref="UnitSuffix"/> 生成（InvariantCulture，避免中文区域把小数点变逗号）。
        /// </summary>
        public string? DisplayText
        {
            get
            {
                if (!string.IsNullOrEmpty(Text))
                {
                    return Text;
                }

                double? value = Measured;
                if (value == null)
                {
                    return null;
                }

                double v = value.Value;
                if (double.IsNaN(v) || double.IsInfinity(v))
                {
                    return null;
                }

                string number = v.ToString(PickFormat(MeasuredFormat), CultureInfo.InvariantCulture);
                return string.IsNullOrEmpty(UnitSuffix) ? number : number + UnitSuffix;
            }
        }

        private static string PickFormat(string? format)
        {
            return string.IsNullOrEmpty(format) ? "0.###" : format!;
        }

        private static SKPoint Move(SKPoint p, double dx, double dy)
        {
            return new SKPoint((float)((double)p.X + dx), (float)((double)p.Y + dy));
        }
    }

    /// <summary>
    /// 链式标注：若干段首尾相接逐段相加。超限许可图的轴位分段、总长分段用它。
    /// 链式的定义就是"各段之和等于端到端距离"，切点由归一化累计量算出而非逐点累加，故无浮点漂移。
    /// </summary>
    public sealed class DimensionChain
    {
        private readonly List<DimensionRun> _runs = new List<DimensionRun>();

        /// <summary>按占比切出一条链。</summary>
        /// <param name="origin">链起点（像素）。</param>
        /// <param name="terminator">链终点（像素）；最后一段的终点强制取它。</param>
        /// <param name="fractions">各段占比（相对占比之和，不必等于 1；非负、有限且不全零）。为空时退化为整条一段。</param>
        public DimensionChain(SKPoint origin, SKPoint terminator, IEnumerable<double> fractions)
        {
            if (fractions == null)
            {
                throw new ArgumentNullException(nameof(fractions));
            }

            Origin = origin;
            Terminator = terminator;
            TotalLength = DimensionRun.Distance(origin, terminator);

            List<double> values = new List<double>();
            foreach (double f in fractions)
            {
                if (double.IsNaN(f) || double.IsInfinity(f))
                {
                    throw new ArgumentException("链式占比必须是有限数值：" + f.ToString(CultureInfo.InvariantCulture));
                }
                if (f < 0d)
                {
                    throw new ArgumentException("链式占比不得为负：" + f.ToString(CultureInfo.InvariantCulture));
                }
                values.Add(f);
            }

            double sum = 0d;
            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }

            if (values.Count == 0 || !(sum > 0d) || !(TotalLength > 0f))
            {
                _runs.Add(NewRun(origin, terminator, 0));
                FractionSum = 1d;
                return;
            }

            // 切点直接用 origin + (终点 - 起点) × 归一化累计量算，不做逐点累加，因此无 0.1 类漂移。
            double deltaX = (double)terminator.X - origin.X;
            double deltaY = (double)terminator.Y - origin.Y;

            double acc = 0d;
            for (int i = 0; i < values.Count; i++)
            {
                SKPoint from = At(origin, deltaX, deltaY, acc / sum);
                acc += values[i];
                SKPoint to = i == values.Count - 1 ? terminator : At(origin, deltaX, deltaY, acc / sum);
                _runs.Add(NewRun(from, to, i));
            }
            FractionSum = sum;
        }

        /// <summary>链起点。</summary>
        public SKPoint Origin { get; }

        /// <summary>链终点。</summary>
        public SKPoint Terminator { get; }

        /// <summary>按占比切出的连续段：方向一致、端点严格相接，最后一段终点等于 <see cref="Terminator"/>。</summary>
        public IReadOnlyList<DimensionRun> Runs { get { return _runs; } }

        /// <summary>端到端像素长度（= 各段像素长度之和）。</summary>
        public float TotalLength { get; }

        /// <summary>传入占比的原始和（供模板核对是否漏段）。</summary>
        public double FractionSum { get; }

        /// <summary>各段像素长度之和，用于断言"链式总长 == 端到端距离"。</summary>
        public float SumOfRunLengths
        {
            get
            {
                double total = 0d;
                for (int i = 0; i < _runs.Count; i++)
                {
                    total += _runs[i].LengthInPixels;
                }
                return (float)total;
            }
        }

        /// <summary>逐段回写量值：像素长度按 <paramref name="pixelsPerUnit"/> 映射，并统一单位后缀。</summary>
        public void ApplyScale(double pixelsPerUnit, string? unitSuffix)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                _runs[i].SetMeasuredFromPixels(pixelsPerUnit);
                if (unitSuffix != null)
                {
                    _runs[i].UnitSuffix = unitSuffix;
                }
            }
        }

        /// <summary>逐段填字（<paramref name="texts"/> 不足或为 null 的段保持无字，只画线）。</summary>
        public void SetTexts(IEnumerable<string?> texts)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }

            int index = 0;
            foreach (string? text in texts)
            {
                if (index >= _runs.Count)
                {
                    break;
                }
                _runs[index].Text = text;
                index++;
            }
        }

        private static DimensionRun NewRun(SKPoint from, SKPoint to, int index)
        {
            DimensionRun run = new DimensionRun(from, to, DimensionOrientation.Chain, ArrowKind.Filled, ArrowKind.Filled);
            // 链式交接点只落一条延伸线：起点侧只在首段画，终点侧每段画，末段正好补上链尾。
            run.ShowExtensionFrom = index == 0;
            run.ShowExtensionTo = true;
            return run;
        }

        /// <summary>取链上分点：<paramref name="deltaX"/>/<paramref name="deltaY"/> 是端到端的原始差值（不单位化），
        /// 因此 fraction=1 时精确落在终点，fraction=0 时精确落在起点。</summary>
        private static SKPoint At(SKPoint origin, double deltaX, double deltaY, double fraction)
        {
            double t = fraction < 0d ? 0d : (fraction > 1d ? 1d : fraction);
            return new SKPoint((float)((double)origin.X + deltaX * t), (float)((double)origin.Y + deltaY * t));
        }
    }

    /// <summary>
    /// 坐标式（ordinate）标注：所有尺寸从同一基准独立引出，各段之间不做加减，读一个是一个。
    /// 超限许可图的"前悬 / 轴距 / 后悬"这类同基准轴位就用它。
    /// </summary>
    public sealed class DimensionOrdinateSet
    {
        private readonly List<DimensionRun> _runs = new List<DimensionRun>();
        private readonly List<double> _positions = new List<double>();
        private readonly double _stackPitch;

        /// <summary>沿基准引出若干条坐标尺寸。</summary>
        /// <param name="baselineFrom">基准线起点（像素）；同时是量值的起算点。</param>
        /// <param name="baselineTo">基准线终点，给出基准方向与阶梯可用跨度。</param>
        /// <param name="positions">各特征的坐标值 = 沿基准方向的像素距离（自 <paramref name="baselineFrom"/> 起算）。</param>
        /// <param name="stackPitch">
        /// 相邻坐标线的阶梯间距（px）；null 时按基准线长度自动分摊，保证不越出基准跨度。
        /// 各段都从基准出发、按阶梯平行错开落位，因此坐标式可以逐条独立读数。
        /// </param>
        public DimensionOrdinateSet(SKPoint baselineFrom, SKPoint baselineTo, IEnumerable<double> positions,
                                    double? stackPitch = null)
        {
            if (positions == null)
            {
                throw new ArgumentNullException(nameof(positions));
            }

            BaselineFrom = baselineFrom;
            BaselineTo = baselineTo;
            Span = DimensionRun.Distance(baselineFrom, baselineTo);

            foreach (double p in positions)
            {
                if (double.IsNaN(p) || double.IsInfinity(p))
                {
                    throw new ArgumentException("坐标式位置必须是有限数值：" + p.ToString(CultureInfo.InvariantCulture));
                }
                _positions.Add(p);
            }

            _stackPitch = stackPitch ?? AutoPitch(Span, _positions.Count);
            Rebuild();
        }

        /// <summary>基准线起点。</summary>
        public SKPoint BaselineFrom { get; }

        /// <summary>基准线终点。</summary>
        public SKPoint BaselineTo { get; }

        /// <summary>基准线像素长度。</summary>
        public float Span { get; }

        /// <summary>相邻坐标线的阶梯间距（px）。</summary>
        public double StackPitch { get { return _stackPitch; } }

        /// <summary>各段：与基准平行、按阶梯逐条错开，起点在基准一侧、箭头指向特征，段长即该条的坐标值。</summary>
        public IReadOnlyList<DimensionRun> Runs { get { return _runs; } }

        /// <summary>坐标条目数。</summary>
        public int Count { get { return _runs.Count; } }

        /// <summary>第 <paramref name="index"/> 条的坐标值（像素，自基准起算）。</summary>
        public double PositionOf(int index)
        {
            if (index < 0 || index >= _positions.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _positions[index];
        }

        /// <summary>逐段回写量值：像素坐标按 <paramref name="pixelsPerUnit"/> 映射，并统一单位后缀。</summary>
        public void ApplyScale(double pixelsPerUnit, string? unitSuffix)
        {
            for (int i = 0; i < _runs.Count; i++)
            {
                _runs[i].SetMeasuredFromPixels(pixelsPerUnit);
                if (unitSuffix != null)
                {
                    _runs[i].UnitSuffix = unitSuffix;
                }
            }
        }

        /// <summary>逐段填字（不足或为 null 的段保持无字）。</summary>
        public void SetTexts(IEnumerable<string?> texts)
        {
            if (texts == null)
            {
                throw new ArgumentNullException(nameof(texts));
            }

            int index = 0;
            foreach (string? text in texts)
            {
                if (index >= _runs.Count)
                {
                    break;
                }
                _runs[index].Text = text;
                index++;
            }
        }

        private void Rebuild()
        {
            _runs.Clear();

            double dx = (double)BaselineTo.X - BaselineFrom.X;
            double dy = (double)BaselineTo.Y - BaselineFrom.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            double ux = len > 0d ? dx / len : 1d;
            double uy = len > 0d ? dy / len : 0d;
            double nx = -uy;
            double ny = ux;

            for (int i = 0; i < _positions.Count; i++)
            {
                double step = (double)(i + 1) * _stackPitch;
                double sx = (double)BaselineFrom.X + nx * step;
                double sy = (double)BaselineFrom.Y + ny * step;
                double ex = sx + ux * _positions[i];
                double ey = sy + uy * _positions[i];

                SKPoint from = new SKPoint((float)sx, (float)sy);
                SKPoint to = new SKPoint((float)ex, (float)ey);

                // 坐标式惯例：基准端画短斜线，特征端画实心箭头；引出线本身即连接，不再画延伸线。
                DimensionRun run = new DimensionRun(from, to, DimensionOrientation.Ordinate,
                                                    ArrowKind.Slash, ArrowKind.Filled);
                _runs.Add(run);
            }
        }

        private static double AutoPitch(float span, int count)
        {
            if (count <= 0 || !(span > 0f))
            {
                return 0d;
            }
            return (double)span / (count + 1d);
        }
    }
}
