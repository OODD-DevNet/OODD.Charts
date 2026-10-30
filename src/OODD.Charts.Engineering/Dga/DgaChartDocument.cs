using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Model;

namespace OODD.Charts.Engineering.Dga
{
    /// <summary>一条气体趋势轨上的一个取样：自首次取样的天数偏移、日期文字与浓度值。</summary>
    public sealed class DgaGasSample
    {
        public DgaGasSample(double dayOffset, string dateText, double value)
        {
            DayOffset = dayOffset;
            DateText = dateText ?? string.Empty;
            Value = value;
        }

        public double DayOffset { get; }

        public string DateText { get; }

        public double Value { get; }
    }

    /// <summary>一种气体的趋势序列（H2、CH4、C2H2 等）。量程由模板按数据给出。</summary>
    public sealed class DgaGasSeries
    {
        public DgaGasSeries(string key, string label, string unit)
        {
            Key = key;
            Label = label ?? string.Empty;
            Unit = unit ?? string.Empty;
        }

        public string Key { get; }

        public string Label { get; }

        public string Unit { get; }

        public IList<DgaGasSample> Samples { get; } = new List<DgaGasSample>();
    }

    /// <summary>
    /// 故障分区多边形。顶点以三组分给出，由文档提供；
    /// 库不内置 DL/T 722 的分区坐标（该标准的坐标未核实），只负责按三轴映射落图。
    /// </summary>
    public sealed class DgaZone
    {
        public DgaZone(string name, IEnumerable<TriaxialComponents> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            Name = name ?? string.Empty;
            List<TriaxialComponents> list = new List<TriaxialComponents>(vertices);
            if (list.Count < 3)
            {
                throw new ArgumentException("故障分区至少需要 3 个顶点，实到 " + list.Count + "。", nameof(vertices));
            }
            Vertices = list;
        }

        public string Name { get; }

        public IReadOnlyList<TriaxialComponents> Vertices { get; }
    }

    /// <summary>一次油色谱取样在三角图上的投点：日期与三组分（不必已归一）。</summary>
    public sealed class DgaSample
    {
        public DgaSample(string dateText, TriaxialComponents components)
        {
            DateText = dateText ?? string.Empty;
            Components = components;
        }

        public string DateText { get; }

        public TriaxialComponents Components { get; }
    }

    /// <summary>
    /// 变压器油色谱 DGA 文档。上半部是多气体趋势轨（共享非等距取样日期轴），
    /// 下半部是大卫三角形；三组分投点与故障分区顶点均由文档给。
    /// </summary>
    public sealed class DgaChartDocument : ChartDocument
    {
        public DgaChartDocument()
        {
            FormTitle = "变压器油色谱分析（大卫三角形）";
        }

        public string? TransformerId { get; set; }

        public string? OilBrand { get; set; }

        public string? SampleDateText { get; set; }

        public IList<DgaGasSeries> GasSeries { get; } = new List<DgaGasSeries>();

        public IList<DgaZone> Zones { get; } = new List<DgaZone>();

        public IList<DgaSample> Samples { get; } = new List<DgaSample>();

        public string? FooterNote { get; set; }
    }
}
