using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.HyperbaricCompanion
{
    /// <summary>高压氧陪舱记录附属行与纵轴的稳定标识。</summary>
    public static class HyperbaricCompanionRows
    {
        public const string Persons = "persons";
        public const string Oxygen = "oxygen";
        public const string Status = "status";
        public const string Nurse = "nurse";

        public const string AxisPressure = "pressure";
    }

    /// <summary>
    /// 高压氧治疗陪舱记录单。以进舱为 0 分钟、每 10 分钟一槽，
    /// 纵轴为舱压（0–3.0 ATA），下方逐槽记录陪舱人员、吸氧方式、舱内状态与记录者。
    /// </summary>
    public sealed class HyperbaricCompanionDocument : TimelineFormDocument
    {
        public HyperbaricCompanionDocument()
        {
            FormTitle = "高压氧治疗陪舱记录单";
            AxisKey = HyperbaricCompanionRows.AxisPressure;
            AxisMin = 0d;
            AxisMax = 3.0d;
            MinorStep = 0.1d;
            MajorStep = 0.5d;
            AxisFormat = "0.0";
            AxisLabel = "舱压 (ATA)";

            for (int minute = 0; minute <= 120; minute += 10)
            {
                TimeLabels.Add(minute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            Rows.Add(new TimelineRow(HyperbaricCompanionRows.Persons, "陪舱人员"));
            Rows.Add(new TimelineRow(HyperbaricCompanionRows.Oxygen, "吸氧方式"));
            Rows.Add(new TimelineRow(HyperbaricCompanionRows.Status, "舱内状态"));
            Rows.Add(new TimelineRow(HyperbaricCompanionRows.Nurse, "记录者"));
        }
    }

    /// <summary>陪舱记录版式：13 个 10 分钟槽，槽宽 44px，舱压细分格 14px。</summary>
    public sealed class HyperbaricCompanionTemplate : TimelineFormTemplate
    {
        public HyperbaricCompanionTemplate()
        {
            Tokens.SlotWidth = 44f;
            Tokens.LabelWidth = 56f;
            Tokens.GridCellHeight = 14f;
        }

        public override string Key => "medical.hyperbaric-companion";

        public override string DisplayName => "高压氧治疗陪舱记录单";
    }
}
