using System;
using System.Collections.Generic;

namespace OODD.Charts.Medical.Catalog
{
    /// <summary>
    /// “另类”医疗记录单的四类分型。收单时先问“它属于哪一类”，
    /// 比逐张判断“像不像三测单”更快，也更容易判断现有版面能不能直接复用。
    /// </summary>
    public enum SpecialtyFormKind
    {
        /// <summary>① 专科专属型：只有某个专科/某种治疗才会用到，但仍是随时间或分期的护理记录。</summary>
        Specialty = 0,

        /// <summary>② 事件触发型：平时不写，事件发生时才启动，时间轴以分钟/小时计。</summary>
        EventTriggered = 1,

        /// <summary>③ 设备/检查输出型：数据由仪器或评分算法产生，以指标、参考区间和结论为主。</summary>
        DeviceOutput = 2,

        /// <summary>④ 科室运营/边界事务型：台账、交接、会诊、终末处置等，服务对象常常不是“一张趋势图”。</summary>
        DepartmentOperations = 3,
    }

    /// <summary>入库状态。Planned 只表示已登记与已归型，不表示已有可用模板。</summary>
    public enum SpecialtyFormStatus
    {
        Implemented = 0,
        Planned = 1,
    }

    /// <summary>目录中的一条特殊记录单。</summary>
    public sealed class SpecialtyFormEntry
    {
        public SpecialtyFormEntry(
            string key,
            string name,
            SpecialtyFormKind kind,
            SpecialtyFormStatus status,
            string scene,
            string whySpecial,
            string? templateKey = null,
            string? nextStep = null)
        {
            Key = key;
            Name = name;
            Kind = kind;
            Status = status;
            Scene = scene;
            WhySpecial = whySpecial;
            TemplateKey = templateKey;
            NextStep = nextStep;
        }

        /// <summary>稳定标识，供业务方做权限、路由或开关。</summary>
        public string Key { get; }

        public string Name { get; }

        public SpecialtyFormKind Kind { get; }

        public SpecialtyFormStatus Status { get; }

        /// <summary>典型使用场景。</summary>
        public string Scene { get; }

        /// <summary>为什么它不能直接套用三测单/产程图。</summary>
        public string WhySpecial { get; }

        /// <summary>已实现的模板键；未实现为 null。</summary>
        public string? TemplateKey { get; }

        /// <summary>排期建议：复用哪条版面路径、还缺什么能力。</summary>
        public string? NextStep { get; }
    }

