using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OODD.Charts.Adapters.WinForms;
using OODD.Charts.Adapters.Wpf;
using OODD.Charts.Demo;
using OODD.Charts.Interaction;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;
using OODD.Charts.Style;
using SkiaSharp;
using WinFormsChartControl = OODD.Charts.Adapters.WinForms.ChartControl;

namespace OODD.Charts.AdapterCheck
{
    /// <summary>
    /// 桌面适配器像素一致性检查（T50/T51/T52 验收）。
    /// <para>
    /// 基准是内核自己的 <see cref="Chart.Encode(ChartImageFormat,float,int)"/> 输出：适配器只是把
    /// 同一次光栅化的字节搬到宿主位图里，所以判据是"逐字节完全相等"，不是"看起来一样"。
    /// </para>
    /// <para>
    /// 覆盖范围按"本机能不能起宿主"划线：WinForms / WPF / Avalonia（headless + Skia 真帧缓冲）在这里
    /// 有运行时证据；Eto 缺平台程序集（Eto.WinForms 等在本机的 NuGet 源上取不到），WinUI / Uno / MAUI
    /// 需要各自的宿主进程，这三类只有编译证据，交付文档里如实标注"仅编译验证"。
    /// </para>
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            string outDir = args.Length > 0 && !args[0].StartsWith("-")
                ? args[0]
                : DefaultOutDir();
            Directory.CreateDirectory(outDir);
            Console.WriteLine("输出目录: " + outDir);

            int failures = 0;
            byte[] temperatureReference;
            using (Chart chart = new Chart(TemperatureDoc(), new TemperatureChartTemplate(), new ChartStyle()))
            {
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                string referencePath = Path.Combine(outDir, "adapter-reference.png");
                File.WriteAllBytes(referencePath, png);
                byte[] reference = DecodePng(referencePath);
                temperatureReference = reference;

                Console.WriteLine("表单 96dpi: " + chart.MeasureSize().Width + " x " + chart.MeasureSize().Height
                                  + "，基准 PNG " + png.Length + " B");

                failures += CheckRaster(chart, reference);
                failures += CheckWinForms(chart, reference, outDir);
                failures += CheckWpf(chart, reference, outDir);
                failures += CheckWinFormsControl(chart, reference);
                failures += CheckWpfControl(chart);
                failures += CheckAvalonia(chart, reference, outDir);
            }

            // 新域表单走同一条像素桥：宿主位图与内核基准逐字节相等，且确实换了一张图（不是复用旧帧）。
            string[] newForms = { "well-log", "dga", "train-graph", "tlnp", "ast-report", "electrical-work-ticket" };
            foreach (string name in newForms)
            {
                failures += CheckSampleParity(name, temperatureReference, outDir);
            }

            failures += CheckInteraction();

