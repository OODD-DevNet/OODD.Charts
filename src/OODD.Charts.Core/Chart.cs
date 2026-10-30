using System;
using System.IO;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts
{
    /// <summary>
    /// 表单门面。把 <see cref="ChartDocument"/> + <see cref="IChartTemplate"/> + <see cref="ChartStyle"/>
    /// 组合成一个可渲染对象。
    /// <para>
    /// 管线：<c>Measure()</c> 求解几何 → <c>DrawContent()</c> 往画布落笔 →
    /// 产物可以是 <see cref="SKPicture"/>（矢量，供任意宿主）、<see cref="SKImage"/>、
    /// 编码字节（PNG/JPEG/WebP）、PDF 或 SVG。
    /// </para>
    /// </summary>
    public sealed class Chart : IDisposable
    {
        /// <summary>
        /// 进程级共享字体解析器。<see cref="Chart"/> 不会 Dispose 它。
        /// 需要嵌入字体或多解析器时，给 <see cref="Fonts"/> 赋自己的实例。
        /// </summary>
        public static IFontResolver SharedFonts { get; } = new SystemFontResolver();

        private readonly ChartDocument _document;
        private readonly IChartTemplate _template;
        private ChartGeometry? _geometry;
        private bool _disposed;

        public Chart(ChartDocument document, IChartTemplate template)
            : this(document, template, new ChartStyle())
        {
        }

        public Chart(ChartDocument document, IChartTemplate template, ChartStyle style)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _template = template ?? throw new ArgumentNullException(nameof(template));
            _style = style ?? throw new ArgumentNullException(nameof(style));
        }

        private ChartStyle _style;

        public ChartDocument Document => _document;
        public IChartTemplate Template => _template;

        public ChartStyle Style
        {
            get => _style;
            set
            {
                _style = value ?? throw new ArgumentNullException(nameof(value));
                Invalidate();
            }
        }

        private IFontResolver _fonts = SharedFonts;

        /// <summary>字体解析器。赋值不会触发 Dispose 旧实例。</summary>
        public IFontResolver Fonts
        {
            get => _fonts;
            set
            {
                _fonts = value ?? throw new ArgumentNullException(nameof(value));
                Invalidate();
            }
        }

        /// <summary>版面几何，惰性求解并缓存。文档内容变更后需 <see cref="Invalidate"/>。</summary>
        public ChartGeometry Geometry
        {
            get
            {
                if (_geometry == null)
                {
                    _geometry = _template.Measure(_document, _style, _fonts);
                }
                return _geometry;
            }
        }

        /// <summary>丢弃缓存的几何。修改 Document/Style 后调用。</summary>
        public void Invalidate()
        {
            _geometry = null;
        }

        /// <summary>96dpi 逻辑尺寸。</summary>
        public SKSize MeasureSize()
        {
            SKRect bounds = Geometry.Bounds;
            return new SKSize(bounds.Width, bounds.Height);
        }

        // ------------------------------------------------------------------ 渲染

        /// <summary>
        /// 录制为矢量图。这是所有导出与宿主适配的唯一中间产物。
        /// <para>
        /// 注意：本方法结束时闸门即释放，拿到的 <see cref="SKPicture"/> 之后由宿主自己回放，
        /// 那次回放不在 <c>TextRenderGate</c> 覆盖范围内。若同时有别的线程导出 SVG，
        /// Skia 的字形反查可能让产物丢字（实测与处置见 ChartExport.WriteSvg）；
        /// 需要绝对保险就用 <see cref="Draw(SKCanvas)"/> / <see cref="RenderImage"/>。
        /// </para>
        /// </summary>
        public SKPicture Record()
        {
            return Record(1f);
        }

        /// <summary>
        /// 按目标设备缩放录制。图样填充的 tile 在录制时就按这个分辨率重画，
        /// 之后的 <c>canvas.Scale</c> 恰好把它还原成 1:1 落墨（见 <see cref="Painter"/>）。
        /// </summary>
        internal SKPicture Record(float patternDeviceScale)
        {
            ThrowIfDisposed();
            ChartGeometry geometry = Geometry;

            using (SKPictureRecorder recorder = new SKPictureRecorder())
            {
                SKCanvas canvas = recorder.BeginRecording(geometry.Bounds);
                using (Painter painter = new Painter(canvas, _style, _fonts))
                {
                    painter.SetPatternDeviceScale(patternDeviceScale);
                    _template.DrawContent(painter, geometry, _document);
                }
                SKPicture? picture = recorder.EndRecording();
                if (picture == null)
                {
                    throw new InvalidOperationException("录制失败：模板未产生任何绘制指令。");
                }
                return picture;
            }
        }

        /// <summary>1:1 画到宿主画布。</summary>
        public void Draw(SKCanvas canvas)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }
            // 录制+回放整段占住落字闸门：Skia 的字形栅格缓存不是线程安全的，
            // SVG 导出需要它排空（见 TextRenderGate）。
            TextRenderGate.EnterRender();
            try
            {
                using (SKPicture picture = Record())
                {
                    canvas.DrawPicture(picture);
                }
            }
            finally
            {
                TextRenderGate.ExitRender();
            }
        }

        /// <summary>等比缩放画进目标矩形（零适配路径，任何能拿到 SKCanvas 的宿主都可用）。</summary>
        public void Draw(SKCanvas canvas, SKRect target)
        {
            if (canvas == null)
            {
                throw new ArgumentNullException(nameof(canvas));
            }

            // 闸门从 Measure 之前罩起：版面求解里要量字宽，那也算一次落字动作。
            TextRenderGate.EnterRender();
            try
            {
                SKSize size = MeasureSize();
                if (size.Width <= 0f || size.Height <= 0f)
                {
                    return;
                }

                float scale = Math.Min(target.Width / size.Width, target.Height / size.Height);
                float drawnWidth = size.Width * scale;
                float drawnHeight = size.Height * scale;

                using (SKPicture picture = Record())
                {
                    int saved = canvas.Save();
                    canvas.Translate(target.Left + (target.Width - drawnWidth) / 2f,
                                     target.Top + (target.Height - drawnHeight) / 2f);
                    canvas.Scale(scale);
                    canvas.DrawPicture(picture);
                    canvas.RestoreToCount(saved);
                }
            }
            finally
            {
                TextRenderGate.ExitRender();
            }
        }

        /// <summary>
        /// 光栅化。DPI 通过 <c>canvas.Scale</c> 实现，<b>几何只按 96dpi 求解一次</b>，
        /// 因此高 DPI 输出与低 DPI 版面绝对对齐（设计决策 D7）。
        /// </summary>
        public SKImage RenderImage(float dpi = 96f)
        {
            ThrowIfDisposed();
            if (dpi <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(dpi));
            }

            TextRenderGate.EnterRender();
            try
            {
                SKSize size = MeasureSize();
                float scale = dpi / 96f;
                SKImageInfo info = new SKImageInfo(
                    Math.Max(1, (int)Math.Ceiling(size.Width * scale)),
                    Math.Max(1, (int)Math.Ceiling(size.Height * scale)),
                    SKColorType.Bgra8888,
                    SKAlphaType.Premul);

                // 注意：不要用 SKImage.FromPicture —— SkiaSharp 3.116.1 上会抛不可捕获的
                // AccessViolationException。必须走 Surface + DrawPicture。见 docs/02 §7.2。
                using (SKSurface? surface = SKSurface.Create(info))
                {
                    if (surface == null)
                    {
                        throw new InvalidOperationException("无法创建 " + info.Width + "x" + info.Height + " 渲染面。");
                    }

                    SKCanvas canvas = surface.Canvas;
                    canvas.Clear(_style.Colors.Background);
                    canvas.Scale(scale);
                    using (SKPicture picture = Record(scale))
                    {
                        canvas.DrawPicture(picture);
                    }

                    SKImage? image = surface.Snapshot();
                    if (image == null)
                    {
                        throw new InvalidOperationException("Snapshot 返回空。");
                    }
                    return image;
                }
            }
            finally
            {
                TextRenderGate.ExitRender();
            }
        }

        // ------------------------------------------------------------------ 导出

        public byte[] Encode(ChartImageFormat format, float dpi = 96f, int quality = 100)
        {
            using (SKImage image = RenderImage(dpi))
            {
                return ChartExport.EncodeImage(image, format, quality);
            }
        }

        public void WritePng(Stream output, float dpi = 96f) => ChartExport.WriteImage(this, output, ChartImageFormat.Png, dpi, 100);
        public void WriteJpeg(Stream output, float dpi = 96f, int quality = 90) => ChartExport.WriteImage(this, output, ChartImageFormat.Jpeg, dpi, quality);
        public void WritePdf(Stream output, PdfOptions? options = null) => ChartExport.WritePdf(this, output, options ?? PdfOptions.Auto);
        public void WriteSvg(Stream output) => ChartExport.WriteSvg(this, output);

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Chart));
            }
        }

        public void Dispose()
        {
            _disposed = true;
            _geometry = null;
        }
    }
}
