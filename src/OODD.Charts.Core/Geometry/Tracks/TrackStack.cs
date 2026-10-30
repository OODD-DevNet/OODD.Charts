using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>道（track）的内容类型：决定渲染层如何解释该道的数据。</summary>
    public enum TrackKind
    {
        /// <summary>线性刻度曲线道（如视电阻率）。</summary>
        Curve,
        /// <summary>对数刻度曲线道（如自然电位）。</summary>
        LogCurve,
        /// <summary>岩性/分层图案列道。</summary>
        PatternColumn,
        /// <summary>颜色分段区间道（如含油级别色带）。</summary>
        ColorInterval,
        /// <summary>柱状比较道（如油色谱烃类气体含量）。</summary>
        BarColumn,
        /// <summary>纯文字道（如地层代号、备注）。</summary>
        TextColumn,
    }

    /// <summary>一道的定义：宽度、量程轴、内容类型与道头文字。</summary>
    public sealed class Track
    {
        /// <summary>
        /// 构造一道。<paramref name="valueAxis"/> 的方向与像素区间（From=道左缘、To=道右缘）
        /// 由调用方预先设好；<paramref name="width"/> 必须为正。
        /// </summary>
        public Track(string key, string headerLabel, float width, NumericAxis valueAxis, TrackKind kind)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("道键名不能为空。", nameof(key));
            }
            if (width <= 0f)
            {
                throw new ArgumentException("道宽度必须为正：" + key + " " + width, nameof(width));
            }
            if (valueAxis == null)
            {
                throw new ArgumentNullException(nameof(valueAxis), "道必须携带值轴：" + key);
            }

            Key = key;
            HeaderLabel = headerLabel ?? string.Empty;
            Width = width;
            ValueAxis = valueAxis;
            Kind = kind;
        }

        /// <summary>道的唯一键名。</summary>
        public string Key { get; }

        /// <summary>道头标题文字（如 "视电阻率"）。</summary>
        public string HeaderLabel { get; }

        /// <summary>道的像素宽度。</summary>
        public float Width { get; }

        /// <summary>该道自己的值轴（横向：From=道左缘，To=道右缘）。Curve/LogCurve 用它定位曲线 x。</summary>
        public NumericAxis ValueAxis { get; }

        /// <summary>道的内容类型。</summary>
        public TrackKind Kind { get; }

        /// <summary>刻度位置：把值映射到道内 x。ValueAxis 的方向已由调用方设好，这里只做便捷转发。</summary>
        public float PositionOf(double value)
        {
            return ValueAxis.PositionOf(value);
        }
    }

    /// <summary>道头（页顶标题条）的一格，续页时必须原样重复。</summary>
    public readonly struct TrackHeader
    {
        /// <summary>构造一格道头。<paramref name="rect"/> 为其在页面上的像素矩形。</summary>
        public TrackHeader(string key, SKRect rect, string label, string scaleCaption)
        {
            Key = key;
            Rect = rect;
            Label = label;
            ScaleCaption = scaleCaption;
        }

        /// <summary>对应道的键名。</summary>
        public string Key { get; }

        /// <summary>道头格子的像素矩形。</summary>
        public SKRect Rect { get; }

        /// <summary>道头标题文字。</summary>
        public string Label { get; }

        /// <summary>量程说明文字，如 "0~400 Ω·m" 或 "1~1000（对数）"。</summary>
        public string ScaleCaption { get; }
    }

    /// <summary>轨道组的间距与分隔方式。</summary>
    public sealed class TrackLayoutOptions
    {
        /// <summary>道与道之间的空白（像素）。</summary>
        public float TrackGap = 0f;

        /// <summary>道头高（像素），0 表示无道头。</summary>
        public float HeaderHeight = 0f;

        /// <summary>道间分隔线粗（供渲染层读取）。</summary>
        public float SeparatorWidth = 1f;

        /// <summary>是否绘制道间分隔线（供渲染层读取）。</summary>
        public bool DrawSeparators = true;
    }

    /// <summary>
    /// 共享一条域轴的并置多轨：石油测井成果图、变压器油色谱趋势、盾构环记录等
    /// "横向并置若干独立量程的道、纵向共用一条深度/里程/时间轴" 的公共结构。
    /// 本类型只做纯几何映射，不绘制；续页切分见 <see cref="SplitByDomain"/>。
    /// </summary>
    public sealed class TrackStack
    {
        /// <summary>值轴像素区间与道矩形比较用的容差（像素）。</summary>
        private const double AxisPixelTolerance = 0.01d;

        /// <summary>
        /// 构造轨道栈。
        /// </summary>
        /// <param name="key">栈键名。</param>
        /// <param name="left">最左缘像素 x。</param>
        /// <param name="domainFrom">共享域轴的像素起点（深度轴通常为页顶 y）；方向由调用方决定。</param>
        /// <param name="domainTo">共享域轴的像素终点（深度轴通常为页底 y）。</param>
        /// <param name="domainMin">共享域的值域下限（如起始深度），不是像素。</param>
        /// <param name="domainMax">共享域的值域上限（如终止深度），不是像素。必须大于 domainMin。</param>
        /// <param name="tracks">道的序列，按从左到右排布。道键名在本栈内必须唯一——按 key 取道/取矩形只认第一个。</param>
        /// <param name="options">间距与道头选项；传 null 视为全默认值。负的 <c>TrackGap</c>（道体重叠）与
        /// 负的 <c>HeaderHeight</c>（图头跑到绘图区上方之外）都会在这里被拒。</param>
        public TrackStack(string key, float left, float domainFrom, float domainTo,
                          double domainMin, double domainMax,
                          IEnumerable<Track> tracks, TrackLayoutOptions options)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("轨道栈的键名不能为空。", nameof(key));
            }
            if (tracks == null)
            {
                throw new ArgumentNullException(nameof(tracks));
            }
            if (domainMax <= domainMin)
            {
                throw new ArgumentException(
                    "域值上限必须大于下限：[" + domainMin + "," + domainMax + "]");
            }

            Key = key;
            Left = left;
            DomainFrom = domainFrom;
            DomainTo = domainTo;
            DomainMin = domainMin;
            DomainMax = domainMax;
            Options = options ?? new TrackLayoutOptions();

            // !(x >= 0) 同时挡住 NaN；∞ 间隙会让右边所有道的 x 变成 ∞，一并挡掉。
            if (!(Options.TrackGap >= 0f) || double.IsInfinity(Options.TrackGap))
            {
                throw new ArgumentException(
                    "栈 \"" + key + "\" 的道间隙必须是不小于 0 的有限数，实际为 " + Options.TrackGap,
                    nameof(options));
            }
            if (!(Options.HeaderHeight >= 0f) || double.IsInfinity(Options.HeaderHeight))
            {
                throw new ArgumentException(
                    "栈 \"" + key + "\" 的道头高必须是不小于 0 的有限数，实际为 " + Options.HeaderHeight,
                    nameof(options));
            }

            List<Track> list = new List<Track>(tracks);
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null)
                {
                    throw new ArgumentException(
                        "栈 \"" + key + "\" 的第 " + i + " 个道是 null。", nameof(tracks));
                }
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(list[j].Key, list[i].Key, StringComparison.Ordinal))
                    {
                        throw new ArgumentException(
                            "栈 \"" + key + "\" 内出现重复的道键名 \"" + list[i].Key + "\"（第 " + j + " 与第 "
                            + i + " 道），按 key 取道只会命中第一个。", nameof(tracks));
                    }
                }
            }
            Tracks = list;

            // 逐道累计横向位置：x0 = Left + i*(宽之和) + i*间隙。
            float x = Left;
            for (int i = 0; i < list.Count; i++)
            {
                if (i > 0)
                {
                    x += Options.TrackGap;
                }

                float top = Math.Min(DomainFrom, DomainTo);
                float bottom = Math.Max(DomainFrom, DomainTo);
                _rects.Add(new SKRect(x, top, x + list[i].Width, bottom));
                x += list[i].Width;
            }
            Right = x;

            // 护栏：道的值轴像素区间必须落在自己的道矩形里（允许内缩留白，不许越进邻道）。
            // Track 的构造注释写着"From=道左缘、To=道右缘"，但量程一旦写错，
            // 投点会静默飞进邻道——混排互不污染正是要在这里拦住的。
            for (int i = 0; i < list.Count; i++)
            {
                NumericAxis axis = list[i].ValueAxis;
                SKRect rect = _rects[i];
                double lo = Math.Min(axis.From, axis.To);
                double hi = Math.Max(axis.From, axis.To);
                if (lo < rect.Left - AxisPixelTolerance || hi > rect.Right + AxisPixelTolerance)
                {
                    throw new ArgumentException(
                        "栈 \"" + key + "\" 的道 \"" + list[i].Key + "\" 的值轴像素区间是 [" + axis.From + ","
                        + axis.To + "]，道矩形是 [" + rect.Left + "," + rect.Right + "]：轴超出道边界，"
                        + "投点会画进邻道。把值轴的 From/To 设成道左/右缘（或道内的留白区间）。");
                }
            }

            // 道头行在绘图区上边界之上，高 HeaderHeight；无道头时为空矩形。
            float headerTop = Math.Min(DomainFrom, DomainTo) - Options.HeaderHeight;
            float headerBottom = Math.Min(DomainFrom, DomainTo);
            List<TrackHeader> headers = new List<TrackHeader>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                SKRect rect = Options.HeaderHeight > 0f
                    ? new SKRect(_rects[i].Left, headerTop, _rects[i].Right, headerBottom)
                    : SKRect.Empty;
                headers.Add(new TrackHeader(
                    list[i].Key, rect, list[i].HeaderLabel, BuildScaleCaption(list[i])));
            }
            Headers = headers;
        }

        private readonly List<SKRect> _rects = new List<SKRect>();

        /// <summary>栈键名。</summary>
        public string Key { get; }

        /// <summary>按从左到右顺序的各道。</summary>
        public IReadOnlyList<Track> Tracks { get; }

        /// <summary>道数。</summary>
        public int Count { get { return Tracks.Count; } }

        /// <summary>最左缘像素 x。</summary>
        public float Left { get; }

        /// <summary>最右缘像素 x（Left + 各道宽 + 间隙）。</summary>
        public float Right { get; }

        /// <summary>共享域轴的像素起点（如页顶 y）。</summary>
        public float DomainFrom { get; }

        /// <summary>共享域轴的像素终点（如页底 y）。</summary>
        public float DomainTo { get; }

        /// <summary>共享域的值域下限（深度/里程/时间，不是像素）。</summary>
        public double DomainMin { get; }

        /// <summary>共享域的值域上限（不是像素）。</summary>
        public double DomainMax { get; }

        /// <summary>构造时传入的间距与道头选项。</summary>
        public TrackLayoutOptions Options { get; }

        /// <summary>栈总宽（Right - Left）。</summary>
        public float Width { get { return Right - Left; } }

        /// <summary>第 index 道的横向矩形（不含道头）。</summary>
        public SKRect RectOf(int index)
        {
            return _rects[index];
        }

        /// <summary>按键名取道矩形；键不存在时抛 <see cref="KeyNotFoundException"/>。</summary>
        public SKRect RectOf(string key)
        {
            return _rects[IndexOf(key)];
        }

        /// <summary>第 index 道。</summary>
        public Track this[int index] { get { return Tracks[index]; } }

        /// <summary>按键名取道；键不存在时抛 <see cref="KeyNotFoundException"/>。</summary>
        public Track this[string key]
        {
            get { return Tracks[IndexOf(key)]; }
        }

        /// <summary>道头行矩形（HeaderHeight 为 0 时返回空矩形）。</summary>
        public SKRect HeaderRectOf(int index)
        {
            return Headers[index].Rect;
        }

        /// <summary>所有道头，供续页重复绘制。</summary>
        public IReadOnlyList<TrackHeader> Headers { get; }

        /// <summary>
        /// 在指定道内，把 (域值, 值) 定位到像素点：x 走该道自己的值轴，
        /// y（或横向栈的 x）走栈的共享域轴线性映射，保证各道纵向严格对齐。
        /// </summary>
        public SKPoint PointOf(int index, double domain, double value)
        {
            return new SKPoint(Tracks[index].PositionOf(value), DomainPositionOf(domain));
        }

        /// <summary>
        /// 共享域轴的像素位置（深度/时间/里程都走这里，保证各道对齐）。
        /// 域值先线性归一并夹紧到 [domainMin, domainMax]，再映射到 [DomainFrom, DomainTo]。
        /// <para>
        /// 方向不在这里表达：<c>DomainFrom</c>/<c>DomainTo</c> 谁是页顶谁是页底就是方向，
        /// 与 <see cref="NumericAxis.Direction"/> 那个语义标注无关（轴自己的映射也不读它）。
        /// 域外值夹紧到端点是有意为之——续页接缝处的像素连续比"飞出去"更有用。
        /// </para>
        /// </summary>
        public float DomainPositionOf(double domain)
        {
            double t = (domain - DomainMin) / (DomainMax - DomainMin);
            if (t < 0d)
            {
                t = 0d;
            }
            else if (t > 1d)
            {
                t = 1d;
            }

            return (float)(DomainFrom + (double)(DomainTo - DomainFrom) * t);
        }

        /// <summary>
        /// 把栈按域值切成若干段（续页用）。从 <see cref="DomainMin"/> 起按
        /// <paramref name="segmentLength"/> 等分，最后一段允许短；每段的像素区间用同一
        /// 线性映射算出，保证相邻段接缝像素级连续。纯函数，可反复调用。
        /// </summary>
        public IReadOnlyList<TrackSegment> SplitByDomain(double segmentLength)
        {
            if (segmentLength <= 0d)
            {
                throw new ArgumentException("分段长度必须为正：" + segmentLength, nameof(segmentLength));
            }

            // 段数用乘法式起点 + 容差向上取整，避免浮点累加漂移导致跨平台段数不一致。
            double ratio = (DomainMax - DomainMin) / segmentLength;
            int count = (int)Math.Ceiling(ratio - 1e-9);
            if (count < 1)
            {
                count = 1;
            }

            List<TrackSegment> segments = new List<TrackSegment>(count);
            for (int i = 0; i < count; i++)
            {
                double start = DomainMin + i * segmentLength;
                double end = i == count - 1 ? DomainMax : DomainMin + (i + 1) * segmentLength;
                if (end > DomainMax)
                {
                    end = DomainMax;
                }

                segments.Add(new TrackSegment(
                    start, end, DomainPositionOf(start), DomainPositionOf(end), i));
            }
            return segments;
        }

        /// <summary>按键名找道的下标；找不到抛 <see cref="KeyNotFoundException"/>，消息带键名。</summary>
        public int IndexOf(string key)
        {
            for (int i = 0; i < Tracks.Count; i++)
            {
                if (string.Equals(Tracks[i].Key, key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            throw new KeyNotFoundException("轨道栈中不存在该道：" + key);
        }

        /// <summary>
        /// 由道的值轴拼量程说明：线性写 "min~max"，对数写 "min~max（对数）"。
        /// 数字一律用 InvariantCulture 的 "0.###" 格式化，
        /// 避免中文环境把小数点变逗号造成跨 TFM/跨机器不一致。
        /// </summary>
        private static string BuildScaleCaption(Track track)
        {
            NumericAxis axis = track.ValueAxis;
            string min = axis.Min.ToString("0.###", CultureInfo.InvariantCulture);
            string max = axis.Max.ToString("0.###", CultureInfo.InvariantCulture);
            string caption = min + "~" + max;

            if (axis.Scale == AxisScale.Logarithmic)
            {
                caption += "（对数）";
            }
            return caption;
        }
    }

    /// <summary>一段续页的域区间与像素区间。</summary>
    public readonly struct TrackSegment
    {
        /// <summary>手工构造一段（不经 <see cref="TrackStack.SplitByDomain"/>），序号默认 0。</summary>
        public TrackSegment(double domainStart, double domainEnd, float pixelFrom, float pixelTo)
            : this(domainStart, domainEnd, pixelFrom, pixelTo, 0)
        {
        }

        /// <summary>构造一段并指定序号；<see cref="TrackStack.SplitByDomain"/> 内部使用。</summary>
        public TrackSegment(double domainStart, double domainEnd, float pixelFrom, float pixelTo, int index)
        {
            DomainStart = domainStart;
            DomainEnd = domainEnd;
            PixelFrom = pixelFrom;
            PixelTo = pixelTo;
            Index = index;
        }

        /// <summary>该段域值起点（含）。</summary>
        public double DomainStart { get; }

        /// <summary>该段域值终点（末段含，其余段作为下一段起点出现）。</summary>
        public double DomainEnd { get; }

        /// <summary>域起点对应的像素位置。</summary>
        public float PixelFrom { get; }

        /// <summary>域终点对应的像素位置。</summary>
        public float PixelTo { get; }

        /// <summary>段序号，从 0 起，SplitByDomain 内部赋值。</summary>
        public int Index { get; }
    }
}
