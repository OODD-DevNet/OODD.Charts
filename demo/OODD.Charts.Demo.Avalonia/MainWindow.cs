using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using OODD.Charts.Adapters.Avalonia;
using OODD.Charts.Demo;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Demo.AvaloniaDemo
{
    /// <summary>
    /// 最小可运行演示：左上切表单与适配方式，中间看效果，右上导出 PNG/PDF/SVG。
    /// 界面全部用代码搭建，不依赖 XAML；画布是适配器的 <see cref="ChartControl"/>。
    /// </summary>
    public sealed class MainWindow : Window
    {
        private readonly ChartControl _canvas = new ChartControl();
        private readonly ComboBox _formPicker = new ComboBox { Width = 280 };
        private readonly ComboBox _fitPicker = new ComboBox { Width = 100 };
        private readonly Slider _scaleSlider = new Slider
        {
            Width = 120,
            Minimum = 0.5,
            Maximum = 3,
            Value = 1,
            TickFrequency = 0.25,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        private readonly TextBlock _scaleText = new TextBlock { Text = "1.00", VerticalAlignment = VerticalAlignment.Center };
        private readonly TextBlock _status = new TextBlock { Text = "就绪", Margin = new Thickness(8, 4, 8, 4) };
        private readonly CheckBox _animateBox = new CheckBox
        {
            Content = "扫描动画",
            IsChecked = true,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
        };
        private Chart? _chart;

        public MainWindow()
        {
            Title = "OODD.Charts 演示（Avalonia）";
            Width = 1100;
            Height = 820;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            foreach (DemoSample sample in DemoCatalog.All)
            {
                _formPicker.Items.Add(sample.DisplayName);
            }
            _formPicker.SelectedIndex = 0;
            _formPicker.SelectionChanged += (sender, args) => ReloadChart();

            _fitPicker.Items.Add("贴合窗口");
            _fitPicker.Items.Add("原尺寸");
            _fitPicker.SelectedIndex = 0;
            _fitPicker.SelectionChanged += (sender, args) => ApplyFit();

            _scaleSlider.ValueChanged += (sender, args) =>
            {
                _scaleText.Text = _scaleSlider.Value.ToString("0.00");
                ApplyFit();
            };
            _animateBox.Click += (sender, args) => ApplyAnimation();

            DockPanel root = new DockPanel();
            Control toolbar = BuildToolbar();
            DockPanel.SetDock(toolbar, Dock.Top);
            root.Children.Add(toolbar);
            DockPanel.SetDock(_status, Dock.Bottom);
            root.Children.Add(_status);
            root.Children.Add(_canvas);
            Content = root;

            Closed += (sender, args) => _chart?.Dispose();
            ReloadChart();
        }

        private Control BuildToolbar()
        {
            WrapPanel bar = new WrapPanel { Margin = new Thickness(8, 6, 8, 6) };
            bar.Children.Add(new TextBlock { Text = "表单", Margin = new Thickness(0, 6, 4, 0) });
            bar.Children.Add(_formPicker);
            bar.Children.Add(new TextBlock { Text = "适配", Margin = new Thickness(12, 6, 4, 0) });
            bar.Children.Add(_fitPicker);
            bar.Children.Add(new TextBlock { Text = "缩放", Margin = new Thickness(12, 6, 4, 0) });
            bar.Children.Add(_scaleSlider);
            bar.Children.Add(_scaleText);
            bar.Children.Add(_animateBox);
            bar.Children.Add(MakeButton("导出 PNG(300dpi)", (sender, args) => ExportPng()));
            bar.Children.Add(MakeButton("导出 PDF", (sender, args) => ExportPdf()));
            bar.Children.Add(MakeButton("导出 SVG", (sender, args) => ExportSvg()));
            return bar;
        }

        private void ReloadChart()
        {
            _chart?.Dispose();
            int index = _formPicker.SelectedIndex;
            _chart = index >= 0 && index < DemoCatalog.All.Count
                ? DemoCatalog.All[index].Create()
                : DemoCatalog.All[0].Create();

            _canvas.Chart = _chart;
            _canvas.Interaction = RadarInteraction.TryCreate(_chart);
            ApplyFit();
            ApplyAnimation();

            SKSize logical = _chart.MeasureSize();
            bool radar = _canvas.Interaction is RadarInteraction;
            _animateBox.IsEnabled = radar;
            _status.Text = string.Format("逻辑尺寸 {0:0} × {1:0}（96dpi）{2}",
                logical.Width, logical.Height, radar ? "　点击目标选中，悬停高亮" : string.Empty);
        }

        private void ApplyAnimation()
        {
            if (_canvas.Interaction is RadarInteraction radar)
            {
                radar.IsAnimating = _animateBox.IsChecked == true;
            }
        }

        private void ApplyFit()
        {
            _canvas.Fit = _fitPicker.SelectedIndex == 0 ? ChartFit.Fit : ChartFit.Actual;
            _canvas.ScaleFactor = _scaleSlider.Value;
        }

        private async void ExportPng()
        {
            if (_chart == null)
            {
                return;
            }

            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出 PNG",
                SuggestedFileName = "chart-300dpi.png",
                FileTypeChoices = new[] { new FilePickerFileType("PNG 图片") { Patterns = new[] { "*.png" } } },
            });
            if (file == null)
            {
                return;
            }

            await using (Stream stream = await file.OpenWriteAsync())
            {
                _chart.WritePng(stream, 300f);
            }
            _status.Text = "已导出 " + file.Path.LocalPath;
        }

        private async void ExportPdf()
        {
            if (_chart == null)
            {
                return;
            }

            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出 PDF",
                SuggestedFileName = "chart.pdf",
                FileTypeChoices = new[] { new FilePickerFileType("PDF 文档") { Patterns = new[] { "*.pdf" } } },
            });
            if (file == null)
            {
                return;
            }

            await using (Stream stream = await file.OpenWriteAsync())
            {
                _chart.WritePdf(stream, PdfOptions.A4Portrait);
            }
            _status.Text = "已导出 " + file.Path.LocalPath;
        }

        private async void ExportSvg()
        {
            if (_chart == null)
            {
                return;
            }

            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出 SVG",
                SuggestedFileName = "chart.svg",
                FileTypeChoices = new[] { new FilePickerFileType("SVG 矢量图") { Patterns = new[] { "*.svg" } } },
            });
            if (file == null)
            {
                return;
            }

            await using (Stream stream = await file.OpenWriteAsync())
            {
                _chart.WriteSvg(stream);
            }
            _status.Text = "已导出 " + file.Path.LocalPath;
        }

        private static Button MakeButton(string text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> onClick)
        {
            Button button = new Button
            {
                Content = text,
                Padding = new Thickness(8, 2, 8, 2),
                Margin = new Thickness(12, 0, 0, 0),
            };
            button.Click += onClick;
            return button;
        }
    }
}
