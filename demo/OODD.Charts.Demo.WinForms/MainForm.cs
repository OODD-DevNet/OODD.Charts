using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using OODD.Charts.Adapters.WinForms;
using OODD.Charts.Demo;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;
using SkiaSharp;

namespace OODD.Charts.Demo.WinForms
{
    /// <summary>
    /// 最小可运行演示：左上切表单与适配方式，中间看效果，右上导出 PNG/PDF/SVG。
    /// 控件本身用适配器的 <see cref="ChartControl"/>，不经过任何图片编解码。
    /// </summary>
    public sealed class MainForm : Form
    {
        private readonly ChartControl _canvas = new ChartControl { Dock = DockStyle.Fill };
        private readonly ComboBox _formPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 280 };
        private readonly ComboBox _fitPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
        private readonly NumericUpDown _scaleBox = new NumericUpDown
        {
            DecimalPlaces = 2,
            Increment = 0.25m,
            Minimum = 0.25m,
            Maximum = 4m,
            Value = 1m,
            Width = 60,
        };
        private readonly ToolStripStatusLabel _status = new ToolStripStatusLabel("就绪");
        private readonly CheckBox _animateBox = new CheckBox
        {
            Text = "扫描动画",
            AutoSize = true,
            Checked = true,
            Margin = new Padding(8, 6, 0, 0),
        };
        private Chart? _chart;

        public MainForm()
        {
            Text = "OODD.Charts 演示（WinForms）";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1100, 820);

            foreach (DemoSample sample in DemoCatalog.All)
            {
                _formPicker.Items.Add(sample.DisplayName);
            }
            _formPicker.SelectedIndex = 0;
            _formPicker.SelectedIndexChanged += (sender, args) => ReloadChart();

            _fitPicker.Items.AddRange(new object[] { "贴合窗口", "原尺寸" });
            _fitPicker.SelectedIndex = 0;
            _fitPicker.SelectedIndexChanged += (sender, args) => ApplyFit();

            _scaleBox.ValueChanged += (sender, args) => ApplyFit();
            _animateBox.CheckedChanged += (sender, args) => ApplyAnimation();

            Controls.Add(_canvas);
            Controls.Add(BuildToolbar());
            Controls.Add(BuildStatusBar());
            FormClosed += (sender, args) => _chart?.Dispose();

            ReloadChart();
        }

        private Control BuildToolbar()
        {
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 42,
                Padding = new Padding(8, 8, 8, 0),
                WrapContents = false,
            };

            bar.Controls.Add(MakeLabel("表单"));
            bar.Controls.Add(_formPicker);
            bar.Controls.Add(MakeLabel("适配"));
            bar.Controls.Add(_fitPicker);
            bar.Controls.Add(MakeLabel("缩放"));
            bar.Controls.Add(_scaleBox);
            bar.Controls.Add(_animateBox);
            bar.Controls.Add(MakeButton("导出 PNG(300dpi)", (sender, args) => ExportPng()));
            bar.Controls.Add(MakeButton("导出 PDF", (sender, args) => ExportPdf()));
            bar.Controls.Add(MakeButton("导出 SVG", (sender, args) => ExportSvg()));
            return bar;
        }

        private Control BuildStatusBar()
        {
            StatusStrip strip = new StatusStrip { Dock = DockStyle.Bottom };
            strip.Items.Add(_status);
            return strip;
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
            _animateBox.Enabled = radar;
            _status.Text = string.Format("逻辑尺寸 {0:0} × {1:0}（96dpi）{2}",
                logical.Width, logical.Height, radar ? "　点击目标选中，悬停高亮" : string.Empty);
        }

        private void ApplyAnimation()
        {
            if (_canvas.Interaction is RadarInteraction radar)
            {
                radar.IsAnimating = _animateBox.Checked;
            }
        }

        private void ApplyFit()
        {
            _canvas.Fit = _fitPicker.SelectedIndex == 0 ? ChartFit.Fit : ChartFit.Actual;
            _canvas.ScaleFactor = (float)_scaleBox.Value;
        }

        private void ExportPng()
        {
            if (_chart == null)
            {
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "PNG 图片|*.png",
                FileName = "chart-300dpi.png",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (FileStream stream = File.Create(dialog.FileName))
                {
                    _chart.WritePng(stream, 300f);
                }
                _status.Text = "已导出 " + dialog.FileName;
            }
        }

        private void ExportPdf()
        {
            if (_chart == null)
            {
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "PDF 文档|*.pdf",
                FileName = "chart.pdf",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (FileStream stream = File.Create(dialog.FileName))
                {
                    _chart.WritePdf(stream, PdfOptions.A4Portrait);
                }
                _status.Text = "已导出 " + dialog.FileName;
            }
        }

        private void ExportSvg()
        {
            if (_chart == null)
            {
                return;
            }

            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "SVG 矢量图|*.svg",
                FileName = "chart.svg",
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                using (FileStream stream = File.Create(dialog.FileName))
                {
                    _chart.WriteSvg(stream);
                }
                _status.Text = "已导出 " + dialog.FileName;
            }
        }

        private static Label MakeLabel(string text) => new Label
        {
            Text = text,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(8, 6, 2, 0),
        };

        private static Button MakeButton(string text, EventHandler onClick)
        {
            Button button = new Button { Text = text, AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
            button.Click += onClick;
            return button;
        }
    }
}
