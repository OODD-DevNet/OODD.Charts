using System;
using Eto.Drawing;
using Eto.Forms;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Eto
{
    /// <summary>
    /// Eto.Forms 宿主控件：把 <see cref="OODD.Charts.Chart"/> 光栅化成位图后贴进 <see cref="Drawable"/>。
    /// <para>
    /// 帧位图按“目标逻辑尺寸 × 宿主像素比”出像素，贴到同尺寸的逻辑矩形上，
    /// 在 96dpi 宿主机上即 1:1 位块传输，缩放不经过后端重采样。
    /// 与 WinForms/WPF 适配器不同，这里缓存帧并在 <see cref="InvalidateChart()"/> 或尺寸变化时重出图，
    /// 因为 Skia 光栅化比 GDI+ 贴图贵得多。
    /// </para>
    /// </summary>
    public class ChartControl : Drawable
    {
        private Chart? _chart;
        private Bitmap? _frame;
        private Chart? _frameOwner;
        private int _framePixelWidth = -1;
        private ChartFit _fit = ChartFit.Fit;
        private double _scaleFactor = 1d;
        private double _dpi;
        private readonly UITimer _animationTimer;
        private readonly ChartInteractionHost _interactionHost;

        public ChartControl()
        {
            BackgroundColor = Colors.White;
            Paint += HandlePaint;

            _interactionHost = new ChartInteractionHost(InvalidateChart, SyncAnimation);
            _animationTimer = new UITimer();
            _animationTimer.Elapsed += (sender, args) => _interactionHost.Tick();

            MouseDown += HandleMouseDown;
            MouseMove += HandleMouseMove;
            MouseUp += HandleMouseUp;
            MouseLeave += HandleMouseLeave;
        }

        /// <summary>
        /// 交互（选中/悬停/动画等）。赋 null 退回纯静态显示；换表单时自动重新挂载。
        /// 雷达等交互可由 <c>RadarInteraction.TryCreate(chart)</c> 创建，非雷达表单返回 null。
        /// </summary>
        public IChartInteraction? Interaction
        {
            get => _interactionHost.Interaction;
            set => _interactionHost.Interaction = value;
        }

        /// <summary>按交互层的动画开关启停帧定时器（Eto 的间隔单位是秒）。</summary>
        private void SyncAnimation()
        {
            if (!_interactionHost.WantsAnimation)
            {
                _animationTimer.Stop();
                return;
            }

            _animationTimer.Interval = Math.Max(1, _interactionHost.FrameIntervalMilliseconds) / 1000d;
            _animationTimer.Start();
        }

        /// <summary>要显示的表单。控件不接管其生命周期，由调用方 Dispose。</summary>
        public Chart? Chart
        {
            get => _chart;
            set
            {
                if (ReferenceEquals(_chart, value))
                {
                    return;
                }
                _chart = value;
                _interactionHost.Chart = value;
                InvalidateFrame();
                Invalidate(true);
            }
        }

        /// <summary>
        /// 适配方式。<see cref="ChartFit.Fit"/> 等比缩到客户区并居中；
        /// <see cref="ChartFit.Actual"/> 按 <see cref="Dpi"/> 原尺寸绘制，超出裁切。
        /// </summary>
        public ChartFit Fit
        {
            get => _fit;
            set
            {
                if (_fit == value)
                {
                    return;
                }
                _fit = value;
                Invalidate(true);
            }
        }

        /// <summary>在各自适配方式基础上的额外缩放倍率（1.0 = 不缩放）。</summary>
        public double ScaleFactor
        {
            get => _scaleFactor;
            set
            {
                if (value <= 0d)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (_scaleFactor == value)
                {
                    return;
                }
                _scaleFactor = value;
                Invalidate(true);
            }
        }

        /// <summary>
        /// 仅 <see cref="ChartFit.Actual"/> 模式生效的渲染 DPI；0 表示跟随宿主 DPI。
        /// Fit 模式下渲染 DPI 由客户区尺寸反推，无需设置。
        /// </summary>
        public double Dpi
        {
            get => _dpi;
            set
            {
                if (value < 0d)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (_dpi == value)
                {
                    return;
                }
                _dpi = value;
                InvalidateFrame();
                Invalidate(true);
            }
        }

        /// <summary>外部改过 Document/Style 之后调用：丢弃缓存帧并重绘。</summary>
        public void InvalidateChart()
        {
            _chart?.Invalidate();
            InvalidateFrame();
            Invalidate(true);
        }

        private void InvalidateFrame()
        {
            _frameOwner = null;
            _framePixelWidth = -1;
        }

        private void HandlePaint(object? sender, PaintEventArgs e)
        {
            Chart? chart = _chart;
            if (chart == null || e == null)
            {
                return;
            }

            Graphics graphics = e.Graphics;
            SKSize logical = chart.MeasureSize();
            if (logical.Width <= 0f || logical.Height <= 0f)
            {
                return;
            }

            SizeF area = new SizeF(Math.Max(1, Width), Math.Max(1, Height));

            // Eto 的绘制坐标是逻辑单位（点），Graphics.DPI 已含宿主缩放比，
            // Actual 模式按它出像素即与设备 1:1。
            double scale = _fit == ChartFit.Fit
                ? Math.Min(area.Width / logical.Width, area.Height / logical.Height) * _scaleFactor
                : ((_dpi > 0d ? _dpi : graphics.DPI) / 96d) * _scaleFactor;
            if (scale <= 0d)
            {
                return;
            }

            double drawnWidth = logical.Width * scale;
            double drawnHeight = logical.Height * scale;
            int pixelWidth = Math.Max(1, (int)Math.Round(drawnWidth * DeviceScale(graphics)));
            Bitmap frame = EnsureFrame(chart, pixelWidth, logical.Width);

            PointF origin = _fit == ChartFit.Fit
                ? new PointF((float)((area.Width - drawnWidth) / 2d), (float)((area.Height - drawnHeight) / 2d))
                : new PointF(0f, 0f);

            // EnsureFrame 出图的像素宽恒为 pixelWidth，即 drawnWidth × 设备比，
            // 后端把逻辑矩形映射回设备后是 1:1 位块传输，不经重采样。
            graphics.DrawImage(frame, new RectangleF(origin, new SizeF((float)drawnWidth, (float)drawnHeight)));

            // 指针坐标是逻辑点，绘制也按逻辑点落位，视口直接用 scale 与居中偏移。
            _interactionHost.Viewport = ChartViewport.FromPlacement(
                logical, (float)scale, origin.X, origin.Y);
        }

        private void HandleMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Buttons.HasFlag(MouseButtons.Primary))
            {
                Focus();
                _interactionHost.PointerDown(ToSkPoint(e.Location));
            }
        }

        private void HandleMouseMove(object? sender, MouseEventArgs e)
        {
            _interactionHost.PointerMove(ToSkPoint(e.Location));
            Cursor = _interactionHost.IsPointerOverInteractive ? Cursors.Pointer : Cursors.Default;
        }

        private void HandleMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Buttons.HasFlag(MouseButtons.Primary))
            {
                _interactionHost.PointerUp(ToSkPoint(e.Location));
            }
        }

        private void HandleMouseLeave(object? sender, MouseEventArgs e)
        {
            _interactionHost.PointerLeave();
            Cursor = Cursors.Default;
        }

        private static SKPoint ToSkPoint(PointF point)
        {
            return new SKPoint(point.X, point.Y);
        }

        /// <summary>宿主逻辑单位→设备像素比；设计期或后端缺省按 1:1。</summary>
        private static double DeviceScale(Graphics graphics)
        {
            float pixelsPerPoint = graphics.PixelsPerPoint;
            return pixelsPerPoint > 0f ? pixelsPerPoint : 1d;
        }

        private Bitmap EnsureFrame(Chart chart, int pixelWidth, float logicalWidth)
        {
            if (_frame != null && ReferenceEquals(_frameOwner, chart) && _framePixelWidth == pixelWidth)
            {
                return _frame;
            }

            // 让 Skia 直接按目标设备像素宽出图，贴图时不再二次重采样。
            double dpi = 96d * pixelWidth / Math.Max(1d, logicalWidth);
            Bitmap next = chart.ToEtoBitmap(dpi);

            _frame?.Dispose();
            _frame = next;
            _frameOwner = chart;
            _framePixelWidth = pixelWidth;
            return next;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _interactionHost.Dispose();
                _frame?.Dispose();
                _frame = null;
                _frameOwner = null;
            }
            base.Dispose(disposing);
        }
    }
}
