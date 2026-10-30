using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;
using Windows.Foundation;

namespace OODD.Charts.Adapters.Uno
{
    /// <summary>
    /// Uno Platform 宿主控件：把 <see cref="OODD.Charts.Chart"/> 光栅化后交给内部
    /// <see cref="Image"/> 元素显示。
    /// <para>
    /// 为什么不是 WPF 那种 <c>OnRender(DrawingContext)</c>：Uno 走的是 UWP/WinUI 契约，
    /// <see cref="FrameworkElement"/> 上没有 <c>OnRender</c>，自绘要么用 Composition 绘制面，
    /// 要么让布局系统去画位图。表单是"一次算好、整帧刷新"的静态内容，用
    /// <see cref="Image"/> 吃 <see cref="WriteableBitmap"/> 最省，也天然覆盖 Skia /
    /// Android / iOS / WASM 各个后端。
    /// </para>
    /// <para>
    /// 清晰度策略：<c>Image</c> 会把位图像素数当逻辑尺寸用，直接放大会发虚。这里反过来做——
    /// 先按"目标设备像素宽"（逻辑尺寸 × 缩放 × <c>RasterizationScale</c>）出图，再把
    /// 控件尺寸设成 <c>位图像素数 ÷ RasterizationScale</c>，配 <see cref="Stretch.Fill"/>，
    /// 让上屏时严格 1:1，Uno 侧不发生重采样。
    /// </para>
    /// <para>
    /// 线程约束：与所有 XAML 元素一样只能在 UI 线程构造与更新；属性变更回调里主动
    /// <see cref="UIElement.InvalidateMeasure"/> / <see cref="UIElement.InvalidateArrange"/>，
    /// 因为 WinUI 契约的 <see cref="PropertyMetadata"/> 没有 WPF 的
    /// <c>FrameworkPropertyMetadataOptions.AffectsMeasure</c> 可用。
    /// </para>
    /// </summary>
    public class ChartControl : Grid
    {
        private readonly Image _image = new Image();
        private WriteableBitmap? _frame;
        private Chart? _frameOwner;
        private int _framePixelWidth = -1;
        private double _appliedWidth = -1d;
        private double _appliedHeight = -1d;
        private readonly DispatcherTimer _animationTimer = new DispatcherTimer();
        private readonly ChartInteractionHost _interactionHost;

        /// <summary>要显示的表单。控件不接管其生命周期。</summary>
        public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
            nameof(Chart),
            typeof(Chart),
            typeof(ChartControl),
            new PropertyMetadata(null, OnChartChanged));

        /// <summary>适配方式，默认等比贴合容器。</summary>
        public static readonly DependencyProperty FitProperty = DependencyProperty.Register(
            nameof(Fit),
            typeof(ChartFit),
            typeof(ChartControl),
            new PropertyMetadata(ChartFit.Fit, OnLayoutHintChanged));

        /// <summary>各自适配方式基础上的额外缩放倍率。</summary>
        public static readonly DependencyProperty ScaleFactorProperty = DependencyProperty.Register(
            nameof(ScaleFactor),
            typeof(double),
            typeof(ChartControl),
            new PropertyMetadata(1d, OnScaleFactorChanged));

        /// <summary>仅 <see cref="ChartFit.Actual"/> 生效的基准 DPI；0 表示 96，即 1 逻辑单位 = 1 Uno 逻辑像素。</summary>
        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register(
            nameof(Dpi),
            typeof(double),
            typeof(ChartControl),
            new PropertyMetadata(0d, OnLayoutHintChanged));

        /// <summary>初始化内部图像元素；对齐方式随 <see cref="Fit"/> 在出图时设定。</summary>
        public ChartControl()
        {
            _image.Stretch = Stretch.Fill;
            _image.IsHitTestVisible = false;
            Children.Add(_image);

            _interactionHost = new ChartInteractionHost(InvalidateChart, SyncAnimation);
            _animationTimer.Tick += (sender, args) => _interactionHost.Tick();

            // Grid 默认区域不参与命中测试；透明底色保证指针事件能落到本控件。
            Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            PointerPressed += HandlePointerPressed;
            PointerMoved += HandlePointerMoved;
            PointerReleased += HandlePointerReleased;
            PointerExited += HandlePointerExited;

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

        /// <summary>要显示的表单。</summary>
        public Chart? Chart
        {
            get => (Chart?)GetValue(ChartProperty);
            set => SetValue(ChartProperty, value);
        }

        /// <summary>适配方式。</summary>
        public ChartFit Fit
        {
            get => (ChartFit)GetValue(FitProperty);
            set => SetValue(FitProperty, value);
        }

        /// <summary>额外缩放倍率。</summary>
        public double ScaleFactor
        {
            get => (double)GetValue(ScaleFactorProperty);
            set => SetValue(ScaleFactorProperty, value);
        }

        /// <summary><see cref="ChartFit.Actual"/> 模式的基准 DPI，0 表示 96。</summary>
        public double Dpi
        {
            get => (double)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartControl control = (ChartControl)d;
            control._interactionHost.Chart = (Chart?)e.NewValue;
            control._frameOwner = null;
            control._framePixelWidth = -1;
            control.InvalidateMeasure();
            control.InvalidateArrange();
        }

        private static void OnLayoutHintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartControl control = (ChartControl)d;
            control.InvalidateMeasure();
            control.InvalidateArrange();
        }

