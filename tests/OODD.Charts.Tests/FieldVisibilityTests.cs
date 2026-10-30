using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A2：三测单 12 类数据项全部可见。判据是"该项所在格子里真的有墨"，
    /// 而不是"文档里挂了这条数据"。
    /// </summary>
    public sealed class FieldVisibilityTests
    {
        [Fact]
        public void 十类附属行的取值格子有墨()
        {
            Dictionary<string, int> slots = new Dictionary<string, int>
            {
                { TemperatureFields.Respiration, 5 },
                { TemperatureFields.BloodPressure, 6 },
                { TemperatureFields.Intake, 6 },
                { TemperatureFields.Output, 6 },
                { TemperatureFields.Stool, 6 },
                { TemperatureFields.Urine, 6 },
                { TemperatureFields.Weight, 6 },
                { TemperatureFields.Height, 0 },
                { TemperatureFields.Allergy, 0 },
                { TemperatureFields.Other, 12 },
            };

            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                List<string> tooFaint = new List<string>();
                foreach (KeyValuePair<string, int> pair in slots)
                {
                    SKRect cell = geo.CellRect(pair.Key, pair.Value);
                    double ratio = probe.NonWhiteRatio(cell);
                    if (ratio <= 0.005)
                    {
                        tooFaint.Add(pair.Key + " 槽" + pair.Value + " 非白占比 " + ratio.ToString("0.0000"));
                    }
                }

                Assert.True(tooFaint.Count == 0, "以下字段所在格几乎空白：" + string.Join("; ", tooFaint.ToArray()));
            }
        }

        [Fact]
        public void 体温与脉搏曲线的每个测点都有墨()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                TemperatureChartDocument doc = (TemperatureChartDocument)chart.Document;

                List<string> missing = new List<string>();
                foreach (PointSeries series in doc.Series)
                {
                    foreach (KeyValuePair<int, double> point in series.Values)
                    {
                        int x = (int)Math.Round(geo.SlotCenterX(point.Key));
                        int y = (int)Math.Round(geo.YOf(series.AxisKey, point.Value));
                        if (!probe.HasInkInBox(x - 5, y - 5, x + 6, y + 6))
                        {
                            missing.Add(series.Key + "@" + point.Key + "(" + x + "," + y + ")");
                        }
                    }
                }

                Assert.True(missing.Count == 0, "以下测点没有落墨：" + string.Join(", ", missing.ToArray()));
            }
        }

        [Fact]
        public void 页眉患者信息与日期带都有墨()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;

                Assert.True(probe.NonWhiteRatio(geo.Header.RowRect(TemperatureChartTemplate.RowHospital)) > 0.01,
                    "医院名行为空白");
                Assert.True(probe.NonWhiteRatio(geo.Header.RowRect(TemperatureChartTemplate.RowTitle)) > 0.01,
                    "表题行为空白");
                Assert.True(probe.NonWhiteRatio(geo.Header.RowRect(TemperatureChartTemplate.RowPatient)) > 0.005,
                    "患者信息行为空白");
                Assert.True(probe.NonWhiteRatio(geo.DateBand.RowRect(TemperatureChartTemplate.RowDate)) > 0.005,
                    "日期行为空白");
                Assert.True(probe.NonWhiteRatio(geo.DateBand.RowRect(TemperatureChartTemplate.RowWeek)) > 0.005,
                    "星期行为空白");
                Assert.True(probe.NonWhiteRatio(geo.DateBand.RowRect(TemperatureChartTemplate.RowTime)) > 0.005,
                    "时间行为空白");
            }
        }
    }
}
