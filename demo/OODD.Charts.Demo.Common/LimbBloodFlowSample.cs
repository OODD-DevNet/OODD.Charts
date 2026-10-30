using OODD.Charts.Medical.LimbBloodFlow;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Demo
{
    /// <summary>四肢血流探测样例：68 岁男性，右下肢间歇性跛行 3 个月，右 ABI 0.72。</summary>
    public static class LimbBloodFlowSample
    {
        public static LimbBloodFlowDocument Build()
        {
            LimbBloodFlowDocument doc = new LimbBloodFlowDocument
            {
                HospitalName = "测试医院",
                HeaderRightNote = "检查号：VAS-2026-0319",
            };

            doc.Patient.Name = "李长海";
            doc.Patient.Sex = "男";
            doc.Patient.Age = "68";
            doc.Patient.Department = "血管外科";
            doc.Patient.Ward = "二病区";
            doc.Patient.BedNumber = "08床";
            doc.Patient.AdmissionNumber = "2026031207";
            doc.Patient.AdmissionDate = "2026-03-12";
            doc.Patient.ExtraFields.Add(new HeaderField("diagnosis", "诊断",
                "右下肢动脉硬化闭塞症（Fontaine IIb 期）"));
            doc.Patient.ExtraFields.Add(new HeaderField("smoking", "吸烟史", "40 年，20 支/日"));

            // 一、检查信息
            doc.SetCell(LimbBloodFlowSections.Info, LimbBloodFlowRows.ExamDate, 1, "2026-03-19 14:40");
            doc.SetCell(LimbBloodFlowSections.Info, LimbBloodFlowRows.Device, 1,
                "PeriFlux 5000 多普勒血流探测仪（8MHz 探头）");
            doc.SetCell(LimbBloodFlowSections.Info, LimbBloodFlowRows.Site, 1,
                "双上肢、双下肢（含踝部与趾端）");

            // 二、节段压力测量（mmHg）
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, 1, "122");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, 2, "120");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, 3, "2");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.UpperArm, 4, "正常");

            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Forearm, 1, "118");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Forearm, 2, "110");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Forearm, 3, "8");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Forearm, 4, "正常");

            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Thigh, 1, "126");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Thigh, 2, "104");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Thigh, 3, "22");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Thigh, 4, "右侧偏低");

            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Calf, 1, "120");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Calf, 2, "96");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Calf, 3, "24");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Calf, 4, "右侧偏低");

            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 1, "118");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 2, "86");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 3, "32");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle, 4, "右下肢缺血");

            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Toe, 1, "104");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Toe, 2, "62");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Toe, 3, "42");
            doc.SetCell(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Toe, 4, "右下肢缺血");

            doc.Row(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Ankle)!.Cells[4].Color = SKColors.Red;
            doc.Row(LimbBloodFlowSections.Pressure, LimbBloodFlowRows.Toe)!.Cells[4].Color = SKColors.Red;

            // 三、多普勒血流探测
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Femoral, 1, "可探及，三相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Femoral, 2, "可探及，三相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Femoral, 3,
                "双侧三相波，峰值流速 左 82 / 右 74 cm/s");

            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Popliteal, 1, "可探及，三相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Popliteal, 2, "可探及，双相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.Popliteal, 3,
                "右侧波幅减低，峰值流速 左 68 / 右 41 cm/s");

            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.DorsalisPedis, 1, "可探及，三相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.DorsalisPedis, 2, "未探及");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.DorsalisPedis, 3,
                "左侧三相波 56 cm/s；右侧未探及血流信号");

            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.PosteriorTibial, 1, "可探及，三相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.PosteriorTibial, 2, "单相波");
            doc.SetCell(LimbBloodFlowSections.Doppler, LimbBloodFlowRows.PosteriorTibial, 3,
                "左侧 52 cm/s；右侧单相波 26 cm/s，舒张期血流消失");

            // 四、结论
            doc.Section(LimbBloodFlowSections.Conclusion)!.Note =
                "右下肢踝肱指数 0.72（左 0.98），右踝压 86 mmHg、趾压 62 mmHg，右侧胫后动脉呈单相波、足背动脉未探及，提示右下肢中度缺血。\n" +
                "建议血管外科会诊，完善下肢 CTA 评估髂股及膝下动脉病变，必要时行血运重建；同时控制血压、血脂，严格戒烟并规律步行锻炼。";

            // 五、签名
            doc.SetCell(LimbBloodFlowSections.Signature, LimbBloodFlowRows.Sign, 0, "陈志远");
            doc.SetCell(LimbBloodFlowSections.Signature, LimbBloodFlowRows.Sign, 1, "黄丽华");
            doc.SetCell(LimbBloodFlowSections.Signature, LimbBloodFlowRows.Sign, 2, "2026-03-19 15:20");

            return doc;
        }
    }
}
