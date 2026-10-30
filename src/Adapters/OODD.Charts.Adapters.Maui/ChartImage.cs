using System;
using Microsoft.Maui.Controls;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Adapters.Maui
{
    /// <summary>
    /// MAUI 宿主控件：把 <see cref="Chart"/> 编码成 PNG 后塞进 <see cref="Image.Source"/>。
    /// <para>
    /// 与 WPF / WinForms 适配器不同，这里<b>没有"每帧写像素"的回调</b>：MAUI 的 <c>Image</c> 只吃
    /// <see cref="ImageSource"/>，位图由平台解码器异步生成，所以我们只在参数变化时重新出图，
    /// 不做帧缓存（缓存交给 MAUI）。
    /// </para>
    /// </summary>
    public class ChartImage : Image
    {
        /// <summary>要显示的表单；<c>null</c> 时清空 <c>Source</c>。控件不接管其生命周期。</summary>
        public static readonly BindableProperty ChartProperty = BindableProperty.Create(
            nameof(Chart),
            typeof(Chart),
            typeof(ChartImage),
            defaultValue: null,
            propertyChanged: OnRenderParameterChanged);

        /// <summary>出图 DPI，默认 96（1 逻辑单位 = 1 像素）。≤0 视为 96。</summary>
        public static readonly BindableProperty DpiProperty = BindableProperty.Create(
            nameof(Dpi),
            typeof(float),
            typeof(ChartImage),
            defaultValue: 96f,
            propertyChanged: OnRenderParameterChanged);

        /// <summary>适配方式，默认 <see cref="ChartFit.Fit"/>（等比贴合容器、居中留白）。</summary>
        public static readonly BindableProperty FitProperty = BindableProperty.Create(
            nameof(Fit),
            typeof(ChartFit),
            typeof(ChartImage),
            defaultValue: ChartFit.Fit,
            propertyChanged: OnRenderParameterChanged);

        /// <summary>各自适配方式基础上的额外缩放倍率，默认 1。</summary>
        public static readonly BindableProperty ScaleFactorProperty = BindableProperty.Create(
            nameof(ScaleFactor),
            typeof(double),
            typeof(ChartImage),
            defaultValue: 1d,
            propertyChanged: OnRenderParameterChanged);

        public Chart? Chart
        {
            get => (Chart?)GetValue(ChartProperty);
            set => SetValue(ChartProperty, value);
        }

        public float Dpi
        {
            get => (float)GetValue(DpiProperty);
            set => SetValue(DpiProperty, value);
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
            ((ChartImage)bindable).Refresh();
        }

        /// <summary>
        /// 重新出图。外部改过 <c>Document</c> / <c>Style</c> 之后调用：
        /// 让 <see cref="Chart"/> 丢弃几何缓存，再重算 <c>Source</c>。
        /// </summary>
        public void InvalidateChart()
        {
            Chart?.Invalidate();
            Refresh();
        }

        /// <summary>
        /// 按当前 <see cref="Dpi"/> / <see cref="Fit"/> / <see cref="ScaleFactor"/> 重算 <see cref="Image.Source"/>。
        /// <para>
        /// 同步执行（Skia 编码 + 数组分配），大表在 UI 线程上可能要几十毫秒；
        /// 需要更稳的帧率就自行在后台线程调 <see cref="MauiChartExtensions.ToImageSource(Chart, float)"/>，
        /// 然后回主线程赋 <c>Source</c>，别用本控件。
        /// </para>
        /// </summary>
        public void Refresh()
        {
            Chart? chart = Chart;
            if (chart == null)
            {
                Source = null;
                WidthRequest = -1d;
                HeightRequest = -1d;
                return;
            }

            double factor = ScaleFactor > 0d ? ScaleFactor : 1d;
            float baseline = Dpi > 0f ? Dpi : 96f;

            if (Fit == ChartFit.Actual)
            {
                // 原尺寸：控件按 MAUI 逻辑单位占位，位图按"设备像素"出图，
                // 缩放到 1:1 才不会经平台重采样发虚。未挂到窗口时按 1:1 密度。
                float density = (float)(Window?.DisplayDensity ?? 1d);
                Source = chart.ToImageSource(baseline * (float)factor * density);

                SKSize logical = chart.MeasureSize();
                WidthRequest = logical.Width * factor;
                HeightRequest = logical.Height * factor;
                // 必须写全名：成员名 Aspect 遮蔽了枚举类型 Microsoft.Maui.Aspect（实测 CS1061）。
                // 该枚举在 MAUI 10 是 { AspectFit, AspectFill, Fill, Center }——旧文档里的 Aspect.Aspect 已不存在（实测 CS0117）。
                Aspect = Microsoft.Maui.Aspect.Fill;
            }
            else
            {
                // 贴合容器：尺寸交给布局，位图按 Dpi×ScaleFactor 出，由平台按 AspectFit 等比缩放居中。
                Source = chart.ToImageSource(baseline * (float)factor);
                WidthRequest = -1d;
                HeightRequest = -1d;
                Aspect = Microsoft.Maui.Aspect.AspectFit;
            }
        }
    }
}
