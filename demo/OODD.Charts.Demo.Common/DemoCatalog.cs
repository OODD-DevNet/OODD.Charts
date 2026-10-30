using System;
using System.Collections.Generic;
using OODD.Charts.Medical.AdverseEventBook;
using OODD.Charts.Medical.ApacheIi;
using OODD.Charts.Medical.BodyComposition;
using OODD.Charts.Medical.CancerPsychologyConsult;
using OODD.Charts.Medical.CodeBlue;
using OODD.Charts.Medical.HyperbaricCompanion;
using OODD.Charts.Medical.HyperbaricEducation;
using OODD.Charts.Medical.HyperbaricTransfer;
using OODD.Charts.Medical.InventoryBook;
using OODD.Charts.Medical.LimbBloodFlow;
using OODD.Charts.Medical.OrganDonation;
using OODD.Charts.Medical.Pain;
using OODD.Charts.Medical.Partogram;
using OODD.Charts.Medical.Pca;
using OODD.Charts.Medical.PostmortemCare;
using OODD.Charts.Medical.Restraint;
using OODD.Charts.Medical.SurgicalSafetyChecklist;
using OODD.Charts.Medical.TcmNursing;
using OODD.Charts.Medical.Temperature;
using OODD.Charts.Medical.Transfusion;
using OODD.Charts.Medical.WorkloadBook;
using OODD.Charts.Radar;
using OODD.Charts.Style;

namespace OODD.Charts.Demo
{
    /// <summary>一张可演示的样例：稳定名称 + 中文表单名 + 建图表工厂。</summary>
    public sealed class DemoSample
    {
        public DemoSample(string name, string displayName, Func<Chart> create)
        {
            Name = name;
            DisplayName = displayName;
            Create = create;
        }

        /// <summary>命令行/文件名使用的稳定标识。</summary>
        public string Name { get; }

        /// <summary>演示界面上显示的中文名称。</summary>
        public string DisplayName { get; }

        public Func<Chart> Create { get; }
    }

    /// <summary>
    /// 全部样例的登记表。样例工具、三端演示与通用遍历测试都从这里取，
    /// 新增一张单据只需在末尾加一条，不再散落 switch。
    /// </summary>
    public static class DemoCatalog
    {
        public static IReadOnlyList<DemoSample> All { get; } = Build();

        private static List<DemoSample> Build()
        {
            List<DemoSample> samples = new List<DemoSample>
            {
                new DemoSample("temperature", "三测单（体温单）",
                    () => new Chart(TemperatureSample.Build(), new TemperatureChartTemplate(), new ChartStyle())),
                new DemoSample("partogram", "产程图",
                    () => new Chart(PartogramSample.Build(), new PartogramChartTemplate(), new ChartStyle())),
                new DemoSample("pain", "疼痛评估记录单",
                    () => new Chart(PainSample.Build(), new PainChartTemplate(), new ChartStyle())),
                new DemoSample("restraint", "保护性约束巡视记录单",
                    () => new Chart(RestraintSample.Build(), new RestraintChartTemplate(), new ChartStyle())),
                new DemoSample("transfusion", "输血反应记录单",
                    () => new Chart(TransfusionSample.Build(), new TransfusionChartTemplate(), new ChartStyle())),
                new DemoSample("hyperbaric-companion", "高压氧治疗陪舱记录单",
                    () => new Chart(HyperbaricCompanionSample.Build(), new HyperbaricCompanionTemplate(), new ChartStyle())),
                new DemoSample("hyperbaric-education", "高压氧进舱宣教记录单",
                    () => new Chart(HyperbaricEducationSample.Build(), new HyperbaricEducationTemplate(), new ChartStyle())),
                new DemoSample("neurosurgery-hyperbaric-transfer", "神经外科高压氧疗接送记录单",
                    () => new Chart(HyperbaricTransferSample.Build(), new HyperbaricTransferTemplate(), new ChartStyle())),
                new DemoSample("code-blue", "Code Blue 抢救记录本",
                    () => new Chart(CodeBlueSample.Build(), new CodeBlueTemplate(), new ChartStyle())),
                new DemoSample("pca", "患者自控镇痛（PCA）记录单",
                    () => new Chart(PcaSample.Build(), new PcaTemplate(), new ChartStyle())),
                new DemoSample("apache-ii", "加護病房疾病嚴重程度評估表（APACHE II）",
                    () => new Chart(ApacheIiSample.Build(), new ApacheIiTemplate(), new ChartStyle())),
                new DemoSample("surgical-safety-checklist", "手术护理查核單/手术安全核查表",
                    () => new Chart(SurgicalSafetyChecklistSample.Build(),
                        new SurgicalSafetyChecklistTemplate(), new ChartStyle())),
                new DemoSample("tcm-syndrome-nursing", "中医专病辨证施护护理记录单",
                    () => new Chart(TcmNursingSample.Build(), new TcmNursingTemplate(), new ChartStyle())),
                new DemoSample("cancer-psychology-consult", "癌症心理服务会诊回覆",
                    () => new Chart(CancerPsychologyConsultSample.Build(),
                        new CancerPsychologyConsultTemplate(), new ChartStyle())),
                new DemoSample("body-composition", "健检身体组成分析检查报告",
                    () => new Chart(BodyCompositionSample.Build(), new BodyCompositionTemplate(), new ChartStyle())),
                new DemoSample("limb-blood-flow", "四肢血流探测及压力测量检查报告",
                    () => new Chart(LimbBloodFlowSample.Build(), new LimbBloodFlowTemplate(), new ChartStyle())),
                new DemoSample("inventory-book", "点物本",
                    () => new Chart(InventoryBookSample.Build(), new InventoryBookTemplate(), new ChartStyle())),
                new DemoSample("workload-book", "工作量统计本",
                    () => new Chart(WorkloadBookSample.Build(), new WorkloadBookTemplate(), new ChartStyle())),
                new DemoSample("adverse-event-book", "不良事件登记本",
                    () => new Chart(AdverseEventBookSample.Build(), new AdverseEventBookTemplate(), new ChartStyle())),
                new DemoSample("postmortem-care", "尸体护理/遗体转运记录",
                    () => new Chart(PostmortemCareSample.Build(), new PostmortemCareTemplate(), new ChartStyle())),
                new DemoSample("organ-donation", "人体器官捐献过程记录表/器官分配转运记录单",
                    () => new Chart(OrganDonationSample.Build(), new OrganDonationTemplate(), new ChartStyle())),
                new DemoSample("radar-full", "雷达态势图（360° 全向）",
                    () => new Chart(RadarSample.BuildFull(), new RadarChartTemplate(), new ChartStyle())),
                new DemoSample("radar-half", "雷达态势图（180° 扇区）",
                    () => new Chart(RadarSample.BuildHalf(), new RadarChartTemplate(), new ChartStyle())),
            };

            samples.AddRange(EngineeringSamples.All);
            samples.AddRange(TransitSamples.All);
            samples.AddRange(GeoscienceSamples.All);
            samples.AddRange(NewMedicalSamples.All);
            samples.AddRange(TicketSamples.All);

            return samples;
        }

        public static DemoSample? Find(string name)
        {
            foreach (DemoSample sample in All)
            {
                if (string.Equals(sample.Name, name, StringComparison.Ordinal))
                {
                    return sample;
                }
            }
            return null;
        }
    }
}
