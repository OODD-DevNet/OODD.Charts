using System.Globalization;
using OODD.Charts.Medical.WorkloadBook;
using OODD.Charts.Model;

namespace OODD.Charts.Demo
{
    /// <summary>工作量统计本样例：普通外科二病区 2026-03-01 至 03-07 一周护理工作量。</summary>
    public static class WorkloadBookSample
    {
        private static readonly string[] RowKeys =
        {
            WorkloadBookRows.Intravenous,
            WorkloadBookRows.Intramuscular,
            WorkloadBookRows.IndwellingNeedle,
            WorkloadBookRows.Catheterization,
            WorkloadBookRows.Suction,
            WorkloadBookRows.OralCare,
            WorkloadBookRows.PressureUlcer,
            WorkloadBookRows.CriticalCare,
        };

        /// <summary>行 = 护理项目，列 = 03-01 至 03-07 的当日执行次数。</summary>
        private static readonly int[,] Counts =
        {
            { 45, 48, 52, 56, 51, 47, 43 },
            { 12, 14, 11, 13, 12, 10, 9 },
            { 6, 7, 9, 8, 7, 6, 5 },
            { 2, 1, 3, 2, 2, 1, 2 },
            { 8, 9, 7, 10, 9, 8, 7 },
            { 15, 16, 18, 17, 16, 14, 13 },
            { 3, 4, 4, 5, 6, 5, 4 },
            { 4, 5, 3, 5, 6, 4, 4 },
        };

        public static WorkloadBookDocument Build()
        {
            WorkloadBookDocument doc = new WorkloadBookDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "台账编号：GZL-2026-031",
            };

            doc.Patient.Name = "张慧";
            doc.Patient.Sex = "女";
            doc.Patient.Department = "普通外科";
            doc.Patient.Ward = "二病区";
            doc.Patient.ExtraFields.Add(new HeaderField("period", "统计周期", "2026-03-01 至 03-07"));
            doc.Patient.ExtraFields.Add(new HeaderField("beds", "开放床位", "48 张"));

            int grandTotal = 0;
            for (int row = 0; row < RowKeys.Length; row++)
            {
                int rowTotal = 0;
                for (int day = 0; day < 7; day++)
                {
                    int value = Counts[row, day];
                    rowTotal += value;
                    doc.SetCell(WorkloadBookSections.Workload, RowKeys[row], day + 1,
                        value.ToString(CultureInfo.InvariantCulture));
                }
                doc.SetCell(WorkloadBookSections.Workload, RowKeys[row], 8,
                    rowTotal.ToString(CultureInfo.InvariantCulture));
                grandTotal += rowTotal;
            }

            for (int day = 0; day < 7; day++)
            {
                int columnTotal = 0;
                for (int row = 0; row < RowKeys.Length; row++)
                {
                    columnTotal += Counts[row, day];
                }
                doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Total, day + 1,
                    columnTotal.ToString(CultureInfo.InvariantCulture));
            }
            doc.SetCell(WorkloadBookSections.Workload, WorkloadBookRows.Total, 8,
                grandTotal.ToString(CultureInfo.InvariantCulture));

            doc.Section(WorkloadBookSections.Notes)!.Note =
                "本周开放床位 48 张，日均在院 45 人；白班 6 人、夜班 3 人排班。" +
                "输液、注射按实际执行次数统计，危重患者护理按班次人次累计；" +
                "数据由责任护士当日登记，护士长周日汇总并与医嘱系统核对。";

            doc.SetCell(WorkloadBookSections.Signature, WorkloadBookRows.Sign, 0, "张慧");
            doc.SetCell(WorkloadBookSections.Signature, WorkloadBookRows.Sign, 1, "周敏");
            doc.SetCell(WorkloadBookSections.Signature, WorkloadBookRows.Sign, 2, "2026-03-08");

            return doc;
        }
    }
}
