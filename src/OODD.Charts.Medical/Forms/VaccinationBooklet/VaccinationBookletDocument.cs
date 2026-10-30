using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.Forms.VaccinationBooklet
{
    /// <summary>预印疫苗行（国家免疫规划疫苗剂次表的一行）。</summary>
    public sealed class VaccinationScheduleRow
    {
        public VaccinationScheduleRow(string vaccine, string dose)
        {
            Vaccine = vaccine ?? string.Empty;
            Dose = dose ?? string.Empty;
        }

        public string Vaccine { get; }
        public string Dose { get; }
    }

    /// <summary>一条实际接种记录。</summary>
    public sealed class VaccinationRecord
    {
        public VaccinationRecord(string vaccine, string dose, string date, string manufacturer, string lot)
        {
            Vaccine = vaccine ?? string.Empty;
            Dose = dose ?? string.Empty;
            Date = date ?? string.Empty;
            Manufacturer = manufacturer ?? string.Empty;
            Lot = lot ?? string.Empty;
        }

        public string Vaccine { get; }
        public string Dose { get; }
        public string Date { get; }
        public string Manufacturer { get; }
        public string Lot { get; }
    }

    /// <summary>
    /// 预防接种证文档。页序由 <see cref="TotalLogicalPages"/> 与 <see cref="SpreadIndex"/> 决定：
    /// 12 个逻辑页按鞍装配对：(12,1)(2,11)(10,3)(4,9)(8,5)(6,7)。
    /// <para>页面约定：1 封面；2 封二；3/10/4/9/5/8 接种记录页；6 评估；7 备注；11 基本信息；12 封底。</para>
    /// </summary>
    public sealed class VaccinationBookletDocument : ChartDocument
    {
        public VaccinationBookletDocument()
        {
            foreach (VaccinationScheduleRow row in CreateNationalSchedule())
            {
                NationalSchedule.Add(row);
            }
        }

        /// <summary>国家免疫规划疫苗剂次表（模板据此预印疫苗名称与剂次行）。</summary>
        public IList<VaccinationScheduleRow> NationalSchedule { get; } = new List<VaccinationScheduleRow>();

        public IList<VaccinationRecord> Records { get; } = new List<VaccinationRecord>();

        /// <summary>评估意见（封二/评估页）。</summary>
        public IList<string> Assessments { get; } = new List<string>();

        /// <summary>专色原始 CMYK 值（供印厂；版面用近似 RGB 呈现）。</summary>
        public string SpotColorCmyk { get; set; } = "C85 M30 Y65 K65";

        public int TotalLogicalPages { get; set; } = 12;

        /// <summary>当前渲染的开面序号（0–5）。默认 1 = (2,11)：封二与基本信息同开面。</summary>
        public int SpreadIndex { get; set; } = 1;

        /// <summary>
        /// 国家免疫规划疫苗剂次表（22 行）：乙肝 3、卡介苗 1、脊灰 4、百白破 4、白破 1、
        /// 麻腮风 2、乙脑 2、A 群流脑 2、A+C 流脑 2、甲肝 1。数据驱动，模板不写死行数。
        /// </summary>
        public static IReadOnlyList<VaccinationScheduleRow> CreateNationalSchedule()
        {
            List<VaccinationScheduleRow> rows = new List<VaccinationScheduleRow>();
            Add(rows, "乙肝疫苗", new[] { "1", "2", "3" });
            Add(rows, "卡介苗", new[] { "1" });
            Add(rows, "脊灰疫苗", new[] { "1", "2", "3", "4" });
            Add(rows, "百白破疫苗", new[] { "1", "2", "3", "4" });
            Add(rows, "白破疫苗", new[] { "1" });
            Add(rows, "麻腮风疫苗", new[] { "1", "2" });
            Add(rows, "乙脑疫苗", new[] { "1", "2" });
            Add(rows, "A 群流脑疫苗", new[] { "1", "2" });
            Add(rows, "A+C 群流脑疫苗", new[] { "1", "2" });
            Add(rows, "甲肝疫苗", new[] { "1" });
            return rows.AsReadOnly();
        }

        private static void Add(List<VaccinationScheduleRow> rows, string vaccine, string[] doses)
        {
            foreach (string dose in doses)
            {
                rows.Add(new VaccinationScheduleRow(vaccine, dose));
            }
        }
    }
}
