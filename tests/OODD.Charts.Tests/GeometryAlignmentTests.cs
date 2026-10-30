using System;
using System.Collections.Generic;
using OODD.Charts.Geometry;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A1：三测单渲染产物与参考图逐格对齐。
    /// 判据不看几何对象，只看渲染出来的像素里真实存在哪些线、间距是多少。
    /// </summary>
    public sealed class GeometryAlignmentTests
    {
        private const double Coverage = 0.6;

        [Fact]
        public void 体温轴横线_细格15px粗格75px()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                IReadOnlyList<double> rules = probe.StrongHorizontalLines(
                    geo.GridRect.Top, geo.GridRect.Bottom,
                    geo.GridLeft + 2, geo.GridRight - 2,
                    Coverage);

                Assert.True(rules.Count > 10, "只扫到 " + rules.Count + " 条横线，版面没画出来");

                List<double> gaps = new List<double>(ImageProbe.Gaps(rules));
                int offGrid = 0;
                foreach (double gap in gaps)
                {
                    if (Math.Abs(gap - 15d) > 1d)
                    {
                        offGrid++;
                    }
                }
                Assert.True(offGrid <= gaps.Count / 20,
                    "细格距应为 15px，偏差项 " + offGrid + "/" + gaps.Count + "，首几距=" + Join(gaps));

                // 1℃ = 5 个 0.2℃ 细格 = 75px：跨 5 个间隔求和，避开"哪条是粗线"的判定。
                for (int i = 0; i + 5 < rules.Count; i++)
                {
                    double span = rules[i + 5] - rules[i];
                    Assert.True(Math.Abs(span - 75d) <= 1d,
                        "第 " + i + " 条起跨 1℃ 应为 75px，实测 " + span.ToString("0.#"));
                }
            }
        }

        [Fact]
        public void 竖线_槽宽20px合并日列120px()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                IReadOnlyList<double> columns = probe.StrongVerticalLines(
                    geo.GridLeft, geo.GridRight,
                    geo.GridRect.Top + 1, geo.GridRect.Bottom - 1,
                    Coverage);

                Assert.True(columns.Count >= geo.Slots.TotalSlots,
                    "竖线数 " + columns.Count + " 少于槽数 " + geo.Slots.TotalSlots);

                List<double> gaps = new List<double>(ImageProbe.Gaps(columns));
                foreach (double gap in gaps)
                {
                    Assert.True(Math.Abs(gap - 20d) <= 1d, "槽宽应为 20px，实测 " + gap.ToString("0.#"));
                }

                // 一日 6 槽 = 120px：合并列的边界必须正好落在每 6 条线处。
                int perGroup = geo.Slots.SlotsPerGroup;
                for (int g = 0; g + perGroup < columns.Count; g += perGroup)
                {
                    double span = columns[g + perGroup] - columns[g];
                    Assert.True(Math.Abs(span - 120d) <= 1d,
                        "合并日列宽应为 120px，实测 " + span.ToString("0.#"));
                }
            }
        }

        [Fact]
        public void 附属行行高30px()
        {
            using (Chart chart = TestDocuments.Temperature())
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                ChartGeometry geo = chart.Geometry;
                IReadOnlyList<double> lines = probe.StrongHorizontalLines(
                    geo.CellTable.Rect.Top, geo.CellTable.Rect.Bottom,
                    2, geo.Bounds.Right - 2,
                    Coverage);

                Assert.True(lines.Count >= geo.CellRows.Count + 1,
                    "附属行分隔线只扫到 " + lines.Count + " 条，应有 " + (geo.CellRows.Count + 1) + " 条");

                foreach (CellRow row in geo.CellRows)
                {
                    // 呼吸行按国标只有合并行的一半高（15px），其余每行 30px。
                    double expect = row.SplitBySlot ? 15d : 30d;
                    Assert.True(Math.Abs(row.Rect.Height - expect) <= 0.5d,
                        row.Key + " 行高应为 " + expect + "px，实测 " + row.Rect.Height.ToString("0.#"));
                    AssertLineAt(lines, row.Rect.Top, row.Key + " 上边线");
                    AssertLineAt(lines, row.Rect.Bottom, row.Key + " 下边线");

                    if (!row.SplitBySlot)
                    {
                        Assert.False(LineNear(lines, row.Rect.MidY, 2d),
                            row.Key + " 这一格中间出现了横线，合并格被切开了");
                    }
                }
            }
        }

        private static void AssertLineAt(IReadOnlyList<double> lines, double expectedY, string what)
        {
            Assert.True(LineNear(lines, expectedY, 1.5d),
                what + " 没有落在 " + expectedY.ToString("0.#") + "px 处");
        }

        private static bool LineNear(IReadOnlyList<double> lines, double y, double tolerance)
        {
            foreach (double line in lines)
            {
                if (Math.Abs(line - y) <= tolerance)
                {
                    return true;
                }
            }
            return false;
        }

        private static string Join(List<double> values)
        {
            return string.Join(", ", values.ConvertAll(v => v.ToString("0.#")).ToArray());
        }
    }
}
