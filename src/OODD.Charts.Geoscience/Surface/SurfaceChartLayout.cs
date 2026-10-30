using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Geoscience.Surface
{
    /// <summary>一个层次面板的像素矩形与站点落点映射。</summary>
    public sealed class SurfacePanel
    {
        internal SurfacePanel(SKRect rect, SKRect content, int? pressureHpa, string label)
        {
            Rect = rect;
            Content = content;
            PressureHpa = pressureHpa;
            Label = label;
        }

        public SKRect Rect { get; }

        /// <summary>面板内可取点的区域（已按 <c>PanelInset</c> 内缩，站点符号不会压框）。</summary>
        public SKRect Content { get; }

        public int? PressureHpa { get; }

        public string Label { get; }
    }

    /// <summary>
    /// 地面天气图的版面结果。所有落点都从本对象取，绘制与测试共用同一套映射。
    /// </summary>
    public sealed class SurfaceChartLayout
    {
        private readonly List<SurfacePanel> _panels;

        internal SurfaceChartLayout(SKRect bounds, SKRect titleRect, SKRect footerRect,
                                    List<SurfacePanel> panels)
        {
            Bounds = bounds;
            TitleRect = titleRect;
            FooterRect = footerRect;
            _panels = panels;

            float left = float.MaxValue;
            float top = float.MaxValue;
            float right = float.MinValue;
            float bottom = float.MinValue;
            foreach (SurfacePanel panel in panels)
            {
                left = Math.Min(left, panel.Rect.Left);
                top = Math.Min(top, panel.Rect.Top);
                right = Math.Max(right, panel.Rect.Right);
                bottom = Math.Max(bottom, panel.Rect.Bottom);
            }
            PlotRect = panels.Count == 0 ? SKRect.Empty : new SKRect(left, top, right, bottom);
        }

        public SKRect Bounds { get; }

        public SKRect TitleRect { get; }

        public SKRect FooterRect { get; }

        /// <summary>全部层次面板的并集。</summary>
        public SKRect PlotRect { get; }

        public IReadOnlyList<SurfacePanel> Panels => _panels;

        /// <summary>页面坐标 → 面板内像素。PageX/PageY 会夹到 [0,1]：站点不许落在面板外。</summary>
        public SKPoint CenterOf(SurfacePanel panel, SurfaceStation station)
        {
            double x = station.PageX < 0d ? 0d : station.PageX > 1d ? 1d : station.PageX;
            double y = station.PageY < 0d ? 0d : station.PageY > 1d ? 1d : station.PageY;
            return new SKPoint(
                panel.Content.Left + (float)x * panel.Content.Width,
                panel.Content.Top + (float)y * panel.Content.Height);
        }
    }
}
