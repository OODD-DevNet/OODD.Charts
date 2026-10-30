using OODD.Charts.Medical.Forms;
using OODD.Charts.Model;

namespace OODD.Charts.Medical.CodeBlue
{
    /// <summary>抢救记录附属行的稳定标识。</summary>
    public static class CodeBlueRows
    {
        public const string Rhythm = "rhythm";
        public const string Drug = "drug";
        public const string Shock = "shock";
        public const string Nurse = "nurse";

        public const string AxisVital = "vital";
    }

    /// <summary>
    /// Code Blue 抢救记录本。以抢救开始为 0 分钟、每 5 分钟一槽，
    /// 纵轴同时承载心率与收缩压（20–200），下方逐槽记录心律、用药、除颤与记录者。
    /// </summary>
    public sealed class CodeBlueDocument : TimelineFormDocument
    {
        public CodeBlueDocument()
        {
            FormTitle = "Code Blue 抢救记录本";
            AxisKey = CodeBlueRows.AxisVital;
            AxisMin = 20d;
            AxisMax = 200d;
            MinorStep = 10d;
            MajorStep = 20d;
            AxisFormat = "0";
            AxisLabel = "心率 / 收缩压";

            for (int minute = 0; minute <= 60; minute += 5)
            {
                TimeLabels.Add(minute.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            Rows.Add(new TimelineRow(CodeBlueRows.Rhythm, "心律"));
            Rows.Add(new TimelineRow(CodeBlueRows.Drug, "用药"));
            Rows.Add(new TimelineRow(CodeBlueRows.Shock, "除颤/操作"));
            Rows.Add(new TimelineRow(CodeBlueRows.Nurse, "记录者"));
        }
    }

    /// <summary>抢救记录版式：13 个 5 分钟槽，槽宽 48px。</summary>
    public sealed class CodeBlueTemplate : TimelineFormTemplate
    {
        public CodeBlueTemplate()
        {
            Tokens.SlotWidth = 48f;
            Tokens.LabelWidth = 58f;
            Tokens.GridCellHeight = 16f;
        }

        public override string Key => "medical.code-blue";

        public override string DisplayName => "Code Blue 抢救记录本";
    }
}
