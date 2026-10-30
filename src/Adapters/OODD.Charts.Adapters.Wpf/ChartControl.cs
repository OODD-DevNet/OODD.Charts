using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Wpf
{
    /// <summary>
    /// WPF 宿主元素：把 <see cref="OODD.Charts.Chart"/> 光栅化后 <c>DrawImage</c> 到渲染上下文。
    /// <para>
    /// 帧位图按"目标设备像素"出图，因此 <c>LayoutTransform</c> / <c>BitmapScalingMode</c>
    /// 不会让表单发虚；宿主 DPI 变化时自动重新出图。
    /// </para>
    /// </summary>
    public class ChartControl : FrameworkElement
    {
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
            Loaded += (sender, args) => SyncAnimation();
            Unloaded += (sender, args) => _animationTimer.Stop();
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

        /// <summary>要显示的表单。控件不接管其生命周期。</summary>
        public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
            nameof(Chart),
            typeof(Chart),
            typeof(ChartControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnChartChanged));

        /// <summary>适配方式，默认等比贴合容器。</summary>
        public static readonly DependencyProperty FitProperty = DependencyProperty.Register(
            nameof(Fit),
            typeof(ChartFit),
            typeof(ChartControl),
            new FrameworkPropertyMetadata(ChartFit.Fit, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>各自适配方式基础上的额外缩放倍率。</summary>
        public static readonly DependencyProperty ScaleFactorProperty = DependencyProperty.Register(
            nameof(ScaleFactor),
            typeof(double),
            typeof(ChartControl),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender),
            ValidatePositive);

        /// <summary>仅 <see cref="ChartFit.Actual"/> 生效的基准 DPI；0 表示 96（即 1 逻辑单位 = 1 DIP）。</summary>
        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register(
            nameof(Dpi),
            typeof(double),
            typeof(ChartControl),
            new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender),
            ValidateNonNegative);

        private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartControl control = (ChartControl)d;
            control._interactionHost.Chart = (Chart?)e.NewValue;
            control._frameOwner = null;
            control._framePixelWidth = -1;
        }

        private static bool ValidatePositive(object value)
        {
            double number = (double)value;
            return !double.IsNaN(number) && !double.IsInfinity(number) && number > 0d;
        }

        private static bool ValidateNonNegative(object value)
        {
            double number = (double)value;
            return !double.IsNaN(number) && !double.IsInfinity(number) && number >= 0d;
        }

        public Chart? Chart
        {
            get => (Chart?)GetValue(ChartProperty);
            set => SetValue(ChartProperty, value);
        }

        public ChartFit Fit
        {
            get => (ChartFit)GetValue(FitProperty);
            set => SetValue(FitProperty, value);
        }

        public double ScaleFactor
        {
            get => (double)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        public double Dpi
        {
            get => (double)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        /// <summary>外部改过 Document/Style 之后调用：丢弃缓存帧并重绘。</summary>
        public void InvalidateChart()
        {
            Chart? chart = Chart;
            chart?.Invalidate();
            _frameOwner = null;
            _framePixelWidth = -1;
            InvalidateMeasure();
            InvalidateVisual();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Chart? chart = Chart;
            if (chart == null)
            {
                return new Size(0d, 0d);
            }

            SKSize logical = chart.MeasureSize();
            return new Size(logical.Width * NativeScale(), logical.Height * NativeScale());
        }

        /// <summary>Actual 模式下的 DIP/逻辑单位换算比；Fit 模式返回 1，控件尺寸交给容器。</summary>
        private double NativeScale()
        {
            double baseline = Dpi > 0d ? Dpi / 96d : 1d;
            return Math.Max(ScaleFactor, 0d) * baseline;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            Chart? chart = Chart;
            SKSize logical = default;
            if (chart == null || (logical = chart.MeasureSize()).Width <= 0f || logical.Height <= 0f)
            {
                return;
            }

            double availableWidth = RenderSize.Width;
            double availableHeight = RenderSize.Height;
            if (availableWidth <= 0d || availableHeight <= 0d)
            {
                return;
            }

            double fit = Fit == ChartFit.Fit
                ? Math.Min(availableWidth / logical.Width, availableHeight / logical.Height) * ScaleFactor
                : NativeScale();
            if (fit <= 0d)
            {
                return;
            }

            Size drawn = new Size(logical.Width * fit, logical.Height * fit);
            Vector device = DevicePixelScale();
            int pixelWidth = Math.Max(1, (int)Math.Round(drawn.Width * device.X));
            ImageSource frame = EnsureFrame(chart, pixelWidth, logical.Width);

            Point origin = Fit == ChartFit.Fit
                ? new Point((availableWidth - drawn.Width) / 2d, (availableHeight - drawn.Height) / 2d)
                : new Point(0d, 0d);
            dc.DrawImage(frame, new Rect(origin, drawn));

            // 指针是 DIP、绘制也是 DIP，视口直接用 fit 与居中偏移。
            _interactionHost.Viewport = ChartViewport.FromPlacement(
                logical, (float)fit, (float)origin.X, (float)origin.Y);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Left)
            {
                Focus();
                _interactionHost.PointerDown(ToSkPoint(e.GetPosition(this)));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _interactionHost.PointerMove(ToSkPoint(e.GetPosition(this)));
            Cursor = _interactionHost.IsPointerOverInteractive ? Cursors.Hand : null;
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton == MouseButton.Left)
            {
                _interactionHost.PointerUp(ToSkPoint(e.GetPosition(this)));
            }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            _interactionHost.PointerLeave();
            Cursor = null;
        }

        private static SKPoint ToSkPoint(Point point)
        {
            return new SKPoint((float)point.X, (float)point.Y);
        }

        /// <summary>当前宿主的 DIP→设备像素比；脱离视觉树（设计期/单测）时按 1:1。</summary>
        private Vector DevicePixelScale()
        {
            PresentationSource? source = PresentationSource.FromVisual(this);
            CompositionTarget? target = source?.CompositionTarget;
            if (target == null)
            {
                return new Vector(1d, 1d);
            }
            return new Vector(target.TransformToDevice.M11, target.TransformToDevice.M22);
        }

        private ImageSource EnsureFrame(Chart chart, int pixelWidth, double logicalWidth)
        {
            if (_frame != null && ReferenceEquals(_frameOwner, chart) && _framePixelWidth == pixelWidth)
            {
                return _frame;
            }

            // 让 Skia 直接按目标设备像素宽出图，DrawImage 时 1:1，不经过 WPF 重采样。
            double dpi = 96d * pixelWidth / Math.Max(1d, logicalWidth);
            WriteableBitmap next = chart.ToWriteableBitmap(dpi);

            _frame = next;
            _frameOwner = chart;
            _framePixelWidth = pixelWidth;
            return next;
        }
    }
}
