using System;
using System.Collections.Generic;
using System.IO;
using OODD.Charts.Medical.Pain;
using OODD.Charts.Medical.Partogram;
using OODD.Charts.Medical.Restraint;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Medical.Transfusion;
using OODD.Charts.Model;
using OODD.Charts.Style;
using SkiaSharp;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 测试用文档。刻意做得比样例小，但每一类数据项都至少有一条，
    /// 这样"可见性"断言可以逐类点名，而不是被一张大图整体兜住。
    /// </summary>
    internal static class TestDocuments
    {
        /// <summary>仓库根（含 OODD.Charts.slnx 的目录），跨 TFM 产物对比要用它定位。</summary>
        public static string RepoRoot
        {
            get
            {
                DirectoryInfo? dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "OODD.Charts.slnx")))
                {
                    dir = dir.Parent;
                }
                return dir?.FullName ?? Path.GetTempPath();
            }
        }

        public static Chart Temperature()
        {
            TemperatureChartDocument doc = TemperatureDocument();
            return new Chart(doc, new TemperatureChartTemplate(), new ChartStyle());
        }

        public static Chart Partogram()
        {
            PartogramDocument doc = PartogramDocument6h();
            return new Chart(doc, new PartogramChartTemplate(), new ChartStyle());
        }

        public static Chart Pain()
        {
            PainChartDocument doc = PainDocument();
            return new Chart(doc, new PainChartTemplate(), new ChartStyle());
        }

        public static Chart Restraint()
        {
            RestraintChartDocument doc = RestraintDocument();
            return new Chart(doc, new RestraintChartTemplate(), new ChartStyle());
        }

        public static Chart Transfusion()
        {
            TransfusionChartDocument doc = TransfusionDocument();
            return new Chart(doc, new TransfusionChartTemplate(), new ChartStyle());
        }

        public static TemperatureChartDocument TemperatureDocument()
        {
            TemperatureChartDocument doc = new TemperatureChartDocument
            {
                HospitalName = "验收医院",
                FormTitle = "体温单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "王测试";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "34";
            doc.Patient.Department = "产科";
            doc.Patient.Ward = "一病区";
            doc.Patient.BedNumber = "8床";
            doc.Patient.AdmissionNumber = "20260410";
            doc.Patient.AdmissionDate = "2026-04-10";

            DateTime start = new DateTime(2026, 4, 10);
            for (int day = 0; day < 3; day++)
            {
                doc.Days.Add(new ChartGroup(start.AddDays(day)));
            }

            PointSeries axillary = new PointSeries("axillary", TemperatureChartTemplate.AxisTemperature)
            {
                Symbol = MarkerSymbol.Ring,
                Color = SKColors.Blue,
                LegendText = "腋表",
            };
            PointSeries pulse = new PointSeries("pulse", TemperatureChartTemplate.AxisPulse)
            {
                Symbol = MarkerSymbol.Dot,
                Color = SKColors.Red,
                LegendText = "脉搏",
            };

            double[] temps =
            {
                36.8, 37.2, 36.9, 37.4, 38.1, 37.6,
                37.0, 37.3, 36.8, 37.1, 36.9, 37.0,
                38.6, 39.2, 38.4, 37.8, 37.2, 36.9,
            };
            double[] pulses =
            {
                80, 84, 78, 90, 96, 88,
                76, 82, 79, 85, 80, 77,
                104, 118, 110, 98, 92, 86,
            };
            for (int i = 0; i < temps.Length; i++)
            {
                axillary.Values[i] = temps[i];
                pulse.Values[i] = pulses[i];
            }
            doc.Series.Add(axillary);
            doc.Series.Add(pulse);

            // 十类附属行各给一条真实取值，A2 才能逐类断言"看得见"。
            doc.Cells.Add(new CellValue(TemperatureFields.Respiration, 5, "20"));
            doc.Cells.Add(new CellValue(TemperatureFields.BloodPressure, 6, "125/80"));
            doc.Cells.Add(new CellValue(TemperatureFields.Intake, 6, "1450"));
            doc.Cells.Add(new CellValue(TemperatureFields.Output, 6, "1380"));
            doc.Cells.Add(new CellValue(TemperatureFields.Stool, 6, "1"));
            doc.Cells.Add(new CellValue(TemperatureFields.Urine, 6, "4"));
            doc.Cells.Add(new CellValue(TemperatureFields.Weight, 6, "54.2"));
            doc.Cells.Add(new CellValue(TemperatureFields.Height, 0, "162"));
            doc.Cells.Add(new CellValue(TemperatureFields.Allergy, 0, "青霉素"));
            doc.Cells.Add(new CellValue(TemperatureFields.Other, 12, "外出检查"));

            doc.Events.Add(new EventMark(0, "入院"));
            doc.Events.Add(new EventMark(12, "转出"));

            doc.Legend.Add(new LegendItem(MarkerSymbol.Dot, SKColors.Red, "脉搏"));
            doc.Legend.Add(new LegendItem(MarkerSymbol.Ring, SKColors.Blue, "腋表"));
            return doc;
        }

        /// <summary>6 小时、每 2 小时一次宫口检查的初产程；警戒线/处理线的斜率要能在解析式上直接验。</summary>
        public static PartogramDocument PartogramDocument6h()
        {
            PartogramDocument doc = new PartogramDocument
            {
                HospitalName = "验收医院",
                FormTitle = "产程图",
                StartTime = new DateTime(2026, 4, 10, 8, 0, 0),
                Hours = 6,
                SlotsPerHour = 2,
            };

            doc.Patient.Name = "李测试";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "29";
            doc.Patient.Department = "产科";
            doc.Patient.BedNumber = "12床";

            // 槽号 = 小时 × SlotsPerHour
            doc.Cervix.Values[0] = 2d;
            doc.Cervix.Values[4] = 3d;
            doc.Cervix.Values[8] = 4d;
            doc.Cervix.Values[12] = 6d;

            doc.Station.Values[0] = -2d;
            doc.Station.Values[4] = -1d;
            doc.Station.Values[8] = 0d;
            doc.Station.Values[12] = 1d;

            for (int hour = 0; hour <= 6; hour++)
            {
                doc.FetalHeart.Add(new FetalHeartReading(hour * 2, 140 + hour * 2));
            }

            // 异常值刻意落在奇数槽（半小时点），不与整点读数的文字叠在一起，
            // 这样"红圈"断言采到的一定是它自己的墨。
            doc.FetalHeart.Add(new FetalHeartReading(7, 168));

            doc.Contractions.Add(new ContractionReading(2, 3) { DurationSeconds = 40 });
            doc.Contractions.Add(new ContractionReading(6, 4) { DurationSeconds = 45 });

            // 破膜箭头落在宫口曲线上（槽 1 内插 2.25cm），刻意远离两条参考线的取样行。
            doc.Membrane.Add(new MembraneMark(1));

            doc.Cells.Add(new CellValue(PartogramFields.BloodPressure, 4, "128/82"));
            doc.Cells.Add(new CellValue(PartogramFields.Note, 2, "宫口 3cm"));

            return doc;
        }

        /// <summary>2 天 × 6 个评估时点；槽 0 是轻中度（3 分），槽 5 是重度（8 分），阈值线两边都有对照。</summary>
        public static PainChartDocument PainDocument()
        {
            PainChartDocument doc = new PainChartDocument
            {
                HospitalName = "验收医院",
                FormTitle = "疼痛评估记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "陈测试";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "48";
            doc.Patient.Department = "普外科";
            doc.Patient.BedNumber = "18床";

            DateTime start = new DateTime(2026, 3, 2);
            for (int day = 0; day < 2; day++)
            {
                doc.Days.Add(new ChartGroup(start.AddDays(day)));
            }

            PointSeries rest = new PointSeries("rest", PainChartTemplate.AxisNrs)
            {
                Symbol = MarkerSymbol.Ring,
                Color = SKColors.Blue,
                LegendText = "静息痛",
            };
            double[] scores = { 3, 4, 5, 6, 7, 8, 2, 3, 4, 5, 6, 7 };
            for (int slot = 0; slot < scores.Length; slot++)
            {
                rest.Values[slot] = scores[slot];
            }
            doc.Series.Add(rest);

            doc.Cells.Add(new CellValue(PainFields.Site, 0, "切口"));
            doc.Cells.Add(new CellValue(PainFields.Character, 0, "胀痛"));
            doc.Cells.Add(new CellValue(PainFields.Site, 6, "切口"));
            doc.Cells.Add(new CellValue(PainFields.Intervention, 5, "下床"));
            doc.Cells.Add(new CellValue(PainFields.Assessor, 5, "甲"));

            return doc;
        }

        /// <summary>2 天 × 24 小时；第二段约束跨过零点，用来验分页裁切。</summary>
        public static RestraintChartDocument RestraintDocument()
        {
            RestraintChartDocument doc = new RestraintChartDocument
            {
                HospitalName = "验收医院",
                FormTitle = "保护性约束巡视记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "周测试";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "34";
            doc.Patient.Department = "精神科";
            doc.Patient.BedNumber = "12床";

            DateTime start = new DateTime(2026, 3, 5);
            doc.Days.Add(new ChartGroup(start));
            doc.Days.Add(new ChartGroup(start.AddDays(1)));

            doc.Episodes.Add(new RestraintEpisode(1, 6, "双腕"));
            doc.Episodes.Add(new RestraintEpisode(20, 28, "四肢") { IsEmergency = true });

            doc.Cells.Add(new CellValue(RestraintFields.Skin, 2, "完好"));
            doc.Cells.Add(new CellValue(RestraintFields.Nurse, 2, "甲"));
            doc.Cells.Add(new CellValue(RestraintFields.Skin, 25, "完好"));
            doc.Cells.Add(new CellValue(RestraintFields.Note, 25, "冲动"));

            return doc;
        }

        /// <summary>2 天 × 8 个半小时时点；槽 3 体温越过 38 ℃ 并带一次反应事件。</summary>
        public static TransfusionChartDocument TransfusionDocument()
        {
            TransfusionChartDocument doc = new TransfusionChartDocument
            {
                HospitalName = "验收医院",
                FormTitle = "输血反应记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "李测试";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "61";
            doc.Patient.Department = "血液科";
            doc.Patient.BedNumber = "23床";

            DateTime start = new DateTime(2026, 2, 11);
            doc.Days.Add(new ChartGroup(start));
            doc.Days.Add(new ChartGroup(start.AddDays(1)));

            double[] temps =
            {
                36.5, 36.8, 37.2, 39.0, 38.6, 38.0, 37.6, 37.2,
                36.7, 36.9, 37.4, 37.8, 38.8, 38.2, 37.7, 37.3,
            };
            for (int slot = 0; slot < temps.Length; slot++)
            {
                doc.Temperature.Values[slot] = temps[slot];
            }

            doc.Events.Add(new EventMark(3, "寒战"));
            doc.Events.Add(new EventMark(12, "送检"));

            doc.Cells.Add(new CellValue(TransfusionFields.Symptoms, 3, "寒战"));
            doc.Cells.Add(new CellValue(TransfusionFields.BloodPressure, 3, "104/62"));
            doc.Cells.Add(new CellValue(TransfusionFields.Recorder, 12, "乙"));

            return doc;
        }
    }
}
