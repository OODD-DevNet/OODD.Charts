using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Tickets.WorkTicket
{
    /// <summary>
    /// 八类特殊作业票的类别。类别只决定票面副标题与默认票名，<b>不</b>内置任何
    /// "该类作业必须有哪些措施"的标准数据表（docs/06 §5 D21）：措施行由文档逐条给出。
    /// </summary>
    public enum HotWorkCategory
    {
        /// <summary>动火作业。</summary>
        HotWork = 0,

        /// <summary>受限空间作业。</summary>
        ConfinedSpace = 1,

        /// <summary>盲板抽堵作业。</summary>
        BlindPlate = 2,

        /// <summary>高处作业。</summary>
        WorkingAtHeight = 3,

        /// <summary>吊装作业。</summary>
        Lifting = 4,

        /// <summary>临时用电作业。</summary>
        TemporaryPower = 5,

        /// <summary>断路作业。</summary>
        RoadClosure = 6,

        /// <summary>动土作业。</summary>
        Excavation = 7,
    }

    /// <summary>
    /// 八类特殊作业的枚举与中文名。中文名是<b>类别标识</b>，不是对标准条文的复述；
    /// 类别次序按 GB 30871-2022 的常见列举顺序，但票面栏位与措施并未按该标准校准（D22）。
    /// </summary>
    public static class HotWorkCategories
    {
        private static readonly HotWorkCategory[] AllCategories =
        {
            HotWorkCategory.HotWork,
            HotWorkCategory.ConfinedSpace,
            HotWorkCategory.BlindPlate,
            HotWorkCategory.WorkingAtHeight,
            HotWorkCategory.Lifting,
            HotWorkCategory.TemporaryPower,
            HotWorkCategory.RoadClosure,
            HotWorkCategory.Excavation,
        };

        private static readonly string[] Labels =
        {
            "动火作业",
            "受限空间作业",
            "盲板抽堵作业",
            "高处作业",
            "吊装作业",
            "临时用电作业",
            "断路作业",
            "动土作业",
        };

        /// <summary>八类全部（只读数组，顺序稳定）。</summary>
        public static IReadOnlyList<HotWorkCategory> All
        {
            get { return AllCategories; }
        }

        /// <summary>取类别的中文名。</summary>
        /// <param name="category">作业类别。</param>
        public static string Label(HotWorkCategory category)
        {
            int index = (int)category;
            if (index < 0 || index >= Labels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(category),
                    "未知的特殊作业类别：" + index + "。");
            }
            return Labels[index];
        }
    }

    /// <summary>
    /// 八类特殊作业票的字段键。
    /// <para>
    /// <b>D22 / docs/05 §5</b>：栏位次序与联次划分是结构样例，票样未取证，
    /// 不宣称与 GB 30871-2022 附录 A 逐项一致。校验重点只放在"能切成 N 联、联间字段有差异"。
    /// </para>
    /// </summary>
    public static class HotWorkTicketFields
    {
        public const string WorkUnit = "work-unit";
        public const string WorkPlace = "work-place";
        public const string WorkContent = "work-content";
        public const string WorkLeader = "work-leader";
        public const string Workers = "workers";
        public const string Guardian = "guardian";
        public const string PlanStart = "plan-start";
        public const string PlanEnd = "plan-end";
        public const string WorkLevel = "work-level";
        public const string GasTest = "gas-test";
        public const string Approver = "approver";

        /// <summary>字段目录（按票面自上而下的声明顺序）。</summary>
        public static IList<TicketField> CreateCatalog()
        {
            return new List<TicketField>
            {
                new TicketField(WorkUnit, "作业单位"),
                new TicketField(WorkPlace, "作业地点"),
                new TicketField(WorkContent, "作业内容"),
                new TicketField(WorkLeader, "作业负责人"),
                new TicketField(Workers, "作业人员"),
                new TicketField(Guardian, "监护人"),
                new TicketField(PlanStart, "计划开始时间"),
                new TicketField(PlanEnd, "计划结束时间"),
                new TicketField(WorkLevel, "作业级别"),
                new TicketField(GasTest, "气体检测结果"),
                new TicketField(Approver, "审批人"),
            };
        }
    }

    /// <summary>
    /// 八类特殊作业票文档。类别只换副标题；措施与危害辨识由文档给出，库不内置标准数据表。
    /// </summary>
    public sealed class HotWorkTicketDocument : ChartDocument
    {
        /// <summary>构造一张空白特殊作业票（动火作业，字段齐全、值为空）。</summary>
        public HotWorkTicketDocument()
        {
            FormTitle = "特殊作业票";
            Fields = HotWorkTicketFields.CreateCatalog();
            Measures = new List<TicketMeasureRow>();
            Hazards = new List<string>();
            Signatures = new List<string> { "作业负责人", "监护人", "审批人" };
        }

        /// <summary>作业类别（八类之一）。</summary>
        public HotWorkCategory Category { get; set; } = HotWorkCategory.HotWork;

        /// <summary>单位名（落在编号行左侧）。</summary>
        public string? UnitName { get; set; }

        /// <summary>票号（落在编号行右侧）。</summary>
        public string? SerialNo { get; set; }

        /// <summary>字段目录（键→标签/值）。</summary>
        public IList<TicketField> Fields { get; }

        /// <summary>安全措施行；行数决定 AppendableRowGroup 的行数，进而决定整页高。</summary>
        public IList<TicketMeasureRow> Measures { get; }

        /// <summary>危害辨识条目；行数同样进版面高度。</summary>
        public IList<string> Hazards { get; }

        /// <summary>签名列标签。</summary>
        public IList<string> Signatures { get; }

        /// <summary>措施区小标题。</summary>
        public string SectionTitle { get; set; } = "安全措施";

        /// <summary>印章状态（已执行 / 作废 / 未执行）。</summary>
        public WorkTicketStampState Stamp { get; set; } = WorkTicketStampState.NotExecuted;

        /// <summary>章内文字；null 时按 <see cref="Stamp"/> 取默认值。</summary>
        public string? StampText { get; set; }

        /// <summary>按字段键取值（找不到返回 null）。</summary>
        /// <param name="key">字段键。</param>
        public TicketField? Field(string key)
        {
            foreach (TicketField field in Fields)
            {
                if (string.Equals(field.Key, key, StringComparison.Ordinal))
                {
                    return field;
                }
            }
            return null;
        }

        /// <summary>按字段键写值；键不存在时追加一个新字段（标签即键），避免静默丢失。</summary>
        /// <param name="key">字段键。</param>
        /// <param name="value">字段值。</param>
        public HotWorkTicketDocument With(string key, string? value)
        {
            TicketField? field = Field(key);
            if (field == null)
            {
                Fields.Add(new TicketField(key, key, value));
            }
            else
            {
                field.Value = value;
            }
            return this;
        }

        /// <summary>副标题文字："作业类别：" + 类别中文名。</summary>
        public string CategoryText
        {
            get { return "作业类别：" + HotWorkCategories.Label(Category); }
        }
    }
}
