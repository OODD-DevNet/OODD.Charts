using System;
using System.Diagnostics;
using SkiaSharp;

namespace OODD.Charts.Interaction
{
    /// <summary>
    /// 宿主控件与 <see cref="IChartInteraction"/> 之间的胶水：负责挂载/卸载、指针坐标换算、
    /// 动画时钟与重绘通知，让七个平台适配器只需转发原生事件并起一个定时器。
    /// <para>
    /// 适配器在每次绘制结束时把当帧的 <see cref="Viewport"/> 写进来；之后收到的指针事件
    /// 用同一视口换算，因此缩放、留白、DPI 都不需要适配器重复推导。
    /// </para>
    /// </summary>
    public sealed class ChartInteractionHost : IDisposable
    {
        private readonly Action _redraw;
        private readonly Action? _animationStateChanged;
        private readonly Stopwatch _clock = new Stopwatch();

        private Chart? _chart;
        private IChartInteraction? _interaction;
        private long _lastTick;
        private bool _attached;
        private bool _disposed;

        /// <param name="redraw">要求宿主丢弃缓存帧并重绘。</param>
        /// <param name="animationStateChanged">动画开关/帧间隔变化时回调；宿主据此启停定时器。</param>
        public ChartInteractionHost(Action redraw, Action? animationStateChanged = null)
        {
            _redraw = redraw ?? throw new ArgumentNullException(nameof(redraw));
            _animationStateChanged = animationStateChanged;
        }

        /// <summary>当前表单；换表单会自动把交互重新挂到新表单上。</summary>
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
                Reattach();
            }
        }

        /// <summary>当前交互；为 null 时控件只做静态渲染。</summary>
        public IChartInteraction? Interaction
        {
            get => _interaction;
            set
            {
                if (ReferenceEquals(_interaction, value))
                {
                    return;
                }

                _interaction = value;
                Reattach();
            }
        }

        /// <summary>最近一帧的落位；未绘制过是 <see cref="ChartViewport.Empty"/>。</summary>
        public ChartViewport Viewport { get; set; }

        /// <summary>
        /// 是否需要宿主持续出帧。交互未挂载（表单不认它、或还没设表单）时为 false，
        /// 否则控件会为一个跑不动的动画空转定时器。
        /// </summary>
        public bool WantsAnimation => _attached && _interaction != null && _interaction.WantsAnimation;

        public int FrameIntervalMilliseconds =>
            _interaction == null ? 0 : Math.Max(1, _interaction.FrameIntervalMilliseconds);

        public bool IsPointerOverInteractive => _interaction != null && _interaction.IsPointerOverInteractive;

        /// <summary>视图坐标下的按下事件。</summary>
        public void PointerDown(SKPoint viewPoint)
        {
            if (TryMap(viewPoint, out SKPoint chartPoint))
            {
                _interaction!.PointerDown(chartPoint);
            }
        }

        public void PointerMove(SKPoint viewPoint)
        {
            if (TryMap(viewPoint, out SKPoint chartPoint))
            {
                _interaction!.PointerMove(chartPoint);
            }
        }

        public void PointerUp(SKPoint viewPoint)
        {
            if (TryMap(viewPoint, out SKPoint chartPoint))
            {
                _interaction!.PointerUp(chartPoint);
            }
        }

        public void PointerLeave()
        {
            _interaction?.PointerLeave();
        }

        /// <summary>推进一帧；未在动画或未挂载时直接返回。</summary>
        public void Tick()
        {
            if (!_attached || _interaction == null || !_interaction.WantsAnimation)
            {
                return;
            }

            long now = _clock.ElapsedTicks;
            double elapsed = (now - _lastTick) * 1000d / Stopwatch.Frequency;
            _lastTick = now;
            if (elapsed <= 0d)
            {
                return;
            }

            _interaction.Tick(elapsed);
        }

        private void Reattach()
        {
            _attached = false;
            if (_interaction != null)
            {
                _interaction.RedrawRequested -= OnRedrawRequested;
                _interaction.AnimationStateChanged -= OnAnimationStateChanged;
                _interaction.Detach();
            }

            if (_interaction != null && _chart != null && _interaction.CanAttach(_chart))
            {
                _interaction.Attach(_chart);
                _interaction.RedrawRequested += OnRedrawRequested;
                _interaction.AnimationStateChanged += OnAnimationStateChanged;
                _attached = true;
            }

            ResetClock();
            _animationStateChanged?.Invoke();
        }

        private bool TryMap(SKPoint viewPoint, out SKPoint chartPoint)
        {
            chartPoint = default;
            if (_interaction == null || _chart == null || Viewport.IsEmpty)
            {
                return false;
            }

            chartPoint = Viewport.ToChart(viewPoint);
            return true;
        }

        private void ResetClock()
        {
            _clock.Restart();
            _lastTick = 0;
        }

        private void OnRedrawRequested(object? sender, EventArgs e)
        {
            _redraw();
        }

        private void OnAnimationStateChanged(object? sender, EventArgs e)
        {
            ResetClock();
            _animationStateChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _attached = false;
            if (_interaction != null)
            {
                _interaction.RedrawRequested -= OnRedrawRequested;
                _interaction.AnimationStateChanged -= OnAnimationStateChanged;
                _interaction.Detach();
            }

            _interaction = null;
            _chart = null;
        }
    }
}
