using OODD.Charts.Medical.Forms;

namespace OODD.Charts.Medical.Pca
{
    /// <summary>PCA 记录附属行与纵轴的稳定标识。</summary>
    public static class PcaRows
    {
        public const string Volume = "volume";
        public const string Lock = "lock";
        public const string Nrs = "nrs";
        public const string Rescue = "rescue";
        public const string Nurse = "nurse";

        public const string AxisPresses = "presses";
    }

    /// <summary>
    /// 患者自控镇痛（PCA）记录单。以术后返回病房为 0 时、每 2 小时一槽，
    /// 纵轴为 2 小时内的有效按压次数（0–12），下方逐槽记录累计进药量、
    /// 锁定时间、疼痛评分、追加给药与记录者；泵参数写在页眉扩展字段。
    /// </summary>
    public sealed class PcaDocument : TimelineFormDocument
    {
        public PcaDocument()
        {
            FormTitle = "患者自控镇痛（PCA）记录单";
            AxisKey = PcaRows.AxisPresses;
            AxisMin = 0d;
            AxisMax = 12d;
            MinorStep = 1d;
            MajorStep = 4d;
            AxisFormat = "0";
            AxisLabel = "按压次数/2h";

            for (int hour = 0; hour <= 22; hour += 2)
            {
                TimeLabels.Add(hour.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            }

            Rows.Add(new TimelineRow(PcaRows.Volume, "进药量"));
            Rows.Add(new TimelineRow(PcaRows.Lock, "锁定时间"));
            Rows.Add(new TimelineRow(PcaRows.Nrs, "疼痛评分"));
            Rows.Add(new TimelineRow(PcaRows.Rescue, "追加给药"));
            Rows.Add(new TimelineRow(PcaRows.Nurse, "记录者"));
        }
    }

    /// <summary>PCA 记录版式：12 个 q2h 槽，槽宽 46px，按压次数细格 20px。</summary>
    public sealed class PcaTemplate : TimelineFormTemplate
    {
        public PcaTemplate()
        {
            Tokens.SlotWidth = 46f;
            Tokens.LabelWidth = 56f;
            Tokens.GridCellHeight = 20f;
        }

        public override string Key => "medical.pca";

        public override string DisplayName => "患者自控镇痛（PCA）记录单";
    }
}
