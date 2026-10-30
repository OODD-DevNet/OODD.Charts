using System;
using System.Collections.Generic;
using System.Globalization;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 一次版面求解的完整结果。所有落点都必须经由此对象取，
    /// 绘制代码里不允许出现字面量坐标——这是版面可被自动断言的前提。
    /// </summary>
    public sealed class ChartGeometry
    {
        private readonly Dictionary<string, LinearAxis> _axes;
        private readonly Dictionary<string, CellRow> _rows;

        public ChartGeometry(
            SKRect bounds,
            SlotAxis slots,
            LinearAxis ruleAxis,
            IEnumerable<LinearAxis> valueAxes,
            Band header,
            Band dateBand,
            SKRect gridRect,
            IReadOnlyList<LabelColumn> labelColumns,
            Band cellTable,
            IEnumerable<CellRow> cellRows,
            Band footer)
        {
            Bounds = bounds;
            Slots = slots;
            RuleAxis = ruleAxis;
            Header = header;
            DateBand = dateBand;
            GridRect = gridRect;
            LabelColumns = new List<LabelColumn>(labelColumns);
            CellTable = cellTable;
            Footer = footer;

            _axes = new Dictionary<string, LinearAxis>(StringComparer.Ordinal) { { ruleAxis.Key, ruleAxis } };
            foreach (LinearAxis axis in valueAxes)
            {
                _axes[axis.Key] = axis;
            }

            CellRows = new List<CellRow>(cellRows);
            _rows = new Dictionary<string, CellRow>(StringComparer.Ordinal);
            foreach (CellRow row in CellRows)
            {
                _rows[row.Key] = row;
            }
        }

        private ChartGeometry(SKRect bounds, SKRect gridRect, object layout)
        {
            Bounds = bounds;
            GridRect = gridRect;
            Layout = layout;
            LabelColumns = new List<LabelColumn>();
            CellRows = new List<CellRow>();
            _axes = new Dictionary<string, LinearAxis>(StringComparer.Ordinal);
            _rows = new Dictionary<string, CellRow>(StringComparer.Ordinal);
        }

        /// <summary>
        /// 领域模板自己的版面对象。三测单类模板为 null；非表格式模板（雷达、级配曲线、三角图…）
        /// 由 <see cref="ForLayout"/> 挂上自己的 Layout，绘制与命中测试都从它取落点。
        /// </summary>
        public object? Layout { get; }

        /// <summary>取回领域版面；类型不符或为空返回 null。</summary>
        public T? LayoutOf<T>() where T : class
        {
            return Layout as T;
        }

        /// <summary>
        /// 构造一个"只带外接矩形 + 领域版面"的几何，替代"九件套塞假值"。
        /// <para>
        /// 这样构造出的实例里，<see cref="Slots"/>、<see cref="RuleAxis"/>、<see cref="Header"/>、
        /// <see cref="DateBand"/>、<see cref="CellTable"/>、<see cref="Footer"/> 一律为 null，
        /// <see cref="LabelColumns"/> 与 <see cref="CellRows"/> 为空集合——非表格式域不该被要求
        /// 编造一条不存在的列轴或刻度轴。取用它们会抛 <see cref="InvalidOperationException"/> 并指明原因，
        /// 而不是留下一串假坐标等着被断言。
        /// </para>
        /// </summary>
        /// <param name="bounds">整张表单的外接矩形（96dpi 逻辑像素），录制与导出都以它为准。</param>
        /// <param name="gridRect">主绘图区矩形；没有"网格"概念时可传 <see cref="SKRect.Empty"/>。</param>
        /// <param name="layout">领域版面对象，不能为 null——为 null 就该用表格式构造器。</param>
        public static ChartGeometry ForLayout(SKRect bounds, SKRect gridRect, object layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout),
                    "ForLayout 必须挂一个领域版面对象；没有版面对象请用 ChartGeometry 的表格式构造器。");
            }
            if (!(bounds.Width > 0f) || !(bounds.Height > 0f))
            {
                throw new ArgumentException(
                    "外接矩形宽高必须大于 0，实际为 " + bounds.Width.ToString(CultureInfo.InvariantCulture)
                    + "×" + bounds.Height.ToString(CultureInfo.InvariantCulture) + "。", nameof(bounds));
            }

            return new ChartGeometry(bounds, gridRect, layout);
        }

        /// <summary>整张表单的外接矩形（96dpi 逻辑像素）。</summary>
        public SKRect Bounds { get; }

        /// <summary>列轴。由 <see cref="ForLayout"/> 构造时为 null。</summary>
        public SlotAxis Slots { get; } = null!;

        /// <summary>决定横向网格线位置的轴。全表单唯一。由 <see cref="ForLayout"/> 构造时为 null。</summary>
        public LinearAxis RuleAxis { get; } = null!;

        /// <summary>页眉带。由 <see cref="ForLayout"/> 构造时为 null。</summary>
        public Band Header { get; } = null!;

        /// <summary>日期带。由 <see cref="ForLayout"/> 构造时为 null。</summary>
        public Band DateBand { get; } = null!;

        public SKRect GridRect { get; }

        public IReadOnlyList<LabelColumn> LabelColumns { get; }

        /// <summary>附属表格带。由 <see cref="ForLayout"/> 构造时为 null。</summary>
        public Band CellTable { get; } = null!;

        public IReadOnlyList<CellRow> CellRows { get; }

        /// <summary>页脚带。由 <see cref="ForLayout"/> 构造时为 null。</summary>
        public Band Footer { get; } = null!;

        /// <summary>网格带内可用于放标签/事件的左边界（= 刻度标签列右缘）。</summary>
        public float GridLeft
        {
            get
            {
                RequireSlots();
                return Slots.Left;
            }
        }

        public float GridRight
        {
            get
            {
                RequireSlots();
                return Slots.Right;
            }
        }

        public LinearAxis Axis(string axisKey)
        {
            LinearAxis? axis;
            if (_axes.TryGetValue(axisKey, out axis))
            {
                return axis;
            }
            if (Layout != null)
            {
                throw new InvalidOperationException(AxisOnlyMessage(axisKey));
            }
            throw new KeyNotFoundException("未登记的数值轴：" + axisKey);
        }

        public bool TryAxis(string axisKey, out LinearAxis axis)
        {
            LinearAxis? found;
            if (_axes.TryGetValue(axisKey, out found))
            {
                axis = found;
                return true;
            }
            axis = null!;
            return false;
        }

        /// <summary>把某轴上的数据值换算成像素 y。</summary>
        public float YOf(string axisKey, double value) => Axis(axisKey).YOf(value);

        /// <summary>槽位中心 x。</summary>
        public float SlotCenterX(int slot)
        {
            RequireSlots();
            return Slots.SlotCenterX(slot);
        }

        /// <summary>组合并后的中心 x。</summary>
        public float GroupCenterX(int group)
        {
            RequireSlots();
            return Slots.GroupCenterX(group);
        }

        /// <summary>
        /// 附属表格单元格矩形。<see cref="CellRow.SplitBySlot"/> 为 false 时按分组合并。
        /// </summary>
        public SKRect CellRect(string rowKey, int slot)
        {
            RequireSlots();
            CellRow row = _rows[rowKey];
            return row.SplitBySlot
                ? Slots.SlotRect(slot, row.Rect.Top, row.Rect.Bottom)
                : Slots.GroupRect(Slots.GroupOfSlot(slot), row.Rect.Top, row.Rect.Bottom);
        }

        /// <summary>
        /// 同一合并格内第 <paramref name="occurrence"/> 条记录（共 <paramref name="occurrenceCount"/> 条）
        /// 所占的纵向切片。用于一天测两次血压这类情形。
        /// </summary>
        public SKRect CellRect(string rowKey, int slot, int occurrence, int occurrenceCount)
        {
            SKRect full = CellRect(rowKey, slot);
            if (occurrenceCount <= 1)
            {
                return full;
            }

            float h = full.Height / occurrenceCount;
            return new SKRect(full.Left, full.Top + occurrence * h, full.Right, full.Top + (occurrence + 1) * h);
        }

        /// <summary>按标签取附属行；找不到返回 null。</summary>
        public CellRow? Row(string rowKey)
        {
            CellRow? row;
            return _rows.TryGetValue(rowKey, out row) ? row : null;
        }

        private void RequireSlots()
        {
            if (Slots == null)
            {
                throw new InvalidOperationException(
                    "该 ChartGeometry 由 ForLayout 构造，版面在 " + Layout!.GetType().Name + " 上，"
                    + "没有表格式列轴（SlotAxis）。落点请经 LayoutOf<T>() 取领域版面。");
            }
        }

        private string AxisOnlyMessage(string axisKey)
        {
            return "该 ChartGeometry 由 ForLayout 构造，未登记任何 LinearAxis（请求的轴键：\"" + axisKey
                   + "\"）。刻度换算请经 LayoutOf<T>() 取领域版面。";
        }
    }
}
