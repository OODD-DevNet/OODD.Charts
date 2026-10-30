using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Forms.AnesthesiaRecord;
using OODD.Charts.Medical.Forms.AstReport;
using OODD.Charts.Medical.Forms.DeathCertificate;
using OODD.Charts.Medical.Forms.IcuNursing;
using OODD.Charts.Medical.Forms.VaccinationBooklet;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Templates;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 波次 6 / 腿 D：医疗域 T270–T274 的界面与像素验收。
    /// 每个验收条目至少一条 Fact；凡是"没有墨"的断言都配同版面的正对照。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class MedicalFormsTests
    {
        // ================================================================== 公共工具

        private static byte[] Render(ChartDocument document, IChartTemplate template, out ChartGeometry geometry)
        {
            using (Chart chart = new Chart(document, template, new OODD.Charts.Style.ChartStyle()))
            {
                geometry = chart.Geometry;
                return chart.Encode(ChartImageFormat.Png, 96f, 100);
            }
        }

        private static void AssertInk(ImageProbe probe, SKRect rect, string label)
        {
            Assert.True(rect.Width > 0f && rect.Height > 0f,
                label + "：矩形为空 (" + Describe(rect) + ")");
            Assert.True(probe.HasInkInBox((int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
                    (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom)),
                label + "：矩形 " + Describe(rect) + " 内没有墨迹");
        }

        private static void AssertNoInk(ImageProbe probe, SKRect rect, string label)
        {
            Assert.True(probe.HasInkInBox((int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
                    (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom)) == false,
                label + "：矩形 " + Describe(rect) + " 内不应有墨迹");
        }

        private static string Describe(SKRect rect)
        {
            return "(" + rect.Left.ToString("0.#") + "," + rect.Top.ToString("0.#") + ")-("
                   + rect.Right.ToString("0.#") + "," + rect.Bottom.ToString("0.#") + ")";
        }

        /// <summary>A18：令牌变化必须落到渲染产物上，不能只改几何。</summary>
        private static void AssertRenderedChange(byte[] before, byte[] after, string label)
        {
            bool same = before.Length == after.Length;
            if (same)
            {
                for (int i = 0; i < before.Length; i++)
                {
                    if (before[i] != after[i])
                    {
                        same = false;
                        break;
                    }
                }
            }
            Assert.True(!same,
                label + "：改令牌后 PNG 应发生变化，实测前后均 " + before.Length + " 字节且逐字节相同");
        }

        /// <summary>统计单元格内"离开对角斜线带"的墨迹像素数（用于"斜线格不写字"）。</summary>
        private static int InkOffDiagonal(ImageProbe probe, SKRect rect, float bandPx)
        {
            int left = (int)Math.Ceiling(rect.Left) + 3;
            int right = (int)Math.Floor(rect.Right) - 3;
            int top = (int)Math.Ceiling(rect.Top) + 3;
            int bottom = (int)Math.Floor(rect.Bottom) - 3;
            int count = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (!probe.IsInk(x, y))
                    {
                        continue;
                    }
                    double lineY = rect.Top + (x - rect.Left) * (rect.Height / rect.Width);
                    if (Math.Abs(y - lineY) > bandPx)
                    {
                        count++;
                    }
                }
            }
            return count;
        }

        private static AnesthesiaRecordDocument AnesthesiaDocument()
        {
            AnesthesiaRecordDocument doc = new AnesthesiaRecordDocument();
            doc.HospitalName = "示范医院";
            doc.FormTitle = "麻醉记录单";
            doc.Patient.Name = "张三";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "45";
            doc.Patient.Department = "普外科";
            doc.Patient.BedNumber = "12";
            doc.Operation.Procedure = "腹腔镜胆囊切除";
            doc.Operation.AnesthesiaType = "全身麻醉";
            doc.Operation.Anesthetist = "李四";
            doc.Operation.StartTime = "08:00";
            doc.Operation.EndTime = "12:00";
            doc.Points.Add(new AnesthesiaPoint(0d, 120d, 80d, 93d, 78d, 36.5d, 98d, 35d));
            doc.Points.Add(new AnesthesiaPoint(20d, 110d, 70d, 83d, 72d, 36.3d, 99d, 34d));
            doc.Points.Add(new AnesthesiaPoint(30d, 140d, 90d, 106d, 88d, 36.8d, 97d, 36d));
            doc.Points.Add(new AnesthesiaPoint(60d, 130d, 85d, 100d, 80d, 36.6d, 98d, 35d));
            doc.ContinuousMedications.Add(new ContinuousMedication("丙泊酚", 5d, 55d, "6 mg/kg/h"));
            doc.Events.Add(new AnesthesiaEvent(10d, 30d, "BP", 1, 0));
            doc.Events.Add(new AnesthesiaEvent(40d, 60d, "HR", 2, 6));
            return doc;
        }

        private static DeathCertificateDocument DeathDocument()
        {
            DeathCertificateDocument doc = new DeathCertificateDocument();
            doc.HospitalName = "示范医院";
            doc.Patient.Name = "王五";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "78";
            doc.Set(DeathCertificateFields.IdNumber, "110101194801011234");
            doc.Set(DeathCertificateFields.DeathDate, "2026-10-01");
            doc.Set(DeathCertificateFields.Household, "北京市东城区");
            doc.Set(DeathCertificateFields.Phone, "13800000000");
            doc.Set(DeathCertificateFields.Reporter, "王小明");
            doc.Set(DeathCertificateFields.FuneralHotline, "96156");
            doc.Set(DeathCertificateFields.CauseA, "心力衰竭");
            doc.Set(DeathCertificateFields.CauseB, "冠心病");
            doc.Set(DeathCertificateFields.CauseC, "高血压");
            doc.Set(DeathCertificateFields.CauseD, "动脉硬化");
            return doc;
        }

        private static IcuNursingDocument IcuDocument()
        {
            IcuNursingDocument doc = new IcuNursingDocument();
            doc.HospitalName = "示范医院";
            doc.FormTitle = "ICU 护理记录单";
            doc.Patient.Name = "赵六";
            doc.Patient.BedNumber = "ICU-3";
            string[] labels = { "08:00", "09:00", "10:00", "11:00" };
            string[][] values =
            {
                new[] { "82", "18", "37.1", "118", "72", "8", "500", "300", "200" },
                new[] { "88", "19", "37.3", "125", "78", "9", "450", "280", "210" },
                new[] { "91", "20", "37.6", "130", "80", "10", "600", "320", "240" },
                new[] { "86", "18", "37.2", "122", "76", "9", "550", "300", "225" },
            };
            for (int r = 0; r < labels.Length; r++)
            {
                IcuRecord record = new IcuRecord(labels[r]);
                for (int c = 0; c < IcuNursingColumns.All.Count; c++)
                {
                    record.Set(IcuNursingColumns.All[c], values[r][c]);
                }
                doc.Records.Add(record);
            }
            doc.HandoverNotes.Add("白班：生命体征平稳");
            doc.HandoverNotes.Add("夜间：尿量偏少");
            doc.HandoverNotes.Add("注意气道护理");
            doc.HandoverNotes.Add("已通知主管医师");
            return doc;
        }

        private static VaccinationBookletDocument VaccinationDocument()
        {
            VaccinationBookletDocument doc = new VaccinationBookletDocument();
            doc.HospitalName = "示范社区卫生服务中心";
            doc.Patient.Name = "李小明";
            doc.Patient.Sex = "男";
            doc.Patient.AdmissionDate = "2021-05-20";
            doc.Records.Add(new VaccinationRecord("乙肝疫苗", "1", "2021-05-20", "示范生物", "20210501"));
            doc.Records.Add(new VaccinationRecord("卡介苗", "1", "2021-05-22", "示范生物", "20210502"));
            doc.Assessments.Add("已完成首剂乙肝与卡介苗接种。");
            return doc;
        }

        private static AstReportDocument AstDocument()
        {
            AstReportDocument doc = new AstReportDocument();
            doc.HospitalName = "示范医院";
            doc.FormTitle = "药敏报告单";
            doc.Patient.Name = "孙七";
            doc.Specimen = "痰";
            doc.Organism = "肺炎克雷伯菌";
            doc.Antibiotics.Add(new AstAntibiotic("亚胺培南", "0.5", "R"));
            doc.Antibiotics.Add(new AstAntibiotic("阿莫西林", "2", "S"));
            doc.Antibiotics.Add(new AstAntibiotic("头孢曲松", "4", "SDD"));
            doc.Antibiotics.Add(new AstAntibiotic("左氧氟沙星", "2", "I"));
            return doc;
        }



        // ================================================================== T270 麻醉记录单

        [Fact]
        public void 麻醉记录单左右纵轴共用横线且标签列在网格外()
        {
            AnesthesiaRecordDocument doc = AnesthesiaDocument();
            AnesthesiaRecordTemplate template = new AnesthesiaRecordTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            AnesthesiaRecordLayout layout = geometry.LayoutOf<AnesthesiaRecordLayout>()!;

            Assert.True(Math.Abs(layout.LeftAxis.PositionOf(20d) - layout.RightAxis.PositionOf(12d)) < 0.01f,
                "左轴 20 与右轴 12 应落在同一横线：左=" + layout.LeftAxis.PositionOf(20d).ToString("0.###")
                + " 右=" + layout.RightAxis.PositionOf(12d).ToString("0.###"));
            Assert.True(Math.Abs(layout.LeftAxis.PositionOf(20d) - layout.PlotRect.Bottom) < 0.01f,
                "左轴 20 应落在绘图区下缘 " + layout.PlotRect.Bottom.ToString("0.###")
                + "，实测 " + layout.LeftAxis.PositionOf(20d).ToString("0.###"));
            Assert.True(Math.Abs(layout.LeftAxis.PositionOf(260d) - layout.RightAxis.PositionOf(40d)) < 0.01f,
                "左轴 260 与右轴 40 应落在同一横线：左=" + layout.LeftAxis.PositionOf(260d).ToString("0.###")
                + " 右=" + layout.RightAxis.PositionOf(40d).ToString("0.###"));
            Assert.True(layout.LeftLabelRect.Right <= layout.PlotRect.Left,
                "左侧标签列必须完全在网格外：标签右缘 " + layout.LeftLabelRect.Right.ToString("0.#")
                + "，网格左缘 " + layout.PlotRect.Left.ToString("0.#"));
            Assert.True(layout.RightLabelRect.Left >= layout.PlotRect.Right,
                "右侧标签列必须完全在网格外：标签左缘 " + layout.RightLabelRect.Left.ToString("0.#")
                + "，网格右缘 " + layout.PlotRect.Right.ToString("0.#"));

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                IReadOnlyList<double> lines = probe.StrongHorizontalLines(
                    layout.PlotRect.Top + 1f, layout.PlotRect.Bottom - 1f,
                    layout.PlotRect.Left + 2f, layout.PlotRect.Right - 2f, 0.5);
                Assert.True(lines.Count == 13,
                    "绘图区应有 13 条横线（20–260 步长 20），实测 " + lines.Count);
                double expected = layout.PlotRect.Height / 12d;
                double average = (lines[lines.Count - 1] - lines[0]) / 12d;
                Assert.True(Math.Abs(average - expected) <= 0.3,
                    "13 条横线的平均间距应为 " + expected.ToString("0.##") + "px，实测 "
                    + average.ToString("0.###") + "；线心 " + string.Join(",", lines));
                foreach (double gap in ImageProbe.Gaps(lines))
                {
                    Assert.True(Math.Abs(gap - expected) <= 1.3,
                        "横线间距应为 " + expected.ToString("0.##") + "px（=" + layout.PlotRect.Height.ToString("0.#")
                        + "/12，位图量化容差 1.3px），实测 " + gap.ToString("0.##") + "；全部线心 "
                        + string.Join(",", lines));
                }

                AssertInk(probe, new SKRect(0f, layout.PlotRect.Top - 9f, layout.PlotRect.Left - 3f,
                    layout.PlotRect.Top + 9f), "左轴 260 标签");
                AssertInk(probe, new SKRect(0f, layout.PlotRect.Bottom - 9f, layout.PlotRect.Left - 3f,
                    layout.PlotRect.Bottom + 9f), "左轴 20 标签");
                AssertInk(probe, new SKRect(layout.PlotRect.Right + 4f, layout.PlotRect.Bottom - 8f,
                    layout.Bounds.Right, layout.PlotRect.Bottom + 8f), "右轴 12 标签");
            }
        }

        [Fact]
        public void 麻醉记录单图例逐项落在页内且序号不压符号()
        {
            AnesthesiaRecordDocument doc = AnesthesiaDocument();
            AnesthesiaRecordTemplate template = new AnesthesiaRecordTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            AnesthesiaRecordLayout layout = geometry.LayoutOf<AnesthesiaRecordLayout>()!;

            Assert.True(layout.LegendItems.Count == 11,
                "页脚应登记 11 个图例项，实到 " + layout.LegendItems.Count);
            for (int i = 0; i < layout.LegendItems.Count; i++)
            {
                SKRect item = layout.LegendItems[i];
                Assert.True(item.Left >= 0f && item.Right <= layout.Bounds.Width + 0.01f
                            && item.Top >= layout.FooterRect.Top && item.Bottom <= layout.FooterRect.Bottom + 0.01f,
                    "图例项 " + (i + 1) + " " + Describe(item) + " 越出页脚/页面 "
                    + Describe(layout.FooterRect));
                for (int j = i + 1; j < layout.LegendItems.Count; j++)
                {
                    Assert.False(item.IntersectsWith(layout.LegendItems[j]),
                        "图例项 " + (i + 1) + " " + Describe(item) + " 与第 " + (j + 1) + " 项 "
                        + Describe(layout.LegendItems[j]) + " 相交");
                }
            }

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                float halfSpan = template.Tokens.LegendSymbolSize * 2.1f;
                for (int i = 0; i < layout.LegendItems.Count; i++)
                {
                    SKRect item = layout.LegendItems[i];
                    SKRect glyph = new SKRect(item.Left - 1f, item.Top - 1f,
                        item.Left + halfSpan * 2f + 1f, item.Bottom + 1f);
                    SKRect label = new SKRect(item.Right - template.Tokens.LegendLabelWidth, item.Top,
                        item.Right, item.Bottom);
                    AssertInk(probe, glyph, "图例符号 " + (i + 1));
                    AssertInk(probe, label, "图例序号 " + (i + 1));
                }
            }

            using (Chart chart = new Chart(doc, template, new OODD.Charts.Style.ChartStyle()))
            {
                string svg = ChartExport.ToSvgString(chart);
                Assert.True(svg.Contains("⑩") && svg.Contains("(11)"),
                    "图例序号应与事件带同源的圆圈数字（1–10）并对第 11 项退回 (11)，SVG 里找不到 ⑩/(11)");
                Assert.False(svg.Contains(">E11<"),
                    "图例序号不应再退回 E1…E11 的纯文本编号");
            }
        }

        [Fact]
        public void 麻醉记录单复合符号两端点落在两个时刻的收缩压上()
        {
            AnesthesiaRecordDocument doc = AnesthesiaDocument();
            AnesthesiaRecordTemplate template = new AnesthesiaRecordTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            AnesthesiaRecordLayout layout = geometry.LayoutOf<AnesthesiaRecordLayout>()!;

            Assert.True(layout.EventEnds.Count == 2,
                "两条事件都应求出端点，实测 " + layout.EventEnds.Count + " 条");
            SKPoint start = layout.EventEnds[0][0];
            SKPoint end = layout.EventEnds[0][1];
            float expectedStartX = layout.TimeAxis.PositionOf(10d);
            float expectedEndX = layout.TimeAxis.PositionOf(30d);
            float expectedStartY = layout.LeftAxis.PositionOf(115d);
            float expectedEndY = layout.LeftAxis.PositionOf(140d);

            Assert.True(Math.Abs(start.X - expectedStartX) < 0.01f && Math.Abs(start.Y - expectedStartY) < 0.01f,
                "左端点应为 (10min, 收缩压120 与 110 的线性插值 115) → (" + expectedStartX.ToString("0.###") + ","
                + expectedStartY.ToString("0.###") + ")，实测 (" + start.X.ToString("0.###") + ","
                + start.Y.ToString("0.###") + ")");
            Assert.True(Math.Abs(end.X - expectedEndX) < 0.01f && Math.Abs(end.Y - expectedEndY) < 0.01f,
                "右端点应为 (30min, 收缩压140) → (" + expectedEndX.ToString("0.###") + ","
                + expectedEndY.ToString("0.###") + ")，实测 (" + end.X.ToString("0.###") + ","
                + end.Y.ToString("0.###") + ")");
            Assert.True(Math.Abs(end.Y - start.Y) > 5f,
                "两端点 y 必须不同（120 vs 140），实测差 " + Math.Abs(end.Y - start.Y).ToString("0.##") + "px");

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                AssertInk(probe, new SKRect(start.X - 6f, start.Y - 6f, start.X + 6f, start.Y + 6f),
                    "事件符号左端点");
                AssertInk(probe, new SKRect(end.X - 6f, end.Y - 6f, end.X + 6f, end.Y + 6f),
                    "事件符号右端点");
                float midX = (start.X + end.X) / 2f;
                float midY = (start.Y + end.Y) / 2f;
                AssertInk(probe, new SKRect(midX - 3f, midY - 3f, midX + 3f, midY + 3f),
                    "两端点之间的连线");
            }
        }

        [Fact]
        public void 麻醉记录单采样密度只告警不拦截()
        {
            AnesthesiaRecordDocument sparse = new AnesthesiaRecordDocument();
            sparse.Points.Add(new AnesthesiaPoint(0d, 120d, 80d, 93d, 78d, 36.5d, 98d, 35d));
            sparse.Points.Add(new AnesthesiaPoint(10d, 118d, 78d, 91d, 76d, 36.4d, 98d, 35d));
            sparse.Points.Add(new AnesthesiaPoint(240d, 115d, 75d, 88d, 74d, 36.2d, 97d, 34d));

            IReadOnlyList<string> warnings = sparse.SamplingWarnings();
            Assert.True(warnings.Count == 2,
                "0→10 与 10→240 两段超 5min，应 2 条告警，实测 " + warnings.Count);
            Assert.True(warnings[0].Contains("5 min"),
                "告警应写明 WS 329—2024 的 5 min 要求，实测：" + warnings[0]);

            AnesthesiaRecordDocument dense = new AnesthesiaRecordDocument();
            dense.Points.Add(new AnesthesiaPoint(0d, 120d, 80d, 93d, 78d, 36.5d, 98d, 35d));
            dense.Points.Add(new AnesthesiaPoint(5d, 118d, 78d, 91d, 76d, 36.4d, 98d, 35d));
            dense.Points.Add(new AnesthesiaPoint(10d, 116d, 77d, 90d, 75d, 36.3d, 98d, 35d));
            Assert.True(dense.SamplingWarnings().Count == 0,
                "5min 间隔不应告警，实测 " + dense.SamplingWarnings().Count);

            AnesthesiaRecordTemplate template = new AnesthesiaRecordTemplate();
            ChartGeometry geometry;
            byte[] png = Render(sparse, template, out geometry);
            Assert.True(png.Length > 1000, "告警不得阻止绘制，实测 PNG " + png.Length + " 字节");
            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                Assert.True(probe.Width > 0 && probe.Height > 0,
                    "告警文档应正常出图，实测 " + probe.Width + "x" + probe.Height);
            }
        }

        [Fact]
        public void 麻醉记录单提供十一种成对符号且页脚可见()
        {
            IReadOnlyList<PairedEventGlyph> symbols = AnesthesiaRecordDocument.CreatePairedSymbols();
            Assert.True(symbols.Count == 11, "成对符号应为 11 种，实测 " + symbols.Count);
            foreach (PairedEventGlyph glyph in symbols)
            {
                Assert.True(glyph != null, "成对符号列表含 null");
            }

            AnesthesiaRecordDocument doc = AnesthesiaDocument();
            AnesthesiaRecordTemplate template = new AnesthesiaRecordTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            AnesthesiaRecordLayout layout = geometry.LayoutOf<AnesthesiaRecordLayout>()!;
            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                double ratio = probe.NonWhiteRatio(layout.FooterRect);
                Assert.True(ratio > 0.02,
                    "页脚 11 种符号图例应有墨，实测非白占比 " + ratio.ToString("0.000"));
            }
        }

        [Fact]
        public void 麻醉记录单版面高度由令牌驱动()
        {
            AnesthesiaRecordDocument doc = AnesthesiaDocument();
            AnesthesiaRecordTemplate template = new AnesthesiaRecordTemplate();
            ChartGeometry first;
            byte[] firstPng = Render(doc, template, out first);
            AnesthesiaRecordLayout before = first.LayoutOf<AnesthesiaRecordLayout>()!;
            float beforeY = before.LeftAxis.PositionOf(100d);
            float beforeHeight = before.Bounds.Height;

            template.Tokens.PlotHeight = 320f;
            ChartGeometry second;
            byte[] secondPng = Render(doc, template, out second);
            AnesthesiaRecordLayout after = second.LayoutOf<AnesthesiaRecordLayout>()!;
            float afterY = after.LeftAxis.PositionOf(100d);

            Assert.True(Math.Abs(after.PlotRect.Height - 320f) < 0.01f,
                "改令牌后绘图区高应为 320，实测 " + after.PlotRect.Height.ToString("0.#"));
            Assert.True(Math.Abs(afterY - beforeY) > 20f,
                "PlotHeight 260→320 后 100 单位的 y 应明显移动，前 " + beforeY.ToString("0.##")
                + " 后 " + afterY.ToString("0.##"));
            Assert.True(Math.Abs((after.Bounds.Height - beforeHeight) - 60f) < 0.01f,
                "版面高应增加 60px，实测增加 " + (after.Bounds.Height - beforeHeight).ToString("0.##"));
            AssertRenderedChange(firstPng, secondPng, "麻醉记录单 PlotHeight");
        }

        // ================================================================== T271 死亡证明四联

        [Fact]
        public void 死亡证明四联宽度之和等于版面宽且逐联接续()
        {
            DeathCertificateDocument doc = DeathDocument();
            DeathCertificateTemplate template = new DeathCertificateTemplate();
            ChartGeometry geometry;
            Render(doc, template, out geometry);
            DeathCertificateLayout layout = geometry.LayoutOf<DeathCertificateLayout>()!;

            Assert.True(Math.Abs(layout.Spec.TotalExtent - layout.Bounds.Width) < 0.01f,
                "四联宽之和应等于版面宽：和=" + layout.Spec.TotalExtent.ToString("0.###")
                + " 版面=" + layout.Bounds.Width.ToString("0.###"));
            Assert.True(layout.Spec.Count == 4, "应为四联，实测 " + layout.Spec.Count);
            for (int i = 0; i < layout.Spec.Count; i++)
            {
                Assert.True(Math.Abs(layout.CopyRects[i].Width - template.Tokens.CopyWidth) < 0.01f,
                    "第 " + i + " 联宽应为 " + template.Tokens.CopyWidth.ToString("0.#")
                    + "，实测 " + layout.CopyRects[i].Width.ToString("0.##"));
                if (i > 0)
                {
                    Assert.True(Math.Abs(layout.CopyRects[i].Left - layout.CopyRects[i - 1].Right) < 0.01f,
                        "第 " + (i - 1) + " 联右缘 " + layout.CopyRects[i - 1].Right.ToString("0.###")
                        + " 应等于第 " + i + " 联左缘 " + layout.CopyRects[i].Left.ToString("0.###"));
                }
            }
            Assert.True(Math.Abs(layout.CopyRects[3].Right - layout.Bounds.Right) < 0.01f,
                "第四联右缘应贴版面右缘：联=" + layout.CopyRects[3].Right.ToString("0.###")
                + " 版面=" + layout.Bounds.Right.ToString("0.###"));
        }

        [Fact]
        public void 死亡证明每联只画自己的可见字段()
        {
            DeathCertificateDocument doc = DeathDocument();
            DeathCertificateTemplate template = new DeathCertificateTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            DeathCertificateLayout layout = geometry.LayoutOf<DeathCertificateLayout>()!;

            int stub = 0;
            int police = 1;
            int relative = 2;
            int funeral = 3;

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                AssertInk(probe, layout.FieldRect(stub, DeathCertificateFields.Name), "存根联姓名（共有字段）");
                AssertInk(probe, layout.FieldRect(police, DeathCertificateFields.Name), "公安联姓名（共有字段）");

                AssertNoInk(probe, layout.FieldRect(stub, DeathCertificateFields.Household),
                    "存根联不显示公安联独有的户籍地址");
                AssertInk(probe, layout.FieldRect(police, DeathCertificateFields.Household),
                    "公安联户籍地址（正对照）");

                AssertNoInk(probe, layout.FieldRect(relative, DeathCertificateFields.FuneralHotline),
                    "近亲属联不显示殡仪馆独有的殡葬热线");
                AssertInk(probe, layout.FieldRect(funeral, DeathCertificateFields.FuneralHotline),
                    "殡仪馆联殡葬热线（正对照）");

                AssertNoInk(probe, layout.CauseRect(police, DeathCertificateFields.CauseB),
                    "公安联只显示死因 (a)，不显示 (b)");
                AssertInk(probe, layout.CauseRect(stub, DeathCertificateFields.CauseB),
                    "存根联死因 (b)（正对照）");
            }
        }

        [Fact]
        public void 死亡证明六格行政区划与十七格编号都落字()
        {
            DeathCertificateDocument doc = DeathDocument();
            DeathCertificateTemplate template = new DeathCertificateTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            DeathCertificateLayout layout = geometry.LayoutOf<DeathCertificateLayout>()!;

            Assert.True(template.Tokens.AdminBoxCount == 6,
                "行政区划格数应为 6，实测 " + template.Tokens.AdminBoxCount);
            Assert.True(template.Tokens.SerialBoxCount == 17,
                "编号格数应为 17，实测 " + template.Tokens.SerialBoxCount);

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                SKRect admin = layout.AdminRowRect(0);
                SKRect serial = layout.SerialRowRect(0);
                AssertInk(probe, admin, "行政区划栅格行");
                AssertInk(probe, serial, "编号栅格行");

                DigitBox adminBox = new DigitBox(6, 0.62f, 0f);
                SKRect lastAdmin = adminBox.BoxRectFromLeft(new SKPoint(admin.Left, admin.Top),
                    template.Tokens.AdminBoxSize, 5);
                AssertInk(probe, lastAdmin, "行政区划第 6 格（应为末位数字）");

                DigitBox serialBox = new DigitBox(17, 0.62f, 0f);
                SKRect lastSerial = serialBox.BoxRectFromLeft(new SKPoint(serial.Left, serial.Top),
                    template.Tokens.SerialBoxSize, 16);
                AssertInk(probe, lastSerial, "编号第 17 格（应为末位数字）");

                IReadOnlyList<double> verticals = probe.StrongVerticalLines(
                    serial.Left - 1f, serial.Left + serialBox.RowWidthOf(template.Tokens.SerialBoxSize) + 1f,
                    serial.Top + 1f, serial.Bottom - 1f, 0.8);
                Assert.True(verticals.Count == 18,
                    "编号 17 格应有 18 条竖边界，实测 " + verticals.Count);
            }
        }

        [Fact]
        public void 死亡证明字段行高由令牌驱动()
        {
            DeathCertificateDocument doc = DeathDocument();
            DeathCertificateTemplate template = new DeathCertificateTemplate();
            ChartGeometry first;
            byte[] firstPng = Render(doc, template, out first);
            DeathCertificateLayout before = first.LayoutOf<DeathCertificateLayout>()!;
            float beforeFieldHeight = before.FieldRect(0, DeathCertificateFields.Name).Height;
            float beforeCauseTop = before.CauseRect(0, DeathCertificateFields.CauseA).Top;
            float beforeHeight = before.Bounds.Height;

            template.Tokens.RowHeight = 30f;
            ChartGeometry second;
            byte[] secondPng = Render(doc, template, out second);
            DeathCertificateLayout after = second.LayoutOf<DeathCertificateLayout>()!;
            float afterFieldHeight = after.FieldRect(0, DeathCertificateFields.Name).Height;
            float afterCauseTop = after.CauseRect(0, DeathCertificateFields.CauseA).Top;

            Assert.True(Math.Abs((afterFieldHeight - beforeFieldHeight) - 8f) < 0.01f,
                "RowHeight 22→30 后普通字段行高应增加 8px，实测增加 "
                + (afterFieldHeight - beforeFieldHeight).ToString("0.##"));
            Assert.True(Math.Abs((afterCauseTop - beforeCauseTop) - 80f) < 0.01f,
                "死因 (a) 前有 10 个普通字段行，行高 +8 后其顶应下移 80px，实测 "
                + (afterCauseTop - beforeCauseTop).ToString("0.##"));
            Assert.True(Math.Abs((after.Bounds.Height - beforeHeight) - 112f) < 0.01f,
                "14 行（10 普通 + 4 死因）各增 8px，版面应增高 112px，实测 "
                + (after.Bounds.Height - beforeHeight).ToString("0.##"));
            AssertRenderedChange(firstPng, secondPng, "死亡证明 RowHeight");
        }

        // ================================================================== T272 ICU 护理记录单

        [Fact]
        public void Icu三级表头各层叶跨度求和等于叶子列数()
        {
            IcuNursingDocument doc = IcuDocument();
            IcuNursingTemplate template = new IcuNursingTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            IcuNursingLayout layout = geometry.LayoutOf<IcuNursingLayout>()!;
            ColumnHeaderNode root = layout.Root;

            Assert.True(root.Depth == 2,
                "应为根/组/叶三级表头（根深度 2），实测 " + root.Depth);
            Assert.True(layout.Header.LevelCount == 3,
                "表头层数应为 3，实测 " + layout.Header.LevelCount);
            Assert.True(root.LeafSpan == 9,
                "根 LeafSpan 应等于叶子列数 9，实测 " + root.LeafSpan);

            int childSum = 0;
            foreach (ColumnHeaderNode group in root.Children)
            {
                int groupChildSum = 0;
                foreach (ColumnHeaderNode leaf in group.Children)
                {
                    groupChildSum += leaf.LeafSpan;
                }
                Assert.True(groupChildSum == group.LeafSpan,
                    "组 \"" + group.Key + "\" 子节点 LeafSpan 之和 " + groupChildSum
                    + " 应等于自身 " + group.LeafSpan);
                childSum += group.LeafSpan;
            }
            Assert.True(childSum == root.LeafSpan,
                "根的子组 LeafSpan 之和 " + childSum + " 应等于根 " + root.LeafSpan);
            Assert.True(layout.Header.LeafBounds.Count == 10,
                "9 列应有 10 条列边界，实测 " + layout.Header.LeafBounds.Count);

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                IReadOnlyList<double> lines = probe.StrongHorizontalLines(
                    layout.TableHeaderRect.Top - 1f, layout.TableHeaderRect.Bottom + 1f,
                    layout.TableHeaderRect.Left + 2f, layout.TableHeaderRect.Right - 2f, 0.6);
                Assert.True(lines.Count >= 4,
                    "三级表头应至少有 4 条层边界，实测 " + lines.Count);
                AssertInk(probe, layout.Header.RectOf("root"), "根层表头");
                AssertInk(probe, layout.Header.RectOf("vitals"), "生命体征组");
                AssertInk(probe, layout.Header.RectOf("balance"), "出入量组");
            }
        }

        [Fact]
        public void Icu斜线格不写字而相邻格有字()
        {
            IcuNursingDocument doc = IcuDocument();
            IcuNursingTemplate template = new IcuNursingTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            IcuNursingLayout layout = geometry.LayoutOf<IcuNursingLayout>()!;

            ColumnHeaderNode diagonal = layout.Header.FindNode(IcuNursingColumns.CentralVenousPressure);
            Assert.True(diagonal.Diagonal,
                "中心静脉压叶子的 Diagonal 标志应为 true");
            Assert.True(diagonal.DiagonalDirection == DiagonalDirection.UpperLeftToLowerRight,
                "斜线方向应为左上→右下，实测 " + diagonal.DiagonalDirection);

            SKRect diagonalRect = layout.Header.RectOf(diagonal);
            SKRect neighbor = layout.Header.RectOf(IcuNursingColumns.Diastolic);

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                int offDiagonal = InkOffDiagonal(probe, diagonalRect, 3.5f);
                Assert.True(offDiagonal == 0,
                    "斜线格内除斜线外不应有墨迹，实测离斜线墨点 " + offDiagonal + " 个");

                int neighborInk = InkOffDiagonal(probe, neighbor, 3.5f);
                Assert.True(neighborInk > 0,
                    "相邻舒张压格应有文字墨迹（正对照），实测离斜线墨点 " + neighborInk + " 个");

                AssertInk(probe,
                    new SKRect(diagonalRect.MidX - 2f, diagonalRect.MidY - 2f,
                        diagonalRect.MidX + 2f, diagonalRect.MidY + 2f),
                    "斜线格中点的对角斜线");
            }
        }

        [Fact]
        public void Icu侧挂子表行距独立于主表()
        {
            IcuNursingDocument doc = IcuDocument();
            IcuNursingTemplate template = new IcuNursingTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            IcuNursingLayout layout = geometry.LayoutOf<IcuNursingLayout>()!;

            Assert.True(Math.Abs(layout.SideTable.RowHeight - layout.MainRows[0].Height) > 1f,
                "侧表行高 " + layout.SideTable.RowHeight.ToString("0.#") + " 应不同于主表行高 "
                + layout.MainRows[0].Height.ToString("0.#"));
            Assert.True(Math.Abs(layout.SideRowRect(0).Top - layout.MainRows[0].Top) < 0.01f,
                "侧表应与主表首行同顶，实测侧 " + layout.SideRowRect(0).Top.ToString("0.##")
                + " 主 " + layout.MainRows[0].Top.ToString("0.##"));
            Assert.True(Math.Abs(layout.SideRowRect(0).Bottom - layout.MainRows[0].Bottom) > 1f,
                "侧表首行下缘不应与主表首行对齐，实测差 "
                + Math.Abs(layout.SideRowRect(0).Bottom - layout.MainRows[0].Bottom).ToString("0.##") + "px");
            Assert.True(Math.Abs(layout.SideRowRect(1).Top - layout.MainRows[1].Top) > 1f,
                "侧表第二行顶不应与主表第二行对齐，实测侧 " + layout.SideRowRect(1).Top.ToString("0.##")
                + " 主 " + layout.MainRows[1].Top.ToString("0.##"));

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                AssertInk(probe, layout.SideRowRect(0), "侧表首行（交班小结）");
                AssertInk(probe, layout.MainRows[0], "主表首行（正对照）");

                IReadOnlyList<double> sideLines = probe.StrongHorizontalLines(
                    layout.SidePlaced.Top - 1f, layout.SidePlaced.Bottom,
                    layout.SidePlaced.Left + 2f, layout.SidePlaced.Right - 2f, 0.95);
                Assert.True(sideLines.Count >= 5,
                    "侧表 4 行应有 5 条横边界，实测 " + sideLines.Count + "；线心 "
                    + string.Join(",", sideLines));
                foreach (double gap in ImageProbe.Gaps(sideLines))
                {
                    Assert.True(Math.Abs(gap - 30d) <= 1.5,
                        "侧表行距应为 30px，实测 " + gap.ToString("0.##") + "；全部线心 "
                        + string.Join(",", sideLines) + "，子表 " + Describe(layout.SidePlaced));
                }
            }
        }

        [Fact]
        public void Icu主表行高由令牌驱动而侧表不跟随()
        {
            IcuNursingDocument doc = IcuDocument();
            IcuNursingTemplate template = new IcuNursingTemplate();
            ChartGeometry first;
            byte[] firstPng = Render(doc, template, out first);
            IcuNursingLayout before = first.LayoutOf<IcuNursingLayout>()!;
            float beforeTop = before.MainRows[1].Top;
            float beforeSideTop = before.SideRowRect(1).Top;

            template.Tokens.DataRowHeight = 32f;
            ChartGeometry second;
            byte[] secondPng = Render(doc, template, out second);
            IcuNursingLayout after = second.LayoutOf<IcuNursingLayout>()!;

            Assert.True(Math.Abs((after.MainRows[1].Top - beforeTop) - 8f) < 0.01f,
                "DataRowHeight 24→32 后第二行应下移 8px，实测 "
                + (after.MainRows[1].Top - beforeTop).ToString("0.##"));
            Assert.True(Math.Abs(after.SideRowRect(1).Top - beforeSideTop) < 0.01f,
                "主表行高改变不应影响侧表（独立行距），实测侧表位移 "
                + (after.SideRowRect(1).Top - beforeSideTop).ToString("0.##"));
            AssertRenderedChange(firstPng, secondPng, "ICU DataRowHeight");
        }

        // ================================================================== T273 预防接种证

        [Fact]
        public void 接种证印序是排列且开面配对逐条符合规范()
        {
            BookletImposition imposition = BookletImposition.SaddleStitch(12);

            List<int> orders = new List<int>();
            for (int page = 1; page <= 12; page++)
            {
                orders.Add(imposition.PrintOrderOf(page));
            }
            orders.Sort();
            for (int i = 0; i < 12; i++)
            {
                Assert.True(orders[i] == i + 1,
                    "印刷序应构成 1..12 的排列，第 " + i + " 位实测 " + orders[i]);
            }

            int[] expectedFlat = { 12, 1, 2, 11, 10, 3, 4, 9, 8, 5, 6, 7 };
            IReadOnlyList<int> flat = imposition.FlattenedPrintOrder;
            Assert.True(flat.Count == expectedFlat.Length,
                "12 页应有 12 个印刷页位，实测 " + flat.Count);
            for (int i = 0; i < expectedFlat.Length; i++)
            {
                Assert.True(flat[i] == expectedFlat[i],
                    "印刷页序第 " + i + " 位应为 " + expectedFlat[i] + "，实测 " + flat[i]);
            }

            AssertPair(imposition, 12, 1);
            AssertPair(imposition, 2, 11);
            AssertPair(imposition, 3, 10);
            AssertPair(imposition, 4, 9);
            AssertPair(imposition, 5, 8);
            AssertPair(imposition, 6, 7);

            Assert.True(imposition.PrintOrderOf(3) == 6,
                "记录一（页 3）应在印刷序第 6 位，实测 " + imposition.PrintOrderOf(3));
        }

        private static void AssertPair(BookletImposition imposition, int a, int b)
        {
            foreach (Spread spread in imposition.Spreads)
            {
                bool match = (spread.LeftLogicalPage == a && spread.RightLogicalPage == b)
                             || (spread.LeftLogicalPage == b && spread.RightLogicalPage == a);
                if (match)
                {
                    return;
                }
            }
            Assert.Fail("逻辑页 " + a + " 与 " + b + " 应在同一开面，但配对表中没有这一对");
        }

        [Fact]
        public void 接种证幅面为210乘145毫米且开面为两页()
        {
            VaccinationBookletDocument doc = VaccinationDocument();
            VaccinationBookletTemplate template = new VaccinationBookletTemplate();
            ChartGeometry geometry;
            Render(doc, template, out geometry);
            VaccinationBookletLayout layout = geometry.LayoutOf<VaccinationBookletLayout>()!;

            float expectedWidth = Unit.Mm(210f);
            float expectedHeight = Unit.Mm(145f);
            Assert.True(Math.Abs(layout.LeftRect.Width - expectedWidth) < 0.01f,
                "单页宽应为 210mm = " + expectedWidth.ToString("0.###") + "px，实测 "
                + layout.LeftRect.Width.ToString("0.###"));
            Assert.True(Math.Abs(layout.LeftRect.Height - expectedHeight) < 0.01f,
                "单页高应为 145mm = " + expectedHeight.ToString("0.###") + "px，实测 "
                + layout.LeftRect.Height.ToString("0.###"));
            Assert.True(Math.Abs(layout.Bounds.Width - expectedWidth * 2f) < 0.01f,
                "开面宽应为两页 = " + (expectedWidth * 2f).ToString("0.###") + "px，实测 "
                + layout.Bounds.Width.ToString("0.###"));
            Assert.True(layout.LeftPage == 2 && layout.RightPage == 11,
                "默认开面应为 (2,11)：封二 + 基本信息，实测 ("
                + layout.LeftPage + "," + layout.RightPage + ")");
        }

        [Fact]
        public void 接种证预印行数量等于国家免疫规划剂次表长度()
        {
            VaccinationBookletDocument doc = VaccinationDocument();
            doc.SpreadIndex = 2;
            VaccinationBookletTemplate template = new VaccinationBookletTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            VaccinationBookletLayout layout = geometry.LayoutOf<VaccinationBookletLayout>()!;

            Assert.True(layout.ScheduleRows.Count == 22,
                "国家免疫规划剂次表应为 22 行，文档实测 " + layout.ScheduleRows.Count);
            Assert.True(layout.ScheduleRows.Count == doc.NationalSchedule.Count,
                "预印行数应与文档表长一致：版面 " + layout.ScheduleRows.Count
                + " 文档 " + doc.NationalSchedule.Count);

            int pageCount = 0;
            for (int i = 0; i < layout.ScheduleRows.Count; i++)
            {
                int page = layout.PageOfRow(i);
                Assert.True(page == 3 || page == 10 || page == 4 || page == 9 || page == 5 || page == 8,
                    "第 " + i + " 行落在非记录页 " + page);
                pageCount++;
            }
            Assert.True(pageCount == 22, "应有 22 行映射到记录页，实测 " + pageCount);

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                Assert.True(layout.PageOfRow(0) == 3 && layout.PageOfRow(3) == 3
                        && layout.PageOfRow(4) == 10 && layout.PageOfRow(7) == 10,
                    "本开面应承载第 1–4 行（页 3）与第 5–8 行（页 10），实测页号 "
                    + layout.PageOfRow(0) + "," + layout.PageOfRow(3) + ","
                    + layout.PageOfRow(4) + "," + layout.PageOfRow(7));
                AssertInk(probe, layout.ScheduleRowRect(0), "第一行预印疫苗（乙肝 1）");
                AssertInk(probe, layout.ScheduleRowRect(3), "页 3 第四行预印疫苗");
                AssertInk(probe, layout.ScheduleRowRect(4), "页 10 第一行预印疫苗（第 5 行）");
                AssertInk(probe, layout.ScheduleRowRect(7), "页 10 第四行预印疫苗（第 8 行）");
            }
        }

        [Fact]
        public void 接种证跨开面不重复表头()
        {
            VaccinationBookletDocument doc = VaccinationDocument();
            doc.SpreadIndex = 2;
            VaccinationBookletTemplate template = new VaccinationBookletTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            VaccinationBookletLayout layout = geometry.LayoutOf<VaccinationBookletLayout>()!;

            Assert.True(layout.LeftPage == 10 && layout.RightPage == 3,
                "开面 2 应为 (10,3)，实测 (" + layout.LeftPage + "," + layout.RightPage + ")");

            SKRect firstHeader = layout.TableHeaderRect(3);
            SKRect continuationRows = layout.RecordRowsRect(10);

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                AssertInk(probe, firstHeader, "记录首页（页 3）表头");
                AssertNoInk(probe, layout.TableHeaderRect(10),
                    "续页（页 10）不应重复表头");

                AssertInk(probe, new SKRect(continuationRows.Left - 1f, continuationRows.Top + 2f,
                    continuationRows.Left + 1f, continuationRows.Top + 20f), "续页表格左边框");
                float midSegment = continuationRows.Top + continuationRows.Height * 0.3f;
                AssertInk(probe, new SKRect(continuationRows.Left - 1f, midSegment - 8f,
                    continuationRows.Left + 1f, midSegment + 8f), "续页表格左边框中段");
                AssertInk(probe, new SKRect(layout.LeftRect.Left + 20f, layout.LeftRect.Top + 20f,
                    layout.LeftRect.Right - 20f, layout.LeftRect.Top + 45f),
                    "续页标题（正对照：页面有内容，只是没有表头）");
            }
        }

        [Fact]
        public void 接种证锁定令牌拒绝字段增删()
        {
            VaccinationBookletDocument doc = VaccinationDocument();
            VaccinationBookletTemplate template = new VaccinationBookletTemplate();
            Assert.True(template.Tokens.AllowLayoutDeviation == false,
                "默认必须是 AllowLayoutDeviation=false（规范要求不得自行变更加印）");

            int before = doc.Patient.ExtraFields.Count;
            InvalidOperationException addError = Assert.Throws<InvalidOperationException>(
                () => template.AddField(doc, "guardian", "监护人", "李大军"));
            Assert.True(addError.Message.Contains("AllowLayoutDeviation"),
                "增字段异常应点明 AllowLayoutDeviation，实测：" + addError.Message);
            Assert.True(doc.Patient.ExtraFields.Count == before,
                "拒绝后字段数不应变化，实测 " + doc.Patient.ExtraFields.Count + " vs " + before);

            Assert.Throws<InvalidOperationException>(() => template.RemoveField(doc, "guardian"));

            template.Tokens.AllowLayoutDeviation = true;
            HeaderField field = template.AddField(doc, "guardian", "监护人", "李大军");
            Assert.True(field.Key == "guardian" && doc.Patient.ExtraFields.Count == before + 1,
                "放开锁定后应能增加字段，实测字段数 " + doc.Patient.ExtraFields.Count);
            Assert.True(template.RemoveField(doc, "guardian"),
                "放开锁定后应能删除刚加的字段");
            Assert.True(doc.Patient.ExtraFields.Count == before,
                "删除后字段数应回到 " + before + "，实测 " + doc.Patient.ExtraFields.Count);
        }

        [Fact]
        public void 接种证专色保留CMYK原值并以近似RGB呈现()
        {
            VaccinationBookletDocument doc = VaccinationDocument();
            Assert.True(doc.SpotColorCmyk == "C85 M30 Y65 K65",
                "文档应保留原始 CMYK 值，实测 \"" + doc.SpotColorCmyk + "\"");

            VaccinationBookletTemplate template = new VaccinationBookletTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            VaccinationBookletLayout layout = geometry.LayoutOf<VaccinationBookletLayout>()!;
            SKRect swatch = layout.SpotSwatchRect;
            Assert.True(!swatch.IsEmpty, "基本信息页应画出专色色块，实测矩形为空");

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                SKColor ink = probe.AverageInkColor(swatch);
                Assert.True(ink.Red < 40 && ink.Green > 45 && ink.Green < 85 && ink.Blue > 40 && ink.Blue < 80,
                    "专色近似 RGB 应约为 (13,63,58)，实测 (" + ink.Red + "," + ink.Green + "," + ink.Blue + ")");
            }
        }

        [Fact]
        public void 接种证记录行高由令牌驱动()
        {
            VaccinationBookletDocument doc = VaccinationDocument();
            doc.SpreadIndex = 2;
            VaccinationBookletTemplate template = new VaccinationBookletTemplate();
            ChartGeometry first;
            byte[] firstPng = Render(doc, template, out first);
            VaccinationBookletLayout before = first.LayoutOf<VaccinationBookletLayout>()!;
            float beforeHeight = before.Bounds.Height;

            // 默认铺满：4 行均摊数据行区，最后一行下缘与表框底边重合，整页不再七成空白。
            SKRect fillArea = before.RecordRowsRect(3);
            Assert.True(Math.Abs(before.RecordRowHeight * 4f - fillArea.Height) < 0.01f,
                "铺满时行高应为数据行区高/4 = " + (fillArea.Height / 4f).ToString("0.##")
                + "，实测 " + before.RecordRowHeight.ToString("0.##"));
            Assert.True(Math.Abs(before.ScheduleRowRect(3).Bottom - fillArea.Bottom) < 0.01f,
                "第 4 行下缘应贴到表框底边 " + fillArea.Bottom.ToString("0.##")
                + "，实测 " + before.ScheduleRowRect(3).Bottom.ToString("0.##"));
            Assert.True(fillArea.Height > before.Bounds.Height * 0.6f,
                "记录表数据区应占页高一半以上（消除七成空白），实测 "
                + fillArea.Height.ToString("0.##") + " / 页高 " + before.Bounds.Height.ToString("0.##"));

            // 关掉铺满：回到令牌定高，第二行随 RecordRowHeight 变化同量位移。
            template.Tokens.FillRecordPage = false;
            ChartGeometry fixedHeight;
            byte[] fixedPng = Render(doc, template, out fixedHeight);
            VaccinationBookletLayout fixedLayout = fixedHeight.LayoutOf<VaccinationBookletLayout>()!;
            Assert.True(Math.Abs(fixedLayout.RecordRowHeight - 18f) < 0.01f,
                "FillRecordPage=false 时应回到令牌定高 18，实测 " + fixedLayout.RecordRowHeight.ToString("0.##"));
            float fixedTop = fixedLayout.ScheduleRowRect(1).Top;

            template.Tokens.RecordRowHeight = 24f;
            ChartGeometry second;
            byte[] secondPng = Render(doc, template, out second);
            VaccinationBookletLayout after = second.LayoutOf<VaccinationBookletLayout>()!;

            Assert.True(Math.Abs((after.ScheduleRowRect(1).Top - fixedTop) - 6f) < 0.01f,
                "RecordRowHeight 18→24 后第二行应下移 6px，实测 "
                + (after.ScheduleRowRect(1).Top - fixedTop).ToString("0.##"));
            Assert.True(Math.Abs(after.Bounds.Height - beforeHeight) < 0.01f,
                "mm 幅面固定，行高变化不应改页面尺寸，实测差 "
                + (after.Bounds.Height - beforeHeight).ToString("0.##"));
            AssertRenderedChange(fixedPng, secondPng, "接种证 RecordRowHeight");
            AssertRenderedChange(firstPng, fixedPng, "接种证 FillRecordPage");
        }

        // ================================================================== T274 药敏

        [Fact]
        public void 药敏RangeBar是WeightedCategoryAxis实例且段宽不等()
        {
            AstReportDocument doc = AstDocument();
            AstReportTemplate template = new AstReportTemplate();
            ChartGeometry geometry;
            Render(doc, template, out geometry);
            AstReportLayout layout = geometry.LayoutOf<AstReportLayout>()!;

            Assert.True(layout.RangeBar is WeightedCategoryAxis,
                "RangeBar 必须是 WeightedCategoryAxis 的直接实例，实测 " + layout.RangeBar.GetType().Name);
            Assert.True(layout.RangeBar.Count == 4, "RangeBar 应有 4 段，实测 " + layout.RangeBar.Count);

            float w0 = layout.RangeBar.WidthOf(0);
            float w1 = layout.RangeBar.WidthOf(1);
            Assert.True(w1 > w0 + 1f,
                "SDD 段宽应大于 S 段（权重 1.5 vs 1），实测 " + w1.ToString("0.##") + " vs " + w0.ToString("0.##"));
            Assert.True(Math.Abs(w1 / w0 - 1.5f) < 0.01f,
                "SDD/S 段宽比应为 1.5，实测 " + (w1 / w0).ToString("0.####"));

            float[] expected = { 0.2f, 0.5f, 0.7f, 1.0f };
            for (int i = 1; i <= 4; i++)
            {
                // 段边界按累计权重：S 1、SDD 1.5、I 1、R 1.5，总 5。
                float actual = layout.RangeBar.PositionOf(Math.Min(i, 3));
                if (i == 4)
                {
                    actual = layout.RangeBar.To;
                }
                float target = layout.RangeBar.From + expected[i - 1] * (layout.RangeBar.To - layout.RangeBar.From);
                Assert.True(Math.Abs(actual - target) < 0.02f,
                    "第 " + i + " 个段边界应在 " + expected[i - 1].ToString("0.##") + "×长度处（"
                    + target.ToString("0.###") + "），实测 " + actual.ToString("0.###"));
            }

            Assert.True(layout.RangeBar.IndexAt(layout.RangeBar.CenterOf(3)) == 3,
                "R 段中心应反查为第 3 段，实测 " + layout.RangeBar.IndexAt(layout.RangeBar.CenterOf(3)));
        }

        [Fact]
        public void 药敏耐药段落红墨而敏感行不落红()
        {
            AstReportDocument doc = AstDocument();
            AstReportTemplate template = new AstReportTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            AstReportLayout layout = geometry.LayoutOf<AstReportLayout>()!;

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                SKRect resistant = layout.SegmentRect(0, 3);
                Assert.True(probe.HasRedInBox((int)resistant.Left + 1, (int)resistant.Top + 1,
                        (int)resistant.Right - 1, (int)resistant.Bottom - 1),
                    "第 0 行判读 R，R 段应落红墨");

                SKRect sensitiveRowR = layout.SegmentRect(1, 3);
                Assert.True(!probe.HasRedInBox((int)sensitiveRowR.Left + 1, (int)sensitiveRowR.Top + 1,
                        (int)sensitiveRowR.Right - 1, (int)sensitiveRowR.Bottom - 1),
                    "第 1 行判读 S，不应在 R 段落红墨（正对照）");

                SKRect sensitive = layout.SegmentRect(1, 0);
                SKColor ink = probe.AverageInkColor(sensitive);
                Assert.True(ink.Green > ink.Red + 20 && ink.Green > ink.Blue + 20,
                    "S 段应为绿色，实测 RGB=(" + ink.Red + "," + ink.Green + "," + ink.Blue + ")");
            }
        }

        [Fact]
        public void 药敏判读结果落在结果列()
        {
            AstReportDocument doc = AstDocument();
            AstReportTemplate template = new AstReportTemplate();
            ChartGeometry geometry;
            byte[] png = Render(doc, template, out geometry);
            AstReportLayout layout = geometry.LayoutOf<AstReportLayout>()!;

            using (ImageProbe probe = ImageProbe.FromPng(png))
            {
                for (int row = 0; row < 4; row++)
                {
                    AssertInk(probe, layout.ResultRects[row],
                        "第 " + row + " 行 MIC/判读结果列");
                    AssertInk(probe, layout.LabelRects[row], "第 " + row + " 行药名列");
                }
            }
        }

        [Fact]
        public void 药敏行高由令牌驱动()
        {
            AstReportDocument doc = AstDocument();
            AstReportTemplate template = new AstReportTemplate();
            ChartGeometry first;
            byte[] firstPng = Render(doc, template, out first);
            AstReportLayout before = first.LayoutOf<AstReportLayout>()!;
            float beforeTop = before.RowRects[1].Top;
            float beforeHeight = before.Bounds.Height;

            template.Tokens.RowHeight = 36f;
            ChartGeometry second;
            byte[] secondPng = Render(doc, template, out second);
            AstReportLayout after = second.LayoutOf<AstReportLayout>()!;

            Assert.True(Math.Abs((after.RowRects[1].Top - beforeTop) - 8f) < 0.01f,
                "RowHeight 28→36 后第二行应下移 8px，实测 "
                + (after.RowRects[1].Top - beforeTop).ToString("0.##"));
            Assert.True(Math.Abs((after.Bounds.Height - beforeHeight) - 32f) < 0.01f,
                "4 行各增 8px，版面应增高 32px，实测 "
                + (after.Bounds.Height - beforeHeight).ToString("0.##"));
            AssertRenderedChange(firstPng, secondPng, "药敏 RowHeight");
        }






        private static int CountLabel(IReadOnlyList<KeyValuePair<string, string>> fields, string label)
        {
            int count = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].Key, label, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static string ValueOf(IReadOnlyList<KeyValuePair<string, string>> fields, string label)
        {
            for (int i = 0; i < fields.Count; i++)
            {
                if (string.Equals(fields[i].Key, label, StringComparison.Ordinal))
                {
                    return fields[i].Value;
                }
            }
            return string.Empty;
        }

        private static int CountText(string text, string needle)
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
    }
}
