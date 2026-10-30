using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Style;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A9 / FR-3：新增一张表单不得修改引擎。
    /// <para>
    /// 判据不能靠"看 diff"（本交付物不是 git 仓库），这里换成三条可执行的证据：
    /// ① 一张引擎从没见过的外部表单（<see cref="PainScoreChartTemplate"/>，就在本测试工程里）
    /// 　 只实现 <see cref="OODD.Charts.Templates.IChartTemplate"/> 就能出图，像素级逐项对得上；
    /// ② Core 的源码里扫不到任何表单专有标识符，程序集里也没有表单专有类型；
    /// ③ Core 不引用 Medical，即依赖方向单一，模板侧无法反向污染引擎。
    /// </para>
    /// </summary>
    public sealed class TemplateExtensionTests
    {
        /// <summary>表单专有名词。出现任何一个都说明引擎被某张表单"记名"了。</summary>
        private static readonly string[] FormSpecificWords =
        {
            "Partogram",
            "Temperature",
            "Cervix",
            "Fetal",
            "Contraction",
            "Membrane",
            "Axillary",
        };

        private const double LineCoverage = 0.5;

        // ------------------------------------------------------------------ ① 出图

        [Fact]
        public void 外部模板只实现接口就能渲染()
        {
            using (Chart chart = new Chart(Document(), new PainScoreChartTemplate(), new ChartStyle()))
            {
                ChartGeometry geo = chart.Geometry;
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                WriteArtifact("a9-external-template-96dpi.png", png);

                using (ImageProbe probe = ImageProbe.FromPng(png))
                {
                    Assert.True(probe.Width >= (int)geo.Bounds.Right && probe.Height >= (int)geo.Bounds.Bottom,
                        "位图比版面小：" + probe.Width + "x" + probe.Height + " vs " + geo.Bounds.Width + "x" + geo.Bounds.Height);

                    // 评分轴 0..10 分、每分 20px —— 由模板自带 tokens 决定，引擎里没有这两个数。
                    IReadOnlyList<double> rows = probe.StrongHorizontalLines(
                        geo.GridRect.Top, geo.GridRect.Bottom,
                        geo.GridRect.Left + 2, geo.GridRect.Right - 2,
                        LineCoverage);
                    Assert.True(rows.Count == 11, "横线应 11 条（0–10 分），实测 " + rows.Count);
                    foreach (double gap in ImageProbe.Gaps(rows))
                    {
                        Assert.True(Math.Abs(gap - 20d) <= 1d, "1 分应为 20px，实测 " + gap.ToString("0.#"));
                    }

                    IReadOnlyList<double> columns = probe.StrongVerticalLines(
                        geo.GridRect.Left, geo.GridRect.Right,
                        geo.GridRect.Top + 2, geo.GridRect.Bottom - 2,
                        LineCoverage);
                    Assert.True(columns.Count >= 10, "9 个时点至少 10 条竖线，实测 " + columns.Count);
                    foreach (double gap in ImageProbe.Gaps(columns))
                    {
                        Assert.True(Math.Abs(gap - 24d) <= 1d || Math.Abs(gap - 48d) <= 1d,
                            "槽宽 24px（跨槽合并 48px），实测 " + gap.ToString("0.#"));
                    }

                    // 页眉三行、刻度列、附属行、图例：外部模板用到的每一类通用令牌都落了墨。
                    AssertInk(probe, geo.Header.RowRect("hospital"), "医院名");
                    AssertInk(probe, geo.Header.RowRect("title"), "表单标题");
                    AssertInk(probe, geo.Header.RowRect("patient"), "患者信息");
                    AssertInk(probe, geo.DateBand.RowRect("date"), "日期行");
                    AssertInk(probe, geo.DateBand.RowRect("time"), "时点行");
                    AssertInk(probe, ScaleCell(geo, 5d), "刻度 5");
                    AssertInk(probe, ScaleCell(geo, 10d), "刻度 10");
                    AssertInk(probe, geo.CellRect(PainScoreChartTemplate.RowNote, 1), "备注取值");
                    AssertInk(probe, geo.CellRect(PainScoreChartTemplate.RowAssessor, 4), "评估者取值");
                    AssertInk(probe, geo.Footer.Rect, "图例");

                    // 数据点：颜色取文档自己的系列色，说明配色也由模板/文档携带。
                    SKPoint point = new SKPoint(geo.SlotCenterX(4), geo.YOf(PainScoreChartTemplate.AxisNrs, 7d));
                    SKColor ink = probe.AverageInkColor(new SKRect(point.X - 6f, point.Y - 6f, point.X + 6f, point.Y + 6f));
                    Assert.True(ink.Blue > 120 && ink.Blue > ink.Red + 60 && ink.Blue > ink.Green + 60,
                        "评分点应为文档指定的蓝色，实测 RGB=" + ink.Red + "," + ink.Green + "," + ink.Blue);

                    // 槽 6 缺测：模板按 PointSeries.BreakRule 自己断线，缺测列上不该有蓝色穿过。
                    float upperY = geo.YOf(PainScoreChartTemplate.AxisNrs, 2d);
                    float lowerY = geo.YOf(PainScoreChartTemplate.AxisNrs, 1d);
                    int blue = CountBlue(probe,
                        (int)geo.SlotCenterX(6) - 2,
                        (int)upperY - 2,
                        (int)geo.SlotCenterX(6) + 3,
                        (int)lowerY + 3);
                    Assert.True(blue == 0, "缺测槽被折线穿过，断线规则没生效，蓝色像素 " + blue + " 个");
                }
            }
        }

        [Fact]
        public void 外部模板的版面求解无副作用且DPI只放大位图()
        {
            PainScoreDocument doc = Document();
            PainScoreChartTemplate template = new PainScoreChartTemplate();
            ChartStyle style = new ChartStyle();

            ChartGeometry first = template.Measure(doc, style, Chart.SharedFonts);
            ChartGeometry second = template.Measure(doc, style, Chart.SharedFonts);
            Assert.True(SameRect(first.Bounds, second.Bounds) && SameRect(first.GridRect, second.GridRect),
                "Measure 被调用两次给出不同版面：" + first.Bounds + " vs " + second.Bounds);
            Assert.True(Math.Abs(first.YOf(PainScoreChartTemplate.AxisNrs, 7d) -
                                 second.YOf(PainScoreChartTemplate.AxisNrs, 7d)) < 0.001f,
                "同一数据的落点不稳定");

            using (Chart chart = new Chart(doc, template, style))
            {
                SKSize logical = chart.MeasureSize();
                using (SKImage at96 = chart.RenderImage(96f))
                using (SKImage at192 = chart.RenderImage(192f))
                {
                    Assert.True(at192.Width == at96.Width * 2 && at192.Height == at96.Height * 2,
                        "192dpi 应是 96dpi 的两倍：" + at96.Width + "x" + at96.Height + " → " + at192.Width + "x" + at192.Height);
                    Assert.True(Math.Abs(logical.Width - chart.Geometry.Bounds.Width) < 0.01f,
                        "逻辑宽与几何宽不一致");
                }

                using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 192f, 100)))
                {
                    ChartGeometry geo = chart.Geometry;
                    IReadOnlyList<double> rows = probe.StrongHorizontalLines(
                        geo.GridRect.Top * 2f, geo.GridRect.Bottom * 2f,
                        geo.GridRect.Left * 2f + 4f, geo.GridRect.Right * 2f - 4f,
                        LineCoverage);
                    Assert.True(rows.Count == 11, "192dpi 下横线仍应 11 条，实测 " + rows.Count);
                    Assert.True(Math.Abs(rows[0] - geo.GridRect.Top * 2f) <= 1.5f
                                && Math.Abs(rows[rows.Count - 1] - geo.GridRect.Bottom * 2f) <= 1.5f,
                        "192dpi 的网格首末线没有落在 96dpi 版面的 2 倍处：" + rows[0] + " / " + rows[rows.Count - 1]);
                    foreach (double gap in ImageProbe.Gaps(rows))
                    {
                        // 2× 光栅下曲线与格线叠墨会把一行的墨迹摊到相邻行，行中心允许 ±2px 抖动；
                        // 版面本身不重排由上面的首末线绝对位置钉住。
                        Assert.True(Math.Abs(gap - 40d) <= 2d,
                            "192dpi 的 1 分应是 40px（版面只放大不重排），实测 " + gap.ToString("0.#"));
                    }
                }
            }
        }

        [Fact]
        public void 零适配路径把外部模板画进任意矩形()
        {
            using (Chart chart = new Chart(Document(), new PainScoreChartTemplate(), new ChartStyle()))
            {
                SKRect target = new SKRect(60f, 40f, 480f, 420f);
                SKImageInfo info = new SKImageInfo(700, 500, SKColorType.Bgra8888, SKAlphaType.Premul);
                using (SKSurface? surface = SKSurface.Create(info))
                {
                    if (surface == null)
                    {
                        throw new InvalidOperationException("无法创建 700x500 渲染面");
                    }
                    surface.Canvas.Clear(SKColors.White);
                    chart.Draw(surface.Canvas, target);

                    using (SKImage image = surface.Snapshot())
                    using (SKData? data = image.Encode(SKEncodedImageFormat.Png, 100))
                    {
                        if (data == null)
                        {
                            throw new InvalidOperationException("PNG 编码失败");
                        }
                        using (ImageProbe probe = ImageProbe.FromPng(data.ToArray()))
                        {
                            Assert.True(probe.HasInkInBox((int)target.Left, (int)target.Top, (int)target.Right, (int)target.Bottom),
                                "目标矩形内没有墨迹，等于什么都没画");
                            Assert.False(probe.HasInkInBox(0, 0, (int)target.Left - 1, 500), "溢出到了目标矩形左侧");
                            Assert.False(probe.HasInkInBox((int)target.Right + 2, 0, 700, 500), "溢出到了目标矩形右侧");
                            Assert.False(probe.HasInkInBox(0, 0, 700, (int)target.Top - 1), "溢出到了目标矩形上方");
                            Assert.False(probe.HasInkInBox(0, (int)target.Bottom + 2, 700, 500), "溢出到了目标矩形下方");
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------ ② 引擎不知情

        [Fact]
        public void 引擎源码里没有任何表单专有标识符()
        {
            List<string> hits = new List<string>();
            foreach (string file in SourceScan.FilesUnder(Path.Combine("src", "OODD.Charts.Core")))
            {
                foreach (string hit in SourceScan.Hits(SourceScan.CodeOf(file), FormSpecificWords))
                {
                    hits.Add(file + " → " + hit);
                }
            }
            Assert.True(hits.Count == 0, "Core 记住了某张表单的名字：" + string.Join("; ", hits.ToArray()));
        }

        [Fact]
        public void 引擎程序集里没有表单专有类型()
        {
            Assembly core = typeof(Chart).Assembly;
            List<string> offenders = new List<string>();
            foreach (Type type in core.GetTypes())
            {
                string name = type.Name;
                if (name[0] == '<')
                {
                    continue;
                }
                foreach (string word in FormSpecificWords)
                {
                    if (name.IndexOf(word, StringComparison.Ordinal) >= 0)
                    {
                        offenders.Add(type.FullName ?? type.Name);
                    }
                }
            }
            Assert.True(offenders.Count == 0, "Core 导出类型带表单名：" + string.Join(", ", offenders.ToArray()));
        }

        [Fact]
        public void 样式令牌表只放跨表单通用项()
        {
            // 产程图重构前这里有一条 ChartStyle.Partogram：专属尺寸挂在引擎上，
            // 新增表单就得改 ChartStyle —— 那才是真正违反 A9 的地方。
            foreach (PropertyInfo property in typeof(ChartStyle).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.True(property.PropertyType.Assembly == typeof(ChartStyle).Assembly,
                    "ChartStyle." + property.Name + " 的类型来自外部程序集：" + property.PropertyType.FullName);
            }
            foreach (string word in FormSpecificWords)
            {
                Assert.True(typeof(ChartStyle).GetProperty(word, BindingFlags.Public | BindingFlags.Instance) == null,
                    "ChartStyle 上出现了表单专属令牌：" + word);
            }
        }

        [Fact]
        public void 引擎不依赖任何模板库()
        {
            AssemblyName[] references = typeof(Chart).Assembly.GetReferencedAssemblies();
            List<string> names = new List<string>();
            foreach (AssemblyName reference in references)
            {
                names.Add(reference.Name ?? string.Empty);
            }
            Assert.True(names.Count > 0, "取到空引用表，判据没生效");
            Assert.False(names.Contains("OODD.Charts.Medical"),
                "Core 引用了模板库，依赖方向反了：" + string.Join(", ", names.ToArray()));
        }

        [Fact]
        public void 判据本身能抓到表单专有标识符()
        {
            // 反向自检：把违禁词放进一段假源码，扫描必须抓到；
            // 只在注释里提则不算。否则上面两条正判据可能只是"永远绿"。
            string violating = "public class ChartStyle { public int FetalHeartRowHeight = 26; }";
            Assert.True(SourceScan.Hits(SourceScan.CodeOnly(violating), FormSpecificWords).Count == 1,
                "真代码里的表单专有字段没被抓到");

            string commentOnly = "// 产程图里的 FetalHeartRowHeight 已迁到模板 tokens";
            Assert.True(SourceScan.Hits(SourceScan.CodeOnly(commentOnly), FormSpecificWords).Count == 0,
                "注释里的提及被误判");

            Assert.True(MedicalTemplateStillHasThem(),
                "Medical 里的产程图类型不见了——说明判据扫的目录本身没内容");
        }

        // ------------------------------------------------------------------ 辅助

        /// <summary>同样的词表在 Medical 必须能扫到，证明词表本身有效。</summary>
        private static bool MedicalTemplateStillHasThem()
        {
            foreach (string file in SourceScan.FilesUnder(Path.Combine("src", "OODD.Charts.Medical")))
            {
                if (SourceScan.Hits(SourceScan.CodeOf(file), FormSpecificWords).Count > 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static SKRect ScaleCell(ChartGeometry geo, double value)
        {
            LabelColumn column = geo.LabelColumns[0];
            float y = geo.YOf(PainScoreChartTemplate.AxisNrs, value);
            return new SKRect(column.Rect.Left + 1f, y - 8f, column.Rect.Right - 1f, y + 8f);
        }

        private static void AssertInk(ImageProbe probe, SKRect rect, string what)
        {
            Assert.True(probe.HasInkInBox((int)rect.Left, (int)rect.Top, (int)rect.Right, (int)rect.Bottom),
                what + " 所在位置无墨迹：" + rect);
        }

        private static bool SameRect(SKRect a, SKRect b)
        {
            return Math.Abs(a.Left - b.Left) < 0.01f && Math.Abs(a.Top - b.Top) < 0.01f
                && Math.Abs(a.Right - b.Right) < 0.01f && Math.Abs(a.Bottom - b.Bottom) < 0.01f;
        }

        /// <summary>蓝色墨迹像素数，用于确认断线处真的没有线。</summary>
        private static int CountBlue(ImageProbe probe, int left, int top, int right, int bottom)
        {
            int count = 0;
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    SKColor pixel = probe.Pixel(x, y);
                    if (pixel.Blue > 120 && pixel.Blue > pixel.Red + 60 && pixel.Blue > pixel.Green + 60)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static void WriteArtifact(string name, byte[] png)
        {
            string dir = Path.Combine(TestDocuments.RepoRoot, "artifacts");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name), png);
        }

        private static PainScoreDocument Document()
        {
            PainScoreDocument doc = new PainScoreDocument
            {
                HospitalName = "验收医院",
                FormTitle = "疼痛评分记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };
            doc.Patient.Name = "赵测试";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "61";
            doc.Patient.Department = "普外科";
            doc.Patient.BedNumber = "17床";

            DateTime start = new DateTime(2026, 4, 10);
            for (int day = 0; day < 3; day++)
            {
                doc.Days.Add(new ChartGroup(start.AddDays(day)));
            }

            // 3 天 × 3 时点 = 9 槽；刻意留一个缺测（槽 6）验证折线断开规则。
            double[] scores = { 6d, 4d, 5d, 3d, 7d, 2d, double.NaN, 1d, 2d };
            for (int slot = 0; slot < scores.Length; slot++)
            {
                if (!double.IsNaN(scores[slot]))
                {
                    doc.Scores.Values[slot] = scores[slot];
                }
            }

            doc.Cells.Add(new CellValue(PainScoreChartTemplate.RowNote, 1, "夜间痛醒"));
            doc.Cells.Add(new CellValue(PainScoreChartTemplate.RowAssessor, 0, "护士甲"));
            doc.Legend.Add(new LegendItem(MarkerSymbol.DotRing, SKColors.Blue, "NRS 评分"));
            return doc;
        }
    }
}
