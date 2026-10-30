using System;
using System.Collections.Generic;
using OODD.Charts.Model;

namespace OODD.Charts.Tickets.WorkTicket
{
    /// <summary>
    /// 电气第一种工作票的字段键。
    /// <para>
    /// <b>D22 / docs/05 §5</b>：这里的栏位次序与联次划分是<b>结构样例</b>，票样未取证，
    /// 不宣称与 GB 26860-2011 附录 A 逐项一致。校验重点只放在"能切成 N 联、联间字段有差异"。
    /// </para>
    /// </summary>
    public static class ElectricalWorkTicketFields
    {
        public const string WorkLeader = "work-leader";
        public const string WorkClass = "work-class";
        public const string WorkMembers = "work-members";
        public const string WorkPlace = "work-place";
        public const string WorkTask = "work-task";
        public const string PlanStart = "plan-start";
        public const string PlanEnd = "plan-end";
        public const string IssueDate = "issue-date";
        public const string SafetyIssuer = "safety-issuer";
        public const string TerminateTime = "terminate-time";
        public const string PermitTime = "permit-time";
        public const string GroundingCount = "grounding-count";

        /// <summary>字段目录（按票面自上而下的声明顺序）。</summary>
        public static IList<TicketField> CreateCatalog()
        {
            return new List<TicketField>
            {
                new TicketField(WorkLeader, "工作负责人"),
                new TicketField(WorkClass, "工作班组"),
                new TicketField(WorkMembers, "工作班成员"),
                new TicketField(WorkPlace, "工作地点及设备双重名称"),
                new TicketField(WorkTask, "工作任务"),
                new TicketField(PlanStart, "计划开始时间"),
                new TicketField(PlanEnd, "计划结束时间"),
                new TicketField(IssueDate, "签发日期"),
                new TicketField(SafetyIssuer, "安全措施编制人"),
                new TicketField(TerminateTime, "终结时间"),
                new TicketField(PermitTime, "许可开始时间"),
                new TicketField(GroundingCount, "装设接地线组数"),
            };
        }
    }

    /// <summary>
    /// 电气第一种工作票文档。字段、措施与联次可见性都是视图数据：库不做任何"工作票是否合格"的判定。
    /// </summary>
    public sealed class ElectricalWorkTicketDocument : ChartDocument
    {
        /// <summary>构造一张空白工作票（字段齐全、值为空，措施为空）。</summary>
        public ElectricalWorkTicketDocument()
        {
            FormTitle = "电气第一种工作票";
            Fields = ElectricalWorkTicketFields.CreateCatalog();
            Measures = new List<TicketMeasureRow>();
            Signatures = new List<string> { "工作票签发人", "工作负责人", "许可人" };
        }

        /// <summary>单位名（落在编号行左侧）。</summary>
        public string? UnitName { get; set; }

        /// <summary>票号（落在编号行右侧）。</summary>
        public string? SerialNo { get; set; }

        /// <summary>字段目录（键→标签/值）。</summary>
        public IList<TicketField> Fields { get; }

        /// <summary>安全措施行；行数决定 AppendableRowGroup 的行数，进而决定整页高。</summary>
        public IList<TicketMeasureRow> Measures { get; }

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
        public ElectricalWorkTicketDocument With(string key, string? value)
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
    }
}
