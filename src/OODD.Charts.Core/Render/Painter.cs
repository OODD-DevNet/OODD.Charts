using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 绘制器：持有画布、样式与字体解析器，并把 <see cref="SKPaint"/> / <see cref="SKFont"/>
    /// 按参数缓存起来。绘制代码通过它落笔，从而保证同一份样式只分配一次资源。
    /// </summary>
    public sealed class Painter : IDisposable
    {
        private readonly SKCanvas _canvas;
        private readonly ChartStyle _style;
        private readonly IFontResolver _fonts;
        private readonly Dictionary<string, SKPaint> _paints = new Dictionary<string, SKPaint>(StringComparer.Ordinal);
        private readonly Dictionary<FontKey, SKFont> _fontObjects = new Dictionary<FontKey, SKFont>();
        private readonly Dictionary<string, SKShader> _shaders = new Dictionary<string, SKShader>(StringComparer.Ordinal);
        private int _clipDepth;
        private float _patternDeviceScale;

        public Painter(SKCanvas canvas, ChartStyle style, IFontResolver fonts)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            _style = style ?? throw new ArgumentNullException(nameof(style));
            _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        }

        public SKCanvas Canvas => _canvas;
        public ChartStyle Style => _style;
        public IFontResolver Fonts => _fonts;

        /// <summary>已分配的画笔数，用于验证缓存生效（T20 验收）。</summary>
        public int PaintCount => _paints.Count;

        /// <summary>已分配的字体对象数。</summary>
        public int FontCount => _fontObjects.Count;

        /// <summary>已分配的图样 shader 数（按 kind+间距+线粗+色+设备缩放缓存）。</summary>
        public int ShaderCount => _shaders.Count;

        /// <summary>
        /// 未闭合的 <see cref="PushClipRect"/> 层数。正常落笔结束时必须是 0，
        /// <see cref="Dispose"/> 在此值非 0 时抛异常——裁剪忘配对会污染后续全部绘制。
        /// </summary>
        public int ClipDepth => _clipDepth;

        // ---------------------------------------------------------------- 画笔

        private SKPaint GetPaint(SKColor color, float strokeWidth, float[]? dash)
        {
            string key = color.ToString() + "|" + strokeWidth.ToString("R") + "|" +
                         (dash == null ? "s" : "d" + string.Join(",", ToStrings(dash)));
            SKPaint? cached;
            if (_paints.TryGetValue(key, out cached))
            {
                return cached;
            }

            SKPaint paint = new SKPaint
            {
                Color = color,
                IsAntialias = true,
                Style = strokeWidth <= 0f ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
                StrokeWidth = strokeWidth <= 0f ? 1f : strokeWidth,
                StrokeCap = SKStrokeCap.Butt,
                StrokeJoin = SKStrokeJoin.Miter,
            };

            if (dash != null)
            {
                paint.PathEffect = SKPathEffect.CreateDash(dash, 0f);
            }

            _paints[key] = paint;
            return paint;
        }

        private static string[] ToStrings(float[] values)
        {
            string[] result = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                result[i] = values[i].ToString("R");
            }
            return result;
        }

        private SKPaint Fill(SKColor color) => GetPaint(color, 0f, null);
        private SKPaint Stroke(SKColor color, float width) => GetPaint(color, width, null);
        private SKPaint Dashed(SKColor color, float width)
        {
            return GetPaint(color, width, new[] { _style.Series.DashInterval, _style.Series.DashPhase });
        }

        // ---------------------------------------------------------------- 字体

        /// <summary>取字体对象。字号变化会新建，同一 FontKey 复用。</summary>
        public SKFont GetFont(FontKey key)
        {
            SKFont? cached;
            if (_fontObjects.TryGetValue(key, out cached))
            {
                return cached;
            }

            SKTypeface typeface = _fonts.Resolve(key);
            SKFont font = new SKFont(typeface, key.Size <= 0f ? _style.Text.CellValueSize : key.Size);
            _fontObjects[key] = font;
            return font;
        }

        public SKFontMetrics MetricsOf(FontKey key) => GetFont(key).Metrics;

        public float TextWidth(string text, FontKey key) => GetFont(key).MeasureText(text ?? string.Empty);

        // ---------------------------------------------------------------- 图元

        public void Line(float x0, float y0, float x1, float y1, SKColor color, float width)
        {
            _canvas.DrawLine(x0, y0, x1, y1, Stroke(color, width));
        }

        public void DashedLine(float x0, float y0, float x1, float y1, SKColor color, float width)
        {
            _canvas.DrawLine(x0, y0, x1, y1, Dashed(color, width));
        }

        public void Polyline(IEnumerable<SKPoint> points, SKColor color, float width, bool dashed)
        {
            using (SKPath path = BuildPath(points, false))
            {
                if (path.IsEmpty)
                {
                    return;
                }
                SKPaint paint = dashed ? Dashed(color, width) : Stroke(color, width);
                _canvas.DrawPath(path, paint);
            }
        }

        /// <summary>把若干不连续的点段分别连成折线（缺测断线用）。</summary>
        public void Polylines(IEnumerable<IEnumerable<SKPoint>> segments, SKColor color, float width, bool dashed)
        {
            foreach (IEnumerable<SKPoint> segment in segments)
            {
                Polyline(segment, color, width, dashed);
            }
        }

        public SKPath BuildPath(IEnumerable<SKPoint> points, bool close)
        {
            SKPath path = new SKPath();
            bool first = true;
            foreach (SKPoint point in points)
            {
                if (first)
                {
                    path.MoveTo(point);
                    first = false;
                }
                else
                {
                    path.LineTo(point);
                }
            }
            if (close && !first)
            {
                path.Close();
            }
            return path;
        }

        public void Rect(SKRect rect, SKColor color, float width)
        {
            _canvas.DrawRect(rect, Stroke(color, width));
        }

        public void FillRect(SKRect rect, SKColor color)
        {
            _canvas.DrawRect(rect, Fill(color));
        }

        /// <summary>画矩形边框（不填充）。用四条边避免描边一半落在框外。</summary>
        public void Frame(SKRect rect, SKColor color, float width)
        {
            float half = width / 2f;
            SKRect inset = new SKRect(rect.Left + half, rect.Top + half, rect.Right - half, rect.Bottom - half);
            _canvas.DrawRect(inset, Stroke(color, width));
        }

        public void Circle(SKPoint center, float radius, SKColor color, float width)
        {
            _canvas.DrawCircle(center.X, center.Y, radius, Stroke(color, width));
        }

        public void FillCircle(SKPoint center, float radius, SKColor color)
        {
            _canvas.DrawCircle(center.X, center.Y, radius, Fill(color));
        }

        /// <summary>画椭圆框，用于产程图圈注异常胎心率这类标记。</summary>
        public void Ellipse(SKRect bounds, SKColor color, float width)
        {
            _canvas.DrawOval(bounds, Stroke(color, width));
        }

        // ---------------------------------------------------------------- 文本

        /// <summary>在基线 (x, y) 处画文本。</summary>
        public void Text(string text, float x, float y, FontKey key, SKColor color)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            _canvas.DrawText(text, x, y, GetFont(key), Fill(color));
        }

        /// <summary>在矩形框内按水平/垂直对齐画单行文本。</summary>
        public void TextInBox(string text, SKRect box, FontKey key, SKColor color,
                              CellAlign horizontal, CellAlign vertical, float padding = 1f)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SKFont font = GetFont(key);
            float width = font.MeasureText(text);
            SKFontMetrics metrics = font.Metrics;

            float x;
            if (horizontal == CellAlign.Near)
            {
                x = box.Left + padding;
            }
            else if (horizontal == CellAlign.Far)
            {
                x = box.Right - padding - width;
            }
            else
            {
                x = box.MidX - width / 2f;
            }

            float baseline;
            if (vertical == CellAlign.Near)
            {
                baseline = box.Top - metrics.Ascent + padding;
            }
            else if (vertical == CellAlign.Far)
            {
                baseline = box.Bottom - metrics.Descent - padding;
            }
            else
            {
                baseline = box.MidY - (metrics.Ascent + metrics.Descent) / 2f;
            }

            _canvas.DrawText(text, x, baseline, font, Fill(color));
        }

        /// <summary>
        /// 逐字竖排。不用 canvas.Rotate：旋转文本导出 PDF/SVG 后会退化成带变换的路径，
        /// 无法作为文本检索。三测单事件带要求逐字落位。
        /// </summary>
        public void VerticalText(string text, float centerX, float topY, FontKey key, SKColor color,
                                 float lineSpacingFactor = 1.15f, float maxBottom = float.MaxValue)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            SKFont font = GetFont(key);
            SKFontMetrics metrics = font.Metrics;
            float step = font.Size * lineSpacingFactor;
            float y = topY - metrics.Ascent;

            for (int i = 0; i < text.Length; i++)
            {
                if (y > maxBottom)
                {
                    return;
                }
                // 空格没有墨迹，但 SKSvgCanvas 照样写出 <text x=""> 空节点，污染矢量产物；
                // 走格不进画布，行距仍按字步进。
                if (!char.IsWhiteSpace(text[i]))
                {
                    string glyph = char.ToString(text[i]);
                    float w = font.MeasureText(glyph);
                    _canvas.DrawText(glyph, centerX - w / 2f, y, font, Fill(color));
                }
                y += step;
            }
        }

        // ---------------------------------------------------------------- 符号

        /// <summary>画行业约定的点标记符号。</summary>
        public void Marker(MarkerSymbol symbol, SKPoint center, float radius, SKColor color, float stroke)
        {
            switch (symbol)
            {
                case MarkerSymbol.None:
                    return;

                case MarkerSymbol.Dot:
                    FillCircle(center, radius, color);
                    break;

                case MarkerSymbol.Ring:
                    Circle(center, radius, color, stroke);
                    break;

                case MarkerSymbol.Cross:
                    Line(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius, color, stroke);
                    Line(center.X - radius, center.Y + radius, center.X + radius, center.Y - radius, color, stroke);
                    break;

                case MarkerSymbol.DotRing:
                    Circle(center, radius, color, stroke);
                    FillCircle(center, radius * 0.45f, color);
                    break;

                case MarkerSymbol.PlusInRing:
                    Circle(center, radius, color, stroke);
                    Line(center.X - radius, center.Y, center.X + radius, center.Y, color, stroke);
                    Line(center.X, center.Y - radius, center.X, center.Y + radius, color, stroke);
                    break;

                case MarkerSymbol.DotStem:
                    FillCircle(center, radius, color);
                    Line(center.X, center.Y - radius, center.X, center.Y - radius * 3f, color, stroke);
                    break;
            }
        }

        // ---------------------------------------------------------------- 路径与裁剪

        /// <summary>填充一条路径（包线、断面轮廓、三角分区）。</summary>
        public void FillPath(SKPath path, SKColor color)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _canvas.DrawPath(path, Fill(color));
        }

        /// <summary>描一条路径（不填充）。线状端部符号、限值折线用它。</summary>
        public void StrokePath(SKPath path, SKColor color, float width)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            _canvas.DrawPath(path, Stroke(color, width));
        }

        /// <summary>填充一个多边形（首尾自动相接）。三角分区、评价带、风向杆羽旗用它。</summary>
        public void FillPolygon(IEnumerable<SKPoint> points, SKColor color)
        {
            using (SKPath path = BuildPath(points, true))
            {
                if (path.IsEmpty)
                {
                    return;
                }

                _canvas.DrawPath(path, Fill(color));
            }
        }

        /// <summary>用图样填充一条路径。图样按真实 mm 间距排布，缩放不拉伸（见 <see cref="PatternFill"/>）。</summary>
        public void FillPathWithPattern(SKPath path, PatternFill pattern)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (pattern == null)
            {
                throw new ArgumentNullException(nameof(pattern));
            }

            SKShader shader = PatternShaderOf(pattern);
            // shader 挂在缓存里，不能进 paint 缓存：GetPaint 的键不含图样，
            // 复用会把上一种图样带过来。这里每次都新建一支临时 paint。
            using (SKPaint paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Shader = shader,
                IsAntialias = true,
            })
            {
                _canvas.DrawPath(path, paint);
            }
        }

        /// <summary>
        /// 指定图样 tile 的目标设备缩放（光栅导出用）。
        /// <para>
        /// 默认不设置时 tile 跟随画布 CTM——那是宿主直绘那条路，画布本来就带着系统缩放。
        /// 但 <c>Chart.RenderImage</c> 走"先录 <see cref="SKPicture"/>（此刻 CTM 是单位阵）
        /// 再 <c>canvas.Scale(dpi/96)</c>"，录制时看不见最终缩放；不传这个提示，300dpi 出图
        /// 用的仍是 96dpi 的 tile，纹线是被放大的低分辨率位图而不是按设备重画的。
        /// </para>
        /// </summary>
        internal void SetPatternDeviceScale(float scale)
        {
            if (float.IsNaN(scale) || scale < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(scale), "图样设备缩放必须是非负有限数，实到 " + scale);
            }

            _patternDeviceScale = scale;
        }

        /// <summary>
        /// 取（或首次生成）图样 shader。<b>tile 位图按当前设备缩放重画</b>，
        /// 所以 96dpi 与 144dpi 各有一份 tile，每个 tile 在设备上都是 1:1 落墨。
        /// </summary>
        internal SKShader PatternShaderOf(PatternFill pattern)
        {
            SKMatrix matrix = _canvas.TotalMatrix;
            float scaleX = Math.Abs(matrix.ScaleX) < 1e-6f ? 1f : matrix.ScaleX;
            float scaleY = Math.Abs(matrix.ScaleY) < 1e-6f ? 1f : matrix.ScaleY;
            if (_patternDeviceScale > 0f)
            {
                scaleX = _patternDeviceScale;
                scaleY = _patternDeviceScale;
            }

            string key = pattern.CacheKey(scaleX, scaleY);
            SKShader? cached;
            if (_shaders.TryGetValue(key, out cached))
            {
                return cached;
            }

            using (SKBitmap tile = pattern.BuildTile(scaleX, scaleY))
            {
                // SkiaSharp 3.116.1 没有 CreatePattern/CreateTileImage，位图图样走 CreateBitmap。
                // 局部矩阵把图样从"设备像素"放回"逻辑像素"：tile 边长是 spacingPx*缩放，
                // 除以缩放后它在用户空间的周期正好等于 spacingPx，于是设备周期 == tile 边长，
                // 一位图像素对一个设备像素——放大倍率全进了位图，没进拉伸。
                SKShader shader = SKShader.CreateBitmap(
                    tile, SKShaderTileMode.Repeat, SKShaderTileMode.Repeat,
                    SKMatrix.CreateScale(1f / scaleX, 1f / scaleY));
                _shaders[key] = shader;
                return shader;
            }
        }

        /// <summary>压入一层矩形裁剪。必须与 <see cref="Pop"/> 配对。</summary>
        public void PushClipRect(SKRect rect)
        {
            _clipDepth++;
            _canvas.Save();
            _canvas.ClipRect(rect);
        }

        /// <summary>撤销最近一层裁剪（配合 <see cref="PushClipRect"/>，也配合 <see cref="RotateAround"/> 之外的自存自取）。</summary>
        public void Pop()
        {
            if (_clipDepth <= 0)
            {
                throw new InvalidOperationException("clip 栈已空：Pop 多于 PushClipRect，画布状态会被恢复到绘制之外。");
            }

            _clipDepth--;
            _canvas.Restore();
        }

        /// <summary>
        /// 绕一点旋转后落笔，画完立刻还原。风杆、指北针、图章用这个。
        /// <para>
        /// <b>闭包里只准放几何</b>：文字经 canvas 旋转导出 PDF/SVG 后会退化成带变换的路径，
        /// 无法作为文本检索（与 <see cref="VerticalText"/> 注释里同一个坑）。要旋转的文字
        /// 由调用方逐字取位后直接落笔。
        /// </para>
        /// </summary>
        public void RotateAround(float degrees, SKPoint pivot, Action draw)
        {
            if (draw == null)
            {
                throw new ArgumentNullException(nameof(draw));
            }

            int save = _canvas.Save();
            try
            {
                _canvas.Translate(pivot.X, pivot.Y);
                _canvas.RotateDegrees(degrees);
                _canvas.Translate(-pivot.X, -pivot.Y);
                draw();
            }
            finally
            {
                _canvas.RestoreToCount(save);
            }
        }

        /// <summary>画一段带箭头的线：杆身从 a 到 b，实心三角箭尖落在 b。</summary>
        /// <param name="a">起点。</param>
        /// <param name="b">箭尖。</param>
        /// <param name="head">箭头长度（逻辑像素）。</param>
        /// <param name="color">颜色。</param>
        /// <param name="width">杆身线粗。给 0 表示只画箭头。</param>
        /// <param name="baseRatio">箭头底边半宽 / 箭头长。尺寸线用 <c>DimensionTokens.ArrowBaseRatio</c>，
        /// 风向杆末梢这类"扁箭头"可以给更小的值。</param>
        public void ArrowSegment(SKPoint a, SKPoint b, float head, SKColor color, float width,
                                 float baseRatio = 0.18f)
        {
            if (float.IsNaN(head) || head < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(head), "箭头长度必须是非负有限数，实到 " + head);
            }

            double dx = (double)b.X - a.X;
            double dy = (double)b.Y - a.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 1e-9)
            {
                // 零长杆：方向无从谈起，画不出箭头，直接什么都不落（不留一支乱指的箭头）。
                return;
            }

            double ux = dx / length;
            double uy = dy / length;
            if (width > 0f)
            {
                Line(a.X, a.Y, b.X, b.Y, color, width);
            }

            if (head <= 0f)
            {
                return;
            }

            float half = (float)(head * baseRatio);
            float backX = (float)(b.X - ux * head);
            float backY = (float)(b.Y - uy * head);
            FillPolygon(new[]
            {
                b,
                new SKPoint(backX + (float)(-uy * half), backY + (float)(ux * half)),
                new SKPoint(backX - (float)(-uy * half), backY - (float)(ux * half)),
            }, color);
        }

        public void Dispose()
        {
            if (_clipDepth != 0)
            {
                throw new InvalidOperationException(
                    "clip 栈未配对：仍有 " + _clipDepth + " 层 PushClipRect 没有对应的 Pop。");
            }

            foreach (SKPaint paint in _paints.Values)
            {
                paint.Dispose();
            }
            _paints.Clear();
            foreach (SKFont font in _fontObjects.Values)
            {
                font.Dispose();
            }
            _fontObjects.Clear();
            foreach (SKShader shader in _shaders.Values)
            {
                shader.Dispose();
            }
            _shaders.Clear();
        }
    }
}
