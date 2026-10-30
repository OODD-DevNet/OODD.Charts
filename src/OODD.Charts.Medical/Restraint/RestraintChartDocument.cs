using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Medical.Restraint
{
    /// <summary>保护性约束巡视记录单附属行的稳定标识。</summary>
    public static class RestraintFields
    {
        public const string Position = "position";
        public const string Skin = "skin";
        public const string Circulation = "circulation";
        public const string Release = "release";
        public const string BasicCare = "basic-care";
        public const string Nurse = "nurse";
        public const string Note = "note";

        public static IList<string> DefaultOrder { get; } = new List<string>
        {
            Position, Skin, Circulation, Release, BasicCare, Nurse, Note,
        };

        public static string DefaultLabel(string key)
        {
            switch (key)
            {
                case Position: return "约束部位";
                case Skin: return "皮肤情况";
                case Circulation: return "末梢血运";
                case Release: return "定时松解";
                case BasicCare: return "饮食/排泄";
                case Nurse: return "巡视者";
                case Note: return "备注";
                default: return key;
            }
        }
    }

    /// <summary>一段实际约束时段，画在记录单顶部的时段条里。</summary>
    public sealed class RestraintEpisode
    {
        public RestraintEpisode(int startSlot, int endSlot, string? text = null)
        {
            if (endSlot < startSlot)
            {
                throw new ArgumentOutOfRangeException(nameof(endSlot), "约束结束槽不得早于开始槽。");
            }
            StartSlot = startSlot;
            EndSlot = endSlot;
            Text = text;
        }

        /// <summary>开始槽（含）。</summary>
        public int StartSlot { get; set; }

        /// <summary>结束槽（不含）。</summary>
        public int EndSlot { get; set; }

        /// <summary>条内文字，如“双上肢”。</summary>
        public string? Text { get; set; }

        public SKColor? Color { get; set; }

        /// <summary>紧急约束：用强调色标出，便于交接班快速识别。</summary>
        public bool IsEmergency { get; set; }

        public int Length => Math.Max(0, EndSlot - StartSlot);
    }

    /// <summary>
    /// 保护性约束巡视记录单。一槽 = 1 小时，一组 = 1 天（24 槽）；
    /// 顶部时段条画实际约束区间，下方逐小时填写约束部位、皮肤、血运、松解与巡视者。
    /// </summary>
    public sealed class RestraintChartDocument : ChartDocument
    {
        public RestraintChartDocument()
        {
            FormTitle = "保护性约束巡视记录单";
            Days = new List<ChartGroup>();
            Episodes = new List<RestraintEpisode>();
            Cells = new List<CellValue>();
            RowOrder = new List<string>(RestraintFields.DefaultOrder);
        }

        public IList<ChartGroup> Days { get; }
        public IList<RestraintEpisode> Episodes { get; }
        public IList<CellValue> Cells { get; }
        public IList<string> RowOrder { get; }

        /// <summary>一天记录多少个时点。约束巡视默认每小时一次，即 24 槽。</summary>
        public int SlotsPerDay { get; set; } = 24;

        public string[] WeekNames { get; set; } = { "日", "一", "二", "三", "四", "五", "六" };

        public string DateFormat { get; set; } = "M-d";

        /// <summary>取一段分组生成子文档，用于分页；跨页的约束时段按页裁切。</summary>
        public RestraintChartDocument Subset(int startGroup, int groupCount)
        {
            RestraintChartDocument copy = CloneShell();
            int slotsPerGroup = Math.Max(1, SlotsPerDay);
            int firstSlot = startGroup * slotsPerGroup;
            int lastSlot = firstSlot + groupCount * slotsPerGroup;

            for (int g = startGroup; g < Math.Min(startGroup + groupCount, Days.Count); g++)
            {
                copy.Days.Add(Days[g]);
            }

            foreach (RestraintEpisode episode in Episodes)
            {
                int start = Math.Max(episode.StartSlot, firstSlot);
                int end = Math.Min(episode.EndSlot, lastSlot);
                if (end <= start)
                {
                    continue;
                }
                copy.Episodes.Add(new RestraintEpisode(start - firstSlot, end - firstSlot, episode.Text)
                {
                    Color = episode.Color,
                    IsEmergency = episode.IsEmergency,
                });
            }

            foreach (CellValue cell in Cells)
            {
                if (cell.Slot >= firstSlot && cell.Slot < lastSlot)
                {
                    copy.Cells.Add(new CellValue(cell.RowKey, cell.Slot - firstSlot, cell.Text)
                    {
                        Color = cell.Color,
                        Align = cell.Align,
                        Size = cell.Size,
                        Occurrence = cell.Occurrence,
                        OccurrenceCount = cell.OccurrenceCount,
                    });
                }
            }

            return copy;
        }

        private RestraintChartDocument CloneShell()
        {
            RestraintChartDocument copy = new RestraintChartDocument
            {
                HospitalName = HospitalName,
                FormTitle = FormTitle,
                Patient = Patient,
                HeaderRightNote = HeaderRightNote,
                DateFormat = DateFormat,
                WeekNames = WeekNames,
                SlotsPerDay = SlotsPerDay,
            };
            foreach (string row in RowOrder)
            {
                copy.RowOrder.Add(row);
            }
            foreach (LegendItem item in Legend)
            {
                copy.Legend.Add(item);
            }
            return copy;
        }
    }
}
