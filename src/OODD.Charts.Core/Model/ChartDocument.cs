using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Model
{
    /// <summary>页眉患者信息栏的一个自定义字段。</summary>
    public sealed class HeaderField
    {
        public HeaderField(string key, string label, string? value = null)
        {
            Key = key;
            Label = label;
            Value = value;
        }

        public string Key { get; }
        public string Label { get; }
        public string? Value { get; set; }
    }

    /// <summary>患者与就诊信息。</summary>
    public sealed class PatientHeader
    {
        public string? Name { get; set; }
        public string? Sex { get; set; }
        public string? Age { get; set; }
        public string? Department { get; set; }
        public string? Ward { get; set; }
        public string? BedNumber { get; set; }
        public string? AdmissionNumber { get; set; }
        public string? AdmissionDate { get; set; }

        /// <summary>行业自定义字段，按顺序追加在标准字段之后。</summary>
        public IList<HeaderField> ExtraFields { get; } = new List<HeaderField>();

        /// <summary>渲染成“标签: 值”序列；空值字段跳过。</summary>
        public IEnumerable<KeyValuePair<string, string>> EnumeratePairs()
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            Add(pairs, "姓名", Name);
            Add(pairs, "性别", Sex);
            Add(pairs, "年龄", Age);
            Add(pairs, "科别", Department);
            Add(pairs, "病区", Ward);
            Add(pairs, "床号", BedNumber);
            Add(pairs, "住院号", AdmissionNumber);
            Add(pairs, "入院日期", AdmissionDate);
            return pairs;
        }

        /// <summary>标准字段 + 自定义字段的完整序列，供页眉绘制使用。</summary>
        public IReadOnlyList<KeyValuePair<string, string>> EnumerateAll()
        {
            List<KeyValuePair<string, string>> all = new List<KeyValuePair<string, string>>(EnumeratePairs());
            foreach (HeaderField field in ExtraFields)
            {
                Add(all, field.Label, field.Value);
            }
            return all;
        }

        private static void Add(List<KeyValuePair<string, string>> target, string label, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                target.Add(new KeyValuePair<string, string>(label, value!));
            }
        }
    }

    /// <summary>图例中的一项。</summary>
    public sealed class LegendItem
    {
        public LegendItem(MarkerSymbol symbol, SKColor color, string text)
        {
            Symbol = symbol;
            Color = color;
            Text = text;
        }

        public MarkerSymbol Symbol { get; set; }
        public SKColor Color { get; set; }
        public string Text { get; set; }
    }

    /// <summary>表单上的一条自由批注。</summary>
    public sealed class Annotation
    {
        public Annotation(string text, float x, float y)
        {
            Text = text;
            X = x;
            Y = y;
        }

        public string Text { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public SKColor? Color { get; set; }
        public float? Size { get; set; }
    }

    /// <summary>所有表单文档的基类。</summary>
    public class ChartDocument
    {
        public string? HospitalName { get; set; }
        public string? FormTitle { get; set; }
        public PatientHeader Patient { get; set; } = new PatientHeader();
        public IList<LegendItem> Legend { get; } = new List<LegendItem>();
        public IList<Annotation> Annotations { get; } = new List<Annotation>();

        /// <summary>页眉右侧的附加文本（如“共 3 页 第 1 页”）。</summary>
        public string? HeaderRightNote { get; set; }
    }
}