    /// <summary>
    /// 特殊医疗记录单目录。分型口径见 <see cref="SpecialtyFormKind"/>；
    /// 全部 19 张单据均已实现并带稳定模板键。
    /// </summary>
    public static class SpecialtyFormCatalog
    {
        private static readonly SpecialtyFormEntry[] Entries =
        {
            new SpecialtyFormEntry(
                "psych-restraint", "精神科保护性约束巡视记录单",
                SpecialtyFormKind.Specialty, SpecialtyFormStatus.Implemented,
                "精神科病房对约束患者的逐小时巡视与解除评估。",
                "约束是有严格时限的强制措施，巡视项、松解记录与解除条件本身就是法律文书要求。",
                "medical.restraint-observation"),

            new SpecialtyFormEntry(
                "hyperbaric-companion", "高压氧治疗陪舱记录单",
                SpecialtyFormKind.Specialty, SpecialtyFormStatus.Implemented,
                "高压氧舱内陪舱人员记录加压、稳压、减压全过程。",
                "舱内无法随时进出，记录必须覆盖压力阶段与陪舱人员状态，属舱内专属文书。",
                "medical.hyperbaric-companion"),

            new SpecialtyFormEntry(
                "hyperbaric-education", "高压氧进舱宣教记录单",
                SpecialtyFormKind.Specialty, SpecialtyFormStatus.Implemented,
                "患者首次进舱前的安全宣教与禁忌症确认。",
                "一次性的知情与安全确认，内容固定、以勾选和签名为主，与趋势图无关。",
                "medical.hyperbaric-education"),

            new SpecialtyFormEntry(
                "neurosurgery-hyperbaric-transfer", "神经外科高压氧疗接送记录单",
                SpecialtyFormKind.DepartmentOperations, SpecialtyFormStatus.Implemented,
                "神经外科患者往返高压氧舱的接送与交接。",
                "跨科室转运的交接凭据，核心是时间、护送人、途中病情变化与双方签字。",
                "medical.neurosurgery-hyperbaric-transfer"),

            new SpecialtyFormEntry(
                "code-blue", "Code Blue 抢救记录本",
                SpecialtyFormKind.EventTriggered, SpecialtyFormStatus.Implemented,
                "院内急救团队到达后的抢救过程记录。",
                "由抢救事件触发、以分钟计的高密度时间轴，事后补记，格式与常规护理记录完全不同。",
                "medical.code-blue"),

            new SpecialtyFormEntry(
                "transfusion-reaction", "输血反应记录单",
                SpecialtyFormKind.EventTriggered, SpecialtyFormStatus.Implemented,
                "输血过程中出现发热、寒战等反应时的处置与复测。",
                "由不良反应事件触发，要把体温趋势、停输血时刻、标本送检与用药串在同一条时间线上。",
                "medical.transfusion-reaction"),

            new SpecialtyFormEntry(
                "pain-assessment", "初步/持续疼痛记录表",
                SpecialtyFormKind.Specialty, SpecialtyFormStatus.Implemented,
                "术后、肿瘤或慢性疼痛患者的 NRS 评分与干预追踪。",
                "疼痛没有客观检验指标，只能靠量表评分与干预前后对比，静息痛与活动痛要分开记。",
                "medical.pain-assessment"),

            new SpecialtyFormEntry(
                "pca", "患者自控镇痛（PCA）记录单",
                SpecialtyFormKind.DeviceOutput, SpecialtyFormStatus.Implemented,
                "PCA 镇痛泵使用期间的自控按压与给药记录。",
                "数据主要来自镇痛泵日志：按压次数、实际进药量与锁定时间是核心，属设备输出型。",
                "medical.pca"),

            new SpecialtyFormEntry(
                "apache-ii", "加護病房疾病嚴重程度評估表（APACHE II）",
                SpecialtyFormKind.DeviceOutput, SpecialtyFormStatus.Implemented,
                "ICU 入科 24 小时内的 APACHE II 评分。",
                "由一组生理参数计算总分，是评分算法的输出，不是随时间变化的趋势。",
                "medical.apache-ii"),

            new SpecialtyFormEntry(
                "surgical-safety-checklist", "手术护理查核單/手术安全核查表",
                SpecialtyFormKind.EventTriggered, SpecialtyFormStatus.Implemented,
                "手术患者麻醉前、切皮前、离室前的三方核查。",
                "只在手术事件前后三个固定时点使用，勾选与三方签字是核心，时间轴意义弱。",
                "medical.surgical-safety-checklist"),

            new SpecialtyFormEntry(
                "tcm-syndrome-nursing", "中医专病辨证施护护理记录单",
                SpecialtyFormKind.Specialty, SpecialtyFormStatus.Implemented,
                "中医病房按证型实施辨证施护的过程记录。",
                "以证型、治则、施护措施等叙述性内容为主，不是结构化数值。",
                "medical.tcm-syndrome-nursing"),

            new SpecialtyFormEntry(
                "cancer-psychology-consult", "癌症心理服务会诊回覆",
                SpecialtyFormKind.DepartmentOperations, SpecialtyFormStatus.Implemented,
                "肿瘤患者心理会诊的申请与回覆。",
                "跨科室会诊的往来文书，核心是问题、评估、建议与签名，属边界事务。",
                "medical.cancer-psychology-consult"),

            new SpecialtyFormEntry(
                "body-composition", "健检身体组成分析检查报告",
                SpecialtyFormKind.DeviceOutput, SpecialtyFormStatus.Implemented,
                "健康体检的人体成分分析报告。",
                "由体成分仪直接输出，指标多、以参考区间判读为主。",
                "medical.body-composition"),

            new SpecialtyFormEntry(
                "limb-blood-flow", "四肢血流探测及压力测量检查报告",
                SpecialtyFormKind.DeviceOutput, SpecialtyFormStatus.Implemented,
                "四肢动静脉血流探测与节段压力测量。",
                "多通道设备输出的分段测量值，需要左右肢对照，不是护理趋势图。",
                "medical.limb-blood-flow"),

            new SpecialtyFormEntry(
                "inventory-book", "点物本",
                SpecialtyFormKind.DepartmentOperations, SpecialtyFormStatus.Implemented,
                "病区急救车、毒麻药品、备用物资的班班清点。",
                "科室内部物资台账，按班次清点与签名，与患者无关。",
                "medical.inventory-book"),

            new SpecialtyFormEntry(
                "workload-book", "工作量统计本",
                SpecialtyFormKind.DepartmentOperations, SpecialtyFormStatus.Implemented,
                "护理单元每日治疗、护理操作的工作量统计。",
                "科室运营统计，按日累计，用于绩效与排班。",
                "medical.workload-book"),

            new SpecialtyFormEntry(
                "adverse-event-book", "不良事件登记本",
                SpecialtyFormKind.DepartmentOperations, SpecialtyFormStatus.Implemented,
                "护理不良事件的登记、分级与整改追踪。",
                "事件台账，强调编号、分级、原因分析与整改闭环。",
                "medical.adverse-event-book"),

            new SpecialtyFormEntry(
                "postmortem-care", "尸体护理/遗体转运记录",
                SpecialtyFormKind.DepartmentOperations, SpecialtyFormStatus.Implemented,
                "患者死亡后的尸体护理与遗体转运交接。",
                "终末处置与遗体交接的边界事务，一次性、以核对与签名为核心。",
                "medical.postmortem-care"),

            new SpecialtyFormEntry(
                "organ-donation", "人体器官捐献过程记录表/器官分配转运记录单",
                SpecialtyFormKind.EventTriggered, SpecialtyFormStatus.Implemented,
                "公民逝世后器官捐献的评估、获取、分配与转运。",
                "由捐献事件触发、跨机构多环节，时间与责任人链条是文书核心。",
                "medical.organ-donation"),
        };

