using System;
using System.Collections.Generic;
using System.Globalization;

namespace OODD.Charts
{
    /// <summary>一个开面（一次摊开看到的左右两页）。</summary>
    public readonly struct Spread
    {
        /// <summary>构造一个开面。</summary>
        /// <param name="leftLogicalPage">左半的逻辑页号；0 表示该侧是封面/封底外侧（无内容页）。</param>
        /// <param name="rightLogicalPage">右半的逻辑页号；0 表示空。</param>
        /// <param name="spreadIndex">开面序号，从 0 起。</param>
        public Spread(int leftLogicalPage, int rightLogicalPage, int spreadIndex)
        {
            LeftLogicalPage = leftLogicalPage;
            RightLogicalPage = rightLogicalPage;
            SpreadIndex = spreadIndex;
        }

        /// <summary>左半的逻辑页号（0 表示空白/封底外侧）。</summary>
        public int LeftLogicalPage { get; }

        /// <summary>右半的逻辑页号（0 表示空白）。</summary>
        public int RightLogicalPage { get; }

        /// <summary>开面序号，从 0 起，按印刷顺序排列。</summary>
        public int SpreadIndex { get; }

        /// <summary>左半是否空白。</summary>
        public bool IsEmptyOnLeft => LeftLogicalPage == 0;

        /// <summary>右半是否空白。</summary>
        public bool IsEmptyOnRight => RightLogicalPage == 0;

        /// <summary>形如 "spread#0: L=3 R=4"（InvariantCulture，三套 TFM 逐字一致）。</summary>
        public override string ToString()
        {
            return "spread#" + SpreadIndex.ToString(CultureInfo.InvariantCulture) +
                   ": L=" + LeftLogicalPage.ToString(CultureInfo.InvariantCulture) +
                   " R=" + RightLogicalPage.ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// 骑马钉/扎线装订的<b>开面配对</b>分页：逻辑页序与印刷页序不是同一个顺序。
    /// 典型场景是预防接种证——《预防接种工作规范（2023年版）》附件5-2 明文规定开面配对顺序
    /// （封二与基本信息同开面、接种记录一与二同开面……），页序非单调且"不得自行变更加印"。
    /// 与 ChartPager（按逻辑组数纵向切页）、DepthPager（按连续域值切页）是三种不同的切分依据，故并存。
    /// 本类只算页序，不算矩形，因此不依赖 SkiaSharp。
    /// </summary>
    public sealed class BookletImposition
    {
        private readonly int[] _flat;
        private readonly int[] _sheetOfPage;
        private readonly int[] _printOrderOfPage;
        private readonly Spread[] _spreads;

        /// <summary>构造鞍装配对并当场自洽校验。</summary>
        /// <param name="totalLogicalPages">内容逻辑页总数（&gt;=1）。</param>
        /// <param name="pagesPerSpread">每个开面放几页（接种证是 2：左右各一）。目前只支持 2，其他值抛异常。</param>
        public BookletImposition(int totalLogicalPages, int pagesPerSpread)
        {
            if (totalLogicalPages < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(totalLogicalPages),
                    "逻辑页总数必须 >= 1，实际为 " + totalLogicalPages.ToString(CultureInfo.InvariantCulture) + "。");
            }
            if (pagesPerSpread != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(pagesPerSpread),
                    "目前只支持 pagesPerSpread = 2（一开面左右两页），实际为 " +
                    pagesPerSpread.ToString(CultureInfo.InvariantCulture) + "。");
            }

            TotalLogicalPages = totalLogicalPages;
            PagesPerSpread = pagesPerSpread;

            // 一张纸正反各一个开面 => 每"纸"占 2*pagesPerSpread 个页位，先把总页位补齐到它的整数倍。
            int roundTo = 2 * pagesPerSpread;
            int sheets = (totalLogicalPages + roundTo - 1) / roundTo;
            int physical = sheets * roundTo;
            PhysicalPages = physical;
            SheetCount = sheets;

            // 配对规则（slot 编号 1..physical，即装订成册后的阅读页序；0 = 补出来的空白）：
            //   第 k 个开面（k 从 0 起，共 physical/pagesPerSpread 个）取两端首尾相接的一对页位
            //     a = physical - k，b = k + 1
            //   k 为偶数时（每张纸的【正面】）左大右小：(a, b)；k 为奇数时（【背面】）左小右大：(b, a)。
            //   页位编号 > totalLogicalPages 的一律落到空白（逻辑页 0）。
            // 于是 spread#0 = (physical, 1)、spread#1 = (2, physical-1)、spread#2 = (physical-2, 3)…
            // 即第一张纸正面 [末, 1]、背面 [2, 末-1]，是标准鞍装表。
            int spreadCount = physical / pagesPerSpread;
            _spreads = new Spread[spreadCount];
            _flat = new int[physical];
            _sheetOfPage = new int[totalLogicalPages + 1];
            _printOrderOfPage = new int[totalLogicalPages + 1];
            for (int page = 0; page <= totalLogicalPages; page++)
            {
                _sheetOfPage[page] = -1;
                _printOrderOfPage[page] = -1;
            }

            int cursor = 0;
            for (int k = 0; k < spreadCount; k++)
            {
                int a = Map(physical - k, totalLogicalPages);
                int b = Map(k + 1, totalLogicalPages);
                int left;
                int right;
                if ((k & 1) == 0) { left = a; right = b; }
                else { left = b; right = a; }

                _spreads[k] = new Spread(left, right, k);
                _flat[cursor++] = left;
                _flat[cursor++] = right;

                int sheet = k / 2;
                Remember(left, sheet, cursor - 2, totalLogicalPages, _sheetOfPage, _printOrderOfPage);
                Remember(right, sheet, cursor - 1, totalLogicalPages, _sheetOfPage, _printOrderOfPage);
            }

            Verify();
        }

        /// <summary>便捷入口：2 页/开面的标准骑马钉。</summary>
        /// <param name="totalLogicalPages">内容逻辑页总数（&gt;=1）。</param>
        /// <returns>配对结果。</returns>
        public static BookletImposition SaddleStitch(int totalLogicalPages)
        {
            return new BookletImposition(totalLogicalPages, 2);
        }

        /// <summary>内容逻辑页总数。</summary>
        public int TotalLogicalPages { get; }

        /// <summary>每个开面放的页数。</summary>
        public int PagesPerSpread { get; }

        /// <summary>补齐到开面整数倍后的物理页总数（含空白背面）。</summary>
        public int PhysicalPages { get; }

        /// <summary>纸张数（每张纸正反各一个开面），即 PrintSheetOf 返回值的上界 + 1。</summary>
        public int SheetCount { get; }

        /// <summary>全部开面，按<b>印刷顺序</b>排列。</summary>
        public IReadOnlyList<Spread> Spreads => _spreads;

        /// <summary>与 Spreads 等价但展平成页序列，便于逐页导出（0 表示空白页位）。</summary>
        public IReadOnlyList<int> FlattenedPrintOrder => _flat;

        /// <summary>逻辑页 → 印刷时的第几张纸（从 0 起）；不在册内抛 ArgumentOutOfRangeException。</summary>
        /// <param name="logicalPage">逻辑页号，1..<see cref="TotalLogicalPages"/>。空白页位（0）不算在册内。</param>
        /// <returns>纸张序号。</returns>
        public int PrintSheetOf(int logicalPage)
        {
            if (logicalPage < 1 || logicalPage > TotalLogicalPages)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalPage),
                    "逻辑页 " + logicalPage.ToString(CultureInfo.InvariantCulture) +
                    " 不在册内（1.." + TotalLogicalPages.ToString(CultureInfo.InvariantCulture) + "）。");
            }
            return _sheetOfPage[logicalPage];
        }

        /// <summary>逻辑页 → 印刷页序（从 1 起，与打印机输入顺序一致）。</summary>
        /// <param name="logicalPage">逻辑页号，1..<see cref="TotalLogicalPages"/>。传 0 表示空白页位：鞍装不会重复出现同一逻辑页，
        /// 空白页位可能有多个，此处返回<b>第一个</b>空白槽位的印刷序（允许，供逐页导出使用）。</param>
        /// <returns>印刷页序。</returns>
        public int PrintOrderOf(int logicalPage)
        {
            if (logicalPage < 0 || logicalPage > TotalLogicalPages)
            {
                throw new ArgumentOutOfRangeException(nameof(logicalPage),
                    "逻辑页 " + logicalPage.ToString(CultureInfo.InvariantCulture) +
                    " 超出范围（0.." + TotalLogicalPages.ToString(CultureInfo.InvariantCulture) + "）。");
            }
            if (logicalPage == 0)
            {
                for (int i = 0; i < _flat.Length; i++)
                {
                    if (_flat[i] == 0) return i + 1;
                }
                throw new InvalidOperationException("册内没有空白页位，无法为逻辑页 0 给出印刷序。");
            }
            return _printOrderOfPage[logicalPage];
        }

        /// <summary>页位编号 → 逻辑页号；超出内容页数的补位返回 0（空白）。</summary>
        private static int Map(int slot, int totalLogicalPages)
        {
            return slot <= totalLogicalPages ? slot : 0;
        }

        /// <summary>记录某逻辑页所在的纸张与印刷序；鞍装下同一逻辑页只出现一次，重复登记视为算错。</summary>
        private static void Remember(int logicalPage, int sheet, int flatIndex, int totalLogicalPages,
                                     int[] sheetOfPage, int[] printOrderOfPage)
        {
            if (logicalPage < 1 || logicalPage > totalLogicalPages) return;
            if (sheetOfPage[logicalPage] >= 0)
            {
                throw new InvalidOperationException("逻辑页 " +
                    logicalPage.ToString(CultureInfo.InvariantCulture) + " 在配对表里出现了多次，鞍装配对算法有误。");
            }
            sheetOfPage[logicalPage] = sheet;
            printOrderOfPage[logicalPage] = flatIndex + 1;
        }

        /// <summary>
        /// 自洽校验（这块极易写错，算错就当场炸，而不是版面错了没人知道）：
        /// 开面槽位数 == 物理页数；1..TotalLogicalPages 每个逻辑页恰好出现一次；
        /// 空白（0）个数 == PhysicalPages - TotalLogicalPages。
        /// </summary>
        private void Verify()
        {
            if (_spreads.Length * PagesPerSpread != PhysicalPages)
            {
                throw new InvalidOperationException(
                    "开面数 " + _spreads.Length.ToString(CultureInfo.InvariantCulture) +
                    " 乘每开面页数 " + PagesPerSpread.ToString(CultureInfo.InvariantCulture) +
                    " 不等于物理页数 " + PhysicalPages.ToString(CultureInfo.InvariantCulture) + "。");
            }

            int[] seen = new int[TotalLogicalPages + 1];
            int blanks = 0;
            for (int i = 0; i < _flat.Length; i++)
            {
                int page = _flat[i];
                if (page == 0) { blanks++; continue; }
                if (page < 0 || page > TotalLogicalPages)
                {
                    throw new InvalidOperationException("配对表含非法页号 " +
                        page.ToString(CultureInfo.InvariantCulture) + "。");
                }
                seen[page]++;
            }

            int expectedBlanks = PhysicalPages - TotalLogicalPages;
            if (blanks != expectedBlanks)
            {
                throw new InvalidOperationException(
                    "空白页位数 " + blanks.ToString(CultureInfo.InvariantCulture) +
                    " 应等于 PhysicalPages - TotalLogicalPages = " + expectedBlanks.ToString(CultureInfo.InvariantCulture) + "。");
            }

            for (int page = 1; page <= TotalLogicalPages; page++)
            {
                if (seen[page] != 1)
                {
                    throw new InvalidOperationException(
                        "逻辑页 " + page.ToString(CultureInfo.InvariantCulture) + " 在配对表中出现 " +
                        seen[page].ToString(CultureInfo.InvariantCulture) + " 次，应恰好 1 次。");
                }
            }
        }
    }
}
