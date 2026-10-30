# OODD.Charts

**简体中文** ｜ [English](README.en.md)

用 SkiaSharp 复刻那些必须“一格一格对齐”的纸质单据：体温单、产程图、抢救记录、工作票、发票、测井图、天气图。它们不是数据可视化——通用图表控件画不出来，因为格子尺寸、线宽、几套坐标系共用同一条横线、字要落在格里，全是硬约束。

这里的做法是把版面当几何来解：每张单子一个文档模型加一个模板，在 96dpi 逻辑坐标里算一次，PNG / JPEG / PDF / SVG 都从同一份 `SKPicture` 出来；300dpi 只做 `canvas.Scale`，格线不会漂。控件层很薄，WinForms / WPF / Avalonia 拿到就能显示，雷达还能直接点选、悬停、扫描动画。

目前是 40 张单据、14 个 NuGet 包、7 个宿主适配器。473 项测试在 net8.0 与 net462 上各跑一遍；40 张样例逐张出图并断言版面几何，“能渲染”不算通过。

| 三测单 | 产程图 |
|---|---|
| ![三测单](docs/images/temperature-96dpi.png) | ![产程图](docs/images/partogram-96dpi.png) |

| Code Blue 抢救记录 | 雷达态势图（360°） |
|---|---|
| ![抢救记录](docs/images/code-blue-96dpi.png) | ![雷达](docs/images/radar-full-96dpi.png) |

| 列车运行图 | 增值税专用发票 |
|---|---|
| ![运行图](docs/images/train-graph-96dpi.png) | ![发票](docs/images/vat-invoice-96dpi.png) |

## 目录

