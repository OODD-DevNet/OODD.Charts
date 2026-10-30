using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Model;
using OODD.Charts.Render;

namespace OODD.Charts.Medical.Forms.AnesthesiaRecord
{
    /// <summary>一台手术的麻醉信息（术式、麻醉方式、麻醉医师等）。</summary>
    public sealed class AnesthesiaOperation
    {
        public string? Procedure { get; set; }
        public string? AnesthesiaType { get; set; }
        public string? Anesthetist { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
    }

    /// <summary>一次时点采样。时间以"距麻醉开始第几分钟"表示。</summary>
    public sealed class AnesthesiaPoint
    {
        public AnesthesiaPoint(double timeMin, double systolic, double diastolic, double mean,
                               double pulse, double temperature, double spo2, double etco2)
        {
            TimeMin = timeMin;
            Systolic = systolic;
            Diastolic = diastolic;
            Mean = mean;
            Pulse = pulse;
            Temperature = temperature;
            SpO2 = spo2;
            EtCO2 = etco2;
        }

        public double TimeMin { get; }
        public double Systolic { get; }
        public double Diastolic { get; }
        public double Mean { get; }
        public double Pulse { get; }

        /// <summary>体温（℃），画在右侧 12–40 轴上。</summary>
        public double Temperature { get; }

        public double SpO2 { get; }
        public double EtCO2 { get; }
    }

    /// <summary>持续用药：起止时间与剂量。版面用 <see cref="OODD.Charts.Geometry.SpanBand"/> 表达。</summary>
    public sealed class ContinuousMedication
    {
        public ContinuousMedication(string name, double startMin, double endMin, string dose)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("药名不能为空。", nameof(name));
            }
            if (!(endMin > startMin))
            {
                throw new ArgumentException("持续用药的结束时间必须大于开始时间：" + name, nameof(endMin));
            }

            Name = name;
            StartMin = startMin;
            EndMin = endMin;
            Dose = dose;
        }

