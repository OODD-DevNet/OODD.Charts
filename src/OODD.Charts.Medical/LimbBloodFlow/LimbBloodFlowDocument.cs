using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.LimbBloodFlow
{
    /// <summary>四肢血流探测报告的区段标识，供样例回填。</summary>
    public static class LimbBloodFlowSections
    {
        public const string Info = "info";
        public const string Pressure = "pressure";
        public const string Doppler = "doppler";
        public const string Conclusion = "conclusion";
        public const string Signature = "signature";
    }

    /// <summary>检查信息、测量节段、探测血管与签名行的稳定标识。</summary>
    public static class LimbBloodFlowRows
    {
        public const string ExamDate = "exam-date";
        public const string Device = "device";
        public const string Site = "site";

        public const string UpperArm = "upper-arm";
        public const string Forearm = "forearm";
        public const string Thigh = "thigh";
        public const string Calf = "calf";
        public const string Ankle = "ankle";
        public const string Toe = "toe";

        public const string Femoral = "femoral";
        public const string Popliteal = "popliteal";
        public const string DorsalisPedis = "dorsalis-pedis";
        public const string PosteriorTibial = "posterior-tibial";

        public const string Sign = "sign";
    }

    /// <summary>
    /// 四肢血流探测及压力测量检查报告。上部为检查信息，中部为六节段双侧压力
    /// 与四条动脉的多普勒频谱，下部为结论与签名，适用于下肢动脉硬化闭塞症筛查。
    /// </summary>
    public sealed class LimbBloodFlowDocument : FormSheetDocument
    {
        public LimbBloodFlowDocument()
        {
            FormTitle = "四肢血流探测及压力测量检查报告";
            FooterNote = "本报告依据本次多普勒与节段压力测量结果出具，仅供临床参考。";

            AddSection(new FormSection("一、检查信息",
                new FormColumn("项目", 1f),
                new FormColumn("内容", 2.4f))
            {
                Key = LimbBloodFlowSections.Info,
                RowHeight = 30f,
            });
            AddRow(LimbBloodFlowSections.Info, LimbBloodFlowRows.ExamDate, "检查日期");
            AddRow(LimbBloodFlowSections.Info, LimbBloodFlowRows.Device, "检查设备");
            AddRow(LimbBloodFlowSections.Info, LimbBloodFlowRows.Site, "检查部位");

            AddSection(new FormSection("二、节段压力测量（mmHg）",
                new FormColumn("测量节段", 1.4f),
                new FormColumn("左肢", 1f),
                new FormColumn("右肢", 1f),
                new FormColumn("左右差", 1f),
                new FormColumn("判读", 1.4f))
            {
                Key = LimbBloodFlowSections.Pressure,
                RowHeight = 30f,
            });
            AddRow(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, "上臂");
            AddRow(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Forearm, "前臂");
            AddRow(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Thigh, "大腿");
            AddRow(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Calf, "小腿");
            AddRow(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, "踝部");
            AddRow(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Toe, "趾端");

            AddSection(new FormSection("三、多普勒血流探测",
                new FormColumn("血管", 1.4f),
                new FormColumn("左侧", 1.4f),
                new FormColumn("右侧", 1.4f),
                new FormColumn("频谱形态", 2f))
            {
                Key = LimbBloodFlowSections.Doppler,
                RowHeight = 30f,
            });
            AddRow(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Femoral, "股动脉");
            AddRow(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Popliteal, "腘动脉");
            AddRow(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.DorsalisPedis, "足背动脉");
            AddRow(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.PosteriorTibial, "胫后动脉");

            AddSection(new FormSection("四、结论",
                new FormColumn("结论", 1f))
            {
                Key = LimbBloodFlowSections.Conclusion,
            });

            AddSection(new FormSection("五、签名",
                new FormColumn("检查医师", 1.6f),
                new FormColumn("审核医师", 1.6f),
                new FormColumn("报告时间", 1.6f))
            {
                Key = LimbBloodFlowSections.Signature,
                RowHeight = 34f,
            });
            AddRow(LimbBloodFlowSections.Signature, LimbBloodFlowRows.Sign);
        }
    }

    /// <summary>四肢血流报告版式：A4 横向加宽，压力表与多普勒表上下排布。</summary>
    public sealed class LimbBloodFlowTemplate : FormSheetTemplate
    {
        public LimbBloodFlowTemplate()
        {
            Tokens.Width = 1040f;
        }

        public override string Key => "medical.limb-blood-flow";

        public override string DisplayName => "四肢血流探测及压力测量检查报告";
    }
}
