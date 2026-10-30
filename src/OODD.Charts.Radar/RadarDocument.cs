using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Radar
{
    /// <summary>盘面覆盖范围：360° 全向 / 180° 扇区。</summary>
    public enum RadarScope
    {
        /// <summary>整圆，北向上。</summary>
        Full = 0,

        /// <summary>半圆（默认覆盖 270°→360°→90°，即上方扇区）。</summary>
        Half = 1,
    }

    /// <summary>信息面板位置。</summary>
    public enum RadarPanelPosition
    {
        /// <summary>盘面右侧竖排。</summary>
        Right = 0,

        /// <summary>盘面下方横排，每个目标一行。</summary>
        Bottom = 1,
    }

    /// <summary>目标轨迹上的一个历史点（极坐标）。</summary>
    public sealed class RadarTrackPoint
    {
        public RadarTrackPoint(double bearing, double distance)
        {
            Bearing = bearing;
            Distance = distance;
        }

        /// <summary>真方位角，0° 为正北，顺时针。</summary>
        public double Bearing { get; set; }

        /// <summary>距本舰距离，单位同 <see cref="RadarDocument.RangeUnit"/>。</summary>
        public double Distance { get; set; }
    }

    /// <summary>一个被跟踪目标。</summary>
    public sealed class RadarTarget
    {
        public RadarTarget(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("目标编号不能为空。", nameof(id));
            }
            Id = id;
        }

        /// <summary>稳定编号，供 <see cref="RadarDocument.SelectedTargetId"/> 引用。</summary>
        public string Id { get; }

        /// <summary>显示名称；为空时回落到 <see cref="Id"/>。</summary>
        public string? Label { get; set; }

        /// <summary>真方位角，0° 为正北，顺时针。</summary>
        public double Bearing { get; set; }

        /// <summary>距本舰距离，单位同 <see cref="RadarDocument.RangeUnit"/>。</summary>
        public double Distance { get; set; }

        /// <summary>航速；null 表示未测。</summary>
        public double? Speed { get; set; }

        /// <summary>航向（真方位角）；null 表示未测。</summary>
        public double? Course { get; set; }

        /// <summary>丢失回波：用空心点心色画出，便于区分。</summary>
        public bool Lost { get; set; }

        /// <summary>覆盖默认目标色。</summary>
        public SKColor? Color { get; set; }

        /// <summary>历史轨迹点，按时间先后排列；只保留最近 <see cref="RadarDocument.TrailLength"/> 个。</summary>
        public IList<RadarTrackPoint> Track { get; } = new List<RadarTrackPoint>();

        /// <summary>自定义字段，供信息面板按 "extra.xxx" 取值。</summary>
        public IDictionary<string, string> Extra { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public string DisplayText => string.IsNullOrEmpty(Label) ? Id : Label!;

        /// <summary>把当前位置压入轨迹，再移动到新位置。模拟一次扫描更新。</summary>
        public void MoveTo(double bearing, double distance)
        {
            Track.Add(new RadarTrackPoint(Bearing, Distance));
            Bearing = bearing;
            Distance = distance;
        }
    }

    /// <summary>信息面板里的一个字段。由用户手动编排显示哪些字段、顺序和数值格式。</summary>
    public sealed class RadarInfoField
    {
        public RadarInfoField(string key, string? label = null, string? format = null)
        {
            Key = key;
            Label = label;
            Format = format;
        }

        /// <summary>id / label / bearing / distance / speed / course / status / extra.名称。</summary>
        public string Key { get; }

        /// <summary>自定义字段名；null 用内置中文名。</summary>
        public string? Label { get; set; }

        /// <summary>数值格式串（仅 distance/speed 生效）；null 用内置格式。</summary>
        public string? Format { get; set; }
    }

    /// <summary>目标信息面板配置。</summary>
    public sealed class RadarInfoPanel
    {
        public RadarInfoPanel()
        {
            Fields = new List<RadarInfoField>
            {
                new RadarInfoField("id"),
                new RadarInfoField("bearing"),
                new RadarInfoField("distance"),
            };
        }

        /// <summary>是否显示信息面板。</summary>
        public bool Show { get; set; } = true;

        public string Title { get; set; } = "目标信息";

        public RadarPanelPosition Position { get; set; } = RadarPanelPosition.Right;

        /// <summary>逐目标的字段列表；顺序即显示顺序。</summary>
        public IList<RadarInfoField> Fields { get; }

        /// <summary>是否在光点旁标目标名。</summary>
        public bool ShowTargetLabels { get; set; } = true;

        /// <summary>没有目标时的占位文字。</summary>
        public string EmptyText { get; set; } = "无目标";

        /// <summary>按配置解析出一个目标的“标签 + 值”序列；空值字段跳过。</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Resolve(RadarTarget target, RadarDocument document)
        {
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>();
            foreach (RadarInfoField field in Fields)
            {
                string? value = ValueOf(field, target, document);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }
                string label = string.IsNullOrEmpty(field.Label) ? LabelOf(field.Key) : field.Label!;
                pairs.Add(new KeyValuePair<string, string>(label, value!));
            }
            return pairs;
        }

        /// <summary>字段键的默认中文名。</summary>
        public static string LabelOf(string key)
        {
            switch (key)
            {
                case "id": return "编号";
                case "label": return "名称";
                case "bearing": return "方位";
                case "distance": return "距离";
                case "speed": return "航速";
                case "course": return "航向";
                case "status": return "状态";
                default:
                    if (key != null && key.StartsWith("extra.", StringComparison.Ordinal))
                    {
                        return key.Substring("extra.".Length);
                    }
                    return key ?? string.Empty;
            }
        }

        /// <summary>取字段值；未知键返回 null。</summary>
        public static string? ValueOf(RadarInfoField field, RadarTarget target, RadarDocument document)
        {
            switch (field.Key)
            {
                case "id":
                    return target.Id;
                case "label":
                    return target.Label;
                case "bearing":
                    return FormatBearing(target.Bearing);
                case "course":
                    return target.Course.HasValue ? FormatBearing(target.Course.Value) : null;
                case "distance":
                    return target.Distance.ToString(field.Format ?? "0.0", CultureInfo.InvariantCulture)
                           + " " + document.RangeUnit;
                case "speed":
                    return target.Speed.HasValue
                        ? target.Speed.Value.ToString(field.Format ?? "0.0", CultureInfo.InvariantCulture)
                          + " " + document.SpeedUnit
                        : null;
                case "status":
                    return target.Lost ? "丢失" : "跟踪中";
                default:
                    if (field.Key != null && field.Key.StartsWith("extra.", StringComparison.Ordinal))
                    {
                        string name = field.Key.Substring("extra.".Length);
                        string? extra;
                        return target.Extra.TryGetValue(name, out extra) ? extra : null;
                    }
                    return null;
            }
        }

        /// <summary>方位角格式化为三位数加度数符：045°。</summary>
        public static string FormatBearing(double degrees)
        {
            return NormalizeBearing(degrees).ToString("000", CultureInfo.InvariantCulture) + "°";
        }

        /// <summary>归一到 [0, 360)。</summary>
        public static double NormalizeBearing(double degrees)
        {
            double value = degrees % 360d;
            if (value < 0d)
            {
                value += 360d;
            }
            return value;
        }
    }

    /// <summary>
    /// 雷达态势图文档。方位角一律 0° = 正北、顺时针；距离单位由 <see cref="RangeUnit"/> 给出。
    /// 目标、选中项、扫描线、轨迹保留点数、信息面板字段全部由数据侧控制。
    /// </summary>
    public sealed class RadarDocument : ChartDocument
    {
        public RadarDocument()
        {
            FormTitle = "雷达态势图";
            Targets = new List<RadarTarget>();
            InfoPanel = new RadarInfoPanel();
        }

        /// <summary>360° 整圆或 180° 扇区。</summary>
        public RadarScope Scope { get; set; } = RadarScope.Full;

        /// <summary>量程（盘面最外圈代表的最大距离）。</summary>
        public double MaxRange { get; set; } = 50d;

        /// <summary>距离单位，如 km / nmi / m。</summary>
        public string RangeUnit { get; set; } = "km";

        /// <summary>航速单位，如 kn / km/h。</summary>
        public string SpeedUnit { get; set; } = "kn";

        /// <summary>距标环数量（不含最外圈）。</summary>
        public int RingCount { get; set; } = 5;

        /// <summary>180° 扇区起始方位；270 表示覆盖上方（270→90）。</summary>
        public double HalfStartBearing { get; set; } = 270d;

        public IList<RadarTarget> Targets { get; }

        /// <summary>当前选中目标编号；null 表示无选中。</summary>
        public string? SelectedTargetId { get; set; }

        /// <summary>指针悬停目标编号；null 表示无悬停。由 <see cref="RadarInteraction"/> 写入，模板据此画悬停圈。</summary>
        public string? HoverTargetId { get; set; }

        /// <summary>是否画扫描线。</summary>
        public bool ShowSweep { get; set; } = true;

        /// <summary>扫描线当前方位（0° = 正北，顺时针）。</summary>
        public double SweepAngle { get; set; } = 45d;

        /// <summary>扫描光束宽度（度）；越大拖尾越宽。</summary>
        public double SweepBeamWidth { get; set; } = 28d;

        /// <summary>是否画目标历史轨迹。</summary>
        public bool ShowTrails { get; set; } = true;

        /// <summary>每个目标保留多少个轨迹点。</summary>
        public int TrailLength { get; set; } = 6;

        /// <summary>是否在右上角画南北向罗盘。</summary>
        public bool ShowNorthIndicator { get; set; } = true;

        public RadarInfoPanel InfoPanel { get; set; }

        /// <summary>当前选中目标；编号对不上时返回 null。</summary>
        public RadarTarget? SelectedTarget
        {
            get
            {
                if (string.IsNullOrEmpty(SelectedTargetId))
                {
                    return null;
                }
                foreach (RadarTarget target in Targets)
                {
                    if (string.Equals(target.Id, SelectedTargetId, StringComparison.Ordinal))
                    {
                        return target;
                    }
                }
                return null;
            }
        }
    }
}
