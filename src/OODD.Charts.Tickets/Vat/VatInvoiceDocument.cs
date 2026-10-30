using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OODD.Charts.Model;

namespace OODD.Charts.Tickets.Vat
{
    /// <summary>
    /// 增值税专用发票的一个明细行。金额与税额由数量 × 单价、金额 × 税率算出，
    /// 只做两位小数四舍五入，<b>不</b>内置任何税率表或货物分类表（docs/06 §5 D21）。
    /// </summary>
    public sealed class VatInvoiceItem
    {
        /// <summary>货物或应税劳务名称。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>规格型号。</summary>
        public string Specification { get; set; } = string.Empty;

        /// <summary>单位。</summary>
        public string Unit { get; set; } = string.Empty;

        /// <summary>数量。</summary>
        public double Quantity { get; set; }

        /// <summary>单价（不含税）。</summary>
        public double UnitPrice { get; set; }

        /// <summary>税率（0.13 表示 13%）。由调用方给出，库不推断。</summary>
        public double TaxRate { get; set; }

        /// <summary>金额 = 数量 × 单价，保留两位小数。</summary>
        public double Amount
        {
            get { return Math.Round(Quantity * UnitPrice, 2, MidpointRounding.AwayFromZero); }
        }

        /// <summary>税额 = 金额 × 税率，保留两位小数。</summary>
        public double TaxAmount
        {
            get { return Math.Round(Amount * TaxRate, 2, MidpointRounding.AwayFromZero); }
        }