            Console.WriteLine(failures == 0 ? "ALL ADAPTER CHECKS OK" : "FAILURES=" + failures);
            return failures == 0 ? 0 : 1;
        }

        /// <summary>
        /// 一张新域样例的三端像素桥：内核基准 vs WinForms vs WPF 逐字节相等，
        /// 并且与体温单基准至少有一个字节不同——否则"通过"可能只是拿旧帧比对旧帧。
        /// </summary>
        private static int CheckSampleParity(string name, byte[] temperatureReference, string outDir)
        {
            DemoSample? sample = DemoCatalog.Find(name);
            if (sample == null)
            {
                Console.WriteLine("  FAIL 样例 " + name + " 不在目录里");
                return 1;
            }

            int failures = 0;
            using (Chart chart = sample.Create())
            {
                Console.WriteLine("样例 " + name + "（" + sample.DisplayName + "）96dpi: "
                                  + chart.MeasureSize().Width + " x " + chart.MeasureSize().Height);
                string referencePath = Path.Combine(outDir, name + "-reference.png");
                File.WriteAllBytes(referencePath, chart.Encode(ChartImageFormat.Png, 96f, 100));
                byte[] reference = DecodePng(referencePath);

                int changed = 0;
                if (reference.Length == temperatureReference.Length)
                {
                    for (int i = 0; i < reference.Length; i++)
                    {
                        if (reference[i] != temperatureReference[i])
                        {
                            changed++;
                        }
                    }
                }
                else
                {
                    changed = 1;
                }
                failures += Assert(name + " 帧与体温单基准不同", changed > 0);

                using (System.Drawing.Bitmap bitmap = chart.ToBitmap(96f))
                {
                    failures += DiffReport(name + " WinForms ToBitmap", ReadGdi(bitmap), reference);
                    string path = Path.Combine(outDir, name + "-winforms-96dpi.png");
                    bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                    failures += DiffReport(name + " WinForms PNG 落盘回读", DecodePng(path), reference);
                }

                WriteableBitmap wpf = chart.ToWriteableBitmap(96d);
                int stride = wpf.PixelWidth * 4;
                byte[] pixels = new byte[stride * wpf.PixelHeight];
                wpf.CopyPixels(new Int32Rect(0, 0, wpf.PixelWidth, wpf.PixelHeight), pixels, stride, 0);
                failures += DiffReport(name + " WPF ToWriteableBitmap", pixels, reference);

                string wpfPath = Path.Combine(outDir, name + "-wpf-96dpi.png");
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(wpf));
                using (FileStream stream = File.Create(wpfPath))
                {
                    encoder.Save(stream);
                }
                failures += DiffReport(name + " WPF PNG 落盘回读", DecodePng(wpfPath), reference);
            }
            return failures;
        }

        private static string DefaultOutDir()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OODD.Charts.slnx")))
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir?.FullName ?? Path.GetTempPath(), "artifacts", "adapters");
        }

        // ------------------------------------------------------------------ 像素桥

        private static int CheckRaster(Chart chart, byte[] reference)
        {
            Raster raster = ChartRaster.Render(chart, 96f);
            int failures = 0;
            int expectedPixels = reference.Length;

            failures += Assert("ChartRaster 帧长与基准 PNG 解码后一致", raster.Pixels.Length == expectedPixels);
            failures += Assert("ChartRaster Stride = 宽×4", raster.Stride == raster.Width * 4);
            failures += DiffReport("ChartRaster.Render", raster.Pixels, reference);
            return failures;
        }

        private static int CheckWinForms(Chart chart, byte[] reference, string outDir)
        {
            int failures = 0;
            using (System.Drawing.Bitmap bitmap = chart.ToBitmap(96f))
            {
                failures += Assert("WinForms 位图尺寸 = 基准尺寸",
                    bitmap.Width * bitmap.Height * 4 == reference.Length);

                byte[] pixels = ReadGdi(bitmap);
                failures += DiffReport("WinForms ToBitmap", pixels, reference);

                string path = Path.Combine(outDir, "winforms-96dpi.png");
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                failures += DiffReport("WinForms PNG 落盘回读", DecodePng(path), reference);
                Console.WriteLine("  winforms-96dpi.png " + new FileInfo(path).Length + " B");
            }
            return failures;
        }

        private static int CheckWpf(Chart chart, byte[] reference, string outDir)
        {
            int failures = 0;
            WriteableBitmap bitmap = chart.ToWriteableBitmap(96d);
            int stride = bitmap.PixelWidth * 4;
            byte[] pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), pixels, stride, 0);

            failures += Assert("WPF 位图尺寸 = 基准尺寸", pixels.Length == reference.Length);
            failures += DiffReport("WPF ToWriteableBitmap", pixels, reference);

            string path = Path.Combine(outDir, "wpf-96dpi.png");
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path))
            {
                encoder.Save(stream);
            }
            failures += DiffReport("WPF PNG 落盘回读", DecodePng(path), reference);
            Console.WriteLine("  wpf-96dpi.png " + new FileInfo(path).Length + " B");
            return failures;
        }

        // ------------------------------------------------------------------ Avalonia（T52）

        /// <summary>
        /// Avalonia 像素桥。用 <c>Avalonia.Headless</c> + <c>Avalonia.Skia</c> 起无窗口后端，
        /// 并把 <c>UseHeadlessDrawing</c> 关掉：默认的 headless 绘制是空壳位图（写进去的像素读不出来），
        /// 会让逐字节比对给出假绿，所以必须用真 Skia 帧缓冲。
        /// </summary>
        private static int CheckAvalonia(Chart chart, byte[] reference, string outDir)
        {
            try
            {
                AvaloniaBootstrapper.InitializeHeadless();
            }
            catch (Exception ex)
            {
                Console.WriteLine("  FAIL Avalonia headless 初始化：" + ex.GetType().Name + " — " + ex.Message);
                return 1;
            }

            int failures = 0;
            using (Avalonia.Media.Imaging.WriteableBitmap bitmap =
                       OODD.Charts.Adapters.Avalonia.AvaloniaChartExtensions.ToWriteableBitmap(chart, 96d))
            {
                int width = bitmap.PixelSize.Width;
                int height = bitmap.PixelSize.Height;

                failures += Assert("Avalonia 位图尺寸 = 基准尺寸",
                    (long)width * height * 4L == reference.Length);
                failures += Assert("Avalonia DPI 写进位图元数据", bitmap.Dpi.X == 96d && bitmap.Dpi.Y == 96d);

                byte[] pixels;
                int rowBytes;
                using (Avalonia.Platform.ILockedFramebuffer locked = bitmap.Lock())
                {
                    rowBytes = locked.RowBytes;
                    pixels = ReadFramebuffer(locked.Address, width, height, rowBytes);
                }
                Console.WriteLine("  Avalonia 帧缓冲 " + width + "x" + height + "，RowBytes=" + rowBytes
                                  + "（宽×4=" + width * 4 + "）");

                failures += DiffReport("Avalonia ToWriteableBitmap", pixels, reference);

                string path = Path.Combine(outDir, "avalonia-96dpi.png");
                using (FileStream stream = File.Create(path))
                {
                    bitmap.Save(stream);
                }
                failures += DiffReport("Avalonia PNG 落盘回读", DecodePng(path), reference);
                Console.WriteLine("  avalonia-96dpi.png " + new FileInfo(path).Length + " B");
            }
            return failures;
        }

        /// <summary>按宿主行距搬回托管缓冲：宿主帧缓冲可能带行对齐填充，不能整块拷。</summary>
        private static byte[] ReadFramebuffer(nint source, int width, int height, int rowBytes)
        {
            int stride = width * 4;
            byte[] buffer = new byte[stride * height];
            unsafe
            {
                for (int y = 0; y < height; y++)
                {
                    new ReadOnlySpan<byte>((void*)IntPtr.Add(source, y * rowBytes), stride)
                        .CopyTo(new Span<byte>(buffer, y * stride, stride));
                }
            }
            return buffer;
        }

        // ------------------------------------------------------------------ 宿主控件

        private static int CheckWinFormsControl(Chart chart, byte[] reference)
        {
            int failures = 0;
            const int AreaWidth = 800;
            const int AreaHeight = 600;

            SKSize logical = chart.MeasureSize();
            int chartWidth = (int)(logical.Width);
            int chartHeight = (int)(logical.Height);

            // 表单自己的墨迹包围盒：版面四周留白不是 0，控件的期望位置必须叠加这个内偏移。
            InkBounds(reference, chartWidth, chartHeight, out int inkLeft, out int inkTop, out int inkRight, out int inkBottom);

            using (WinFormsChartControl control = new WinFormsChartControl())
            {
                control.Size = new System.Drawing.Size(AreaWidth, AreaHeight);
                control.Chart = chart;
                control.Fit = ChartFit.Fit;
                control.ScaleFactor = 1f;
                control.Dpi = 0f;

                failures += Assert("WinForms ChartControl 回读 Chart 引用", ReferenceEquals(control.Chart, chart));
                failures += Assert("ScaleFactor <= 0 抛异常", Throws<ArgumentOutOfRangeException>(() => control.ScaleFactor = 0f));
                failures += Assert("Dpi < 0 抛异常", Throws<ArgumentOutOfRangeException>(() => control.Dpi = -1f));

                using (System.Drawing.Bitmap shot = new System.Drawing.Bitmap(AreaWidth, AreaHeight))
                {
                    control.DrawToBitmap(shot, new System.Drawing.Rectangle(0, 0, AreaWidth, AreaHeight));
                    InkBounds(shot, out int left, out int top, out int right, out int bottom);

                    float scale = Math.Min(AreaWidth / logical.Width, AreaHeight / logical.Height);
                    int originX = (int)Math.Round((AreaWidth - logical.Width * scale) / 2f);
                    int originY = (int)Math.Round((AreaHeight - logical.Height * scale) / 2f);
                    int expectLeft = (int)Math.Round(originX + inkLeft * scale);
                    int expectTop = (int)Math.Round(originY + inkTop * scale);
                    int expectRight = (int)Math.Round(originX + inkRight * scale);
                    int expectBottom = (int)Math.Round(originY + inkBottom * scale);

                    Console.WriteLine("  Fit 墨迹 " + left + "," + top + "→" + right + "," + bottom +
                                      " 期望 " + expectLeft + "," + expectTop + "→" + expectRight + "," + expectBottom +
                                      "（缩放 " + scale.ToString("0.####") + "）");
                    failures += Assert("Fit 模式水平居中且等比",
                        Math.Abs(left - expectLeft) <= 2 && Math.Abs(right - expectRight) <= 2);
                    failures += Assert("Fit 模式垂直居中且不裁切",
                        Math.Abs(top - expectTop) <= 2 && Math.Abs(bottom - expectBottom) <= 2);
                }

                control.Fit = ChartFit.Actual;
                control.ScaleFactor = 2f;
                using (System.Drawing.Bitmap shot = new System.Drawing.Bitmap(AreaWidth, AreaHeight))
                {
                    control.DrawToBitmap(shot, new System.Drawing.Rectangle(0, 0, AreaWidth, AreaHeight));
                    InkBounds(shot, out int left, out int top, out int right, out int bottom);

                    int expectLeft = inkLeft * 2;
                    int expectTop = inkTop * 2;
                    int expectRight = Math.Min(inkRight * 2, AreaWidth - 1);
                    Console.WriteLine("  Actual×2 墨迹 " + left + "," + top + "→" + right + "," + bottom +
                                      " 期望 " + expectLeft + "," + expectTop + "→" + expectRight + "（超出 " + AreaWidth + " 裁切）");
                    failures += Assert("Actual 模式从原点起画（含版面自身内偏移）",
                        Math.Abs(left - expectLeft) <= 2 && Math.Abs(top - expectTop) <= 2);
                    failures += Assert("Actual 模式按 ScaleFactor 出图，超宽部分裁切",
                        Math.Abs(right - expectRight) <= 2);
                }
            }
            return failures;
        }

        private static int CheckWpfControl(Chart chart)
        {
            int failures = 0;
            OODD.Charts.Adapters.Wpf.ChartControl control = new OODD.Charts.Adapters.Wpf.ChartControl();
            control.Chart = chart;

            SKSize logical = chart.MeasureSize();
            control.Measure(new Size(1600d, 1200d));
            failures += Assert("WPF ChartControl 自然尺寸 = 96dpi 逻辑尺寸",
                Math.Abs(control.DesiredSize.Width - logical.Width) < 1d
                && Math.Abs(control.DesiredSize.Height - logical.Height) < 1d);

            control.ScaleFactor = 2d;
            control.Measure(new Size(1600d, 1200d));
            failures += Assert("WPF ScaleFactor 参与测量",
                Math.Abs(control.DesiredSize.Width - logical.Width * 2d) < 1d);

            failures += Assert("WPF ScaleFactor <= 0 被依赖属性校验拒绝",
                Throws<ArgumentException>(() => control.ScaleFactor = 0d));
            failures += Assert("WPF Dpi < 0 被依赖属性校验拒绝",
                Throws<ArgumentException>(() => control.Dpi = -1d));
            return failures;
        }

        // ------------------------------------------------------------------ 交互

        /// <summary>
        /// 控件级交互验收：指针消息经由宿主的消息管线进入 <see cref="WinFormsChartControl"/>，
        /// 由控件自己换算坐标、交给交互层，再回写文档并重绘。
        /// 这条链路证明"拿到控件就能用"，不依赖任何应用侧手写事件代码。
        /// </summary>
        private static int CheckInteraction()
        {
            int failures = 0;
            const int AreaWidth = 800;
            const int AreaHeight = 600;
            const int Down = 0x0201;
            const int Up = 0x0202;
            const int Move = 0x0200;
            const int MkLButton = 0x0001;

            RadarDocument doc = new RadarDocument
            {
                FormTitle = "交互验收",
                Scope = RadarScope.Full,
                MaxRange = 40d,
                RangeUnit = "km",
                RingCount = 4,
                ShowSweep = false,
                ShowTrails = false,
            };
            doc.Targets.Add(new RadarTarget("A") { Label = "甲", Bearing = 60d, Distance = 20d });
            doc.Targets.Add(new RadarTarget("B") { Label = "乙", Bearing = 210d, Distance = 12d });

            using (Chart chart = new Chart(doc, new RadarChartTemplate(), new ChartStyle()))
            using (WinFormsChartControl control = new WinFormsChartControl())
            {
                control.Size = new System.Drawing.Size(AreaWidth, AreaHeight);
                control.Chart = chart;
                control.Fit = ChartFit.Fit;
                control.ScaleFactor = 1f;
                control.Dpi = 0f;

                RadarInteraction? interaction = RadarInteraction.TryCreate(chart);
                control.Interaction = interaction;
                failures += Assert("WinForms 控件挂上雷达交互", ReferenceEquals(control.Interaction, interaction));
                if (interaction == null)
                {
                    return failures + 1;
                }

                System.Reflection.FieldInfo? timerField = typeof(WinFormsChartControl).GetField(
                    "_animationTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                System.Windows.Forms.Timer? timer = timerField?.GetValue(control) as System.Windows.Forms.Timer;
                failures += Assert("开启动画时控件自动起帧定时器", timer != null && timer.Enabled);
                interaction.IsAnimating = false;
                failures += Assert("关闭动画时控件自动停帧定时器", timer != null && !timer.Enabled);
                interaction.IsAnimating = true;

                // 先出一帧：控件在这一步记下视口，后续指针换算全部基于它。
                byte[] plain = Paint(control, AreaWidth, AreaHeight);

                SKSize logical = chart.MeasureSize();
                RadarLayout layout = new RadarChartTemplate().Layout(doc);
                float fit = Math.Min(AreaWidth / logical.Width, AreaHeight / logical.Height);
                int frameWidth = Math.Max(1, (int)Math.Round(logical.Width * fit));
                int frameHeight = Math.Max(1, (int)Math.Round(logical.Height * fit));
                ChartViewport viewport = ChartViewport.FromPlacement(
                    logical,
                    frameWidth / Math.Max(1f, logical.Width),
                    (AreaWidth - frameWidth) / 2f,
                    (AreaHeight - frameHeight) / 2f);

                SKPoint atTarget = viewport.ToView(layout.PointOf(doc.Targets[0].Bearing, doc.Targets[0].Distance));
                IntPtr handle = control.Handle;
                SendMessage(handle, Move, IntPtr.Zero, LParam(atTarget));
                failures += Assert("指针移到目标上：控件转发悬停并写回文档", doc.HoverTargetId == "A");

                SendMessage(handle, Down, (IntPtr)MkLButton, LParam(atTarget));
                SendMessage(handle, Up, IntPtr.Zero, LParam(atTarget));
                failures += Assert("指针点击目标：控件换算坐标后选中该目标", doc.SelectedTargetId == "A");

                byte[] selected = Paint(control, AreaWidth, AreaHeight);
                failures += Assert("选中后控件确实出了新的一帧", !SequenceEqual(plain, selected));

                // 盘面中心没有目标（最近的目标在 12 圈层外），点击应取消选中。
                SKPoint atEmpty = viewport.ToView(new SKPoint(layout.Center.X, layout.Center.Y));
                SendMessage(handle, Down, (IntPtr)MkLButton, LParam(atEmpty));
                SendMessage(handle, Up, IntPtr.Zero, LParam(atEmpty));
                failures += Assert("点击盘内空白：取消选中", doc.SelectedTargetId == null);

                // 版面外的留白不属于交互区，控件不应把它当成"点空白"。
                doc.SelectedTargetId = "B";
                SKPoint outside = viewport.ToView(new SKPoint(-60f, -60f));
                SendMessage(handle, Down, (IntPtr)MkLButton, LParam(outside));
                SendMessage(handle, Up, IntPtr.Zero, LParam(outside));
                failures += Assert("点击版面外留白：保持当前选中", doc.SelectedTargetId == "B");

                timer?.Stop();
            }

            return failures;
        }

        private static byte[] Paint(WinFormsChartControl control, int width, int height)
        {
            using (System.Drawing.Bitmap shot = new System.Drawing.Bitmap(width, height))
            {
                control.DrawToBitmap(shot, new System.Drawing.Rectangle(0, 0, width, height));
                return ReadGdi(shot);
            }
        }

        private static IntPtr LParam(SKPoint point)
        {
            int x = (int)Math.Round(point.X);
            int y = (int)Math.Round(point.Y);
            return (IntPtr)((y << 16) | (x & 0xFFFF));
        }

        private static bool SequenceEqual(byte[] left, byte[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }
            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }
            return true;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        // ------------------------------------------------------------------ 工具

        private static int Assert(string label, bool condition)
        {
            Console.WriteLine((condition ? "  ok   " : "  FAIL ") + label);
            return condition ? 0 : 1;
        }

        private static bool Throws<TException>(Action action) where TException : Exception
        {
            try
            {
                action();
                return false;
            }
            catch (TException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>逐字节比对，报出差异字节数与最大单通道偏差。</summary>
        private static int DiffReport(string label, byte[] actual, byte[] expected)
        {
            if (actual.Length != expected.Length)
            {
                Console.WriteLine("  FAIL " + label + " 长度不同: " + actual.Length + " vs " + expected.Length);
                return 1;
            }

            int differences = 0;
            int worst = 0;
            for (int i = 0; i < actual.Length; i++)
            {
                if (actual[i] == expected[i])
                {
                    continue;
                }
                differences++;
                int delta = Math.Abs(actual[i] - expected[i]);
                if (delta > worst)
                {
                    worst = delta;
                }
            }

            Console.WriteLine((differences == 0 ? "  ok   " : "  FAIL ") + label +
                              " 差异字节 " + differences + " / " + actual.Length +
                              (differences == 0 ? string.Empty : "，最大单通道偏差 " + worst));
            return differences == 0 ? 0 : 1;
        }

        private static byte[] ReadGdi(System.Drawing.Bitmap bitmap)
        {
            System.Drawing.Rectangle rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
                rect,
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            try
            {
                byte[] buffer = new byte[Math.Abs(data.Stride) * data.Height];
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
                return buffer;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static byte[] DecodePng(string path)
        {
            using (SKImage? image = SKImage.FromEncodedData(path))
            {
                if (image == null)
                {
                    throw new InvalidDataException("无法解码 PNG: " + path);
                }
                SKImageInfo info = new SKImageInfo(image.Width, image.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                byte[] buffer = new byte[info.BytesSize];
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, 0, 0))
                    {
                        throw new InvalidDataException("ReadPixels 失败: " + path);
                    }
                }
                finally
                {
                    handle.Free();
                }
                return buffer;
            }
        }

        /// <summary>非白像素的包围盒，用来量控件的贴合与居中是否真的成立。</summary>
        private static void InkBounds(System.Drawing.Bitmap bitmap, out int left, out int top, out int right, out int bottom)
        {
            InkBounds(ReadGdi(bitmap), bitmap.Width, bitmap.Height, out left, out top, out right, out bottom);
        }

        private static void InkBounds(byte[] bgra, int width, int height, out int left, out int top, out int right, out int bottom)
        {
            left = int.MaxValue;
            top = int.MaxValue;
            right = -1;
            bottom = -1;

            for (int y = 0; y < height; y++)
            {
                int row = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x * 4;
                    bool ink = !(bgra[index] == 255 && bgra[index + 1] == 255 && bgra[index + 2] == 255);
                    if (!ink)
                    {
                        continue;
                    }
                    if (x < left)
                    {
                        left = x;
                    }
                    if (x > right)
                    {
                        right = x;
                    }
                    if (y < top)
                    {
                        top = y;
                    }
                    if (y > bottom)
                    {
                        bottom = y;
                    }
                }
            }

            if (left == int.MaxValue)
            {
                left = top = 0;
                right = bottom = -1;
            }
        }

        private static TemperatureChartDocument TemperatureDoc()
        {
            TemperatureChartDocument doc = new TemperatureChartDocument
            {
                HospitalName = "适配器检查医院",
                FormTitle = "体温单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "测试";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "48";
            doc.Patient.Department = "产科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "12床";
            doc.Patient.AdmissionNumber = "20260417";
            doc.Patient.AdmissionDate = "2026-04-17";

            DateTime start = new DateTime(2026, 4, 17);
            for (int day = 0; day < 3; day++)
            {
                doc.Days.Add(new ChartGroup(start.AddDays(day)));
            }

            PointSeries axillary = new PointSeries("axillary", TemperatureChartTemplate.AxisTemperature)
            {
                Symbol = MarkerSymbol.Ring,
                Color = SKColors.Blue,
                LegendText = "腋表",
            };
            PointSeries pulse = new PointSeries("pulse", TemperatureChartTemplate.AxisPulse)
            {
                Symbol = MarkerSymbol.Dot,
                Color = SKColors.Red,
                LegendText = "脉搏",
            };

            double[] temps =
            {
                36.8, 37.2, 36.9, 37.4, 38.1, 37.6,
                37.0, 37.3, 36.8, 37.1, 36.9, 37.0,
                38.6, 39.2, 38.4, 37.8, 37.2, 36.9,
            };
            double[] pulses =
            {
                80, 84, 78, 90, 96, 88,
                76, 82, 79, 85, 80, 77,
                104, 118, 110, 98, 92, 86,
            };
            for (int i = 0; i < temps.Length; i++)
            {
                axillary.Values[i] = temps[i];
                pulse.Values[i] = pulses[i];
            }
            doc.Series.Add(axillary);
            doc.Series.Add(pulse);

            doc.Cells.Add(new CellValue(TemperatureFields.Respiration, 5, "20"));
            doc.Cells.Add(new CellValue(TemperatureFields.Respiration, 11, "22"));
            doc.Cells.Add(new CellValue(TemperatureFields.Respiration, 17, "18"));
            doc.Cells.Add(new CellValue(TemperatureFields.BloodPressure, 12, "125/80"));
            doc.Cells.Add(new CellValue(TemperatureFields.Weight, 0, "54.2"));

            doc.Events.Add(new EventMark(0, "入院"));
            doc.Events.Add(new EventMark(12, "转出"));

            doc.Legend.Add(new LegendItem(MarkerSymbol.Dot, SKColors.Red, "脉搏"));
            doc.Legend.Add(new LegendItem(MarkerSymbol.Ring, SKColors.Blue, "腋表"));
            return doc;
        }
    }
}
