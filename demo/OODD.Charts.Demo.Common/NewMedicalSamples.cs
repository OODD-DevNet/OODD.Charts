using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Forms.AnesthesiaRecord;
using OODD.Charts.Medical.Forms.AstReport;
using OODD.Charts.Medical.Forms.DeathCertificate;
using OODD.Charts.Medical.Forms.IcuNursing;
using OODD.Charts.Medical.Forms.VaccinationBooklet;
using OODD.Charts.Model;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>
    /// 医疗域 E5 / T290 的 5 张补演示样例。
    /// <para>
    /// 儿童生长百分位曲线只是普通百分位折线图，通用图表控件即可实现，按库定位剔除。
    /// 本文件里的全部人名、机构、编号、检验值与版面数值都是<b>虚构的示例数据</b>，
    /// <b>示例数据，非真实机型/标准值</b>；不代表任何校准后的纸面版式或临床判读标准。
    /// </para>
    /// <para>
    /// 每张表单除 <see cref="All"/> 的建图工厂外，还提供 <see cref="Verify"/>：
    /// 按样例名做版面结构断言并输出带实测值的通过/失败日志，返回失败计数。
    /// </para>
    /// </summary>
    public static class NewMedicalSamples
    {
        /// <summary>演示登记项（由主代理接进 DemoCatalog 与样例工具分发）。</summary>
        public static IReadOnlyList<DemoSample> All { get; } = new List<DemoSample>
        {
            new DemoSample("anesthesia-record", "麻醉记录单",
                () => new Chart(BuildAnesthesiaRecord(), new AnesthesiaRecordTemplate(), new ChartStyle())),
            new DemoSample("death-certificate", "居民死亡医学证明（四联）",
                () => new Chart(BuildDeathCertificate(), new DeathCertificateTemplate(), new ChartStyle())),
            new DemoSample("icu-nursing", "ICU 护理记录单",
                () => new Chart(BuildIcuNursing(), new IcuNursingTemplate(), new ChartStyle())),
            new DemoSample("vaccination-booklet", "预防接种证",
                () => new Chart(BuildVaccinationBooklet(), new VaccinationBookletTemplate(), new ChartStyle())),
            new DemoSample("ast-report", "药敏报告单",
                () => new Chart(BuildAstReport(), new AstReportTemplate(), new ChartStyle())),
        }.AsReadOnly();

        // ================================================================ 建图工厂

        /// <summary>
        /// 麻醉记录单：总时长 150 min、10 个时点（≥6），收缩压/舒张压两条血压曲线，
        /// 2 条持续用药时段带、4 条成对事件。示例数据，非真实机型/标准值。
        /// </summary>
        private static AnesthesiaRecordDocument BuildAnesthesiaRecord()
        {
            AnesthesiaRecordDocument doc = new AnesthesiaRecordDocument
            {
                HospitalName = "示例医院（虚构）",
                FormTitle = "麻醉记录单",
                HeaderRightNote = "共 1 页　第 1 页",
                TotalMinutes = 150,
            };

            doc.Patient.Name = "示例患者甲";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "45";
            doc.Patient.Department = "示例外科";
            doc.Patient.Ward = "示例病区";
            doc.Patient.BedNumber = "示例床 08";
            doc.Patient.AdmissionNumber = "2026090101";
            doc.Patient.AdmissionDate = "2026-09-01";
            doc.Patient.ExtraFields.Add(new HeaderField("asa", "ASA 分级", "II 级（示例）"));

            doc.Operation.Procedure = "示例术式（虚构）";
            doc.Operation.AnesthesiaType = "全身麻醉（示例）";
            doc.Operation.Anesthetist = "示例医师";
            doc.Operation.StartTime = "08:00";
            doc.Operation.EndTime = "10:30";

            // 时点、收缩压、舒张压、平均压、脉搏、体温、SpO2、EtCO2 —— 全部为虚构示例值。
            AddPoint(doc, 0d, 128d, 76d, 93d, 82d, 36.6d, 98d, 34d);
            AddPoint(doc, 15d, 118d, 70d, 86d, 76d, 36.4d, 99d, 35d);
            AddPoint(doc, 30d, 105d, 62d, 76d, 68d, 36.3d, 99d, 36d);
            AddPoint(doc, 45d, 112d, 66d, 81d, 72d, 36.5d, 98d, 37d);
            AddPoint(doc, 60d, 124d, 74d, 91d, 80d, 36.7d, 98d, 36d);
            AddPoint(doc, 75d, 132d, 80d, 97d, 86d, 36.8d, 97d, 35d);
            AddPoint(doc, 90d, 120d, 72d, 88d, 78d, 36.9d, 98d, 35d);
            AddPoint(doc, 105d, 116d, 70d, 85d, 76d, 37.0d, 98d, 34d);
            AddPoint(doc, 120d, 126d, 78d, 94d, 82d, 36.8d, 99d, 35d);
            AddPoint(doc, 150d, 122d, 74d, 90d, 78d, 36.7d, 98d, 34d);

            doc.ContinuousMedications.Add(new ContinuousMedication("示例镇静药", 5d, 140d, "6 mg/kg/h（示例）"));
            doc.ContinuousMedications.Add(new ContinuousMedication("示例镇痛药", 10d, 145d, "0.15 μg/kg/min（示例）"));

            doc.Events.Add(new AnesthesiaEvent(10d, 30d, "BP", 1, 0));
            doc.Events.Add(new AnesthesiaEvent(45d, 75d, "HR", 2, 6));
            doc.Events.Add(new AnesthesiaEvent(90d, 120d, "TR", 3, 1));
            doc.Events.Add(new AnesthesiaEvent(120d, 150d, "END", 4, 2));

            return doc;
        }

        private static void AddPoint(AnesthesiaRecordDocument doc, double time, double systolic,
            double diastolic, double mean, double pulse, double temperature, double spo2, double etco2)
        {
            doc.Points.Add(new AnesthesiaPoint(time, systolic, diastolic, mean, pulse, temperature, spo2, etco2));
        }

        /// <summary>
        /// 居民死亡医学证明（四联）：默认四联（单位存根 / 公安 / 近亲属 / 殡仪馆），
        /// 普通字段与死因链 I.(a)–(d) 全部给出虚构示例值。示例数据，非真实机型/标准值。
        /// </summary>
        private static DeathCertificateDocument BuildDeathCertificate()
        {
            DeathCertificateDocument doc = new DeathCertificateDocument
            {
                HospitalName = "示例医院（虚构）",
                FormTitle = "居民死亡医学证明（四联）",
                HeaderRightNote = "共 1 页　第 1 页",
                AdminCode = "000000",
                SerialNumber = "20260000000000001",
            };

            doc.Patient.Name = "示例患者乙";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "72";
            doc.Patient.Department = "示例内科";
            doc.Patient.Ward = "示例病区";
            doc.Patient.BedNumber = "示例床 12";
            doc.Patient.AdmissionNumber = "000000194001010000";
            doc.Patient.AdmissionDate = "2026-09-30";

            doc.Set(DeathCertificateFields.IdNumber, "000000194001010000");
            doc.Set(DeathCertificateFields.DeathDate, "2026-09-30");
            doc.Set(DeathCertificateFields.DeathPlace, "示例市示例区示例医院（虚构）");
            doc.Set(DeathCertificateFields.Household, "示例市示例区示例路 0 号");
            doc.Set(DeathCertificateFields.Phone, "000-0000-0000");
            doc.Set(DeathCertificateFields.Reporter, "示例填报人");
            doc.Set(DeathCertificateFields.FuneralHotline, "000-0000");
            doc.Set(DeathCertificateFields.CauseA, "示例直接死因");
            doc.Set(DeathCertificateFields.CauseB, "示例基础疾病");
            doc.Set(DeathCertificateFields.CauseC, "示例既往疾病");
            doc.Set(DeathCertificateFields.CauseD, "示例其他疾病");

            return doc;
        }

        /// <summary>
        /// ICU 护理记录单：9 列三级表头（含"中心静脉压"斜线叶）、6 个时点全列有值、
        /// 右侧交班小结 4 行都有值。示例数据，非真实机型/标准值。
        /// </summary>
        private static IcuNursingDocument BuildIcuNursing()
        {
            IcuNursingDocument doc = new IcuNursingDocument
            {
                HospitalName = "示例医院（虚构）",
                FormTitle = "ICU 护理记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "示例患者丙";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "63";
            doc.Patient.Department = "重症医学科（示例）";
            doc.Patient.Ward = "示例 ICU";
            doc.Patient.BedNumber = "ICU-03";
            doc.Patient.AdmissionNumber = "2026090203";
            doc.Patient.AdmissionDate = "2026-09-02";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "示例诊断（虚构）"));

            string[] labels = { "06:00", "07:00", "08:00", "09:00", "10:00", "11:00" };
            string[][] values =
            {
                new[] { "88", "18", "36.8", "126", "74", "8", "500", "320", "240" },
                new[] { "92", "19", "37.1", "132", "78", "9", "450", "300", "210" },
                new[] { "96", "20", "37.4", "138", "82", "10", "600", "350", "260" },
                new[] { "90", "18", "37.2", "128", "76", "9", "550", "310", "230" },
                new[] { "86", "17", "36.9", "120", "70", "8", "400", "280", "190" },
                new[] { "94", "19", "37.0", "130", "76", "9", "500", "330", "250" },
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

            doc.HandoverNotes.Add("白班：平稳");
            doc.HandoverNotes.Add("夜班：尿少");
            doc.HandoverNotes.Add("气道护理已做");
            doc.HandoverNotes.Add("已通知医师");

            return doc;
        }

        /// <summary>
        /// 预防接种证：12 逻辑页鞍装，取开面 2 = (10,3) 两个记录页；
        /// 前 8 行预印剂次均有接种记录（≥6 剂次）。示例数据，非真实机型/标准值。
        /// </summary>
        private static VaccinationBookletDocument BuildVaccinationBooklet()
        {
            VaccinationBookletDocument doc = new VaccinationBookletDocument
            {
                HospitalName = "示例社区卫生服务中心（虚构）",
                FormTitle = "预防接种证",
                TotalLogicalPages = 12,
                SpreadIndex = 2,
            };

            doc.Patient.Name = "示例儿童";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "4 岁";
            doc.Patient.Department = "预防保健科（示例）";
            doc.Patient.AdmissionNumber = "示例建档号 0001";
            doc.Patient.AdmissionDate = "2022-03-04";
            doc.Patient.ExtraFields.Add(new HeaderField("guardian", "监护人", "示例监护人"));

            doc.Records.Add(new VaccinationRecord("乙肝疫苗", "1", "2022-03-04", "示例生物", "20220301"));
            doc.Records.Add(new VaccinationRecord("乙肝疫苗", "2", "2022-04-04", "示例生物", "20220402"));
            doc.Records.Add(new VaccinationRecord("乙肝疫苗", "3", "2022-09-04", "示例生物", "20220903"));
            doc.Records.Add(new VaccinationRecord("卡介苗", "1", "2022-03-04", "示例生物", "20220302"));
            doc.Records.Add(new VaccinationRecord("脊灰疫苗", "1", "2022-03-04", "示例生物", "20220303"));
            doc.Records.Add(new VaccinationRecord("脊灰疫苗", "2", "2022-04-04", "示例生物", "20220404"));
            doc.Records.Add(new VaccinationRecord("脊灰疫苗", "3", "2022-05-04", "示例生物", "20220505"));
            doc.Records.Add(new VaccinationRecord("脊灰疫苗", "4", "2022-09-04", "示例生物", "20220906"));

            doc.Assessments.Add("已完成示例剂次接种，留观无异常。");

            return doc;
        }

        /// <summary>
        /// 药敏报告单：6 种抗菌药覆盖 S / SDD / I / R 四类判读（≥4 药、≥3 类）。
        /// 药名与 MIC 均为虚构示例值，非真实机型/标准值。
        /// </summary>
        private static AstReportDocument BuildAstReport()
        {
            AstReportDocument doc = new AstReportDocument
            {
                HospitalName = "示例医院（虚构）",
                FormTitle = "药敏报告单",
                HeaderRightNote = "共 1 页　第 1 页",
                Specimen = "痰（示例标本）",
                Organism = "示例肠杆菌（虚构）",
            };

            doc.Patient.Name = "示例患者丁";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "58";
            doc.Patient.Department = "呼吸内科（示例）";
            doc.Patient.Ward = "示例病区";
            doc.Patient.BedNumber = "示例床 21";
            doc.Patient.AdmissionNumber = "2026091501";
            doc.Patient.AdmissionDate = "2026-09-12";
            doc.Patient.ExtraFields.Add(new HeaderField("specimen-no", "标本号", "示例 S-0001"));

            doc.Antibiotics.Add(new AstAntibiotic("示例培南类", "1", "R"));
            doc.Antibiotics.Add(new AstAntibiotic("示例青霉素类", "8", "S"));
            doc.Antibiotics.Add(new AstAntibiotic("示例头孢类", "2", "SDD"));
            doc.Antibiotics.Add(new AstAntibiotic("示例喹诺酮类", "4", "I"));
            doc.Antibiotics.Add(new AstAntibiotic("示例氨基糖苷类", "2", "S"));
            doc.Antibiotics.Add(new AstAntibiotic("示例酶抑制剂复方", "16", "I"));

            return doc;
        }




        // ================================================================ 结构断言

        /// <summary>
        /// 按样例名做结构断言。未知名称不报错、返回 0。
        /// 每条断言输出 <c>[name] 通过/失败: 说明 实到值</c>；返回失败条数。
        /// </summary>
        public static int Verify(string name, Chart chart)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }

            switch (name)
            {
                case "anesthesia-record":
                    return VerifyAnesthesiaRecord(name, chart);
                case "death-certificate":
                    return VerifyDeathCertificate(name, chart);
                case "icu-nursing":
                    return VerifyIcuNursing(name, chart);
                case "vaccination-booklet":
                    return VerifyVaccinationBooklet(name, chart);
                case "ast-report":
                    return VerifyAstReport(name, chart);
                default:
                    return 0;
            }
        }

        private static int VerifyAnesthesiaRecord(string name, Chart chart)
        {
            AnesthesiaRecordDocument doc = (AnesthesiaRecordDocument)chart.Document;
            AnesthesiaRecordLayout layout = chart.Geometry.LayoutOf<AnesthesiaRecordLayout>()
                ?? throw new InvalidOperationException("几何里没有 AnesthesiaRecordLayout。");
            int failures = 0;

            int pressurePairs = 0;
            foreach (AnesthesiaPoint point in doc.Points)
            {
                if (point.Systolic > point.Diastolic)
                {
                    pressurePairs++;
                }
            }
            failures += Check(name, doc.Points.Count >= 6 && pressurePairs == doc.Points.Count,
                "采样时点 " + doc.Points.Count + " 个（要求 ≥6），收缩/舒张压双曲线成对 "
                + pressurePairs + "/" + doc.Points.Count);

            failures += Check(name, doc.Events.Count >= 3 && layout.EventEnds.Count == doc.Events.Count,
                "成对事件 " + doc.Events.Count + " 条，求出端点 " + layout.EventEnds.Count + " 条，"
                + "持续用药带 " + layout.MedicationBands.Count + " 条");

            float lowLeft = layout.LeftAxis.PositionOf(20d);
            float lowRight = layout.RightAxis.PositionOf(12d);
            float highLeft = layout.LeftAxis.PositionOf(260d);
            float highRight = layout.RightAxis.PositionOf(40d);
            failures += Check(name,
                Math.Abs(lowLeft - lowRight) < 0.01f && Math.Abs(highLeft - highRight) < 0.01f
                && Math.Abs(lowLeft - layout.PlotRect.Bottom) < 0.01f,
                "左右轴共横线：左 20=" + F(lowLeft) + " / 右 12=" + F(lowRight)
                + "，左 260=" + F(highLeft) + " / 右 40=" + F(highRight)
                + "，下缘=" + F(layout.PlotRect.Bottom));

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            failures += Check(name, png.Length > 5000,
                "96dpi PNG 字节数 " + png.Length + "（要求 >5000），尺寸 "
                + F(layout.Bounds.Width) + "×" + F(layout.Bounds.Height));

            return failures;
        }

        private static int VerifyDeathCertificate(string name, Chart chart)
        {
            DeathCertificateDocument doc = (DeathCertificateDocument)chart.Document;
            DeathCertificateLayout layout = chart.Geometry.LayoutOf<DeathCertificateLayout>()
                ?? throw new InvalidOperationException("几何里没有 DeathCertificateLayout。");
            int failures = 0;

            failures += Check(name,
                layout.Spec.Count == 4 && Math.Abs(layout.Spec.TotalExtent - layout.Bounds.Width) < 0.01f,
                "四联宽度和 " + F(layout.Spec.TotalExtent) + " = 版面宽 " + F(layout.Bounds.Width)
                + "，联数 " + layout.Spec.Count);

            bool contiguous = true;
            for (int i = 1; i < layout.CopyRects.Count; i++)
            {
                if (Math.Abs(layout.CopyRects[i].Left - layout.CopyRects[i - 1].Right) > 0.01f)
                {
                    contiguous = false;
                }
            }
            failures += Check(name, contiguous && Math.Abs(layout.CopyRects[3].Right - layout.Bounds.Right) < 0.01f,
                "四联逐联接续且末联贴右缘：第 4 联右缘 " + F(layout.CopyRects[3].Right)
                + " / 版面右缘 " + F(layout.Bounds.Right));

            int totalFields = 0;
            int filledFields = 0;
            for (int i = 0; i < doc.Copies.Count; i++)
            {
                DeathCopy copy = doc.Copies[i];
                foreach (string key in copy.VisibleFields)
                {
                    totalFields++;
                    if (!string.IsNullOrEmpty(doc.Get(key)))
                    {
                        filledFields++;
                    }
                }
                foreach (string key in copy.ExtraFields)
                {
                    totalFields++;
                    if (!string.IsNullOrEmpty(doc.Get(key)))
                    {
                        filledFields++;
                    }
                }
            }
            failures += Check(name, totalFields > 0 && filledFields == totalFields,
                "四联字段齐全 " + filledFields + "/" + totalFields + " 个可见字段有值");

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            failures += Check(name, png.Length > 5000,
                "96dpi PNG 字节数 " + png.Length + "（要求 >5000），尺寸 "
                + F(layout.Bounds.Width) + "×" + F(layout.Bounds.Height));

            return failures;
        }

        private static int VerifyIcuNursing(string name, Chart chart)
        {
            IcuNursingDocument doc = (IcuNursingDocument)chart.Document;
            IcuNursingLayout layout = chart.Geometry.LayoutOf<IcuNursingLayout>()
                ?? throw new InvalidOperationException("几何里没有 IcuNursingLayout。");
            int failures = 0;

            ColumnHeaderNode root = layout.Root;
            int groupChildSum = 0;
            bool spansOk = true;
            foreach (ColumnHeaderNode group in root.Children)
            {
                int groupSum = 0;
                foreach (ColumnHeaderNode leaf in group.Children)
                {
                    groupSum += leaf.LeafSpan;
                }
                if (groupSum != group.LeafSpan)
                {
                    spansOk = false;
                }
                groupChildSum += group.LeafSpan;
            }
            failures += Check(name,
                root.Depth == 2 && root.LeafSpan == IcuNursingColumns.All.Count && spansOk
                && groupChildSum == root.LeafSpan,
                "三级表头：层数 " + layout.Header.LevelCount + "，叶跨度求和 " + groupChildSum
                + " = 叶子列数 " + IcuNursingColumns.All.Count + "，列边界 "
                + layout.Header.LeafBounds.Count + " 条");

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            using (PixelProbe probe = PixelProbe.FromPng(png))
            {
                ColumnHeaderNode diagonal = layout.Header.FindNode(IcuNursingColumns.CentralVenousPressure);
                SKRect diagonalRect = layout.Header.RectOf(diagonal);
                SKRect neighborRect = layout.Header.RectOf(IcuNursingColumns.Diastolic);
                int offDiagonal = probe.InkOffDiagonal(diagonalRect, 3.5f);
                int neighborInk = probe.InkOffDiagonal(neighborRect, 3.5f);
                failures += Check(name,
                    diagonal.Diagonal && offDiagonal == 0 && neighborInk > 0,
                    "斜线格（中心静脉压）Diagonal=" + diagonal.Diagonal + "，离斜线墨点 "
                    + offDiagonal + " 个；相邻舒张压格离斜线墨点 " + neighborInk + " 个（正对照）");

                int sideFilled = 0;
                bool sideInk = true;
                for (int row = 0; row < layout.SideTable.RowCount; row++)
                {
                    if (row < doc.HandoverNotes.Count && !string.IsNullOrEmpty(doc.HandoverNotes[row]))
                    {
                        sideFilled++;
                    }
                    SKRect rect = layout.SideRowRect(row);
                    SKRect valueRect = new SKRect(rect.Left + layout.SideLabelWidth + 3f, rect.Top + 3f,
                        rect.Right - 3f, rect.Bottom - 3f);
                    if (!probe.HasInkInBox((int)Math.Floor(valueRect.Left), (int)Math.Floor(valueRect.Top),
                            (int)Math.Ceiling(valueRect.Right), (int)Math.Ceiling(valueRect.Bottom)))
                    {
                        sideInk = false;
                    }
                }
                failures += Check(name, sideFilled == layout.SideTable.RowCount && sideInk,
                    "侧挂子表 " + sideFilled + "/" + layout.SideTable.RowCount + " 行有值且落墨="
                    + sideInk + "，侧表行距 " + F(layout.SideTable.RowHeight)
                    + " ≠ 主表行距 " + F(layout.MainRows[0].Height));
            }

            failures += Check(name, png.Length > 5000,
                "96dpi PNG 字节数 " + png.Length + "（要求 >5000），尺寸 "
                + F(layout.Bounds.Width) + "×" + F(layout.Bounds.Height));

            return failures;
        }

        private static int VerifyVaccinationBooklet(string name, Chart chart)
        {
            VaccinationBookletDocument doc = (VaccinationBookletDocument)chart.Document;
            VaccinationBookletLayout layout = chart.Geometry.LayoutOf<VaccinationBookletLayout>()
                ?? throw new InvalidOperationException("几何里没有 VaccinationBookletLayout。");
            int failures = 0;

            failures += Check(name,
                layout.LeftPage == 10 && layout.RightPage == 3
                && layout.LeftPage + layout.RightPage == doc.TotalLogicalPages + 1,
                "开面配对 (" + layout.LeftPage + "," + layout.RightPage + ") 之和 "
                + (layout.LeftPage + layout.RightPage) + " = 逻辑页数+1 " + (doc.TotalLogicalPages + 1));

            int matched = 0;
            for (int i = 0; i < layout.ScheduleRows.Count; i++)
            {
                if (HasRecord(doc, layout.ScheduleRows[i]))
                {
                    matched++;
                }
            }
            failures += Check(name, doc.Records.Count >= 6 && matched == doc.Records.Count,
                "接种剂次 " + doc.Records.Count + " 条（要求 ≥6），命中预印行 " + matched
                + " 条，开面承载预印行 " + layout.ScheduleRows.Count + " 条");

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            using (PixelProbe probe = PixelProbe.FromPng(png))
            {
                SKRect firstHeader = layout.TableHeaderRect(3);
                SKRect continuationHeader = layout.TableHeaderRect(10);
                bool firstInk = probe.HasInkInBox((int)Math.Floor(firstHeader.Left),
                    (int)Math.Floor(firstHeader.Top), (int)Math.Ceiling(firstHeader.Right),
                    (int)Math.Ceiling(firstHeader.Bottom));
                bool continuationClean = !probe.HasInkInBox((int)Math.Floor(continuationHeader.Left),
                    (int)Math.Floor(continuationHeader.Top), (int)Math.Ceiling(continuationHeader.Right),
                    (int)Math.Ceiling(continuationHeader.Bottom));
                failures += Check(name, firstInk && continuationClean,
                    "记录首页（页 3）表头落墨=" + firstInk + "，续页（页 10）表头无墨="
                    + continuationClean + "（跨开面不重复表头）");

                int rowsWithInk = 0;
                for (int i = 0; i < 8 && i < layout.ScheduleRows.Count; i++)
                {
                    SKRect row = layout.ScheduleRowRect(i);
                    if (probe.HasInkInBox((int)Math.Floor(row.Left), (int)Math.Floor(row.Top),
                            (int)Math.Ceiling(row.Right), (int)Math.Ceiling(row.Bottom)))
                    {
                        rowsWithInk++;
                    }
                }
                failures += Check(name, rowsWithInk == 8,
                    "前 8 条预印剂次行（含已填记录）落墨 " + rowsWithInk + "/8；单页 "
                    + F(layout.LeftRect.Width) + "×" + F(layout.LeftRect.Height) + "px（210×145mm）");
            }

            failures += Check(name, png.Length > 5000,
                "96dpi PNG 字节数 " + png.Length + "（要求 >5000），开面尺寸 "
                + F(layout.Bounds.Width) + "×" + F(layout.Bounds.Height));

            return failures;
        }

        private static bool HasRecord(VaccinationBookletDocument doc, VaccinationScheduleRow row)
        {
            for (int i = 0; i < doc.Records.Count; i++)
            {
                VaccinationRecord record = doc.Records[i];
                if (string.Equals(record.Vaccine, row.Vaccine, StringComparison.Ordinal)
                    && string.Equals(record.Dose, row.Dose, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static int VerifyAstReport(string name, Chart chart)
        {
            AstReportDocument doc = (AstReportDocument)chart.Document;
            AstReportLayout layout = chart.Geometry.LayoutOf<AstReportLayout>()
                ?? throw new InvalidOperationException("几何里没有 AstReportLayout。");
            int failures = 0;

            failures += Check(name,
                layout.RangeBar is WeightedCategoryAxis && layout.RangeBar.Count == 4,
                "RangeBar 是 " + layout.RangeBar.GetType().Name + "，段数 " + layout.RangeBar.Count);

            float widthS = layout.RangeBar.WidthOf(0);
            float widthSdd = layout.RangeBar.WidthOf(1);
            failures += Check(name,
                widthS > 0f && Math.Abs(widthSdd / widthS - 1.5f) < 0.01f,
                "SDD/S 段宽 " + F(widthSdd) + "/" + F(widthS) + " = " + F(widthSdd / widthS)
                + "（权重 1.5/1）");

            List<string> categories = new List<string>();
            foreach (AstAntibiotic antibiotic in doc.Antibiotics)
            {
                if (!categories.Contains(antibiotic.Category))
                {
                    categories.Add(antibiotic.Category);
                }
            }
            failures += Check(name,
                doc.Antibiotics.Count >= 4 && categories.Count >= 3,
                "抗菌药 " + doc.Antibiotics.Count + " 种（要求 ≥4），判读 " + categories.Count
                + " 类（要求 ≥3）：" + string.Join("/", categories.ToArray()));

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            using (PixelProbe probe = PixelProbe.FromPng(png))
            {
                int rowsInked = 0;
                for (int row = 0; row < layout.RowRects.Count; row++)
                {
                    bool labelInk = probe.HasInkInBox((int)Math.Floor(layout.LabelRects[row].Left) + 1,
                        (int)Math.Floor(layout.LabelRects[row].Top) + 1,
                        (int)Math.Ceiling(layout.LabelRects[row].Right) - 1,
                        (int)Math.Ceiling(layout.LabelRects[row].Bottom) - 1);
                    bool resultInk = probe.HasInkInBox((int)Math.Floor(layout.ResultRects[row].Left) + 1,
                        (int)Math.Floor(layout.ResultRects[row].Top) + 1,
                        (int)Math.Ceiling(layout.ResultRects[row].Right) - 1,
                        (int)Math.Ceiling(layout.ResultRects[row].Bottom) - 1);
                    if (labelInk && resultInk)
                    {
                        rowsInked++;
                    }
                }
                failures += Check(name, rowsInked == layout.RowRects.Count,
                    "药名 + MIC/判读列落墨 " + rowsInked + "/" + layout.RowRects.Count + " 行");
            }

            failures += Check(name, png.Length > 5000,
                "96dpi PNG 字节数 " + png.Length + "（要求 >5000），尺寸 "
                + F(layout.Bounds.Width) + "×" + F(layout.Bounds.Height));

            return failures;
        }


        private static int Check(string name, bool passed, string detail)
        {
            Console.WriteLine("[" + name + "] " + (passed ? "通过" : "失败") + ": " + detail);
            return passed ? 0 : 1;
        }

        private static string F(double value)
        {
            if (double.IsNaN(value))
            {
                return "NaN";
            }
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 轻量像素探针：把 96dpi PNG 读回 BGRA8888 裸像素，供"斜线格无文字""表头不重复"等落位断言。
        /// 只用 SkiaSharp 的 Skia 解码入口，不引第三方图像库，也不用 SKImage.FromPicture。
        /// </summary>
        private sealed class PixelProbe : IDisposable
        {
            private readonly byte[] _pixels;

            private PixelProbe(byte[] pixels, int width, int height)
            {
                _pixels = pixels;
                Width = width;
                Height = height;
            }

            public int Width { get; }

            public int Height { get; }

            public static PixelProbe FromPng(byte[] png)
            {
                using (SKData data = SKData.CreateCopy(png))
                using (SKImage? image = SKImage.FromEncodedData(data))
                {
                    if (image == null)
                    {
                        throw new InvalidOperationException("PNG 解码失败。");
                    }

                    SKImageInfo info = new SKImageInfo(image.Width, image.Height,
                        SKColorType.Bgra8888, SKAlphaType.Premul);
                    byte[] buffer = new byte[info.BytesSize];
                    GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                    try
                    {
                        if (!image.ReadPixels(info, handle.AddrOfPinnedObject(), info.RowBytes, 0, 0))
                        {
                            throw new InvalidOperationException("ReadPixels 失败。");
                        }
                    }
                    finally
                    {
                        handle.Free();
                    }

                    return new PixelProbe(buffer, image.Width, image.Height);
                }
            }

            /// <summary>非白：任一通道低于 247（与测试用探针同判据）。</summary>
            public bool IsInk(int x, int y)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height)
                {
                    return false;
                }
                int index = y * Width * 4 + x * 4;
                return _pixels[index] < 247 || _pixels[index + 1] < 247 || _pixels[index + 2] < 247;
            }

            public bool HasInkInBox(int left, int top, int right, int bottom)
            {
                for (int y = Math.Max(0, top); y < Math.Min(Height, bottom); y++)
                {
                    for (int x = Math.Max(0, left); x < Math.Min(Width, right); x++)
                    {
                        if (IsInk(x, y))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            /// <summary>矩形内非白像素占比。</summary>
            public double NonWhiteRatio(SKRect rect)
            {
                int left = Math.Max(0, (int)Math.Ceiling(rect.Left));
                int top = Math.Max(0, (int)Math.Ceiling(rect.Top));
                int right = Math.Min(Width, (int)Math.Floor(rect.Right));
                int bottom = Math.Min(Height, (int)Math.Floor(rect.Bottom));
                int area = (right - left) * (bottom - top);
                if (area <= 0)
                {
                    return 0d;
                }

                int ink = 0;
                for (int y = top; y < bottom; y++)
                {
                    for (int x = left; x < right; x++)
                    {
                        if (IsInk(x, y))
                        {
                            ink++;
                        }
                    }
                }
                return (double)ink / area;
            }

            /// <summary>统计单元格内离开对角斜线带的墨点（=0 即"斜线格无文字"）。</summary>
            public int InkOffDiagonal(SKRect rect, float bandPx)
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
                        if (!IsInk(x, y))
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

            public void Dispose()
            {
            }
        }
    }
}