        public string Name { get; }
        public double StartMin { get; }
        public double EndMin { get; }
        public string Dose { get; }
    }

    /// <summary>
    /// 成对事件：一次事件由两个时刻、两端符号表达（WS 329—2024 附录 B 的 ∨∨—∨∨ 类符号）。
    /// </summary>
    public sealed class AnesthesiaEvent
    {
        public AnesthesiaEvent(double startMin, double endMin, string code, int sequence, int symbolIndex)
        {
            if (string.IsNullOrEmpty(code))
            {
                throw new ArgumentException("事件码不能为空。", nameof(code));
            }
            StartMin = startMin;
            EndMin = endMin;
            Code = code;
            Sequence = sequence;
            SymbolIndex = symbolIndex;
        }

        public double StartMin { get; }
        public double EndMin { get; }
        public string Code { get; }

        /// <summary>未列图例时的圆圈序号（①②③…）。</summary>
        public int Sequence { get; }

        /// <summary>使用第几种成对符号（0–10）。</summary>
        public int SymbolIndex { get; }
    }

    /// <summary>
    /// 麻醉记录单文档。
    /// <para>
    /// 采样密度按 WS 329—2024 的"血压/脉搏至少每 5 分钟一次"做<b>文档校验</b>：
    /// <see cref="SamplingWarnings"/> 返回告警文本，但模板<b>不拦截</b>绘制——本库不是质控系统。
    /// </para>
    /// </summary>
    public sealed class AnesthesiaRecordDocument : ChartDocument
    {
        public const int PairedSymbolCount = 11;

        public AnesthesiaOperation Operation { get; } = new AnesthesiaOperation();

        public IList<AnesthesiaPoint> Points { get; } = new List<AnesthesiaPoint>();

        public IList<ContinuousMedication> ContinuousMedications { get; } = new List<ContinuousMedication>();

        public IList<AnesthesiaEvent> Events { get; } = new List<AnesthesiaEvent>();

        /// <summary>麻醉总时长（分钟），横轴 0–TotalMinutes。</summary>
        public int TotalMinutes { get; set; } = 240;

        /// <summary>
        /// 11 种成对连线符号。顺序即 <see cref="AnesthesiaEvent.SymbolIndex"/>。
        /// 全部是 <see cref="PairedEventGlyph"/>，不允许在模板里退化成单点 Marker。
        /// </summary>
        public static IReadOnlyList<PairedEventGlyph> CreatePairedSymbols()
        {
            return new List<PairedEventGlyph>
            {
                new PairedEventGlyph(MarkerSymbol.Dot, MarkerSymbol.Dot, true),
                new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, true),
                new PairedEventGlyph(MarkerSymbol.Cross, MarkerSymbol.Cross, true),
                new PairedEventGlyph(MarkerSymbol.DotRing, MarkerSymbol.DotRing, true),
                new PairedEventGlyph(MarkerSymbol.PlusInRing, MarkerSymbol.PlusInRing, true),
                new PairedEventGlyph(MarkerSymbol.Dot, MarkerSymbol.Ring, true),
                new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Dot, true),
                new PairedEventGlyph(MarkerSymbol.Cross, MarkerSymbol.Dot, true),
                new PairedEventGlyph(MarkerSymbol.Dot, MarkerSymbol.Cross, true),
                new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Cross, true),
                new PairedEventGlyph(MarkerSymbol.PlusInRing, MarkerSymbol.DotRing, false),
            }.AsReadOnly();
        }

        /// <summary>收缩压在给定时刻的取值：先找同刻点，找不到在相邻两点间线性内插；无数据返回 null。</summary>
        public double? ValueAt(double timeMin, Func<AnesthesiaPoint, double> selector)
        {
            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }
            if (Points.Count == 0)
            {
                return null;
            }

            List<AnesthesiaPoint> ordered = new List<AnesthesiaPoint>(Points);
            ordered.Sort((a, b) => a.TimeMin.CompareTo(b.TimeMin));

            foreach (AnesthesiaPoint point in ordered)
            {
                if (Math.Abs(point.TimeMin - timeMin) <= 1e-6)
                {
                    return selector(point);
                }
            }

            if (timeMin < ordered[0].TimeMin || timeMin > ordered[ordered.Count - 1].TimeMin)
            {
                return null;
            }

            for (int i = 1; i < ordered.Count; i++)
            {
                AnesthesiaPoint left = ordered[i - 1];
                AnesthesiaPoint right = ordered[i];
                if (timeMin <= right.TimeMin)
                {
                    double span = right.TimeMin - left.TimeMin;
                    if (span <= 0d)
                    {
                        return selector(right);
                    }
                    double t = (timeMin - left.TimeMin) / span;
                    return selector(left) + t * (selector(right) - selector(left));
                }
            }

            return selector(ordered[ordered.Count - 1]);
        }

        /// <summary>
        /// 采样密度校验（WS 329—2024，血压/脉搏 ≥5min）。返回告警，不改变文档、不抛异常、不阻止绘制。
        /// </summary>
        public IReadOnlyList<string> SamplingWarnings()
        {
            List<AnesthesiaPoint> ordered = new List<AnesthesiaPoint>(Points);
            ordered.Sort((a, b) => a.TimeMin.CompareTo(b.TimeMin));

            List<string> warnings = new List<string>();
            for (int i = 1; i < ordered.Count; i++)
            {
                double gap = ordered[i].TimeMin - ordered[i - 1].TimeMin;
                if (gap > 5.0001d)
                {
                    warnings.Add("时间 " + Format(ordered[i - 1].TimeMin) + "→" + Format(ordered[i].TimeMin)
                                 + " 分钟采样间隔 " + Format(gap) + " min，超过 WS 329—2024 要求的 5 min（告警，不拦截）。");
                }
            }
            return warnings;
        }

        private static string Format(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary>时间轴数字标签（分钟）。</summary>
        public static string TimeLabel(double minutes)
        {
            return Math.Round(minutes).ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
