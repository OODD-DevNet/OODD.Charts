using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.ApacheIi
{
    /// <summary>APACHE II 各区段的稳定标识。</summary>
    public static class ApacheIiSections
    {
        public const string Physiology = "physiology";
        public const string Chronic = "chronic";
        public const string Result = "result";
    }

    /// <summary>
    /// 加護病房疾病嚴重程度評估表（APACHE II）。入 ICU 24 小时内取最差值，
    /// 12 项生理指标 + 年龄 + 慢性健康状况三项分值相加得出总分，
    /// 再按总分查预计病死率；本表只排版与汇总，不内置评分算法。
    /// </summary>
    public sealed class ApacheIiDocument : FormSheetDocument
    {
        public ApacheIiDocument()
        {
            FormTitle = "加護病房疾病嚴重程度評估表（APACHE II）";
            FooterNote = "本表由主管医师于入 ICU 24 小时内完成，结果记入重症监护记录单。";

            AddSection(new FormSection("一、生理指标",
                new FormColumn("指标", 1.4f, CellAlign.Near, true),
                new FormColumn("实测值", 1f),
                new FormColumn("分值", 0.7f),
                new FormColumn("说明", 1.8f, CellAlign.Near))
            {
                Key = ApacheIiSections.Physiology,
                RowHeight = 28f,
            });
            AddRow(ApacheIiSections.Physiology, "temperature", "体温(℃)");
            AddRow(ApacheIiSections.Physiology, "map", "平均动脉压(mmHg)");
            AddRow(ApacheIiSections.Physiology, "heart-rate", "心率(次/分)");
            AddRow(ApacheIiSections.Physiology, "respiratory-rate", "呼吸频率(次/分)");
            AddRow(ApacheIiSections.Physiology, "oxygenation", "氧合(A-aDO2 或 PaO2)");
            AddRow(ApacheIiSections.Physiology, "ph", "动脉血 pH");
            AddRow(ApacheIiSections.Physiology, "sodium", "血钠(mmol/L)");
            AddRow(ApacheIiSections.Physiology, "potassium", "血钾(mmol/L)");
            AddRow(ApacheIiSections.Physiology, "creatinine", "血肌酐(μmol/L)");
            AddRow(ApacheIiSections.Physiology, "hematocrit", "红细胞压积(%)");
            AddRow(ApacheIiSections.Physiology, "wbc", "白细胞计数(×10^9/L)");
            AddRow(ApacheIiSections.Physiology, "gcs", "GCS 评分");

            AddSection(new FormSection("二、年龄与慢性健康",
                new FormColumn("项目", 1.4f, CellAlign.Near, true),
                new FormColumn("实测值", 1f),
                new FormColumn("分值", 0.7f),
                new FormColumn("说明", 1.8f, CellAlign.Near))
            {
                Key = ApacheIiSections.Chronic,
                RowHeight = 28f,
            });
            AddRow(ApacheIiSections.Chronic, "age", "年龄");
            AddRow(ApacheIiSections.Chronic, "chronic-health", "慢性健康状况");

            AddSection(new FormSection("三、评分结果",
                new FormColumn("项目", 1.4f, CellAlign.Near, true),
                new FormColumn("结果", 1f),
                new FormColumn("说明", 2f, CellAlign.Near))
            {
                Key = ApacheIiSections.Result,
                RowHeight = 30f,
                Note = "评分说明：总分 = 12 项生理指标分值之和 + 年龄分值 + 慢性健康状况分值；"
                    + "总分 25 分以上属高危，建议每 24 小时复评并记录于病程。",
            });
            FormRow total = AddRow(ApacheIiSections.Result, "total", "总分");
            total.IsTotal = true;
            AddRow(ApacheIiSections.Result, "mortality", "预计病死率");
        }
    }

    /// <summary>APACHE II 版式：A4 横向，三区段（12 + 2 + 2 行）。</summary>
    public sealed class ApacheIiTemplate : FormSheetTemplate
    {
        public ApacheIiTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.apache-ii";

        public override string DisplayName => "加護病房疾病嚴重程度評估表（APACHE II）";
    }
}
