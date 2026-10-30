using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.TcmNursing
{
    /// <summary>区段标识，供样例回填。</summary>
    public static class TcmNursingSections
    {
        public const string Syndrome = "syndrome";
        public const string Measures = "measures";
        public const string Notes = "notes";
        public const string Signature = "signature";
    }

    /// <summary>行标识，供样例按名回填。</summary>
    public static class TcmNursingRows
    {
        public const string Pattern = "pattern";

        public const string Auricular = "auricular";
        public const string Acupoint = "acupoint";
        public const string FootBath = "foot-bath";
        public const string Emotion = "emotion";
        public const string Diet = "diet";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 中医专病辨证施护护理记录单。首段记录证型、治则、舌脉与主症，
    /// 中段按日期逐项记录中医护理技术实施与效果，末段为连续护理叙述与签名。
    /// </summary>
    public sealed class TcmNursingDocument : FormSheetDocument
    {
        public TcmNursingDocument()
        {
            FormTitle = "中医专病辨证施护护理记录单";
            FooterNote = "本单随病历保存；施护项目按医嘱执行，出现不良反应立即停止并报告医师。";

            AddSection(new FormSection("一、辨证信息",
                new FormColumn("证型", 1.2f),
                new FormColumn("治则", 1.4f),
                new FormColumn("舌脉", 1.4f),
                new FormColumn("主症", 1.6f, CellAlign.Near))
            {
                Key = TcmNursingSections.Syndrome,
                RowHeight = 34f,
            });
            AddRow(TcmNursingSections.Syndrome, TcmNursingRows.Pattern);

            AddSection(new FormSection("二、施护记录",
                new FormColumn("日期时间", 1.2f),
                new FormColumn("施护项目", 1.2f),
                new FormColumn("实施情况", 2.2f, CellAlign.Near),
                new FormColumn("效果评价", 1.6f, CellAlign.Near))
            {
                Key = TcmNursingSections.Measures,
                RowHeight = 32f,
            });
            AddRow(TcmNursingSections.Measures, TcmNursingRows.Auricular, null, "耳穴压豆");
            AddRow(TcmNursingSections.Measures, TcmNursingRows.Acupoint, null, "穴位贴敷");
            AddRow(TcmNursingSections.Measures, TcmNursingRows.FootBath, null, "中药足浴");
            AddRow(TcmNursingSections.Measures, TcmNursingRows.Emotion, null, "情志护理");
            AddRow(TcmNursingSections.Measures, TcmNursingRows.Diet, null, "饮食调护");

            AddSection(new FormSection("三、护理记录")
            {
                Key = TcmNursingSections.Notes,
            });

            AddSection(new FormSection("四、签名",
                new FormColumn("责任护士", 1.6f, CellAlign.Center, true),
                new FormColumn("护士长", 1.6f, CellAlign.Center, true),
                new FormColumn("记录时间", 1.6f, CellAlign.Center, true))
            {
                Key = TcmNursingSections.Signature,
                RowHeight = 34f,
            });
            AddRow(TcmNursingSections.Signature, TcmNursingRows.Sign);
        }
    }

    /// <summary>辨证施护版式：宽幅横向，四区段竖排，护理记录整段叙述。</summary>
    public sealed class TcmNursingTemplate : FormSheetTemplate
    {
        public TcmNursingTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.tcm-syndrome-nursing";

        public override string DisplayName => "中医专病辨证施护护理记录单";
    }
}
