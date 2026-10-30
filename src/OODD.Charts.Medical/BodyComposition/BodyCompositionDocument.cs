using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.BodyComposition
{
    /// <summary>身体组成分析报告的区段标识，供样例回填。</summary>
    public static class BodyCompositionSections
    {
        public const string Info = "info";
        public const string Results = "results";
        public const string Conclusion = "conclusion";
        public const string Signature = "signature";
    }

    /// <summary>检查信息、测量指标与签名行的稳定标识。</summary>
    public static class BodyCompositionRows
    {
        public const string ExamDate = "exam-date";
        public const string Device = "device";
        public const string Position = "position";

        public const string Weight = "weight";
        public const string Height = "height";
        public const string Bmi = "bmi";
        public const string BodyFat = "body-fat";
        public const string SkeletalMuscle = "skeletal-muscle";
        public const string VisceralFat = "visceral-fat";
        public const string Bmr = "bmr";
        public const string BodyWater = "body-water";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 健检身体组成分析检查报告。上半为检查信息与八项测量指标（含参考区间与判读），
    /// 下半为结论与双人签名，供体检中心打印归档。
    /// </summary>
    public sealed class BodyCompositionDocument : FormSheetDocument
    {
        public BodyCompositionDocument()
        {
            FormTitle = "健检身体组成分析检查报告";
            FooterNote = "本报告仅对本次测量负责，结果供临床与健康管理参考。";

            AddSection(new FormSection("一、检查信息",
                new FormColumn("项目", 1f),
                new FormColumn("内容", 2.4f))
            {
                Key = BodyCompositionSections.Info,
                RowHeight = 30f,
            });
            AddRow(BodyCompositionSections.Info, BodyCompositionRows.ExamDate, "检查日期");
            AddRow(BodyCompositionSections.Info, BodyCompositionRows.Device, "设备型号");
            AddRow(BodyCompositionSections.Info, BodyCompositionRows.Position, "测量体位");

            AddSection(new FormSection("二、测量结果",
                new FormColumn("指标", 1.4f),
                new FormColumn("测量值", 1f),
                new FormColumn("参考区间", 1.6f),
                new FormColumn("判读", 1f))
            {
                Key = BodyCompositionSections.Results,
                RowHeight = 30f,
            });
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.Weight, "体重(kg)", null, "—", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.Height, "身高(cm)", null, "—", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.Bmi, "BMI", null, "18.5–23.9", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.BodyFat, "体脂率(%)", null, "20.0–28.0", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.SkeletalMuscle, "骨骼肌量(kg)", null,
                "21.0–25.0", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.VisceralFat, "内脏脂肪等级", null,
                "1–8", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.Bmr, "基础代谢(kcal)", null,
                "1100–1400", null);
            AddRow(BodyCompositionSections.Results, BodyCompositionRows.BodyWater, "体水分率(%)", null,
                "45.0–60.0", null);

            AddSection(new FormSection("三、结论",
                new FormColumn("结论", 1f))
            {
                Key = BodyCompositionSections.Conclusion,
            });

            AddSection(new FormSection("四、签名",
                new FormColumn("检查技师", 1.6f),
                new FormColumn("审核医师", 1.6f),
                new FormColumn("报告时间", 1.6f))
            {
                Key = BodyCompositionSections.Signature,
                RowHeight = 34f,
            });
            AddRow(BodyCompositionSections.Signature, BodyCompositionRows.Sign);
        }
    }

    /// <summary>身体组成报告版式：A4 纵向，检查信息 / 指标表 / 结论 / 签名四段。</summary>
    public sealed class BodyCompositionTemplate : FormSheetTemplate
    {
        public BodyCompositionTemplate()
        {
            Tokens.Width = 794f;
        }

        public override string Key => "medical.body-composition";

        public override string DisplayName => "健检身体组成分析检查报告";
    }
}
