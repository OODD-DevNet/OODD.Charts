# OODD.Charts

基于 SkiaSharp 的跨平台“刚性版面”渲染引擎与行业表单库。表单不是数据可视化：格子尺寸、线宽、多套坐标系共用横线、逐格落字都由模板在 96dpi 逻辑单位里精确求解，一份文档可导出 PNG / JPEG / PDF / SVG，并由 WinForms / WPF / Avalonia 等宿主控件直接显示与交互。

Rigid-form rendering engine on SkiaSharp. Forms are laid out as fixed grids in 96dpi logical units, then exported to PNG / JPEG / PDF / SVG or hosted in WinForms / WPF / Avalonia / Eto / WinUI / Uno / MAUI controls.

## 包一览 / Packages

| 包 | 内容 |
|---|---|
| `OODD.Charts.Core` | 渲染管线、几何与轴、导出、字体回退、交互契约 |
| `OODD.Charts.Medical` | 三测单、产程图、疼痛评估、约束巡视、输血反应等 21 张医疗单据 + 5 张医疗扩展表 |
| `OODD.Charts.Radar` | 雷达态势图（360°/180° PPI）与 `RadarInteraction` |
| `OODD.Charts.Engineering` | 测井成果图、变压器油色谱（大卫三角形） |
| `OODD.Charts.Transit` | 列车运行图、载重平衡、积载图、危货联单、超限附图 |
| `OODD.Charts.Geoscience` | 地面天气图、T-lnP 温度对数压力图 |
| `OODD.Charts.Tickets` | 电气工作票、特殊作业票、增值税专用发票 |
| `OODD.Charts.Adapters.{WinForms,Wpf,Avalonia,Eto,WinUi,Uno,Maui}` | 各宿主位图桥与 `ChartControl` |

## 最小示例 / Quick start

```csharp
using System.IO;
using OODD.Charts;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Model;
using SkiaSharp;

var doc = new TemperatureChartDocument { HospitalName = "示例医院", FormTitle = "体温单" };
doc.Patient.Name = "张三";
doc.Days.Add(new ChartGroup(new DateTime(2026, 10, 30)));

var series = new PointSeries("axillary", TemperatureChartTemplate.AxisTemperature)
{
    Symbol = MarkerSymbol.Ring,
    Color = SKColors.Blue,
};
series.Values[0] = 36.8;
series.Values[2] = 37.4;
doc.Series.Add(series);

using var chart = new Chart(doc, new TemperatureChartTemplate());
using (FileStream fs = File.Create("temperature.png")) chart.WritePng(fs, dpi: 96f);
using (FileStream fs = File.Create("temperature.pdf")) chart.WritePdf(fs, PdfOptions.A4Portrait);
using (FileStream fs = File.Create("temperature.svg")) chart.WriteSvg(fs);
```

复杂文种由 HarfBuzzSharp 做 OpenType 整形：阿拉伯文连写、天城文辅音簇合并、RTL 方向与 `CellAlign.Near`/`Far` 对应；拉丁文与中文的量宽、落墨与 1.0 逐位一致。复杂文种的 SVG 导出走字形轮廓（不可检索），PNG / JPEG / PDF 不受影响。Linux 需系统字体并额外引用 `HarfBuzzSharp.NativeAssets.Linux`；本库不打包任何字体。

Complex scripts (Arabic, Hebrew, Devanagari, Thai and others) are shaped with HarfBuzzSharp, including RTL alignment; Latin and CJK output is unchanged. Complex-script SVG is emitted as glyph outlines (not searchable). On Linux add `HarfBuzzSharp.NativeAssets.Linux` and system fonts; no fonts ship with the library.

完整文档、每个控件的用法与案例见仓库 README（中英双语）。
Full documentation and per-form examples: <https://github.com/OODD-DevNet/OODD.Charts>.

## 许可 / License

Apache-2.0。字体需自行确认授权：宋体/微软雅黑等商业字体不可随程序分发，嵌入 PDF 前请换成思源黑体/Noto 等 OFL 字体。依赖 SkiaSharp（MIT）、HarfBuzzSharp（MIT）及其原生库（BSD-3-Clause）。
Apache-2.0. Ship only fonts you are licensed to redistribute. Depends on SkiaSharp (MIT), HarfBuzzSharp (MIT) and their native libraries (BSD-3-Clause).
