using System;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Partogram;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A3：护理惯例的红蓝双色。脉搏 ● 红、体温 ○ 蓝，产程图越界胎心红圈。
    /// 判据取测点周围像素的平均色／是否存在红墨，避免"符号是空心的就采不到色"的假阴性。
    /// </summary>
    public sealed class ColorConventionTests
    {
        /// <summary>槽 4：腋温 38.1℃ 与脉搏 96 次/分（等效 37.8℃）在像素上差 22.5px，采样框不互相污染。</summary>
        private const int Slot = 4;

        private const double AxillaryCelsius = 38.1d;
        private const double PulseRate = 96d;

        [Fact]
        public void 两个测点的采样框不重叠()
        {
            using (Chart chart = TestDocuments.Temperature())
            {
                ChartGeometry geo = chart.Geometry;
                double delta = Math.Abs(geo.YOf(TemperatureChartTemplate.AxisPulse, PulseRate)
                                        - geo.YOf(TemperatureChartTemplate.AxisTemperature, AxillaryCelsius));
                Assert.True(delta >= 12d,
                    "测点太近（" + delta.ToString("0.#") + "px），红蓝断言会互相污染，换一个槽位");
            }
        }

        [Fact]
        public void 脉搏点采出来是红()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                SKColor ink = ProbeAround(probe, chart.Geometry, TemperatureChartTemplate.AxisPulse,
                    Slot, PulseRate);

                Assert.True(ink.Red > 150, "脉搏点红色分量过低：" + ink.Red + "," + ink.Green + "," + ink.Blue);
                Assert.True(ink.Red > ink.Green + 60 && ink.Red > ink.Blue + 60,
                    "脉搏点不偏红：" + ink.Red + "," + ink.Green + "," + ink.Blue);
            }
        }

        [Fact]
        public void 体温点采出来是蓝()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                SKColor ink = ProbeAround(probe, chart.Geometry, TemperatureChartTemplate.AxisTemperature,
                    Slot, AxillaryCelsius);

                Assert.True(ink.Blue > 150, "体温点蓝色分量过低：" + ink.Red + "," + ink.Green + "," + ink.Blue);
                Assert.True(ink.Blue > ink.Red + 60,
                    "体温点不偏蓝：" + ink.Red + "," + ink.Green + "," + ink.Blue);
            }
        }

        [Fact]
        public void 异常胎心圈注为红而正常胎心不圈()
        {
            using (Chart chart = TestDocuments.Partogram())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                PartogramDocument doc = (PartogramDocument)chart.Document;
                BandRow? band = geo.CellTable.Row(PartogramFields.FetalHeart);
                Assert.NotNull(band);

                FetalHeartReading abnormal = Find(doc, 7);
                FetalHeartReading normal = Find(doc, 6);
                Assert.True(doc.IsAbnormal(abnormal), "测试数据本身应判为异常（168 > 160）");
                Assert.False(doc.IsAbnormal(normal), "对照读数应为正常（146）");
                Assert.Equal(normal.Slot + 1, abnormal.Slot);

                int bandTop = (int)band!.Rect.Top + 2;
                int bandBottom = (int)band.Rect.Bottom - 2;
                int midY = (int)band.Rect.MidY;

                // 实测（槽宽 20px，胎心带 Top=404/Bottom=430）：
                // 槽 7 的 168 红字落在 centerX±6，圈注椭圆左弧在 centerX-11、右弧在 centerX+10.5；
                // 所以对照读数的采样框右界必须止于 centerX+8，否则会被隔壁槽的圈注蹭到。
                Assert.True(probe.HasRedInBox(CenterX(geo, abnormal) - 8, bandTop,
                        CenterX(geo, abnormal) + 9, bandBottom),
                    "异常胎心 168 的数字不是红的");
                Assert.True(probe.HasRedInBox(CenterX(geo, abnormal) - 12, midY - 4,
                        CenterX(geo, abnormal) - 9, midY + 5),
                    "异常胎心 168 外侧没有圈注椭圆的左弧");

                Assert.True(probe.HasInkInBox(CenterX(geo, normal) - 8, bandTop,
                        CenterX(geo, normal) + 8, bandBottom),
                    "对照读数本身没有墨，'不是红的'就成了空判");
                Assert.False(probe.HasRedInBox(CenterX(geo, normal) - 8, bandTop,
                        CenterX(geo, normal) + 8, bandBottom),
                    "正常胎心 146 的数字被画成红的了");
                Assert.False(probe.HasRedInBox(CenterX(geo, normal) - 12, midY - 4,
                        CenterX(geo, normal) - 9, midY + 5),
                    "正常胎心 146 外侧出现了圈注，异常判定用错了");
            }
        }

        private static FetalHeartReading Find(PartogramDocument doc, int slot)
        {
            foreach (FetalHeartReading reading in doc.FetalHeart)
            {
                if (reading.Slot == slot)
                {
                    return reading;
                }
            }
            throw new InvalidOperationException("测试文档缺少槽 " + slot + " 的胎心读数。");
        }

        private static int CenterX(ChartGeometry geo, FetalHeartReading reading)
        {
            return (int)Math.Round(geo.SlotCenterX(reading.Slot));
        }

        private static SKColor ProbeAround(ImageProbe probe, ChartGeometry geo, string axisKey, int slot, double value)
        {
            int x = (int)Math.Round(geo.SlotCenterX(slot));
            int y = (int)Math.Round(geo.YOf(axisKey, value));
            return probe.AverageInkColor(new SKRect(x - 5, y - 5, x + 6, y + 6));
        }
    }
}
