using System;
using SkiaSharp;

namespace OODD.Charts.Interaction
{
    /// <summary>
    /// 表单交互：控件把指针事件与动画帧转发进来，交互层只改文档状态、请求重绘，
    /// 自身不持有任何宿主对象。
    /// <para>
    /// 坐标一律是表单 96dpi 逻辑坐标（由 <see cref="ChartViewport"/> 换算），
    /// 因此同一份交互实现可以被任意宿主适配器复用。
    /// </para>
    /// </summary>
    public interface IChartInteraction
    {
        /// <summary>能否挂到该表单上（类型不匹配时返回 false，由宿主忽略而不是抛异常）。</summary>
        bool CanAttach(Chart chart);

        /// <summary>挂上表单。宿主在每次换表单时调用。</summary>
        void Attach(Chart chart);

        /// <summary>摘下当前表单并清掉临时状态（悬停等）。</summary>
        void Detach();

        /// <summary>是否需要按 <see cref="FrameIntervalMilliseconds"/> 持续重绘。</summary>
        bool WantsAnimation { get; }

        /// <summary>动画帧间隔（毫秒）。</summary>
        int FrameIntervalMilliseconds { get; }

        /// <summary>指针是否悬停在可交互对象上；宿主据此换手型光标。</summary>
        bool IsPointerOverInteractive { get; }

        /// <summary>推进一帧；<paramref name="elapsedMilliseconds"/> 是距上一帧的真实耗时。</summary>
        void Tick(double elapsedMilliseconds);

        void PointerDown(SKPoint chartPoint);

        void PointerMove(SKPoint chartPoint);

        void PointerUp(SKPoint chartPoint);

        /// <summary>指针离开控件；清掉悬停态。</summary>
        void PointerLeave();

        /// <summary>交互层改了需要重画的内容。</summary>
        event EventHandler? RedrawRequested;

        /// <summary><see cref="WantsAnimation"/> 或 <see cref="FrameIntervalMilliseconds"/> 变了。</summary>
        event EventHandler? AnimationStateChanged;
    }
}
