using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Transit.DangerousGoods
{
    /// <summary>
    /// 一联（托运/承运/收货…）的定义：联名、占比、可见字段与独有字段。
    /// <para>字段键见 <see cref="DangerousGoodsDocument"/> 的 F* 常量；联宽在版面求解时按占比分配。</para>
    /// </summary>
    public sealed class DangerousGoodsCopy
    {
        public DangerousGoodsCopy(string key, string label, double share,
                                  IReadOnlyList<string> visibleFields,
                                  IReadOnlyList<string>? extraFields = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("联 key 不能为空。", nameof(key));
            }
            if (string.IsNullOrEmpty(label))
            {
                throw new ArgumentException("联名不能为空。", nameof(label));
            }
            if (!(share > 0d) || double.IsNaN(share) || double.IsInfinity(share))
            {
                throw new ArgumentOutOfRangeException(nameof(share), "联占比必须为正有限值，实到 " + share);
            }
            if (visibleFields == null)
            {
                throw new ArgumentNullException(nameof(visibleFields));
            }

            Key = key;
            Label = label;
            Share = share;
            VisibleFields = new List<string>(visibleFields);

            List<string> extra = new List<string>();
            if (extraFields != null)
            {
                for (int i = 0; i < extraFields.Count; i++)
                {
                    string field = extraFields[i];
                    bool visible = false;
                    for (int v = 0; v < VisibleFields.Count; v++)
                    {
                        if (string.Equals(VisibleFields[v], field, StringComparison.Ordinal))
                        {
                            visible = true;
                            break;
                        }
                    }
                    if (visible)
                    {
                        throw new ArgumentException(
                            "联 \"" + key + "\" 的独有字段 \"" + field + "\" 不能同时出现在可见字段里。", nameof(extraFields));
                    }
                    extra.Add(field);
                }
            }
            ExtraFields = extra;
        }

        public string Key { get; }
        public string Label { get; }
        public double Share { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> ExtraFields { get; }
    }

    /// <summary>
    /// 危险货物道路运输联单文档。
    /// <para>
    /// 库只持有结构化字段与联次定义：UN 编号、正式运输名称、类别项别、包装类别、隧道限制码
    /// 由调用方按其托运文件填入；<b>官方 UN 象形图不在库内</b>，由调用方通过模板的
    /// <c>SymbolPath</c> 槽位注入（docs/06 §4.13 的诚实边界）。
    /// </para>
    /// </summary>
    public sealed class DangerousGoodsDocument : ChartDocument
    {
        // 字段键（字段顺序与联间增删都按这些键走，避免用显示文字当键）。
        public const string FUnNumber = "un";
        public const string FName = "name";
        public const string FClass = "class";
        public const string FDivision = "division";
        public const string FPackingGroup = "pg";
        public const string FTunnel = "tunnel";
        public const string FConsignor = "consignor";
        public const string FCarrier = "carrier";
        public const string FConsignee = "consignee";
        public const string FEmergency = "emergency";

        public DangerousGoodsDocument()
        {
            FormTitle = "危险货物道路运输联单";
            Copies = new List<DangerousGoodsCopy>();
        }

        /// <summary>UN 编号（如 "1203"）。</summary>
        public string UnNumber { get; set; } = string.Empty;

        /// <summary>正式运输名称。</summary>
        public string ProperShippingName { get; set; } = string.Empty;

        /// <summary>类别（如 "3"）。</summary>
        public string Class { get; set; } = string.Empty;

        /// <summary>项别（可空）。</summary>
        public string? Division { get; set; }

        /// <summary>包装类别（如 "II"）。</summary>
        public string? PackingGroup { get; set; }

        /// <summary>隧道限制码（如 "D/E"）。</summary>
        public string? TunnelRestrictionCode { get; set; }

        /// <summary>副标志类别（如 "6.1"、"8"）；null 表示无副标志。</summary>
        public string? SubClass { get; set; }

        public string? Consignor { get; set; }

        public string? Carrier { get; set; }

        public string? Consignee { get; set; }

        public string? EmergencyPhone { get; set; }

        /// <summary>罐体标志牌文字；null 表示不画罐体示意。</summary>
        public string? TankPlacardText { get; set; }

        public IList<DangerousGoodsCopy> Copies { get; }

        /// <summary>加入行业通行的三联：托运人存根 / 承运人 / 收货人（示例配比，未取证）。</summary>
        public DangerousGoodsDocument AddStandardCopies()
        {
            Copies.Add(new DangerousGoodsCopy("shipper", "托运人存根", 1d, new[]
            {
                FUnNumber, FName, FClass, FDivision, FPackingGroup, FTunnel, FConsignor, FConsignee,
            }));
            Copies.Add(new DangerousGoodsCopy("carrier", "承运人联", 1d, new[]
            {
                FUnNumber, FName, FClass, FDivision, FPackingGroup, FTunnel, FCarrier,
            }, new[] { FEmergency }));
            Copies.Add(new DangerousGoodsCopy("consignee", "收货人联", 1d, new[]
            {
                FUnNumber, FName, FClass, FDivision, FPackingGroup, FConsignee,
            }));
            return this;
        }

        /// <summary>字段显示名（与字段键一一对应）。</summary>
        public static string LabelOf(string field)
        {
            if (string.Equals(field, FUnNumber, StringComparison.Ordinal)) return "UN 编号";
            if (string.Equals(field, FName, StringComparison.Ordinal)) return "正式运输名称";
            if (string.Equals(field, FClass, StringComparison.Ordinal)) return "类别";
            if (string.Equals(field, FDivision, StringComparison.Ordinal)) return "项别";
            if (string.Equals(field, FPackingGroup, StringComparison.Ordinal)) return "包装类别";
            if (string.Equals(field, FTunnel, StringComparison.Ordinal)) return "隧道限制";
            if (string.Equals(field, FConsignor, StringComparison.Ordinal)) return "托运人";
            if (string.Equals(field, FCarrier, StringComparison.Ordinal)) return "承运人";
            if (string.Equals(field, FConsignee, StringComparison.Ordinal)) return "收货人";
            if (string.Equals(field, FEmergency, StringComparison.Ordinal)) return "应急电话";
            return field ?? string.Empty;
        }

        /// <summary>按字段键取值（未知键返回空串）。</summary>
        public string ValueOf(string field)
        {
            if (string.Equals(field, FUnNumber, StringComparison.Ordinal)) return UnNumber ?? string.Empty;
            if (string.Equals(field, FName, StringComparison.Ordinal)) return ProperShippingName ?? string.Empty;
            if (string.Equals(field, FClass, StringComparison.Ordinal)) return Class ?? string.Empty;
            if (string.Equals(field, FDivision, StringComparison.Ordinal)) return Division ?? string.Empty;
            if (string.Equals(field, FPackingGroup, StringComparison.Ordinal)) return PackingGroup ?? string.Empty;
            if (string.Equals(field, FTunnel, StringComparison.Ordinal)) return TunnelRestrictionCode ?? string.Empty;
            if (string.Equals(field, FConsignor, StringComparison.Ordinal)) return Consignor ?? string.Empty;
            if (string.Equals(field, FCarrier, StringComparison.Ordinal)) return Carrier ?? string.Empty;
            if (string.Equals(field, FConsignee, StringComparison.Ordinal)) return Consignee ?? string.Empty;
            if (string.Equals(field, FEmergency, StringComparison.Ordinal)) return EmergencyPhone ?? string.Empty;
            return string.Empty;
        }
    }
}
