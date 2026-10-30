using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Tickets.Vat;
using OODD.Charts.Tickets.WorkTicket;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>
    /// 凭证家族三张表单的演示样例（T290）：电气第一种工作票、八类特殊作业票（动火作业票）、
    /// 增值税专用发票（四联）。
    /// <para>
    /// <b>示例数据，非真实机型/标准值；票面栏位未按票样校准。</b>
    /// 栏位次序、联名、措施文本、密码区格位与金额都只用于演示结构与落位，
    /// 不代表任何真实票样，也不宣称符合 GB 26860 附录 A / GB 30871 附录 A 或任何税务规范
    /// （docs/05 §5「版面待取证」、docs/06 D22）。
    /// </para>
    /// <para>
    /// <see cref="All"/> 供演示程序与样例工具遍历；<see cref="Verify(string, Chart)"/> 是每张表的
    /// 结构断言（点数、落位、导出非空），按名称分发并返回失败条数，
    /// 逐条打印 <c>[name] 通过/失败: 说明 实到值</c>；未知名称返回 0。
    /// </para>
    /// </summary>
    public static class TicketSamples
    {
        /// <summary>电气第一种工作票的稳定样例名（逐字）。</summary>
        public const string ElectricalSampleName = "electrical-work-ticket";

        /// <summary>八类特殊作业票的稳定样例名（逐字）。</summary>
        public const string HotWorkSampleName = "hot-work-ticket";

        /// <summary>增值税专用发票的稳定样例名（逐字）。</summary>
        public const string VatSampleName = "vat-invoice";

        /// <summary>票面上随数据出现的示例标注（示例数据，非真实机型/标准值）。</summary>
        private const string SampleNote = "示例数据（非真实机型/标准值）";

        /// <summary>三张凭证表的样例登记（名称、中文名、建图表工厂）。</summary>
        public static IReadOnlyList<DemoSample> All { get; } = new List<DemoSample>
        {
            new DemoSample(ElectricalSampleName, "电气第一种工作票", CreateElectricalWorkTicket),
            new DemoSample(HotWorkSampleName, "八类特殊作业票", CreateHotWorkTicket),
            new DemoSample(VatSampleName, "增值税专用发票（四联）", CreateVatInvoice),
        };

        // ------------------------------------------------------------------ 建图表

        private static Chart CreateElectricalWorkTicket()
        {
            return new Chart(BuildElectricalWorkTicket(), new ElectricalWorkTicketTemplate(), new ChartStyle());
        }

        private static Chart CreateHotWorkTicket()
        {
            return new Chart(BuildHotWorkTicket(), new HotWorkTicketTemplate(), new ChartStyle());
        }

        private static Chart CreateVatInvoice()
        {
            return new Chart(BuildVatInvoice(), new VatInvoiceTemplate(), new ChartStyle());
        }

        // ------------------------------------------------------------------ 样例数据

        /// <summary>
        /// 电气第一种工作票样例：三联（存根 / 工作 / 许可）、印章已执行、六条安全措施
        /// 按"勾 2 / 叉 2 / 空框 2"分布。
        /// <para>
        /// <b>示例数据，非真实机型/标准值；票面栏位未按票样校准。</b>
        /// 单位名、票号、人名与措施文本都是编造的演示值，不对应任何真实作业或标准条文。
        /// </para>
        /// </summary>
        public static ElectricalWorkTicketDocument BuildElectricalWorkTicket()
        {
            ElectricalWorkTicketDocument doc = new ElectricalWorkTicketDocument
            {
                UnitName = SampleNote + "　示例供电段",
                SerialNo = "DQ-2026-0001（示例）",
                Stamp = WorkTicketStampState.Executed,
            };

            doc.With(ElectricalWorkTicketFields.WorkLeader, "张三（示例）");
            doc.With(ElectricalWorkTicketFields.WorkClass, "检修一班");
            doc.With(ElectricalWorkTicketFields.WorkMembers, "李四、王五");
            doc.With(ElectricalWorkTicketFields.WorkPlace, "10kV 示例一线 03 号杆");
            doc.With(ElectricalWorkTicketFields.WorkTask, "更换跌落式熔断器（演示任务）");
            doc.With(ElectricalWorkTicketFields.PlanStart, "2026-10-30 08:30");
            doc.With(ElectricalWorkTicketFields.PlanEnd, "2026-10-30 17:30");
            doc.With(ElectricalWorkTicketFields.IssueDate, "2026-10-29");
            doc.With(ElectricalWorkTicketFields.SafetyIssuer, "赵六");
            doc.With(ElectricalWorkTicketFields.TerminateTime, "2026-10-30 17:20");
            doc.With(ElectricalWorkTicketFields.PermitTime, "2026-10-30 08:30");
            doc.With(ElectricalWorkTicketFields.GroundingCount, "2 组");

            // 勾 / 叉 / 空框各两行：样例要能同时看到三种确认态。
            doc.Measures.Add(new TicketMeasureRow("断开电源并验电，确认无电压", TriState.Checked));
            doc.Measures.Add(new TicketMeasureRow("装设接地线并悬挂标示牌", TriState.Checked));
            doc.Measures.Add(new TicketMeasureRow("邻近带电体加装绝缘遮蔽", TriState.NotApplicable));
            doc.Measures.Add(new TicketMeasureRow("二次回路及计量回路已隔离", TriState.NotApplicable));
            doc.Measures.Add(new TicketMeasureRow("高处作业安全带已挂设", TriState.Unchecked));
            doc.Measures.Add(new TicketMeasureRow("终结前清理现场工器具", TriState.Unchecked));
            return doc;
        }

        /// <summary>
        /// 八类特殊作业票样例：动火作业（副标题"作业类别：动火作业"）、两联（作业 / 监护）、
        /// 气体检测栏有值、印章已执行、危害辨识四条。
        /// <para>
        /// <b>示例数据，非真实机型/标准值；票面栏位未按票样校准。</b>
        /// 气体检测读数与措施、危害条目都是编造的演示值，不是任何标准的规定值。
        /// </para>
        /// </summary>
        public static HotWorkTicketDocument BuildHotWorkTicket()
        {
            HotWorkTicketDocument doc = new HotWorkTicketDocument
            {
                Category = HotWorkCategory.HotWork,
                UnitName = SampleNote + "　示例化工厂",
                SerialNo = "TS-2026-0007（示例）",
                Stamp = WorkTicketStampState.Executed,
                SectionTitle = "安全措施",
            };

            doc.With(HotWorkTicketFields.WorkUnit, "检修车间");
            doc.With(HotWorkTicketFields.WorkPlace, "二号罐区东侧管廊");
            doc.With(HotWorkTicketFields.WorkContent, "管架补焊（演示内容）");
            doc.With(HotWorkTicketFields.WorkLeader, "张三（示例）");
            doc.With(HotWorkTicketFields.Workers, "李四、王五");
            doc.With(HotWorkTicketFields.Guardian, "赵六");
            doc.With(HotWorkTicketFields.PlanStart, "2026-10-30 08:00");
            doc.With(HotWorkTicketFields.PlanEnd, "2026-10-30 16:00");
            doc.With(HotWorkTicketFields.WorkLevel, "一级动火（示例）");
            doc.With(HotWorkTicketFields.GasTest, "O2 20.9%，可燃气 0%LEL，H2S 0ppm（示例读数）");
            doc.With(HotWorkTicketFields.Approver, "钱七");

            doc.Measures.Add(new TicketMeasureRow("动火点周边可燃物已清除", TriState.Checked));
            doc.Measures.Add(new TicketMeasureRow("消防器材已就位", TriState.Checked));
            doc.Measures.Add(new TicketMeasureRow("邻近管廊已加装盲板隔离", TriState.NotApplicable));
            doc.Measures.Add(new TicketMeasureRow("气体检测合格后方可动火", TriState.Checked));
            doc.Measures.Add(new TicketMeasureRow("动火完毕检查余火", TriState.Unchecked));

            doc.Hazards.Add("火花引燃周边可燃物");
            doc.Hazards.Add("气瓶间距不足");
            doc.Hazards.Add("监护缺位");
            doc.Hazards.Add("动火完毕未清理余火");
            return doc;
        }

        /// <summary>
        /// 增值税专用发票样例：四联（存根 / 记账 / 抵扣 / 发票）、8 位发票号码、
        /// 30 位密文（密码区 4 行 × 21 格）、四条明细且金额合计自洽。
        /// <para>
        /// <b>示例数据，非真实机型/标准值；票面栏位未按票样校准。</b>
        /// 购销双方、税号、开户行、密文与货物名称都是编造的演示值，
        /// 不构成任何真实发票或税务凭证，库也不做真伪与合规判定。
        /// </para>
        /// </summary>
        public static VatInvoiceDocument BuildVatInvoice()
        {
            VatInvoiceDocument doc = new VatInvoiceDocument
            {
                CopyCount = 4,
                InvoiceCode = "000000000000",
                InvoiceNumber = "01234567",
                InvoiceDate = new DateTime(2026, 10, 30),
                CipherText = "0123456789ABCDEFGHIJKLMNOPQRST",
                BuyerName = "示例购买方有限公司",
                BuyerTaxId = "91110000MA00000001",
                BuyerAddressPhone = "示例市示例路 1 号 010-00000000",
                BuyerBankAccount = "示例银行示例支行 0200000000000000001",
                SellerName = "示例销售方有限公司",
                SellerTaxId = "91110000MA00000002",
                SellerAddressPhone = "示例市示例路 2 号 010-11111111",
                SellerBankAccount = "示例银行示例支行 1100000000000000002",
                Remark = "示例数据，非真实机型/标准值；票面栏位未按票样校准",
                Payee = "张三",
                Reviewer = "李四",
                Drawer = "王五",
                StubNo = "CG-2026-001",
                VoucherNo = "JZ-2026-045",
                DeductionPeriod = "2026-11",
                Reimburser = "赵六",
            };

            // 四条明细：金额 = 数量 × 单价，税额 = 金额 × 税率，合计由文档逐行累加。
            doc.Items.Add(new VatInvoiceItem
            {
                Name = "技术服务费", Specification = "—", Unit = "次",
                Quantity = 2d, UnitPrice = 1500.50d, TaxRate = 0.06d,
            });
            doc.Items.Add(new VatInvoiceItem
            {
                Name = "软件维护费", Specification = "V1.0", Unit = "月",
                Quantity = 3d, UnitPrice = 800d, TaxRate = 0.06d,
            });
            doc.Items.Add(new VatInvoiceItem
            {
                Name = "培训费", Specification = "—", Unit = "人",
                Quantity = 5d, UnitPrice = 300d, TaxRate = 0.03d,
            });
            doc.Items.Add(new VatInvoiceItem
            {
                Name = "设备租赁费", Specification = "XG-200", Unit = "台班",
                Quantity = 4d, UnitPrice = 625d, TaxRate = 0.13d,
            });
            return doc;
        }

        // ------------------------------------------------------------------ 结构断言

        /// <summary>
        /// 按样例名做结构断言，返回失败条数（未知名称返回 0）。
        /// 每条断言打印一行 <c>[name] 通过/失败: 说明 实到值</c>。
        /// </summary>
        /// <param name="name">样例稳定名：electrical-work-ticket / hot-work-ticket / vat-invoice。</param>
        /// <param name="chart">由 <see cref="All"/> 中的工厂建出的图表（本方法不 Dispose 它）。</param>
        public static int Verify(string name, Chart chart)
        {
            if (chart == null)
            {
                throw new ArgumentNullException(nameof(chart));
            }
            if (string.Equals(name, ElectricalSampleName, StringComparison.Ordinal))
            {
                return VerifyElectricalWorkTicket(chart);
            }
            if (string.Equals(name, HotWorkSampleName, StringComparison.Ordinal))
            {
                return VerifyHotWorkTicket(chart);
            }
            if (string.Equals(name, VatSampleName, StringComparison.Ordinal))
            {
                return VerifyVatInvoice(chart);
            }
            return 0;
        }

        private static int VerifyElectricalWorkTicket(Chart chart)
        {
            WorkTicketLayout? layout = chart.Geometry.LayoutOf<WorkTicketLayout>();
            if (layout == null)
            {
                return Check(ElectricalSampleName, "版面类型", false,
                    "几何里没有 WorkTicketLayout，实到 " + chart.Geometry.Layout?.GetType().FullName);
            }

            int failures = 0;
            failures += Check(ElectricalSampleName, "三联纵叠且联高之和等于版面高",
                layout.Collate.Count == 3 && Near(layout.Collate.TotalExtent, layout.Bounds.Height, 0.01f),
                "联数 " + Count(layout.Collate.Count) + "，联高和 " + F(layout.Collate.TotalExtent)
                + "，版面高 " + F(layout.Bounds.Height));

            failures += Check(ElectricalSampleName, "印章为已执行（三态之一）",
                layout.StampState == WorkTicketStampState.Executed
                && string.Equals(layout.StampText, "已执行", StringComparison.Ordinal),
                layout.StampState + " / 章内文字 " + (layout.StampText ?? "<null>"));

            int checkedRows = 0;
            int notApplicableRows = 0;
            int emptyRows = 0;
            foreach (WorkTicketMeasureLayout row in layout.Copy(0).Measures)
            {
                if (row.State == TriState.Checked)
                {
                    checkedRows++;
                }
                else if (row.State == TriState.NotApplicable)
                {
                    notApplicableRows++;
                }
                else
                {
                    emptyRows++;
                }
            }
            failures += Check(ElectricalSampleName, "措施 6 行且勾/叉/空框齐备",
                layout.MeasureRowCount == 6 && checkedRows == 2 && notApplicableRows == 2 && emptyRows == 2,
                "行数 " + Count(layout.MeasureRowCount) + "，勾 " + Count(checkedRows)
                + "，叉 " + Count(notApplicableRows) + "，空框 " + Count(emptyRows));

            ElectricalWorkTicketTemplate? template = chart.Template as ElectricalWorkTicketTemplate;
            if (template == null)
            {
                failures += Check(ElectricalSampleName, "模板类型", false,
                    "实到 " + chart.Template.GetType().FullName);
                return failures;
            }
            WorkTicketGridStyle grid = template.Tokens.Grid;

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            failures += Check(ElectricalSampleName, "96dpi PNG 导出非空", png.Length > 5000,
                png.Length.ToString(CultureInfo.InvariantCulture) + " B");

            using (InkProbe probe = InkProbe.FromPng(png))
            {
                WorkTicketCopyLayout stub = layout.Copy(0);
                WorkTicketCopyLayout work = layout.Copy(1);
                WorkTicketCopyLayout permit = layout.Copy(2);

                WorkTicketFieldRow? terminateStub = FindField(stub, ElectricalWorkTicketFields.TerminateTime);
                WorkTicketFieldRow? terminateWork = FindField(work, ElectricalWorkTicketFields.TerminateTime);
                WorkTicketFieldRow? terminatePermit = FindField(permit, ElectricalWorkTicketFields.TerminateTime);
                if (terminateStub == null || terminateWork == null || terminatePermit == null)
                {
                    failures += Check(ElectricalSampleName, "终结时间行几何", false,
                        "三联中至少一联缺少终结时间行");
                }
                else
                {
                    bool workInk = probe.HasInk(ValueBox(terminateWork, grid), 2f);
                    bool stubInk = probe.HasInk(ValueBox(terminateStub, grid), 2f);
                    bool permitInk = probe.HasInk(ValueBox(terminatePermit, grid), 2f);
                    bool ruleInk = probe.HasInk(RowBottomRule(terminateStub.Rect), 0f);
                    failures += Check(ElectricalSampleName, "终结时间只落在工作联，其余联留白",
                        workInk && !stubInk && !permitInk && ruleInk,
                        "工作联 " + Ink(workInk) + "，存根联 " + Ink(stubInk) + "，许可联 " + Ink(permitInk)
                        + "，留白行底线 " + Ink(ruleInk));
                }

                WorkTicketFieldRow? permitStub = FindField(stub, ElectricalWorkTicketFields.PermitTime);
                WorkTicketFieldRow? permitWork = FindField(work, ElectricalWorkTicketFields.PermitTime);
                WorkTicketFieldRow? permitPermit = FindField(permit, ElectricalWorkTicketFields.PermitTime);
                if (permitStub == null || permitWork == null || permitPermit == null)
                {
                    failures += Check(ElectricalSampleName, "许可开始时间行几何", false,
                        "三联中至少一联缺少许可开始时间行");
                }
                else
                {
                    bool permitInk = probe.HasInk(ValueBox(permitPermit, grid), 2f);
                    bool workInk = probe.HasInk(ValueBox(permitWork, grid), 2f);
                    bool stubInk = probe.HasInk(ValueBox(permitStub, grid), 2f);
                    failures += Check(ElectricalSampleName, "许可开始时间只落在许可联，其余联留白",
                        permitInk && !workInk && !stubInk,
                        "许可联 " + Ink(permitInk) + "，工作联 " + Ink(workInk) + "，存根联 " + Ink(stubInk));
                }

                WorkTicketCopyLayout first = layout.Copy(0);
                bool checkInk = probe.HasInk(CenterBox(first.Measures[0].BoxRect, 3f), 0f);
                bool crossInk = probe.HasInk(CenterBox(first.Measures[2].BoxRect, 3f), 0f);
                bool emptyInk = probe.HasInk(CenterBox(first.Measures[4].BoxRect, 3f), 0f);
                failures += Check(ElectricalSampleName, "三态框内勾/叉有墨且空框内部留白",
                    checkInk && crossInk && !emptyInk,
                    "勾行 " + Ink(checkInk) + "，叉行 " + Ink(crossInk) + "，空框行 " + Ink(emptyInk));

                int redCopies = 0;
                for (int c = 0; c < layout.Copies.Count; c++)
                {
                    if (probe.HasRed(layout.Copy(c).StampBounds))
                    {
                        redCopies++;
                    }
                }
                failures += Check(ElectricalSampleName, "每联已执行章落红墨",
                    redCopies == layout.Copies.Count,
                    "红章联数 " + Count(redCopies) + "/" + Count(layout.Copies.Count));
            }

            return failures;
        }

        private static int VerifyHotWorkTicket(Chart chart)
        {
            WorkTicketLayout? layout = chart.Geometry.LayoutOf<WorkTicketLayout>();
            if (layout == null)
            {
                return Check(HotWorkSampleName, "版面类型", false,
                    "几何里没有 WorkTicketLayout，实到 " + chart.Geometry.Layout?.GetType().FullName);
            }

            int failures = 0;
            failures += Check(HotWorkSampleName, "两联纵叠且联高之和等于版面高",
                layout.Collate.Count == 2 && Near(layout.Collate.TotalExtent, layout.Bounds.Height, 0.01f),
                "联数 " + Count(layout.Collate.Count) + "，联高和 " + F(layout.Collate.TotalExtent)
                + "，版面高 " + F(layout.Bounds.Height));

            failures += Check(HotWorkSampleName, "副标题为作业类别：动火作业",
                string.Equals(layout.Subtitle, "作业类别：动火作业", StringComparison.Ordinal),
                layout.Subtitle ?? "<null>");

            failures += Check(HotWorkSampleName, "11 个字段行，共有 9 / 差异 2",
                layout.FieldRowCount == 11 && layout.Collate.CommonFields.Count == 9
                && layout.Collate.DifferentiatingFields.Count == 2,
                "字段行 " + Count(layout.FieldRowCount) + "，共有 " + Count(layout.Collate.CommonFields.Count)
                + "，差异 " + Count(layout.Collate.DifferentiatingFields.Count));

            HotWorkTicketTemplate? template = chart.Template as HotWorkTicketTemplate;
            if (template == null)
            {
                failures += Check(HotWorkSampleName, "模板类型", false,
                    "实到 " + chart.Template.GetType().FullName);
                return failures;
            }
            WorkTicketGridStyle grid = template.Tokens.Grid;

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            failures += Check(HotWorkSampleName, "96dpi PNG 导出非空", png.Length > 5000,
                png.Length.ToString(CultureInfo.InvariantCulture) + " B");

            using (InkProbe probe = InkProbe.FromPng(png))
            {
                WorkTicketCopyLayout work = layout.Copy(0);
                WorkTicketCopyLayout guardian = layout.Copy(1);

                WorkTicketFieldRow? gasWork = FindField(work, HotWorkTicketFields.GasTest);
                WorkTicketFieldRow? gasGuardian = FindField(guardian, HotWorkTicketFields.GasTest);
                WorkTicketFieldRow? approverWork = FindField(work, HotWorkTicketFields.Approver);
                WorkTicketFieldRow? approverGuardian = FindField(guardian, HotWorkTicketFields.Approver);
                if (gasWork == null || gasGuardian == null || approverWork == null || approverGuardian == null)
                {
                    failures += Check(HotWorkSampleName, "气体检测/审批人行几何", false,
                        "两联中至少一联缺少气体检测或审批人行");
                }
                else
                {
                    bool gasInk = probe.HasInk(ValueBox(gasWork, grid), 2f);
                    bool gasHiddenInk = probe.HasInk(ValueBox(gasGuardian, grid), 2f);
                    failures += Check(HotWorkSampleName, "气体检测栏有值且只在作业联落墨",
                        gasInk && !gasHiddenInk && !string.IsNullOrEmpty(gasWork.Value),
                        "作业联 " + Ink(gasInk) + "（值 " + (gasWork.Value ?? "<空>") + "），监护联 " + Ink(gasHiddenInk));

                    bool approverInk = probe.HasInk(ValueBox(approverGuardian, grid), 2f);
                    bool approverHiddenInk = probe.HasInk(ValueBox(approverWork, grid), 2f);
                    failures += Check(HotWorkSampleName, "审批人只在监护联落墨",
                        approverInk && !approverHiddenInk,
                        "监护联 " + Ink(approverInk) + "，作业联 " + Ink(approverHiddenInk));
                }

                int hazardRows = work.SecondaryRows.Count;
                bool hazardPitch = hazardRows < 2
                    || Near(work.SecondaryRows[1].Row.Top - work.SecondaryRows[0].Row.Top,
                        grid.SecondaryRowHeight, 0.01f);
                failures += Check(HotWorkSampleName, "危害辨识行数与行距等于令牌",
                    hazardRows == 4 && hazardPitch,
                    "行数 " + Count(hazardRows) + "，行距 "
                    + (hazardRows < 2 ? "<单行>" : F(work.SecondaryRows[1].Row.Top - work.SecondaryRows[0].Row.Top))
                    + "，令牌 " + F(grid.SecondaryRowHeight));

                int redCopies = 0;
                for (int c = 0; c < layout.Copies.Count; c++)
                {
                    if (probe.HasRed(layout.Copy(c).StampBounds))
                    {
                        redCopies++;
                    }
                }
                failures += Check(HotWorkSampleName, "每联已执行章落红墨",
                    redCopies == layout.Copies.Count,
                    "红章联数 " + Count(redCopies) + "/" + Count(layout.Copies.Count));
            }

            return failures;
        }

        private static int VerifyVatInvoice(Chart chart)
        {
            VatInvoiceLayout? layout = chart.Geometry.LayoutOf<VatInvoiceLayout>();
            if (layout == null)
            {
                return Check(VatSampleName, "版面类型", false,
                    "几何里没有 VatInvoiceLayout，实到 " + chart.Geometry.Layout?.GetType().FullName);
            }

            int failures = 0;
            string labels = string.Empty;
            for (int c = 0; c < layout.Copies.Count; c++)
            {
                labels += (c == 0 ? string.Empty : "/") + layout.Copy(c).CopyLabel;
            }
            failures += Check(VatSampleName, "四联纵叠且联高之和等于版面高",
                layout.CopyCount == 4 && Near(layout.Collate.TotalExtent, layout.Bounds.Height, 0.01f)
                && string.Equals(labels, "存根联/记账联/抵扣联/发票联", StringComparison.Ordinal),
                "联数 " + Count(layout.CopyCount) + "，联名 " + labels
                + "，联高和 " + F(layout.Collate.TotalExtent) + "，版面高 " + F(layout.Bounds.Height));

            VatInvoiceStyle? tokens = (chart.Template as VatInvoiceTemplate)?.Tokens;
            if (tokens == null)
            {
                failures += Check(VatSampleName, "模板类型", false,
                    "实到 " + chart.Template.GetType().FullName);
                return failures;
            }

            VatCopyLayout first = layout.Copy(0);
            failures += Check(VatSampleName, "密码区 4 行 × 21 格共 84 格",
                first.CipherCells.Count == 84 && tokens.CipherRows == 4 && tokens.CipherColumns == 21,
                "格数 " + Count(first.CipherCells.Count) + "，令牌 "
                + Count(tokens.CipherRows) + "×" + Count(tokens.CipherColumns));

            failures += Check(VatSampleName, "发票号码 8 格且为 8 位数字",
                first.NumberCells.Count == 8 && layout.InvoiceNumber != null
                && layout.InvoiceNumber!.Length == 8,
                "格数 " + Count(first.NumberCells.Count) + "，号码 " + (layout.InvoiceNumber ?? "<null>"));

            VatInvoiceDocument doc = (VatInvoiceDocument)chart.Document;
            double amount = 0d;
            double tax = 0d;
            foreach (VatInvoiceItem item in doc.Items)
            {
                double lineAmount = Math.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
                amount += lineAmount;
                tax += Math.Round(lineAmount * item.TaxRate, 2, MidpointRounding.AwayFromZero);
            }
            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            tax = Math.Round(tax, 2, MidpointRounding.AwayFromZero);
            failures += Check(VatSampleName, "合计等于明细逐行之和",
                NearD(layout.AmountTotal, amount, 0.005d) && NearD(layout.TaxTotal, tax, 0.005d)
                && NearD(layout.GrandTotal, amount + tax, 0.005d) && doc.Items.Count >= 3,
                "明细 " + Count(doc.Items.Count) + " 行，金额 " + M(layout.AmountTotal) + "（算 " + M(amount)
                + "），税额 " + M(layout.TaxTotal) + "（算 " + M(tax) + "），价税合计 "
                + M(layout.GrandTotal) + "（算 " + M(amount + tax) + "）");

            failures += Check(VatSampleName, "大写金额与合计同源",
                string.Equals(layout.UppercaseAmount, doc.UppercaseAmount, StringComparison.Ordinal)
                && string.Equals(first.Pressed.Text, doc.UppercaseAmount, StringComparison.Ordinal),
                layout.UppercaseAmount + " / 压印 " + first.Pressed.Text);

            bool inside = first.Pressed.CharCells.Count == first.Pressed.Text.Length;
            int escape = 0;
            foreach (SKRect cell in first.Pressed.CharCells)
            {
                if (cell.Left < first.UpperAmountRect.Left - 0.001f
                    || cell.Right > first.UpperAmountRect.Right + 0.001f
                    || cell.Right > first.AmountGutterRect.Left + 0.001f
                    || Intersects(cell, first.LowerAmountRect))
                {
                    escape++;
                }
            }
            failures += Check(VatSampleName, "大写金额压印不越格、不压空槽与小写格",
                inside && escape == 0,
                "字数 " + Count(first.Pressed.Text.Length) + "，字格 " + Count(first.Pressed.CharCells.Count)
                + "，越界 " + Count(escape) + "，压印字号 " + F(first.Pressed.FontSize)
                + "（名义 " + F(first.Pressed.BaseSize) + "）");

            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            failures += Check(VatSampleName, "96dpi PNG 导出非空", png.Length > 5000,
                png.Length.ToString(CultureInfo.InvariantCulture) + " B");

            using (InkProbe probe = InkProbe.FromPng(png))
            {
                int inkedCells = 0;
                foreach (SKRect cell in first.NumberCells)
                {
                    if (probe.HasInk(cell, 2f))
                    {
                        inkedCells++;
                    }
                }
                failures += Check(VatSampleName, "8 个号码格逐格落字",
                    inkedCells == 8,
                    "有墨格数 " + Count(inkedCells) + "/8");

                string cipher = layout.CipherText ?? string.Empty;
                int filled = Math.Min(cipher.Length, first.CipherCells.Count);
                bool cellZeroInk = probe.HasInk(first.CipherCells[0], 2f);
                bool firstBlankEmpty = filled < first.CipherCells.Count
                    && !probe.HasInk(first.CipherCells[filled], 2f);
                bool lastEmpty = !probe.HasInk(first.CipherCells[first.CipherCells.Count - 1], 2f);
                failures += Check(VatSampleName, "30 位密文逐格落字，其余密文格留白",
                    cipher.Length == 30 && cellZeroInk && firstBlankEmpty && lastEmpty,
                    "密文长 " + Count(cipher.Length) + "，第 1 格 " + Ink(cellZeroInk)
                    + "，第 " + Count(filled + 1) + " 格 " + Ink(!firstBlankEmpty)
                    + "，第 84 格 " + Ink(!lastEmpty));

                int copyFieldFailures = 0;
                int copyFieldChecks = 0;
                for (int c = 0; c < layout.CopyCount; c++)
                {
                    VatCopyLayout copy = layout.Copy(c);
                    for (int r = 0; r < copy.CopyFieldRows.Count; r++)
                    {
                        VatCopyFieldRowLayout row = copy.CopyFieldRows[r];
                        bool inked = probe.HasInk(row.ValueRect, 2f);
                        bool expected = r == c;
                        copyFieldChecks++;
                        if (inked != expected)
                        {
                            copyFieldFailures++;
                        }
                    }
                }
                failures += Check(VatSampleName, "联次附加字段只在本联落墨、他联留白",
                    copyFieldFailures == 0 && copyFieldChecks == 16,
                    "检查 " + Count(copyFieldChecks) + " 行，不符 " + Count(copyFieldFailures));

                bool upperInk = probe.HasInk(first.UpperAmountRect, 5f);
                bool gutterEmpty = !probe.HasInk(first.AmountGutterRect, 1.5f);
                bool lowerInk = probe.HasInk(first.LowerAmountRect, 5f);
                failures += Check(VatSampleName, "大写格有墨、空槽无墨、小写格有墨",
                    upperInk && gutterEmpty && lowerInk,
                    "大写格 " + Ink(upperInk) + "，空槽 " + Ink(!gutterEmpty) + "，小写格 " + Ink(lowerInk));
            }

            return failures;
        }

        // ------------------------------------------------------------------ 小工具

        private static int Check(string name, string title, bool ok, string actual)
        {
            Console.WriteLine("[" + name + "] " + (ok ? "通过" : "失败") + ": " + title + " 实到 " + actual);
            return ok ? 0 : 1;
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string F(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string M(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Ink(bool inked)
        {
            return inked ? "有墨" : "无墨";
        }

        private static bool Near(float a, float b, float epsilon)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool NearD(double a, double b, double epsilon)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool Intersects(SKRect a, SKRect b)
        {
            return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
        }

        private static WorkTicketFieldRow? FindField(WorkTicketCopyLayout copy, string key)
        {
            foreach (WorkTicketFieldRow row in copy.Fields)
            {
                if (string.Equals(row.Key, key, StringComparison.Ordinal))
                {
                    return row;
                }
            }
            return null;
        }

        /// <summary>字段值探针：跳开标签列与行线，只覆盖值列内部。</summary>
        private static SKRect ValueBox(WorkTicketFieldRow row, WorkTicketGridStyle grid)
        {
            float labelRight = row.Rect.Left + grid.FieldLabelWidth;
            return new SKRect(labelRight + 6f, row.Rect.Top + 3f, row.Rect.Right - 6f, row.Rect.Bottom - 3f);
        }

        /// <summary>行底线探针：空白字段行也要能证明"行网格在，只是不落字"。</summary>
        private static SKRect RowBottomRule(SKRect row)
        {
            return new SKRect(row.Left + 2f, row.Bottom - 1f, row.Right - 2f, row.Bottom + 1f);
        }

        private static SKRect CenterBox(SKRect box, float half)
        {
            return new SKRect(box.MidX - half, box.MidY - half, box.MidX + half, box.MidY + half);
        }

        /// <summary>
        /// 96dpi PNG 的墨迹探针（与测试侧 ImageProbe 同一判据：任一通道低于 247 记为墨）。
        /// 只用于样例的自检断言，不参与绘制。
        /// </summary>
        private sealed class InkProbe : IDisposable
        {
            private readonly SKBitmap _bitmap;

            private InkProbe(SKBitmap bitmap)
            {
                _bitmap = bitmap;
            }

            public static InkProbe FromPng(byte[] png)
            {
                using (SKData data = SKData.CreateCopy(png))
                {
                    SKBitmap? bitmap = SKBitmap.Decode(data);
                    if (bitmap == null)
                    {
                        throw new InvalidOperationException("PNG 解码失败（" + png.Length + " B）。");
                    }
                    return new InkProbe(bitmap);
                }
            }

            public bool HasInk(SKRect rect, float inset)
            {
                int left = (int)Math.Ceiling(rect.Left + inset);
                int top = (int)Math.Ceiling(rect.Top + inset);
                int right = (int)Math.Floor(rect.Right - inset);
                int bottom = (int)Math.Floor(rect.Bottom - inset);
                if (right <= left || bottom <= top)
                {
                    return false;
                }

                for (int y = Math.Max(0, top); y < Math.Min(_bitmap.Height, bottom); y++)
                {
                    for (int x = Math.Max(0, left); x < Math.Min(_bitmap.Width, right); x++)
                    {
                        SKColor color = _bitmap.GetPixel(x, y);
                        if (color.Red < 247 || color.Green < 247 || color.Blue < 247)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public bool HasRed(SKRect rect)
            {
                int left = Math.Max(0, (int)Math.Ceiling(rect.Left));
                int top = Math.Max(0, (int)Math.Ceiling(rect.Top));
                int right = Math.Min(_bitmap.Width, (int)Math.Floor(rect.Right));
                int bottom = Math.Min(_bitmap.Height, (int)Math.Floor(rect.Bottom));
                for (int y = top; y < bottom; y++)
                {
                    for (int x = left; x < right; x++)
                    {
                        SKColor color = _bitmap.GetPixel(x, y);
                        if (color.Red > 120 && color.Red - color.Green > 50 && color.Red - color.Blue > 50)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }

            public void Dispose()
            {
                _bitmap.Dispose();
            }
        }
    }
}
