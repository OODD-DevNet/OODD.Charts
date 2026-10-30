using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OODD.Charts.Medical.Temperature;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A8：四种导出产物都可辨识，且 PDF/SVG 是真矢量（不是把位图塞进容器）。
    /// 判据只看字节头与容器内的对象名，不依赖任何 PDF/SVG 解析库。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class ExportTests
    {
        [Fact]
        public void 三种位图魔数各自正确()
        {
            using (Chart chart = TestDocuments.Temperature())
            {
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                byte[] jpeg = chart.Encode(ChartImageFormat.Jpeg, 96f, 90);
                byte[] webp = chart.Encode(ChartImageFormat.Webp, 96f, 80);

                Assert.True(png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50
                            && png[2] == 0x4E && png[3] == 0x47, "PNG 魔数不对");
                Assert.True(jpeg.Length > 1000 && jpeg[0] == 0xFF && jpeg[1] == 0xD8
                            && jpeg[2] == 0xFF, "JPEG 魔数不对");
                Assert.True(webp.Length > 1000 && Ascii(webp, 0, 4) == "RIFF"
                            && Ascii(webp, 8, 4) == "WEBP", "WebP 魔数不对");
            }
        }

        [Fact]
        public void PDF是矢量且带内嵌字体()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (MemoryStream stream = new MemoryStream())
            {
                chart.WritePdf(stream);
                byte[] pdf = stream.ToArray();

                Assert.True(pdf.Length > 2000, "PDF 过小：" + pdf.Length);
                Assert.True(Ascii(pdf, 0, 5) == "%PDF-", "PDF 头不对");
                Assert.True(Contains(pdf, "%EOF"), "PDF 没有收尾的 %EOF");
                Assert.True(Contains(pdf, "/Contents"), "PDF 缺页面内容流");
                Assert.True(Contains(pdf, "/MediaBox"), "PDF 缺页面尺寸");

                // 文字以 Type0 组合字体嵌入 → 是矢量字形；位图路线不会产生字体对象。
                Assert.True(Contains(pdf, "/Type0") || Contains(pdf, "/FontFile"),
                    "PDF 里没有矢量字体对象，说明内容是位图");

                // 反过来禁止位图页：Skia 的 PDF 里位图会写成 /XObject + /Subtype/Image。
                Assert.False(Contains(pdf, "/Subtype /Image") || Contains(pdf, "/Subtype/Image"),
                    "PDF 内嵌了位图对象，不是纯矢量");
                Assert.False(Contains(pdf, "/XObject"), "PDF 含 XObject，疑似位图封装");
            }
        }

        [Fact]
        public void SVG含path与text且不含image()
        {
            using (Chart chart = TestDocuments.Temperature())
            {
                string svg = ChartExport.ToSvgString(chart);

                Assert.True(svg.Length > 2000, "SVG 过小：" + svg.Length);
                Assert.True(svg.StartsWith("<?xml") || svg.StartsWith("<svg"), "SVG 开头不对：" + Head(svg));
                Assert.True(svg.Contains("<path"), "SVG 没有 path，线条不是矢量");
                Assert.True(svg.Contains("<text"), "SVG 没有 text，文字被转成了曲线或位图");
                Assert.False(svg.Contains("<image"), "SVG 内嵌了位图");
            }
        }

        [Fact]
        public void 多页PDF每页都有内容流()
        {
            using (Chart chart = TestDocuments.Temperature())
            {
                TemperatureChartDocument doc = (TemperatureChartDocument)chart.Document;
                List<Chart> pages = new List<Chart>();
                foreach (GroupRange range in ChartPager.SplitGroups(doc.Days.Count, 2))
                {
                    TemperatureChartDocument subset = doc.Subset(range.Start, range.Count);
                    pages.Add(new Chart(subset, chart.Template, chart.Style));
                }
                Assert.True(pages.Count == 2, "3 天按每页 2 组应切 2 页，实得 " + pages.Count);

                byte[] pdf;
                using (MemoryStream stream = new MemoryStream())
                {
                    ChartExport.WritePdf(pages, stream);
                    pdf = stream.ToArray();
                }
                foreach (Chart page in pages)
                {
                    page.Dispose();
                }

                Assert.True(Ascii(pdf, 0, 5) == "%PDF-", "多页 PDF 头不对");
                Assert.True(Count(pdf, "/Contents") == 2,
                    "2 页应有 2 条内容流，实测 " + Count(pdf, "/Contents"));
                Assert.True(Count(pdf, "/MediaBox") == 2,
                    "2 页应有 2 个 MediaBox，实测 " + Count(pdf, "/MediaBox"));
            }
        }

        [Fact]
        public void A5分页切分边界()
        {
            Assert.True(ChartPager.SplitGroups(0, 3).Count == 0, "0 组不该出页");
            Assert.True(ChartPager.SplitGroups(3, 0).Count == 1, "不分页时应单页");
            Assert.True(ChartPager.SplitGroups(3, 5).Count == 1, "每页大于总数时应单页");

            List<GroupRange> pages = ChartPager.SplitGroups(7, 3);
            Assert.True(pages.Count == 3, "7 组每页 3 应切 3 页，实得 " + pages.Count);
            Assert.True(pages[0].Start == 0 && pages[0].Count == 3, "首页 [0,3)");
            Assert.True(pages[1].Start == 3 && pages[1].Count == 3, "次页 [3,6)");
            Assert.True(pages[2].Start == 6 && pages[2].Count == 1, "末页 [6,7)");
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

        private static string Head(string text)
        {
            return text.Substring(0, Math.Min(40, text.Length));
        }
    }
}
