using System.Collections.Generic;
using OODD.Charts.Radar;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>雷达样例：360° 全向态势与 180° 扇区态势各一张。</summary>
    public static class RadarSample
    {
        /// <summary>
        /// 360° 全向：4 个目标（含丢失回波、自定义色），选中“远海 7”，
        /// 扫描线 045°，保留 6 个轨迹点，信息面板竖排在右侧。
        /// </summary>
        public static RadarDocument BuildFull()
        {
            RadarDocument doc = new RadarDocument
            {
                FormTitle = "雷达态势图（360° 全向）",
                Scope = RadarScope.Full,
                MaxRange = 50d,
                RangeUnit = "km",
                SpeedUnit = "kn",
                RingCount = 5,
                ShowSweep = true,
                SweepAngle = 45d,
                SweepBeamWidth = 30d,
                ShowTrails = true,
                TrailLength = 6,
                ShowNorthIndicator = true,
                SelectedTargetId = "T2",
            };

            RadarTarget fishing = new RadarTarget("T1")
            {
                Label = "渔政 3102",
                Bearing = 32d,
                Distance = 18.5d,
                Speed = 8.2d,
                Course = 45d,
            };
            AddTrack(fishing, 46d, 22.6d, 43d, 21.8d, 40d, 20.6d, 37d, 19.8d, 34.5d, 19.1d, 32d, 18.5d);
            fishing.Extra["type"] = "渔船";
            doc.Targets.Add(fishing);

            RadarTarget freighter = new RadarTarget("T2")
            {
                Label = "远海 7",
                Bearing = 118d,
                Distance = 32.0d,
                Speed = 12.6d,
                Course = 128d,
            };
            AddTrack(freighter, 106d, 27.4d, 109d, 28.6d, 112d, 29.7d, 114.5d, 30.8d, 116d, 31.5d, 118d, 32d);
            freighter.Extra["type"] = "货轮";
            freighter.Extra["imo"] = "9271834";
            doc.Targets.Add(freighter);

            RadarTarget lost = new RadarTarget("T3")
            {
                Label = "未知目标",
                Bearing = 295d,
                Distance = 41.5d,
                Lost = true,
            };
            lost.Extra["type"] = "待识别";
            doc.Targets.Add(lost);

            RadarTarget speedboat = new RadarTarget("T4")
            {
                Label = "快艇 A-07",
                Bearing = 210d,
                Distance = 9.0d,
                Speed = 24.0d,
                Course = 195d,
                Color = new SKColor(90, 210, 255),
            };
            AddTrack(speedboat, 186d, 21.4d, 192d, 18.2d, 198d, 14.6d, 204d, 11.6d, 208d, 10.2d, 210d, 9d);
            speedboat.Extra["type"] = "快艇";
            doc.Targets.Add(speedboat);

            doc.InfoPanel.Position = RadarPanelPosition.Right;
            doc.InfoPanel.Title = "目标信息";
            doc.InfoPanel.Fields.Clear();
            doc.InfoPanel.Fields.Add(new RadarInfoField("id", "批号"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("bearing"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("distance", "距离", "0.0"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("speed"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("course"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("status"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("extra.type", "类型"));

            return doc;
        }

        /// <summary>
        /// 180° 扇区（覆盖上方 270°→90°）：量程 24 海里，扫描线 310°，
        /// 保留 8 个轨迹点，信息面板横排在下方。
        /// </summary>
        public static RadarDocument BuildHalf()
        {
            RadarDocument doc = new RadarDocument
            {
                FormTitle = "雷达态势图（180° 扇区）",
                Scope = RadarScope.Half,
                HalfStartBearing = 270d,
                MaxRange = 24d,
                RangeUnit = "nmi",
                SpeedUnit = "kn",
                RingCount = 4,
                ShowSweep = true,
                SweepAngle = 310d,
                SweepBeamWidth = 34d,
                ShowTrails = true,
                TrailLength = 8,
                ShowNorthIndicator = true,
                SelectedTargetId = "B2",
            };

            RadarTarget ferry = new RadarTarget("B1")
            {
                Label = "海峡轮渡",
                Bearing = 338d,
                Distance = 6.5d,
                Speed = 16.0d,
                Course = 352d,
            };
            AddTrack(ferry, 318d, 9.8d, 322d, 9.1d, 326d, 8.4d, 330d, 7.7d, 333d, 7.2d,
                335d, 6.9d, 338d, 6.5d);
            ferry.Extra["type"] = "轮渡";
            doc.Targets.Add(ferry);

            RadarTarget tanker = new RadarTarget("B2")
            {
                Label = "油轮 昆仑",
                Bearing = 12d,
                Distance = 12.2d,
                Speed = 9.4d,
                Course = 20d,
            };
            AddTrack(tanker, 2d, 14.8d, 4d, 14.1d, 6d, 13.5d, 8d, 13.1d, 10d, 12.6d, 12d, 12.2d);
            tanker.Extra["type"] = "油轮";
            doc.Targets.Add(tanker);

            RadarTarget patrol = new RadarTarget("B3")
            {
                Label = "海巡 06",
                Bearing = 58d,
                Distance = 19.0d,
                Speed = 14.2d,
                Course = 64d,
            };
            AddTrack(patrol, 50d, 20.8d, 52d, 20.4d, 54d, 19.9d, 56d, 19.5d, 58d, 19d);
            patrol.Extra["type"] = "公务船";
            doc.Targets.Add(patrol);

            RadarTarget unknown = new RadarTarget("B4")
            {
                Label = "小型目标",
                Bearing = 83d,
                Distance = 22.4d,
                Lost = true,
            };
            unknown.Extra["type"] = "待识别";
            doc.Targets.Add(unknown);

            doc.InfoPanel.Position = RadarPanelPosition.Bottom;
            doc.InfoPanel.Title = "目标信息";
            doc.InfoPanel.Fields.Clear();
            doc.InfoPanel.Fields.Add(new RadarInfoField("bearing"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("distance"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("speed"));
            doc.InfoPanel.Fields.Add(new RadarInfoField("extra.type", "类型"));

            return doc;
        }

        private static void AddTrack(RadarTarget target, params double[] bearingDistancePairs)
        {
            for (int i = 0; i + 1 < bearingDistancePairs.Length; i += 2)
            {
                target.Track.Add(new RadarTrackPoint(bearingDistancePairs[i], bearingDistancePairs[i + 1]));
            }
        }
    }
}
