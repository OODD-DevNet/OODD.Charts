using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.Forms.DeathCertificate
{
    /// <summary>死亡证明上的字段键。联间差异全部按键判定，不按文字判定。</summary>
    public static class DeathCertificateFields
    {
        public const string Name = "name";
        public const string Sex = "sex";
        public const string Age = "age";
        public const string IdNumber = "id-number";
        public const string DeathDate = "death-date";
        public const string DeathPlace = "death-place";
        public const string Household = "household";
        public const string Phone = "phone";
        public const string Reporter = "reporter";
        public const string FuneralHotline = "funeral-hotline";

        public const string CauseA = "cause.a";
        public const string CauseB = "cause.b";
        public const string CauseC = "cause.c";
        public const string CauseD = "cause.d";

        /// <summary>普通字段的固定行序（隐藏字段留空行，保证像素对照位置一致）。</summary>
        public static readonly IReadOnlyList<string> Ordered = new List<string>
        {
            Name, Sex, Age, IdNumber, DeathDate, DeathPlace, Household, Phone, Reporter, FuneralHotline,
        }.AsReadOnly();

        /// <summary>死因链 I.(a)–(d) 的固定行序。</summary>
        public static readonly IReadOnlyList<string> Causes = new List<string>
        {
            CauseA, CauseB, CauseC, CauseD,
        }.AsReadOnly();

        /// <summary>字段显示名（不作为判定键）。</summary>
        public static string LabelOf(string key)
        {
            switch (key)
            {
                case Name: return "姓名";
                case Sex: return "性别";
                case Age: return "年龄";
                case IdNumber: return "身份证号";
                case DeathDate: return "死亡日期";
                case DeathPlace: return "死亡地点";
                case Household: return "户籍地址";
                case Phone: return "联系电话";
                case Reporter: return "填报人";
                case FuneralHotline: return "殡葬热线";
                case CauseA: return "I.(a) 直接死因";
                case CauseB: return "I.(b) 引起 (a) 的疾病";
                case CauseC: return "I.(c) 引起 (b) 的疾病";
                case CauseD: return "I.(d) 引起 (c) 的疾病";
                default: return key;
            }
        }

        /// <summary>死因链行的短标签（列宽有限，画在值左侧）。</summary>
        public static string CauseMarkOf(string key)
        {
            switch (key)
            {
                case CauseA: return "I.(a)";
                case CauseB: return "I.(b)";
                case CauseC: return "I.(c)";
                case CauseD: return "I.(d)";
                default: return key;
            }
        }
    }

    /// <summary>一联的字段可见性定义（不含宽度；宽度由模板令牌给出）。</summary>
    public sealed class DeathCopy
    {
        private readonly HashSet<string> _set;

        public DeathCopy(string key, string label, IEnumerable<string> visibleFields,
                         IEnumerable<string> extraFields)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("联 key 不能为空。", nameof(key));
            }

            Key = key;
            Label = label ?? string.Empty;
            VisibleFields = ToList(visibleFields);
            ExtraFields = ToList(extraFields);
            _set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string field in VisibleFields) _set.Add(field);
            foreach (string field in ExtraFields) _set.Add(field);
        }

        public string Key { get; }
        public string Label { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> ExtraFields { get; }

        public bool ShowsField(string fieldKey)
        {
            return !string.IsNullOrEmpty(fieldKey) && _set.Contains(fieldKey);
        }

        private static IReadOnlyList<string> ToList(IEnumerable<string> source)
        {
            List<string> list = new List<string>();
            if (source != null)
            {
                foreach (string item in source)
                {
                    if (!string.IsNullOrEmpty(item)) list.Add(item);
                }
            }
            return list.AsReadOnly();
        }
    }

    /// <summary>
    /// 居民死亡医学证明（四联）文档。
    /// <para>
    /// 四联依次为：单位存根、公安、近亲属、殡仪馆。联间字段差异由 <see cref="Copies"/> 声明，
    /// 模板据此构造 <see cref="OODD.Charts.CollateSpec"/>，每联只画自己的字段。
    /// </para>
    /// </summary>
    public sealed class DeathCertificateDocument : ChartDocument
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        public DeathCertificateDocument()
        {
            foreach (DeathCopy copy in CreateDefaultCopies())
            {
                Copies.Add(copy);
            }
        }

        public IList<DeathCopy> Copies { get; } = new List<DeathCopy>();

        /// <summary>行政区划代码（6 格，超长截断、不足左侧补 0）。</summary>
        public string AdminCode { get; set; } = "110101";

        /// <summary>证明编号（17 格，超长截断、不足左侧补 0）。</summary>
        public string SerialNumber { get; set; } = "20260000000000001";

        public void Set(string key, string value)
        {
            _values[key] = value ?? string.Empty;
        }

        /// <summary>取字段值；标准患者字段回落到 <see cref="ChartDocument.Patient"/>。</summary>
        public string Get(string key)
        {
            string? value;
            if (_values.TryGetValue(key, out value))
            {
                return value;
            }

            switch (key)
            {
                case DeathCertificateFields.Name: return Patient.Name ?? string.Empty;
                case DeathCertificateFields.Sex: return Patient.Sex ?? string.Empty;
                case DeathCertificateFields.Age: return Patient.Age ?? string.Empty;
                case DeathCertificateFields.IdNumber: return Patient.AdmissionNumber ?? string.Empty;
                case DeathCertificateFields.DeathDate: return Patient.AdmissionDate ?? string.Empty;
                default: return string.Empty;
            }
        }

        /// <summary>四联默认定义。差异字段故意各联不同，供"每联只画 visibleFields"断言。</summary>
        public static IReadOnlyList<DeathCopy> CreateDefaultCopies()
        {
            return new List<DeathCopy>
            {
                new DeathCopy("hospital-stub", "单位存根",
                    new[]
                    {
                        DeathCertificateFields.Name, DeathCertificateFields.Sex, DeathCertificateFields.Age,
                        DeathCertificateFields.IdNumber, DeathCertificateFields.DeathDate,
                        DeathCertificateFields.CauseA, DeathCertificateFields.CauseB,
                        DeathCertificateFields.CauseC, DeathCertificateFields.CauseD,
                    },
                    new[] { DeathCertificateFields.Reporter }),
                new DeathCopy("police", "公安",
                    new[]
                    {
                        DeathCertificateFields.Name, DeathCertificateFields.IdNumber,
                        DeathCertificateFields.DeathDate, DeathCertificateFields.CauseA,
                    },
                    new[] { DeathCertificateFields.Household }),
                new DeathCopy("relative", "近亲属",
                    new[]
                    {
                        DeathCertificateFields.Name, DeathCertificateFields.Sex, DeathCertificateFields.Age,
                        DeathCertificateFields.DeathDate, DeathCertificateFields.CauseA,
                        DeathCertificateFields.CauseB, DeathCertificateFields.CauseC,
                        DeathCertificateFields.CauseD,
                    },
                    new[] { DeathCertificateFields.Phone }),
                new DeathCopy("funeral", "殡仪馆",
                    new[]
                    {
                        DeathCertificateFields.Name, DeathCertificateFields.IdNumber,
                        DeathCertificateFields.DeathDate, DeathCertificateFields.CauseA,
                    },
                    new[] { DeathCertificateFields.FuneralHotline }),
            }.AsReadOnly();
        }
    }
}