        /// <summary>按 InvariantCulture 输出两位小数，跨 TFM 逐字一致。</summary>
        public static string Money(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        /// <summary>按 InvariantCulture 输出税率百分数（0.13 → "13%"）。</summary>
        public static string Percent(double value)
        {
            return (value * 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
        }
    }

    /// <summary>
    /// 发票的联次角色。3 联 = 记账 / 抵扣 / 发票；4 联加存根。
    /// 联次划分是结构样例，不代表任何税务票样的法定联次（D22）。
    /// </summary>
    public enum VatCopyRole
    {
        /// <summary>存根联（仅 4 联模式）。</summary>
        Stub = 0,

        /// <summary>记账联。</summary>
        Accounting = 1,

        /// <summary>抵扣联。</summary>
        Deduction = 2,

        /// <summary>发票联。</summary>
        Invoice = 3,
    }

    /// <summary>联次独有的附加字段：键、标签、值。联间差异只落在这一行上。</summary>
    public sealed class VatCopyField
    {
        /// <summary>构造附加字段。</summary>
        /// <param name="key">稳定键。</param>
        /// <param name="label">标签。</param>
        /// <param name="value">值（可空）。</param>
        public VatCopyField(string key, string label, string? value)
        {
            Key = key;
            Label = label;
            Value = value;
        }

        /// <summary>稳定键。</summary>
        public string Key { get; }

        /// <summary>标签。</summary>
        public string Label { get; }

        /// <summary>值。</summary>
        public string? Value { get; }
    }

    /// <summary>
    /// 增值税专用发票文档 finance.vat-invoice。
    /// <para>
    /// 3/4 联开关由 <see cref="CopyCount"/> 决定：3 联（记账 / 抵扣 / 发票），4 联再加存根。
    /// 每联只显示自己的附加字段，其余联的附加字段行留空——联间差异因此既是几何断言也是像素断言。
    /// </para>
    /// <para>
    /// <b>D22</b>：票面栏位、联名、密码区格位都是结构样例，未按真实发票票样校准。
    /// 本类不校验发票真伪、不做任何税务合规判定。
    /// </para>
    /// </summary>
    public sealed class VatInvoiceDocument : ChartDocument
    {
        /// <summary>构造一张空白发票（3 联，明细为空）。</summary>
        public VatInvoiceDocument()
        {
            FormTitle = "增值税专用发票";
            Items = new List<VatInvoiceItem>();
            CopyCount = 3;
        }

        /// <summary>联数开关：只接受 3 或 4（模板在 Measure 时校验）。</summary>
        public int CopyCount { get; set; }

        /// <summary>发票代码。</summary>
        public string? InvoiceCode { get; set; }

        /// <summary>发票号码（8 位数字；模板用 8 格 DigitBox 落位）。</summary>
        public string? InvoiceNumber { get; set; }

        /// <summary>开票日期；null 时日期栏留空。</summary>
        public DateTime? InvoiceDate { get; set; }

        /// <summary>密码区密文（最多 84 个字符，4 行 × 21 格）。</summary>
        public string? CipherText { get; set; }

        public string? BuyerName { get; set; }
        public string? BuyerTaxId { get; set; }
        public string? BuyerAddressPhone { get; set; }
        public string? BuyerBankAccount { get; set; }

        public string? SellerName { get; set; }
        public string? SellerTaxId { get; set; }
        public string? SellerAddressPhone { get; set; }
        public string? SellerBankAccount { get; set; }

        /// <summary>明细行。行数决定明细区的高度（不足最小行数时补空行）。</summary>
        public IList<VatInvoiceItem> Items { get; }

        /// <summary>备注。</summary>
        public string? Remark { get; set; }

        public string? Payee { get; set; }
        public string? Reviewer { get; set; }
        public string? Drawer { get; set; }

        /// <summary>存根联附加字段（4 联模式）。</summary>
        public string? StubNo { get; set; }

        /// <summary>记账联附加字段。</summary>
        public string? VoucherNo { get; set; }

        /// <summary>抵扣联附加字段。</summary>
        public string? DeductionPeriod { get; set; }

        /// <summary>发票联附加字段。</summary>
        public string? Reimburser { get; set; }

        /// <summary>金额合计（不含税）。</summary>
        public double AmountTotal
        {
            get
            {
                double sum = 0d;
                foreach (VatInvoiceItem item in Items)
                {
                    sum += item.Amount;
                }
                return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>税额合计。</summary>
        public double TaxTotal
        {
            get
            {
                double sum = 0d;
                foreach (VatInvoiceItem item in Items)
                {
                    sum += item.TaxAmount;
                }
                return Math.Round(sum, 2, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>价税合计。</summary>
        public double GrandTotal
        {
            get { return Math.Round(AmountTotal + TaxTotal, 2, MidpointRounding.AwayFromZero); }
        }

        /// <summary>价税合计的中文大写；未填明细时为"零元整"。</summary>
        public string UppercaseAmount
        {
            get { return VatAmounts.ToChineseUpper(GrandTotal); }
        }

        /// <summary>开票日期的票面文字（yyyy年MM月dd日）；未填时为 null。</summary>
        public string? InvoiceDateText
        {
            get
            {
                return InvoiceDate == null
                    ? null
                    : InvoiceDate.Value.ToString("yyyy年MM月dd日", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>当前联数对应的联次角色（3 联：记账 / 抵扣 / 发票；4 联：存根 + 三者）。</summary>
        public IReadOnlyList<VatCopyRole> Roles
        {
            get
            {
                if (CopyCount == 4)
                {
                    return new[]
                    {
                        VatCopyRole.Stub, VatCopyRole.Accounting, VatCopyRole.Deduction, VatCopyRole.Invoice,
                    };
                }
                return new[] { VatCopyRole.Accounting, VatCopyRole.Deduction, VatCopyRole.Invoice };
            }
        }

        /// <summary>取联次角色对应的联名。</summary>
        /// <param name="role">联次角色。</param>
        public static string RoleLabel(VatCopyRole role)
        {
            switch (role)
            {
                case VatCopyRole.Stub: return "存根联";
                case VatCopyRole.Accounting: return "记账联";
                case VatCopyRole.Deduction: return "抵扣联";
                case VatCopyRole.Invoice: return "发票联";
                default: throw new ArgumentOutOfRangeException(nameof(role), "未知联次角色：" + role + "。");
            }
        }

        /// <summary>取联次角色对应的附加字段定义（键、标签、值）。</summary>
        /// <param name="role">联次角色。</param>
        public VatCopyField CopyField(VatCopyRole role)
        {
            switch (role)
            {
                case VatCopyRole.Stub:
                    return new VatCopyField("stub-no", "存根编号", StubNo);
                case VatCopyRole.Accounting:
                    return new VatCopyField("voucher-no", "记账凭证号", VoucherNo);
                case VatCopyRole.Deduction:
                    return new VatCopyField("deduction-period", "抵扣认证期", DeductionPeriod);
                case VatCopyRole.Invoice:
                    return new VatCopyField("reimburser", "报销人", Reimburser);
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), "未知联次角色：" + role + "。");
            }
        }

        /// <summary>取联次角色的稳定键。</summary>
        /// <param name="role">联次角色。</param>
        public static string RoleKey(VatCopyRole role)
        {
            switch (role)
            {
                case VatCopyRole.Stub: return "stub";
                case VatCopyRole.Accounting: return "accounting";
                case VatCopyRole.Deduction: return "deduction";
                case VatCopyRole.Invoice: return "invoice";
                default: throw new ArgumentOutOfRangeException(nameof(role), "未知联次角色：" + role + "。");
            }
        }
    }

    /// <summary>
    /// 金额转中文大写（仅用于票面文字展示）。规则是工程上常用的逐位读法，
    /// 不是对任何票据填写规范的复述；调用方若要固定文案可直接覆盖文档的展示层。
    /// </summary>
    public static class VatAmounts
    {
        private static readonly char[] Digits = { '零', '壹', '贰', '叁', '肆', '伍', '陆', '柒', '捌', '玖' };
        private static readonly string[] GroupUnits = { "", "万", "亿", "万亿" };
        private static readonly string[] PositionUnits = { "仟", "佰", "拾", "" };

        /// <summary>把金额转成中文大写（两位小数；四舍五入到分）。</summary>
        /// <param name="amount">金额。</param>
        public static string ToChineseUpper(double amount)
        {
            if (double.IsNaN(amount) || double.IsInfinity(amount))
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "金额必须是有限数。");
            }

            bool negative = amount < 0d;
            long cents = (long)Math.Round(Math.Abs(amount) * 100d, MidpointRounding.AwayFromZero);
            long yuan = cents / 100L;
            int jiao = (int)((cents / 10L) % 10L);
            int fen = (int)(cents % 10L);

            StringBuilder text = new StringBuilder();
            if (negative)
            {
                text.Append('负');
            }

            text.Append(IntegerPart(yuan)).Append('元');

            if (jiao == 0 && fen == 0)
            {
                text.Append('整');
            }
            else
            {
                if (jiao > 0)
                {
                    text.Append(Digits[jiao]).Append('角');
                }
                else if (fen > 0 && yuan > 0)
                {
                    text.Append('零');
                }
                if (fen > 0)
                {
                    text.Append(Digits[fen]).Append('分');
                }
            }

            return text.ToString();
        }

        private static string IntegerPart(long value)
        {
            if (value == 0L)
            {
                return "零";
            }

            int[] groups = new int[4];
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = (int)(value % 10000L);
                value /= 10000L;
            }

            StringBuilder text = new StringBuilder();
            bool pendingZero = false;
            for (int i = groups.Length - 1; i >= 0; i--)
            {
                int group = groups[i];
                if (group == 0)
                {
                    if (text.Length > 0)
                    {
                        pendingZero = true;
                    }
                    continue;
                }

                if (text.Length > 0 && (pendingZero || group < 1000))
                {
                    text.Append('零');
                }
                text.Append(GroupPart(group)).Append(GroupUnits[i]);
                pendingZero = false;
            }
            return text.ToString();
        }

        private static string GroupPart(int group)
        {
            StringBuilder text = new StringBuilder();
            bool pendingZero = false;
            int divisor = 1000;
            for (int i = 0; i < 4; i++)
            {
                int digit = (group / divisor) % 10;
                divisor /= 10;
                if (digit == 0)
                {
                    if (text.Length > 0)
                    {
                        pendingZero = true;
                    }
                    continue;
                }
                if (pendingZero)
                {
                    text.Append('零');
                    pendingZero = false;
                }
                text.Append(Digits[digit]).Append(PositionUnits[i]);
            }
            return text.ToString();
        }
    }
}
