using System;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>图样种类。岩性花纹、规划图例、天窗带斜线、审计底纹共用这一套。</summary>
    public enum PatternKind
    {
        /// <summary>点阵（砂性土、背景底纹）。</summary>
        Dot = 0,

        /// <summary>水平线（水位线、软土）。</summary>
        Horizontal = 1,

        /// <summary>45° 斜纹，方向同 <see cref="RightDiagonal"/>。天窗带、禁动区的默认。</summary>
        Diagonal = 2,

        /// <summary>交叉斜纹 X（砾石、规划图例的填充）。</summary>
        Cross = 3,

        /// <summary>左斜纹 /（与 <see cref="RightDiagonal"/> 区别只在方向，两套方向才能做出"双向区分"的图例）。</summary>
        LeftDiagonal = 4,

        /// <summary>右斜纹 \。</summary>
        RightDiagonal = 5,
    }

    /// <summary>
    /// 按<b>真实毫米间距</b>密排、<b>缩放不拉伸</b>的图样填充。
    /// <para>
    /// 间距与线粗都用 mm 声明（经 <see cref="Unit"/> 换成 96dpi 逻辑像素），
    /// 落笔时按当前画布的设备缩放<b>重画 tile</b>：144dpi 下 tile 的位图尺寸是 96dpi 下的 1.5 倍，
    /// 于是每个 tile 在设备上仍是 1:1 像素落墨（不放大、不发虚），
    /// 而同一条带里的 tile <b>个数</b>在两个 DPI 下完全一致——这正是"间距是实物 mm"的含义。
    /// </para>
    /// <para>
    /// tile 由 <see cref="Painter"/> 按 (kind, spacing, stroke, color, 设备缩放) 缓存，
    /// 同一样式在同一张图上只生成一次。设备缩放的来源有两条，两条都保证 1:1：
    /// 宿主直绘时取画布 CTM（WPF/MAUI 那条），光栅导出时由 <c>Chart.RenderImage</c>
    /// 把 dpi 折算后显式传给 <see cref="Painter"/>（录制阶段看不见最终缩放，不传就退化成放大位图）。
    /// </para>
    /// </summary>
    public sealed class PatternFill : IEquatable<PatternFill>
    {
        /// <summary>最小 tile 边长（设备像素）。间距给得过小时图样会糊成一片，这里兜住下限。</summary>
        private const int MinTilePixels = 1;

        private PatternFill(PatternKind kind, float spacingMm, float strokeMm, SKColor color)
        {
            Kind = kind;
            SpacingMm = spacingMm;
            StrokeMm = strokeMm;
            Color = color;
        }

        /// <summary>图样种类。</summary>
        public PatternKind Kind { get; }

        /// <summary>相邻纹线中线的距离（mm）。</summary>
        public float SpacingMm { get; }

        /// <summary>纹线线粗（mm）。</summary>
        public float StrokeMm { get; }

        /// <summary>纹线颜色。</summary>
        public SKColor Color { get; }

        /// <summary>间距的逻辑像素数（96dpi 口径）。</summary>
        public float SpacingPixels => Unit.Mm(SpacingMm);

        /// <summary>线粗的逻辑像素数（96dpi 口径）。</summary>
        public float StrokePixels => Unit.Mm(StrokeMm);

        /// <summary>
        /// 声明一份图样。
        /// </summary>
        /// <param name="kind">种类。</param>
        /// <param name="spacingMm">间距（mm），必须为正。</param>
        /// <param name="strokeMm">线粗（mm），必须为正。</param>
        /// <param name="color">颜色。</param>
        public static PatternFill Of(PatternKind kind, float spacingMm, float strokeMm, SKColor color)
        {
            if (float.IsNaN(spacingMm) || float.IsInfinity(spacingMm) || spacingMm <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(spacingMm),
                    "图样间距必须是正毫米数，实到 " + spacingMm);
            }
            if (float.IsNaN(strokeMm) || float.IsInfinity(strokeMm) || strokeMm <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(strokeMm),
                    "图样线粗必须是正毫米数，实到 " + strokeMm);
            }

            return new PatternFill(kind, spacingMm, strokeMm, color);
        }

        /// <summary>取 shader（走 <see cref="Painter"/> 的缓存；换 DPI 会另生成一份 tile）。</summary>
        public SKShader BuildShader(Painter painter)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }

            return painter.PatternShaderOf(this);
        }

        /// <summary>缓存键。设备缩放取到 1/1024 的精度并量化，避免浮点尾数把缓存打散。</summary>
        internal string CacheKey(float scaleX, float scaleY)
        {
            return ((int)Kind).ToString() + "|" + Quantize(SpacingMm) + "|" + Quantize(StrokeMm) + "|"
                   + Color.Alpha + "," + Color.Red + "," + Color.Green + "," + Color.Blue + "|"
                   + Quantize(scaleX) + "|" + Quantize(scaleY);
        }

        private static string Quantize(float value)
        {
            // 四舍五入到 1e-6：版面里 mm 令牌的小数位不会比这更细，
            // 而浮点累计误差若不过滤会让同一图样生成两份 tile。
            return Math.Round((double)value, 6).ToString("0.######",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 生成设备分辨率下的 tile 位图。
        /// <paramref name="scaleX"/> / <paramref name="scaleY"/> 是当前画布的设备缩放，
        /// 乘上去是为了让 tile 在设备上 1:1 落墨（不靠放大位图凑尺寸）。
        /// </summary>
        internal SKBitmap BuildTile(float scaleX, float scaleY)
        {
            int width = Math.Max(MinTilePixels, (int)Math.Round((double)SpacingPixels * scaleX));
            int height = Math.Max(MinTilePixels, (int)Math.Round((double)SpacingPixels * scaleY));

            SKBitmap tile = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas canvas = new SKCanvas(tile))
            {
                // 透明底：图样只贡献纹线，底色由带自身的填充负责，
                // 这样同一条带可以先铺淡色再叠花纹而不互相盖掉。
                canvas.Clear(SKColors.Transparent);

                float stroke = Math.Max(1f, (float)((double)StrokePixels * Math.Min(scaleX, scaleY)));
                using (SKPaint line = new SKPaint
                {
                    Color = Color,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = stroke,
                    StrokeCap = SKStrokeCap.Butt,
                    IsAntialias = false,
                })
                using (SKPaint dot = new SKPaint
                {
                    Color = Color,
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                })
                {
                    float cx = width / 2f;
                    float cy = height / 2f;
                    float radius = stroke / 2f;

                    switch (Kind)
                    {
                        case PatternKind.Dot:
                            canvas.DrawCircle(cx, cy, radius, dot);
                            break;

                        case PatternKind.Horizontal:
                            canvas.DrawLine(0f, cy, width, cy, line);
                            break;

                        case PatternKind.LeftDiagonal:
                            DrawDiagonal(canvas, line, width, height, true);
                            break;

                        case PatternKind.Cross:
                            DrawDiagonal(canvas, line, width, height, true);
                            DrawDiagonal(canvas, line, width, height, false);
                            break;

                        case PatternKind.Diagonal:
                        case PatternKind.RightDiagonal:
                        default:
                            DrawDiagonal(canvas, line, width, height, false);
                            break;
                    }
                }
            }

            return tile;
        }

        /// <summary>
        /// 画一条 45° 纹线并补上跨边界的两份平移：tile 只有 (0,H)-(W,0) 一段时，
        ///  stroke 宽度会被 tile 边界齐切，接缝处出现断口；把上、下各平移一份画进来，
        /// 接缝由相邻 tile 的重叠部分补齐。
        /// </summary>
        private static void DrawDiagonal(SKCanvas canvas, SKPaint paint, int width, int height, bool leftward)
        {
            float[] offsets = { -height, 0f, height };
            foreach (float dy in offsets)
            {
                if (leftward)
                {
                    canvas.DrawLine(0f, height + dy, width, dy, paint);
                }
                else
                {
                    canvas.DrawLine(0f, dy, width, height + dy, paint);
                }
            }
        }

        public bool Equals(PatternFill? other)
        {
            return other != null && other.Kind == Kind && other.SpacingMm == SpacingMm
                   && other.StrokeMm == StrokeMm && SameColor(other.Color);
        }

        public override bool Equals(object? obj) => Equals(obj as PatternFill);

        public override int GetHashCode()
        {
            // 不用 HashCode.Combine：它在 netstandard2.0 / net462 上不存在。
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ SpacingMm.GetHashCode();
                hash = hash * 397 ^ StrokeMm.GetHashCode();
                hash = hash * 397 ^ Color.Alpha.GetHashCode();
                hash = hash * 397 ^ Color.Red.GetHashCode();
                hash = hash * 397 ^ Color.Green.GetHashCode();
                hash = hash * 397 ^ Color.Blue.GetHashCode();
                return hash;
            }
        }

        private bool SameColor(SKColor other)
        {
            return other.Alpha == Color.Alpha && other.Red == Color.Red
                   && other.Green == Color.Green && other.Blue == Color.Blue;
        }

        public override string ToString()
        {
            return Kind + " " + SpacingMm.ToString("0.###") + "mm/" + StrokeMm.ToString("0.###") + "mm";
        }
    }
}
