using System;
using OODD.Charts.Interaction;
using SkiaSharp;

namespace OODD.Charts.Radar
{
    /// <summary>
    /// 雷达态势图的交互：点击选中目标、悬停高亮、扫描线匀速旋转。
    /// <para>
    /// 只改文档状态（<see cref="RadarDocument.SelectedTargetId"/> / <see cref="RadarDocument.HoverTargetId"/> /
    /// <see cref="RadarDocument.SweepAngle"/>）并请求重绘，不碰任何宿主对象；
    /// 用户也可以完全绕开本类，自己改文档后调 <c>InvalidateChart()</c>。
    /// </para>
    /// <para>
    /// 宿主控件通过 <see cref="ChartInteractionHost"/> 自动调用 <see cref="Attach"/>，
    /// 演示程序只需要 <c>control.Interaction = RadarInteraction.TryCreate(chart);</c>。
    /// </para>
    /// </summary>
    public sealed class RadarInteraction : IChartInteraction
    {
        private Chart? _chart;
        private RadarChartTemplate? _template;
        private RadarDocument? _document;
        private bool _isAnimating = true;
        private int _frameIntervalMilliseconds = 33;

        /// <summary>命中半径（96dpi 逻辑像素），默认 12。</summary>
        public float HitTolerance { get; set; } = 12f;

        /// <summary>扫描线角速度（度/秒），默认 60，即 6 秒一圈。</summary>
        public double SweepSpeedDegreesPerSecond { get; set; } = 60d;

        /// <summary>动画帧间隔（毫秒），默认 33（约 30 fps）。</summary>
        public int FrameIntervalMilliseconds
        {
            get => _frameIntervalMilliseconds;
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (_frameIntervalMilliseconds == value)
                {
                    return;
                }
                _frameIntervalMilliseconds = value;
                AnimationStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>是否让扫描线持续旋转；默认开启。关掉后交互仍在（选中/悬停照常）。</summary>
        public bool IsAnimating
        {
            get => _isAnimating;
            set
            {
                if (_isAnimating == value)
                {
                    return;
                }
                _isAnimating = value;
                AnimationStateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public bool WantsAnimation => _isAnimating;

        public bool IsPointerOverInteractive { get; private set; }

        /// <summary>当前挂载的文档；未挂载时为 null。</summary>
        public RadarDocument? Document => _document;

        public RadarTarget? SelectedTarget => _document?.SelectedTarget;

        public RadarTarget? HoveredTarget => Find(_document?.HoverTargetId);

        /// <summary>在已有雷达表单上创建交互；非雷达表单返回 null（直接赋给控件即可，不必判型）。</summary>
        public static RadarInteraction? TryCreate(Chart? chart)
        {
            if (chart == null || !(chart.Template is RadarChartTemplate))
            {
                return null;
            }

            return new RadarInteraction();
        }

        public bool CanAttach(Chart chart)
        {
            return chart != null && chart.Document is RadarDocument && chart.Template is RadarChartTemplate;
        }

        public void Attach(Chart chart)
        {
            if (!CanAttach(chart))
            {
                throw new ArgumentException("雷达交互只能挂到 OODD.Charts.Radar 生成的表单上。", nameof(chart));
            }

            _chart = chart;
            _document = (RadarDocument)chart.Document;
            _template = (RadarChartTemplate)chart.Template;
        }

        public void Detach()
        {
            if (_document != null && _document.HoverTargetId != null)
            {
                _document.HoverTargetId = null;
            }

            _chart = null;
            _document = null;
            _template = null;
            IsPointerOverInteractive = false;
        }

        /// <summary>按编号选中目标；null 表示取消选中。与点击等价，供外部联动。</summary>
        public void Select(string? targetId)
        {
            if (_document == null)
            {
                return;
            }

            string? normalized = string.IsNullOrEmpty(targetId) ? null : targetId;
            if (string.Equals(_document.SelectedTargetId, normalized, StringComparison.Ordinal))
            {
                return;
            }

            _document.SelectedTargetId = normalized;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            RequestRedraw();
        }

        public void PointerDown(SKPoint chartPoint)
        {
            if (_document == null || _chart == null)
            {
                return;
            }

            // 留白（Fit 模式的信箱区）不参与选中，避免"点空白把选中清掉"的意外。
            if (!InsideChart(chartPoint))
            {
                return;
            }

            RadarTarget? hit = CurrentLayout().HitTest(_document, chartPoint, HitTolerance);
            Select(hit?.Id);
        }

        public void PointerMove(SKPoint chartPoint)
        {
            if (_document == null)
            {
                return;
            }

            RadarTarget? hit = InsideChart(chartPoint)
                ? CurrentLayout().HitTest(_document, chartPoint, HitTolerance)
                : null;
            SetHover(hit);
        }

        public void PointerUp(SKPoint chartPoint)
        {
        }

        public void PointerLeave()
        {
            SetHover(null);
        }

        public void Tick(double elapsedMilliseconds)
        {
            if (_document == null || !_isAnimating || elapsedMilliseconds <= 0d)
            {
                return;
            }

            double speed = SweepSpeedDegreesPerSecond;
            if (speed <= 0d)
            {
                return;
            }

            _document.SweepAngle = RadarInfoPanel.NormalizeBearing(
                _document.SweepAngle + speed * elapsedMilliseconds / 1000d);
            RequestRedraw();
        }

        public event EventHandler? RedrawRequested;

        public event EventHandler? AnimationStateChanged;

        /// <summary>选中目标变化（点击或 <see cref="Select"/>）。</summary>
        public event EventHandler? SelectionChanged;

        /// <summary>悬停目标变化。</summary>
        public event EventHandler? HoverChanged;

        private RadarLayout CurrentLayout()
        {
            return _template!.Layout(_document!);
        }

        private bool InsideChart(SKPoint chartPoint)
        {
            return _chart != null && CurrentLayout().Bounds.Contains(chartPoint.X, chartPoint.Y);
        }

        private void SetHover(RadarTarget? target)
        {
            if (_document == null)
            {
                return;
            }

            IsPointerOverInteractive = target != null;
            if (string.Equals(_document.HoverTargetId, target?.Id, StringComparison.Ordinal))
            {
                return;
            }

            _document.HoverTargetId = target?.Id;
            HoverChanged?.Invoke(this, EventArgs.Empty);
            RequestRedraw();
        }

        private RadarTarget? Find(string? targetId)
        {
            if (_document == null || string.IsNullOrEmpty(targetId))
            {
                return null;
            }

            foreach (RadarTarget target in _document.Targets)
            {
                if (string.Equals(target.Id, targetId, StringComparison.Ordinal))
                {
                    return target;
                }
            }

            return null;
        }

        private void RequestRedraw()
        {
            RedrawRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
