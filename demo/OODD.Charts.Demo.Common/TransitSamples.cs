using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using OODD.Charts.Style;
using OODD.Charts.Transit.DangerousGoods;
using OODD.Charts.Transit.LoadBalance;
using OODD.Charts.Transit.OversizeLoad;
using OODD.Charts.Transit.Stowage;
using OODD.Charts.Transit.TrainGraph;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>
    /// 交通域五张表单的演示样例与结构断言（docs/07 T290）。
    /// <para>
    /// 示例数据均为自造：列车时刻、重心包线、箱位、UN 编号组合与外廓折点
    /// 只用于演示版面和断言落位，<b>示例数据，非真实机型/标准值</b>。
    /// 库不内置任何机型限制、船舶资料或车型轮廓，样例也不冒充真实运行图/许可附图。
    /// </para>
    /// <para>
    /// 本类只写演示数据与自检，不登记到 <c>DemoCatalog</c>（由主代理统一接入）。
    /// </para>
    /// </summary>
    public static class TransitSamples
    {
        /// <summary>
        /// 五张交通表单样例。create 每次新建文档与模板，避免复用可变状态。
        /// 示例数据，非真实机型/标准值。
        /// </summary>
        public static IReadOnlyList<DemoSample> All { get; } = new List<DemoSample>
        {
            new DemoSample("train-graph", "列车运行图",
                () => new Chart(BuildTrainGraph(), new TrainGraphTemplate(), new ChartStyle())),
            new DemoSample("weight-balance", "飞机载重平衡与重心包线",
                () => new Chart(BuildLoadBalance(), new LoadBalanceTemplate(), new ChartStyle())),
            new DemoSample("bay-plan", "集装箱积载图",
                () => new Chart(BuildStowage(), new StowageTemplate(), new ChartStyle())),
            new DemoSample("dg-consignment", "危险货物道路运输联单",
                () => new Chart(BuildDangerousGoods(), new DangerousGoodsTemplate(), new ChartStyle())),
            new DemoSample("ogami-profile", "超限运输许可附图",
                () => new Chart(BuildOversizeLoad(), new OversizeLoadTemplate(), new ChartStyle())),
        };

        // ================================================================== 断言分发

        /// <summary>
        /// 按样例名做结构断言，返回失败条数；未知 name 返回 0。
        /// 每条断言输出一行：<c>[name] 通过/失败: 说明 实到值</c>。
        /// </summary>
        public static int Verify(string name, Chart chart)
        {
            if (chart == null)
            {
                return 0;
            }

            switch (name)
            {
                case "train-graph":
                    return VerifyTrainGraph(chart);
                case "weight-balance":
                    return VerifyLoadBalance(chart);
                case "bay-plan":
                    return VerifyStowage(chart);
                case "dg-consignment":
                    return VerifyDangerousGoods(chart);
                case "ogami-profile":
                    return VerifyOversizeLoad(chart);
                default:
                    return 0;
            }
        }

        // ================================================================== 列车运行图

        /// <summary>
        /// 列车运行图样例：4 站、5 列车（上下行各若干）、1 段天窗。
        /// 示例数据，非真实线路/时刻/标准值。
        /// </summary>
        private static TrainGraphDocument BuildTrainGraph()
        {
            TrainGraphDocument doc = new TrainGraphDocument
            {
                FormTitle = "列车运行图",
                LineName = "示例线 SB 线（示例数据，非真实线路）",
                StartClockMinutes = 360d,
                Minutes = 240d,
            };

            doc.Stations.Add(new TrainStation("A", "甲站", 52d));
            doc.Stations.Add(new TrainStation("B", "乙站", 46d));
            doc.Stations.Add(new TrainStation("C", "丙站", 58d));
            doc.Stations.Add(new TrainStation("D", "丁站", 0d));

            doc.Trains.Add(new TrainRun("G101", true)
                .Stop(0, 0d, 0d).Stop(1, 52d, 56d).Stop(2, 102d, 102d).Stop(3, 160d, 160d));
            doc.Trains.Add(new TrainRun("G102", false)
                .Stop(3, 12d, 12d).Stop(2, 70d, 74d).Stop(1, 120d, 124d).Stop(0, 176d, 176d));
            doc.Trains.Add(new TrainRun("K205", true)
                .Stop(0, 24d, 24d).Stop(1, 76d, 81d).Stop(2, 127d, 127d).Stop(3, 185d, 185d));
            doc.Trains.Add(new TrainRun("K206", false)
                .Stop(3, 36d, 36d).Stop(2, 94d, 98d).Stop(1, 144d, 148d).Stop(0, 200d, 200d));
            doc.Trains.Add(new TrainRun("D301", true)
                .Stop(0, 48d, 48d).Stop(1, 100d, 104d).Stop(2, 150d, 150d).Stop(3, 208d, 208d));

            doc.Windows.Add(new TrainWindow("W1", "天窗 100–140（乙—丁）", 100d, 140d, 1, 3));
            return doc;
        }

        private static int VerifyTrainGraph(Chart chart)
        {
            const string name = "train-graph";
            int failures = 0;

            TrainGraphDocument? doc = chart.Document as TrainGraphDocument;
            if (doc == null)
            {
                Check(name, ref failures, false, "文档类型应为 TrainGraphDocument",
                    chart.Document.GetType().Name);
                return failures;
            }

            TrainGraphLayout? layout = chart.Geometry.LayoutOf<TrainGraphLayout>();
            if (layout == null)
            {
                Check(name, ref failures, false, "版面应挂 TrainGraphLayout", "null");
                return failures;
            }

            // 1. 站界 y：相邻站名线间距 = 区间运行时分权重 × 绘图区高；总间距 = 绘图区高。
            double total = doc.TotalRunMinutes;
            double height = layout.PlotRect.Height;
            double maxGapDeviation = 0d;
            double gapSum = 0d;
            for (int i = 0; i + 1 < doc.Stations.Count; i++)
            {
                double gap = layout.StationLineY(i + 1) - layout.StationLineY(i);
                double expected = doc.Stations[i].RunMinutesToNext / total * height;
                double deviation = Math.Abs(gap - expected);
                if (deviation > maxGapDeviation)
                {
                    maxGapDeviation = deviation;
                }
                gapSum += gap;
            }
            Check(name, ref failures,
                maxGapDeviation <= 0.51d && Math.Abs(gapSum - height) <= 0.51d,
                "站名线按区间运行时分加权且铺满绘图区高（允许偏差 0.51px）",
                "最大偏差 " + N(maxGapDeviation) + "px，总间距 " + N(gapSum)
                + "/" + N(height) + "px");

            // 2. 首列车首站界点落在第 1 条站名线上，且班次/车站数满足样例特征。
            IReadOnlyList<SKPoint> points = layout.PointsOf(doc.Trains[0]);
            double firstStationY = layout.StationLineY(0);
            double yDeviation = points.Count > 0 ? Math.Abs(points[0].Y - firstStationY) : double.MaxValue;
            Check(name, ref failures,
                doc.Stations.Count >= 3 && doc.Trains.Count >= 4 && points.Count >= 3
                && yDeviation <= 1e-4d,
                "首列车首站界点落在站名线上（车站≥3、列车≥4）",
                "车站 " + doc.Stations.Count + " 座，列车 " + doc.Trains.Count + " 列，"
                + "顶点 " + points.Count + " 个，y 偏差 " + N(yDeviation) + "px");

            // 3. 天窗：上下贴站界 y，左右落在时刻轴上。
            if (doc.Windows.Count == 0)
            {
                Check(name, ref failures, false, "样例应含至少 1 段天窗", "0 段");
            }
            else
            {
                TrainWindow window = doc.Windows[0];
                SKRect rect = layout.WindowRect(window);
                double leftDeviation = Math.Abs(rect.Left - layout.TimeAxis.PositionOf(window.FromMinutes));
                double rightDeviation = Math.Abs(rect.Right - layout.TimeAxis.PositionOf(window.ToMinutes));
                double topDeviation = Math.Abs(rect.Top - layout.StationLineY(window.FromStation));
                double bottomDeviation = Math.Abs(rect.Bottom - layout.StationLineY(window.ToStation));
                double windowDeviation = Math.Max(Math.Max(leftDeviation, rightDeviation),
                    Math.Max(topDeviation, bottomDeviation));
                Check(name, ref failures, windowDeviation <= 0.51d,
                    "天窗带上下贴站界 y、左右落在时刻轴（允许偏差 0.51px）",
                    "最大偏差 " + N(windowDeviation) + "px，矩形 "
                    + N(rect.Left) + "–" + N(rect.Right) + " × " + N(rect.Top) + "–" + N(rect.Bottom));
            }

            // 4. 导出非空：96dpi PNG 字节数 > 5000。
            CheckPng(name, chart, ref failures);
            return failures;
        }

        // ================================================================== 载重平衡

        /// <summary>
        /// 飞机载重平衡样例：3 个装载站位、1 条自造包线、4 个重心检查点（包线内/外/边界）。
        /// 示例数据，非真实机型/标准值。
        /// </summary>
        private static LoadBalanceDocument BuildLoadBalance()
        {
            LoadBalanceDocument doc = new LoadBalanceDocument
            {
                FormTitle = "飞机载重平衡与重心包线",
                AircraftId = "示例机型 SB-01（示例数据，非真实机型/标准值）",
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
            doc.Stations.Add(new LoadStation("mid", "中舱", 2d, 25d, 12000d));
            doc.Stations.Add(new LoadStation("aft", "后舱", 3d, 32d, 9000d));

            // 自造示例包线：低重量段宽、高重量段收窄；不是任何真实机型的限制。
            doc.Envelopes.Add(new CgEnvelope("env-sb", "示例包线（非真实限制）", new[]
            {
                new SKPoint(17f, 40000f),
                new SKPoint(17f, 60000f),
                new SKPoint(20f, 74000f),
                new SKPoint(25f, 80000f),
                new SKPoint(30f, 76000f),
                new SKPoint(35f, 60000f),
                new SKPoint(33f, 40000f),
            }));

            doc.CheckPoints.Add(new CgCheckPoint("takeoff", "起飞重心",
                doc.CgPercentMacOf(), doc.TotalWeightKg));
            doc.CheckPoints.Add(new CgCheckPoint("landing", "着陆重心", 26.5d, 70000d));
            doc.CheckPoints.Add(new CgCheckPoint("over", "超限点", 36.0d, 72000d));
            doc.CheckPoints.Add(new CgCheckPoint("vertex", "包线顶点", 25d, 80000d));

            doc.TableCgValues.Add(20d);
            doc.TableCgValues.Add(25d);
            doc.TableCgValues.Add(30d);
            doc.TableCgValues.Add(35d);
            doc.TableRows.Add(new LoadBalanceTableRow("重量 kg",
                new[] { 64000d, 72000d, 78000d, 66000d }));
            doc.TableRows.Add(new LoadBalanceTableRow("重心 %MAC",
                new[] { 20d, 25d, 30d, 35d }));
            return doc;
        }

        private static int VerifyLoadBalance(Chart chart)
        {
            const string name = "weight-balance";
            int failures = 0;

            LoadBalanceDocument? doc = chart.Document as LoadBalanceDocument;
            if (doc == null)
            {
                Check(name, ref failures, false, "文档类型应为 LoadBalanceDocument",
                    chart.Document.GetType().Name);
                return failures;
            }

            LoadBalanceLayout? layout = chart.Geometry.LayoutOf<LoadBalanceLayout>();
            if (layout == null)
            {
                Check(name, ref failures, false, "版面应挂 LoadBalanceLayout", "null");
                return failures;
            }

            if (doc.CheckPoints.Count < 3)
            {
                Check(name, ref failures, false, "样例应含至少 3 个重心检查点",
                    doc.CheckPoints.Count + " 个");
                return failures;
            }

            // 1. 包线内判定：起飞重心由文档力矩算出，应判在包线内。
            CgCheckPoint inside = doc.CheckPoints[0];
            bool insideOk = layout.IsInside(inside.CgPercentMac, inside.WeightKg);
            Check(name, ref failures, insideOk, "起飞重心判在包线内",
                "(" + N(inside.CgPercentMac, "0.00") + "%MAC, " + N(inside.WeightKg, "0") + " kg) → "
                + (insideOk ? "内" : "外"));

            // 2. 包线外判定：远离包线右缘的高重心点应判在包线外。
            CgCheckPoint outside = doc.CheckPoints[2];
            bool outsideOk = !layout.IsInside(outside.CgPercentMac, outside.WeightKg);
            Check(name, ref failures, outsideOk, "超限点判在包线外",
                "(" + N(outside.CgPercentMac, "0.00") + "%MAC, " + N(outside.WeightKg, "0") + " kg) → "
                + (outsideOk ? "外" : "内"));

            // 3. 关键落位：载重表列中心与图上同一重心值的 x 同源。
            double maxColumnDeviation = 0d;
            for (int i = 0; i < doc.TableCgValues.Count; i++)
            {
                double deviation = Math.Abs(layout.ColumnCenter(i) - layout.CgAxis.PositionOf(doc.TableCgValues[i]));
                if (deviation > maxColumnDeviation)
                {
                    maxColumnDeviation = deviation;
                }
            }
            Check(name, ref failures, maxColumnDeviation <= 1e-3d,
                "载重表列中心与图上重心轴同源（允许偏差 0.001px）",
                "最大偏差 " + N(maxColumnDeviation) + "px，列数 " + doc.TableCgValues.Count);

            // 4. 数据自洽：总重与总重心由文档力矩求和，与检查点同源。
            double cg = doc.CgPercentMacOf();
            double weight = doc.TotalWeightKg;
            bool totalsOk = Math.Abs(cg - inside.CgPercentMac) <= 1e-9d
                            && Math.Abs(weight - inside.WeightKg) <= 1e-9d;
            Check(name, ref failures, totalsOk, "总重/总重心与起飞机重心检查点一致",
                "总重 " + N(weight, "0") + " kg，重心 " + N(cg, "0.000") + "%MAC");

            // 5. 导出非空：96dpi PNG 字节数 > 5000。
            CheckPng(name, chart, ref failures);
            return failures;
        }

        // ================================================================== 积载图

        /// <summary>
        /// 集装箱积载图样例：4 个 bay、8 只箱，含 1 只 40 尺箱（bay 3 跨 bay 5）
        /// 与危险品/超限高/空箱角标。示例数据，非真实船型/箱位。
        /// </summary>
        private static StowageDocument BuildStowage()
        {
            StowageDocument doc = new StowageDocument
            {
                FormTitle = "集装箱积载图",
                VesselId = "示例船 SB-01（示例数据，非真实船型）",
                VoyageId = "SB001E",
                CrossSectionBay = 3,
            };

            doc.Bays.Add(new StowageBay(1, 2, 2, 6));
            doc.Bays.Add(new StowageBay(3, 2, 2, 6));
            doc.Bays.Add(new StowageBay(5, 2, 2, 6));
            doc.Bays.Add(new StowageBay(7, 2, 2, 6));

            doc.Containers.Add(new StowageContainer(1, 1, 1, ContainerSize.Twenty, 12000d, "SHA"));
            doc.Containers.Add(new StowageContainer(1, 1, 4, ContainerSize.Twenty, 8000d, "SHA"));
            doc.Containers.Add(new StowageContainer(3, 1, 2, ContainerSize.Forty, 24500d, "LAX"));
            doc.Containers.Add(new StowageContainer(3, 2, 3, ContainerSize.Twenty, 9000d, "HKG",
                "3", true, false));
            doc.Containers.Add(new StowageContainer(7, 3, 5, ContainerSize.Twenty, 9000d, "XMN"));
            doc.Containers.Add(new StowageContainer(5, 3, 1, ContainerSize.Twenty, 15000d, "NGB"));
            doc.Containers.Add(new StowageContainer(1, 2, 1, ContainerSize.Twenty, 0d, "NGB",
                null, false, true));
            doc.Containers.Add(new StowageContainer(5, 1, 4, ContainerSize.Twenty, 11000d, "SHA"));
            return doc;
        }

        private static int VerifyStowage(Chart chart)
        {
            const string name = "bay-plan";
            int failures = 0;

            StowageDocument? doc = chart.Document as StowageDocument;
            if (doc == null)
            {
                Check(name, ref failures, false, "文档类型应为 StowageDocument",
                    chart.Document.GetType().Name);
                return failures;
            }

            StowageLayout? layout = chart.Geometry.LayoutOf<StowageLayout>();
            if (layout == null)
            {
                Check(name, ref failures, false, "版面应挂 StowageLayout", "null");
                return failures;
            }

            if (doc.Bays.Count < 2 || doc.Containers.Count < 3)
            {
                Check(name, ref failures, false, "样例应含至少 2 个 bay 与 3 只箱",
                    doc.Bays.Count + " bay／" + doc.Containers.Count + " 箱");
                return failures;
            }

            // 1. tier 越高 y 越小：同一 bay 的 tier 1 与 tier 4 箱块顶边高差 = 3 × 层高。
            StowageContainer low = doc.Containers[0];
            StowageContainer high = doc.Containers[1];
            SKRect lowRect = layout.ProfileRectOf(low);
            SKRect highRect = layout.ProfileRectOf(high);
            double tierStep = (high.Tier - low.Tier) * (double)layout.Tokens.TierHeight;
            double topDelta = lowRect.Top - highRect.Top;
            Check(name, ref failures,
                highRect.Bottom <= lowRect.Top && Math.Abs(topDelta - tierStep) <= 0.51d,
                "同一 bay 内 tier 越高 y 越小（高差 = 层数差 × 层高）",
                "tier " + low.Tier + "→" + high.Tier + " 顶边高差 " + N(topDelta)
                + "px，应 " + N(tierStep) + "px");

            // 2. 40 尺箱左对齐跨两个 bay 位。
            StowageContainer forty = doc.Containers[2];
            if (forty.Size != ContainerSize.Forty)
            {
                Check(name, ref failures, false, "样例应含一只 40 尺箱", forty.Size.ToString());
            }
            else
            {
                int? next = layout.Axis.NextBay(forty.Bay);
                if (next == null)
                {
                    Check(name, ref failures, false, "40 尺箱右端需要相邻 bay 位", "无");
                }
                else
                {
                    SKRect rect = layout.ProfileRectOf(forty);
                    float slotLeft = layout.Axis.BayBand(forty.Bay).Left + layout.Tokens.ContainerInset;
                    float slotRight = layout.Axis.BayBand(next.Value).Right - layout.Tokens.ContainerInset;
                    double leftDeviation = Math.Abs(rect.Left - slotLeft);
                    double rightDeviation = Math.Abs(rect.Right - slotRight);
                    Check(name, ref failures, leftDeviation <= 0.51d && rightDeviation <= 0.51d,
                        "40 尺箱左对齐跨两个 bay 位（bay " + forty.Bay + "→" + next.Value + "）",
                        "左偏差 " + N(leftDeviation) + "px，右偏差 " + N(rightDeviation)
                        + "px，宽 " + N(rect.Width) + "px");
                }
            }

            // 3. 同箱在两视图的可见性：40 尺箱跨到剖面 bay 可见；bay 1 的箱不可见。
            bool fortyVisible = layout.SectionVisible(forty);
            bool otherVisible = layout.SectionVisible(doc.Containers[0]);
            Check(name, ref failures, fortyVisible && !otherVisible,
                "横剖面只显示 bay " + doc.CrossSectionBay + " 及跨 bay 的 40 尺箱",
                "40 尺箱 " + (fortyVisible ? "可见" : "不可见")
                + "，bay 1 箱 " + (otherVisible ? "可见" : "不可见"));

            // 4. 导出非空：96dpi PNG 字节数 > 5000。
            CheckPng(name, chart, ref failures);
            return failures;
        }

        // ================================================================== 危货联单

        /// <summary>
        /// 危险货物道路运输联单样例：主标志（类 3）+ 副标志（类 8）、三联单、罐体牌示意。
        /// 示例数据，非真实机型/标准值；UN 官方象形图不在库内，样例也未注入。
        /// </summary>
        private static DangerousGoodsDocument BuildDangerousGoods()
        {
            DangerousGoodsDocument doc = new DangerousGoodsDocument
            {
                FormTitle = "危险货物道路运输联单",
                UnNumber = "1263",
                ProperShippingName = "涂料（易燃）",
                Class = "3",
                PackingGroup = "II",
                TunnelRestrictionCode = "D/E",
                SubClass = "8",
                Consignor = "示例托运单位（示例数据）",
                Carrier = "示例承运单位（示例数据）",
                Consignee = "示例收货单位（示例数据）",
                EmergencyPhone = "400-000-0000",
                TankPlacardText = "罐体标志牌：类项号 3（示意，非真实罐体图）",
            };
            doc.AddStandardCopies();
            return doc;
        }

        private static int VerifyDangerousGoods(Chart chart)
        {
            const string name = "dg-consignment";
            int failures = 0;

            DangerousGoodsDocument? doc = chart.Document as DangerousGoodsDocument;
            if (doc == null)
            {
                Check(name, ref failures, false, "文档类型应为 DangerousGoodsDocument",
                    chart.Document.GetType().Name);
                return failures;
            }

            DangerousGoodsLayout? layout = chart.Geometry.LayoutOf<DangerousGoodsLayout>();
            if (layout == null)
            {
                Check(name, ref failures, false, "版面应挂 DangerousGoodsLayout", "null");
                return failures;
            }

            // 1. 菱形顶角朝上：顶点 0 为最高点且与锚点同列。
            SKPoint[] vertices = layout.MainVertices();
            bool topUp = vertices.Length == 4
                         && vertices[0].Y < vertices[1].Y && vertices[0].Y < vertices[2].Y
                         && vertices[0].Y < vertices[3].Y
                         && Math.Abs(vertices[0].X - layout.PlacardAnchor.X) <= 0.01d;
            Check(name, ref failures, topUp, "主标志菱形顶角朝上（顶点 0 最高且与锚点同列）",
                "顶点数 " + vertices.Length + "，顶点 0 y=" + N(vertices[0].Y)
                + "，锚点 y=" + N(layout.PlacardAnchor.Y));

            // 2. 四边等长：边长 = 对角线 / √2。
            double expectedSide = layout.PlacardSize / Math.Sqrt(2d);
            double maxSideDeviation = 0d;
            for (int i = 0; i < vertices.Length; i++)
            {
                SKPoint a = vertices[i];
                SKPoint b = vertices[(i + 1) % vertices.Length];
                double dx = (double)b.X - a.X;
                double dy = (double)b.Y - a.Y;
                double deviation = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - expectedSide);
                if (deviation > maxSideDeviation)
                {
                    maxSideDeviation = deviation;
                }
            }
            Check(name, ref failures, maxSideDeviation <= 0.02d,
                "主标志四边等长（边长 = 对角线/" + N(Math.Sqrt(2d), "0.###") + "，允许偏差 0.02px）",
                "最大偏差 " + N(maxSideDeviation) + "px，应 " + N(expectedSide) + "px");

            // 3. 主/副标志各一并排不重叠：副标志左缘 > 主标志右缘，间隙 = 令牌因子 × 对角线。
            bool hasSub = !string.IsNullOrEmpty(doc.SubClass);
            SKRect mainBounds = layout.MainBounds();
            SKRect subBounds = layout.SubBounds();
            double gap = subBounds.Left - mainBounds.Right;
            double expectedGap = layout.PlacardSize * layout.Tokens.SubPlacardGapFactor;
            Check(name, ref failures, hasSub && gap > 0d && Math.Abs(gap - expectedGap) <= 1d,
                "主/副标志各一并排（副标志类别 " + (doc.SubClass ?? "无") + "，间隙 = "
                + N(layout.Tokens.SubPlacardGapFactor, "0.##") + "×对角线）",
                "间隙 " + N(gap) + "px，应 " + N(expectedGap) + "px");

            // 4. 类项号在下半槽：下半槽顶 > 锚点 y；上半图案槽底 < 锚点 y。
            SKRect numberSlot = layout.NumberSlot();
            SKRect symbolSlot = layout.SymbolSlot();
            Check(name, ref failures,
                numberSlot.Top > layout.PlacardAnchor.Y && symbolSlot.Bottom < layout.PlacardAnchor.Y,
                "类项号槽在下半角、图案槽在上半角",
                "号槽顶 " + N(numberSlot.Top) + "，图案槽底 " + N(symbolSlot.Bottom)
                + "，锚点 y " + N(layout.PlacardAnchor.Y));

            // 5. 多联：标准三联，末联右缘与联单区右缘重合。
            bool copiesOk = doc.Copies.Count == 3
                            && Math.Abs(layout.CopyRect(doc.Copies.Count - 1).Right - layout.CollateRect.Right) <= 0.01d;
            Check(name, ref failures, copiesOk, "三联单横向条排且铺满联单区",
                doc.Copies.Count + " 联，末联右缘 "
                + N(layout.CopyRect(doc.Copies.Count - 1).Right) + "／联单区右缘 " + N(layout.CollateRect.Right));

            // 6. 导出非空：96dpi PNG 字节数 > 5000。
            CheckPng(name, chart, ref failures);
            return failures;
        }

        // ================================================================== 超限附图

        /// <summary>
        /// 超限运输许可附图样例：18.00m × 3.50m 车货外廓、4 个轴位（5 段链式尺寸）、
        /// 2 条关键高度坐标式。示例数据，非真实机型/标准值；外廓不是任何真实车型。
        /// </summary>
        private static OversizeLoadDocument BuildOversizeLoad()
        {
            OversizeLoadDocument doc = new OversizeLoadDocument
            {
                FormTitle = "超限运输许可附图",
                PermitNumber = "示例-SB-2026-001",
                VehiclePlate = "示例·SB0001",
                CargoName = "示例超限货（示例数据，非真实机型/标准值）",
                LengthMeters = 18d,
                HeightMeters = 3.5d,
                WidthMeters = 3.2d,
                ToleranceMillimeters = 50d,
            };

            doc.Profile.Add(new OversizeProfilePoint(0d, 0d));
            doc.Profile.Add(new OversizeProfilePoint(0d, 1.15d));
            doc.Profile.Add(new OversizeProfilePoint(0.5d, 1.55d));
            doc.Profile.Add(new OversizeProfilePoint(1.9d, 1.85d));
            doc.Profile.Add(new OversizeProfilePoint(2.3d, 2.95d));
            doc.Profile.Add(new OversizeProfilePoint(2.9d, 3.15d));
            doc.Profile.Add(new OversizeProfilePoint(3.4d, 3.15d));
            doc.Profile.Add(new OversizeProfilePoint(3.4d, 3.5d));
            doc.Profile.Add(new OversizeProfilePoint(14.8d, 3.5d));
            doc.Profile.Add(new OversizeProfilePoint(15.6d, 2.7d));
            doc.Profile.Add(new OversizeProfilePoint(18d, 2.35d));
            doc.Profile.Add(new OversizeProfilePoint(18d, 0d));
            doc.Profile.Add(new OversizeProfilePoint(0d, 0d));

            doc.KeyHeightsMeters.Add(1.85d);
            doc.KeyHeightsMeters.Add(3.5d);

            doc.Axles.Add(new OversizeAxle(2.7d, 6.0d));
            doc.Axles.Add(new OversizeAxle(11.4d, 10.5d));
            doc.Axles.Add(new OversizeAxle(13.2d, 9.0d));
            doc.Axles.Add(new OversizeAxle(15.6d, 7.5d));
            return doc;
        }

        private static int VerifyOversizeLoad(Chart chart)
        {
            const string name = "ogami-profile";
            int failures = 0;

            OversizeLoadDocument? doc = chart.Document as OversizeLoadDocument;
            if (doc == null)
            {
                Check(name, ref failures, false, "文档类型应为 OversizeLoadDocument",
                    chart.Document.GetType().Name);
                return failures;
            }

            OversizeLoadLayout? layout = chart.Geometry.LayoutOf<OversizeLoadLayout>();
            if (layout == null)
            {
                Check(name, ref failures, false, "版面应挂 OversizeLoadLayout", "null");
                return failures;
            }

            IReadOnlyList<DimensionRun> runs = layout.Chain.Runs;
            int expectedRuns = doc.Axles.Count + 1;

            // 1. 链式段数 = 轴数 + 1，且相邻段首尾严格相接。
            bool joined = runs.Count == expectedRuns;
            double maxJoinGap = 0d;
            for (int i = 0; joined && i + 1 < runs.Count; i++)
            {
                double gap = DimensionRun.Distance(runs[i].To, runs[i + 1].From);
                if (gap > maxJoinGap)
                {
                    maxJoinGap = gap;
                }
                joined = gap <= 1e-3d;
            }
            Check(name, ref failures, joined,
                "链式尺寸 ≥3 段且相邻段首尾相接（段数 = 轴数 + 1）",
                runs.Count + " 段（应 " + expectedRuns + " 段），最大接缝 " + N(maxJoinGap) + "px");

            // 2. 各段像素长之和 = 端到端距离。
            double sum = layout.Chain.SumOfRunLengths;
            double span = DimensionRun.Distance(layout.Chain.Origin, layout.Chain.Terminator);
            Check(name, ref failures, Math.Abs(sum - span) <= 1e-3d,
                "尺寸链各段像素长之和等于端到端距离",
                "段和 " + N(sum) + "px，端到端 " + N(span) + "px");

            // 3. 尺寸数字由坐标回写：各段量值之和 = 总长；总高 = 文档总高。
            double measuredSum = 0d;
            for (int i = 0; i < runs.Count; i++)
            {
                measuredSum += runs[i].Measured ?? double.NaN;
            }
            double heightMeasured = layout.HeightRun.Measured ?? double.NaN;
            Check(name, ref failures,
                Math.Abs(measuredSum - doc.LengthMeters) <= 0.01d
                && Math.Abs(heightMeasured - doc.HeightMeters) <= 0.01d,
                "链式回写量值之和 = 总长，总高回写 = 文档总高",
                "链和 " + N(measuredSum, "0.000") + "m／总长 " + N(doc.LengthMeters, "0.000")
                + "m，总高 " + N(heightMeasured, "0.000") + "m／" + N(doc.HeightMeters, "0.000") + "m");

            // 4. 轴荷条高与轴荷成比例（重轴条更高）。
            OversizeAxle heavy = doc.Axles[0];
            OversizeAxle light = doc.Axles[0];
            for (int i = 1; i < doc.Axles.Count; i++)
            {
                if (doc.Axles[i].LoadTonnes > heavy.LoadTonnes)
                {
                    heavy = doc.Axles[i];
                }
                if (doc.Axles[i].LoadTonnes < light.LoadTonnes)
                {
                    light = doc.Axles[i];
                }
            }
            double heavyHeight = layout.AxleBarRect(heavy).Height;
            double lightHeight = layout.AxleBarRect(light).Height;
            double expectedRatio = light.LoadTonnes > 0d ? heavy.LoadTonnes / light.LoadTonnes : 1d;
            double actualRatio = lightHeight > 0d ? heavyHeight / lightHeight : double.NaN;
            Check(name, ref failures, heavyHeight > lightHeight
                && Math.Abs(actualRatio - expectedRatio) <= 0.01d,
                "轴荷条高与轴荷成比例（重轴更高）",
                "重轴 " + N(heavy.LoadTonnes, "0.0") + "t 条高 " + N(heavyHeight)
                + "px／轻轴 " + N(light.LoadTonnes, "0.0") + "t 条高 " + N(lightHeight)
                + "px，比值 " + N(actualRatio, "0.000"));

            // 5. 导出非空：96dpi PNG 字节数 > 5000。
            CheckPng(name, chart, ref failures);
            return failures;
        }

        // ================================================================== 工具

        private static void Check(string name, ref int failures, bool ok, string description, string actual)
        {
            if (!ok)
            {
                failures++;
            }
            Console.WriteLine("[" + name + "] " + (ok ? "通过" : "失败") + ": " + description
                              + " 实到 " + actual);
        }

        private static void CheckPng(string name, Chart chart, ref int failures)
        {
            byte[] png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            Check(name, ref failures, png.Length > 5000,
                "96dpi PNG 导出非空（字节数 > 5000）",
                png.Length.ToString(CultureInfo.InvariantCulture) + " B");
        }

        private static string N(double value, string format = "0.###")
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
