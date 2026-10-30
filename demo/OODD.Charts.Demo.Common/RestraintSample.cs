using System;
using OODD.Charts.Medical.Restraint;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>保护性约束巡视记录单样例：1 天 × 24 个小时槽，顶部时段条含两段约束。</summary>
    public static class RestraintSample
    {
        public static RestraintChartDocument Build()
        {
            RestraintChartDocument doc = new RestraintChartDocument
            {
                HospitalName = "测试医院",
                FormTitle = "保护性约束巡视记录单",
                HeaderRightNote = "共 1 页　第 1 页",
            };

            doc.Patient.Name = "周敏";
            doc.Patient.Sex = "女";
            doc.Patient.Age = "34";
            doc.Patient.Department = "精神科";
            doc.Patient.Ward = "三病区";
            doc.Patient.BedNumber = "12床";
            doc.Patient.AdmissionNumber = "2026030501";
            doc.Patient.AdmissionDate = "2026-03-05";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断", "双相情感障碍"));
            doc.Patient.ExtraFields.Add(new HeaderField("order", "约束医嘱", "2026-03-05 08:00"));

            doc.Days.Add(new ChartGroup(new DateTime(2026, 3, 5)));

            doc.Episodes.Add(new RestraintEpisode(1, 6, "双上肢"));
            doc.Episodes.Add(new RestraintEpisode(14, 18, "四肢") { IsEmergency = true });

            for (int slot = 1; slot < 6; slot++)
            {
                Add(doc, RestraintFields.Position, slot, "双腕");
                Add(doc, RestraintFields.Skin, slot, "完好");
                Add(doc, RestraintFields.Circulation, slot, "良好");
                Add(doc, RestraintFields.Release, slot, slot == 3 ? "已松" : "未松");
                Add(doc, RestraintFields.BasicCare, slot, "正常");
                Add(doc, RestraintFields.Nurse, slot, slot % 2 == 0 ? "乙" : "甲");
            }

            for (int slot = 14; slot < 18; slot++)
            {
                Add(doc, RestraintFields.Position, slot, "四肢");
                Add(doc, RestraintFields.Skin, slot, "完好");
                Add(doc, RestraintFields.Circulation, slot, "尚可");
                Add(doc, RestraintFields.Release, slot, slot == 16 ? "已松" : "未松");
                Add(doc, RestraintFields.BasicCare, slot, "拒食");
                Add(doc, RestraintFields.Nurse, slot, "甲");
            }

            Add(doc, RestraintFields.Note, 3, "松解");
            Add(doc, RestraintFields.Note, 15, "冲动");
            Add(doc, RestraintFields.Note, 17, "知情");

            return doc;
        }

        private static void Add(RestraintChartDocument doc, string rowKey, int slot, string text)
        {
            doc.Cells.Add(new CellValue(rowKey, slot, text));
        }
    }
}
