using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OODD.Charts.Demo;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Catalog;
using OODD.Charts.Medical.Pain;
using OODD.Charts.Medical.Restraint;
using OODD.Charts.Medical.Transfusion;
using OODD.Charts.Model;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A10：三张"另类"记录单（疼痛评估、保护性约束巡视、输血反应）的版面与服务端产物。
    /// 判据与三测单一致：几何间距可解析验证，关键语义（阈值线、约束条、事件线）必须在像素上可见。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class SpecialtyFormsTests
    {
        // ------------------------------------------------------------ 疼痛评估

        [Fact]
        public void 疼痛单_纵轴1分22px且阈值线在7分()
        {
            using (Chart chart = TestDocuments.Pain())
            {
                ChartGeometry geo = chart.Geometry;
                PainChartStyle tokens = ((PainChartTemplate)chart.Template).Tokens;

                Assert.Equal(6, geo.Slots.SlotsPerGroup);
                Assert.Equal(2, geo.Slots.GroupCount);
                Assert.Equal(tokens.SlotWidth, geo.Slots.SlotWidth, 3);
                Assert.Equal(geo.GridRect.Bottom, geo.YOf(PainChartTemplate.AxisNrs, 0d), 3);
                Assert.Equal(geo.GridRect.Top, geo.YOf(PainChartTemplate.AxisNrs, 10d), 3);

                double perPoint = Math.Abs(geo.YOf(PainChartTemplate.AxisNrs, 4d)
                                           - geo.YOf(PainChartTemplate.AxisNrs, 5d));
                Assert.Equal(tokens.ScoreRowHeight, perPoint, 3);

                double severeY = geo.YOf(PainChartTemplate.AxisNrs, 7d);
                Assert.True(severeY > geo.GridRect.Top && severeY < geo.GridRect.Bottom);
                Assert.Equal(tokens.CellRowHeight, geo.Row(PainFields.Site)!.Rect.Height, 3);
                Assert.Equal(tokens.SlotWidth, geo.CellRect(PainFields.Site, 3).Width, 3);
            }
        }

        [Fact]
        public void 疼痛单_严重阈值线与重度点圈注为红而轻中度点不圈()
        {
            using (Chart chart = TestDocuments.Pain())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                double y = geo.YOf(PainChartTemplate.AxisNrs, 7d);

                Assert.True(probe.HasRedInBox((int)geo.GridLeft + 2, (int)y - 1,
                        (int)geo.GridRight - 2, (int)y + 2),
                    "7 分处没有红色虚线阈值");

                int severeX = (int)Math.Round(geo.SlotCenterX(5));
                int severeY = (int)Math.Round(geo.YOf(PainChartTemplate.AxisNrs, 8d));
                Assert.True(probe.HasRedInBox(severeX - 8, severeY - 4, severeX - 5, severeY + 4),
                    "8 分点外侧没有红色圈注");

                int mildX = (int)Math.Round(geo.SlotCenterX(0));
                int mildY = (int)Math.Round(geo.YOf(PainChartTemplate.AxisNrs, 3d));
                Assert.True(probe.HasInkInBox(mildX - 5, mildY - 5, mildX + 6, mildY + 6),
                    "3 分点本身没有墨，'没有红圈'成了空判");
                Assert.False(probe.HasRedInBox(mildX - 8, mildY - 4, mildX - 5, mildY + 4),
                    "3 分点被误圈成重度");
            }
        }

        [Fact]
        public void 疼痛单_分页后评分按新槽号重算()
        {
            PainChartDocument doc = TestDocuments.PainDocument();
            PainChartDocument page2 = doc.Subset(1, 1);

            Assert.Single(page2.Days);
            Assert.Equal(2d, page2.Series[0].Values[0], 3);
            Assert.Equal("切口", FindCell(page2.Cells, PainFields.Site, 0)?.Text);
        }

        // ------------------------------------------------------ 保护性约束巡视

        [Fact]
        public void 约束单_一天24槽且约束条画出正常段与紧急段()
        {
            using (Chart chart = TestDocuments.Restraint())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                RestraintChartStyle tokens = ((RestraintChartTemplate)chart.Template).Tokens;

                Assert.Equal(24, geo.Slots.SlotsPerGroup);
                Assert.Equal(2, geo.Slots.GroupCount);
                Assert.Equal(tokens.StatusBandHeight, geo.GridRect.Height, 3);

                int normalX = (int)Math.Round(geo.SlotCenterX(3));
                int midY = (int)Math.Round(geo.GridRect.MidY);
                Assert.True(probe.HasInkInBox(normalX - 4, midY - 6, normalX + 5, midY + 7),
                    "正常约束段没有画出时段条");
                Assert.False(probe.HasRedInBox(normalX - 4, midY - 6, normalX + 5, midY + 7),
                    "正常约束段被画成红色");

                int emergencyX = (int)Math.Round(geo.SlotCenterX(22));
                SKColor fill = probe.Pixel(emergencyX - 15, midY);
                Assert.True(fill.Red > fill.Green + 15 && fill.Red > fill.Blue + 15,
                    "紧急约束段没有用强调色标出：" + fill.Red + "," + fill.Green + "," + fill.Blue);
            }
        }

        [Fact]
        public void 约束单_跨页约束段按页裁切()
        {
            RestraintChartDocument doc = TestDocuments.RestraintDocument();
            RestraintChartDocument page2 = doc.Subset(1, 1);

            Assert.Single(page2.Episodes);
            Assert.Equal(0, page2.Episodes[0].StartSlot);
            Assert.Equal(4, page2.Episodes[0].EndSlot);
            Assert.True(page2.Episodes[0].IsEmergency, "紧急标记在裁切后丢了");
            Assert.Equal("完好", FindCell(page2.Cells, RestraintFields.Skin, 1)?.Text);
        }

        // ------------------------------------------------------------ 输血反应

        [Fact]
        public void 输血单_体温轴36到41且每0点2度12px()
        {
            using (Chart chart = TestDocuments.Transfusion())
            {
                ChartGeometry geo = chart.Geometry;
                TransfusionChartDocument doc = (TransfusionChartDocument)chart.Document;
                TransfusionChartStyle tokens = ((TransfusionChartTemplate)chart.Template).Tokens;

                Assert.Equal(8, geo.Slots.SlotsPerGroup);
                Assert.Equal(2, geo.Slots.GroupCount);
                Assert.Equal(tokens.SlotWidth, geo.Slots.SlotWidth, 3);
                Assert.Equal(geo.GridRect.Bottom, geo.YOf(TransfusionChartTemplate.AxisTemperature,
                    doc.Scale.MinCelsius), 3);
                Assert.Equal(geo.GridRect.Top, geo.YOf(TransfusionChartTemplate.AxisTemperature,
                    doc.Scale.MaxCelsius), 3);

                double row = Math.Abs(geo.YOf(TransfusionChartTemplate.AxisTemperature, 37d)
                                      - geo.YOf(TransfusionChartTemplate.AxisTemperature, 37.2d));
                Assert.Equal(tokens.CelsiusRowHeight, row, 3);
                Assert.Equal(tokens.LabelWidth, geo.Slots.Left - chart.Style.Grid.LeftStripWidth, 3);
            }
        }

        [Fact]
        public void 输血单_发热阈值线与反应事件线都是红的()
        {
            using (Chart chart = TestDocuments.Transfusion())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                double feverY = geo.YOf(TransfusionChartTemplate.AxisTemperature, 38d);

                Assert.True(probe.HasRedInBox((int)geo.GridLeft + 2, (int)feverY - 1,
                        (int)geo.GridRight - 2, (int)feverY + 2),
                    "38℃ 处没有红色发热阈值线");

                int eventX = (int)Math.Round(geo.SlotCenterX(3));
                Assert.True(probe.HasRedInBox(eventX - 1, (int)geo.GridRect.Top + 2,
                        eventX + 2, (int)geo.GridRect.Bottom - 2),
                    "反应事件时点没有红色竖线");
            }
        }

        [Fact]
        public void 输血单_分页后体温与事件按新槽号重算()
        {
            TransfusionChartDocument doc = TestDocuments.TransfusionDocument();
            TransfusionChartDocument page2 = doc.Subset(1, 1);

            Assert.Equal(8, page2.Temperature.Values.Count);
            Assert.Equal(36.7d, page2.Temperature.Values[0], 3);
            Assert.Single(page2.Events);
            Assert.Equal(4, page2.Events[0].Slot);
            Assert.Equal("乙", FindCell(page2.Cells, TransfusionFields.Recorder, 4)?.Text);
        }

        // ---------------------------------------------------------------- 目录

        [Fact]
        public void 目录_19张单子全部实现且四类齐备()
        {
            Assert.Equal(19, SpecialtyFormCatalog.All.Count);

            HashSet<SpecialtyFormKind> kinds = new HashSet<SpecialtyFormKind>();
            foreach (SpecialtyFormEntry entry in SpecialtyFormCatalog.All)
            {
                kinds.Add(entry.Kind);
                Assert.Equal(SpecialtyFormStatus.Implemented, entry.Status);
                Assert.False(string.IsNullOrEmpty(entry.TemplateKey),
                    entry.Key + " 标为已实现却没有模板键");
                Assert.False(string.IsNullOrEmpty(entry.Name), entry.Key + " 缺名称");
                Assert.StartsWith("medical.", entry.TemplateKey);
            }

            Assert.Equal(4, kinds.Count);
            Assert.Equal(19, SpecialtyFormCatalog.ByStatus(SpecialtyFormStatus.Implemented).Count());
            Assert.Empty(SpecialtyFormCatalog.ByStatus(SpecialtyFormStatus.Planned));
            Assert.Equal(5, SpecialtyFormCatalog.ByKind(SpecialtyFormKind.Specialty).Count());
            Assert.Equal(6, SpecialtyFormCatalog.ByKind(SpecialtyFormKind.DepartmentOperations).Count());
            Assert.Equal(4, SpecialtyFormCatalog.ByKind(SpecialtyFormKind.EventTriggered).Count());
            Assert.Equal(4, SpecialtyFormCatalog.ByKind(SpecialtyFormKind.DeviceOutput).Count());
            Assert.NotNull(SpecialtyFormCatalog.Find("pain-assessment"));
            Assert.Null(SpecialtyFormCatalog.Find("no-such-form"));
            Assert.Equal("① 专科专属型", SpecialtyFormCatalog.KindText(SpecialtyFormKind.Specialty));
            Assert.Equal("已实现", SpecialtyFormCatalog.StatusText(SpecialtyFormStatus.Implemented));
        }

        [Fact]
        public void 目录_每个模板键都能找到对应模板类型()
        {
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (DemoSample sample in DemoCatalog.All)
            {
                using (Chart chart = sample.Create())
                {
                    keys.Add(chart.Template.Key);
                }
            }

            foreach (SpecialtyFormEntry entry in SpecialtyFormCatalog.All)
            {
                Assert.Contains(entry.TemplateKey!, keys);
            }
        }

        [Fact]
        public void 全部样例都能渲染导出且测量幂等()
        {
            Assert.Equal(40, DemoCatalog.All.Count);
            foreach (DemoSample sample in DemoCatalog.All)
            {
                Assert.False(string.IsNullOrEmpty(sample.DisplayName), sample.Name + " 缺显示名");
                using (Chart chart = sample.Create())
                {
                    SKSize first = chart.MeasureSize();
                    Assert.True(first.Width > 200f && first.Height > 200f,
                        sample.Name + " 版面尺寸异常：" + first.Width + "x" + first.Height);
                    Assert.Equal(first.Width, chart.MeasureSize().Width, 3);
                    Assert.Equal(first.Height, chart.MeasureSize().Height, 3);

                    byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                    Assert.True(png.Length > 3000 && png[0] == 0x89 && png[1] == 0x50,
                        sample.Name + " PNG 产物过小或魔数不对");

                    string svg = ChartExport.ToSvgString(chart);
                    Assert.True(svg.Contains("<path") && svg.Contains("<text"),
                        sample.Name + " SVG 缺矢量线条或文字");
                    AssertSvgNotScreenshot(sample.Name, svg);
                }
            }
        }

        [Fact]
        public void 三张新单都能导出位图与矢量()
        {
            AssertRoundTrip(TestDocuments.Pain(), "pain");
            AssertRoundTrip(TestDocuments.Restraint(), "restraint");
            AssertRoundTrip(TestDocuments.Transfusion(), "transfusion");
        }

        [Fact]
        public void SVG退化成截图的判据能真的拦住两种伪矢量()
        {
            const string png = "iVBORw0KGgo=";
            string bare = "<svg><image x=\"0\" y=\"0\" width=\"794\" height=\"1896\" xlink:href=\"data:image/png;base64," + png + "\"/></svg>";
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSvgNotScreenshot("裸截图", bare));

            string tiledFull = "<svg><defs><pattern id=\"p0\"><image width=\"794\" height=\"1896\" xlink:href=\"data:image/png;base64,"
                + png + "\"/></pattern></defs><rect fill=\"url(#p0)\"/></svg>";
            Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertSvgNotScreenshot("包进 pattern 的整页截图", tiledFull));

            string tile = "<svg><defs><pattern id=\"p0\"><image width=\"12\" height=\"12\" xlink:href=\"data:image/png;base64,"
                + png + "\"/></pattern></defs><rect fill=\"url(#p0)\"/></svg>";
            AssertSvgNotScreenshot("12px 图样 tile", tile);
        }

        /// <summary>
        /// SVG 不得退化成截图：线条与文字必须是矢量。
        /// 唯一例外是图样填充——SkiaSharp 3.116.1 没有矢量 pattern shader（只有 CreateBitmap），
        /// SKSvgCanvas 只能把它写成 &lt;defs&gt;&lt;pattern&gt; 内嵌一枚 tile 位图；
        /// 所以这里允许 &lt;image&gt;，但必须整体小于 64px 且被 &lt;pattern&gt; 包住（见 docs/06 §7 T200）。
        /// </summary>
        private static void AssertSvgNotScreenshot(string label, string svg)
        {
            int images = CountOf(svg, "<image");
            int patterns = CountOf(svg, "<pattern");
            Assert.True(images <= patterns,
                label + " SVG 有未被 <pattern> 包裹的位图：" + images + " 张 <image> / " + patterns + " 个 <pattern>");

            int cursor = 0;
            while ((cursor = svg.IndexOf("<image", cursor, StringComparison.Ordinal)) >= 0)
            {
                int end = svg.IndexOf("/>", cursor, StringComparison.Ordinal);
                Assert.True(end > cursor, label + " SVG 的 <image> 节点没有闭合");
                string node = svg.Substring(cursor, end - cursor);
                double width = AttributeValue(node, "width");
                double height = AttributeValue(node, "height");
                Assert.True(width > 0d && width <= 64d && height > 0d && height <= 64d,
                    label + " SVG 内嵌位图不是图样 tile：" + width + "×" + height);
                Assert.False(node.Contains("width=\"100%\""), label + " SVG 内嵌位图铺满画布");
                cursor = end;
            }
        }

        private static double AttributeValue(string node, string name)
        {
            string needle = name + "=\"";
            int start = node.IndexOf(needle, StringComparison.Ordinal);
            if (start < 0)
            {
                return double.NaN;
            }
            start += needle.Length;
            int end = node.IndexOf('"', start);
            double value;
            return end > start
                   && double.TryParse(node.Substring(start, end - start),
                       System.Globalization.NumberStyles.Float,
                       System.Globalization.CultureInfo.InvariantCulture, out value)
                ? value
                : double.NaN;
        }

        private static int CountOf(string text, string needle)
        {
            int count = 0;
            int cursor = 0;
            while ((cursor = text.IndexOf(needle, cursor, StringComparison.Ordinal)) >= 0)
            {
                count++;
                cursor += needle.Length;
            }
            return count;
        }

        private static void AssertRoundTrip(Chart chart, string name)
        {
            using (chart)
            {
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                Assert.True(png.Length > 1000 && png[0] == 0x89 && png[1] == 0x50,
                    name + " PNG 产物过小或魔数不对");

                byte[] pdf;
                using (MemoryStream stream = new MemoryStream())
                {
                    chart.WritePdf(stream);
                    pdf = stream.ToArray();
                }
                Assert.True(Encoding.ASCII.GetString(pdf, 0, 5) == "%PDF-", name + " PDF 头不对");

                string svg = ChartExport.ToSvgString(chart);
                Assert.True(svg.Contains("<path") && svg.Contains("<text"),
                    name + " SVG 缺矢量线条或文字");
                Assert.False(svg.Contains("<image"), name + " SVG 内嵌了位图");

                // Measure 必须幂等：同一文档连续测量两次，版面不能漂移。
                SKSize first = chart.MeasureSize();
                SKSize second = chart.MeasureSize();
                Assert.Equal(first.Width, second.Width, 3);
                Assert.Equal(first.Height, second.Height, 3);
            }
        }

        private static CellValue? FindCell(IList<CellValue> cells, string rowKey, int slot)
        {
            foreach (CellValue cell in cells)
            {
                if (cell.RowKey == rowKey && cell.Slot == slot)
                {
                    return cell;
                }
            }
            return null;
        }
    }
}
