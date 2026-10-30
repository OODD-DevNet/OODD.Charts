using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using OODD.Charts.Interaction;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.WinForms
{
    /// <summary>
    /// WinForms 自绘宿主控件：把 <see cref="OODD.Charts.Chart"/> 贴进客户区。
    /// <para>
    /// 每一帧都是 Skia 光栅化出的 BGRA 像素，经 <see cref="WinFormsChartExtensions.ToBitmap(Chart,float)"/>
    /// 缓存成 <see cref="Bitmap"/> 再 <see cref="Graphics.DrawImage(Image,Rectangle)"/>，
    /// 全程双缓冲、缩放不经过 GDI+ 排版，因此 Win7 到 Win11 表现一致。
    /// </para>
    /// </summary>
    public class ChartControl : Control
    {
        private Chart? _chart;
        private Bitmap? _frame;
        private Chart? _frameOwner;
        private int _frameWidth = -1;
        private int _frameHeight = -1;
        private ChartFit _fit = ChartFit.Fit;
        private float _scaleFactor = 1f;
        private float _dpi;
        private readonly Timer _animationTimer;
        private readonly ChartInteractionHost _interactionHost;

        public ChartControl()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.White;

            _interactionHost = new ChartInteractionHost(OnInteractionRedraw, OnInteractionAnimationChanged);
            _animationTimer = new Timer();
            _animationTimer.Tick += (sender, args) => _interactionHost.Tick();
        }

        /// <summary>
        /// 交互（选中/悬停/动画等）。赋 null 退回纯静态显示；换表单时自动重新挂载。
        /// <c>RadarInteraction.TryCreate(chart)</c> 之类的工厂会按表单类型返回 null，
        /// 因此可以直接赋值而不必先判型。
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IChartInteraction? Interaction
        {
            get => _interactionHost.Interaction;
            set => _interactionHost.Interaction = value;
        }

        /// <summary>要显示的表单。控件不接管其生命周期，由调用方 Dispose。</summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
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
                Invalidate();
            }
        }

        /// <summary>
        /// 适配方式。<see cref="ChartFit.Fit"/> 等比缩到客户区并居中；
        /// <see cref="ChartFit.Actual"/> 按 <see cref="Dpi"/> 原尺寸绘制，超出裁切。
        /// </summary>
        [DefaultValue(ChartFit.Fit)]
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
                Invalidate();
            }
        }

        /// <summary>在各自适配方式基础上的额外缩放倍率（1.0 = 不缩放）。</summary>
        [DefaultValue(1f)]
        public float ScaleFactor
        {
            get => _scaleFactor;
            set
            {
                if (value <= 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (_scaleFactor == value)
                {
                    return;
                }
                _scaleFactor = value;
                Invalidate();
            }
        }

        /// <summary>
        /// 仅 <see cref="ChartFit.Actual"/> 模式生效的渲染 DPI；0 表示跟随宿主 DPI。
        /// Fit 模式下渲染 DPI 由客户区尺寸反推，无需设置。
        /// </summary>
        [DefaultValue(0f)]
        public float Dpi
        {
            get => _dpi;
            set
            {
                if (value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                if (_dpi == value)
                {
                    return;
                }
                _dpi = value;
                InvalidateFrame();
                Invalidate();
            }
        }

        /// <summary>外部改过 Document/Style 之后调用：丢弃缓存帧并重绘。</summary>
        public void InvalidateChart()
        {
            _chart?.Invalidate();
            InvalidateFrame();
            Invalidate();
        }

        private void OnInteractionRedraw()
        {
            InvalidateChart();
        }

        /// <summary>按交互层的动画开关启停帧定时器；定时器回调用真实耗时推进动画。</summary>
        private void OnInteractionAnimationChanged()
        {
            if (!_interactionHost.WantsAnimation)
            {
                _animationTimer.Stop();
                return;
            }

            int interval = _interactionHost.FrameIntervalMilliseconds;
            if (_animationTimer.Interval != interval)
            {
                _animationTimer.Interval = interval;
            }
            _animationTimer.Start();
        }

        private void InvalidateFrame()
        {
            _frameOwner = null;
            _frameWidth = -1;
            _frameHeight = -1;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Chart? chart = _chart;
            if (chart == null || e == null)
            {
                return;
            }

            // GDI+ 默认 PageUnit 会随系统 DPI 折算逻辑单位，这里锁死成物理像素，
            // 让"Skia 按宿主 DPI 出的像素"与"贴图的像素"一一对应，杜绝二次缩放。
            Graphics graphics = e.Graphics;
            graphics.PageUnit = GraphicsUnit.Pixel;

            SKSize logical = chart.MeasureSize();
            if (logical.Width <= 0f || logical.Height <= 0f)
            {
                return;
            }

            Size area = PhysicalClientSize(graphics);
            float scale = _fit == ChartFit.Fit
                ? Math.Min(area.Width / logical.Width, area.Height / logical.Height) * _scaleFactor
                : ((_dpi > 0f ? _dpi : graphics.DpiX) / 96f) * _scaleFactor;
            if (scale <= 0f)
            {
                return;
            }

            int targetWidth = Math.Max(1, (int)Math.Round(logical.Width * scale));
            int targetHeight = Math.Max(1, (int)Math.Round(logical.Height * scale));
            Bitmap frame = EnsureFrame(chart, targetWidth, targetHeight, logical);

            Point origin = Point.Empty;
            if (_fit == ChartFit.Fit)
            {
                origin = new Point((area.Width - frame.Width) / 2, (area.Height - frame.Height) / 2);
            }

            graphics.DrawImage(frame, new Rectangle(origin.X, origin.Y, frame.Width, frame.Height));

            // 指针坐标是物理像素，帧也是按物理像素贴上去的，因此视口直接用"帧像素/逻辑单位"。
            _interactionHost.Viewport = ChartViewport.FromPlacement(
                logical,
                frame.Width / Math.Max(1f, logical.Width),
                origin.X,
                origin.Y);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Focus();
                _interactionHost.PointerDown(new SKPoint(e.X, e.Y));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _interactionHost.PointerMove(new SKPoint(e.X, e.Y));
            Cursor = _interactionHost.IsPointerOverInteractive ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                _interactionHost.PointerUp(new SKPoint(e.X, e.Y));
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _interactionHost.PointerLeave();
            Cursor = Cursors.Default;
        }

        /// <summary>客户区的物理像素尺寸：以绘制 DC 的可视裁剪区为准（已按 PageUnit.Pixel 折算）。</summary>
        private Size PhysicalClientSize(Graphics graphics)
        {
            RectangleF visible = graphics.VisibleClipBounds;
            int width = (int)Math.Round(visible.Width);
            int height = (int)Math.Round(visible.Height);
            if (width > 0 && height > 0)
            {
                return new Size(width, height);
            }
            return new Size(Math.Max(1, ClientSize.Width), Math.Max(1, ClientSize.Height));
        }

        private Bitmap EnsureFrame(Chart chart, int width, int height, SKSize logical)
        {
            if (_frame != null &&
                ReferenceEquals(_frameOwner, chart) &&
                _frameWidth == width &&
                _frameHeight == height)
            {
                return _frame;
            }

            // 让 Skia 直接按目标像素宽出图，贴图时 1:1，不经过 GDI+ 重采样。
            float dpi = 96f * width / Math.Max(1f, logical.Width);
            Bitmap next = chart.ToBitmap(dpi);

            _frame?.Dispose();
            _frame = next;
            _frameOwner = chart;
            _frameWidth = width;
            _frameHeight = height;
            return next;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _animationTimer.Stop();
                _animationTimer.Dispose();
                _interactionHost.Dispose();
                _frame?.Dispose();
                _frame = null;
                _frameOwner = null;
            }
            base.Dispose(disposing);
        }
    }
}
