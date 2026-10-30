using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Tickets.Vat;
using OODD.Charts.Tickets.WorkTicket;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 凭证家族（T280 电气第一种工作票 + 八类特殊作业票、T281 增值税专用发票）的验收用例。
    /// <para>
    /// 判据分三层：<see cref="CollateSpec"/> 的多联结构（能切成 N 联、联间字段差异）、
    /// 版面的令牌可覆盖（A18：只换令牌不改模板），以及真实 PNG 上的墨迹（黑字、红章、空白行）。
    /// 每个"无墨"判据都配同一坐标的正控，避免探针落在不存在的区域上变成空判。
    /// </para>
    /// <para>
    /// <b>D22</b>：票面栏位未按票样校准，用例只验证结构，不宣称符合 GB 26860 / GB 30871 附录 A。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class TicketFormsTests
    {
        // ------------------------------------------------------------------ 构造样例

        private static ElectricalWorkTicketDocument ElectricalDoc(int measureCount)
        {
            ElectricalWorkTicketDocument doc = new ElectricalWorkTicketDocument
            {
                UnitName = "示例供电段",
                SerialNo = "DQ-2026-0001",
                Stamp = WorkTicketStampState.Executed,
            };
            doc.With(ElectricalWorkTicketFields.WorkLeader, "张三");
            doc.With(ElectricalWorkTicketFields.WorkClass, "检修一班");
            doc.With(ElectricalWorkTicketFields.WorkMembers, "李四、王五");
            doc.With(ElectricalWorkTicketFields.WorkPlace, "10kV 一号线 03 号杆");
            doc.With(ElectricalWorkTicketFields.WorkTask, "更换跌落式熔断器");
            doc.With(ElectricalWorkTicketFields.PlanStart, "2026-10-30 08:30");
            doc.With(ElectricalWorkTicketFields.PlanEnd, "2026-10-30 17:30");
            doc.With(ElectricalWorkTicketFields.IssueDate, "2026-10-29");
            doc.With(ElectricalWorkTicketFields.SafetyIssuer, "赵六");
            doc.With(ElectricalWorkTicketFields.TerminateTime, "2026-10-30 17:30");
            doc.With(ElectricalWorkTicketFields.PermitTime, "2026-10-30 08:30");
            doc.With(ElectricalWorkTicketFields.GroundingCount, "2 组");

            for (int i = 0; i < measureCount; i++)
            {
                TriState state = i % 3 == 0 ? TriState.Checked
                    : (i % 3 == 1 ? TriState.NotApplicable : TriState.Unchecked);
                doc.Measures.Add(new TicketMeasureRow("措施 " + (i + 1).ToString(CultureInfo.InvariantCulture), state));
            }
            return doc;
        }

        private static HotWorkTicketDocument HotDoc(int hazardCount, int measureCount)
        {
            HotWorkTicketDocument doc = new HotWorkTicketDocument
            {
                Category = HotWorkCategory.ConfinedSpace,
                UnitName = "示例化工厂",
                SerialNo = "TS-2026-0007",
                Stamp = WorkTicketStampState.Executed,
            };
            doc.With(HotWorkTicketFields.WorkUnit, "检修车间");
            doc.With(HotWorkTicketFields.WorkPlace, "二号储罐");
            doc.With(HotWorkTicketFields.WorkContent, "罐内清淤");
            doc.With(HotWorkTicketFields.WorkLeader, "张三");
            doc.With(HotWorkTicketFields.Workers, "李四、王五");
            doc.With(HotWorkTicketFields.Guardian, "赵六");
            doc.With(HotWorkTicketFields.PlanStart, "2026-10-30 08:00");
            doc.With(HotWorkTicketFields.PlanEnd, "2026-10-30 16:00");
            doc.With(HotWorkTicketFields.WorkLevel, "一级");
            doc.With(HotWorkTicketFields.GasTest, "O2 20.9%，可燃气 0%LEL");
            doc.With(HotWorkTicketFields.Approver, "钱七");

            for (int i = 0; i < hazardCount; i++)
            {
                doc.Hazards.Add("危害 " + (i + 1).ToString(CultureInfo.InvariantCulture));
            }
            for (int i = 0; i < measureCount; i++)
            {
                doc.Measures.Add(new TicketMeasureRow("措施 " + (i + 1).ToString(CultureInfo.InvariantCulture),
                    i % 2 == 0 ? TriState.Checked : TriState.Unchecked));
            }
            return doc;
        }

        private static VatInvoiceDocument VatDoc(int copyCount)
        {
            VatInvoiceDocument doc = new VatInvoiceDocument
            {
                CopyCount = copyCount,
                InvoiceCode = "011002000111",
                InvoiceNumber = "01234567",
                InvoiceDate = new DateTime(2026, 10, 30),
                CipherText = "0123456789ABCDEFGHIJKLMNOPQRST",
                BuyerName = "示例购买方有限公司",
                BuyerTaxId = "91110000MA00000001",
                BuyerAddressPhone = "北京市示例路 1 号 010-00000000",
                BuyerBankAccount = "工商银行示例支行 0200000000000000001",
                SellerName = "示例销售方有限公司",
                SellerTaxId = "91110000MA00000002",
                SellerAddressPhone = "北京市示例路 2 号 010-11111111",
                SellerBankAccount = "建设银行示例支行 1100000000000000002",
                Remark = "结构样例，未按票样校准",
                Payee = "张三",
                Reviewer = "李四",
                Drawer = "王五",
                StubNo = "CG-2026-001",
                VoucherNo = "JZ-2026-045",
                DeductionPeriod = "2026-11",
                Reimburser = "赵六",
            };
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
            return doc;
        }

        // ------------------------------------------------------------------ 小工具

        private static string R(SKRect rect)
        {
            return "(" + rect.Left.ToString("0.##", CultureInfo.InvariantCulture) + ","
                + rect.Top.ToString("0.##", CultureInfo.InvariantCulture) + ")-("
                + rect.Right.ToString("0.##", CultureInfo.InvariantCulture) + ","
                + rect.Bottom.ToString("0.##", CultureInfo.InvariantCulture) + ")";
        }

        private static bool Near(float a, float b, float epsilon)
        {
            return Math.Abs(a - b) <= epsilon;
        }

        private static bool InkIn(ImageProbe probe, SKRect rect, float inset)
        {
            int left = (int)Math.Ceiling(rect.Left + inset);
            int top = (int)Math.Ceiling(rect.Top + inset);
            int right = (int)Math.Floor(rect.Right - inset);
            int bottom = (int)Math.Floor(rect.Bottom - inset);
            if (right <= left || bottom <= top)
            {
                return false;
            }
            return probe.HasInkInBox(left, top, right, bottom);
        }

        private static bool EmptyIn(ImageProbe probe, SKRect rect, float inset)
        {
            return !InkIn(probe, rect, inset);
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

        private static int ShownCount(WorkTicketCopyLayout copy)
        {
            int shown = 0;
            foreach (WorkTicketFieldRow row in copy.Fields)
            {
                if (row.Shown)
                {
                    shown++;
                }
            }
            return shown;
        }

        private static SKRect ValueBox(WorkTicketFieldRow row, WorkTicketGridStyle grid)
        {
            float labelRight = row.Rect.Left + grid.FieldLabelWidth;
            return new SKRect(labelRight + 6f, row.Rect.Top + 3f,
                row.Rect.Right - 6f, row.Rect.Bottom - 3f);
        }

        private static ImageProbe Render(Chart chart)
        {
            return ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100));
        }

        // ------------------------------------------------------------------ 电气第一种工作票

        [Fact]
        public void 电气工作票_三联纵叠且联高之和等于版面高()
        {
            using (Chart chart = new Chart(ElectricalDoc(8), new ElectricalWorkTicketTemplate()))
            {
                WorkTicketLayout? layout = chart.Geometry.LayoutOf<WorkTicketLayout>();
                Assert.True(layout != null, "几何里没有 WorkTicketLayout，实到 "
                    + chart.Geometry.Layout?.GetType().FullName + "。");
                WorkTicketLayout l = layout!;

                Assert.True(l.Collate.Count == 3, "联数应为 3，实到 " + l.Collate.Count + "。");
                Assert.True(l.Collate.Direction == CollateDirection.VerticalStack,
                    "切联方向应为纵叠，实到 " + l.Collate.Direction + "。");
                Assert.True(Near(l.Collate.TotalExtent, l.Bounds.Height, 0.001f),
                    "联高之和 " + l.Collate.TotalExtent.ToString("0.###", CultureInfo.InvariantCulture)
                    + " 与版面高 " + l.Bounds.Height.ToString("0.###", CultureInfo.InvariantCulture) + " 不等。");
                Assert.True(l.Collate.ExtentDeviation(l.Bounds) < 0.001f,
                    "联高配平偏差 " + l.Collate.ExtentDeviation(l.Bounds).ToString("0.####", CultureInfo.InvariantCulture) + " 不为 0。");

                for (int i = 1; i < l.Copies.Count; i++)
                {
                    Assert.True(Near(l.Copies[i].Rect.Top, l.Copies[i - 1].Rect.Bottom, 0.001f),
                        "第 " + i + " 联上缘 " + l.Copies[i].Rect.Top.ToString("0.##", CultureInfo.InvariantCulture)
                        + " 与第 " + (i - 1) + " 联下缘 "
                        + l.Copies[i - 1].Rect.Bottom.ToString("0.##", CultureInfo.InvariantCulture) + " 不接续。");
                }

                Assert.True(l.Collate.PerforationAt(1, l.Bounds).HasValue,
                    "第 1 道骑缝线不存在。");
                Assert.True(l.Collate.PerforationAt(2, l.Bounds).HasValue,
                    "第 2 道骑缝线不存在。");
                Assert.True(!l.Collate.PerforationAt(0, l.Bounds).HasValue
                    && !l.Collate.PerforationAt(3, l.Bounds).HasValue,
                    "第 0/3 道边界不应有骑缝线。");
            }
        }

        [Fact]
        public void 电气工作票_联间字段差异由字段键集合决定()
        {
            ElectricalWorkTicketDocument doc = ElectricalDoc(8);
            ElectricalWorkTicketStyle tokens = new ElectricalWorkTicketStyle();
            WorkTicketSheetSpec spec = ElectricalWorkTicketTemplate.CreateSpec(doc, tokens);

            using (Chart chart = new Chart(doc, new ElectricalWorkTicketTemplate { Tokens = tokens }))
            {
                WorkTicketLayout? layout = chart.Geometry.LayoutOf<WorkTicketLayout>();
                Assert.True(layout != null, "几何里没有 WorkTicketLayout。");
                WorkTicketLayout l = layout!;

                int expectedCommon = spec.Copies[0].FieldKeys.Count;
                for (int c = 1; c < spec.Copies.Count; c++)
                {
                    if (spec.Copies[c].FieldKeys.Count < expectedCommon)
                    {
                        expectedCommon = spec.Copies[c].FieldKeys.Count;
                    }
                }

                Assert.True(l.Collate.CommonFields.Count == expectedCommon,
                    "共有字段数应为 " + expectedCommon + "，实到 " + l.Collate.CommonFields.Count + "。");
                Assert.True(l.Collate.DifferentiatingFields.Count == 3,
                    "差异字段数应为 3，实到 " + l.Collate.DifferentiatingFields.Count + "。");
                Assert.True(l.FieldRowCount == 12, "字段行数应为 12，实到 " + l.FieldRowCount + "。");

                Assert.True(ShownCount(l.Copy(0)) == expectedCommon,
                    "存根联显示字段数应为 " + expectedCommon + "，实到 " + ShownCount(l.Copy(0)) + "。");
                Assert.True(ShownCount(l.Copy(1)) == expectedCommon + 1,
                    "工作联显示字段数应为 " + (expectedCommon + 1) + "，实到 " + ShownCount(l.Copy(1)) + "。");
                Assert.True(ShownCount(l.Copy(2)) == expectedCommon + 2,
                    "许可联显示字段数应为 " + (expectedCommon + 2) + "，实到 " + ShownCount(l.Copy(2)) + "。");

                Assert.True(l.Collate.FindCopy("stub") != null && l.Collate.FindCopy("work") != null
                    && l.Collate.FindCopy("permit") != null,
                    "三联键应为 stub/work/permit，实到 " + string.Join(",", CopyKeys(l)) + "。");

                Assert.True(l.Copy(0).ShowsNone(ElectricalWorkTicketFields.TerminateTime,
                        ElectricalWorkTicketFields.PermitTime, ElectricalWorkTicketFields.GroundingCount),
                    "存根联不应出现终结/许可/接地字段。");
                Assert.True(l.Copy(1).ShowsField(ElectricalWorkTicketFields.TerminateTime)
                    && !l.Copy(1).ShowsField(ElectricalWorkTicketFields.PermitTime)
                    && !l.Copy(1).ShowsField(ElectricalWorkTicketFields.GroundingCount),
                    "工作联应只多出终结时间。");
                Assert.True(l.Copy(2).ShowsField(ElectricalWorkTicketFields.PermitTime)
                    && l.Copy(2).ShowsField(ElectricalWorkTicketFields.GroundingCount)
                    && !l.Copy(2).ShowsField(ElectricalWorkTicketFields.TerminateTime),
                    "许可联应只多出许可开始时间与接地组数。");

                for (int i = 1; i < l.Copies[0].Fields.Count; i++)
                {
                    Assert.True(Near(l.Copies[0].Fields[i].Rect.Top, l.Copies[0].Fields[i - 1].Rect.Bottom, 0.001f),
                        "字段行网格不连续：第 " + i + " 行上缘 " + l.Copies[0].Fields[i].Rect.Top.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，上一行下缘 " + l.Copies[0].Fields[i - 1].Rect.Bottom.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                }
            }
        }

        [Fact]
        public void 电气工作票_隐藏字段行留白而本联字段有墨()
        {
            using (Chart chart = new Chart(ElectricalDoc(8), new ElectricalWorkTicketTemplate()))
            using (ImageProbe probe = Render(chart))
            {
                WorkTicketLayout l = chart.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketGridStyle grid = ((ElectricalWorkTicketTemplate)chart.Template).Tokens.Grid;

                WorkTicketFieldRow? stubTerminate = FindField(l.Copy(0), ElectricalWorkTicketFields.TerminateTime);
                WorkTicketFieldRow? workTerminate = FindField(l.Copy(1), ElectricalWorkTicketFields.TerminateTime);
                WorkTicketFieldRow? permitTerminate = FindField(l.Copy(2), ElectricalWorkTicketFields.TerminateTime);
                Assert.True(stubTerminate != null && workTerminate != null && permitTerminate != null,
                    "三联都应保留终结时间行的几何。");

                Assert.True(Near(stubTerminate!.Rect.Top - l.Copy(0).Rect.Top,
                        workTerminate!.Rect.Top - l.Copy(1).Rect.Top, 0.001f)
                    && Near(workTerminate.Rect.Top - l.Copy(1).Rect.Top,
                        permitTerminate!.Rect.Top - l.Copy(2).Rect.Top, 0.001f),
                    "同一字段行在联内的相对 y 不一致："
                    + (stubTerminate.Rect.Top - l.Copy(0).Rect.Top).ToString("0.##", CultureInfo.InvariantCulture) + "/"
                    + (workTerminate.Rect.Top - l.Copy(1).Rect.Top).ToString("0.##", CultureInfo.InvariantCulture) + "/"
                    + (permitTerminate.Rect.Top - l.Copy(2).Rect.Top).ToString("0.##", CultureInfo.InvariantCulture) + "。");

                SKRect workValue = ValueBox(workTerminate, grid);
                Assert.True(InkIn(probe, workValue, 2f),
                    "工作联终结时间行应有墨，探针 " + R(workValue) + " 实到无墨。");
                Assert.True(EmptyIn(probe, ValueBox(stubTerminate, grid), 2f),
                    "存根联终结时间行应留白，探针 " + R(ValueBox(stubTerminate, grid)) + " 实到有墨。");
                Assert.True(EmptyIn(probe, ValueBox(permitTerminate, grid), 2f),
                    "许可联终结时间行应留白，探针 " + R(ValueBox(permitTerminate, grid)) + " 实到有墨。");

                WorkTicketFieldRow? permitRow = FindField(l.Copy(2), ElectricalWorkTicketFields.PermitTime);
                WorkTicketFieldRow? workPermitRow = FindField(l.Copy(1), ElectricalWorkTicketFields.PermitTime);
                Assert.True(permitRow != null && workPermitRow != null, "许可开始时间行几何缺失。");
                Assert.True(InkIn(probe, ValueBox(permitRow!, grid), 2f),
                    "许可联许可开始时间行应有墨，探针 " + R(ValueBox(permitRow!, grid)) + " 实到无墨。");
                Assert.True(EmptyIn(probe, ValueBox(workPermitRow!, grid), 2f),
                    "工作联许可开始时间行应留白，探针 " + R(ValueBox(workPermitRow!, grid)) + " 实到有墨。");

                // 正控：空白行的行线必须真实存在，证明探针 y 落在该行上。
                SKRect hiddenRow = stubTerminate.Rect;
                Assert.True(InkIn(probe, new SKRect(hiddenRow.Left + 2f, hiddenRow.Bottom - 1f,
                        hiddenRow.Right - 2f, hiddenRow.Bottom + 1f), 0f),
                    "存根联终结时间行的底线不存在，探针 " + R(hiddenRow) + " 附近无墨。");
            }
        }

        [Fact]
        public void 电气工作票_印章三态几何同源且像素可分()
        {
            ElectricalWorkTicketStyle tokens = new ElectricalWorkTicketStyle();
            float diameter = tokens.Ticket.StampDiameter;

            ElectricalWorkTicketDocument executedDoc = ElectricalDoc(4);
            executedDoc.Stamp = WorkTicketStampState.Executed;
            ElectricalWorkTicketDocument voidedDoc = ElectricalDoc(4);
            voidedDoc.Stamp = WorkTicketStampState.Voided;
            ElectricalWorkTicketDocument notExecutedDoc = ElectricalDoc(4);
            notExecutedDoc.Stamp = WorkTicketStampState.NotExecuted;

            using (Chart executed = new Chart(executedDoc, new ElectricalWorkTicketTemplate { Tokens = tokens }))
            using (Chart voided = new Chart(voidedDoc, new ElectricalWorkTicketTemplate { Tokens = tokens }))
            using (Chart notExecuted = new Chart(notExecutedDoc, new ElectricalWorkTicketTemplate { Tokens = tokens }))
            using (ImageProbe executedProbe = Render(executed))
            using (ImageProbe voidedProbe = Render(voided))
            using (ImageProbe notExecutedProbe = Render(notExecuted))
            {
                WorkTicketCopyLayout executedCopy = executed.Geometry.LayoutOf<WorkTicketLayout>()!.Copy(0);
                WorkTicketCopyLayout voidedCopy = voided.Geometry.LayoutOf<WorkTicketLayout>()!.Copy(0);
                WorkTicketCopyLayout notCopy = notExecuted.Geometry.LayoutOf<WorkTicketLayout>()!.Copy(0);

                Assert.True(Near(executedCopy.StampBounds.Width, voidedCopy.StampBounds.Width, 0.001f)
                    && Near(executedCopy.StampBounds.Width, notCopy.StampBounds.Width, 0.001f)
                    && Near(executedCopy.StampCenter.X, notCopy.StampCenter.X, 0.001f)
                    && Near(executedCopy.StampCenter.Y, notCopy.StampCenter.Y, 0.001f),
                    "三态印章几何不同源：宽 " + executedCopy.StampBounds.Width.ToString("0.##", CultureInfo.InvariantCulture)
                    + "/" + voidedCopy.StampBounds.Width.ToString("0.##", CultureInfo.InvariantCulture)
                    + "/" + notCopy.StampBounds.Width.ToString("0.##", CultureInfo.InvariantCulture) + "。");

                Assert.True(executedProbe.HasRedInBox((int)executedCopy.StampBounds.Left, (int)executedCopy.StampBounds.Top,
                        (int)executedCopy.StampBounds.Right, (int)executedCopy.StampBounds.Bottom),
                    "已执行章在 " + R(executedCopy.StampBounds) + " 内没有红墨。");
                Assert.True(notExecutedProbe.HasInkInBox((int)notCopy.StampBounds.Left, (int)notCopy.StampBounds.Top,
                        (int)notCopy.StampBounds.Right, (int)notCopy.StampBounds.Bottom),
                    "未执行章没有浅色墨（正控失败）。");
                Assert.True(!notExecutedProbe.HasRedInBox((int)notCopy.StampBounds.Left, (int)notCopy.StampBounds.Top,
                        (int)notCopy.StampBounds.Right, (int)notCopy.StampBounds.Bottom),
                    "未执行章不应有红墨，探针 " + R(notCopy.StampBounds) + " 实到有红。");

                float reach = diameter * 0.30f * 0.85f;
                int crossX = (int)Math.Round(voidedCopy.StampCenter.X - reach);
                int crossY = (int)Math.Round(voidedCopy.StampCenter.Y - reach);
                Assert.True(voidedProbe.HasRedInBox(crossX - 2, crossY - 2, crossX + 2, crossY + 2),
                    "作废章交叉线在 (" + crossX + "," + crossY + ") 附近无红墨。");
                Assert.True(!executedProbe.HasRedInBox(crossX - 2, crossY - 2, crossX + 2, crossY + 2),
                    "已执行章不应有交叉线，(" + crossX + "," + crossY + ") 附近实到有红。");
            }
        }

        [Fact]
        public void 电气工作票_措施三态框勾叉与空框可分()
        {
            using (Chart chart = new Chart(ElectricalDoc(6), new ElectricalWorkTicketTemplate()))
            using (ImageProbe probe = Render(chart))
            {
                WorkTicketCopyLayout copy = chart.Geometry.LayoutOf<WorkTicketLayout>()!.Copy(0);
                Assert.True(copy.Measures.Count == 6, "措施行数应为 6，实到 " + copy.Measures.Count + "。");

                for (int i = 0; i < 3; i++)
                {
                    WorkTicketMeasureLayout row = copy.Measures[i];
                    SKRect box = row.BoxRect;
                    int left = (int)Math.Floor(box.MidX - 3f);
                    int top = (int)Math.Floor(box.MidY - 3f);
                    int right = (int)Math.Ceiling(box.MidX + 3f);
                    int bottom = (int)Math.Ceiling(box.MidY + 3f);

                    Assert.True(probe.HasInkInBox((int)box.Left, (int)box.Top, (int)box.Right, (int)box.Bottom),
                        "第 " + i + " 行三态框整体应含框线，探针 " + R(box) + " 无墨。");

                    if (i == 2)
                    {
                        Assert.True(!probe.HasInkInBox(left, top, right, bottom),
                            "未选中框的内部应无墨，探针 (" + left + "," + top + ")-(" + right + "," + bottom + ") 实到有墨。");
                    }
                    else
                    {
                        Assert.True(probe.HasInkInBox(left, top, right, bottom),
                            "第 " + i + " 行（" + row.State + "）框内应有标记，探针 ("
                            + left + "," + top + ")-(" + right + "," + bottom + ") 实到无墨。");
                    }
                }
            }
        }

        [Fact]
        public void 电气工作票_措施行数增加按行高抬高版面()
        {
            ElectricalWorkTicketStyle tokens = new ElectricalWorkTicketStyle();
            float rowHeight = tokens.Grid.MeasureRowHeight;

            using (Chart small = new Chart(ElectricalDoc(8), new ElectricalWorkTicketTemplate { Tokens = tokens }))
            using (Chart large = new Chart(ElectricalDoc(15), new ElectricalWorkTicketTemplate { Tokens = tokens }))
            {
                WorkTicketLayout smallLayout = small.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketLayout largeLayout = large.Geometry.LayoutOf<WorkTicketLayout>()!;

                Assert.True(smallLayout.MeasureRowCount == 8, "小票措施行数应为 8，实到 " + smallLayout.MeasureRowCount + "。");
                Assert.True(largeLayout.MeasureRowCount == 15, "大票措施行数应为 15，实到 " + largeLayout.MeasureRowCount + "。");
                Assert.True(smallLayout.Copy(0).MeasureGroup.Count == 8,
                    "小票行组条目数应为 8，实到 " + smallLayout.Copy(0).MeasureGroup.Count + "。");
                Assert.True(largeLayout.Copy(0).MeasureGroup.Count == 15,
                    "大票行组条目数应为 15，实到 " + largeLayout.Copy(0).MeasureGroup.Count + "。");

                float expected = (largeLayout.MeasureRowCount - smallLayout.MeasureRowCount) * rowHeight * smallLayout.Collate.Count;
                float actual = largeLayout.Bounds.Height - smallLayout.Bounds.Height;
                Assert.True(Near(actual, expected, 0.01f),
                    "行数从 8 增到 15 后整页高差应为 " + expected.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + actual.ToString("0.##", CultureInfo.InvariantCulture) + "。");

                Assert.True(Near(largeLayout.Copy(0).Measures[1].Row.Top - largeLayout.Copy(0).Measures[0].Row.Top,
                        rowHeight, 0.001f),
                    "措施行距应等于令牌 " + rowHeight.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + (largeLayout.Copy(0).Measures[1].Row.Top
                        - largeLayout.Copy(0).Measures[0].Row.Top).ToString("0.##", CultureInfo.InvariantCulture) + "。");
            }
        }

        [Fact]
        public void 电气工作票_换字段标签宽令牌改变像素落点()
        {
            ElectricalWorkTicketDocument doc = ElectricalDoc(4);
            WorkTicketGridStyle wideGrid = new WorkTicketGridStyle { FieldLabelWidth = 260f };

            using (Chart baseline = new Chart(doc, new ElectricalWorkTicketTemplate()))
            using (Chart moved = new Chart(doc, new ElectricalWorkTicketTemplate
            {
                Tokens = new ElectricalWorkTicketStyle { Grid = wideGrid },
            }))
            using (ImageProbe baselineProbe = Render(baseline))
            using (ImageProbe movedProbe = Render(moved))
            {
                WorkTicketLayout baselineLayout = baseline.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketLayout movedLayout = moved.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketGridStyle baselineGrid = ((ElectricalWorkTicketTemplate)baseline.Template).Tokens.Grid;
                WorkTicketGridStyle movedGrid = ((ElectricalWorkTicketTemplate)moved.Template).Tokens.Grid;

                WorkTicketFieldRow baselineRow = FindField(baselineLayout.Copy(0), ElectricalWorkTicketFields.WorkLeader)!;
                WorkTicketFieldRow movedRow = FindField(movedLayout.Copy(0), ElectricalWorkTicketFields.WorkLeader)!;

                SKRect baselineValue = ValueBox(baselineRow, baselineGrid);
                SKRect movedValue = ValueBox(movedRow, movedGrid);
                Assert.True(movedRow.Rect.Left == baselineRow.Rect.Left
                    && Near(movedValue.Left - baselineValue.Left, 260f - 148f, 0.001f),
                    "值列左移量应为令牌差 112，实到 "
                    + (movedValue.Left - baselineValue.Left).ToString("0.##", CultureInfo.InvariantCulture) + "。");

                Assert.True(InkIn(baselineProbe, baselineValue, 2f),
                    "默认令牌下工作负责人值应有墨，探针 " + R(baselineValue) + " 实到无墨。");
                SKRect vacated = new SKRect(baselineValue.Left + 2f, baselineValue.Top + 1f,
                    movedValue.Left - 12f, baselineValue.Bottom - 1f);
                Assert.True(vacated.Width > 20f, "旧值列让出的空白带过窄：" + R(vacated) + "。");
                Assert.True(EmptyIn(movedProbe, vacated, 0f),
                    "把标签宽改成 260 后旧值列让出的空白带 " + R(vacated) + " 实到有墨。");
                Assert.True(InkIn(movedProbe, movedValue, 2f),
                    "把标签宽改成 260 后新值列位置应有墨，探针 " + R(movedValue) + " 实到无墨。");
            }
        }

        // ------------------------------------------------------------------ 八类特殊作业票

        [Fact]
        public void 特殊作业票_八类类别副标题齐备()
        {
            int categories = HotWorkCategories.All.Count;
            Assert.True(categories == 8, "八类特殊作业类别数应为 8，实到 " + categories + "。");

            HashSet<string> labels = new HashSet<string>(StringComparer.Ordinal);
            float expectedHeight = 0f;
            for (int i = 0; i < categories; i++)
            {
                HotWorkCategory category = HotWorkCategories.All[i];
                HotWorkTicketDocument doc = HotDoc(3, 4);
                doc.Category = category;
                using (Chart chart = new Chart(doc, new HotWorkTicketTemplate()))
                {
                    WorkTicketLayout layout = chart.Geometry.LayoutOf<WorkTicketLayout>()!;
                    string label = HotWorkCategories.Label(category);
                    Assert.True(labels.Add(label), "类别中文名重复：" + label + "。");
                    Assert.True(layout.Subtitle == "作业类别：" + label,
                        "第 " + i + " 类副标题应为 作业类别：" + label + "，实到 " + layout.Subtitle + "。");
                    Assert.True(layout.Copy(0).SubtitleRect.Height > 0f,
                        "第 " + i + " 类副标题行高为 " + layout.Copy(0).SubtitleRect.Height.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                    if (i == 0)
                    {
                        expectedHeight = layout.Bounds.Height;
                    }
                    else
                    {
                        Assert.True(Near(layout.Bounds.Height, expectedHeight, 0.001f),
                            "第 " + i + " 类版面高 " + layout.Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture)
                            + " 与第一类 " + expectedHeight.ToString("0.##", CultureInfo.InvariantCulture) + " 不等。");
                    }
                }
            }
            Assert.True(labels.Count == categories, "类别中文名去重后应 " + categories
                + " 个，实到 " + labels.Count + "。");
        }

        [Fact]
        public void 特殊作业票_作业联与监护联字段差异可断言()
        {
            using (Chart chart = new Chart(HotDoc(3, 5), new HotWorkTicketTemplate()))
            {
                WorkTicketLayout layout = chart.Geometry.LayoutOf<WorkTicketLayout>()!;
                Assert.True(layout.Collate.Count == 2, "联数应为 2，实到 " + layout.Collate.Count + "。");
                Assert.True(layout.Collate.CommonFields.Count == 9,
                    "共有字段数应为 9，实到 " + layout.Collate.CommonFields.Count + "。");
                Assert.True(layout.Collate.DifferentiatingFields.Count == 2,
                    "差异字段数应为 2，实到 " + layout.Collate.DifferentiatingFields.Count + "。");
                Assert.True(layout.FieldRowCount == 11, "字段行数应为 11，实到 " + layout.FieldRowCount + "。");

                Assert.True(layout.Copy(0).ShowsField(HotWorkTicketFields.GasTest)
                    && !layout.Copy(0).ShowsField(HotWorkTicketFields.Approver),
                    "作业联应显示气体检测、不显示审批人。");
                Assert.True(layout.Copy(1).ShowsField(HotWorkTicketFields.Approver)
                    && !layout.Copy(1).ShowsField(HotWorkTicketFields.GasTest),
                    "监护联应显示审批人、不显示气体检测。");
                Assert.True(layout.Copy(0).ShowsField(HotWorkTicketFields.WorkLevel)
                    && layout.Copy(1).ShowsField(HotWorkTicketFields.WorkLevel),
                    "作业级别应两联共有。");
                Assert.True(layout.StampState == WorkTicketStampState.Executed,
                    "特殊作业票印章状态应来自文档 Executed，实到 " + layout.StampState + "。");
            }
        }

        [Fact]
        public void 特殊作业票_联间字段只在可见联落墨且红章可见()
        {
            using (Chart chart = new Chart(HotDoc(3, 5), new HotWorkTicketTemplate()))
            using (ImageProbe probe = Render(chart))
            {
                WorkTicketLayout layout = chart.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketGridStyle grid = ((HotWorkTicketTemplate)chart.Template).Tokens.Grid;

                WorkTicketFieldRow gas0 = FindField(layout.Copy(0), HotWorkTicketFields.GasTest)!;
                WorkTicketFieldRow gas1 = FindField(layout.Copy(1), HotWorkTicketFields.GasTest)!;
                WorkTicketFieldRow appr0 = FindField(layout.Copy(0), HotWorkTicketFields.Approver)!;
                WorkTicketFieldRow appr1 = FindField(layout.Copy(1), HotWorkTicketFields.Approver)!;

                Assert.True(InkIn(probe, ValueBox(gas0, grid), 2f),
                    "作业联气体检测值应有墨，探针 " + R(ValueBox(gas0, grid)) + " 实到无墨。");
                Assert.True(EmptyIn(probe, ValueBox(gas1, grid), 2f),
                    "监护联气体检测值应留白，探针 " + R(ValueBox(gas1, grid)) + " 实到有墨。");
                Assert.True(InkIn(probe, ValueBox(appr1, grid), 2f),
                    "监护联审批人值应有墨，探针 " + R(ValueBox(appr1, grid)) + " 实到无墨。");
                Assert.True(EmptyIn(probe, ValueBox(appr0, grid), 2f),
                    "作业联审批人值应留白，探针 " + R(ValueBox(appr0, grid)) + " 实到有墨。");

                WorkTicketCopyLayout copy = layout.Copy(0);
                Assert.True(probe.HasRedInBox((int)copy.StampBounds.Left, (int)copy.StampBounds.Top,
                        (int)copy.StampBounds.Right, (int)copy.StampBounds.Bottom),
                    "已执行章在 " + R(copy.StampBounds) + " 内没有红墨。");

                SKRect hidden = gas1.Rect;
                Assert.True(InkIn(probe, new SKRect(hidden.Left + 2f, hidden.Bottom - 1f,
                        hidden.Right - 2f, hidden.Bottom + 1f), 0f),
                    "监护联气体检测行的底线不存在，探针 " + R(hidden) + " 附近无墨。");
            }
        }

        [Fact]
        public void 特殊作业票_危害辨识条目增加抬高版面()
        {
            HotWorkTicketStyle tokens = new HotWorkTicketStyle();
            float rowHeight = tokens.Grid.SecondaryRowHeight;

            using (Chart small = new Chart(HotDoc(3, 5), new HotWorkTicketTemplate { Tokens = tokens }))
            using (Chart large = new Chart(HotDoc(6, 5), new HotWorkTicketTemplate { Tokens = tokens }))
            {
                WorkTicketLayout smallLayout = small.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketLayout largeLayout = large.Geometry.LayoutOf<WorkTicketLayout>()!;

                Assert.True(smallLayout.SecondaryRowCount == 3, "3 条危害时应为 3 行，实到 " + smallLayout.SecondaryRowCount + "。");
                Assert.True(largeLayout.SecondaryRowCount == 6, "6 条危害时应为 6 行，实到 " + largeLayout.SecondaryRowCount + "。");
                Assert.True(smallLayout.Copy(0).SecondaryGroup != null && smallLayout.Copy(0).SecondaryGroup!.Count == 3,
                    "小票危害行组条目数不是 3。");
                Assert.True(largeLayout.Copy(0).SecondaryGroup != null && largeLayout.Copy(0).SecondaryGroup!.Count == 6,
                    "大票危害行组条目数不是 6。");

                float expected = (largeLayout.SecondaryRowCount - smallLayout.SecondaryRowCount)
                    * rowHeight * largeLayout.Collate.Count;
                float actual = largeLayout.Bounds.Height - smallLayout.Bounds.Height;
                Assert.True(Near(actual, expected, 0.01f),
                    "危害条目从 3 增到 6 后整页高差应为 " + expected.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + actual.ToString("0.##", CultureInfo.InvariantCulture) + "。");

                Assert.True(Near(largeLayout.Copy(0).SecondaryRows[1].Row.Top
                        - largeLayout.Copy(0).SecondaryRows[0].Row.Top, rowHeight, 0.001f),
                    "危害行距应等于令牌 " + rowHeight.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + (largeLayout.Copy(0).SecondaryRows[1].Row.Top
                        - largeLayout.Copy(0).SecondaryRows[0].Row.Top).ToString("0.##", CultureInfo.InvariantCulture) + "。");
            }
        }

        [Fact]
        public void 特殊作业票_换签署行高令牌改变文字落点()
        {
            HotWorkTicketDocument doc = HotDoc(3, 5);
            using (Chart baseline = new Chart(doc, new HotWorkTicketTemplate()))
            using (Chart moved = new Chart(doc, new HotWorkTicketTemplate
            {
                Tokens = new HotWorkTicketStyle { Grid = new WorkTicketGridStyle { SignatureHeight = 60f } },
            }))
            using (ImageProbe baselineProbe = Render(baseline))
            using (ImageProbe movedProbe = Render(moved))
            {
                WorkTicketLayout baselineLayout = baseline.Geometry.LayoutOf<WorkTicketLayout>()!;
                WorkTicketLayout movedLayout = moved.Geometry.LayoutOf<WorkTicketLayout>()!;
                SKRect baselineCell = baselineLayout.Copy(0).SignatureCells[0];
                SKRect movedCell = movedLayout.Copy(0).SignatureCells[0];

                float expectedShift = (60f - 30f) * 0.5f;
                Assert.True(Near(movedCell.MidY - baselineCell.MidY, expectedShift, 0.001f),
                    "签署行文字中心应下移 " + expectedShift.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + (movedCell.MidY - baselineCell.MidY).ToString("0.##", CultureInfo.InvariantCulture) + "。");

                SKRect oldText = new SKRect(baselineCell.Left + 2f, baselineCell.MidY - 3f,
                    baselineCell.Left + 70f, baselineCell.MidY + 3f);
                SKRect newText = new SKRect(movedCell.Left + 2f, movedCell.MidY - 3f,
                    movedCell.Left + 70f, movedCell.MidY + 3f);

                Assert.True(InkIn(baselineProbe, oldText, 0f),
                    "默认签署行在 " + R(oldText) + " 应有墨。");
                Assert.True(EmptyIn(movedProbe, oldText, 0f),
                    "签署行高改为 60 后旧落点 " + R(oldText) + " 应变白。");
                Assert.True(InkIn(movedProbe, newText, 0f),
                    "签署行高改为 60 后新落点 " + R(newText) + " 应有墨。");
            }
        }

        // ------------------------------------------------------------------ 增值税专用发票

        [Fact]
        public void 发票_三联与四联开关切换联数与附加字段()
        {
            using (Chart three = new Chart(VatDoc(3), new VatInvoiceTemplate()))
            using (Chart four = new Chart(VatDoc(4), new VatInvoiceTemplate()))
            {
                VatInvoiceLayout l3 = three.Geometry.LayoutOf<VatInvoiceLayout>()!;
                VatInvoiceLayout l4 = four.Geometry.LayoutOf<VatInvoiceLayout>()!;

                Assert.True(l3.CopyCount == 3, "3 联模式联数应为 3，实到 " + l3.CopyCount + "。");
                Assert.True(l4.CopyCount == 4, "4 联模式联数应为 4，实到 " + l4.CopyCount + "。");
                Assert.True(l3.Collate.Direction == CollateDirection.VerticalStack,
                    "发票切联方向应为纵叠，实到 " + l3.Collate.Direction + "。");
                Assert.True(l3.Collate.DifferentiatingFields.Count == 3,
                    "3 联模式差异字段数应为 3，实到 " + l3.Collate.DifferentiatingFields.Count + "。");
                Assert.True(l4.Collate.DifferentiatingFields.Count == 4,
                    "4 联模式差异字段数应为 4，实到 " + l4.Collate.DifferentiatingFields.Count + "。");
                Assert.True(l3.Collate.CommonFields.Count == VatInvoiceSheet.CommonFieldKeys.Length,
                    "共有字段数应为 " + VatInvoiceSheet.CommonFieldKeys.Length + "，实到 "
                    + l3.Collate.CommonFields.Count + "。");
                Assert.True(l3.Collate.AllFields.Count == VatInvoiceSheet.CommonFieldKeys.Length + 3,
                    "3 联模式字段全集应为 " + (VatInvoiceSheet.CommonFieldKeys.Length + 3)
                    + "，实到 " + l3.Collate.AllFields.Count + "。");
                Assert.True(l4.Collate.AllFields.Count == VatInvoiceSheet.CommonFieldKeys.Length + 4,
                    "4 联模式字段全集应为 " + (VatInvoiceSheet.CommonFieldKeys.Length + 4)
                    + "，实到 " + l4.Collate.AllFields.Count + "。");

                Assert.True(Near(l3.Bounds.Height, l3.CopyHeight * 3f, 0.001f),
                    "3 联版面高应为单联高 ×3，实到 " + l3.Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture)
                    + " / 单联 " + l3.CopyHeight.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                Assert.True(Near(l4.Bounds.Height, l4.CopyHeight * 4f, 0.001f),
                    "4 联版面高应为单联高 ×4，实到 " + l4.Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture)
                    + " / 单联 " + l4.CopyHeight.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                Assert.True(l4.Bounds.Height > l3.Bounds.Height,
                    "4 联版面高应大于 3 联，实到 4 联 " + l4.Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture)
                    + "、3 联 " + l3.Bounds.Height.ToString("0.##", CultureInfo.InvariantCulture) + "。");

                Assert.True(l3.Copy(0).CopyLabel == VatInvoiceDocument.RoleLabel(VatCopyRole.Accounting)
                    && l3.Copy(1).CopyLabel == VatInvoiceDocument.RoleLabel(VatCopyRole.Deduction)
                    && l3.Copy(2).CopyLabel == VatInvoiceDocument.RoleLabel(VatCopyRole.Invoice),
                    "3 联联名顺序不是 记账/抵扣/发票。");
                Assert.True(l4.Copy(0).CopyLabel == VatInvoiceDocument.RoleLabel(VatCopyRole.Stub)
                    && l4.Copy(3).CopyLabel == VatInvoiceDocument.RoleLabel(VatCopyRole.Invoice),
                    "4 联联名顺序不是 存根…发票，实到 " + l4.Copy(0).CopyLabel + "…" + l4.Copy(3).CopyLabel + "。");

                Assert.True(l3.Copy(0).ItemRows.Count == Math.Max(2, VatDoc(3).Items.Count),
                    "3 联明细行数应为 max(2, 3) = 3，实到 " + l3.Copy(0).ItemRows.Count + "。");
            }
        }

        [Fact]
        public void 发票_联间附加字段只在本联有墨()
        {
            using (Chart chart = new Chart(VatDoc(4), new VatInvoiceTemplate()))
            using (ImageProbe probe = Render(chart))
            {
                VatInvoiceLayout layout = chart.Geometry.LayoutOf<VatInvoiceLayout>()!;
                Assert.True(layout.CopyCount == 4, "本用例需要 4 联，实到 " + layout.CopyCount + "。");

                for (int c = 0; c < layout.CopyCount; c++)
                {
                    VatCopyLayout copy = layout.Copy(c);
                    Assert.True(copy.CopyFieldRows.Count == 4,
                        "第 " + c + " 联附加字段行数应为 4，实到 " + copy.CopyFieldRows.Count + "。");

                    for (int r = 0; r < copy.CopyFieldRows.Count; r++)
                    {
                        VatCopyFieldRowLayout row = copy.CopyFieldRows[r];
                        bool own = r == c;
                        Assert.True(row.Shown == own,
                            "第 " + c + " 联第 " + r + " 行 Shown 应为 " + own + "，实到 " + row.Shown + "。");

                        Assert.True(InkIn(probe, row.Row, 0f),
                            "第 " + c + " 联第 " + r + " 行整行应含底线（正控），探针 " + R(row.Row) + " 无墨。");
                        if (own)
                        {
                            Assert.True(InkIn(probe, row.ValueRect, 2f),
                                "第 " + c + " 联自身附加字段 " + row.Label + " 应有墨，探针 "
                                + R(row.ValueRect) + " 无墨。");
                        }
                        else
                        {
                            Assert.True(EmptyIn(probe, row.ValueRect, 2f),
                                "第 " + c + " 联不应出现第 " + r + " 行 " + row.Label + " 的值，探针 "
                                + R(row.ValueRect) + " 实到有墨。");
                        }
                    }
                }
            }
        }

        [Fact]
        public void 发票_密码区四行二十一格且短密文留白()
        {
            using (Chart chart = new Chart(VatDoc(3), new VatInvoiceTemplate()))
            using (ImageProbe probe = Render(chart))
            {
                VatInvoiceLayout layout = chart.Geometry.LayoutOf<VatInvoiceLayout>()!;
                VatCopyLayout copy = layout.Copy(0);
                VatInvoiceStyle tokens = ((VatInvoiceTemplate)chart.Template).Tokens;
                string cipher = layout.CipherText ?? string.Empty;

                Assert.True(copy.CipherCells.Count == 84,
                    "密码区格数应为 4×21=84，实到 " + copy.CipherCells.Count + "。");
                Assert.True(tokens.CipherColumns == 21 && tokens.CipherRows == 4,
                    "密码区令牌应为 4 行 × 21 列，实到 " + tokens.CipherRows + "×" + tokens.CipherColumns + "。");

                for (int i = 1; i < copy.CipherCells.Count; i++)
                {
                    if (i % tokens.CipherColumns == 0)
                    {
                        continue;
                    }
                    Assert.True(copy.CipherCells[i].Left > copy.CipherCells[i - 1].Left,
                        "第 " + i + " 格未排在前一格右侧：" + R(copy.CipherCells[i - 1])
                        + " / " + R(copy.CipherCells[i]) + "。");
                }
                Assert.True(Near(copy.CipherCells[20].Bottom, copy.CipherCells[21].Top, 0.001f),
                    "第 0 行末格下缘 " + copy.CipherCells[20].Bottom.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 与第 1 行首格上缘 " + copy.CipherCells[21].Top.ToString("0.##", CultureInfo.InvariantCulture) + " 不接。");

                int filled = Math.Min(cipher.Length, copy.CipherCells.Count);
                Assert.True(filled == 30, "样例密文长度应为 30，实到 " + filled + "。");

                SKRect first = copy.CipherCells[0];
                SKRect blank = copy.CipherCells[filled];
                Assert.True(InkIn(probe, first, 2f),
                    "第 0 格应落到密文字符，探针 " + R(first) + " 无墨。");
                Assert.True(InkIn(probe, blank, 0f),
                    "第 " + filled + " 格应仍有格线（正控），探针 " + R(blank) + " 无墨。");
                Assert.True(EmptyIn(probe, blank, 2f),
                    "密文只有 " + filled + " 位，第 " + filled + " 格内部应留白，探针 " + R(blank) + " 实到有墨。");

                SKRect last = copy.CipherCells[copy.CipherCells.Count - 1];
                Assert.True(EmptyIn(probe, last, 2f),
                    "第 83 格内部应留白，探针 " + R(last) + " 实到有墨。");

                for (int i = 0; i < copy.CipherCells.Count; i++)
                {
                    Assert.True(copy.CipherCells[i].Left >= copy.Inner.Left - 0.001f
                        && copy.CipherCells[i].Right <= copy.Inner.Right + 0.001f
                        && copy.CipherCells[i].Top >= copy.Inner.Top - 0.001f
                        && copy.CipherCells[i].Bottom <= copy.Inner.Bottom + 0.001f,
                        "第 " + i + " 个密码格越出内容区：" + R(copy.CipherCells[i])
                        + " / 内容区 " + R(copy.Inner) + "。");
                }
            }
        }

        [Fact]
        public void 发票_八格发票号码逐格落字()
        {
            using (Chart chart = new Chart(VatDoc(3), new VatInvoiceTemplate()))
            using (ImageProbe probe = Render(chart))
            {
                VatInvoiceLayout layout = chart.Geometry.LayoutOf<VatInvoiceLayout>()!;
                VatCopyLayout copy = layout.Copy(0);
                VatInvoiceStyle tokens = ((VatInvoiceTemplate)chart.Template).Tokens;

                Assert.True(copy.NumberCells.Count == 8,
                    "发票号码格数应为 8，实到 " + copy.NumberCells.Count + "。");
                Assert.True(layout.InvoiceNumber != null && layout.InvoiceNumber!.Length == 8,
                    "样例号码长度应为 8，实到 " + (layout.InvoiceNumber ?? "<null>") + "。");

                float width = tokens.InvoiceNumberBoxSize * tokens.InvoiceNumberBoxWidthFactor;
                for (int i = 0; i < copy.NumberCells.Count; i++)
                {
                    SKRect cell = copy.NumberCells[i];
                    Assert.True(Near(cell.Width, width, 0.001f),
                        "第 " + i + " 个号码格宽应为 " + width.ToString("0.###", CultureInfo.InvariantCulture)
                        + "，实到 " + cell.Width.ToString("0.###", CultureInfo.InvariantCulture) + "。");
                    Assert.True(InkIn(probe, cell, 2f),
                        "第 " + i + " 个号码格应落到字符，探针 " + R(cell) + " 无墨。");
                }

                Assert.True(Near(copy.NumberGridRect.Width, width * 8f, 0.01f),
                    "号码整排宽应为 " + (width * 8f).ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + copy.NumberGridRect.Width.ToString("0.##", CultureInfo.InvariantCulture) + "。");
            }
        }

        [Fact]
        public void 发票_大写金额压印不越格不压邻格()
        {
            VatInvoiceStyle tokens = new VatInvoiceStyle
            {
                PressedBaseSize = 80f,
                PressedMinSize = 6f,
            };
            using (Chart chart = new Chart(VatDoc(3), new VatInvoiceTemplate { Tokens = tokens }))
            using (ImageProbe probe = Render(chart))
            {
                VatInvoiceLayout layout = chart.Geometry.LayoutOf<VatInvoiceLayout>()!;
                VatCopyLayout copy = layout.Copy(0);
                PressedAmount pressed = copy.Pressed;

                Assert.True(pressed.Text == layout.UppercaseAmount,
                    "压印文字应等于版面大写金额 " + layout.UppercaseAmount + "，实到 " + pressed.Text + "。");
                Assert.True(pressed.NaturalWidth > pressed.AvailableWidth,
                    "名义字号下的自然宽 " + pressed.NaturalWidth.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 未超过可用宽 " + pressed.AvailableWidth.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，压印前提不成立。");
                Assert.True(pressed.Compressed,
                    "自然宽已超出可用宽，但 Compressed 仍为 false。");
                Assert.True(pressed.FontSize < pressed.BaseSize,
                    "压印字号 " + pressed.FontSize.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 应小于名义字号 " + pressed.BaseSize.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                Assert.True(pressed.CharCells.Count == pressed.Text.Length,
                    "字格数 " + pressed.CharCells.Count + " 与字数 " + pressed.Text.Length + " 不等。");

                for (int i = 0; i < pressed.CharCells.Count; i++)
                {
                    SKRect cell = pressed.CharCells[i];
                    Assert.True(cell.Left >= copy.UpperAmountRect.Left + tokens.AmountPaddingX - 0.001f
                        && cell.Right <= copy.UpperAmountRect.Right - tokens.AmountPaddingX + 0.001f,
                        "第 " + i + " 个字格越出大写格：" + R(cell) + " / 大写格 " + R(copy.UpperAmountRect) + "。");
                    Assert.True(cell.Right <= copy.AmountGutterRect.Left + 0.001f,
                        "第 " + i + " 个字格压到空槽：" + R(cell) + " / 空槽 " + R(copy.AmountGutterRect) + "。");
                    Assert.True(!Intersects(cell, copy.LowerAmountRect),
                        "第 " + i + " 个字格与小写格相交：" + R(cell) + " / 小写格 " + R(copy.LowerAmountRect) + "。");
                }
                Assert.True(pressed.InkRight <= copy.AmountGutterRect.Left - tokens.AmountPaddingX + 0.001f,
                    "压印右缘 " + pressed.InkRight.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 越过空槽左缘 " + copy.AmountGutterRect.Left.ToString("0.##", CultureInfo.InvariantCulture) + "。");

                Assert.True(EmptyIn(probe, copy.AmountGutterRect, 1.5f),
                    "大写格与小写格之间的空槽应无墨，探针 " + R(copy.AmountGutterRect) + " 实到有墨。");
                Assert.True(InkIn(probe, copy.UpperAmountRect, 5f),
                    "大写格内应有压印墨，探针 " + R(copy.UpperAmountRect) + " 实到无墨。");
                Assert.True(InkIn(probe, copy.LowerAmountRect, 5f),
                    "小写格内应有金额墨（正控），探针 " + R(copy.LowerAmountRect) + " 实到无墨。");
            }
        }

        [Fact]
        public void 发票_合计由明细算出且压印文字与版面一致()
        {
            VatInvoiceDocument doc = VatDoc(3);
            using (Chart chart = new Chart(doc, new VatInvoiceTemplate()))
            {
                VatInvoiceLayout layout = chart.Geometry.LayoutOf<VatInvoiceLayout>()!;

                double expectedAmount = 0d;
                double expectedTax = 0d;
                foreach (VatInvoiceItem item in doc.Items)
                {
                    double amount = Math.Round(item.Quantity * item.UnitPrice, 2, MidpointRounding.AwayFromZero);
                    expectedAmount += amount;
                    expectedTax += Math.Round(amount * item.TaxRate, 2, MidpointRounding.AwayFromZero);
                }
                expectedAmount = Math.Round(expectedAmount, 2, MidpointRounding.AwayFromZero);
                expectedTax = Math.Round(expectedTax, 2, MidpointRounding.AwayFromZero);

                Assert.True(Near((float)layout.AmountTotal, (float)expectedAmount, 0.0001f),
                    "金额合计应为 " + expectedAmount.ToString("0.00", CultureInfo.InvariantCulture)
                    + "，实到 " + layout.AmountTotal.ToString("0.00", CultureInfo.InvariantCulture) + "。");
                Assert.True(Near((float)layout.TaxTotal, (float)expectedTax, 0.0001f),
                    "税额合计应为 " + expectedTax.ToString("0.00", CultureInfo.InvariantCulture)
                    + "，实到 " + layout.TaxTotal.ToString("0.00", CultureInfo.InvariantCulture) + "。");
                Assert.True(Near((float)layout.GrandTotal, (float)(expectedAmount + expectedTax), 0.0001f),
                    "价税合计应为 " + (expectedAmount + expectedTax).ToString("0.00", CultureInfo.InvariantCulture)
                    + "，实到 " + layout.GrandTotal.ToString("0.00", CultureInfo.InvariantCulture) + "。");
                Assert.True(layout.UppercaseAmount == doc.UppercaseAmount
                    && layout.Copy(0).Pressed.Text == doc.UppercaseAmount,
                    "版面大写金额与文档不一致：" + layout.UppercaseAmount + " / " + doc.UppercaseAmount + "。");
                Assert.True(layout.UppercaseAmount.Length > 0 && layout.UppercaseAmount.EndsWith("分", StringComparison.Ordinal),
                    "样例总额含角分，大写应以 分 结尾，实到 " + layout.UppercaseAmount + "。");
            }
        }

        [Fact]
        public void 发票_换备注行高令牌改变签署落点()
        {
            VatInvoiceDocument doc = VatDoc(3);
            using (Chart baseline = new Chart(doc, new VatInvoiceTemplate()))
            using (Chart moved = new Chart(doc, new VatInvoiceTemplate
            {
                Tokens = new VatInvoiceStyle { RemarkHeight = 40f },
            }))
            using (ImageProbe baselineProbe = Render(baseline))
            using (ImageProbe movedProbe = Render(moved))
            {
                VatInvoiceLayout baselineLayout = baseline.Geometry.LayoutOf<VatInvoiceLayout>()!;
                VatInvoiceLayout movedLayout = moved.Geometry.LayoutOf<VatInvoiceLayout>()!;

                VatCopyLayout baselineCopy = baselineLayout.Copy(0);
                VatCopyLayout movedCopy = movedLayout.Copy(0);
                float expectedShift = 40f - 18f;

                Assert.True(Near(movedCopy.FooterRect.Top - baselineCopy.FooterRect.Top, expectedShift, 0.001f),
                    "签署行上缘应下移 " + expectedShift.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实到 " + (movedCopy.FooterRect.Top - baselineCopy.FooterRect.Top).ToString("0.##", CultureInfo.InvariantCulture) + "。");
                Assert.True(Near(movedLayout.Bounds.Height - baselineLayout.Bounds.Height,
                        expectedShift * baselineLayout.CopyCount, 0.001f),
                    "整页高差应为下移量 × 联数，实到 "
                    + (movedLayout.Bounds.Height - baselineLayout.Bounds.Height).ToString("0.##", CultureInfo.InvariantCulture) + "。");

                SKRect oldText = new SKRect(baselineCopy.FooterCells[2].Left + 3f,
                    baselineCopy.FooterCells[2].MidY - 3f,
                    baselineCopy.FooterCells[2].Left + 80f, baselineCopy.FooterCells[2].MidY + 3f);
                SKRect newText = new SKRect(movedCopy.FooterCells[2].Left + 3f,
                    movedCopy.FooterCells[2].MidY - 3f,
                    movedCopy.FooterCells[2].Left + 80f, movedCopy.FooterCells[2].MidY + 3f);

                Assert.True(InkIn(baselineProbe, oldText, 0f),
                    "默认签署行在 " + R(oldText) + " 应有墨。");
                Assert.True(EmptyIn(movedProbe, oldText, 0f),
                    "备注行高改为 40 后旧签署落点 " + R(oldText) + " 应变白。");
                Assert.True(InkIn(movedProbe, newText, 0f),
                    "备注行高改为 40 后新签署落点 " + R(newText) + " 应有墨。");
            }
        }

        // ------------------------------------------------------------------ 三票共性

        [Fact]
        public void 三张票据_Measure可重复且不改文档()
        {
            ChartStyle style = new ChartStyle();

            ElectricalWorkTicketDocument electrical = ElectricalDoc(5);
            ElectricalWorkTicketTemplate electricalTemplate = new ElectricalWorkTicketTemplate();
            WorkTicketLayout e1 = electricalTemplate.Measure(electrical, style, Chart.SharedFonts)
                .LayoutOf<WorkTicketLayout>()!;
            WorkTicketLayout e2 = electricalTemplate.Measure(electrical, style, Chart.SharedFonts)
                .LayoutOf<WorkTicketLayout>()!;
            Assert.True(e1.Bounds == e2.Bounds && e1.Copy(0).Fields[0].Rect == e2.Copy(0).Fields[0].Rect,
                "电气工作票两次 Measure 的版面不一致："
                + R(e1.Copy(0).Fields[0].Rect) + " / " + R(e2.Copy(0).Fields[0].Rect) + "。");
            Assert.True(electrical.Field(ElectricalWorkTicketFields.WorkLeader)?.Value == "张三"
                && electrical.Measures.Count == 5,
                "电气工作票 Measure 改了文档：负责人 "
                + (electrical.Field(ElectricalWorkTicketFields.WorkLeader)?.Value ?? "<null>")
                + "、措施数 " + electrical.Measures.Count + "。");

            HotWorkTicketDocument hot = HotDoc(3, 5);
            HotWorkTicketTemplate hotTemplate = new HotWorkTicketTemplate();
            WorkTicketLayout h1 = hotTemplate.Measure(hot, style, Chart.SharedFonts).LayoutOf<WorkTicketLayout>()!;
            WorkTicketLayout h2 = hotTemplate.Measure(hot, style, Chart.SharedFonts).LayoutOf<WorkTicketLayout>()!;
            Assert.True(h1.Bounds == h2.Bounds && h1.Copy(0).SecondaryRows[0].Row == h2.Copy(0).SecondaryRows[0].Row,
                "特殊作业票两次 Measure 的版面不一致。");
            Assert.True(hot.Hazards.Count == 3 && hot.Measures.Count == 5,
                "特殊作业票 Measure 改了文档：危害 " + hot.Hazards.Count + "、措施 " + hot.Measures.Count + "。");

            VatInvoiceDocument vat = VatDoc(4);
            VatInvoiceTemplate vatTemplate = new VatInvoiceTemplate();
            VatInvoiceLayout v1 = vatTemplate.Measure(vat, style, Chart.SharedFonts).LayoutOf<VatInvoiceLayout>()!;
            VatInvoiceLayout v2 = vatTemplate.Measure(vat, style, Chart.SharedFonts).LayoutOf<VatInvoiceLayout>()!;
            Assert.True(v1.Bounds == v2.Bounds
                && v1.Copy(0).Pressed.FontSize == v2.Copy(0).Pressed.FontSize
                && v1.Copy(0).NumberCells[0] == v2.Copy(0).NumberCells[0],
                "发票两次 Measure 的版面不一致。");
            Assert.True(vat.Items.Count == 3 && vat.CipherText != null && vat.CipherText!.Length == 30,
                "发票 Measure 改了文档：明细 " + vat.Items.Count + "、密文长 "
                + (vat.CipherText == null ? "<null>" : vat.CipherText!.Length.ToString(CultureInfo.InvariantCulture)) + "。");
        }

        [Fact]
        public void 三张票据_真实光栅产物都有墨()
        {
            using (Chart electrical = new Chart(ElectricalDoc(6), new ElectricalWorkTicketTemplate()))
            using (Chart hot = new Chart(HotDoc(3, 5), new HotWorkTicketTemplate()))
            using (Chart vat = new Chart(VatDoc(3), new VatInvoiceTemplate()))
            {
                AssertRendered(electrical, "电气第一种工作票");
                AssertRendered(hot, "八类特殊作业票");
                AssertRendered(vat, "增值税专用发票");
            }
        }

        private static void AssertRendered(Chart chart, string name)
        {
            using (ImageProbe probe = Render(chart))
            {
                Assert.True(probe.Width > 100 && probe.Height > 100,
                    name + " 的光栅尺寸过小：" + probe.Width + "×" + probe.Height + "。");
                Assert.True(probe.Width >= (int)chart.Geometry.Bounds.Width,
                    name + " 的光栅宽 " + probe.Width + " 小于版面宽 "
                    + chart.Geometry.Bounds.Width.ToString("0.##", CultureInfo.InvariantCulture) + "。");
                double ratio = probe.NonWhiteRatio(chart.Geometry.Bounds);
                Assert.True(ratio > 0.005d,
                    name + " 的整页非白占比过低：" + ratio.ToString("0.0000", CultureInfo.InvariantCulture) + "。");

                SKRect titleBand = new SKRect(0f, 0f, chart.Geometry.Bounds.Width,
                    Math.Min(48f, chart.Geometry.Bounds.Height));
                Assert.True(InkIn(probe, titleBand, 1f),
                    name + " 的票名带 " + R(titleBand) + " 没有墨。");
            }
        }

        private static bool Intersects(SKRect a, SKRect b)
        {
            return a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
        }

        private static string CopyKeys(WorkTicketLayout layout)
        {
            List<string> keys = new List<string>(layout.Copies.Count);
            foreach (WorkTicketCopyLayout copy in layout.Copies)
            {
                keys.Add(copy.CopyKey);
            }
            return string.Join(",", keys);
        }
    }

    internal static class WorkTicketCopyAssertions
    {
        public static bool ShowsField(this WorkTicketCopyLayout copy, string key)
        {
            foreach (WorkTicketFieldRow row in copy.Fields)
            {
                if (string.Equals(row.Key, key, StringComparison.Ordinal))
                {
                    return row.Shown;
                }
            }
            return false;
        }

        public static bool ShowsNone(this WorkTicketCopyLayout copy, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (copy.ShowsField(key))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