        private static void OnScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((double)e.NewValue <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(ScaleFactor));
            }

            OnLayoutHintChanged(d, e);
        }

        /// <summary>外部改过 Document/Style 之后调用：丢弃缓存帧并重绘。</summary>
        public void InvalidateChart()
        {
            Chart? chart = Chart;
            if (chart != null)
            {
                chart.Invalidate();
            }

            _frameOwner = null;
            _framePixelWidth = -1;
            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            Chart? chart = Chart;
            if (chart == null)
            {
                _image.Width = 0d;
                _image.Height = 0d;
                return base.MeasureOverride(availableSize);
            }

            SKSize logical = chart.MeasureSize();
            Size natural = new Size(logical.Width * NativeScale(), logical.Height * NativeScale());
            UpdateFrame(chart, logical, Constrain(availableSize, natural));

            // 子元素刚被设了新尺寸，让它按显式宽高重测一遍，本次 arrange 就能到位。
            base.MeasureOverride(availableSize);
            return natural;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Chart? chart = Chart;
            if (chart != null && finalSize.Width > 0d && finalSize.Height > 0d)
            {
                SKSize logical = chart.MeasureSize();
                if (logical.Width > 0f && logical.Height > 0f)
                {
                    UpdateFrame(chart, logical, finalSize);
                }
            }

            return base.ArrangeOverride(finalSize);
        }

        /// <summary>Actual 模式下的逻辑单位→逻辑像素换算比；Fit 模式返回 1，控件尺寸交给容器。</summary>
        private double NativeScale()
        {
            double baseline = Dpi > 0d ? Dpi / 96d : 1d;
            return Math.Max(ScaleFactor, 0d) * baseline;
        }

        /// <summary>无穷约束（滚动容器）时退回自然尺寸。</summary>
        private static Size Constrain(Size availableSize, Size natural)
        {
            double width = IsUsable(availableSize.Width) ? availableSize.Width : natural.Width;
            double height = IsUsable(availableSize.Height) ? availableSize.Height : natural.Height;
            return new Size(width, height);
        }

        private static bool IsUsable(double value)
        {
            return value > 0d && !double.IsPositiveInfinity(value) && !double.IsNaN(value);
        }

        /// <summary>当前宿主的比例尺；未挂到视觉树（设计期/单测）时按 1:1。</summary>
        private double DevicePixelScale()
        {
            XamlRoot? root = XamlRoot;
            if (root == null)
            {
                return 1d;
            }

            double scale = root.RasterizationScale;
            return scale > 0d && !double.IsNaN(scale) ? scale : 1d;
        }

        /// <summary>按适配算出目标设备像素宽，出图并写回内部图像元素。</summary>
        private void UpdateFrame(Chart chart, SKSize logical, Size box)
        {
            if (logical.Width <= 0f || logical.Height <= 0f || !IsUsable(box.Width) || !IsUsable(box.Height))
            {
                return;
            }

            double fit = Fit == ChartFit.Fit
                ? Math.Min(box.Width / logical.Width, box.Height / logical.Height) * Math.Max(ScaleFactor, 0d)
                : NativeScale();
            if (fit <= 0d)
            {
                return;
            }

            double device = DevicePixelScale();
            int pixelWidth = Math.Max(1, (int)Math.Round(logical.Width * fit * device));
            WriteableBitmap frame = EnsureFrame(chart, pixelWidth, logical.Width);

            if (!ReferenceEquals(_image.Source, frame))
            {
                _image.Source = frame;
            }

            // 位图像素数 ÷ 比例尺 = 逻辑尺寸，保证 Fill 时逐像素 1:1 上屏。
            double width = frame.PixelWidth / device;
            double height = frame.PixelHeight / device;
            if (width != _appliedWidth || height != _appliedHeight)
            {
                _appliedWidth = width;
                _appliedHeight = height;
                _image.Width = width;
                _image.Height = height;
            }

            if (Fit == ChartFit.Fit)
            {
                _image.HorizontalAlignment = HorizontalAlignment.Center;
                _image.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                _image.HorizontalAlignment = HorizontalAlignment.Left;
                _image.VerticalAlignment = VerticalAlignment.Top;
            }

            // 指针是 DIP，图像尺寸也是 DIP：Fit 时图像在容器内居中，Actual 时贴左上。
            double offsetX = Fit == ChartFit.Fit ? Math.Max(0d, box.Width - width) / 2d : 0d;
            double offsetY = Fit == ChartFit.Fit ? Math.Max(0d, box.Height - height) / 2d : 0d;
            _interactionHost.Viewport = ChartViewport.FromPlacement(
                logical, (float)(width / logical.Width), (float)offsetX, (float)offsetY);
        }

        private void HandlePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                _interactionHost.PointerDown(ToSkPoint(point.Position));
                e.Handled = true;
            }
        }

        private void HandlePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            _interactionHost.PointerMove(ToSkPoint(e.GetCurrentPoint(this).Position));
        }

        private void HandlePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _interactionHost.PointerUp(ToSkPoint(e.GetCurrentPoint(this).Position));
        }

        private void HandlePointerExited(object sender, PointerRoutedEventArgs e)
        {
            _interactionHost.PointerLeave();
        }

        private static SKPoint ToSkPoint(Windows.Foundation.Point point)
        {
            return new SKPoint((float)point.X, (float)point.Y);
        }

        /// <summary>按 chart 引用 + 目标设备像素宽缓存帧，两者任一变化才重新光栅化。</summary>
        private WriteableBitmap EnsureFrame(Chart chart, int pixelWidth, double logicalWidth)
        {
            if (_frame != null && ReferenceEquals(_frameOwner, chart) && _framePixelWidth == pixelWidth)
            {
                return _frame;
            }

            // 让 Skia 直接按目标设备像素宽出图，Uno 侧 1:1 显示，不经过重采样。
            double dpi = 96d * pixelWidth / Math.Max(1d, logicalWidth);
            WriteableBitmap next = chart.ToWriteableBitmap(dpi);

            _frame = next;
            _frameOwner = chart;
            _framePixelWidth = pixelWidth;
            return next;
        }
    }
}
