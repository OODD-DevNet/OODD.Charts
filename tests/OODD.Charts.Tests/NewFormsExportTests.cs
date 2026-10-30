using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OODD.Charts.Demo;
using OODD.Charts.Medical.Forms.DeathCertificate;
using OODD.Charts.Medical.Forms.VaccinationBooklet;
using OODD.Charts.Style;
using OODD.Charts.Tickets.Vat;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T293：新域 17 张表的四路导出（PNG/JPEG/SVG/PDF）同源可辨识，SVG 文本可检索
    /// （含风杆、章环这类旋转几何旁边的直立文字），多联/开面的 PDF 页数按 D17
    /// 只随物理页与开面数增长，不随联数增长。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class NewFormsExportTests
    {
        public static IEnumerable<object[]> NewSampleNames()
        {
            foreach (DemoSample sample in EngineeringSamples.All
                .Concat(TransitSamples.All)
                .Concat(GeoscienceSamples.All)
                .Concat(NewMedicalSamples.All)
                .Concat(TicketSamples.All))
            {
                yield return new object[] { sample.Name };
            }
        }

        [Theory]
        [MemberData(nameof(NewSampleNames))]
        public void 新域表四路导出都可辨识且SVG文字可检索(string name)
        {
            DemoSample? sample = DemoCatalog.Find(name);
            Assert.True(sample != null, "样例目录里找不到 " + name);
            using (Chart chart = sample!.Create())
            {
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                byte[] jpeg = chart.Encode(ChartImageFormat.Jpeg, 96f, 90);
                Assert.True(png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50
                            && png[2] == 0x4E && png[3] == 0x47, name + " PNG 魔数不对");
                Assert.True(jpeg.Length > 1000 && jpeg[0] == 0xFF && jpeg[1] == 0xD8
                            && jpeg[2] == 0xFF, name + " JPEG 魔数不对");

                string svg = ChartExport.ToSvgString(chart);
                Assert.True(svg.Length > 2000, name + " SVG 过小：" + svg.Length);
                Assert.True(svg.Contains("<path"), name + " SVG 没有 path，线条退化成位图");
                Assert.True(svg.Contains("<text"), name + " SVG 没有 text，文字被路径化");
                List<string> nodes = TextContents(svg);
                Assert.True(!nodes.Any(c => c.Length == 0), name + " SVG 出现空 text 节点（文字丢失）");

                byte[] pdf = PdfOf(chart);
                Assert.True(Ascii(pdf, 0, 5) == "%PDF-", name + " PDF 头不对");
                Assert.True(Contains(pdf, "%EOF"), name + " PDF 没有收尾 %EOF");
                Assert.True(Count(pdf, "/Contents") == 1,
                    name + " 单画布应导出 1 条内容流，实测 " + Count(pdf, "/Contents"));
                Assert.True(Count(pdf, "/MediaBox") == 1,
                    name + " 单画布应导出 1 个 MediaBox，实测 " + Count(pdf, "/MediaBox"));
                Assert.True(Count(pdf, "/Type0") + Count(pdf, "/FontFile") > 0,
                    name + " PDF 没有矢量字体对象");
            }
        }

        [Fact]
        public void 旋转符号旁的直立文字仍可作为文本检索()
        {
            // 风杆是旋转几何；站名必须仍是 <text>，不能被画进 path。
            using (Chart chart = RequireSample("surface-chart"))
            {
                string svg = ChartExport.ToSvgString(chart);
                Assert.True(CountText(svg, "北京 54511") > 0,
                    "地面天气图 SVG 里找不到站名「北京 54511」（风杆旋转不应把站名路径化）");
                Assert.True(CountText(svg, "地面天气图") > 0, "地面天气图标题不可检索");
            }

            // 章环整体旋转，章内「已执行」按 D20 逐字直立落笔；三联都要能检索到。
            using (Chart chart = RequireSample("electrical-work-ticket"))
            {
                string svg = ChartExport.ToSvgString(chart);
                Assert.True(CountText(svg, "电气第一种工作票") > 0, "工作票标题不可检索");
                int stamps = CountText(svg, "已执行");
                Assert.True(stamps >= 1, "章环内的「已执行」应作为 <text> 出现，实测 " + stamps + " 处");
            }
        }

        [Fact]
        public void 多联PDF页数按D17只随物理页增长不随联数增长()
        {
            using (Chart chart = RequireSample("death-certificate"))
            {
                DeathCertificateLayout layout = chart.Geometry.LayoutOf<DeathCertificateLayout>()!;
                Assert.True(layout.CopyCount == 4, "死亡证明应为 4 联，实到 " + layout.CopyCount);
                Assert.True(layout.Spec.Direction == CollateDirection.HorizontalStrip,
                    "死亡证明四联应横向条排，实到 " + layout.Spec.Direction);

                byte[] pdf = PdfOf(chart);
                Assert.True(Count(pdf, "/Contents") == 1,
                    "4 联仍是一张整页：PDF 内容流应 1 条，实测 " + Count(pdf, "/Contents"));
            }

            using (Chart chart = RequireSample("vat-invoice"))
            {
                VatInvoiceLayout layout = chart.Geometry.LayoutOf<VatInvoiceLayout>()!;
                Assert.True(layout.Collate.Copies.Count >= 3,
                    "增值税发票至少 3 联，实到 " + layout.Collate.Copies.Count);
                Assert.True(layout.Collate.Direction == CollateDirection.VerticalStack,
                    "发票联次应纵向堆叠，实到 " + layout.Collate.Direction);

                byte[] pdf = PdfOf(chart);
                Assert.True(Count(pdf, "/Contents") == 1,
                    "纵向堆叠 N 联仍是一张整页：PDF 内容流应 1 条，实测 " + Count(pdf, "/Contents"));

                string svg = ChartExport.ToSvgString(chart);
                Assert.True(CountText(svg, "增值税") > 0, "发票标题不可检索");
            }
        }

        [Fact]
        public void 接种证PDF页数等于开面数而不是逻辑页数()
        {
            BookletImposition imposition = BookletImposition.SaddleStitch(12);
            Assert.True(imposition.Spreads.Count == 6,
                "12 个逻辑页按鞍装配对应 6 个开面，实到 " + imposition.Spreads.Count);

            List<Chart> pages = new List<Chart>();
            try
            {
                foreach (Spread spread in imposition.Spreads)
                {
                    VaccinationBookletDocument doc = new VaccinationBookletDocument
                    {
                        SpreadIndex = spread.SpreadIndex,
                    };
                    doc.Records.Add(new VaccinationRecord("乙肝疫苗", "1", "2026-03-04", "示例生物", "20260301"));
                    pages.Add(new Chart(doc, new VaccinationBookletTemplate(), new ChartStyle()));
                }

                byte[] pdf;
                using (Stream stream = new MemoryStream())
                {
                    ChartExport.WritePdf(pages, stream);
                    pdf = ((MemoryStream)stream).ToArray();
                }

                Assert.True(Count(pdf, "/Contents") == imposition.Spreads.Count,
                    "开面 PDF 页数应等于开面数 " + imposition.Spreads.Count
                    + "，实测 " + Count(pdf, "/Contents"));
                Assert.True(Count(pdf, "/MediaBox") == imposition.Spreads.Count,
                    "每个开面应有一个 MediaBox，实测 " + Count(pdf, "/MediaBox"));
                Assert.True(Count(pdf, "/Contents") != imposition.TotalLogicalPages,
                    "PDF 页数不应等于逻辑页数 " + imposition.TotalLogicalPages);
            }
            finally
            {
                foreach (Chart page in pages)
                {
                    page.Dispose();
                }
            }
        }

        private static Chart RequireSample(string name)
        {
            DemoSample? sample = DemoCatalog.Find(name);
            Assert.True(sample != null, "样例目录里找不到 " + name);
            return sample!.Create();
        }

        private static byte[] PdfOf(Chart chart)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                chart.WritePdf(stream);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// SKSvgCanvas 把 <c>&lt;text&gt;</c> 的内容写在开标签的下一行并缩进，单行正则匹配不到，
        /// 必须按整个节点取内容再 trim。
        /// </summary>
        private static List<string> TextContents(string svg)
        {
            List<string> contents = new List<string>();
            foreach (Match match in Regex.Matches(svg, "<text[^>]*>(.*?)</text>", RegexOptions.Singleline))
            {
                contents.Add(match.Groups[1].Value.Trim());
            }
            return contents;
        }

        private static int CountText(string svg, string text)
        {
            return TextContents(svg).Count(content => content.Contains(text));
        }

        private static string Ascii(byte[] bytes, int index, int count)
        {
            return Encoding.ASCII.GetString(bytes, index, count);
        }

        private static bool Contains(byte[] haystack, string needle)
        {
            return Count(haystack, needle) > 0;
        }

        private static int Count(byte[] haystack, string needle)
        {
            byte[] pattern = Encoding.ASCII.GetBytes(needle);
            int hits = 0;
            for (int i = 0; i + pattern.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (haystack[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match)
                {
                    hits++;
                    i += pattern.Length - 1;
                }
            }
            return hits;
        }
    }
}
