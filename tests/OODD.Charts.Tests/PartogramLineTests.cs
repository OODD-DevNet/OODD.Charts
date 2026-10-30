using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using OODD.Charts.Medical.Partogram;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A4：产程图两条参考线的斜率与间距，只从渲染像素反查。
    /// <para>
    /// 判据刻意写死教科书约定（1 cm/h、处理线右移 4 小时、1 小时 = 2 槽 × 20px = 40px、
    /// 1 cm = 30px），而不是从 <c>doc.AlertSlopeCmPerHour</c> 回读——
    /// 否则模板把斜率画错而文档参数不变时，测试会跟着错。
    /// </para>
    /// </summary>
    public sealed class PartogramLineTests
    {
        private const double PixelsPerHour = 40d;
        private const double PixelsPerCm = 30d;
        private const double AlertStartCm = 3d;
        private const double ActionOffsetHours = 4d;

        [Fact]
        public void 宫口轴每1cm一行高30px()
        {
            using (Chart chart = TestDocuments.Partogram())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                IReadOnlyList<double> rules = probe.StrongHorizontalLines(
                    geo.GridRect.Top, geo.GridRect.Bottom,
                    geo.GridLeft + 2, geo.GridRight - 2, 0.6);

                Assert.True(rules.Count >= 10, "只扫到 " + rules.Count + " 条横线（0–10cm 应有 11 条）");
                foreach (double gap in ImageProbe.Gaps(rules))
                {
                    Assert.True(Math.Abs(gap - PixelsPerCm) <= 1d,
                        "宫口每 1cm 应为 30px，实测 " + gap.ToString("0.#"));
                }
            }
        }

        [Fact]
        public void 警戒线按1cm每小时下行()
        {
            using (Chart chart = TestDocuments.Partogram())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                PartogramDocument doc = (PartogramDocument)chart.Document;

                // 正证：h 小时后线应当走到 3+h cm 那一行，红墨就在那里。
                for (int hour = 1; hour <= 5; hour++)
                {
                    AssertRedAt(probe, geo, doc, hour, AlertStartCm + hour, true,
                        "警戒线第 " + hour + " 小时");
                }

                // 反证：同一 x 上、上下各偏半格（15px）处不该有红墨。
                // 若斜率画成 0.5 或 2 cm/h，线会整体挪位，红墨就落到这两个框里。
                // 从 h=2 起查：h=1 的上方 15px 正好压在"警戒线"三个红字上。
                for (int hour = 2; hour <= 4; hour++)
                {
                    double cm = AlertStartCm + hour;
                    AssertRedAt(probe, geo, doc, hour, cm + 0.5d, false, "警戒线上方半格（h=" + hour + "）");
                    AssertRedAt(probe, geo, doc, hour, cm - 0.5d, false, "警戒线下方半格（h=" + hour + "）");
                }
            }
        }

        [Fact]
        public void 处理线在警戒线右侧四小时()
        {
            using (Chart chart = TestDocuments.Partogram())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                PartogramDocument doc = (PartogramDocument)chart.Document;

                foreach (double cm in new[] { 4d, 4.5d })
                {
                    int y = (int)Math.Round(geo.YOf(PartogramDocument.AxisDilation, cm));
                    List<double> reds = CollectRedCenters(probe, geo, y);

                    double alertX = PartogramChartTemplate.XOfHour(geo, doc, cm - AlertStartCm);
                    double actionX = alertX + ActionOffsetHours * PixelsPerHour;

                    double alert = Nearest(reds, alertX);
                    double action = Nearest(reds, actionX);

                    Assert.True(Math.Abs(alert - alertX) <= 2.5d,
                        cm + "cm 行上没找到警戒线：期望 x=" + alertX.ToString("0.#")
                        + "，该行红段=" + Join(reds));
                    Assert.True(Math.Abs(action - actionX) <= 2.5d,
                        cm + "cm 行上没找到处理线：期望 x=" + actionX.ToString("0.#")
                        + "，该行红段=" + Join(reds));
                    Assert.True(Math.Abs((action - alert) - ActionOffsetHours * PixelsPerHour) <= 5d,
                        cm + "cm 行上两条线间距 " + (action - alert).ToString("0.#") + "px，应为 160px");
                }
            }
        }

        [Fact]
        public void 产程图各图元都落墨()
        {
            using (Chart chart = TestDocuments.Partogram())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                PartogramDocument doc = (PartogramDocument)chart.Document;

                foreach (KeyValuePair<int, double> point in doc.Cervix.Values)
                {
                    int x = (int)Math.Round(geo.SlotCenterX(point.Key));
                    int y = (int)Math.Round(geo.YOf(PartogramDocument.AxisDilation, point.Value));
                    Assert.True(probe.HasInkInBox(x - 5, y - 5, x + 6, y + 6),
                        "宫口曲线槽 " + point.Key + "（" + point.Value + "cm）没有落墨");
                }

                foreach (KeyValuePair<int, double> point in doc.Station.Values)
                {
                    int x = (int)Math.Round(geo.SlotCenterX(point.Key));
                    int y = (int)Math.Round(geo.YOf(PartogramDocument.AxisStation, point.Value));
                    Assert.True(probe.HasInkInBox(x - 5, y - 5, x + 6, y + 6),
                        "先露曲线槽 " + point.Key + "（S" + point.Value + "）没有落墨");
                }

                BandRow? contraction = geo.CellTable.Row(PartogramFields.Contraction);
                Assert.NotNull(contraction);
                SKRect blockCell = geo.Slots.SlotRect(2, contraction!.Rect.Top, contraction.Rect.Bottom);
                Assert.True(probe.NonWhiteRatio(blockCell) > 0.05d,
                    "槽 2 的宫缩方块区非白占比过低：" + probe.NonWhiteRatio(blockCell).ToString("0.000"));

                // 破膜：箭头从曲线上的点向左上伸出，尾端再往左写"破"字。
                double cm = doc.Cervix.ValueInterpolatedAt(1) ?? double.NaN;
                int tipX = (int)Math.Round(geo.SlotCenterX(1));
                int tipY = (int)Math.Round(geo.YOf(PartogramDocument.AxisDilation, cm));
                Assert.True(probe.HasRedInBox(tipX - 30, tipY - 16, tipX + 3, tipY + 9),
                    "破膜箭头（槽 1，" + cm.ToString("0.##") + "cm）附近没有红墨");
            }
        }

        /// <summary>把"第 hour 小时、宫口 cm"这个约定点换算成像素，检查 ±2px 内有无红墨。</summary>
        private static void AssertRedAt(ImageProbe probe, ChartGeometry geo, PartogramDocument doc,
            double hour, double cm, bool expectRed, string what)
        {
            int x = (int)Math.Round(PartogramChartTemplate.XOfHour(geo, doc, hour));
            int y = (int)Math.Round(geo.YOf(PartogramDocument.AxisDilation, cm));
            bool red = probe.HasRedInBox(x - 2, y - 2, x + 3, y + 3);
            Assert.True(red == expectRed,
                what + "：约定落点 (" + x + "," + y + ") 期望有红墨=" + expectRed
                + "，实测=" + red + "（±2px 邻域）");
        }

        /// <summary>取 y-1..y+1 三行的红段中心并合并（斜线在相邻行只差 1px，合并后更像一条线的落点）。</summary>
        private static List<double> CollectRedCenters(ImageProbe probe, ChartGeometry geo, int y)
        {
            List<double> all = new List<double>();
            for (int row = y - 1; row <= y + 1; row++)
            {
                foreach (double center in probe.RedRunsInRow(row, geo.GridLeft + 1, geo.GridRight - 1))
                {
                    all.Add(center);
                }
            }
            all.Sort();
            return all;
        }

        private static double Nearest(List<double> values, double target)
        {
            double best = double.NaN;
            double bestDelta = double.MaxValue;
            foreach (double value in values)
            {
                double delta = Math.Abs(value - target);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = value;
                }
            }
            return best;
        }

        private static string Join(List<double> values)
        {
            return string.Join(", ", values.ConvertAll(v => v.ToString("0.#")).ToArray());
        }
    }
}
