using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.Forms.AstReport
{
    /// <summary>一条药敏结果。判读类别必须是 S / SDD / I / R 之一。</summary>
    public sealed class AstAntibiotic
    {
        public AstAntibiotic(string name, string mic, string category)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("抗菌药物名不能为空。", nameof(name));
            }
            if (!IsCategory(category))
            {
                throw new ArgumentException("判读类别必须是 S / SDD / I / R，实际为 \"" + category + "\"。",
                    nameof(category));
            }
            Name = name;
            Mic = mic ?? string.Empty;
            Category = category;
        }

        public string Name { get; }
        public string Mic { get; }

        /// <summary>S = 敏感，SDD = 剂量依赖敏感，I = 中介，R = 耐药。</summary>
        public string Category { get; }

        public static bool IsCategory(string? category)
        {
            return category == "S" || category == "SDD" || category == "I" || category == "R";
        }
    }

    /// <summary>药敏报告单文档。</summary>
    public sealed class AstReportDocument : ChartDocument
    {
        public string? Specimen { get; set; }
        public string? Organism { get; set; }

        public IList<AstAntibiotic> Antibiotics { get; } = new List<AstAntibiotic>();
    }
}
