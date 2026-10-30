using System;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Size = Windows.Foundation.Size;
using Rect = Windows.Foundation.Rect;

namespace OODD.Charts.Adapters.WinUi
{
    /// <summary>
    /// WinUI 3 宿主元素：把 <see cref="OODD.Charts.Chart"/> 光栅化后经
    /// <see cref="WinUiChartExtensions.ToWriteableBitmap(Raster,double)"/> 灌进内嵌
    /// <see cref="Image"/> 显示。
    /// <para>
    /// 为什么不是自绘：WinUI 的 <see cref="FrameworkElement"/> 没有 WPF 的
    /// <c>OnRender(DrawingContext)</c>，自绘要起 <c>Composition</c> 可视 + InteropSurface，
    /// 复杂度高且同样受 UI 线程约束。这里选最简可行方案：控件持有一棵
    /// <see cref="Canvas"/> + 一个 <see cref="Image"/> 子元素，位图由 Skia 出，
    /// XAML 管线只做 1:1 摆放、不做重采样。
    /// </para>
    /// <para>
    /// 出图按"目标物理像素"进行：<c>渲染 DPI = 96 × 期望 DIP 尺寸 / 逻辑尺寸 × 宿主缩放</c>，
    /// 摆回视觉树时再除以宿主缩放，保证 1 位图像素 = 1 物理像素，表格细线在高缩放下不发虚。
    /// </para>
    /// <para>
    /// 线程约束：与所有 XAML 元素一样，本控件只能在 UI 线程创建与更新；
    /// 后台线程改 <see cref="Chart"/> 后需经 <c>DispatcherQueue.TryEnqueue</c> 回 UI 线程再调
    /// <see cref="InvalidateChart"/>。
    /// </para>
    /// </summary>
    public class ChartControl : FrameworkElement
    {
        private readonly Canvas _root = new Canvas();
        private readonly Image _image = new Image();

        private WriteableBitmap? _frame;
        private Chart? _frameOwner;
        private int _frameRenderDpi = -1;
        private double _lastArrangeWidth = -1d;
        private double _lastArrangeHeight = -1d;
        private readonly DispatcherTimer _animationTimer = new DispatcherTimer();
        private readonly ChartInteractionHost _interactionHost;

        /// <summary>构造宿主元素，并把承载位图的 <see cref="Image"/> 挂进子树。</summary>
        public ChartControl()
        {
            _interactionHost = new ChartInteractionHost(InvalidateChart, SyncAnimation);
            _animationTimer.Tick += (sender, args) => _interactionHost.Tick();

            // Canvas 默认不参与命中测试；透明底色让指针事件能冒泡到根，交互才收得到。
            _root.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _root.Children.Add(_image);

            _root.PointerPressed += HandlePointerPressed;
            _root.PointerMoved += HandlePointerMoved;
            _root.PointerReleased += HandlePointerReleased;
            _root.PointerExited += HandlePointerExited;

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

        /// <summary>要显示的表单。控件不接管其生命周期，由调用方 Dispose。</summary>
        public static readonly DependencyProperty ChartProperty = DependencyProperty.Register(
            nameof(Chart),
            typeof(Chart),
            typeof(ChartControl),
            new PropertyMetadata(null, OnChartChanged));

        /// <summary>适配方式，默认 <see cref="ChartFit.Fit"/>：等比贴合容器并居中。</summary>
        public static readonly DependencyProperty FitProperty = DependencyProperty.Register(
            nameof(Fit),
            typeof(ChartFit),
            typeof(ChartControl),
            new PropertyMetadata(ChartFit.Fit, OnLayoutInvalidated));

        /// <summary>各自适配方式基础上的额外缩放倍率；必须为正。</summary>
        public static readonly DependencyProperty ScaleFactorProperty = DependencyProperty.Register(
            nameof(ScaleFactor),
            typeof(double),
            typeof(ChartControl),
            new PropertyMetadata(1d, OnScaleFactorChanged));

        /// <summary>
        /// 仅 <see cref="ChartFit.Actual"/> 生效的基准 DPI；0 表示跟随宿主 DPI。
        /// Fit 模式下渲染 DPI 由目标物理像素宽反推，无需设置。
        /// </summary>
        public static readonly DependencyProperty DpiProperty = DependencyProperty.Register(
            nameof(Dpi),
            typeof(double),
            typeof(ChartControl),
            new PropertyMetadata(0d, OnLayoutInvalidated));

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

        /// <summary><see cref="ChartFit.Actual"/> 模式的基准 DPI。</summary>
        public double Dpi
        {
            get => (double)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
        }

        /// <summary>外部改过 Document/Style 之后调用：丢弃缓存帧并重绘。</summary>
        public void InvalidateChart()
        {
            Chart?.Invalidate();
            _frameOwner = null;
            _frameRenderDpi = -1;
            InvalidateMeasure();
            InvalidateArrange();
        }

        private static void OnChartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartControl control = (ChartControl)d;
            control._interactionHost.Chart = (Chart?)e.NewValue;
            control._frameOwner = null;
            control._frameRenderDpi = -1;
            control.InvalidateMeasure();
            control.InvalidateArrange();
        }

