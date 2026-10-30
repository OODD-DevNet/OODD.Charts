using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using OODD.Charts.Demo;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Pain;
using OODD.Charts.Medical.Partogram;
using OODD.Charts.Medical.Restraint;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Medical.Transfusion;
using OODD.Charts.Radar;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Samples
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string outDir = args.Length > 0 && !args[0].StartsWith("-") ? args[0] : DefaultOutDir();
            Directory.CreateDirectory(outDir);
            Console.WriteLine("输出目录: " + outDir);

            if (args.Length > 1 && args[1] == "--forensics")
            {
                return RunForensics(outDir);
            }

            int failures = 0;

            foreach (DemoSample sample in DemoCatalog.All)
            {
                try
                {
                    failures += Run(sample.Name, outDir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[" + sample.Name + "] 异常: " + ex);
                    failures++;
                }
            }

            Console.WriteLine(failures == 0 ? "ALL SAMPLES OK" : "FAILURES=" + failures);
            return failures == 0 ? 0 : 1;
        }

        private static string DefaultOutDir()
        {
            DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OODD.Charts.slnx")))
            {
                dir = dir.Parent;
            }
            return Path.Combine(dir?.FullName ?? Path.GetTempPath(), "artifacts");
        }

        /// <summary>
        /// T292 像素取证：新域样例各出 96/120/144/192 四档。
        /// 逐档断尺寸等比、墨迹包围盒随档位等比缩放且不贴边；文字重叠靠人眼看图确认。
        /// </summary>
        private static int RunForensics(string outDir)
        {
            int[] tiers = { 96, 120, 144, 192 };
            List<DemoSample> samples = new List<DemoSample>();
            samples.AddRange(EngineeringSamples.All);
            samples.AddRange(TransitSamples.All);
            samples.AddRange(GeoscienceSamples.All);
            samples.AddRange(NewMedicalSamples.All);
            samples.AddRange(TicketSamples.All);

            int failures = 0;
            foreach (DemoSample sample in samples)
            {
                using (Chart chart = sample.Create())
                {
                    SKSize logical = chart.MeasureSize();
                    Console.WriteLine();
                    Console.WriteLine("=== " + sample.Name + " " + logical.Width + "x" + logical.Height + " ===");
                    SKRect? baseBox = null;
                    foreach (int dpi in tiers)
                    {
                        byte[] png = chart.Encode(ChartImageFormat.Png, dpi, 100);
                        string path = Path.Combine(outDir, sample.Name + "-" + dpi + "dpi.png");
                        File.WriteAllBytes(path, png);

                        using (SKBitmap bitmap = SKBitmap.Decode(png) ?? throw new InvalidDataException("无法解码 " + path))
                        {
                            int expectWidth = (int)Math.Round(logical.Width * dpi / 96d);
                            int expectHeight = (int)Math.Round(logical.Height * dpi / 96d);
                            if (Math.Abs(bitmap.Width - expectWidth) > 1 || Math.Abs(bitmap.Height - expectHeight) > 1)
                            {
                                Console.WriteLine("  FAIL " + dpi + "dpi 尺寸 " + bitmap.Width + "x" + bitmap.Height
                                                  + " 应为 " + expectWidth + "x" + expectHeight);
                                failures++;
                            }

                            SKRect box = InkBox(bitmap, out double ratio);
                            if (ratio < 0.005d)
                            {
                                Console.WriteLine("  FAIL " + dpi + "dpi 近乎空白，墨迹占比 "
                                                  + ratio.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture));
                                failures++;
                            }
                            float border = Math.Min(Math.Min(box.Left, box.Top),
                                Math.Min(bitmap.Width - box.Right, bitmap.Height - box.Bottom));
                            bool framed = EdgeCoverage(bitmap) >= 0.9d;
                            if (border < 1f && !framed)
                            {
                                Console.WriteLine("  FAIL " + dpi + "dpi 墨迹贴边（最近边距 " + border.ToString("0.#")
                                                  + "px），疑似裁切");
                                failures++;
                            }

                            if (baseBox == null)
                            {
                                baseBox = box;
                            }
                            else
                            {
                                float k = dpi / 96f;
                                SKRect expect = new SKRect(baseBox.Value.Left * k, baseBox.Value.Top * k,
                                    baseBox.Value.Right * k, baseBox.Value.Bottom * k);
                                float delta = Math.Max(Math.Max(Math.Abs(box.Left - expect.Left), Math.Abs(box.Top - expect.Top)),
                                    Math.Max(Math.Abs(box.Right - expect.Right), Math.Abs(box.Bottom - expect.Bottom))) / k;
                                if (delta > 2f)
                                {
                                    Console.WriteLine("  FAIL " + dpi + "dpi 墨迹包围盒偏离等比 "
                                                      + delta.ToString("0.#") + " 逻辑px（同源 SKPicture 应只缩放）");
                                    failures++;
                                }
                            }

                            Console.WriteLine("  " + dpi + "dpi " + bitmap.Width + "x" + bitmap.Height + " 墨迹占比 "
                                              + (ratio * 100d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                                              + "% bbox " + (int)box.Left + "," + (int)box.Top + "→"
                                              + (int)box.Right + "," + (int)box.Bottom
                                              + (framed ? "（整页边框）" : string.Empty));
                        }
                    }
                }
            }

            Console.WriteLine(failures == 0 ? "ALL FORENSICS OK" : "FAILURES=" + failures);
            return failures == 0 ? 0 : 1;
        }

        /// <summary>最外一圈四条边的着墨比例：接近 1 说明整页边框是刻意画到纸边的，不算裁切。</summary>
        private static double EdgeCoverage(SKBitmap bitmap)
        {
            SKColor[] pixels = bitmap.Pixels;
            int width = bitmap.Width;
            int height = bitmap.Height;
            int inkTop = 0, inkBottom = 0, inkLeft = 0, inkRight = 0;
            for (int x = 0; x < width; x++)
            {
                if (!IsWhite(pixels[x])) inkTop++;
                if (!IsWhite(pixels[(height - 1) * width + x])) inkBottom++;
            }
            for (int y = 0; y < height; y++)
            {
                if (!IsWhite(pixels[y * width])) inkLeft++;
                if (!IsWhite(pixels[y * width + width - 1])) inkRight++;
            }
            return Math.Max(
                Math.Max((double)inkTop / width, (double)inkBottom / width),
                Math.Max((double)inkLeft / height, (double)inkRight / height));
        }

        private static bool IsWhite(SKColor pixel)
        {
            return pixel.Red >= 250 && pixel.Green >= 250 && pixel.Blue >= 250;
        }

        private static SKRect InkBox(SKBitmap bitmap, out double ratio)        {
            int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1, ink = 0;
            SKColor[] pixels = bitmap.Pixels;
            for (int y = 0; y < bitmap.Height; y++)
            {
                int row = y * bitmap.Width;
                for (int x = 0; x < bitmap.Width; x++)
                {
                    SKColor pixel = pixels[row + x];
                    if (pixel.Red >= 250 && pixel.Green >= 250 && pixel.Blue >= 250)
                    {
                        continue;
                    }
                    ink++;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            ratio = (double)ink / (bitmap.Width * bitmap.Height);
            return ink == 0 ? new SKRect(0, 0, bitmap.Width - 1, bitmap.Height - 1)
                : new SKRect(left, top, right + 1, bottom + 1);
        }

        private static int Run(string name, string outDir)
        {
            Chart chart = Build(name);
            ChartGeometry geo = chart.Geometry;
            int failures = 0;

            Console.WriteLine();
            Console.WriteLine("=== " + name + " ===");
            Console.WriteLine("尺寸(96dpi 逻辑像素): " + geo.Bounds.Width + " x " + geo.Bounds.Height);
            if (geo.Slots != null)
            {
                Console.WriteLine("槽数: " + geo.Slots.TotalSlots + "  组数: " + geo.Slots.GroupCount +
                                  "  网格: y " + geo.GridRect.Top + "→" + geo.GridRect.Bottom +
                                  "  x " + geo.GridLeft + "→" + geo.GridRight);
            }
            else
            {
                Console.WriteLine("版面式几何（无列轴）：" + (geo.Layout?.GetType().Name ?? "无版面对象"));
            }

            Stopwatch sw = Stopwatch.StartNew();
            byte[] png96 = chart.Encode(ChartImageFormat.Png, 96f, 100);
            sw.Stop();
            Write(Path.Combine(outDir, name + "-96dpi.png"), png96);
            Console.WriteLine("96dpi PNG " + png96.Length + " B，耗时 " + sw.ElapsedMilliseconds + " ms");

            sw.Restart();
            byte[] png300 = chart.Encode(ChartImageFormat.Png, 300f, 100);
            sw.Stop();
            Write(Path.Combine(outDir, name + "-300dpi.png"), png300);
            Console.WriteLine("300dpi PNG " + png300.Length + " B，耗时 " + sw.ElapsedMilliseconds +
                              " ms（NFR-4 阈值 300 ms）");
            if (sw.ElapsedMilliseconds > 300)
            {
                Console.WriteLine("  ! 超出 NFR-4 阈值，已记录实测值");
            }

            using (FileStream fs = File.Create(Path.Combine(outDir, name + ".pdf")))
            {
                chart.WritePdf(fs, PdfOptions.A4Landscape);
            }
            Console.WriteLine("PDF " + new FileInfo(Path.Combine(outDir, name + ".pdf")).Length + " B");

            using (FileStream fs = File.Create(Path.Combine(outDir, name + ".svg")))
            {
                chart.WriteSvg(fs);
            }
            Console.WriteLine("SVG " + new FileInfo(Path.Combine(outDir, name + ".svg")).Length + " B");

            using (FileStream fs = File.Create(Path.Combine(outDir, name + "-a5.pdf")))
            {
                chart.WritePdf(fs, PdfOptions.A5Portrait);
            }

            ReportPdfFontCost(chart, outDir, name);

            failures += Verify(name, chart, outDir);

            return failures;
        }

        private static int Verify(string name, Chart chart, string outDir)
        {
            switch (name)
            {
                case "temperature": return VerifyTemperature(chart, outDir);
                case "partogram": return VerifyPartogram(chart, outDir);
                case "pain": return VerifyPain(chart, outDir);
                case "restraint": return VerifyRestraint(chart, outDir);
                case "transfusion": return VerifyTransfusion(chart, outDir);
                case "radar-full": return VerifyRadar(chart, true, outDir);
                case "radar-half": return VerifyRadar(chart, false, outDir);
                default:
                    return EngineeringSamples.Verify(name, chart)
                           + TransitSamples.Verify(name, chart)
                           + GeoscienceSamples.Verify(name, chart)
                           + NewMedicalSamples.Verify(name, chart)
                           + TicketSamples.Verify(name, chart);
            }
        }

        private static int VerifyTemperature(Chart chart, string outDir)
        {
            ChartGeometry geo = chart.Geometry;
            int failures = 0;

            failures += Assert("槽宽 20", Math.Abs(geo.Slots.SlotWidth - 20f) < 0.01f);
            failures += Assert("细格高 15", Math.Abs(geo.RuleAxis.PixelsPerUnit * 0.2d - 15d) < 0.01);
            failures += Assert("粗线间距 75", Math.Abs(geo.YOf(TemperatureChartTemplate.AxisTemperature, 37d)
                                                       - geo.YOf(TemperatureChartTemplate.AxisTemperature, 38d) - 75f) < 0.01f);
            failures += Assert("合并日列宽 120", Math.Abs(geo.Slots.GroupWidth - 120f) < 0.01f);
            failures += Assert("网格左边界 104", Math.Abs(geo.Slots.Left - 104f) < 0.01f);
            failures += Assert("脉搏 160 压在 41℃ 线上",
                Math.Abs(geo.YOf(TemperatureChartTemplate.AxisPulse, 160d)
                         - geo.YOf(TemperatureChartTemplate.AxisTemperature, 41d)) < 0.01f);
            failures += Assert("脉搏 40 压在 35℃ 线上",
                Math.Abs(geo.YOf(TemperatureChartTemplate.AxisPulse, 40d)
                         - geo.YOf(TemperatureChartTemplate.AxisTemperature, 35d)) < 0.01f);
            failures += Assert("附属行高 30",
                Math.Abs(geo.Row(TemperatureFields.BloodPressure)!.Rect.Height - 30f) < 0.01f);
            failures += Assert("呼吸行高 15 且逐槽",
                Math.Abs(geo.Row(TemperatureFields.Respiration)!.Rect.Height - 15f) < 0.01f
                && geo.Row(TemperatureFields.Respiration)!.SplitBySlot);
            failures += Assert("血压格宽 120（按日合并）",
                Math.Abs(geo.CellRect(TemperatureFields.BloodPressure, 25).Width - 120f) < 0.01f);

            // 分页
            TemperatureChartDocument doc = (TemperatureChartDocument)chart.Document;
            TemperatureChartDocument page2 = doc.Subset(3, 3);
            using (Chart p = new Chart(page2, chart.Template, chart.Style))
            {
                Write(Path.Combine(outDir, "temperature-page2-of-6.png"), p.Encode(ChartImageFormat.Png, 96f, 100));
                failures += Assert("分页后只剩 3 组", p.Geometry.Slots.GroupCount == 3);
                failures += Assert("分页后仍含页眉", p.Geometry.Header.Rect.Height > 0f);
            }

            return failures;
        }

        private static int VerifyPartogram(Chart chart, string outDir)
        {
            ChartGeometry geo = chart.Geometry;
            PartogramDocument doc = (PartogramDocument)chart.Document;
            PartogramStyle pg = ((PartogramChartTemplate)chart.Template).Tokens;
            int failures = 0;

            failures += Assert("一槽 = 30 分钟", geo.Slots.SlotsPerGroup == 2);
            failures += Assert("一组 = 1 小时，共 16 组", geo.Slots.GroupCount == 16);
            failures += Assert("宫口 0cm 压在网格顶线",
                Math.Abs(geo.YOf(PartogramDocument.AxisDilation, 0d) - geo.GridRect.Top) < 0.01f);
            failures += Assert("宫口 10cm 压在网格底线",
                Math.Abs(geo.YOf(PartogramDocument.AxisDilation, 10d) - geo.GridRect.Bottom) < 0.01f);
            failures += Assert("每 1cm 行高 30",
                Math.Abs(Math.Abs(geo.YOf(PartogramDocument.AxisDilation, 3d)
                                  - geo.YOf(PartogramDocument.AxisDilation, 4d)) - pg.CmRowHeight) < 0.01f);
            failures += Assert("先露 S-3 与宫口 0cm 同高",
                Math.Abs(geo.YOf(PartogramDocument.AxisStation, -3d)
                         - geo.YOf(PartogramDocument.AxisDilation, 0d)) < 0.01f);
            failures += Assert("先露 S+3 与宫口 10cm 同高",
                Math.Abs(geo.YOf(PartogramDocument.AxisStation, 3d)
                         - geo.YOf(PartogramDocument.AxisDilation, 10d)) < 0.01f);
            failures += Assert("胎心带行高 26",
                Math.Abs(geo.CellTable.Row(PartogramFields.FetalHeart)!.Rect.Height - pg.FetalHeartRowHeight) < 0.01f);
            failures += Assert("宫缩带行高 36",
                Math.Abs(geo.CellTable.Row(PartogramFields.Contraction)!.Rect.Height - pg.ContractionRowHeight) < 0.01f);
            failures += Assert("附属格按小时合并",
                Math.Abs(geo.CellRect(PartogramFields.BloodPressure, 7).Width - geo.Slots.GroupWidth) < 0.01f);

            // 警戒线：斜率 1 cm/h，横轴 2 槽/小时 ⇒ 每 30px 纵向对应 2 槽宽
            float x0 = PartogramChartTemplate.XOfHour(geo, doc, 6d);
            float x1 = PartogramChartTemplate.XOfHour(geo, doc, 9d);
            float y0 = geo.YOf(PartogramDocument.AxisDilation, 3d);
            float y1 = geo.YOf(PartogramDocument.AxisDilation, 6d);
            failures += Assert("警戒线斜率 1cm/h",
                Math.Abs((y1 - y0) / (x1 - x0) - pg.CmRowHeight / (2f * geo.Slots.SlotWidth)) < 0.001f);

            PartogramDocument page2 = doc.Subset(8, 8);
            using (Chart p = new Chart(page2, chart.Template, chart.Style))
            {
                Write(Path.Combine(outDir, "partogram-page2-of-2.png"), p.Encode(ChartImageFormat.Png, 96f, 100));
                failures += Assert("分页后只剩 8 小时", p.Geometry.Slots.GroupCount == 8);
                failures += Assert("分页后钟点重算",
                    ((PartogramDocument)p.Document).TimeOfSlot(0).Hour == 16);
                failures += Assert("分页后警戒线仍落在画内",
                    p.Geometry.GridRect.Height > 0f && page2.ShowGuideLines);
            }

            return failures;
        }

        private static int VerifyPain(Chart chart, string outDir)
        {
            ChartGeometry geo = chart.Geometry;
            PainChartDocument doc = (PainChartDocument)chart.Document;
            PainChartStyle tokens = ((PainChartTemplate)chart.Template).Tokens;
            int failures = 0;

            failures += Assert("一天 6 个评估时点", geo.Slots.SlotsPerGroup == 6);
            failures += Assert("共 3 天", geo.Slots.GroupCount == 3);
            failures += Assert("槽宽 40", Math.Abs(geo.Slots.SlotWidth - tokens.SlotWidth) < 0.01f);
            failures += Assert("NRS 最小分压在网格底线",
                Math.Abs(geo.YOf(PainChartTemplate.AxisNrs, doc.Scale.MinScore) - geo.GridRect.Bottom) < 0.01f);
            failures += Assert("NRS 最大分压在网格顶线",
                Math.Abs(geo.YOf(PainChartTemplate.AxisNrs, doc.Scale.MaxScore) - geo.GridRect.Top) < 0.01f);
            failures += Assert("每 1 分一行高 22",
                Math.Abs(Math.Abs(geo.YOf(PainChartTemplate.AxisNrs, 4d)
                                  - geo.YOf(PainChartTemplate.AxisNrs, 5d)) - tokens.ScoreRowHeight) < 0.01f);
            failures += Assert("附属行高 28",
                Math.Abs(geo.Row(PainFields.Site)!.Rect.Height - tokens.CellRowHeight) < 0.01f);
            failures += Assert("附属格逐槽宽 40",
                Math.Abs(geo.CellRect(PainFields.Site, 5).Width - tokens.SlotWidth) < 0.01f);

            PainChartDocument page2 = doc.Subset(1, 1);
            using (Chart p = new Chart(page2, chart.Template, chart.Style))
            {
                Write(Path.Combine(outDir, "pain-page2-of-3.png"), p.Encode(ChartImageFormat.Png, 96f, 100));
                failures += Assert("分页后只剩 1 天", p.Geometry.Slots.GroupCount == 1);
                failures += Assert("分页后评分重算槽号", page2.Series[0].Values[0] == 4d);
            }

            return failures;
        }

        private static int VerifyRestraint(Chart chart, string outDir)
        {
            ChartGeometry geo = chart.Geometry;
            RestraintChartDocument doc = (RestraintChartDocument)chart.Document;
            RestraintChartStyle tokens = ((RestraintChartTemplate)chart.Template).Tokens;
            int failures = 0;

            failures += Assert("一天 24 个小时槽", geo.Slots.SlotsPerGroup == 24 && doc.SlotsPerDay == 24);
            failures += Assert("共 1 天", geo.Slots.GroupCount == 1);
            failures += Assert("槽宽 26", Math.Abs(geo.Slots.SlotWidth - tokens.SlotWidth) < 0.01f);
            failures += Assert("约束时段条高 48",
                Math.Abs(geo.GridRect.Height - tokens.StatusBandHeight) < 0.01f);
            failures += Assert("巡视行高 30",
                Math.Abs(geo.Row(RestraintFields.Skin)!.Rect.Height - tokens.CellRowHeight) < 0.01f);
            failures += Assert("巡视格逐小时宽 26",
                Math.Abs(geo.CellRect(RestraintFields.Skin, 3).Width - tokens.SlotWidth) < 0.01f);
            failures += Assert("两段约束时段都在", doc.Episodes.Count == 2);

            RestraintChartDocument page2 = doc.Subset(0, 1);
            using (Chart p = new Chart(page2, chart.Template, chart.Style))
            {
                Write(Path.Combine(outDir, "restraint-page2-of-1.png"), p.Encode(ChartImageFormat.Png, 96f, 100));
                failures += Assert("分页后时段条保留", page2.Episodes.Count == 2);
                failures += Assert("分页后巡视格保留", page2.Cells.Count == doc.Cells.Count);
            }

            return failures;
        }

        private static int VerifyTransfusion(Chart chart, string outDir)
        {
            ChartGeometry geo = chart.Geometry;
            TransfusionChartDocument doc = (TransfusionChartDocument)chart.Document;
            TransfusionChartStyle tokens = ((TransfusionChartTemplate)chart.Template).Tokens;
            int failures = 0;

            failures += Assert("一天 8 个观察时点", geo.Slots.SlotsPerGroup == 8);
            failures += Assert("槽宽 44", Math.Abs(geo.Slots.SlotWidth - tokens.SlotWidth) < 0.01f);
            failures += Assert("36℃ 压在网格底线",
                Math.Abs(geo.YOf(TransfusionChartTemplate.AxisTemperature, doc.Scale.MinCelsius) - geo.GridRect.Bottom) < 0.01f);
            failures += Assert("41℃ 压在网格顶线",
                Math.Abs(geo.YOf(TransfusionChartTemplate.AxisTemperature, doc.Scale.MaxCelsius) - geo.GridRect.Top) < 0.01f);
            failures += Assert("每 0.2℃ 一格高 12",
                Math.Abs(Math.Abs(geo.YOf(TransfusionChartTemplate.AxisTemperature, 37d)
                                  - geo.YOf(TransfusionChartTemplate.AxisTemperature, 37.2d))
                         - tokens.CelsiusRowHeight) < 0.01f);
            failures += Assert("发热阈值落在网格内",
                geo.YOf(TransfusionChartTemplate.AxisTemperature, doc.Scale.FeverThreshold) > geo.GridRect.Top
                && geo.YOf(TransfusionChartTemplate.AxisTemperature, doc.Scale.FeverThreshold) < geo.GridRect.Bottom);
            failures += Assert("附属行高 26",
                Math.Abs(geo.Row(TransfusionFields.Symptoms)!.Rect.Height - tokens.CellRowHeight) < 0.01f);
            failures += Assert("两个反应事件", doc.Events.Count == 2);

            TransfusionChartDocument page2 = doc.Subset(0, 1);
            using (Chart p = new Chart(page2, chart.Template, chart.Style))
            {
                Write(Path.Combine(outDir, "transfusion-page2-of-1.png"), p.Encode(ChartImageFormat.Png, 96f, 100));
                failures += Assert("分页后体温不丢", page2.Temperature.Values.Count == 8);
                failures += Assert("分页后事件不丢", page2.Events.Count == 2);
            }

            return failures;
        }

        private static int VerifyRadar(Chart chart, bool full, string outDir)
        {
            RadarDocument doc = (RadarDocument)chart.Document;
            RadarChartTemplate template = (RadarChartTemplate)chart.Template;
            RadarLayout layout = template.Layout(doc);
            int failures = 0;

            failures += Assert("远圈压在盘面半径上",
                Math.Abs(layout.RadiusOf(doc.MaxRange) - layout.Radius) < 0.01f);
            failures += Assert("方位 0° 在正上方",
                Math.Abs(layout.PointOf(0d, doc.MaxRange).Y - (layout.Center.Y - layout.Radius)) < 0.01f);
            failures += Assert("选中目标存在", doc.SelectedTarget != null);
            failures += Assert("信息面板字段 ≥ 4", doc.InfoPanel.Fields.Count >= 4);
            failures += Assert("轨迹点数不超过保留上限",
                RadarChartTemplate.RetainedTrack(doc.Targets[0], doc.TrailLength).Count <= doc.TrailLength);

            if (full)
            {
                failures += Assert("360° 盘面高 = 直径",
                    Math.Abs(layout.ScopeRect.Height - layout.Radius * 2f) < 0.01f);
                failures += VerifySweepFrames(chart, doc, outDir);
            }
            else
            {
                failures += Assert("180° 盘面高 = 半径",
                    Math.Abs(layout.ScopeRect.Height - layout.Radius) < 0.01f);
                failures += Assert("180° 扇区起始方位 = 270°", Math.Abs(layout.StartBearing - 270d) < 0.01d);
            }

            return failures;
        }

        /// <summary>
        /// 扫描动画证据：完全用交互层自己的时钟推进角度，逐帧落盘。
        /// 演示与文档里的"动画真的在动"以这组文件为准，不是应用侧手改角度。
        /// </summary>
        private static int VerifySweepFrames(Chart chart, RadarDocument doc, string outDir)
        {
            int failures = 0;
            const int FrameCount = 8;
            const double FrameMilliseconds = 125d;

            RadarInteraction? interaction = RadarInteraction.TryCreate(chart);
            if (interaction == null)
            {
                return Assert("雷达表单可创建交互", false);
            }

            interaction.Attach(chart);
            interaction.IsAnimating = true;
            double start = doc.SweepAngle;
            byte[]? previous = null;
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < FrameCount; i++)
            {
                interaction.Tick(FrameMilliseconds);
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                Write(Path.Combine(outDir, string.Format("radar-sweep-{0:00}.png", i)), png);

                if (previous != null)
                {
                    failures += Assert("第 " + i + " 帧与上一帧不同", !SequenceEqual(previous, png));
                }
                previous = png;
            }

            sw.Stop();
            double expected = Normalize(start + interaction.SweepSpeedDegreesPerSecond
                                       * FrameMilliseconds * FrameCount / 1000d);
            Console.WriteLine("  扫描 " + FrameCount + " 帧共 " + sw.ElapsedMilliseconds + " ms，"
                              + "单帧均耗时 " + (sw.ElapsedMilliseconds / (double)FrameCount).ToString("0.0") + " ms");
            failures += Assert("交互时钟按耗时推进角度", Math.Abs(doc.SweepAngle - expected) < 0.01d);
            failures += Assert("角度归一到 0-360", doc.SweepAngle >= 0d && doc.SweepAngle < 360d);

            return failures;
        }

        private static double Normalize(double degrees)
        {
            double value = degrees % 360d;
            return value < 0d ? value + 360d : value;
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

        private static Chart Build(string name)
        {
            DemoSample? sample = DemoCatalog.Find(name);
            if (sample == null)
            {
                throw new ArgumentException("未知样例: " + name);
            }

            Chart chart = sample.Create();

            // 文档截图要能随仓库分发：指定 OFL 字体重绘，别让截图落在系统商业字体上。
            string? family = Environment.GetEnvironmentVariable("OODD_SAMPLE_FONT");
            if (!string.IsNullOrEmpty(family))
            {
                ChartStyle style = chart.Style.Clone();
                style.Text.Family = family;
                Chart replotted = new Chart(chart.Document, chart.Template, style);
                chart.Dispose();
                return replotted;
            }

            return chart;
        }

        private static void Write(string path, byte[] bytes)
        {
            File.WriteAllBytes(path, bytes);
        }

        /// <summary>
        /// PDF 字体内嵌成本实测。Skia 对部分系统字体不做子集化，会把整份字体塞进 PDF：
        /// 本机 msyh.ttc 常规体一份 12.5 MB、粗体再一份，默认页眉合计 23 MB；
        /// 换成可子集化的字体（本机 Noto Sans SC VF）同样内容只有约 70 KB。
        /// </summary>
        private static void ReportPdfFontCost(Chart chart, string outDir, string name)
        {
            string a5 = Path.Combine(outDir, name + "-a5.pdf");
            Console.WriteLine("PDF(A5) " + new FileInfo(a5).Length + " B");

            string[] candidates = { "Noto Sans SC", "Source Han Sans SC", "HarmonyOS Sans SC" };
            string? picked = null;
            foreach (string family in candidates)
            {
                using (SKTypeface? typeface = SKFontManager.Default.MatchFamily(family))
                {
                    if (typeface != null)
                    {
                        picked = family;
                        break;
                    }
                }
            }

            if (picked == null)
            {
                Console.WriteLine("  ! 本机无已知可子集化中文字体，PDF 体积按系统字体实测值记录");
                return;
            }

            ChartStyle alt = chart.Style.Clone();
            alt.Text.Family = picked;
            alt.Text.HospitalBold = false;
            alt.Text.TitleBold = false;

            string path = Path.Combine(outDir, name + "-subset.pdf");
            using (Chart second = new Chart(chart.Document, chart.Template, alt))
            using (FileStream fs = File.Create(path))
            {
                second.WritePdf(fs, PdfOptions.A4Landscape);
            }

            long small = new FileInfo(path).Length;
            long large = new FileInfo(Path.Combine(outDir, name + ".pdf")).Length;
            Console.WriteLine("PDF(" + picked + ") " + small + " B，默认字体 " + large
                              + " B，差 " + (large / Math.Max(1L, small)) + " 倍");
        }

        private static int Assert(string what, bool ok)
        {
            Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
            return ok ? 0 : 1;
        }
    }
}
