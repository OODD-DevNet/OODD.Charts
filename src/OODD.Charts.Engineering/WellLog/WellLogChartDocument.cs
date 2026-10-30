using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;

namespace OODD.Charts.Engineering.WellLog
{
    /// <summary>一条测井曲线上的一个采样：深度 m、测值。</summary>
    public sealed class WellLogSample
    {
        public WellLogSample(double depth, double value)
        {
            Depth = depth;
            Value = value;
        }

        public double Depth { get; }

        public double Value { get; }
    }

    /// <summary>岩性道上的一个层段。图样种类由文档给，库不内置岩性花纹表。</summary>
    public sealed class WellLogInterval
    {
        public WellLogInterval(double topDepth, double bottomDepth, PatternKind pattern, string label)
        {
            TopDepth = topDepth;
            BottomDepth = bottomDepth;
            Pattern = pattern;
            Label = label ?? string.Empty;
        }

        public double TopDepth { get; }

        public double BottomDepth { get; }

        public PatternKind Pattern { get; }

        public string Label { get; }
    }

    /// <summary>一道测井内容：道头、量程、线性/对数、曲线采样或岩性层段。</summary>
    public sealed class WellLogTrack
    {
        public WellLogTrack(string key, string headerLabel, double min, double max, bool logarithmic)
        {
            Key = key;
            HeaderLabel = headerLabel;
            Min = min;
            Max = max;
            Logarithmic = logarithmic;
        }

        public string Key { get; }

        public string HeaderLabel { get; }

        public double Min { get; }

        public double Max { get; }

        public bool Logarithmic { get; }

        public IList<WellLogSample> Samples { get; } = new List<WellLogSample>();

        /// <summary>岩性/颜色层段；仅 <see cref="TrackKind.PatternColumn"/> 道使用。</summary>
        public IList<WellLogInterval> Intervals { get; } = new List<WellLogInterval>();
    }

    /// <summary>
    /// 测井成果图文档。深度自上而下递增，按 <see cref="PageDepth"/> 续页，
    /// 每页重复同一套道头。幅面与比例尺来自文档/令牌，库不内置测井规范数值。
    /// </summary>
    public sealed class WellLogDocument : ChartDocument
    {
        public WellLogDocument()
        {
            FormTitle = "测井成果图";
        }

        public string? WellId { get; set; }

        public string? RunNumber { get; set; }

        public string? Formation { get; set; }

        public string? ScaleCaption { get; set; }

        public double DepthTop { get; set; } = 0d;

        public double DepthBottom { get; set; } = 1000d;

        /// <summary>每页覆盖的深度长度。</summary>
        public double PageDepth { get; set; } = 250d;

        /// <summary>当前页序号，从 0 起；超出范围时模板夹到最后一页。</summary>
        public int PageIndex { get; set; }

        public IList<WellLogTrack> Tracks { get; } = new List<WellLogTrack>();

        public string? FooterNote { get; set; }

        /// <summary>取某深度处的曲线值（按采样深度线性插值）；无数据返回 null。</summary>
        public double? ValueAt(string trackKey, double depth)
        {
            foreach (WellLogTrack track in Tracks)
            {
                if (!string.Equals(track.Key, trackKey, StringComparison.Ordinal))
                {
                    continue;
                }

                List<WellLogSample> samples = new List<WellLogSample>(track.Samples);
                samples.Sort((a, b) => a.Depth.CompareTo(b.Depth));
                for (int i = 1; i < samples.Count; i++)
                {
                    if (depth >= samples[i - 1].Depth && depth <= samples[i].Depth)
                    {
                        double span = samples[i].Depth - samples[i - 1].Depth;
                        if (span <= 0d)
                        {
                            return samples[i - 1].Value;
                        }
                        double t = (depth - samples[i - 1].Depth) / span;
                        return samples[i - 1].Value + t * (samples[i].Value - samples[i - 1].Value);
                    }
                }

                if (samples.Count > 0 && Math.Abs(depth - samples[0].Depth) < 1e-9)
                {
                    return samples[0].Value;
                }
            }

            return null;
        }
    }
}
