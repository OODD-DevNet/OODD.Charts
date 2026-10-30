using System;
using System.Collections.Generic;

namespace OODD.Charts
{
    /// <summary>一段连续的分组区间，用于分页。</summary>
    public readonly struct GroupRange
    {
        public GroupRange(int start, int count)
        {
            Start = start;
            Count = count;
        }

        public int Start { get; }
        public int Count { get; }
        public int EndExclusive => Start + Count;

        public override string ToString() => "[" + Start + "," + EndExclusive + ")";
    }

    /// <summary>
    /// 分页切分器。长表单（如 30 天体温单）按分组（天）切成多页，
    /// 每页用 <c>document.Subset(range)</c> 生成一份子文档再建 <see cref="Chart"/>，
    /// 页眉带因此天然逐页重复。
    /// </summary>
    public static class ChartPager
    {
        /// <summary>
        /// 把 0..<paramref name="totalGroups"/> 切成每页 <paramref name="groupsPerPage"/> 组。
        /// <paramref name="groupsPerPage"/> 为 0 或不小于总数时返回单页。
        /// </summary>
        public static List<GroupRange> SplitGroups(int totalGroups, int groupsPerPage)
        {
            List<GroupRange> pages = new List<GroupRange>();
            if (totalGroups <= 0)
            {
                return pages;
            }
            if (groupsPerPage <= 0 || groupsPerPage >= totalGroups)
            {
                pages.Add(new GroupRange(0, totalGroups));
                return pages;
            }

            for (int start = 0; start < totalGroups; start += groupsPerPage)
            {
                int count = Math.Min(groupsPerPage, totalGroups - start);
                pages.Add(new GroupRange(start, count));
            }
            return pages;
        }
    }
}