        private static void OnScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if ((double)e.NewValue <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(ScaleFactor));
            }
            ((ChartControl)d).InvalidateMeasure();
            ((ChartControl)d).InvalidateArrange();
        }

        private static void OnLayoutInvalidated(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ChartControl control = (ChartControl)d;
            control.InvalidateMeasure();
            control.InvalidateArrange();
        }

        /// <summary>
        /// 期望尺寸（有效像素/DIP）：Actual 模式 = 逻辑尺寸 × (DPI/96) × ScaleFactor；
        /// Fit 模式 = 逻辑尺寸 × ScaleFactor 作为理想值，并按父级可用空间收缩。
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            Chart? chart = Chart;
            if (chart == null)
            {
                return new Size(0d, 0d);
            }

            SKSize logical = chart.MeasureSize();
            if (logical.Width <= 0f || logical.Height <= 0f)
            {
                return new Size(0d, 0d);
            }

            double scale = NativeScale();
            double width = logical.Width * scale;
            double height = logical.Height * scale;
            double measuredWidth = double.IsPositiveInfinity(availableSize.Width) ? width : Math.Min(width, Math.Max(0d, availableSize.Width));
            double measuredHeight = double.IsPositiveInfinity(availableSize.Height) ? height : Math.Min(height, Math.Max(0d, availableSize.Height));
            return new Size(measuredWidth, measuredHeight);
        }

        /// <summary>
        /// 出帧并摆放 <see cref="Image"/>：Fit 等比居中，Actual 左上角 1:1（超出用 <see cref="UIElement.Clip"/> 裁切）。
        /// WinUI 的 FrameworkElement 没有 SizeChanged 事件，尺寸变化必然伴随第二次 Arrange，
        /// 因此这里比较上次 Arrange 尺寸来决定是否重出帧，替代 WPF 的 InvalidateVisual 时机。
        /// </summary>
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (finalSize.Width < 0d)
            {
                finalSize.Width = 0d;
            }
            if (finalSize.Height < 0d)
            {
                finalSize.Height = 0d;
            }

            bool sizeChanged = Math.Abs(finalSize.Width - _lastArrangeWidth) > 0.5d
                || Math.Abs(finalSize.Height - _lastArrangeHeight) > 0.5d;
            _lastArrangeWidth = finalSize.Width;
            _lastArrangeHeight = finalSize.Height;

            Chart? chart = Chart;
            SKSize logical = default;
            if (chart == null || (logical = chart.MeasureSize()).Width <= 0f || logical.Height <= 0f || finalSize.Width <= 0d || finalSize.Height <= 0d)
            {
                _image.Source = null;
                _root.Arrange(new Rect(0d, 0d, finalSize.Width, finalSize.Height));
                return finalSize;
            }

            double device = PixelScale();
            double fit = Fit == ChartFit.Fit
                ? Math.Min(finalSize.Width / logical.Width, finalSize.Height / logical.Height) * Math.Max(ScaleFactor, 0d)
                : NativeScale();
            if (fit <= 0d)
            {
                _image.Source = null;
                _root.Arrange(new Rect(0d, 0d, finalSize.Width, finalSize.Height));
                return finalSize;
            }

            double drawnWidthDip = logical.Width * fit;
            double drawnHeightDip = logical.Height * fit;

            // 渲染 DPI 反推：目标物理像素宽 = 期望 DIP 宽 × 宿主缩放；
            // ChartRaster 内部再按 dpi/96 放大画布，因此 dpi = 96 × 物理像素宽 / 逻辑宽。
            int renderDpi = Math.Max(1, (int)Math.Round(96d * drawnWidthDip * device / Math.Max(1d, logical.Width)));
            if (sizeChanged || !ReferenceEquals(_frameOwner, chart) || _frameRenderDpi != renderDpi || _frame == null)
            {
                _frame = chart.ToWriteableBitmap(renderDpi);
                _frameOwner = chart;
                _frameRenderDpi = renderDpi;
            }

            // 帧的物理像素折算回有效像素：1 位图像素 = 1 物理像素，合成器零插值。
            double imageWidth = _frame.PixelWidth / device;
            double imageHeight = _frame.PixelHeight / device;
            double left = Fit == ChartFit.Fit ? Math.Max(0d, (finalSize.Width - imageWidth) / 2d) : 0d;
            double top = Fit == ChartFit.Fit ? Math.Max(0d, (finalSize.Height - imageHeight) / 2d) : 0d;

            _image.Width = imageWidth;
            _image.Height = imageHeight;
            Canvas.SetLeft(_image, left);
            Canvas.SetTop(_image, top);
            _image.Measure(new Size(imageWidth, imageHeight));
            _image.Arrange(new Rect(left, top, imageWidth, imageHeight));

            _root.Measure(finalSize);
            _root.Arrange(new Rect(0d, 0d, finalSize.Width, finalSize.Height));

            // FrameworkElement 无 ClipToBounds，用显式矩形裁剪 Actual 模式的溢出。
            Clip = new RectangleGeometry { Rect = new Rect(0d, 0d, finalSize.Width, finalSize.Height) };

            // 指针是有效像素（DIP），帧也按 DIP 摆放，视口直接用 fit 与居中偏移。
            _interactionHost.Viewport = ChartViewport.FromPlacement(
                logical, (float)fit, (float)left, (float)top);
            return finalSize;
        }

        private void HandlePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(_root);
            if (point.Properties.IsLeftButtonPressed)
            {
                _interactionHost.PointerDown(ToSkPoint(point.Position));
                e.Handled = true;
            }
        }

        private void HandlePointerMoved(object sender, PointerRoutedEventArgs e)
        {
            _interactionHost.PointerMove(ToSkPoint(e.GetCurrentPoint(_root).Position));
        }

        private void HandlePointerReleased(object sender, PointerRoutedEventArgs e)
        {
            _interactionHost.PointerUp(ToSkPoint(e.GetCurrentPoint(_root).Position));
        }

        private void HandlePointerExited(object sender, PointerRoutedEventArgs e)
        {
            _interactionHost.PointerLeave();
        }

        private static SKPoint ToSkPoint(Windows.Foundation.Point point)
        {
            return new SKPoint((float)point.X, (float)point.Y);
        }

        /// <summary>Actual 模式的 逻辑单位→有效像素 比；Dpi=0 时跟随宿主缩放。</summary>
        private double NativeScale()
        {
            double baseline = Dpi > 0d ? Dpi / 96d : PixelScale();
            return Math.Max(ScaleFactor, 0d) * baseline;
        }

        /// <summary>
        /// 宿主"有效像素→物理像素"比：取 <see cref="XamlRoot.RasterizationScale"/>
        /// （= 系统 DPI/96 × 主缩放）。未挂树（设计期/单测）或 WinRT 抛错时按 1:1。
        /// </summary>
        private double PixelScale()
        {
            try
            {
                XamlRoot? root = XamlRoot;
                if (root != null)
                {
                    double scale = root.RasterizationScale;
                    if (scale > 0d && !double.IsNaN(scale) && !double.IsInfinity(scale))
                    {
                        return scale;
                    }
                }
            }
            catch (Exception)
            {
                // XamlRoot 就绪前访问可能抛异常，退回 1:1 保证可构造。
            }

            return 1d;
        }
    }
}
