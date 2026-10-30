using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Avalonia
{
    /// <summary>
    /// Avalonia 宿主控件：把 <see cref="OODD.Charts.Chart"/> 光栅化成 <see cref="WriteableBitmap"/> 再贴进渲染上下文。
    /// <para>
    /// 帧位图按"目标设备像素宽"出图，<c>DrawImage</c> 时 1:1 落位，因此宿主缩放（含高 DPI）
    /// 不会让表单发虚；渲染 DPI 由客户区尺寸反推，<see cref="Dpi"/> 仅在 <see cref="ChartFit.Actual"/> 下参与折算。
    /// </para>
    /// </summary>
    public class ChartControl : Control, ICustomHitTest
    {
        private static Cursor? _handCursor;

        private WriteableBitmap? _frame;
        private Chart? _frameOwner;
        private int _framePixelWidth = -1;
        private readonly DispatcherTimer _animationTimer = new DispatcherTimer();
        private readonly ChartInteractionHost _interactionHost;

        /// <summary>构造控件并接上交互/动画管线。</summary>
        public ChartControl()
        {
            _interactionHost = new ChartInteractionHost(InvalidateChart, SyncAnimation);
            _animationTimer.Tick += (sender, args) => _interactionHost.Tick();
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

        /// <summary>
        /// 自定义命中测试：本控件没有 Background 可依赖，未画内容时也必须接得住指针。
        /// </summary>
        public bool HitTest(Point point)
        {
            return point.X >= 0d && point.Y >= 0d
                   && point.X <= Bounds.Width && point.Y <= Bounds.Height;
        }

        /// <summary>按交互层的动画开关启停帧定时器。</summary>
        private void SyncAnimation()
        {
            if (!_interactionHost.WantsAnimation)
            {
                _animationTimer.Stop();
                return;
            }

            TimeSpan interval = TimeSpan.FromMilliseconds(_interactionHost.FrameIntervalMilliseconds);
            if (_animationTimer.Interval != interval)
            {
                _animationTimer.Interval = interval;
            }
            _animationTimer.Start();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SyncAnimation();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _animationTimer.Stop();
        }

        /// <summary>要显示的表单。控件不接管其生命周期，由调用方 Dispose。</summary>
        public static readonly StyledProperty<Chart?> ChartProperty =
            AvaloniaProperty.Register<ChartControl, Chart?>(nameof(Chart));

        /// <summary>适配方式，默认等比贴合容器并居中。</summary>
        public static readonly StyledProperty<ChartFit> FitProperty =
            AvaloniaProperty.Register<ChartControl, ChartFit>(nameof(Fit), ChartFit.Fit);

        /// <summary>各自适配方式基础上的额外缩放倍率。</summary>
        public static readonly StyledProperty<double> ScaleFactorProperty =
            AvaloniaProperty.Register<ChartControl, double>(nameof(ScaleFactor), 1d);

        /// <summary>仅 <see cref="ChartFit.Actual"/> 生效的基准 DPI；0 表示 96（即 1 逻辑单位 = 1 DIP）。</summary>
        public static readonly StyledProperty<double> DpiProperty =
            AvaloniaProperty.Register<ChartControl, double>(nameof(Dpi), 0d);

        static ChartControl()
        {
            // 表单/缩放/DPI 变化影响测量与绘制，Fit 变化只影响绘制。
            AffectsMeasure<ChartControl>(ChartProperty, ScaleFactorProperty, DpiProperty);
            AffectsRender<ChartControl>(ChartProperty, FitProperty, ScaleFactorProperty, DpiProperty);
        }

        public Chart? Chart
        {
            get => GetValue(ChartProperty);
            set => SetValue(ChartProperty, value);
        }

        public ChartFit Fit
        {
            get => GetValue(FitProperty);
            set => SetValue(FitProperty, value);
        }

        public double ScaleFactor
        {
            get => GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        public double Dpi
        {
            get => GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        /// <summary>外部改过 Document/Style 之后调用：丢弃缓存帧并重绘。</summary>
        public void InvalidateChart()
        {
            Chart? chart = Chart;
            chart?.Invalidate();
            _frame?.Dispose();
            _frame = null;
            _frameOwner = null;
            _framePixelWidth = -1;
            InvalidateMeasure();
            InvalidateVisual();
        }

        /// <summary>换表单时丢弃旧帧（缓存按表单引用命中，故换引用即失效）。</summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ChartProperty)
            {
                _frame?.Dispose();
                _frame = null;
                _framePixelWidth = -1;
                _interactionHost.Chart = Chart;
            }
        }

        /// <summary>自然尺寸 = 96dpi 逻辑尺寸 × 缩放系数（Fit 模式下即表单原始版面）。</summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            Chart? chart = Chart;
            if (chart == null)
            {
                return new Size(0d, 0d);
            }

            SKSize logical = chart.MeasureSize();
            double scale = NativeScale();
            return new Size(logical.Width * scale, logical.Height * scale);
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            Chart? chart = Chart;
            if (chart == null)
            {
                return;
            }

            SKSize logical = chart.MeasureSize();
            if (logical.Width <= 0f || logical.Height <= 0f)
            {
                return;
            }

            double availableWidth = Bounds.Width;
            double availableHeight = Bounds.Height;
            if (availableWidth <= 0d || availableHeight <= 0d)
            {
                return;
            }

            ChartFit fit = Fit;
            double scale = fit == ChartFit.Fit
                ? Math.Min(availableWidth / logical.Width, availableHeight / logical.Height) * ScaleFactor
                : NativeScale();
            if (scale <= 0d)
            {
                return;
            }

            Size drawn = new Size(logical.Width * scale, logical.Height * scale);
            double deviceScale = RenderScaling();
            int pixelWidth = Math.Max(1, (int)Math.Round(drawn.Width * deviceScale));
            WriteableBitmap frame = EnsureFrame(chart, pixelWidth, logical.Width);

            Point origin = fit == ChartFit.Fit
                ? new Point((availableWidth - drawn.Width) / 2d, (availableHeight - drawn.Height) / 2d)
                : new Point(0d, 0d);
            context.DrawImage(frame, new Rect(origin, drawn));

            // 指针是 DIP、绘制也是 DIP，视口直接用 scale 与居中偏移。
            _interactionHost.Viewport = ChartViewport.FromPlacement(
                logical, (float)scale, (float)origin.X, (float)origin.Y);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            PointerPoint point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                Focus();
                _interactionHost.PointerDown(ToSkPoint(point.Position));
                e.Handled = true;
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            _interactionHost.PointerMove(ToSkPoint(e.GetCurrentPoint(this).Position));
            Cursor = _interactionHost.IsPointerOverInteractive ? HandCursor : null;
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (e.InitialPressMouseButton == MouseButton.Left)
            {
                _interactionHost.PointerUp(ToSkPoint(e.GetCurrentPoint(this).Position));
            }
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            _interactionHost.PointerLeave();
            Cursor = null;
        }

        private static SKPoint ToSkPoint(Point point)
        {
            return new SKPoint((float)point.X, (float)point.Y);
        }

        private static Cursor HandCursor => _handCursor ??= new Cursor(StandardCursorType.Hand);

        /// <summary>Actual 模式下的逻辑单位→DIP 换算比；Fit 模式返回 1，控件尺寸交给容器。</summary>
        private double NativeScale()
        {
            double baseline = Dpi > 0d ? Dpi / 96d : 1d;
            return Math.Max(ScaleFactor, 0d) * baseline;
        }

        /// <summary>当前宿主的 DIP→设备像素比；脱离视觉树（设计期/单测）时按 1:1。</summary>
        private double RenderScaling()
        {
            TopLevel? top = this.GetVisualRoot() as TopLevel;
            return top != null && top.RenderScaling > 0d ? top.RenderScaling : 1d;
        }

        private WriteableBitmap EnsureFrame(Chart chart, int pixelWidth, double logicalWidth)
        {
            if (_frame != null && ReferenceEquals(_frameOwner, chart) && _framePixelWidth == pixelWidth)
            {
                return _frame;
            }

            // 让 Skia 直接按目标设备像素宽出图，DrawImage 时 1:1，不经过 Avalonia 重采样。
            double dpi = 96d * pixelWidth / Math.Max(1d, logicalWidth);
            WriteableBitmap next = chart.ToWriteableBitmap(dpi);

            _frame?.Dispose();
            _frame = next;
            _frameOwner = chart;
            _framePixelWidth = pixelWidth;
            return next;
        }
    }
}
