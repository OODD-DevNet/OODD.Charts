using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace OODD.Charts.Adapters.Maui
{
    /// <summary>
    /// 零适配路径的 MAUI 宿主控件：在 <c>SKCanvasView</c> 的绘制回调里用 <see cref="Chart.Draw(SKCanvas)"/>
    /// 直出矢量，<b>不经过 PNG 编码 / 平台解码</b>。
    /// <para>
    /// 依赖 <c>SkiaSharp.Views.Maui.Controls</c>（本仓库锁 3.116.1，与 Core 用的 SkiaSharp 同版本，
    /// 保证 <see cref="SKCanvas"/> 是同一套原生库）。与 <see cref="ChartImage"/> 的取舍：
    /// 本控件清晰度高、可增量重绘、无字节驻留；代价是宿主必须能装下 Skia 的 MAUI 视图包，
    /// 且每次 <c>InvalidateSurface()</c> 都会重录一遍 <c>SKPicture</c>。
    /// </para>
    /// <para>
    /// 交互：指针事件走 SkiaSharp 的 <c>Touch</c>（坐标与画布同为像素），换算成表单逻辑坐标后
    /// 交给 <see cref="IChartInteraction"/>；动画由 <see cref="IDispatcherTimer"/> 按真实耗时推进。
    /// </para>
    /// </summary>
    public class ChartCanvasView : SkiaSharp.Views.Maui.Controls.SKCanvasView
    {
        private readonly ChartInteractionHost _interactionHost;
        private readonly IDispatcherTimer _animationTimer;

        /// <summary>构造控件：打开触摸事件，并接上交互/动画管线。</summary>
        public ChartCanvasView()
        {
            EnableTouchEvents = true;

            _interactionHost = new ChartInteractionHost(InvalidateChart, SyncAnimation);
            _animationTimer = Dispatcher.CreateTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(33);
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
        /// <summary>要显示的表单；<c>null</c> 时只清屏。控件不接管其生命周期。</summary>
        public static readonly BindableProperty ChartProperty = BindableProperty.Create(
            nameof(Chart),
            typeof(Chart),
            typeof(ChartCanvasView),
            defaultValue: null,
            propertyChanged: OnRenderParameterChanged);

        /// <summary>适配方式，默认 <see cref="ChartFit.Fit"/>（等比贴合控件、居中留白）。</summary>
        public static readonly BindableProperty FitProperty = BindableProperty.Create(
            nameof(Fit),
            typeof(ChartFit),
            typeof(ChartCanvasView),
            defaultValue: ChartFit.Fit,
            propertyChanged: OnRenderParameterChanged);

        /// <summary>各自适配方式基础上的额外缩放倍率，默认 1。</summary>
        public static readonly BindableProperty ScaleFactorProperty = BindableProperty.Create(
            nameof(ScaleFactor),
            typeof(double),
            typeof(ChartCanvasView),
            defaultValue: 1d,
            propertyChanged: OnRenderParameterChanged);

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

        private static void OnRenderParameterChanged(BindableObject bindable, object oldValue, object newValue)
        {
            ChartCanvasView view = (ChartCanvasView)bindable;
            view._interactionHost.Chart = view.Chart;
            view.ApplySizeRequest();
            view.InvalidateSurface();
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

        /// <summary>视图离屏/上屏时停/启动画，避免后台空转。</summary>
        protected override void OnHandlerChanged()
        {
            base.OnHandlerChanged();
            if (Handler == null)
            {
                _animationTimer.Stop();
            }
            else
            {
                SyncAnimation();
            }
        }

        /// <summary>
        /// 外部改过 <c>Document</c> / <c>Style</c> 之后调用：丢弃几何缓存并重绘。
        /// <para>MAUI 的 <c>InvalidateSurface()</c> 只是排队请求，真正的绘制在下一帧的
        /// <see cref="OnPaintSurface"/> 里执行（UI 线程）。</para>
        /// </summary>
        public void InvalidateChart()
        {
            Chart?.Invalidate();
            ApplySizeRequest();
            InvalidateSurface();
        }

        /// <summary>
        /// <see cref="ChartFit.Actual"/> 下按"逻辑尺寸 × 缩放系数"设 <c>WidthRequest</c> / <c>HeightRequest</c>，
        /// 其余模式交回布局（-1 = 不干预）。
        /// <para>没有重写 <c>OnMeasure</c>：MAUI 10 已把它标为过时（CS0672/CS0618，改用
        /// <c>MeasureOverride</c> 返回 <c>Microsoft.Maui.Graphics.Size</c>），而请求尺寸足够表达本控件的诉求。</para>
        /// </summary>
        private void ApplySizeRequest()
        {
            Chart? chart = Chart;
            if (chart == null || Fit != ChartFit.Actual)
            {
                WidthRequest = -1d;
                HeightRequest = -1d;
                InvalidateMeasure();
                return;
            }

            SKSize logical = chart.MeasureSize();
            double factor = ScaleFactor > 0d ? ScaleFactor : 1d;
            WidthRequest = logical.Width * factor;
            HeightRequest = logical.Height * factor;
            InvalidateMeasure();
        }

        /// <summary>
        /// 直绘：把表单录成 <c>SKPicture</c> 后按当前适配方式落到 Skia 画布。
        /// </summary>
        protected override void OnPaintSurface(SKPaintSurfaceEventArgs e)
        {
            DrawChart(e);

            // 最后才让 base 触发 PaintSurface 事件：这样宿主订阅者画在表单之上，可以拿来叠加标注，
            // 而不会被本控件的清屏抹掉。
            base.OnPaintSurface(e);
        }

        private void DrawChart(SKPaintSurfaceEventArgs e)
        {
            SKCanvas canvas = e.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);

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

            double factor = ScaleFactor > 0d ? ScaleFactor : 1d;
            SKImageInfo info = e.Info;

            if (Fit == ChartFit.Fit)
            {
                // Core 内部自己做等比 + 居中，这里给的是设备像素矩形，因此清晰度与控件 DPI 无关。
                chart.Draw(canvas, new SKRect(0f, 0f, info.Width, info.Height));

                float ratio = Math.Min(info.Width / logical.Width, info.Height / logical.Height);
                _interactionHost.Viewport = ChartViewport.FromPlacement(
                    logical, ratio,
                    (info.Width - logical.Width * ratio) / 2f,
                    (info.Height - logical.Height * ratio) / 2f);
                return;
            }

            // Actual：Info 是设备像素、CanvasSize 是 MAUI 逻辑单位，两者之比就是本次的表面缩放比
            // （IgnorePixelScaling=true 时恒为 1）。按它放大后 1:1 绘制，超出的部分由 Skia 裁掉。
            float deviceScale = CanvasSize.Width > 0d ? (float)(info.Width / CanvasSize.Width) : 1f;
            float scale = deviceScale * (float)factor;
            int saved = canvas.Save();
            canvas.Scale(scale);
            chart.Draw(canvas);
            canvas.RestoreToCount(saved);

            _interactionHost.Viewport = ChartViewport.FromPlacement(logical, scale, 0f, 0f);
        }

        /// <summary>
        /// 指针事件：SkiaSharp 的触摸坐标与画布同为像素，直接交给视口换算。
        /// </summary>
        protected override void OnTouch(SKTouchEventArgs e)
        {
            base.OnTouch(e);

            switch (e.ActionType)
            {
                case SKTouchAction.Pressed:
                    _interactionHost.PointerDown(e.Location);
                    e.Handled = true;
                    break;
                case SKTouchAction.Moved:
                    _interactionHost.PointerMove(e.Location);
                    break;
                case SKTouchAction.Released:
                    _interactionHost.PointerUp(e.Location);
                    break;
                case SKTouchAction.Cancelled:
                case SKTouchAction.Exited:
                    _interactionHost.PointerLeave();
                    break;
            }
        }
    }
}
