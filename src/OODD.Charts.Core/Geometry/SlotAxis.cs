using System;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 两级列轴：分组（如“天”）→ 子格（如“一天中的 6 个测量时点”）。
    /// 附属表格常按组合并，本类型提供合并感知的取矩形方法。
    /// </summary>
    public sealed class SlotAxis
    {
        public SlotAxis(float left, float slotWidth, int slotsPerGroup, int groupCount)
        {
            if (slotsPerGroup <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotsPerGroup));
            }
            if (groupCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(groupCount));
            }

            Left = left;
            SlotWidth = slotWidth;
            SlotsPerGroup = slotsPerGroup;
            GroupCount = groupCount;
        }

        public float Left { get; }
        public float SlotWidth { get; }
        public int SlotsPerGroup { get; }
        public int GroupCount { get; }

        public int TotalSlots => GroupCount * SlotsPerGroup;
        public float GroupWidth => SlotWidth * SlotsPerGroup;
        public float Right => Left + TotalSlots * SlotWidth;
        public float Width => Right - Left;

        /// <summary>第 slot 个槽位的左边界 x。</summary>
        public float SlotX(int slot) => Left + slot * SlotWidth;

        /// <summary>第 group 组的左边界 x。</summary>
        public float GroupX(int group) => Left + group * GroupWidth;

        public int GroupOfSlot(int slot) => slot / SlotsPerGroup;

        public int SlotInGroup(int slot) => slot % SlotsPerGroup;

        public float SlotCenterX(int slot) => SlotX(slot) + SlotWidth / 2f;

        public float GroupCenterX(int group) => GroupX(group) + GroupWidth / 2f;

        /// <summary>单个槽位矩形。</summary>
        public SKRect SlotRect(int slot, float top, float bottom)
        {
            return new SKRect(SlotX(slot), top, SlotX(slot + 1), bottom);
        }

        /// <summary>整组（跨 SlotsPerGroup 个槽）矩形。</summary>
        public SKRect GroupRect(int group, float top, float bottom)
        {
            return new SKRect(GroupX(group), top, GroupX(group + 1), bottom);
        }

        /// <summary>从第 firstSlot 到第 lastSlot（含）的连续矩形。</summary>
        public SKRect SlotRangeRect(int firstSlot, int lastSlot, float top, float bottom)
        {
            return new SKRect(SlotX(firstSlot), top, SlotX(lastSlot + 1), bottom);
        }
    }
}