        /// <summary>全部 19 张单据，按临床常见程度排列。</summary>
        public static IReadOnlyList<SpecialtyFormEntry> All { get; } = Entries;

        public static IEnumerable<SpecialtyFormEntry> ByKind(SpecialtyFormKind kind)
        {
            List<SpecialtyFormEntry> result = new List<SpecialtyFormEntry>();
            foreach (SpecialtyFormEntry entry in Entries)
            {
                if (entry.Kind == kind)
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        public static IEnumerable<SpecialtyFormEntry> ByStatus(SpecialtyFormStatus status)
        {
            List<SpecialtyFormEntry> result = new List<SpecialtyFormEntry>();
            foreach (SpecialtyFormEntry entry in Entries)
            {
                if (entry.Status == status)
                {
                    result.Add(entry);
                }
            }
            return result;
        }

        /// <summary>按稳定标识查找；找不到返回 null。</summary>
        public static SpecialtyFormEntry? Find(string key)
        {
            foreach (SpecialtyFormEntry entry in Entries)
            {
                if (string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
            return null;
        }

        public static string KindText(SpecialtyFormKind kind)
        {
            switch (kind)
            {
                case SpecialtyFormKind.Specialty: return "① 专科专属型";
                case SpecialtyFormKind.EventTriggered: return "② 事件触发型";
                case SpecialtyFormKind.DeviceOutput: return "③ 设备/检查输出型";
                case SpecialtyFormKind.DepartmentOperations: return "④ 科室运营/边界事务型";
                default: return kind.ToString();
            }
        }

        public static string StatusText(SpecialtyFormStatus status)
        {
            return status == SpecialtyFormStatus.Implemented ? "已实现" : "待排期";
        }
    }
}
