using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using OODD.Charts.Transit.DangerousGoods;
using OODD.Charts.Transit.LoadBalance;
using OODD.Charts.Transit.OversizeLoad;
using OODD.Charts.Transit.Stowage;
using OODD.Charts.Transit.TrainGraph;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 波次 6 / 腿 B：交通域五张表单（T250–T254）的验收测试。
    /// <para>
    /// 每条断言都来自 docs/06 §4.2/§4.5/§4.12/§4.13/§4.14 的判据：
    /// 期望值一律由文档数据与版面令牌现算，不写"记忆值"；
    /// 关键结论都走 <see cref="Chart.Encode"/> 真出 PNG 再用 <see cref="ImageProbe"/> 量像素，
    /// 需要"这里没有墨"的地方都配了同口径的正控。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class TransitFormsTests
    {
        private const double Eps = 1e-3;

        // ================================================================== T250 列车运行图

        [Fact]
        public void T250_站名线格距按区间运行时分加权且铺满全高()
        {
            TrainGraphDocument doc = TrainSample();
            TrainGraphTemplate template = new TrainGraphTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                TrainGraphLayout layout = geometry.LayoutOf<TrainGraphLayout>()!;
                Assert.True(layout != null, "运行图版面没有挂到 ChartGeometry 上。");

                double total = doc.TotalRunMinutes;
                double height = layout.PlotRect.Height;
                double sum = 0d;
                for (int i = 0; i + 1 < doc.Stations.Count; i++)
                {
                    double gap = layout.StationLineY(i + 1) - layout.StationLineY(i);
                    double expected = doc.Stations[i].RunMinutesToNext / total * height;
                    sum += gap;
                    Assert.True(Math.Abs(gap - expected) <= 0.51,
                        "第 " + (i + 1) + " 段站名线间距应为 " + expected.ToString("0.###", CultureInfo.InvariantCulture)
                        + "px（权重 " + doc.Stations[i].RunMinutesToNext + "/" + total + "），实测 "
                        + gap.ToString("0.###", CultureInfo.InvariantCulture) + "px");
                }
                Assert.True(Math.Abs(sum - height) <= 0.51,
                    "站名线总间距应等于绘图区高 " + height.ToString("0.###", CultureInfo.InvariantCulture)
                    + "px，实测 " + sum.ToString("0.###", CultureInfo.InvariantCulture) + "px");

                IReadOnlyList<double> rules = probe.StrongHorizontalLines(
                    layout.PlotRect.Top - 1, layout.PlotRect.Bottom + 1,
                    layout.PlotRect.Left + 2, layout.PlotRect.Right - 2, 0.85);
                Assert.True(rules.Count == doc.Stations.Count,
                    "站名线应 " + doc.Stations.Count + " 条，实测 " + rules.Count + " 条");
                for (int i = 0; i < doc.Stations.Count; i++)
                {
                    Assert.True(Math.Abs(rules[i] - layout.StationLineY(i)) <= 1d,
                        "第 " + (i + 1) + " 条站名线像素位置应 "
                        + layout.StationLineY(i).ToString("0.##", CultureInfo.InvariantCulture)
                        + "，实测 " + rules[i].ToString("0.##", CultureInfo.InvariantCulture));
                }
            }
        }

        [Fact]
        public void T250_列车在站界的落点精确落在站名线上()
        {
            TrainGraphDocument doc = TrainSample();
            TrainGraphTemplate template = new TrainGraphTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                TrainGraphLayout layout = geometry.LayoutOf<TrainGraphLayout>()!;
                IReadOnlyList<SKPoint> points = layout.PointsOf(doc.Trains[0]);
                Assert.True(points.Count >= 3, "首列车至少有 3 个运行线顶点，实测 " + points.Count);

                Assert.True(Math.Abs(points[0].Y - layout.StationLineY(0)) <= 1e-4,
                    "首站界点 y 应等于站名线 y=" + layout.StationLineY(0).ToString("0.###", CultureInfo.InvariantCulture)
                    + "，实测 " + points[0].Y.ToString("0.###", CultureInfo.InvariantCulture));
                Assert.True(probe.HasInkInBox((int)points[0].X - 2, (int)points[0].Y - 2,
                        (int)points[0].X + 3, (int)points[0].Y + 3),
                    "首站界点 (" + points[0].X.ToString("0.#") + "," + points[0].Y.ToString("0.#")
                    + ") 周围没有墨，运行线没有落在站名线上");

                // 第二个停站（B 站，站序 1）的到达与发车两点 y 都必须在同一站名线上。
                SKPoint arrive = points[1];
                SKPoint depart = points[2];
                Assert.True(Math.Abs(arrive.Y - layout.StationLineY(1)) <= 1e-4
                            && Math.Abs(depart.Y - layout.StationLineY(1)) <= 1e-4,
                    "B 站到发两点 y 应同为 " + layout.StationLineY(1).ToString("0.###", CultureInfo.InvariantCulture)
                    + "，实测 " + arrive.Y.ToString("0.###", CultureInfo.InvariantCulture)
                    + " / " + depart.Y.ToString("0.###", CultureInfo.InvariantCulture));
                Assert.True(depart.X > arrive.X, "B 站停站段应向右延伸：到 "
                    + arrive.X.ToString("0.#") + " 发 " + depart.X.ToString("0.#"));
            }
        }

        [Fact]
        public void T250_四级时间刻度线宽与令牌一致且逐级变粗()
        {
            TrainGraphDocument doc = TrainSample();
            TrainGraphTemplate template = new TrainGraphTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                TrainGraphLayout layout = geometry.LayoutOf<TrainGraphLayout>()!;
                int y0 = (int)layout.PlotRect.Bottom + 3;
                int y1 = (int)layout.PlotRect.Bottom + 8;

                // 粗刻度线宽 3px，±1 三列窗口会把三级、四级刻度一起截平（实测 594 vs 660），取 ±2 才容得下整条粗刻度。
                double hairline = InkMass(probe, layout.TimeAxis.PositionOf(2d), y0, y1, 2);
                double fine = InkMass(probe, layout.TimeAxis.PositionOf(25d), y0, y1, 2);
                double medium = InkMass(probe, layout.TimeAxis.PositionOf(10d), y0, y1, 2);
                double heavy = InkMass(probe, layout.TimeAxis.PositionOf(60d), y0, y1, 2);

                Assert.True(hairline > 0d && fine > 0d && medium > 0d && heavy > 0d,
                    "四级刻度都应有落墨：1=" + hairline.ToString("0.#") + " 5=" + fine.ToString("0.#")
                    + " 10=" + medium.ToString("0.#") + " 60=" + heavy.ToString("0.#"));
                Assert.True(fine > hairline * 1.25,
                    "5 分刻度应明显粗于 1 分：墨量 " + fine.ToString("0.#") + " vs " + hairline.ToString("0.#"));
                Assert.True(medium > fine * 1.25,
                    "10 分刻度应明显粗于 5 分：墨量 " + medium.ToString("0.#") + " vs " + fine.ToString("0.#"));
                Assert.True(heavy > medium * 1.25,
                    "60 分刻度应明显粗于 10 分：墨量 " + heavy.ToString("0.#") + " vs " + medium.ToString("0.#"));

                TrainGraphStyle tokens = layout.Tokens;
                Assert.True(tokens.StrokeOf(RulerTier.Hairline) == tokens.HairlineStroke
                            && tokens.StrokeOf(RulerTier.Fine) == tokens.FineStroke
                            && tokens.StrokeOf(RulerTier.Medium) == tokens.MediumStroke
                            && tokens.StrokeOf(RulerTier.Heavy) == tokens.HeavyStroke,
                    "刻度线宽必须直读令牌：实测 "
                    + tokens.StrokeOf(RulerTier.Hairline) + "/" + tokens.StrokeOf(RulerTier.Fine)
                    + "/" + tokens.StrokeOf(RulerTier.Medium) + "/" + tokens.StrokeOf(RulerTier.Heavy));
                Assert.True(tokens.HairlineStroke < tokens.FineStroke
                            && tokens.FineStroke < tokens.MediumStroke
                            && tokens.MediumStroke < tokens.HeavyStroke,
                    "令牌线宽必须逐级递增："
                    + tokens.HairlineStroke + " < " + tokens.FineStroke + " < "
                    + tokens.MediumStroke + " < " + tokens.HeavyStroke);
            }
        }

        [Fact]
        public void T250_天窗带按时刻与站区间裁剪()
        {
            TrainGraphDocument doc = TrainSample();
            TrainGraphTemplate template = new TrainGraphTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                TrainGraphLayout layout = geometry.LayoutOf<TrainGraphLayout>()!;
                TrainWindow window = doc.Windows[0];
                SKRect rect = layout.WindowRect(window);

                Assert.True(Math.Abs(rect.Left - layout.TimeAxis.PositionOf(window.FromMinutes)) <= 0.51,
                    "天窗左边应落在 " + window.FromMinutes + " 分处，实测 x="
                    + rect.Left.ToString("0.###", CultureInfo.InvariantCulture));
                Assert.True(Math.Abs(rect.Right - layout.TimeAxis.PositionOf(window.ToMinutes)) <= 0.51,
                    "天窗右边应落在 " + window.ToMinutes + " 分处，实测 x="
                    + rect.Right.ToString("0.###", CultureInfo.InvariantCulture));
                Assert.True(Math.Abs(rect.Top - layout.StationLineY(window.FromStation)) <= 0.51
                            && Math.Abs(rect.Bottom - layout.StationLineY(window.ToStation)) <= 0.51,
                    "天窗上下应贴站名线 " + layout.StationLineY(window.FromStation).ToString("0.##", CultureInfo.InvariantCulture)
                    + "–" + layout.StationLineY(window.ToStation).ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实测 " + rect.Top.ToString("0.##", CultureInfo.InvariantCulture)
                    + "–" + rect.Bottom.ToString("0.##", CultureInfo.InvariantCulture));

                float x = rect.Right - 10f;
                SKRect inside = new SKRect(x - 4f, rect.Top + rect.Height * 0.4f, x + 4f,
                    rect.Top + rect.Height * 0.4f + 12f);
                SKRect above = new SKRect(x - 4f, rect.Top - 12f, x + 4f, rect.Top - 3f);
                double insideRatio = probe.NonWhiteRatio(inside);
                double aboveRatio = probe.NonWhiteRatio(above);
                Assert.True(insideRatio > 0.02,
                    "天窗内应有斜纹墨（正控），实测非白比 " + insideRatio.ToString("0.####", CultureInfo.InvariantCulture));
                Assert.True(aboveRatio < insideRatio * 0.6,
                    "天窗上边界外不应延续斜纹：界内 " + insideRatio.ToString("0.####", CultureInfo.InvariantCulture)
                    + " vs 界外 " + aboveRatio.ToString("0.####", CultureInfo.InvariantCulture));

                // 越界天窗（负时刻 / 超过轴长）必须被夹在绘图区内，不能画到页边。
                TrainGraphDocument clamped = TrainSample();
                clamped.Windows.Clear();
                clamped.Windows.Add(new TrainWindow("W9", "越界天窗", -60d, 999d, 0, clamped.Stations.Count - 1));
                TrainGraphLayout clampLayout = TrainGraphLayout.Compute(clamped, template.Tokens);
                SKRect clampRect = clampLayout.WindowRect(clamped.Windows[0]);
                Assert.True(Math.Abs(clampRect.Left - clampLayout.PlotRect.Left) <= 0.51
                            && Math.Abs(clampRect.Right - clampLayout.PlotRect.Right) <= 0.51,
                    "越界天窗应夹在绘图区 " + clampLayout.PlotRect.Left.ToString("0.##", CultureInfo.InvariantCulture)
                    + "–" + clampLayout.PlotRect.Right.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实测 " + clampRect.Left.ToString("0.##", CultureInfo.InvariantCulture)
                    + "–" + clampRect.Right.ToString("0.##", CultureInfo.InvariantCulture));
            }
        }

        // ================================================================== T251 载重平衡

        [Fact]
        public void T251_投点判定与包线判定同源()
        {
            LoadBalanceDocument doc = LoadSample();
            LoadBalanceTemplate template = new LoadBalanceTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                LoadBalanceLayout layout = geometry.LayoutOf<LoadBalanceLayout>()!;

                CgCheckPoint inside = doc.CheckPoints[0];
                CgCheckPoint outside = doc.CheckPoints[1];
                Assert.True(layout.IsInside(inside.CgPercentMac, inside.WeightKg),
                    "检查点 " + inside.Label + "（" + inside.CgPercentMac + "," + inside.WeightKg + "）应判在包线内");
                Assert.True(!layout.IsInside(outside.CgPercentMac, outside.WeightKg),
                    "检查点 " + outside.Label + "（" + outside.CgPercentMac + "," + outside.WeightKg + "）应判在包线外");

                bool contained = false;
                for (int i = 0; i < layout.Envelopes.Count; i++)
                {
                    if (layout.Envelopes[i].Contains(inside.CgPercentMac, inside.WeightKg))
                    {
                        contained = true;
                    }
                }
                Assert.True(contained, "投点判定与包线 Contains 必须一致：Contains 未判内但 IsInside 判内");

                SKPoint pixel = layout.PixelOf(inside.CgPercentMac, inside.WeightKg);
                SKColor insideColor = probe.AverageInkColor(new SKRect(pixel.X - 3f, pixel.Y - 3f,
                    pixel.X + 3f, pixel.Y + 3f));
                SKPoint pixelOut = layout.PixelOf(outside.CgPercentMac, outside.WeightKg);
                SKColor outsideColor = probe.AverageInkColor(new SKRect(pixelOut.X - 3f, pixelOut.Y - 3f,
                    pixelOut.X + 3f, pixelOut.Y + 3f));

                Assert.True(insideColor.Blue > insideColor.Red + 30,
                    "包线内检查点应落蓝点（Secondary），实测 R=" + insideColor.Red + " G=" + insideColor.Green
                    + " B=" + insideColor.Blue);
                Assert.True(outsideColor.Red > outsideColor.Blue + 30,
                    "包线外检查点应落红点（Accent），实测 R=" + outsideColor.Red + " G=" + outsideColor.Green
                    + " B=" + outsideColor.Blue);
            }
        }

        [Fact]
        public void T251_包线顶点顺序不影响判定()
        {
            LoadBalanceDocument doc = LoadSample();
            LoadBalanceLayout layout = LoadBalanceLayout.Compute(doc, new LoadBalanceStyle());

            LoadBalanceDocument reversed = LoadSample();
            reversed.Envelopes.Clear();
            reversed.Envelopes.Add(ReversedEnvelope(doc.Envelopes[0]));
            LoadBalanceLayout reversedLayout = LoadBalanceLayout.Compute(reversed, new LoadBalanceStyle());

            int compared = 0;
            for (double cg = 15d; cg <= 40d; cg += 1d)
            {
                for (double weight = 40000d; weight <= 85000d; weight += 2500d)
                {
                    bool a = layout.Envelopes[0].Contains(cg, weight);
                    bool b = reversedLayout.Envelopes[0].Contains(cg, weight);
                    Assert.True(a == b, "顶点顺序反转后判定翻转：(" + cg + "," + weight + ") 正序=" + a + " 反序=" + b);
                    compared++;
                }
            }
            Assert.True(compared == 26 * 19, "对比网格应为 26×19 点，实测 " + compared + " 点");
        }

        [Fact]
        public void T251_包线边界算内()
        {
            LoadBalanceDocument doc = LoadSample();
            LoadBalanceLayout layout = LoadBalanceLayout.Compute(doc, new LoadBalanceStyle());
            EnvelopePolygon envelope = layout.Envelopes[0];

            for (int i = 0; i < envelope.Vertices.Count; i++)
            {
                SKPoint vertex = envelope.Vertices[i];
                Assert.True(envelope.Contains(vertex.X, vertex.Y),
                    "包线顶点必须算内：第 " + (i + 1) + " 点 (" + vertex.X + "," + vertex.Y + ") 判外");
                Assert.True(layout.IsInside(vertex.X, vertex.Y),
                    "版面投点判定对顶点也该算内：第 " + (i + 1) + " 点判外");
            }

            SKPoint a = envelope.Vertices[0];
            SKPoint b = envelope.Vertices[1];
            double mx = (a.X + b.X) * 0.5d;
            double my = (a.Y + b.Y) * 0.5d;
            Assert.True(envelope.Contains(mx, my),
                "包线边的中点必须算内：边 (" + a.X + "," + a.Y + ")–(" + b.X + "," + b.Y + ") 中点 ("
                + mx.ToString("0.###", CultureInfo.InvariantCulture) + "," + my.ToString("0.###", CultureInfo.InvariantCulture) + ") 判外");

            double far = doc.CgMax + 5d;
            Assert.True(!envelope.Contains(far, doc.WeightMax),
                "包线外远点不应算内：(" + far + "," + doc.WeightMax + ")");
        }

        [Fact]
        public void T251_载重表列中心与图上重心网格对齐()
        {
            LoadBalanceDocument doc = LoadSample();
            LoadBalanceTemplate template = new LoadBalanceTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                LoadBalanceLayout layout = geometry.LayoutOf<LoadBalanceLayout>()!;
                Assert.True(doc.TableCgValues.Count == 4, "样例载重表应有 4 列，实测 " + doc.TableCgValues.Count);

                int y = (int)(layout.TableRect.Bottom - 2f);
                IReadOnlyList<double> tableRules = probe.VerticalRulesAt(y,
                    layout.TableRect.Left + 3f, layout.TableRect.Right - 3f);
                Assert.True(tableRules.Count == doc.TableCgValues.Count,
                    "表体应只有 4 条列位竖线，实测 " + tableRules.Count + " 条");

                int gridY = (int)(layout.PlotRect.Top + 5f);
                IReadOnlyList<double> plotRules = probe.VerticalRulesAt(gridY,
                    layout.PlotRect.Left + 80f, layout.PlotRect.Right - 1f);

                for (int i = 0; i < doc.TableCgValues.Count; i++)
                {
                    float center = layout.ColumnCenter(i);
                    double nearestTable = Nearest(tableRules, center);
                    Assert.True(nearestTable <= 1d,
                        "第 " + (i + 1) + " 列中心 x=" + center.ToString("0.###", CultureInfo.InvariantCulture)
                        + " 与表内竖线最近距离 " + nearestTable.ToString("0.###", CultureInfo.InvariantCulture) + "px");
                    double nearestPlot = Nearest(plotRules, center);
                    Assert.True(nearestPlot <= 1d,
                        "第 " + (i + 1) + " 列中心与图上同 x 的竖线最近距离 "
                        + nearestPlot.ToString("0.###", CultureInfo.InvariantCulture) + "px");
                }

                float between = (layout.ColumnCenter(0) + layout.ColumnCenter(1)) * 0.5f;
                Assert.True(Nearest(tableRules, between) > 1.5d,
                    "两列中线之间不应有竖线（负控）：中点 x=" + between.ToString("0.###", CultureInfo.InvariantCulture)
                    + " 最近竖线 " + Nearest(tableRules, between).ToString("0.###", CultureInfo.InvariantCulture) + "px");
            }
        }

        [Fact]
        public void T251_重量轴标题落在绘图区之外的独占带()
        {
            LoadBalanceDocument doc = LoadSample();
            LoadBalanceTemplate template = new LoadBalanceTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                LoadBalanceLayout layout = geometry.LayoutOf<LoadBalanceLayout>()!;
                SKRect band = layout.WeightAxisTitleRect;

                Assert.True(band.Bottom <= layout.PlotRect.Top + 0.01f,
                    "重量轴标题带下缘 " + band.Bottom.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 应在绘图区上缘 " + layout.PlotRect.Top.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 之上（带内不能再落斜纹与网格）");
                Assert.True(band.Height >= template.Tokens.AxisTitleSize,
                    "标题带高 " + band.Height.ToString("0.##", CultureInfo.InvariantCulture)
                    + " 应容得下 " + template.Tokens.AxisTitleSize + "px 标题");
                Assert.True(band.Top >= layout.NoteRect.Bottom - 0.01f,
                    "标题带不应压到标题下的说明行：带上缘 "
                    + band.Top.ToString("0.##", CultureInfo.InvariantCulture) + "，说明行下缘 "
                    + layout.NoteRect.Bottom.ToString("0.##", CultureInfo.InvariantCulture));

                // 整带有墨不够：带下缘就是绘图区上边框，取整取到那条线会误判。缩进 2px 只认文字。
                SKRect inner = new SKRect(band.Left + 2f, band.Top + 2f, band.Right - 2f, band.Bottom - 2f);
                Assert.True(probe.HasInkInBox((int)Math.Floor(inner.Left), (int)Math.Floor(inner.Top),
                        (int)Math.Ceiling(inner.Right), (int)Math.Ceiling(inner.Bottom)),
                    "重量轴标题带内沿 " + inner.Left.ToString("0.##", CultureInfo.InvariantCulture) + ","
                    + inner.Top.ToString("0.##", CultureInfo.InvariantCulture) + "-"
                    + inner.Right.ToString("0.##", CultureInfo.InvariantCulture) + ","
                    + inner.Bottom.ToString("0.##", CultureInfo.InvariantCulture) + " 内没有标题墨迹（只剩边框不算）");
            }
        }

        // ================================================================== T252 积载图

        [Fact]
        public void T252_同一bay内tier越高y越小()
        {
            StowageDocument doc = StowageSample();
            StowageTemplate template = new StowageTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                StowageLayout layout = geometry.LayoutOf<StowageLayout>()!;
                StowageContainer low = doc.Containers[0];
                StowageContainer high = doc.Containers[1];

                SKRect lowRect = layout.ProfileRectOf(low);
                SKRect highRect = layout.ProfileRectOf(high);
                Assert.True(highRect.Bottom <= lowRect.Top,
                    "tier " + high.Tier + " 应整体高于 tier " + low.Tier + "：高箱底 "
                    + highRect.Bottom.ToString("0.##", CultureInfo.InvariantCulture) + "，低箱顶 "
                    + lowRect.Top.ToString("0.##", CultureInfo.InvariantCulture));
                double step = low.Tier * layout.Tokens.TierHeight;
                double expected = (high.Tier - low.Tier) * (double)layout.Tokens.TierHeight;
                Assert.True(Math.Abs((lowRect.Top - highRect.Top) - expected) <= 0.51,
                    "层间高差应为 " + expected.ToString("0.##", CultureInfo.InvariantCulture)
                    + "px，实测 " + (lowRect.Top - highRect.Top).ToString("0.##", CultureInfo.InvariantCulture));

                Assert.True(probe.NonWhiteRatio(Shrink(lowRect, 6f)) > 0.5,
                    "底层箱块应有填色墨（正控），非白比 "
                    + probe.NonWhiteRatio(Shrink(lowRect, 6f)).ToString("0.####", CultureInfo.InvariantCulture));
                Assert.True(probe.NonWhiteRatio(Shrink(highRect, 6f)) > 0.5,
                    "高层箱块应有填色墨（正控），非白比 "
                    + probe.NonWhiteRatio(Shrink(highRect, 6f)).ToString("0.####", CultureInfo.InvariantCulture));
                Assert.True(step > 0d, "层高令牌必须为正，实测 " + step);
            }
        }

        [Fact]
        public void T252_四十尺箱左对齐跨两个bay位()
        {
            StowageDocument doc = StowageSample();
            StowageTemplate template = new StowageTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                StowageLayout layout = geometry.LayoutOf<StowageLayout>()!;
                StowageContainer forty = doc.Containers[2];
                Assert.True(forty.Size == ContainerSize.Forty, "第 3 只箱应为 40 尺，实测 " + forty.Size);

                int index = layout.Axis.BayIndex(forty.Bay);
                Assert.True(index >= 0 && index + 1 < layout.Axis.BayAxis.Count,
                    "40 尺箱的 bay " + forty.Bay + " 必须有右邻 bay 位");

                float slotLeft = layout.Axis.BayAxis.PositionOf(index);
                float slotRight = layout.Axis.BayAxis.PositionOf(index + 1) + layout.Axis.BayAxis.WidthOf(index + 1);
                SKRect rect = layout.ProfileRectOf(forty);
                Assert.True(Math.Abs(rect.Left - (slotLeft + layout.Tokens.ContainerInset)) <= 0.51,
                    "40 尺箱应左对齐到 bay " + forty.Bay + " 左边界 " + slotLeft.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实测 " + rect.Left.ToString("0.##", CultureInfo.InvariantCulture));
                Assert.True(Math.Abs(rect.Right - (slotRight - layout.Tokens.ContainerInset)) <= 0.51,
                    "40 尺箱右边界应到第二个 bay 位右缘 " + slotRight.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实测 " + rect.Right.ToString("0.##", CultureInfo.InvariantCulture));
                double twoSlots = slotRight - slotLeft;
                Assert.True(Math.Abs(rect.Width - (twoSlots - 2d * layout.Tokens.ContainerInset)) <= 0.51,
                    "箱宽应覆盖两个 bay 位共 " + twoSlots.ToString("0.##", CultureInfo.InvariantCulture)
                    + "px，实测 " + rect.Width.ToString("0.##", CultureInfo.InvariantCulture));

                SKRect leftHalf = new SKRect(rect.Left + 4f, rect.MidY - 6f, rect.Left + 24f, rect.MidY + 6f);
                SKRect rightHalf = new SKRect(rect.Right - 24f, rect.MidY - 6f, rect.Right - 4f, rect.MidY + 6f);
                double leftRatio = probe.NonWhiteRatio(leftHalf);
                double rightRatio = probe.NonWhiteRatio(rightHalf);
                Assert.True(leftRatio > 0.5 && rightRatio > 0.5,
                    "40 尺箱两个 bay 位内都应有填色：左 " + leftRatio.ToString("0.####", CultureInfo.InvariantCulture)
                    + "，右 " + rightRatio.ToString("0.####", CultureInfo.InvariantCulture));
                SKColor lc = probe.AverageInkColor(leftHalf);
                SKColor rc = probe.AverageInkColor(rightHalf);
                Assert.True(Math.Abs(lc.Red - rc.Red) <= 24 && Math.Abs(lc.Green - rc.Green) <= 24
                            && Math.Abs(lc.Blue - rc.Blue) <= 24,
                    "左右两跨应为同一只箱同一底色：左 " + lc.Red + "/" + lc.Green + "/" + lc.Blue
                    + "，右 " + rc.Red + "/" + rc.Green + "/" + rc.Blue);
            }
        }

        [Fact]
        public void T252_row号自舷中向两舷单调增大()
        {
            StowageDocument doc = StowageSample();
            StowageTemplate template = new StowageTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                StowageLayout layout = geometry.LayoutOf<StowageLayout>()!;
                double previous = -1d;
                for (int row = 1; row <= 5; row += 2)
                {
                    double distance = layout.Axis.RowDistance(row);
                    Assert.True(distance > previous,
                        "左舷 row " + row + " 距舷中应更远：实测 " + distance.ToString("0.###", CultureInfo.InvariantCulture)
                        + "px，上一排 " + previous.ToString("0.###", CultureInfo.InvariantCulture) + "px");
                    previous = distance;
                }
                previous = -1d;
                for (int row = 2; row <= 6; row += 2)
                {
                    double distance = layout.Axis.RowDistance(row);
                    Assert.True(distance > previous,
                        "右舷 row " + row + " 距舷中应更远：实测 " + distance.ToString("0.###", CultureInfo.InvariantCulture)
                        + "px，上一排 " + previous.ToString("0.###", CultureInfo.InvariantCulture) + "px");
                    previous = distance;
                }
                Assert.True(layout.Axis.RowCenter(1) < layout.Axis.CenterlineX
                            && layout.Axis.RowCenter(2) > layout.Axis.CenterlineX,
                    "奇数 row 应在舷中左侧、偶数在右侧：01 x="
                    + layout.Axis.RowCenter(1).ToString("0.##", CultureInfo.InvariantCulture)
                    + "，舷中 x=" + layout.Axis.CenterlineX.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，02 x=" + layout.Axis.RowCenter(2).ToString("0.##", CultureInfo.InvariantCulture));

                int y = (int)(layout.Axis.SectionPlot.Bottom + 3f);
                for (int row = 1; row <= 4; row++)
                {
                    float center = layout.Axis.RowCenter(row);
                    Assert.True(probe.HasInkInBox((int)center - 10, y, (int)center + 10, y + 10),
                        "row " + row.ToString("00", CultureInfo.InvariantCulture) + " 的标签墨应落在 x="
                        + center.ToString("0.##", CultureInfo.InvariantCulture) + " 附近");
                }
            }
        }

        [Fact]
        public void T252_同一箱在两个视图的可见性判定一致()
        {
            StowageDocument doc = StowageSample();
            StowageTemplate template = new StowageTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                StowageLayout layout = geometry.LayoutOf<StowageLayout>()!;
                StowageContainer inSection = doc.Containers[2];
                StowageContainer other = doc.Containers[3];

                Assert.True(layout.ProfileVisible(inSection) && layout.ProfileVisible(other),
                    "纵向剖面覆盖全部 bay，两只箱都应在剖面视图可见");
                Assert.True(layout.SectionVisible(inSection),
                    "bay " + inSection.Bay + " 的箱在横剖面（bay " + doc.CrossSectionBay + "）应可见");
                Assert.True(!layout.SectionVisible(other),
                    "bay " + other.Bay + " 的箱不应出现在横剖面（bay " + doc.CrossSectionBay + "）");

                Assert.True(probe.NonWhiteRatio(Shrink(layout.SectionRectOf(inSection), 6f)) > 0.5,
                    "横剖面可见箱应有填色墨（正控），非白比 "
                    + probe.NonWhiteRatio(Shrink(layout.SectionRectOf(inSection), 6f)).ToString("0.####", CultureInfo.InvariantCulture));
                Assert.True(probe.NonWhiteRatio(Shrink(layout.ProfileRectOf(other), 6f)) > 0.5,
                    "另一 bay 的箱在纵向剖面仍应有墨（正控），非白比 "
                    + probe.NonWhiteRatio(Shrink(layout.ProfileRectOf(other), 6f)).ToString("0.####", CultureInfo.InvariantCulture));
                double absent = probe.NonWhiteRatio(Shrink(layout.SectionRectOf(other), 8f));
                Assert.True(absent < 0.02,
                    "横剖面里另一 bay 箱位应留白，实测非白比 " + absent.ToString("0.####", CultureInfo.InvariantCulture));
            }
        }

        // ================================================================== T253 危货联单

        [Fact]
        public void T253_菱形四边等长且顶角朝上()
        {
            DangerousGoodsDocument doc = DangerousGoodsSample();
            DangerousGoodsTemplate template = new DangerousGoodsTemplate();
            using (SKPath symbol = SymbolTriangle())
            {
                template.SymbolPath = symbol;
                ChartGeometry geometry;
                using (ImageProbe probe = Render(doc, template, out geometry))
                {
                    DangerousGoodsLayout layout = geometry.LayoutOf<DangerousGoodsLayout>()!;
                    SKPoint[] v = layout.MainVertices();
                    Assert.True(v.Length == 4, "菱形应有 4 个顶点，实测 " + v.Length);

                    double expectedSide = layout.PlacardSize / Math.Sqrt(2d);
                    for (int i = 0; i < 4; i++)
                    {
                        double side = Distance(v[i], v[(i + 1) % 4]);
                        Assert.True(Math.Abs(side - expectedSide) <= 0.02,
                            "第 " + (i + 1) + " 条边应为 " + expectedSide.ToString("0.###", CultureInfo.InvariantCulture)
                            + "px（对角线 " + layout.PlacardSize + "），实测 "
                            + side.ToString("0.###", CultureInfo.InvariantCulture) + "px");
                    }

                    Assert.True(v[0].Y < v[1].Y && v[0].Y < v[2].Y && v[0].Y < v[3].Y,
                        "顶点 0 必须是最高点（顶角朝上），实测 y=" + v[0].Y.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，其余 " + v[1].Y.ToString("0.##", CultureInfo.InvariantCulture) + "/"
                        + v[2].Y.ToString("0.##", CultureInfo.InvariantCulture) + "/"
                        + v[3].Y.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(Math.Abs(v[0].X - layout.PlacardAnchor.X) <= 0.01,
                        "顶角 x 应与锚点同列：" + v[0].X.ToString("0.###", CultureInfo.InvariantCulture)
                        + " vs " + layout.PlacardAnchor.X.ToString("0.###", CultureInfo.InvariantCulture));

                    float top = float.MaxValue;
                    float bottom = float.MinValue;
                    float half = layout.PlacardSize * 0.5f + 5f;
                    for (int y = (int)(layout.PlacardAnchor.Y - half); y <= (int)(layout.PlacardAnchor.Y + half); y++)
                    {
                        if (probe.HasRedInBox((int)(layout.PlacardAnchor.X - half), y,
                                (int)(layout.PlacardAnchor.X + half), y + 1))
                        {
                            if (y < top) top = y;
                            if (y > bottom) bottom = y;
                        }
                    }
                    double measuredHeight = bottom - top + 1d;
                    Assert.True(Math.Abs(measuredHeight - layout.PlacardSize) <= 5d,
                        "红色菱形的像素高应约为对角线 " + layout.PlacardSize + "px，实测 "
                        + measuredHeight.ToString("0.##", CultureInfo.InvariantCulture) + "px");
                }
            }
        }

        [Fact]
        public void T253_头部字段区同时避开主标志与副标志()
        {
            DangerousGoodsDocument doc = DangerousGoodsSample();
            DangerousGoodsTemplate template = new DangerousGoodsTemplate();
            using (SKPath symbol = SymbolTriangle())
            {
                template.SymbolPath = symbol;
                ChartGeometry geometry;
                using (ImageProbe probe = Render(doc, template, out geometry))
                {
                    DangerousGoodsLayout layout = geometry.LayoutOf<DangerousGoodsLayout>()!;
                    SKRect fields = layout.HeaderFieldsRect;
                    SKRect main = layout.MainBounds();
                    SKRect sub = layout.SubBounds();

                    Assert.True(fields.Left > main.Right,
                        "字段区左缘应让开主牌右缘：字段 " + fields.Left.ToString("0.##", CultureInfo.InvariantCulture)
                        + " vs 主牌 " + main.Right.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(fields.Left > sub.Right,
                        "字段区左缘应让开副牌右缘：字段 " + fields.Left.ToString("0.##", CultureInfo.InvariantCulture)
                        + " vs 副牌 " + sub.Right.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(!fields.IntersectsWith(sub) && !fields.IntersectsWith(main),
                        "字段区不得与任一菱形牌相交：字段 "
                        + fields.Left.ToString("0.##", CultureInfo.InvariantCulture) + "–"
                        + fields.Right.ToString("0.##", CultureInfo.InvariantCulture));

                    SKRect firstCell = layout.HeaderFieldRect(0);
                    Assert.True(probe.HasInkInBox((int)firstCell.Left + 2, (int)firstCell.Top + 2,
                            (int)firstCell.Right - 2, (int)firstCell.Bottom - 2),
                        "正对照：首个字段格必须有字，否则上面的避让判据只是在量空白");
                }
            }
        }

        [Fact]
        public void T253_类项号在下半槽图案在上半槽()
        {
            DangerousGoodsDocument doc = DangerousGoodsSample();
            DangerousGoodsTemplate template = new DangerousGoodsTemplate();
            using (SKPath symbol = SymbolTriangle())
            {
                template.SymbolPath = symbol;
                ChartGeometry geometry;
                using (ImageProbe probe = Render(doc, template, out geometry))
                {
                    DangerousGoodsLayout layout = geometry.LayoutOf<DangerousGoodsLayout>()!;
                    SKRect numberSlot = layout.NumberSlot();
                    SKRect symbolSlot = layout.SymbolSlot();

                    Assert.True(numberSlot.Top > layout.PlacardAnchor.Y,
                        "类项号槽应整体在下半角：槽顶 " + numberSlot.Top.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，中心线 y=" + layout.PlacardAnchor.Y.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(symbolSlot.Bottom < layout.PlacardAnchor.Y,
                        "图案槽应整体在上半角：槽底 " + symbolSlot.Bottom.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，中心线 y=" + layout.PlacardAnchor.Y.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(symbolSlot.Bottom <= numberSlot.Top,
                        "上下两槽不得重叠：图案槽底 " + symbolSlot.Bottom.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，类项号槽顶 " + numberSlot.Top.ToString("0.##", CultureInfo.InvariantCulture));

                    double symbolRatio = probe.NonWhiteRatio(Shrink(symbolSlot, 2f));
                    Assert.True(symbolRatio > 0.15,
                        "注入的象形图应在上半槽留下实心墨（正控），非白比 "
                        + symbolRatio.ToString("0.####", CultureInfo.InvariantCulture));
                    SKColor symbolColor = probe.AverageInkColor(Shrink(symbolSlot, 2f));
                    Assert.True(symbolColor.Red > symbolColor.Green + 50,
                        "象形图应随牌面主色（红）绘制，实测 R=" + symbolColor.Red + " G=" + symbolColor.Green
                        + " B=" + symbolColor.Blue);

                    double numberInk = probe.NonWhiteRatio(Shrink(numberSlot, 2f));
                    Assert.True(numberInk > 0.01,
                        "类项号文字应落在下半槽（正控），非白比 " + numberInk.ToString("0.####", CultureInfo.InvariantCulture));
                    SKColor numberColor = probe.AverageInkColor(Shrink(numberSlot, 2f));
                    Assert.True(numberColor.Red - numberColor.Green < 40,
                        "类项号为黑字，不应是红墨：实测 R=" + numberColor.Red + " G=" + numberColor.Green
                        + " B=" + numberColor.Blue);
                    Assert.True(numberInk < symbolRatio,
                        "类项号文字墨量应明显小于实心象形图：文字 " + numberInk.ToString("0.####", CultureInfo.InvariantCulture)
                        + " vs 图案 " + symbolRatio.ToString("0.####", CultureInfo.InvariantCulture));
                }
            }
        }

        [Fact]
        public void T253_副标志与主标志并排不重叠()
        {
            DangerousGoodsDocument doc = DangerousGoodsSample();
            DangerousGoodsTemplate template = new DangerousGoodsTemplate();
            using (SKPath symbol = SymbolTriangle())
            {
                template.SymbolPath = symbol;
                ChartGeometry geometry;
                using (ImageProbe probe = Render(doc, template, out geometry))
                {
                    DangerousGoodsLayout layout = geometry.LayoutOf<DangerousGoodsLayout>()!;
                    SKRect main = layout.MainBounds();
                    SKRect sub = layout.SubBounds();
                    Assert.True(main.Right < sub.Left,
                        "主标志右缘 " + main.Right.ToString("0.##", CultureInfo.InvariantCulture)
                        + " 应小于副标志左缘 " + sub.Left.ToString("0.##", CultureInfo.InvariantCulture));
                    double gap = sub.Left - main.Right;
                    double expectedGap = layout.PlacardSize * layout.Tokens.SubPlacardGapFactor;
                    Assert.True(Math.Abs(gap - expectedGap) <= 1d,
                        "两牌间隙应为 " + expectedGap.ToString("0.##", CultureInfo.InvariantCulture)
                        + "px，实测 " + gap.ToString("0.##", CultureInfo.InvariantCulture) + "px");

                    float midY = layout.PlacardAnchor.Y;
                    Assert.True(probe.HasRedInBox((int)(main.Right - 4f), (int)midY - 5,
                            (int)(main.Right + 1f), (int)midY + 5),
                        "主标志右角应有红墨（正控），x≈" + main.Right.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(probe.HasRedInBox((int)(sub.Left - 1f), (int)midY - 5,
                            (int)(sub.Left + 4f), (int)midY + 5),
                        "副标志左角应有红墨（正控），x≈" + sub.Left.ToString("0.##", CultureInfo.InvariantCulture));
                    bool gapInk = probe.HasRedInBox((int)(main.Right + 6f), (int)midY - 8,
                        (int)(sub.Left - 6f), (int)midY + 8);
                    Assert.True(!gapInk,
                        "两牌之间不应有红墨：检验带 x=" + (main.Right + 6f).ToString("0.##", CultureInfo.InvariantCulture)
                        + "–" + (sub.Left - 6f).ToString("0.##", CultureInfo.InvariantCulture));
                }
            }
        }

        // ================================================================== T254 超限附图

        [Fact]
        public void T254_尺寸数字由坐标回写而非手填()
        {
            OversizeLoadDocument doc = OversizeSample();
            OversizeLoadLayout layout = OversizeLoadLayout.Compute(doc, new OversizeLoadStyle());

            Assert.True(layout.HeightRun.Text == null,
                "总高尺寸的文字必须为空（由 Measured 生成），实测 \"" + (layout.HeightRun.Text ?? "null") + "\"");
            Assert.True(layout.HeightRun.Measured.HasValue, "总高尺寸回写值为空");
            double measured = layout.HeightRun.Measured!.Value;
            Assert.True(Math.Abs(measured - doc.HeightMeters) <= 0.01,
                "总高回写值应等于文档高 " + doc.HeightMeters + "m，实测 "
                + measured.ToString("0.####", CultureInfo.InvariantCulture) + "m");

            string expectedText = measured.ToString("0.00", CultureInfo.InvariantCulture) + "m";
            Assert.True(layout.HeightRun.DisplayText == expectedText,
                "尺寸数字应由 Measured 按格式生成：应 \"" + expectedText + "\"，实测 \""
                + (layout.HeightRun.DisplayText ?? "null") + "\"");

            double previous = 0d;
            double sum = 0d;
            for (int i = 0; i < layout.Chain.Runs.Count; i++)
            {
                DimensionRun run = layout.Chain.Runs[i];
                Assert.True(run.Measured.HasValue, "第 " + (i + 1) + " 段链式尺寸没有回写值");
                // 段长 = 本轴位（末段取总长）− 上一轴位；首段从 0 起算。
                double position = i < doc.Axles.Count ? doc.Axles[i].PositionMeters : doc.LengthMeters;
                double expected = position - previous;
                double actual = run.Measured!.Value;
                Assert.True(Math.Abs(actual - expected) <= 0.01,
                    "第 " + (i + 1) + " 段回写值应为 " + expected.ToString("0.###", CultureInfo.InvariantCulture)
                    + "m，实测 " + actual.ToString("0.###", CultureInfo.InvariantCulture) + "m");
                Assert.True(run.DisplayText == actual.ToString("0.00", CultureInfo.InvariantCulture) + "m",
                    "第 " + (i + 1) + " 段数字应为回写值，实测 \"" + (run.DisplayText ?? "null") + "\"");
                sum += actual;
                previous = position;
            }
            Assert.True(Math.Abs(sum - doc.LengthMeters) <= 0.01,
                "各段回写值之和应等于总长 " + doc.LengthMeters + "m，实测 "
                + sum.ToString("0.####", CultureInfo.InvariantCulture) + "m");
        }

        [Fact]
        public void T254_链式各段首尾相接且总长等于端到端()
        {
            OversizeLoadDocument doc = OversizeSample();
            OversizeLoadLayout layout = OversizeLoadLayout.Compute(doc, new OversizeLoadStyle());
            IReadOnlyList<DimensionRun> runs = layout.Chain.Runs;

            Assert.True(runs.Count == doc.Axles.Count + 1,
                "链段数应为轴数+1=" + (doc.Axles.Count + 1) + "，实测 " + runs.Count);
            for (int i = 0; i + 1 < runs.Count; i++)
            {
                Assert.True(runs[i].To.X == runs[i + 1].From.X && runs[i].To.Y == runs[i + 1].From.Y,
                    "第 " + (i + 1) + " 段终点 (" + runs[i].To.X + "," + runs[i].To.Y
                    + ") 与第 " + (i + 2) + " 段起点 (" + runs[i + 1].From.X + "," + runs[i + 1].From.Y + ") 不重合");
            }
            Assert.True(runs[0].From.X == layout.Chain.Origin.X && runs[0].From.Y == layout.Chain.Origin.Y,
                "首段起点必须等于链起点");
            Assert.True(runs[runs.Count - 1].To.X == layout.Chain.Terminator.X
                        && runs[runs.Count - 1].To.Y == layout.Chain.Terminator.Y,
                "末段终点必须等于链终点");

            double sum = layout.Chain.SumOfRunLengths;
            double span = DimensionRun.Distance(layout.Chain.Origin, layout.Chain.Terminator);
            Assert.True(Math.Abs(sum - span) <= 1e-3,
                "各段像素长之和应等于端到端 " + span.ToString("0.####", CultureInfo.InvariantCulture)
                + "px，实测 " + sum.ToString("0.####", CultureInfo.InvariantCulture) + "px");
            Assert.True(Math.Abs(span - layout.Chain.TotalLength) <= 1e-3,
                "端到端像素长应与 Chain.TotalLength 一致："
                + span.ToString("0.####", CultureInfo.InvariantCulture) + " vs "
                + layout.Chain.TotalLength.ToString("0.####", CultureInfo.InvariantCulture));
        }

        [Fact]
        public void T254_延伸线留gap且不穿轮廓()
        {
            OversizeLoadDocument doc = OversizeSample();
            OversizeLoadTemplate template = new OversizeLoadTemplate();
            ChartGeometry geometry;
            using (ImageProbe probe = Render(doc, template, out geometry))
            {
                OversizeLoadLayout layout = geometry.LayoutOf<OversizeLoadLayout>()!;
                DimensionRun height = layout.HeightRun;
                float gap = height.ExtensionGapOr(2f);
                Assert.True(gap > 0f, "延伸线 gap 必须为正，实测 " + gap);

                height.ExtensionLines(height.ProfileFrom, height.ProfileTo, gap, 3f,
                    out SKPoint fromA, out SKPoint fromB, out SKPoint toA, out SKPoint toB);
                Assert.True(fromA.X > height.ProfileFrom.X + gap - 0.01f,
                    "总高延伸线起点应离轮廓 " + gap + "px：轮廓 x="
                    + height.ProfileFrom.X.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，延伸线起点 x=" + fromA.X.ToString("0.##", CultureInfo.InvariantCulture));

                // 取 1/4 高处扫描：避开总高尺寸数字所在的中段，只留轮廓边与尺寸线。
                int y = (int)(layout.GroundY + (layout.TopY - layout.GroundY) * 0.25f);
                float profileRight = layout.PointOf(doc.LengthMeters, 0d).X;
                IReadOnlyList<double> rules = probe.VerticalRulesAt(y, profileRight - 2f, layout.DimensionX + 8f);
                Assert.True(rules.Count == 2,
                    "轮廓右缘与高度尺寸线之间只应有 2 条竖墨（轮廓边 + 延伸线），实测 " + rules.Count + " 条");
                Assert.True(rules[0] - profileRight <= 2f,
                    "第一条竖墨应是轮廓右缘 x≈" + profileRight.ToString("0.##", CultureInfo.InvariantCulture)
                    + "，实测 " + rules[0].ToString("0.##", CultureInfo.InvariantCulture));
                Assert.True(rules[1] - profileRight >= gap,
                    "延伸线应比轮廓右缘再远至少 " + gap + "px，实测 "
                    + (rules[1] - profileRight).ToString("0.##", CultureInfo.InvariantCulture) + "px");

                Assert.True(!probe.HasInkInBox((int)profileRight - 9, y - 1, (int)profileRight - 3, y + 2),
                    "轮廓内部应留白（延伸线没有穿进轮廓）");
                Assert.True(probe.HasInkInBox((int)profileRight - 1, y - 1, (int)profileRight + 2, y + 2),
                    "轮廓右缘应有墨（正控）");

                for (int i = 0; i < layout.Chain.Runs.Count; i++)
                {
                    DimensionRun run = layout.Chain.Runs[i];
                    run.ExtensionLines(run.ProfileFrom, run.ProfileTo, gap, 3f,
                        out SKPoint ca, out SKPoint cb, out SKPoint cc, out SKPoint cd);
                    Assert.True(ca.Y >= layout.GroundY + gap - 0.01f,
                        "第 " + (i + 1) + " 段链式延伸线应从地面下 " + gap + "px 处起笔，实测 y="
                        + ca.Y.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，地面 y=" + layout.GroundY.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }
        }

        // ================================================================== A18 令牌可覆盖

        [Fact]
        public void A18_列车运行图站名列宽改动即改变渲染()
        {
            TrainGraphDocument doc = TrainSample();
            TrainGraphTemplate first = new TrainGraphTemplate();
            ChartGeometry geometry1;
            using (ImageProbe probe1 = Render(doc, first, out geometry1))
            {
                TrainGraphLayout layout1 = geometry1.LayoutOf<TrainGraphLayout>()!;
                double oldX = layout1.TimeAxis.PositionOf(60d);
                int y0 = (int)layout1.PlotRect.Bottom + 3;
                int y1 = (int)layout1.PlotRect.Bottom + 8;
                double oldMass = InkMass(probe1, oldX, y0, y1, 2);

                TrainGraphTemplate second = new TrainGraphTemplate();
                second.Tokens.StationLabelWidth = first.Tokens.StationLabelWidth + 32f;
                ChartGeometry geometry2;
                using (ImageProbe probe2 = Render(doc, second, out geometry2))
                {
                    TrainGraphLayout layout2 = geometry2.LayoutOf<TrainGraphLayout>()!;
                    double newX = layout2.TimeAxis.PositionOf(60d);
                    Assert.True(Math.Abs(newX - oldX) > 20d,
                        "站名列宽 +32px 后 60 分刻度应明显横移：旧 x=" + oldX.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，新 x=" + newX.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(Math.Abs(layout2.PlotRect.Left - (layout1.PlotRect.Left + 32f)) <= 0.51,
                        "绘图区左边应按列宽增量右移 32px：旧 " + layout1.PlotRect.Left.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，新 " + layout2.PlotRect.Left.ToString("0.##", CultureInfo.InvariantCulture));
                    int y2 = (int)layout2.PlotRect.Bottom + 3;
                    int y3 = (int)layout2.PlotRect.Bottom + 8;
                    double newMass = InkMass(probe2, newX, y2, y3, 2);
                    double shiftedMass = InkMass(probe2, oldX, y2, y3, 2);
                    Assert.True(Math.Abs(newMass - oldMass) <= oldMass * 0.1,
                        "粗刻度应原样出现在新位置：旧图墨量 " + oldMass.ToString("0.#")
                        + "，新图新位置 " + newMass.ToString("0.#"));
                    // 刻度间距只有约 4.5px，横移 32px 后旧位置必然压到别的细刻度上，不可能回到纯空白；
                    // 能证明"粗刻度挪走了"的是旧位置只剩细刻度级墨量（约粗刻度的三分之一）。
                    Assert.True(shiftedMass < newMass * 0.5,
                        "旧位置不应再是粗刻度：新图旧位置墨量 " + shiftedMass.ToString("0.#")
                        + " vs 新位置 " + newMass.ToString("0.#"));
                }
            }
        }

        [Fact]
        public void A18_载重平衡图高度令牌改动即改变落点()
        {
            LoadBalanceDocument doc = LoadSample();
            LoadBalanceTemplate first = new LoadBalanceTemplate();
            ChartGeometry geometry1;
            using (ImageProbe probe1 = Render(doc, first, out geometry1))
            {
                LoadBalanceLayout layout1 = geometry1.LayoutOf<LoadBalanceLayout>()!;
                CgCheckPoint inside = doc.CheckPoints[0];
                SKPoint oldPoint = layout1.PixelOf(inside.CgPercentMac, inside.WeightKg);

                LoadBalanceTemplate second = new LoadBalanceTemplate();
                second.Tokens.PlotHeight = first.Tokens.PlotHeight - 80f;
                ChartGeometry geometry2;
                using (ImageProbe probe2 = Render(doc, second, out geometry2))
                {
                    LoadBalanceLayout layout2 = geometry2.LayoutOf<LoadBalanceLayout>()!;
                    SKPoint newPoint = layout2.PixelOf(inside.CgPercentMac, inside.WeightKg);
                    Assert.True(Math.Abs(newPoint.Y - oldPoint.Y) > 10d,
                        "绘图区高减少 80px 后检查点应纵移：旧 y=" + oldPoint.Y.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，新 y=" + newPoint.Y.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(Math.Abs(newPoint.X - oldPoint.X) <= 0.51,
                        "重心轴未变，横向不应移动：旧 x=" + oldPoint.X.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，新 x=" + newPoint.X.ToString("0.##", CultureInfo.InvariantCulture));

                    double newRatio = probe2.NonWhiteRatio(new SKRect(newPoint.X - 4f, newPoint.Y - 4f,
                        newPoint.X + 4f, newPoint.Y + 4f));
                    double oldRatio = probe2.NonWhiteRatio(new SKRect(oldPoint.X - 4f, oldPoint.Y - 4f,
                        oldPoint.X + 4f, oldPoint.Y + 4f));
                    Assert.True(newRatio > oldRatio * 4d,
                        "新落点应是装载点（实心墨），旧落点应回到白底：新 "
                        + newRatio.ToString("0.####", CultureInfo.InvariantCulture)
                        + " vs 旧 " + oldRatio.ToString("0.####", CultureInfo.InvariantCulture));
                }
            }
        }

        [Fact]
        public void A18_积载图层高令牌改动即改变箱块位置()
        {
            StowageDocument doc = StowageSample();
            StowageTemplate first = new StowageTemplate();
            ChartGeometry geometry1;
            using (ImageProbe probe1 = Render(doc, first, out geometry1))
            {
                StowageLayout layout1 = geometry1.LayoutOf<StowageLayout>()!;
                StowageContainer target = doc.Containers[1];
                SKRect oldRect = layout1.ProfileRectOf(target);

                StowageTemplate second = new StowageTemplate();
                second.Tokens.TierHeight = first.Tokens.TierHeight + 14f;
                ChartGeometry geometry2;
                using (ImageProbe probe2 = Render(doc, second, out geometry2))
                {
                    StowageLayout layout2 = geometry2.LayoutOf<StowageLayout>()!;
                    SKRect newRect = layout2.ProfileRectOf(target);
                    // tier 1 贴绘图区底，tier n 的上边距底 n 个层高；层高 +14px 时上边应累计上移 n×14。
                    double expected = target.Tier * 14d;
                    Assert.True(Math.Abs((oldRect.Top - newRect.Top) - expected) <= 0.51,
                        "层高 +14px 后 tier " + target.Tier + " 应上移 " + expected
                        + "px，实测 " + (oldRect.Top - newRect.Top).ToString("0.##", CultureInfo.InvariantCulture));

                    double newRatio = probe2.NonWhiteRatio(Shrink(newRect, 6f));
                    double oldRatio = probe2.NonWhiteRatio(Shrink(oldRect, 6f));
                    Assert.True(newRatio > 0.5 && oldRatio < 0.05,
                        "箱块应出现在新位置、旧位置回到空白：新非白比 "
                        + newRatio.ToString("0.####", CultureInfo.InvariantCulture)
                        + "，旧非白比 " + oldRatio.ToString("0.####", CultureInfo.InvariantCulture));
                }
            }
        }

        [Fact]
        public void A18_危货联单菱形尺寸令牌改动即改变槽位()
        {
            DangerousGoodsDocument doc = DangerousGoodsSample();
            DangerousGoodsTemplate first = new DangerousGoodsTemplate();
            using (SKPath symbol = SymbolTriangle())
            {
                first.SymbolPath = symbol;
                ChartGeometry geometry1;
                using (ImageProbe probe1 = Render(doc, first, out geometry1))
                {
                    DangerousGoodsLayout layout1 = geometry1.LayoutOf<DangerousGoodsLayout>()!;
                    SKRect oldNumber = layout1.NumberSlot();
                    double oldHeight = RedSpan(probe1, layout1.PlacardAnchor.X,
                        layout1.PlacardAnchor.Y, layout1.PlacardSize);

                    DangerousGoodsTemplate second = new DangerousGoodsTemplate();
                    second.SymbolPath = symbol;
                    second.Tokens.PlacardSize = first.Tokens.PlacardSize + 30f;
                    ChartGeometry geometry2;
                    using (ImageProbe probe2 = Render(doc, second, out geometry2))
                    {
                        DangerousGoodsLayout layout2 = geometry2.LayoutOf<DangerousGoodsLayout>()!;
                        SKRect newNumber = layout2.NumberSlot();
                        Assert.True(Math.Abs(newNumber.Top - oldNumber.Top) > 10d,
                            "菱形对角线 +30px 后类项号槽应下移：旧槽顶 "
                            + oldNumber.Top.ToString("0.##", CultureInfo.InvariantCulture)
                            + "，新槽顶 " + newNumber.Top.ToString("0.##", CultureInfo.InvariantCulture));

                        double newHeight = RedSpan(probe2, layout2.PlacardAnchor.X,
                            layout2.PlacardAnchor.Y, layout2.PlacardSize);
                        Assert.True(newHeight > oldHeight + 20d,
                            "红色菱形的像素高应随令牌增大：旧 " + oldHeight.ToString("0.##", CultureInfo.InvariantCulture)
                            + "px，新 " + newHeight.ToString("0.##", CultureInfo.InvariantCulture) + "px");
                    }
                }
            }
        }

        [Fact]
        public void A18_超限附图比例令牌改动即改变落点且量值不变()
        {
            OversizeLoadDocument doc = OversizeSample();
            OversizeLoadLayout layout1 = OversizeLoadLayout.Compute(doc, new OversizeLoadStyle());
            OversizeLoadTemplate first = new OversizeLoadTemplate();
            ChartGeometry geometry1;
            using (ImageProbe probe1 = Render(doc, first, out geometry1))
            {
                OversizeLoadTemplate second = new OversizeLoadTemplate();
                second.Tokens.ScalePixelsPerMeter = 40f;
                ChartGeometry geometry2;
                using (ImageProbe probe2 = Render(doc, second, out geometry2))
                {
                    OversizeLoadLayout layout2 = geometry2.LayoutOf<OversizeLoadLayout>()!;
                    Assert.True(layout2.Chain.TotalLength < layout1.Chain.TotalLength * 0.85d,
                        "比例 52→40 后链长应缩短：旧 " + layout1.Chain.TotalLength.ToString("0.#", CultureInfo.InvariantCulture)
                        + "px，新 " + layout2.Chain.TotalLength.ToString("0.#", CultureInfo.InvariantCulture) + "px");
                    Assert.True(Math.Abs(layout2.HeightRun.Measured!.Value - layout1.HeightRun.Measured!.Value) <= 1e-3,
                        "比例变化不得改变回写量值：旧 " + layout1.HeightRun.Measured!.Value.ToString("0.####", CultureInfo.InvariantCulture)
                        + "m，新 " + layout2.HeightRun.Measured!.Value.ToString("0.####", CultureInfo.InvariantCulture) + "m");

                    float oldRight = layout1.PointOf(doc.LengthMeters, 0d).X;
                    float newRight = layout2.PointOf(doc.LengthMeters, 0d).X;
                    Assert.True(oldRight - newRight > 100d,
                        "轮廓右端应明显左移：旧 x=" + oldRight.ToString("0.##", CultureInfo.InvariantCulture)
                        + "，新 x=" + newRight.ToString("0.##", CultureInfo.InvariantCulture));
                    int y = (int)(layout2.GroundY - 60f);
                    Assert.True(probe2.HasInkInBox((int)newRight - 2, y - 2, (int)newRight + 3, y + 3),
                        "新轮廓右缘应有墨（正控），x≈" + newRight.ToString("0.##", CultureInfo.InvariantCulture));
                    Assert.True(!probe2.HasInkInBox((int)oldRight - 2, y - 2, (int)oldRight + 3, y + 3),
                        "旧轮廓位置应回到白纸，x≈" + oldRight.ToString("0.##", CultureInfo.InvariantCulture));
                }
            }
        }

        // ================================================================== 样例文档

        private static TrainGraphDocument TrainSample()
        {
            TrainGraphDocument doc = new TrainGraphDocument
            {
                LineName = "示例线（未校准）",
                StartClockMinutes = 360d,
                Minutes = 240d,
            };
            doc.Stations.Add(new TrainStation("A", "甲站", 50d));
            doc.Stations.Add(new TrainStation("B", "乙站", 50d));
            doc.Stations.Add(new TrainStation("C", "丙站", 50d));
            doc.Stations.Add(new TrainStation("D", "丁站", 50d));
            doc.Stations.Add(new TrainStation("E", "戊站", 40d));
            doc.Stations.Add(new TrainStation("F", "己站", 0d));

            doc.Trains.Add(new TrainRun("K101", true)
                .Stop(0, 0d, 0d).Stop(1, 50d, 55d).Stop(2, 105d, 105d)
                .Stop(3, 150d, 155d).Stop(4, 195d, 195d).Stop(5, 235d, 235d));
            doc.Trains.Add(new TrainRun("K102", false)
                .Stop(0, 10d, 10d).Stop(2, 120d, 120d).Stop(5, 240d, 240d));

            doc.Windows.Add(new TrainWindow("W1", "天窗 100–140", 100d, 140d, 2, 4));
            return doc;
        }

        private static LoadBalanceDocument LoadSample()
        {
            LoadBalanceDocument doc = new LoadBalanceDocument
            {
                AircraftId = "示例机型（非真实限制）",
                EmptyWeight = 42000d,
                EmptyCgPercentMac = 24d,
                FuelKg = 8000d,
                FuelArm = 26d,
                CgMin = 15d,
                CgMax = 40d,
                WeightMin = 40000d,
                WeightMax = 85000d,
            };
            doc.Stations.Add(new LoadStation("fwd", "前舱", 1d, 18d, 6000d));
            doc.Stations.Add(new LoadStation("mid", "中舱", 2d, 25d, 10000d));
            doc.Stations.Add(new LoadStation("aft", "后舱", 3d, 32d, 10000d));
            doc.Envelopes.Add(new CgEnvelope("env", "示例包线", new[]
            {
                new SKPoint(17f, 40000f),
                new SKPoint(17f, 62000f),
                new SKPoint(20f, 76000f),
                new SKPoint(27f, 80000f),
                new SKPoint(33f, 76000f),
                new SKPoint(36f, 60000f),
                new SKPoint(33f, 40000f),
            }));
            doc.CheckPoints.Add(new CgCheckPoint("to", "起飞重心", doc.CgPercentMacOf(), doc.TotalWeightKg));
            doc.CheckPoints.Add(new CgCheckPoint("over", "超限点", 35.5d, 76000d));
            doc.CheckPoints.Add(new CgCheckPoint("vertex", "包线顶点", 27d, 80000d));
            doc.TableCgValues.Add(20d);
            doc.TableCgValues.Add(25d);
            doc.TableCgValues.Add(30d);
            doc.TableCgValues.Add(35d);
            doc.TableRows.Add(new LoadBalanceTableRow("重量 kg", new[] { 69000d, 72000d, 75000d, 68000d }));
            doc.TableRows.Add(new LoadBalanceTableRow("重心 %MAC", new[] { 20d, 25d, 30d, 35d }));
            return doc;
        }

        private static CgEnvelope ReversedEnvelope(CgEnvelope source)
        {
            List<SKPoint> reversed = new List<SKPoint>(source.Vertices.Count);
            for (int i = source.Vertices.Count - 1; i >= 0; i--)
            {
                reversed.Add(source.Vertices[i]);
            }
            return new CgEnvelope(source.Key + "-rev", source.Label + "（逆序）", reversed);
        }

        private static StowageDocument StowageSample()
        {
            StowageDocument doc = new StowageDocument
            {
                VesselId = "示例船（未校准）",
                VoyageId = "001E",
                CrossSectionBay = 3,
            };
            doc.Bays.Add(new StowageBay(1, 2, 2, 6));
            doc.Bays.Add(new StowageBay(3, 2, 2, 6));
            doc.Bays.Add(new StowageBay(5, 2, 2, 6));
            doc.Bays.Add(new StowageBay(7, 2, 2, 6));

            doc.Containers.Add(new StowageContainer(1, 1, 1, ContainerSize.Twenty, 12000d, "SHA"));
            doc.Containers.Add(new StowageContainer(1, 1, 4, ContainerSize.Twenty, 8000d, "SHA"));
            doc.Containers.Add(new StowageContainer(3, 1, 2, ContainerSize.Forty, 24000d, "LAX"));
            doc.Containers.Add(new StowageContainer(7, 3, 5, ContainerSize.Twenty, 9000d, "XMN"));
            doc.Containers.Add(new StowageContainer(5, 3, 1, ContainerSize.Twenty, 15000d, "NGB"));
            doc.Containers.Add(new StowageContainer(1, 2, 1, ContainerSize.Twenty, 0d, "NGB",
                null, false, true));
            doc.Containers.Add(new StowageContainer(3, 2, 3, ContainerSize.Twenty, 26000d, "HKG",
                "3", true, false));
            return doc;
        }

        private static DangerousGoodsDocument DangerousGoodsSample()
        {
            DangerousGoodsDocument doc = new DangerousGoodsDocument
            {
                UnNumber = "1203",
                ProperShippingName = "汽油",
                Class = "3",
                PackingGroup = "II",
                TunnelRestrictionCode = "D/E",
                SubClass = "6.1",
                Consignor = "示例托运人",
                Carrier = "示例承运人",
                Consignee = "示例收货人",
                EmergencyPhone = "400-000-0000",
                TankPlacardText = "罐体标志牌（类项号 3）",
            };
            doc.AddStandardCopies();
            return doc;
        }

        private static SKPath SymbolTriangle()
        {
            SKPath path = new SKPath();
            path.MoveTo(0f, 0f);
            path.LineTo(10f, 0f);
            path.LineTo(5f, 9f);
            path.Close();
            return path;
        }

        private static OversizeLoadDocument OversizeSample()
        {
            OversizeLoadDocument doc = new OversizeLoadDocument
            {
                PermitNumber = "示例-2026-001",
                VehiclePlate = "沪A·00000",
                CargoName = "示例超限货",
                LengthMeters = 18d,
                HeightMeters = 3.4d,
                WidthMeters = 3d,
                ToleranceMillimeters = 50d,
            };
            doc.Profile.Add(new OversizeProfilePoint(0d, 0d));
            doc.Profile.Add(new OversizeProfilePoint(0d, 1.1d));
            doc.Profile.Add(new OversizeProfilePoint(0.6d, 1.55d));
            doc.Profile.Add(new OversizeProfilePoint(2.0d, 1.8d));
            doc.Profile.Add(new OversizeProfilePoint(2.4d, 2.9d));
            doc.Profile.Add(new OversizeProfilePoint(3.0d, 3.05d));
            doc.Profile.Add(new OversizeProfilePoint(3.0d, 3.4d));
            doc.Profile.Add(new OversizeProfilePoint(15.2d, 3.4d));
            doc.Profile.Add(new OversizeProfilePoint(15.8d, 2.6d));
            doc.Profile.Add(new OversizeProfilePoint(18d, 2.4d));
            doc.Profile.Add(new OversizeProfilePoint(18d, 0d));
            doc.Profile.Add(new OversizeProfilePoint(0d, 0d));

            doc.KeyHeightsMeters.Add(1.8d);
            doc.KeyHeightsMeters.Add(3.4d);

            doc.Axles.Add(new OversizeAxle(2.6d, 6.5d));
            doc.Axles.Add(new OversizeAxle(11.6d, 10.0d));
            doc.Axles.Add(new OversizeAxle(13.0d, 9.5d));
            doc.Axles.Add(new OversizeAxle(15.0d, 8.0d));
            return doc;
        }

        // ================================================================== 工具

        private static ImageProbe Render(ChartDocument document, IChartTemplate template,
                                         out ChartGeometry geometry)
        {
            using (Chart chart = new Chart(document, template, new ChartStyle()))
            {
                geometry = chart.Geometry;
                byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
                Assert.True(png.Length > 1024, "PNG 字节数过小：实测 " + png.Length + " 字节");
                return ImageProbe.FromPng(png);
            }
        }

        /// <summary>指定列附近、指定行带内的"墨量"（255−最暗通道之和），用来比较线宽。</summary>
        private static double InkMass(ImageProbe probe, double centerX, int y0, int y1, int halfWidth)
        {
            int cx = (int)Math.Round(centerX);
            double mass = 0d;
            for (int x = cx - halfWidth; x <= cx + halfWidth; x++)
            {
                if (x < 0 || x >= probe.Width)
                {
                    continue;
                }
                for (int y = Math.Max(0, y0); y <= Math.Min(probe.Height - 1, y1); y++)
                {
                    SKColor c = probe.Pixel(x, y);
                    int darkest = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                    mass += 255 - darkest;
                }
            }
            return mass;
        }

        private static double Nearest(IReadOnlyList<double> positions, double target)
        {
            double best = double.MaxValue;
            for (int i = 0; i < positions.Count; i++)
            {
                double d = Math.Abs(positions[i] - target);
                if (d < best)
                {
                    best = d;
                }
            }
            return positions.Count == 0 ? double.MaxValue : best;
        }

        private static double Distance(SKPoint a, SKPoint b)
        {
            double dx = (double)b.X - a.X;
            double dy = (double)b.Y - a.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private static SKRect Shrink(SKRect rect, float inset)
        {
            return new SKRect(rect.Left + inset, rect.Top + inset, rect.Right - inset, rect.Bottom - inset);
        }

        /// <summary>红色墨在菱形纵向扫描范围内的像素高（顶–底）。</summary>
        private static double RedSpan(ImageProbe probe, float centerX, float centerY, float size)
        {
            float half = size * 0.5f + 8f;
            float halfWidth = half + 2f;
            int top = int.MaxValue;
            int bottom = int.MinValue;
            for (int y = Math.Max(0, (int)(centerY - half)); y <= Math.Min(probe.Height - 1, (int)(centerY + half)); y++)
            {
                if (probe.HasRedInBox((int)(centerX - halfWidth), y, (int)(centerX + halfWidth) + 1, y + 1))
                {
                    if (y < top) top = y;
                    if (y > bottom) bottom = y;
                }
            }
            if (top == int.MaxValue)
            {
                return 0d;
            }
            return bottom - top + 1d;
        }
    }
}