- [安装](#安装)
- [快速上手](#快速上手)
- [医疗表单](#医疗表单)（21 张）
- [行业扩展表单](#行业扩展表单)（17 张）
- [雷达、引擎与宿主控件](#雷达引擎与宿主控件)
- [验证到哪一步](#验证到哪一步)
- [已知限制](#已知限制)
- [许可](#许可)

## 安装

包随 [Releases](https://github.com/OODD-DevNet/OODD.Charts/releases) 提供，没有发布到 nuget.org。把需要的 `.nupkg` 下载到本地目录，加一个本地源即可：

```bash
mkdir packages && cd packages
# 从 Release 页面下载 nupkg 到这里，然后：
dotnet nuget add source . -n oodd-local
dotnet add package OODD.Charts.Medical --version 1.0.0
```

| 包 | 装什么 |
|---|---|
| `OODD.Charts.Core` | 渲染管线、几何与轴、导出、字体回退、交互契约 |
| `OODD.Charts.Medical` | 5 张手写模板 + 16 张数据驱动记录单 + 5 张医疗扩展表（共 26 张） |
| `OODD.Charts.Radar` | 雷达态势图与 `RadarInteraction` |
| `OODD.Charts.Engineering` | 测井成果图、变压器油色谱 |
| `OODD.Charts.Transit` | 列车运行图、载重平衡、积载图、危货联单、超限附图 |
| `OODD.Charts.Geoscience` | 地面天气图、T-lnP |
| `OODD.Charts.Tickets` | 电气工作票、特殊作业票、增值税专用发票 |
| `OODD.Charts.Adapters.WinForms` | WinForms `ChartControl` + `ToBitmap` |
| `OODD.Charts.Adapters.Wpf` | WPF `ChartControl` + `ToWriteableBitmap` |
| `OODD.Charts.Adapters.Avalonia` | Avalonia `ChartControl` + `ToWriteableBitmap` |
| `OODD.Charts.Adapters.Eto` | Eto.Forms `ChartControl` + `ToEtoBitmap` |
| `OODD.Charts.Adapters.WinUi` | WinUI 3 `ChartControl` + `ToWriteableBitmap` |
| `OODD.Charts.Adapters.Uno` | Uno Platform `ChartControl` + `ToWriteableBitmap` |
| `OODD.Charts.Adapters.Maui` | MAUI `ChartImage` / `ChartCanvasView` + `ToImageSource` |

核心包的目标框架是 `net462;netstandard2.0;net8.0`，适配器按宿主收窄（例如 WinForms/WPF 是 `net462;net8.0-windows`）。SkiaSharp 的原生库由 NuGet 自动带；Linux 上额外需要 `libfontconfig1`。

## 快速上手

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using SkiaSharp;

// 1. 造文档
TemperatureChartDocument doc = new TemperatureChartDocument
{
    HospitalName = "示例医院",
    FormTitle = "体温单",
};
doc.Patient.Name = "张三";
doc.Patient.Sex = "男";
doc.Patient.Age = "36";
doc.Days.Add(new ChartGroup(new DateTime(2026, 10, 30)));

PointSeries axillary = new PointSeries("axillary", TemperatureChartTemplate.AxisTemperature)
{
    Symbol = MarkerSymbol.Ring,
    Color = SKColors.Blue,
};
axillary.Values[0] = 36.8;   // 槽位 0 = 当天 15 时
axillary.Values[2] = 37.4;   // 槽位 2 = 当天 23 时
doc.Series.Add(axillary);

// 2. 选模板，组 Chart
using Chart chart = new Chart(doc, new TemperatureChartTemplate());

// 3. 出图
using (FileStream fs = File.Create("temperature.png")) chart.WritePng(fs, 96f);
using (FileStream fs = File.Create("temperature.pdf")) chart.WritePdf(fs, PdfOptions.A4Portrait);
using (FileStream fs = File.Create("temperature.svg")) chart.WriteSvg(fs);

// 4. 或者交给宿主：任何 SKCanvas（控件 / 打印 / 离屏 Surface）
```

文档与模板是解耦的：同一份文档可以随时换模板重绘，模板也不持有业务状态。要新增一张单据，实现一个 `IChartTemplate` 就行，引擎不用改。

## 演示程序

Release 里附了一个自包含的 WPF 演示（`OODD.Charts.Demo.Wpf-1.0.0-win-x64.zip`，解压双击 exe，不需要装 .NET）。下拉框能切全部 40 张单据，支持贴合窗口 / 原尺寸缩放，导出 PNG(300dpi) / PDF / SVG。

源码里的三个演示都能直接跑：

```bash
dotnet run --project demo/OODD.Charts.Demo.WinForms
dotnet run --project demo/OODD.Charts.Demo.Wpf
dotnet run --project demo/OODD.Charts.Demo.Avalonia
```

## 医疗表单

21 张单据分三组：三测单与产程图是两套坐标压在同一条横线上的连续曲线单，疼痛、保护性约束、输血反应是三张手工模板，其余 16 张由 `TimelineFormTemplate` / `FormSheetTemplate` 承载。前两组在模板里逐笔求解版面，后一组是数据驱动、零渲染代码：模板只声明区段或轴参数，业务侧往 `SetCell(...)` / `Cells` 里填文字。全部单据共用 `Chart` 门面、同一条 PNG/PDF/SVG 导出路径，也能直接挂到 WinForms / WPF 等宿主控件。

### 三测单（体温单） · `temperature`

![三测单（体温单）](docs/images/temperature-96dpi.png)

**模板** `TemperatureChartTemplate` · **文档** `TemperatureChartDocument` · **样例** `demo/OODD.Charts.Demo.Common/TemperatureSample.cs`

版面的核心难点是两套刻度压在同一条横线上：体温轴 35–41℃（0.2℃ 细格、1℃ 粗格）与脉搏轴 40–160 次/分共用同一组网格，曲线靠 `AxisTemperature` / `AxisPulse` 两个轴键区分落点。未赋值的槽位在 `Values` 里根本不存在，曲线自动断开，物理降温后的复测点用 `ValueLabels` 加"↓"。附属表里呼吸按 15px 逐槽细分，其余行按日合并成 30px 单格；`Days` 每加一天就多一页，日期、星期与钟点由模板重算。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using SkiaSharp;

TemperatureChartDocument doc = new TemperatureChartDocument { HospitalName = "测试医院", FormTitle = "体温单" };
doc.Patient.Name = "刘敏";
doc.Patient.Department = "中医科";
doc.Days.Add(new ChartGroup(new DateTime(2025, 12, 28)));
PointSeries axillary = new PointSeries("axillary", TemperatureChartTemplate.AxisTemperature)
{
    Symbol = MarkerSymbol.Ring,
    Color = SKColors.Blue,
    LegendText = "腋表",
};
axillary.Values[0] = 36.8;
axillary.Values[2] = 37.4;
doc.Series.Add(axillary);
using Chart chart = new Chart(doc, new TemperatureChartTemplate());
File.WriteAllBytes("temperature.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 产程图 · `partogram`

![产程图](docs/images/partogram-96dpi.png)

**模板** `PartogramChartTemplate` · **文档** `PartogramDocument` · **样例** `demo/OODD.Charts.Demo.Common/PartogramSample.cs`

宫口扩张（0–10cm）与胎先露（−3–+3cm）是两套方向相反的纵轴，共用同一条时间横线；`Cervix`、`Station` 是文档自带的两个 `PointSeries`。警戒线与处理线的位置由 `AlertStartHour`、`AlertStartCm`、`AlertSlopeCmPerHour`、`ActionOffsetHours` 四个数算出，不进模板。横轴一小时两槽（`SlotsPerHour = 2`），胎心落在 120–160 之外自动套红圈，宫缩按次数与持续秒数画块，破膜标记吸附到宫口曲线上。按小时分页时槽号与钟点一起重算。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Partogram;

PartogramDocument doc = new PartogramDocument
{
    HospitalName = "测试医院",
    StartTime = new DateTime(2026, 1, 15, 8, 0, 0),
    Hours = 16,
    SlotsPerHour = 2,
    AlertStartHour = 6,
};
doc.Patient.Name = "王芳";
doc.Patient.Department = "产科";
doc.Cervix.Values[12] = 4;
doc.FetalHeart.Add(new FetalHeartReading(12, 168));
doc.Contractions.Add(new ContractionReading(12, 4) { DurationSeconds = 40 });
doc.Membrane.Add(new MembraneMark(22) { Text = "破" });
using Chart chart = new Chart(doc, new PartogramChartTemplate());
File.WriteAllBytes("partogram.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 疼痛评估记录单 · `pain`

![疼痛评估记录单](docs/images/pain-96dpi.png)

**模板** `PainChartTemplate` · **文档** `PainChartDocument` · **样例** `demo/OODD.Charts.Demo.Common/PainSample.cs`

纵轴 NRS 0–10，1 分 22px、偶数分画粗线；静息痛与活动痛各一条 `PointSeries`，都落在 `PainChartTemplate.AxisNrs` 上。评分 ≥7 时模板画红色虚线阈值并给重度点套红圈，缺测的槽位断线而不补零。横轴一天 6 个评估时点（02/06/10/14/18/22），每列 52px，附属行逐次填写部位、性质、干预、用药与评估者。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Pain;
using OODD.Charts.Model;
using SkiaSharp;

PainChartDocument doc = new PainChartDocument { HospitalName = "测试医院", FormTitle = "疼痛评估记录单" };
doc.Patient.Name = "陈晓";
doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 2)));
PointSeries rest = new PointSeries("rest", PainChartTemplate.AxisNrs)
{
    Symbol = MarkerSymbol.Ring,
    Color = SKColors.Blue,
};
rest.Values[0] = 6;
rest.Values[1] = 8;
doc.Series.Add(rest);
doc.Cells.Add(new CellValue(PainFields.Site, 1, "切口"));
doc.Cells.Add(new CellValue(PainFields.Character, 1, "胀痛"));
using Chart chart = new Chart(doc, new PainChartTemplate());
File.WriteAllBytes("pain.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 保护性约束巡视记录单 · `restraint`

![保护性约束巡视记录单](docs/images/restraint-96dpi.png)

**模板** `RestraintChartTemplate` · **文档** `RestraintChartDocument` · **样例** `demo/OODD.Charts.Demo.Common/RestraintSample.cs`

一天 24 个小时槽，顶部是一条 Gantt 式约束时段条：`RestraintEpisode(start, endExclusive, text)` 给闭开区间，`IsEmergency` 切到强调色，跨页时按页裁切但保留紧急标记。下方逐小时填约束部位、皮肤、末梢血运、是否定时松解、饮食排泄与巡视者，列宽 26px、行高 30px，一小时一格不合并。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Restraint;
using OODD.Charts.Model;

RestraintChartDocument doc = new RestraintChartDocument { HospitalName = "测试医院", FormTitle = "保护性约束巡视记录单" };
doc.Patient.Name = "周敏";
doc.Patient.Department = "精神科";
doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 5)));
doc.Episodes.Add(new RestraintEpisode(1, 6, "双上肢"));
doc.Episodes.Add(new RestraintEpisode(14, 18, "四肢") { IsEmergency = true });
doc.Cells.Add(new CellValue(RestraintFields.Skin, 2, "完好"));
doc.Cells.Add(new CellValue(RestraintFields.Nurse, 2, "甲"));
using Chart chart = new Chart(doc, new RestraintChartTemplate());
File.WriteAllBytes("restraint.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 输血反应记录单 · `transfusion`

![输血反应记录单](docs/images/transfusion-96dpi.png)

**模板** `TransfusionChartTemplate` · **文档** `TransfusionChartDocument` · **样例** `demo/OODD.Charts.Demo.Common/TransfusionSample.cs`

横轴是输注开始后的半小时观察点（默认 8 槽），纵轴 36–41℃、每 0.2℃ 12px。`Temperature` 是文档自带的单条曲线，38℃ 发热阈值由 `Scale.FeverThreshold` 决定并画成红虚线；反应事件用 `EventMark` 画红色竖线，标签按索引奇偶错行置顶。症状、血压、脉搏呼吸、处理、用药、标本逐槽记录，分页时槽号一起重映射。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Transfusion;
using OODD.Charts.Model;

TransfusionChartDocument doc = new TransfusionChartDocument { HospitalName = "测试医院", FormTitle = "输血反应记录单" };
doc.Patient.Name = "李国栋";
doc.Patient.Department = "血液科";
doc.Days.Add(new ChartGroup(new DateTime(2026, 2, 11)));
doc.Temperature.Values[0] = 36.6;
doc.Temperature.Values[3] = 38.9;
doc.Temperature.Values[4] = 39.2;
doc.Events.Add(new EventMark(3, "寒战"));
doc.Cells.Add(new CellValue(TransfusionFields.Symptoms, 3, "寒战"));
doc.Cells.Add(new CellValue(TransfusionFields.BloodPressure, 3, "104/62"));
using Chart chart = new Chart(doc, new TransfusionChartTemplate());
File.WriteAllBytes("transfusion.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 高压氧治疗陪舱记录单 · `hyperbaric-companion`

![高压氧治疗陪舱记录单](docs/images/hyperbaric-companion-96dpi.png)

**模板** `HyperbaricCompanionTemplate` · **文档** `HyperbaricCompanionDocument` · **样例** `demo/OODD.Charts.Demo.Common/HyperbaricCompanionSample.cs`

`TimelineFormTemplate` 实例，时间带与数值轴由引擎固定，文档只声明参数：引擎把"日期/时点带 + 数值轴网格 + 附属行"固定下来，`HyperbaricCompanionDocument` 只声明纵轴 0–3.0 ATA、每 10 分钟一槽与四行附属表。舱压曲线落在 `AxisPressure` 上，升压—稳压—减压三段连成折线；`ThresholdMark` 画稳压上限，`EventMark` 标注开始加压、达压、开始减压与安全出舱。0.1 ATA 一格、0.5 ATA 一条粗线。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Medical.HyperbaricCompanion;
using OODD.Charts.Model;
using SkiaSharp;

HyperbaricCompanionDocument doc = new HyperbaricCompanionDocument { HospitalName = "测试医院" };
doc.Patient.Name = "刘桂芳";
doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 12)) { DateText = "2026-03-12 08:30 首次进舱" });
PointSeries pressure = new PointSeries("pressure", HyperbaricCompanionRows.AxisPressure);
pressure.Color = SKColors.Blue;
pressure.Values[0] = 1.0;
pressure.Values[2] = 2.0;
pressure.Values[9] = 2.0;
pressure.Values[12] = 1.0;
doc.Series.Add(pressure);
doc.Thresholds.Add(new ThresholdMark(2.2, "稳压上限") { Color = SKColors.Red });
doc.Cells.Add(new CellValue(HyperbaricCompanionRows.Status, 6, "耳压不适"));
using Chart chart = new Chart(doc, new HyperbaricCompanionTemplate());
File.WriteAllBytes("hyperbaric-companion.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 高压氧进舱宣教记录单 · `hyperbaric-education`

![高压氧进舱宣教记录单](docs/images/hyperbaric-education-96dpi.png)

**模板** `HyperbaricEducationTemplate` · **文档** `HyperbaricEducationDocument` · **样例** `demo/OODD.Charts.Demo.Common/HyperbaricEducationSample.cs`

`FormSheetTemplate` 实例。模板里没有一行只服务于这张单子，骨架全由引擎给：引擎提供页眉、区段标题、列头与合并备注，文档只声明四个区段（进舱信息、宣教内容、禁忌症确认、签名）。宣教区段三列，长句按中英文混排贪心换行，行高随实际行数加高，内容写三行也不会压到下一行。勾选用 √ 与 □，因为 Microsoft YaHei / SimSun 没有 ☑、☐ 字形，渲染成豆腐块。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.HyperbaricEducation;

HyperbaricEducationDocument doc = new HyperbaricEducationDocument { HospitalName = "测试医院" };
doc.Patient.Name = "李建国";
doc.Patient.Department = "高压氧科";
doc.SetCell(HyperbaricEducationSections.Info, HyperbaricEducationRows.Cabin, 1, "2 号多人舱（2.0 ATA）");
doc.SetCell(HyperbaricEducationSections.Info, HyperbaricEducationRows.Escort, 1, "家属陪舱（配偶 王秀兰）");
doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EarPressure, 1,
    "升压时做捏鼻鼓气、吞咽或咀嚼动作；出现耳闷、耳痛立即告知陪舱人员。");
doc.SetCell(HyperbaricEducationSections.Teaching, HyperbaricEducationRows.EarPressure, 2, "√ 已掌握");
doc.SetCell(HyperbaricEducationSections.Contraindication, HyperbaricEducationRows.Absolute, 1, "√ 已排除（无气胸）");
using Chart chart = new Chart(doc, new HyperbaricEducationTemplate());
File.WriteAllBytes("hyperbaric-education.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 神经外科高压氧疗接送记录单 · `neurosurgery-hyperbaric-transfer`

![神经外科高压氧疗接送记录单](docs/images/neurosurgery-hyperbaric-transfer-96dpi.png)

**模板** `HyperbaricTransferTemplate` · **文档** `HyperbaricTransferDocument` · **样例** `demo/OODD.Charts.Demo.Common/HyperbaricTransferSample.cs`

又一张表格引擎的单子，`FormSheetTemplate` 只认文档里的区段与列：同一页混排两种版式，去程与回程各是一张六列生命体征表（体温、脉搏、呼吸、血压、SpO2、意识），途中情况与交接则是跨全宽的叙述格。转运时间、接送方式写在两列的"项目 / 内容"区段，长段交接记录按行数自动加高，不会把签名区挤出纸面。区段与列宽都在文档构造里一次定死。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.HyperbaricTransfer;

HyperbaricTransferDocument doc = new HyperbaricTransferDocument { HospitalName = "测试医院" };
doc.Patient.Name = "陈立军";
doc.Patient.Department = "神经外科";
doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.OutboundTime, 1, "08:40（病房出发）");
doc.SetCell(HyperbaricTransferSections.Transfer, HyperbaricTransferRows.ReturnTime, 1, "10:25（返回病房）");
doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 0, "36.8");
doc.SetCell(HyperbaricTransferSections.Outbound, HyperbaricTransferRows.OutboundVital, 3, "142/86");
doc.SetCell(HyperbaricTransferSections.Inbound, HyperbaricTransferRows.InboundVital, 3, "136/82");
doc.SetCell(HyperbaricTransferSections.Handover, HyperbaricTransferRows.InboundNote, 1,
    "10:10 出舱，无头痛、恶心，切口敷料干燥；10:25 返回病房，床旁核对腕带与输液通路。");
using Chart chart = new Chart(doc, new HyperbaricTransferTemplate());
File.WriteAllBytes("neurosurgery-hyperbaric-transfer.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### Code Blue 抢救记录本 · `code-blue`

![Code Blue 抢救记录本](docs/images/code-blue-96dpi.png)

**模板** `CodeBlueTemplate` · **文档** `CodeBlueDocument` · **样例** `demo/OODD.Charts.Demo.Common/CodeBlueSample.cs`

时间轴单据引擎的另一例：`TimelineFormTemplate`，连附属行都是数据：0–60 分钟每 5 分钟一槽，心率与收缩压共用 20–200 的同一根纵轴，两条曲线都设 `Connect = false`，避免把测量点连成并不存在的趋势。`ThresholdMark(90, "低灌注")` 画阈值线，`EventMark` 记录发现骤停、肾上腺素、除颤与自主循环恢复，标签按奇偶错行。心律、用药、除颤/操作、记录者四行附属表逐槽填写。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.CodeBlue;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;
using SkiaSharp;

CodeBlueDocument doc = new CodeBlueDocument { HospitalName = "测试医院" };
doc.Patient.Name = "赵国强";
doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 9)) { DateText = "14:32 心搏骤停，启动 Code Blue" });
PointSeries heartRate = new PointSeries("hr", CodeBlueRows.AxisVital);
heartRate.Color = SKColors.Red;
heartRate.Connect = false;
heartRate.Values[0] = 128;
heartRate.Values[3] = 150;
doc.Series.Add(heartRate);
doc.Thresholds.Add(new ThresholdMark(90, "低灌注") { Color = SKColors.Red });
doc.Events.Add(new EventMark(3, "除颤 200J") { Color = SKColors.Red });
doc.Cells.Add(new CellValue(CodeBlueRows.Rhythm, 0, "室颤"));
using Chart chart = new Chart(doc, new CodeBlueTemplate());
File.WriteAllBytes("code-blue.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 患者自控镇痛（PCA）记录单 · `pca`

![患者自控镇痛（PCA）记录单](docs/images/pca-96dpi.png)

**模板** `PcaTemplate` · **文档** `PcaDocument` · **样例** `demo/OODD.Charts.Demo.Common/PcaSample.cs`

这张走 `TimelineFormTemplate`，没有一行绘制代码：以术后返回病房为 0 时、每 2 小时一槽，纵轴是 2 小时内的有效按压次数（0–12）。`ThresholdMark(6, ...)` 标出"频繁按压需评估"的界线，`EventMark` 记追加给药。附属行逐槽填累计进药量、锁定时间、NRS 与记录者；泵型号、药物配比、单次剂量、锁定时间写在页眉扩展字段里，不占表格行。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Medical.Pca;
using OODD.Charts.Model;
using SkiaSharp;

PcaDocument doc = new PcaDocument { HospitalName = "测试医院" };
doc.Patient.Name = "林静";
doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 14)) { DateText = "2026-03-14 术后第 1 天" });
PointSeries presses = new PointSeries("presses", PcaRows.AxisPresses);
presses.Color = SKColors.Red;
presses.Connect = false;
presses.Values[1] = 2;
presses.Values[5] = 3;
doc.Series.Add(presses);
doc.Thresholds.Add(new ThresholdMark(6, "频繁按压需评估"));
doc.Cells.Add(new CellValue(PcaRows.Volume, 5, "10.0"));
doc.Events.Add(new EventMark(5, "追加吗啡 2mg"));
using Chart chart = new Chart(doc, new PcaTemplate());
File.WriteAllBytes("pca.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### APACHE II 评分表 · `apache-ii`

![APACHE II 评分表](docs/images/apache-ii-96dpi.png)

**模板** `ApacheIiTemplate` · **文档** `ApacheIiDocument` · **样例** `demo/OODD.Charts.Demo.Common/ApacheIiSample.cs`

这张同样交给 `FormSheetTemplate`，渲染代码为零，信息都在文档里：三个区段分别是 12 项生理指标、年龄与慢性健康、评分结果，每行四列（指标 / 实测值 / 分值 / 说明），总分行的 `IsTotal` 打开后底色加深。`SetCell(section, row, column, text)` 按区段键与行键改文字，不碰几何，因此 12 行的顺序与列宽都在文档里一次定死，填表方只管数值。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.ApacheIi;

ApacheIiDocument doc = new ApacheIiDocument { HospitalName = "测试医院" };
doc.Patient.Name = "陈国栋";
doc.Patient.Department = "重症医学科";
doc.SetCell(ApacheIiSections.Physiology, "temperature", 1, "38.6");
doc.SetCell(ApacheIiSections.Physiology, "temperature", 2, "2");
doc.SetCell(ApacheIiSections.Physiology, "gcs", 1, "9");
doc.SetCell(ApacheIiSections.Physiology, "gcs", 2, "6");
doc.SetCell(ApacheIiSections.Chronic, "age", 1, "72 岁");
doc.SetCell(ApacheIiSections.Chronic, "age", 2, "5");
doc.SetCell(ApacheIiSections.Result, "total", 1, "44");
using Chart chart = new Chart(doc, new ApacheIiTemplate());
File.WriteAllBytes("apache-ii.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 手术安全核查表 · `surgical-safety-checklist`

![手术安全核查表](docs/images/surgical-safety-checklist-96dpi.png)

**模板** `SurgicalSafetyChecklistTemplate` · **文档** `SurgicalSafetyChecklistDocument` · **样例** `demo/OODD.Charts.Demo.Common/SurgicalSafetyChecklistSample.cs`

`FormSheetTemplate` 的又一例：引擎画页眉与列头，业务侧只管填文字：麻醉前（Sign In）、切皮前（Time Out）、离室前（Sign Out）三段各是一张三列表（核查项目 / 核查结果 / 核查人），最后一段是四方签名。行键固定在文档里（`PatientIdentity`、`Procedure`、`Counts` 等），业务侧只填结果与签名；勾选用 √ 而不是 ☑，理由同宣教单。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.SurgicalSafetyChecklist;

SurgicalSafetyChecklistDocument doc = new SurgicalSafetyChecklistDocument { HospitalName = "测试医院" };
doc.Patient.Name = "张慧敏";
doc.Patient.Department = "普外科";
doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.PatientIdentity, 1, "√ 是（与腕带一致）");
doc.SetCell(SurgicalSafetyChecklistSections.SignIn, SurgicalSafetyChecklistRows.PatientIdentity, 2, "王芳");
doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Procedure, 1, "√ 已核对（腹腔镜胆囊切除）");
doc.SetCell(SurgicalSafetyChecklistSections.TimeOut, SurgicalSafetyChecklistRows.Procedure, 2, "陈立");
doc.SetCell(SurgicalSafetyChecklistSections.SignOut, SurgicalSafetyChecklistRows.Counts, 1, "√ 已核对（器械敷料相符）");
doc.SetCell(SurgicalSafetyChecklistSections.Signature, SurgicalSafetyChecklistRows.Sign, 0, "陈立");
using Chart chart = new Chart(doc, new SurgicalSafetyChecklistTemplate());
File.WriteAllBytes("surgical-safety-checklist.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 中医专病辨证施护护理记录单 · `tcm-syndrome-nursing`

![中医专病辨证施护护理记录单](docs/images/tcm-syndrome-nursing-96dpi.png)

**模板** `TcmNursingTemplate` · **文档** `TcmNursingDocument` · **样例** `demo/OODD.Charts.Demo.Common/TcmNursingSample.cs`

表格引擎承载的一张。区段、列宽、合并备注都写在文档构造里：辨证信息四列（证型 / 治则 / 舌脉 / 主症），施护记录四列（日期时间 / 施护项目 / 实施情况 / 效果评价），护理记录是一段跨全宽的 `Note`。施护条目常写成一整句，引擎按中英文混排换行并加高行高，同一条写三行也不会挤掉下一条。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.TcmNursing;

TcmNursingDocument doc = new TcmNursingDocument { HospitalName = "测试医院" };
doc.Patient.Name = "吴国华";
doc.Patient.Department = "心血管内科";
doc.SetCell(TcmNursingSections.Syndrome, TcmNursingRows.Pattern, 0, "气虚血瘀证");
doc.SetCell(TcmNursingSections.Syndrome, TcmNursingRows.Pattern, 1, "益气活血、通络止痛");
doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Auricular, 0, "2026-03-09 09:30");
doc.SetCell(TcmNursingSections.Measures, TcmNursingRows.Auricular, 2,
    "取心、神门、交感、皮质下穴，王不留行籽贴压，双耳交替，每日按压 3 次。");
doc.Section(TcmNursingSections.Notes)!.Note = "3 月 9 日：胸闷发作 2 次，含服硝酸甘油后缓解。";
using Chart chart = new Chart(doc, new TcmNursingTemplate());
File.WriteAllBytes("tcm-syndrome-nursing.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 癌症心理服务会诊回复 · `cancer-psychology-consult`

![癌症心理服务会诊回复](docs/images/cancer-psychology-consult-96dpi.png)

**模板** `CancerPsychologyConsultTemplate` · **文档** `CancerPsychologyConsultDocument` · **样例** `demo/OODD.Charts.Demo.Common/CancerPsychologyConsultSample.cs`

这张没有自己的模板类，直接组合 `FormSheetTemplate`：申请、评估、意见、签名四个区段，申请区段是三列表加一段合并备注，评估区段逐项写量表分值与依据，意见区段第二列放编号列表（`\n` 分段、引擎按行数加高）。会诊单号与诊断走页眉扩展字段，不占表格行。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.CancerPsychologyConsult;

CancerPsychologyConsultDocument doc = new CancerPsychologyConsultDocument { HospitalName = "测试医院" };
doc.Patient.Name = "王丽";
doc.Patient.Department = "肿瘤内科";
doc.SetCell(CancerPsychologyConsultSections.Application, "basic", 0, "肿瘤内科");
doc.SetCell(CancerPsychologyConsultSections.Application, "basic", 2, "2026-03-18 09:20");
doc.Section(CancerPsychologyConsultSections.Application)!.Note =
    "会诊目的：术后化疗期间持续失眠、夜间惊恐发作，请协助评估并制定干预方案。";
doc.SetCell(CancerPsychologyConsultSections.Assessment, "anxiety", 1, "62 分");
doc.SetCell(CancerPsychologyConsultSections.Assessment, "anxiety", 2, "SAS 中度焦虑");
doc.SetCell(CancerPsychologyConsultSections.Opinion, "diagnosis", 1, "每晚睡前正念呼吸训练 10 分钟，一周后复评 SAS。");
using Chart chart = new Chart(doc, new CancerPsychologyConsultTemplate());
File.WriteAllBytes("cancer-psychology-consult.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 健检身体组成分析检查报告 · `body-composition`

![健检身体组成分析检查报告](docs/images/body-composition-96dpi.png)

**模板** `BodyCompositionTemplate` · **文档** `BodyCompositionDocument` · **样例** `demo/OODD.Charts.Demo.Common/BodyCompositionSample.cs`

继续是 `FormSheetTemplate`，换的只是区段与数据：测量结果区段四列（指标 / 测量值 / 参考区间 / 判读），异常标红不靠模板判断——业务侧用 `doc.Row(section, row)!.Cells[3].Color` 直接把该格文字染色。检查信息、结论、签名各占一个区段，结论是跨全宽的合并备注。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.BodyComposition;
using SkiaSharp;

BodyCompositionDocument doc = new BodyCompositionDocument { HospitalName = "测试医院" };
doc.Patient.Name = "陈雅琴";
doc.Patient.Department = "健康管理中心";
doc.SetCell(BodyCompositionSections.Info, BodyCompositionRows.Device, 1, "InBody 770 生物电阻抗分析仪");
doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.Weight, 1, "68.2");
doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.BodyFat, 1, "32.4");
doc.SetCell(BodyCompositionSections.Results, BodyCompositionRows.BodyFat, 3, "偏高 ↑");
doc.Row(BodyCompositionSections.Results, BodyCompositionRows.BodyFat)!.Cells[3].Color = SKColors.Red;
doc.Section(BodyCompositionSections.Conclusion)!.Note = "体脂率偏高、骨骼肌量偏低，呈肌少性肥胖倾向。";
using Chart chart = new Chart(doc, new BodyCompositionTemplate());
File.WriteAllBytes("body-composition.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 四肢血流探测及压力测量检查报告 · `limb-blood-flow`

![四肢血流探测及压力测量检查报告](docs/images/limb-blood-flow-96dpi.png)

**模板** `LimbBloodFlowTemplate` · **文档** `LimbBloodFlowDocument` · **样例** `demo/OODD.Charts.Demo.Common/LimbBloodFlowSample.cs`

`FormSheetTemplate` 实例。模板里没有一行只服务于这张单子，骨架全由引擎给：节段压力是六行五列（测量节段 / 左肢 / 右肢 / 左右差 / 判读），多普勒探测是四行四列（血管 / 左侧 / 右侧 / 频谱形态）。左右对照靠列而不是靠两张表，"左右差"因此能直接读出来；异常判读同样用 `Cells[4].Color` 标红。结论与签名各占一个区段。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.LimbBloodFlow;
using SkiaSharp;

LimbBloodFlowDocument doc = new LimbBloodFlowDocument { HospitalName = "测试医院" };
doc.Patient.Name = "李长海";
doc.Patient.Department = "血管外科";
doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, 1, "122");
doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, 2, "120");
doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 1, "118");
doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 2, "86");
doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 3, "32");
doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 4, "右下肢缺血");
doc.Row(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle)!.Cells[4].Color = SKColors.Red;
doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.DorsalisPedis, 2, "未探及");
using Chart chart = new Chart(doc, new LimbBloodFlowTemplate());
File.WriteAllBytes("limb-blood-flow.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 点物本 · `inventory-book`

![点物本](docs/images/inventory-book-96dpi.png)

**模板** `InventoryBookTemplate` · **文档** `InventoryBookDocument` · **样例** `demo/OODD.Charts.Demo.Common/InventoryBookSample.cs`

又一张表格引擎的单子，`FormSheetTemplate` 只认文档里的区段与列：物资区段六列（物品名称 / 基数 / 白班 / 夜班 / 差异 / 备注），差异列允许负数，负号即缺口，备注里写补货时间。第二个区段按班次记清点人、复核人与时间，第三个区段是一段跨全宽的差异说明。行键都是常量，增减物资只需在文档里加删行。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.InventoryBook;

InventoryBookDocument doc = new InventoryBookDocument { HospitalName = "测试医院" };
doc.Patient.Name = "李娜";
doc.Patient.Department = "急诊科";
doc.SetCell(InventoryBookSections.Items, InventoryBookRows.Epinephrine, 1, "5");
doc.SetCell(InventoryBookSections.Items, InventoryBookRows.Epinephrine, 2, "5");
doc.SetCell(InventoryBookSections.Items, InventoryBookRows.Epinephrine, 3, "5");
doc.SetCell(InventoryBookSections.Items, InventoryBookRows.Syringe, 3, "8");
doc.SetCell(InventoryBookSections.Items, InventoryBookRows.Syringe, 4, "-2");
doc.SetCell(InventoryBookSections.Items, InventoryBookRows.Syringe, 5, "22:10 已从库房补足 2 支");
doc.SetCell(InventoryBookSections.Shifts, InventoryBookRows.DayShift, 1, "李娜");
doc.SetCell(InventoryBookSections.Shifts, InventoryBookRows.DayShift, 3, "2026-03-20 08:10");
using Chart chart = new Chart(doc, new InventoryBookTemplate());
File.WriteAllBytes("inventory-book.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 工作量统计本 · `workload-book`

![工作量统计本](docs/images/workload-book-96dpi.png)

**模板** `WorkloadBookTemplate` · **文档** `WorkloadBookDocument` · **样例** `demo/OODD.Charts.Demo.Common/WorkloadBookSample.cs`

这张同样交给 `FormSheetTemplate`，渲染代码为零，信息都在文档里：8 个护理项目 × 7 天是一张固定列矩阵，第 8 列是行合计，最后一行的 `IsTotal` 打开后画列合计与总计。七天日期写在列头里、由文档构造时给定，`SetCell` 只改格子里的数字，合计需要业务侧自己算好再填。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.WorkloadBook;
using OODD.Charts.Model;

WorkloadBookDocument doc = new WorkloadBookDocument { HospitalName = "测试医院" };
doc.Patient.Department = "普通外科";
doc.Patient.ExtraFields.Add(new HeaderField("period", "统计周期", "2026-03-01 至 03-07"));
doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Intravenous, 1, "45");
doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Intravenous, 2, "48");
doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Intravenous, 8, "93");
doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Total, 1, "91");
doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Total, 8, "182");
doc.Section(WorkloadBookSections.Notes)!.Note = "按实际执行次数统计，与医嘱系统核对后汇总。";
using Chart chart = new Chart(doc, new WorkloadBookTemplate());
File.WriteAllBytes("workload-book.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 不良事件登记本 · `adverse-event-book`

![不良事件登记本](docs/images/adverse-event-book-96dpi.png)

**模板** `AdverseEventBookTemplate` · **文档** `AdverseEventBookDocument` · **样例** `demo/OODD.Charts.Demo.Common/AdverseEventBookSample.cs`

`FormSheetTemplate` 的又一例：引擎画页眉与列头，业务侧只管填文字：事件信息六列（编号 / 日期 / 类型 / 分级 / 涉及患者 / 报告人），事件经过是跨全宽的 `Note`，原因分析与整改五列（原因 / 措施 / 责任人 / 完成期限 / 闭环状态）。长句在"经过"和"措施"格里按行数加高，不会把相邻行挤出纸面。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.AdverseEventBook;

AdverseEventBookDocument doc = new AdverseEventBookDocument { HospitalName = "测试医院" };
doc.Patient.Name = "王秀兰";
doc.Patient.Department = "老年医学科";
doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 0, "AE-2026-017");
doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 2, "跌倒坠床");
doc.SetCell(AdverseEventBookSections.Event, AdverseEventBookRows.Event, 3, "Ⅱ级");
doc.Section(AdverseEventBookSections.Course)!.Note = "患者夜间如厕时于床旁跌倒，左膝着地，无骨折，生命体征平稳。";
doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 0, "夜间照明不足、陪护离岗");
doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 1, "加装夜灯、加强巡视");
doc.SetCell(AdverseEventBookSections.Analysis, AdverseEventBookRows.Corrective, 4, "√ 已闭环");
using Chart chart = new Chart(doc, new AdverseEventBookTemplate());
File.WriteAllBytes("adverse-event-book.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 尸体护理/遗体转运记录 · `postmortem-care`

![尸体护理/遗体转运记录](docs/images/postmortem-care-96dpi.png)

**模板** `PostmortemCareTemplate` · **文档** `PostmortemCareDocument` · **样例** `demo/OODD.Charts.Demo.Common/PostmortemCareSample.cs`

表格引擎承载的一张。区段、列宽、合并备注都写在文档构造里：死亡信息两列（项目 / 内容），护理项目三列（项目 / 完成情况 / 执行人），遗体交接四列（时间 / 接收单位 / 接收人 / 护送人）。六项护理逐条打勾（√ 已完成 / √ 已处理 / √ 已核对），签名区段落执行护士、护士长与日期。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.PostmortemCare;

PostmortemCareDocument doc = new PostmortemCareDocument { HospitalName = "测试医院" };
doc.Patient.Name = "李桂芳";
doc.Patient.Department = "肿瘤内科";
doc.SetCell(PostmortemCareSections.Death, PostmortemCareRows.DeathTime, 1, "2026-03-13 23:40");
doc.SetCell(PostmortemCareSections.Death, PostmortemCareRows.Cause, 1, "呼吸循环衰竭");
doc.SetCell(PostmortemCareSections.Care, PostmortemCareRows.Clean, 1, "√ 已完成");
doc.SetCell(PostmortemCareSections.Care, PostmortemCareRows.Clean, 2, "王芳");
doc.SetCell(PostmortemCareSections.Handover, PostmortemCareRows.Handover, 1, "太平间（后勤保障科）");
doc.SetCell(PostmortemCareSections.Handover, PostmortemCareRows.Handover, 2, "赵师傅");
using Chart chart = new Chart(doc, new PostmortemCareTemplate());
File.WriteAllBytes("postmortem-care.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 人体器官捐献过程记录表 · `organ-donation`

![人体器官捐献过程记录表](docs/images/organ-donation-96dpi.png)

**模板** `OrganDonationTemplate` · **文档** `OrganDonationDocument` · **样例** `demo/OODD.Charts.Demo.Common/OrganDonationSample.cs`

这张没有自己的模板类，直接组合 `FormSheetTemplate`：捐献者信息两列，过程事件链五行（环节 / 时间 / 责任人 / 结果 / 备注）按时间顺序排成七个环节，转运与交接是一段合并备注。时间精确到分钟、备注里写冷缺血计时，都只是 `SetCell` 的文本，不参与版面计算。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.OrganDonation;

OrganDonationDocument doc = new OrganDonationDocument { HospitalName = "测试医院" };
doc.Patient.Name = "周建军";
doc.Patient.Department = "重症医学科";
doc.SetCell(OrganDonationSections.Donor, OrganDonationRows.Disease, 1, "重型颅脑外伤后脑疝形成");
doc.SetCell(OrganDonationSections.Chain, OrganDonationRows.Consent, 1, "2026-03-21 11:00");
doc.SetCell(OrganDonationSections.Chain, OrganDonationRows.Consent, 2, "协调员 陈立");
doc.SetCell(OrganDonationSections.Chain, OrganDonationRows.Consent, 3, "同意捐献");
doc.SetCell(OrganDonationSections.Chain, OrganDonationRows.Procurement, 1, "2026-03-22 02:10");
doc.SetCell(OrganDonationSections.Chain, OrganDonationRows.Procurement, 3, "获取完成");
doc.Section(OrganDonationSections.Handover)!.Note = "肝脏经 UW 液灌注后低温转运，冷缺血时间 6 小时 20 分。";
using Chart chart = new Chart(doc, new OrganDonationTemplate());
File.WriteAllBytes("organ-donation.png", chart.Encode(ChartImageFormat.Png, 96f));
```

## 行业扩展表单

### 工程

#### 测井成果图 · `well-log`

![测井成果图](docs/images/well-log-96dpi.png)

**模板** `WellLogChartTemplate` · **文档** `WellLogDocument` · **样例** `demo/OODD.Charts.Demo.Common/EngineeringSamples.cs`

深度轴自上而下递增，多道横向并置共享同一根深度轴：自然伽马走 0–150 线性刻度，深电阻率走 1–1000 对数刻度，岩性道用 `PatternKind` 花纹分段。`PageDepth` 把 0–1000 m 切成两页，`PageIndex` 选当前页，页内道头与深度刻度重画；一次导出只含选中的那一页。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Engineering.WellLog;

WellLogDocument doc = new WellLogDocument
{
    WellId = "示例井 X-7",
    RunNumber = "2",
    DepthTop = 0d,
    DepthBottom = 1000d,
    PageDepth = 500d,
};
WellLogTrack gr = new WellLogTrack("gr", "自然伽马 (API)", 0d, 150d, false);
gr.Samples.Add(new WellLogSample(120d, 68d));
gr.Samples.Add(new WellLogSample(240d, 54d));
doc.Tracks.Add(gr);
WellLogTrack res = new WellLogTrack("res", "深电阻率 (Ω·m)", 1d, 1000d, true);
res.Samples.Add(new WellLogSample(120d, 6d));
doc.Tracks.Add(res);
using Chart chart = new Chart(doc, new WellLogChartTemplate());
File.WriteAllBytes("well-log.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 变压器油色谱（大卫三角形） · `dga`

![变压器油色谱（大卫三角形）](docs/images/dga-96dpi.png)

**模板** `DgaChartTemplate` · **文档** `DgaChartDocument` · **样例** `demo/OODD.Charts.Demo.Common/EngineeringSamples.cs`

上半部五条气体趋势轨共用按天偏移的非等距取样轴，下半部是三条反向刻度轴的大卫三角形；`TriaxialComponents` 经重心坐标映射成像素，`DgaZone` 多边形与 `DgaSample` 投点都直接由文档给出。模板本身不内置任何 DL/T 722 分区坐标，只负责把三组分落到三角形里。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Engineering.Dga;
using OODD.Charts.Geometry;

DgaChartDocument doc = new DgaChartDocument
{
    TransformerId = "示例 1# 主变",
    OilBrand = "示例 25#",
    SampleDateText = "2026-05-18",
};
DgaGasSeries h2 = new DgaGasSeries("h2", "H2", "μL/L");
h2.Samples.Add(new DgaGasSample(0d, "03-02", 2d));
h2.Samples.Add(new DgaGasSample(30d, "04-01", 10d));
doc.GasSeries.Add(h2);
doc.Samples.Add(new DgaSample("04-16", new TriaxialComponents(0.15d, 0.65d, 0.20d)));
using Chart chart = new Chart(doc, new DgaChartTemplate());
File.WriteAllBytes("dga.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 交通

#### 列车运行图 · `train-graph`

![列车运行图](docs/images/train-graph-96dpi.png)

**模板** `TrainGraphTemplate` · **文档** `TrainGraphDocument` · **样例** `demo/OODD.Charts.Demo.Common/TransitSamples.cs`

横轴是四级刻度的时分标尺（1/5/10/60 分钟），纵轴站名线按区间运行时分加权铺满绘图区；运行线由站界点之间的解析斜线与停站水平段组成。天窗是时刻区间与站区间双约束的 `SpanBand`，越出站区间自动裁切。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Transit.TrainGraph;

TrainGraphDocument doc = new TrainGraphDocument
{
    FormTitle = "列车运行图",
    LineName = "示例线 SB 线",
    StartClockMinutes = 360d,
    Minutes = 240d,
};
doc.Stations.Add(new TrainStation("A", "甲站", 52d));
doc.Stations.Add(new TrainStation("B", "乙站", 46d));
doc.Trains.Add(new TrainRun("G101", true).Stop(0, 0d, 0d).Stop(1, 52d, 56d));
doc.Windows.Add(new TrainWindow("W1", "天窗 100–140", 100d, 140d, 0, 1));
using Chart chart = new Chart(doc, new TrainGraphTemplate());
File.WriteAllBytes("train-graph.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 飞机载重平衡与重心包线 · `weight-balance`

![飞机载重平衡与重心包线](docs/images/weight-balance-96dpi.png)

**模板** `LoadBalanceTemplate` · **文档** `LoadBalanceDocument` · **样例** `demo/OODD.Charts.Demo.Common/TransitSamples.cs`

横轴重心 %MAC、纵轴重量 kg，包线是数据坐标下的 `EnvelopePolygon`，包线外铺斜纹、内留白，`IsInside` 直接做超限判定。载重表每一列的中心与图上同一重心值共用一根 `NumericAxis`，表图同源，不需要二次换算。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Transit.LoadBalance;
using SkiaSharp;

LoadBalanceDocument doc = new LoadBalanceDocument
{
    AircraftId = "示例机型 SB-01", EmptyWeight = 42000d, EmptyCgPercentMac = 24d,
    FuelKg = 8000d, FuelArm = 26d, CgMin = 15d, CgMax = 40d,
    WeightMin = 40000d, WeightMax = 85000d,
};
doc.Stations.Add(new LoadStation("fwd", "前舱", 1d, 18d, 6000d));
doc.Envelopes.Add(new CgEnvelope("env", "示例包线", new[]
{
    new SKPoint(17f, 40000f), new SKPoint(20f, 74000f), new SKPoint(33f, 40000f),
}));
doc.CheckPoints.Add(new CgCheckPoint("takeoff", "起飞重心",
    doc.CgPercentMacOf(), doc.TotalWeightKg));
using Chart chart = new Chart(doc, new LoadBalanceTemplate());
File.WriteAllBytes("weight-balance.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 集装箱积载图 · `bay-plan`

![集装箱积载图](docs/images/bay-plan-96dpi.png)

**模板** `StowageTemplate` · **文档** `StowageDocument` · **样例** `demo/OODD.Charts.Demo.Common/TransitSamples.cs`

左视图按 bay × tier 画纵向剖面，右视图按 row × tier 画横剖面，40 尺箱左对齐跨两个 20 尺 bay 位。tier 自底向上递增、row 自舷中向两舷非等距展开，箱内文字走 `Painter.Text`，导出 SVG 后仍可检索。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Transit.Stowage;

StowageDocument doc = new StowageDocument
{
    FormTitle = "集装箱积载图",
    VesselId = "示例船 SB-01",
    VoyageId = "SB001E",
    CrossSectionBay = 3,
};
doc.Bays.Add(new StowageBay(1, 2, 2, 6));
doc.Bays.Add(new StowageBay(3, 2, 2, 6));
doc.Bays.Add(new StowageBay(5, 2, 2, 6));
doc.Containers.Add(new StowageContainer(1, 1, 1, ContainerSize.Twenty, 12000d, "SHA"));
doc.Containers.Add(new StowageContainer(3, 1, 2, ContainerSize.Forty, 24500d, "LAX"));
using Chart chart = new Chart(doc, new StowageTemplate());
File.WriteAllBytes("bay-plan.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 危险货物道路运输联单 · `dg-consignment`

![危险货物道路运输联单](docs/images/dg-consignment-96dpi.png)

**模板** `DangerousGoodsTemplate` · **文档** `DangerousGoodsDocument` · **样例** `demo/OODD.Charts.Demo.Common/TransitSamples.cs`

菱形标志牌由 Core 的 `DiamondPlacard` 给出几何，类项号直立写在下半槽，上半槽的 UN 象形图只能经 `SymbolPath` 注入——库不内置、不重绘官方图形。`AddStandardCopies()` 生成三联（托运人存根 / 承运人联 / 收货人联），横向条排，一页内 3 联且联宽之和等于联单区宽。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Transit.DangerousGoods;

DangerousGoodsDocument doc = new DangerousGoodsDocument
{
    FormTitle = "危险货物道路运输联单",
    UnNumber = "1263",
    ProperShippingName = "涂料（易燃）",
    Class = "3",
    PackingGroup = "II",
    Consignor = "示例托运单位",
    Carrier = "示例承运单位",
};
doc.AddStandardCopies();
using Chart chart = new Chart(doc, new DangerousGoodsTemplate());
File.WriteAllBytes("dg-consignment.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 超限运输许可附图 · `ogami-profile`

![超限运输许可附图](docs/images/ogami-profile-96dpi.png)

**模板** `OversizeLoadTemplate` · **文档** `OversizeLoadDocument` · **样例** `demo/OODD.Charts.Demo.Common/TransitSamples.cs`

车货外廓是一条闭合折线，轴位之间的链式尺寸由 `DimensionRun` 生成：段数 = 轴数 + 1，各段量值由像素坐标回写，模板不填数字。总高尺寸与关键高度坐标式共用同一套延伸线规则，轴荷条高与轴荷吨位成比例。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Transit.OversizeLoad;

OversizeLoadDocument doc = new OversizeLoadDocument
{
    FormTitle = "超限运输许可附图",
    PermitNumber = "示例-SB-2026-001",
    VehiclePlate = "示例·SB0001",
    LengthMeters = 18d,
    HeightMeters = 3.5d,
    WidthMeters = 3.2d,
};
doc.Profile.Add(new OversizeProfilePoint(0d, 0d));
doc.Profile.Add(new OversizeProfilePoint(0d, 3.5d));
doc.Profile.Add(new OversizeProfilePoint(18d, 3.5d));
doc.Profile.Add(new OversizeProfilePoint(18d, 0d));
doc.Profile.Add(new OversizeProfilePoint(0d, 0d));
doc.Axles.Add(new OversizeAxle(2.7d, 6.0d));
using Chart chart = new Chart(doc, new OversizeLoadTemplate());
File.WriteAllBytes("ogami-profile.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 地学

#### 地面天气图填图 · `surface-chart`

![地面天气图填图](docs/images/surface-chart-96dpi.png)

**模板** `SurfaceChartTemplate` · **文档** `SurfacePlotDocument` · **样例** `demo/OODD.Charts.Demo.Common/GeoscienceSamples.cs`

每个层次一个面板，站圈居中：左上气温、右上露点、右下海压、左下风杆，风杆羽旗全部是几何并绕站圈旋转，符号内不落文字，SVG 里的站旁数字仍是可检索文本。`StackPressureLevels` 打开后按层次数纵向等分堆叠，同一模板兼顾地面图与高空等压面图。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Geoscience.Surface;

SurfacePlotDocument doc = new SurfacePlotDocument
{
    FormTitle = "地面天气图填图",
    ValidTimeText = "2026-03-01 08 时",
};
SurfaceLevelPlot level = doc.Levels[0];
level.Stations.Add(new SurfaceStation("哈尔滨 50953", 0.16d, 0.20d)
{
    TemperatureC = -13.6d,
    DewPointC = -19.2d,
    PressureHpa = 1028.6d,
    WindDirectionDegrees = 0d,
    WindSpeedMps = 9d,
    CloudOktas = 7,
});
using Chart chart = new Chart(doc, new SurfaceChartTemplate());
File.WriteAllBytes("surface-chart.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### T-lnP 温度对数压力图 · `tlnp`

![T-lnP 温度对数压力图](docs/images/tlnp-96dpi.png)

**模板** `TlnpTemplate` · **文档** `SoundingDocument` · **样例** `demo/OODD.Charts.Demo.Common/GeoscienceSamples.cs`

纵轴是对数气压，等压线严格等距；横轴 x = T + 倾斜量·log10(P₀/p)，等温线在 (x, log p) 域里是直线。等压线、等温线、干/湿绝热线与混合比线五族全部由 `LineFamilyGenerator` 采样，T 与 td 两条曲线、特征层标注和单层斜线填图都挂在同一份 `SoundingDocument` 上。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Geoscience.Tlnp;

SoundingDocument doc = new SoundingDocument
{
    StationName = "示例探空站 54511",
    ValidTimeText = "2026-03-01 08 时",
    HatchedLevelIndex = 1,
};
doc.Levels.Add(new SoundingLevel(1000d, 22.4d, 15.1d));
doc.Levels.Add(new SoundingLevel(850d, 11.8d, 6.4d));
doc.Levels.Add(new SoundingLevel(500d, -9.8d, -21.0d));
doc.CharacteristicLevels.Add(new SoundingCharacteristicLevel(850d, "湿层顶") { PreferLeft = true });
using Chart chart = new Chart(doc, new TlnpTemplate());
File.WriteAllBytes("tlnp.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 医疗（新增）

#### 麻醉记录单 · `anesthesia-record`

![麻醉记录单](docs/images/anesthesia-record-96dpi.png)

**模板** `AnesthesiaRecordTemplate` · **文档** `AnesthesiaRecordDocument` · **样例** `demo/OODD.Charts.Demo.Common/NewMedicalSamples.cs`

左轴（脉搏/血压 20–260，步长 20）与右轴（体温 12–40，步长 2）共用同一段像素区间，横线只由左轴枚举一次，标签列分列网格左右。血压成对画收缩/舒张两条曲线，持续用药是 `SpanBand` 时段带，事件用一对端点符号落在时间轴上。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms.AnesthesiaRecord;

AnesthesiaRecordDocument doc = new AnesthesiaRecordDocument
{
    HospitalName = "示例医院",
    FormTitle = "麻醉记录单",
    TotalMinutes = 150,
};
doc.Patient.Name = "示例患者甲";
doc.Patient.AdmissionNumber = "2026090101";
doc.Operation.Procedure = "示例术式";
doc.Operation.AnesthesiaType = "全身麻醉";
doc.Points.Add(new AnesthesiaPoint(0d, 128d, 76d, 93d, 82d, 36.6d, 98d, 34d));
doc.Points.Add(new AnesthesiaPoint(60d, 124d, 74d, 91d, 80d, 36.7d, 98d, 36d));
doc.ContinuousMedications.Add(new ContinuousMedication("示例镇静药", 5d, 140d, "6 mg/kg/h"));
doc.Events.Add(new AnesthesiaEvent(10d, 30d, "BP", 1, 0));
using Chart chart = new Chart(doc, new AnesthesiaRecordTemplate());
File.WriteAllBytes("anesthesia-record.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 居民死亡医学证明（四联） · `death-certificate`

![居民死亡医学证明（四联）](docs/images/death-certificate-96dpi.png)

**模板** `DeathCertificateTemplate` · **文档** `DeathCertificateDocument` · **样例** `demo/OODD.Charts.Demo.Common/NewMedicalSamples.cs`

四联由 `CollateSpec` 横向切分，一页内 4 联、联宽之和等于版面宽；每联只画自己可见的字段，行政区划代码用 6 格 `DigitBox`、编号用 17 格。死因链 I.(a)–(d) 按固定行序落位，字段隐藏时保留空行，四联在同一 y 上可直接做像素对照。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms.DeathCertificate;

DeathCertificateDocument doc = new DeathCertificateDocument
{
    HospitalName = "示例医院",
    FormTitle = "居民死亡医学证明（四联）",
    AdminCode = "000000",
    SerialNumber = "20260000000000001",
};
doc.Patient.Name = "示例患者乙";
doc.Patient.AdmissionNumber = "000000194001010000";
doc.Set(DeathCertificateFields.DeathDate, "2026-09-30");
doc.Set(DeathCertificateFields.DeathPlace, "示例市示例区示例医院");
doc.Set(DeathCertificateFields.CauseA, "示例直接死因");
doc.Set(DeathCertificateFields.CauseB, "示例基础疾病");
using Chart chart = new Chart(doc, new DeathCertificateTemplate());
File.WriteAllBytes("death-certificate.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### ICU 护理记录单 · `icu-nursing`

![ICU 护理记录单](docs/images/icu-nursing-96dpi.png)

**模板** `IcuNursingTemplate` · **文档** `IcuNursingDocument` · **样例** `demo/OODD.Charts.Demo.Common/NewMedicalSamples.cs`

表头是根 / 组 / 叶三级分组树，9 个叶子列由 `HeaderTableLayout` 落到像素；“中心静脉压”是划对角斜线的叶子，只画线不落字。右侧交班小结挂一张行距独立的 `SideAttachedTable`，主表行高变化不影响侧表。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms.IcuNursing;

IcuNursingDocument doc = new IcuNursingDocument
{
    HospitalName = "示例医院",
    FormTitle = "ICU 护理记录单",
};
doc.Patient.Name = "示例患者丙";
doc.Patient.AdmissionNumber = "2026090203";
IcuRecord record = new IcuRecord("06:00");
record.Set(IcuNursingColumns.All[0], "88");
record.Set(IcuNursingColumns.All[1], "18");
doc.Records.Add(record);
doc.HandoverNotes.Add("白班：平稳");
using Chart chart = new Chart(doc, new IcuNursingTemplate());
File.WriteAllBytes("icu-nursing.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 预防接种证 · `vaccination-booklet`

![预防接种证](docs/images/vaccination-booklet-96dpi.png)

**模板** `VaccinationBookletTemplate` · **文档** `VaccinationBookletDocument` · **样例** `demo/OODD.Charts.Demo.Common/NewMedicalSamples.cs`

12 个逻辑页按鞍式装订配对，`SpreadIndex = 2` 取到 (10,3) 这一开面；记录表列头只在记录首页画一次，跨开面不重复。一次导出就是一张开面（两个逻辑页），PDF 也只有 1 页，翻页由 `SpreadIndex` 切换。专色以近似 RGB 呈现，原始 CMYK 值留在文档字段里。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms.VaccinationBooklet;

VaccinationBookletDocument doc = new VaccinationBookletDocument
{
    HospitalName = "示例社区卫生服务中心",
    FormTitle = "预防接种证",
    TotalLogicalPages = 12,
    SpreadIndex = 2,
};
doc.Patient.Name = "示例儿童";
doc.Patient.AdmissionNumber = "示例建档号 0001";
doc.Records.Add(new VaccinationRecord("乙肝疫苗", "1", "2022-03-04", "示例生物", "20220301"));
doc.Assessments.Add("已完成示例剂次接种，留观无异常。");
using Chart chart = new Chart(doc, new VaccinationBookletTemplate());
File.WriteAllBytes("vaccination-booklet.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 药敏报告单 · `ast-report`

![药敏报告单](docs/images/ast-report-96dpi.png)

**模板** `AstReportTemplate` · **文档** `AstReportDocument` · **样例** `demo/OODD.Charts.Demo.Common/NewMedicalSamples.cs`

判读条 `RangeBar` 直接由 `WeightedCategoryAxis` 承载，S / SDD / I / R 四段宽度由权重令牌决定（SDD 是 S 的 1.5 倍），没有新增轴类。药名、MIC 与判读类别逐行落格，行数与药物条目数同步增长。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms.AstReport;

AstReportDocument doc = new AstReportDocument
{
    HospitalName = "示例医院",
    FormTitle = "药敏报告单",
    Specimen = "痰",
    Organism = "示例肠杆菌",
};
doc.Patient.Name = "示例患者丁";
doc.Patient.AdmissionNumber = "2026091501";
doc.Antibiotics.Add(new AstAntibiotic("示例培南类", "1", "R"));
doc.Antibiotics.Add(new AstAntibiotic("示例青霉素类", "8", "S"));
doc.Antibiotics.Add(new AstAntibiotic("示例头孢类", "2", "SDD"));
doc.Antibiotics.Add(new AstAntibiotic("示例喹诺酮类", "4", "I"));
using Chart chart = new Chart(doc, new AstReportTemplate());
File.WriteAllBytes("ast-report.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 票据凭证

#### 电气第一种工作票（三联） · `electrical-work-ticket`

![电气第一种工作票（三联）](docs/images/electrical-work-ticket-96dpi.png)

**模板** `ElectricalWorkTicketTemplate` · **文档** `ElectricalWorkTicketDocument` · **样例** `demo/OODD.Charts.Demo.Common/TicketSamples.cs`

三联纵向叠排（存根 / 工作 / 许可），一页内 3 联，每联宽等于版面宽、联高之和等于版面高；前九项基本栏位三联共有，工作联加终结时间，许可联加许可开始时间与接地线组数。差异不是画完再遮：每联只落自己可见的字段，其余行留空但保持同一行网格。措施行的勾 / 叉 / 空框由 `TriState` 三态几何绘制，已执行章在每联各落一枚红章。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Render;
using OODD.Charts.Tickets.WorkTicket;

ElectricalWorkTicketDocument doc = new ElectricalWorkTicketDocument
{
    UnitName = "示例供电段",
    SerialNo = "DQ-2026-0001",
    Stamp = WorkTicketStampState.Executed,
};
doc.With(ElectricalWorkTicketFields.WorkLeader, "张三");
doc.With(ElectricalWorkTicketFields.WorkPlace, "10kV 示例一线 03 号杆");
doc.Measures.Add(new TicketMeasureRow("断开电源并验电", TriState.Checked));
doc.Measures.Add(new TicketMeasureRow("装设接地线并悬挂标示牌", TriState.Checked));
doc.Measures.Add(new TicketMeasureRow("邻近带电体加装绝缘遮蔽", TriState.NotApplicable));
using Chart chart = new Chart(doc, new ElectricalWorkTicketTemplate());
File.WriteAllBytes("electrical-work-ticket.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 特殊作业票 · `hot-work-ticket`

![特殊作业票](docs/images/hot-work-ticket-96dpi.png)

**模板** `HotWorkTicketTemplate` · **文档** `HotWorkTicketDocument` · **样例** `demo/OODD.Charts.Demo.Common/TicketSamples.cs`

两联纵向叠排（作业 / 监护），一页内 2 联，每联宽等于版面宽、联高之和等于版面高；9 个共有字段之外，气体检测只在作业联、审批人只在监护联落墨。危害辨识是 `AppendableRowGroup` 副表，条目数直接进入整页高度，改条目数就改版面而不是绘制期撑开。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Render;
using OODD.Charts.Tickets.WorkTicket;

HotWorkTicketDocument doc = new HotWorkTicketDocument
{
    Category = HotWorkCategory.HotWork,
    UnitName = "示例化工厂",
    SerialNo = "TS-2026-0007",
    Stamp = WorkTicketStampState.Executed,
};
doc.With(HotWorkTicketFields.WorkPlace, "二号罐区东侧管廊");
doc.With(HotWorkTicketFields.GasTest, "O2 20.9%，可燃气 0%LEL");
doc.Measures.Add(new TicketMeasureRow("动火点周边可燃物已清除", TriState.Checked));
doc.Measures.Add(new TicketMeasureRow("消防器材已就位", TriState.Checked));
doc.Hazards.Add("火花引燃周边可燃物");
doc.Hazards.Add("气瓶间距不足");
using Chart chart = new Chart(doc, new HotWorkTicketTemplate());
File.WriteAllBytes("hot-work-ticket.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 增值税专用发票（四联） · `vat-invoice`

![增值税专用发票（四联）](docs/images/vat-invoice-96dpi.png)

**模板** `VatInvoiceTemplate` · **文档** `VatInvoiceDocument` · **样例** `demo/OODD.Charts.Demo.Common/TicketSamples.cs`

四联纵向叠排（存根 / 记账 / 抵扣 / 发票），一页内 4 联，每联宽等于版面宽、联高之和等于版面高；每联只显示自己的附加字段（存根编号 / 记账凭证号 / 抵扣认证期 / 报销人）。发票号码是 8 格 `DigitBox`，密码区 4 行 × 21 格，30 位密文逐格落字、其余格留白。价税合计大写用 `PressedAmount` 压进固定单元格，字号自动收缩且不压到相邻的小写格。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Tickets.Vat;

VatInvoiceDocument doc = new VatInvoiceDocument
{
    CopyCount = 4,
    InvoiceCode = "000000000000",
    InvoiceNumber = "01234567",
    InvoiceDate = new DateTime(2026, 10, 30),
    CipherText = "0123456789ABCDEFGHIJKLMNOPQRST",
    BuyerName = "示例购买方有限公司",
    SellerName = "示例销售方有限公司",
};
doc.Items.Add(new VatInvoiceItem
{
    Name = "技术服务费", Unit = "次", Quantity = 2d, UnitPrice = 1500.50d, TaxRate = 0.06d,
});
using Chart chart = new Chart(doc, new VatInvoiceTemplate());
File.WriteAllBytes("vat-invoice.png", chart.Encode(ChartImageFormat.Png, 96f));
```

## 雷达、引擎与宿主控件

雷达态势图走极坐标版面，两套通用表单引擎负责把“时刻 + 指标”和“任意表格”收敛成模板。它们和医疗单据共用同一个 `Chart` 门面，导出与宿主接入不需要另写一套代码。

### 雷达态势图（360° 全向） · `radar-full`

![雷达态势图（360° 全向）](docs/images/radar-full-96dpi.png)

**模板** `RadarChartTemplate` · **文档** `RadarDocument` · **样例** `demo/OODD.Charts.Demo.Common/RadarSample.cs`

这张图麻烦的地方在于它不是直角坐标：方位 0° 在正上方、顺时针增加，距离从圆心往外量。模板只认 `RadarDocument`，盘面、罗盘和信息面板由 `RadarLayout` 一次算完；改过 `MaxRange`、`RingCount` 或目标之后要 `chart.Invalidate()`，否则下一帧还在用旧的几何。扫描扇由 `SweepAngle` 和 `SweepBeamWidth` 决定，这里指向 045°、拖尾 30°；轨迹按 `TrailLength` 截断，信息面板默认竖排在右侧。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Radar;

RadarDocument doc = new RadarDocument
{
    FormTitle = "雷达态势图（360° 全向）",
    Scope = RadarScope.Full,
    MaxRange = 50d,
    RangeUnit = "km",
    SpeedUnit = "kn",
    RingCount = 5,
    SweepAngle = 45d,
    SweepBeamWidth = 30d,
    SelectedTargetId = "T2",
};
RadarTarget target = new RadarTarget("T2")
{
    Label = "远海 7",
    Bearing = 118d,
    Distance = 32d,
    Speed = 12.6d,
    Course = 128d,
};
target.Track.Add(new RadarTrackPoint(106d, 27.4d));
target.Track.Add(new RadarTrackPoint(112d, 29.7d));
target.Track.Add(new RadarTrackPoint(118d, 32d));
target.Extra["type"] = "货轮";
doc.Targets.Add(target);
doc.InfoPanel.Position = RadarPanelPosition.Right;
doc.InfoPanel.Fields.Add(new RadarInfoField("status"));
doc.InfoPanel.Fields.Add(new RadarInfoField("extra.type", "类型"));
using Chart chart = new Chart(doc, new RadarChartTemplate());
File.WriteAllBytes("radar-full.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 雷达态势图（180° 扇区） · `radar-half`

![雷达态势图（180° 扇区）](docs/images/radar-half-96dpi.png)

**模板** `RadarChartTemplate` · **文档** `RadarDocument` · **样例** `demo/OODD.Charts.Demo.Common/RadarSample.cs`

180° 不是把 360° 裁一半，而是换 `Scope = RadarScope.Half` 再给 `HalfStartBearing`：默认 270 表示从 270° 顺时针扫到 90°，覆盖正上方。盘面角度范围就用这两个属性表达，跨度固定 180°，没有单独的扇区跨度属性；能调的是起始方位、量程和环数。扫描线仍是 `SweepAngle`，它可以落在扇区外——那只是光束方向，不裁盘面。信息面板改到底部横排，每个目标占一行。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Radar;

RadarDocument doc = new RadarDocument
{
    FormTitle = "雷达态势图（180° 扇区）",
    Scope = RadarScope.Half,
    HalfStartBearing = 270d,
    MaxRange = 24d,
    RangeUnit = "nmi",
    RingCount = 4,
    SweepAngle = 310d,
    SweepBeamWidth = 34d,
    TrailLength = 8,
};
doc.Targets.Add(new RadarTarget("B2")
{
    Label = "油轮 昆仑",
    Bearing = 12d,
    Distance = 12.2d,
    Speed = 9.4d,
    Course = 20d,
});
doc.InfoPanel.Position = RadarPanelPosition.Bottom;
using Chart chart = new Chart(doc, new RadarChartTemplate());
File.WriteAllBytes("radar-half.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 雷达交互与扫描动画

交互全在 `RadarInteraction` 里，宿主控件只负责转发指针和跑定时器。`TryCreate` 对非雷达表单返回 null，所以直接赋给 `Interaction` 就行，不必判型。`ChartInteractionHost` 负责把视图坐标换算成 96dpi 逻辑坐标，并用 `Stopwatch` 计时；定时器每帧只调它的 `Tick()`，再由它把真实耗时交给 `RadarInteraction.Tick(elapsed)`。外部想联动目标列表，可以订阅 `SelectionChanged`，或者直接调 `Select(id)`。

```csharp
using System;
using OODD.Charts;
using OODD.Charts.Adapters.WinForms;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

RadarDocument doc = new RadarDocument { MaxRange = 50d };
doc.Targets.Add(new RadarTarget("T1") { Label = "渔政 3102", Bearing = 32d, Distance = 18.5d });
using Chart chart = new Chart(doc, new RadarChartTemplate());

ChartControl view = new ChartControl
{
    Chart = chart,
    Fit = ChartFit.Fit,
    Dock = System.Windows.Forms.DockStyle.Fill,
};
view.Interaction = RadarInteraction.TryCreate(chart);
```

| 操作 | 行为 |
|---|---|
| 点选目标 | 命中半径内（`HitTolerance`，默认 12 逻辑像素）写 `SelectedTargetId`，盘面画选择环，面板对应块高亮 |
| 悬停 | 写 `HoverTargetId`，画一圈细环并把光标换手型；指针离开控件即清空 |
| 点盘内空白 | 选中被清空；`Fit` 模式的信箱留白不算盘内，不会误清 |
| 版面外 | 命中检测直接跳过：按下不改选中，移出会清掉悬停 |
| 扫描动画 | `Tick(elapsed)` 按调用方给的真实毫秒推进，默认 60°/s；`IsAnimating = false` 只停扫描，选中与悬停照常 |

### 两套通用表单引擎

两套引擎都是具体类，不用先写子类：`new TimelineFormTemplate()` 和 `new FormSheetTemplate()` 直接能跑。它们各自把文档 `Cast` 成 `TimelineFormDocument` / `FormSheetDocument`，类型不对会抛异常，所以别把三测单文档塞进来。

#### 时刻 + 一个主指标（`TimelineFormTemplate`）

**模板** `TimelineFormTemplate` · **文档** `TimelineFormDocument` · **样例** `demo/OODD.Charts.Demo.Common/CodeBlueSample.cs`

横轴是 `TimeLabels` 里的时刻，每个 `ChartGroup` 是一组（一天或一个阶段）；纵轴按 `MinorStep` 切格，`MajorStep` 只影响粗线。主指标用 `PointSeries`，附属行先用 `TimelineRow` 声明，再按槽号往 `CellValue` 里填字。下面是一张虚构的“术中生命体征记录”，没有定义任何新类。

```csharp
using System;
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;
using SkiaSharp;

TimelineFormDocument doc = new TimelineFormDocument
{
    HospitalName = "示例医院",
    FormTitle = "术中生命体征记录",
    AxisLabel = "心率（次/分）",
    AxisMin = 40d, AxisMax = 160d, MinorStep = 20d, MajorStep = 40d,
};
doc.Patient.Name = "示例患者";
foreach (string time in new[] { "08:00", "08:15", "08:30", "08:45", "09:00" }) doc.TimeLabels.Add(time);
doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 18)));
doc.Rows.Add(new TimelineRow("event", "术中事件"));
PointSeries hr = new PointSeries("hr", TimelineFormDocument.DefaultAxisKey)
{
    Symbol = MarkerSymbol.Dot, Color = SKColors.Red, LegendText = "心率",
};
hr.Values[0] = 82d; hr.Values[2] = 96d; hr.Values[4] = 78d;
doc.Series.Add(hr);
doc.Events.Add(new EventMark(2, "追加局麻"));
doc.Cells.Add(new CellValue("event", 2, "追加局麻 5mL"));
using Chart chart = new Chart(doc, new TimelineFormTemplate());
File.WriteAllBytes("timeline-form.png", chart.Encode(ChartImageFormat.Png, 96f));
```

#### 纯表格（`FormSheetTemplate`）

**模板** `FormSheetTemplate` · **文档** `FormSheetDocument` · **样例** `demo/OODD.Charts.Demo.Common/CancerPsychologyConsultSample.cs`

表格这张没有坐标轴，只有区段：一个 `FormSection` 带列定义、若干 `FormRow` 和可选的整段 `Note`。列宽是权重，不是像素；`Wrap` 打开后长文本按列宽换行，行高跟着长。`AddRow` 会把单元格自动补齐到列数，`SetCell` 按区段和行 `Key` 回填，不需要碰版面。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Forms;

FormSheetDocument doc = new FormSheetDocument
{
    HospitalName = "示例医院",
    FormTitle = "设备交接班记录",
    FooterNote = "交接双方签字：____________",
};
doc.Patient.Name = "示例患者";
FormSection section = new FormSection("交接项目",
    new FormColumn("项目", 1.2f, CellAlign.Near),
    new FormColumn("班次", 1f),
    new FormColumn("备注", 3f, CellAlign.Near));
section.Key = "handover";
doc.AddSection(section);
doc.AddRow("handover", "monitor", "监护仪", "白班", "运行正常");
doc.AddRow("handover", "pump", "输液泵", "夜班", "电池待换");
doc.SetCell("handover", "pump", 2, "电池已换");
using Chart chart = new Chart(doc, new FormSheetTemplate());
File.WriteAllBytes("form-sheet.png", chart.Encode(ChartImageFormat.Png, 96f));
```

### 七个宿主控件

七个平台各有一个入口控件，形状一致：`Chart`、`Fit`、`ScaleFactor`、`Dpi`，外加 `Interaction`。WinForms、WPF、Avalonia 在本机做到了与 `Chart.Encode(Png)` 逐字节相同；Eto、WinUI、Uno、MAUI 当前只在编译层面验证，像素桥和 API 契约同源，但没有运行时证据。下面的片段都从同一个 `chart` 继续。

```csharp
using OODD.Charts;
using OODD.Charts.Radar;

RadarDocument doc = new RadarDocument { MaxRange = 50d };
doc.Targets.Add(new RadarTarget("T1") { Label = "渔政 3102", Bearing = 32d, Distance = 18.5d });
using Chart chart = new Chart(doc, new RadarChartTemplate());
```

| 平台 | 入口 | 本机验证 |
|---|---|---|
| WinForms | `ChartControl` / `chart.ToBitmap` | 像素级 0 差异（对基准 PNG） |
| WPF | `ChartControl` / `chart.ToWriteableBitmap` | 像素级 0 差异 |
| Avalonia | `ChartControl` / `chart.ToWriteableBitmap` | 像素级 0 差异（headless + Skia） |
| Eto.Forms | `ChartControl` / `chart.ToEtoBitmap` | 当前仅在编译层面验证 |
| WinUI 3 | `ChartControl` / `chart.ToWriteableBitmap` | 当前仅在编译层面验证 |
| Uno Platform | `ChartControl` / `chart.ToWriteableBitmap` | 当前仅在编译层面验证 |
| MAUI | `ChartImage` / `ChartCanvasView` / `chart.ToImageSource` | 当前仅在编译层面验证 |

WinForms 还额外用真实控件句柄跑过指针消息（悬停、点选、点空白取消、动画定时器启停），其余平台共用同一套 `ChartInteractionHost`，当前只有编译证据。

#### WinForms

控件自己双缓冲，并按控件 DPI 出帧；`ToBitmap` 把同一份 BGRA 像素贴进 GDI+ 位图，本机与基准 PNG 0 差异。

```csharp
using System.Drawing;
using System.Windows.Forms;
using OODD.Charts.Adapters.WinForms;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartControl view = new ChartControl
{
    Chart = chart,
    Fit = ChartFit.Fit,
    Dock = DockStyle.Fill,
};
view.Interaction = RadarInteraction.TryCreate(chart);
using Bitmap bitmap = chart.ToBitmap(96f);
bitmap.Save("winforms.png", System.Drawing.Imaging.ImageFormat.Png);
```

#### WPF

`ToWriteableBitmap` 直接把 BGRA 帧写进 `WriteableBitmap` 并冻结，创建后可以跨线程；创建这一步必须在 UI 线程。

```csharp
using System.Windows.Media.Imaging;
using OODD.Charts.Adapters.Wpf;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartControl view = new ChartControl { Chart = chart, Fit = ChartFit.Fit, Dpi = 0d };
view.Interaction = RadarInteraction.TryCreate(chart);
WriteableBitmap bitmap = chart.ToWriteableBitmap(96d);
```

#### Avalonia

契约与 WPF 相同：`ToWriteableBitmap` 给的是 Avalonia 的 `WriteableBitmap`，控件自己处理指针事件。本机在 headless + Skia 下做过像素比对。

```csharp
using Avalonia.Media.Imaging;
using OODD.Charts.Adapters.Avalonia;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartControl view = new ChartControl { Chart = chart, Fit = ChartFit.Fit };
view.Interaction = RadarInteraction.TryCreate(chart);
WriteableBitmap bitmap = chart.ToWriteableBitmap(96d);
```

#### Eto.Forms

`ToEtoBitmap` 直接在 `Bitmap.Lock()` 的原生缓冲上按行拷贝，`ScanWidth` 行填充和 `Flipped` 行序都已处理。Eto 要求平台已初始化，位图要在 `Application` 启动之后创建。

```csharp
using Eto.Drawing;
using OODD.Charts.Adapters.Eto;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartControl view = new ChartControl { Chart = chart, Fit = ChartFit.Fit };
view.Interaction = RadarInteraction.TryCreate(chart);
Bitmap bitmap = chart.ToEtoBitmap(96d);
```

#### WinUI 3

`WriteableBitmap` 是 STA 亲和的 COM 对象，可以在后台线程先光栅化，再回 UI 线程建位图。

```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using OODD.Charts.Adapters.WinUi;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartControl view = new ChartControl { Chart = chart, Fit = ChartFit.Fit };
view.Interaction = RadarInteraction.TryCreate(chart);
WriteableBitmap bitmap = chart.ToWriteableBitmap(192d);
Image host = new Image { Source = bitmap, Width = 960d };
```

#### Uno Platform

与 WinUI 3 同一套 `WriteableBitmap` 契约，Uno 5.x 的命名空间同样是 `Microsoft.UI.Xaml`。

```csharp
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using OODD.Charts.Adapters.Uno;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartControl view = new ChartControl { Chart = chart, Fit = ChartFit.Fit };
view.Interaction = RadarInteraction.TryCreate(chart);
WriteableBitmap bitmap = chart.ToWriteableBitmap(192d);
Image host = new Image { Source = bitmap };
```

#### MAUI

`ChartImage` 是 `Image` 的子类，改 `Chart`、`Dpi` 或 `Fit` 会自己重出图；`ToImageSource` 走 PNG 字节，是 MAUI 里能接裸像素的唯一公开路径。要交互就用 `ChartCanvasView`，它直接画矢量，并且带 `Interaction`。

```csharp
using Microsoft.Maui.Controls;
using OODD.Charts.Adapters.Maui;
using OODD.Charts.Radar;
using OODD.Charts.Rendering;

ChartImage still = new ChartImage { Chart = chart, Dpi = 192f, Fit = ChartFit.Fit };
Image image = new Image { Source = chart.ToImageSource(96f) };

ChartCanvasView live = new ChartCanvasView { Chart = chart, Fit = ChartFit.Fit };
live.Interaction = RadarInteraction.TryCreate(chart);
```

### 导出与字体

导出入口都在 `Chart` 上：PNG/JPEG 是位图，PDF/SVG 保持矢量。DPI 只作用在 `canvas.Scale`，几何始终按 96dpi 求解一次，所以 300dpi 的格线和 96dpi 完全重合，不用为打印另调版面。`PdfOptions` 自带 `Auto`、`A4Portrait`、`A4Landscape`、`A5Portrait` 四个预置，也可以自己设页宽、页高、边距和缩放策略。

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Fonts;
using OODD.Charts.Radar;
using OODD.Charts.Style;

RadarDocument doc = new RadarDocument { MaxRange = 50d };
doc.Targets.Add(new RadarTarget("T1") { Bearing = 32d, Distance = 18.5d });
ChartStyle style = new ChartStyle();
style.Text.Family = "Noto Sans SC";
style.Text.HospitalBold = false;
style.Text.TitleBold = false;
FontCollection embedded = new FontCollection();
embedded.Add(File.OpenRead("NotoSansSC-Regular.otf"));
using Chart chart = new Chart(doc, new RadarChartTemplate(), style);
chart.Fonts = new SystemFontResolver(embedded);
File.WriteAllBytes("radar.png", chart.Encode(ChartImageFormat.Png, 96f));
File.WriteAllBytes("radar.jpg", chart.Encode(ChartImageFormat.Jpeg, 300f, 92));
PdfOptions options = new PdfOptions
{
    PageWidthPt = 595.28f,
    PageHeightPt = 841.89f,
    MarginPt = 12f,
    ShrinkToFit = true,
    ExpandToFitWidth = false,
    Title = "雷达态势图",
    Author = "示例医院",
};
using (FileStream pdf = File.Create("radar.pdf")) chart.WritePdf(pdf, options);
using (FileStream svg = File.Create("radar.svg")) chart.WriteSvg(svg);
```

字体由 `IFontResolver` 的默认实现 `SystemFontResolver` 解析，回退链是：指定字族 → 内置 CJK 候选表（雅黑/宋体/苹方/Noto/思源…）→ 用探针字 `'体'` 匹配系统字体 → `SKTypeface.Default`，任何平台都不会返回 null。PDF 这边，Skia 对能子集化的字体会按实际字形嵌入；微软雅黑这类 ttc 往往整份塞进去，本机一张表约 23.2 MB，同一张表换成 Noto Sans SC 后约 160 KB。库侧没有额外的“子集开关”，换 `Text.Family` 或嵌入字体就够了；`Chart.SharedFonts` 是进程级共享的，不要 Dispose。

## 验证到哪一步

仓库里的检查不是“能打开图片”那种。下面这些命令都能在本机复跑：

```bash
dotnet build OODD.Charts.slnx
dotnet test tests/OODD.Charts.Tests/OODD.Charts.Tests.csproj -f net8.0
dotnet test tests/OODD.Charts.Tests/OODD.Charts.Tests.csproj -f net462
dotnet run --project tools/OODD.Charts.Samples -f net8.0-windows
dotnet run --project tools/OODD.Charts.Samples -f net8.0-windows -- artifacts --forensics
dotnet run --project tools/OODD.Charts.AdapterCheck -f net8.0-windows
```

| 检查 | 现在的实测结果 |
|---|---|
| 编译 | 0 warning / 0 error（net462 / netstandard2.0 / net8.0 三目标） |
| 测试 | net8.0 与 net462 各 **473/473** |
| 样例 | `ALL SAMPLES OK`：40 张逐张断言格高、线宽、共线、槽宽等版面几何，再落盘 PNG/PDF/SVG |
| 取证 | `ALL FORENSICS OK`：17 张扩展表 × 96/120/144/192 dpi |
| 适配器 | WinForms / WPF / Avalonia 与内核基准 PNG **逐字节 0 差异**；控件级鼠标消息链路 7 项通过 |
| 跨 TFM | 同一文档在 net462 与 net8.0 的 PNG 字节完全一致 |

README 里每个可独立编译的代码块都抽出来编译过：快速上手、38 张表单、雷达与引擎示例，共 45 段；七个平台的宿主片段依赖各自 TFM，没并入这次编译。

## 已知限制

- **没有 net40**。SkiaSharp 从未发布 net40 目标，最低只能到 net462；`netstandard2.0` 覆盖 .NET Core / .NET 5+ / Mono / Xamarin。
- **Eto / WinUI 3 / Uno / MAUI 只做了编译验证**。本机没有这些平台的运行时或图形会话，不宣称跑过像素比对。WinForms / WPF / Avalonia 是实测 0 差异。
- **PDF 默认内嵌全量字体**，中文文档约 23 MB；嵌入子集后 160 KB 左右。商业字体（宋体、微软雅黑）不能随程序分发，嵌入前请换成思源黑体 / Noto 等 OFL 字体。
- **不做 CMYK 分色和打印公差**。SkiaSharp 是 RGB 管线，专色只保留原始 CMYK 值加 RGB 近似；印厂环节仍需专业软件。
- **17 张扩展表不是逐张对齐国标**：3 张有标准原文取证，12 张结构已验、版面未校准，2 张只有结构断言。拿去做正式票据前请按自己的样本调令牌。
- **交互目前只有雷达**（点选、悬停、扫描动画）。其他表单提供几何与逆算原语，但没有逐表的 `HitTest`，格子内就地编辑属于业务系统。
- `TemperatureChart/` 目录里是网上找的 GDI+ 参考实现，只用于早期版面实测，不参与编译，也不随仓库发布。

## 许可

Apache-2.0，见 [LICENSE](LICENSE)。

依赖 SkiaSharp（MIT）及其原生库（BSD-3-Clause）。字体请自行确认授权。
