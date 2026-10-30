using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Transit.LoadBalance
{
    /// <summary>货舱/装载站位：STA 编号、臂距（m）与装载重量（kg）。</summary>
    public sealed class LoadStation
    {
        public LoadStation(string key, string name, double sta, double arm, double weightKg)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("装载站位必须有 key。", nameof(key));
            }

            Key = key;
            Name = name ?? key;
            Sta = sta;
            Arm = arm;
            WeightKg = weightKg;
        }

        public string Key { get; }
        public string Name { get; }

        /// <summary>站位号（非线性轴上的货舱编号，允许跳号）。</summary>
        public double Sta { get; }

        /// <summary>臂距（由文档换算成与重心轴同量纲的数值；样例直接给 %MAC 等效值）。</summary>
        public double Arm { get; }

        /// <summary>装载重量（kg）。</summary>
        public double WeightKg { get; }
    }

    /// <summary>
    /// 一条重心包线：顶点是数据坐标（X = 重心 %MAC，Y = 重量 kg），
    /// 直接交给 Core 的 <see cref="Geometry.EnvelopePolygon"/>，判定与填充同源。
    /// </summary>
    public sealed class CgEnvelope
    {
        public CgEnvelope(string key, string label, IReadOnlyList<SKPoint> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            Key = key;
            Label = label ?? key;
            Vertices = new List<SKPoint>(vertices);
        }

        public string Key { get; }
        public string Label { get; }

        /// <summary>顶点（数据坐标，X = %MAC，Y = kg）。</summary>
        public IReadOnlyList<SKPoint> Vertices { get; }
    }

    /// <summary>一个装载检查点（装载点 / 起飞重心 / 着陆重心）。</summary>
    public sealed class CgCheckPoint
    {
        public CgCheckPoint(string key, string label, double cgPercentMac, double weightKg)
        {
            Key = key;
            Label = label ?? key;
            CgPercentMac = cgPercentMac;
            WeightKg = weightKg;
        }

        public string Key { get; }
        public string Label { get; }

        /// <summary>重心位置（%MAC）。</summary>
        public double CgPercentMac { get; }

        /// <summary>重量（kg）。</summary>
        public double WeightKg { get; }
    }

    /// <summary>载重表的一行：行名 + 与各重心列一一对应的数值。</summary>
    public sealed class LoadBalanceTableRow
    {
        public LoadBalanceTableRow(string label, IReadOnlyList<double> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            Label = label ?? string.Empty;
            Values = new List<double>(values);
        }

        public string Label { get; }
        public IReadOnlyList<double> Values { get; }
    }

    /// <summary>
    /// 飞机载重平衡与重心包线文档。
    /// <para>
    /// 包线折点与装载数据全部由文档给（docs/06 D21：库不内置未核实的机型限制），
    /// 库只负责"画包线 + 判定投点 + 把表和图对齐"。
    /// </para>
    /// </summary>
    public sealed class LoadBalanceDocument : ChartDocument
    {
        public LoadBalanceDocument()
        {
            FormTitle = "飞机载重平衡表";
            Stations = new List<LoadStation>();
            Envelopes = new List<CgEnvelope>();
            CheckPoints = new List<CgCheckPoint>();
            TableCgValues = new List<double>();
            TableRows = new List<LoadBalanceTableRow>();
        }

        public string? AircraftId { get; set; }

        public double EmptyWeight { get; set; }

        public double EmptyCgPercentMac { get; set; }

        public double FuelKg { get; set; }

        public double FuelArm { get; set; }

        /// <summary>重心轴下限（%MAC）。</summary>
        public double CgMin { get; set; } = 15d;

        /// <summary>重心轴上限（%MAC）。</summary>
        public double CgMax { get; set; } = 40d;

        /// <summary>重量轴下限（kg）。</summary>
        public double WeightMin { get; set; } = 40000d;

        /// <summary>重量轴上限（kg）。</summary>
        public double WeightMax { get; set; } = 80000d;

        public IList<LoadStation> Stations { get; }

        public IList<CgEnvelope> Envelopes { get; }

        public IList<CgCheckPoint> CheckPoints { get; }

        /// <summary>载重表各列对应的重心位置（%MAC）；列中心与图上该重心像素位置对齐。</summary>
        public IList<double> TableCgValues { get; }

        /// <summary>载重表行；每行数值个数必须与 <see cref="TableCgValues"/> 一致。</summary>
        public IList<LoadBalanceTableRow> TableRows { get; }

        /// <summary>总重（空机 + 装载 + 燃油）。</summary>
        public double TotalWeightKg
        {
            get
            {
                double total = EmptyWeight + FuelKg;
                for (int i = 0; i < Stations.Count; i++)
                {
                    total += Stations[i].WeightKg;
                }
                return total;
            }
        }

        /// <summary>
        /// 由力矩求重心（%MAC）：重心 = Σ(重量×臂距)/Σ重量。
        /// 本库不猜机型，臂距一律由文档按其机型资料换算成与重心轴同量纲的数值后传入。
        /// </summary>
        public double CgPercentMacOf()
        {
            double total = TotalWeightKg;
            if (!(total > 0d))
            {
                return EmptyCgPercentMac;
            }

            double moment = EmptyWeight * EmptyCgPercentMac + FuelKg * FuelArm;
            for (int i = 0; i < Stations.Count; i++)
            {
                moment += Stations[i].WeightKg * Stations[i].Arm;
            }
            return moment / total;
        }
    }
}
