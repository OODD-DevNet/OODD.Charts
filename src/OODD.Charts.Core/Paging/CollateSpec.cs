using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts
{
    /// <summary>
    /// 一联的定义：一式多联凭证（死亡医学证明四联、发血单一式两份、工作票多联次、
    /// 增值税发票三/四联）中单独一联的标识、联名与字段可见性。
    /// </summary>
    public sealed class CopyDef
    {
        /// <summary>切分方向上的尺寸容差（浮点比较一律用显式容差，不用 ==）。</summary>
        internal const float WidthEpsilon = 0.0001f;

        private readonly HashSet<string> _fieldSet;

        /// <summary>构造一联。</summary>
        /// <param name="key">稳定标识，如 "hospital-stub"。</param>
        /// <param name="label">联名（竖排显示，如"单位存根"）。</param>
        /// <param name="width">该联的版面宽（96dpi 逻辑像素）。</param>
        /// <param name="visibleFields">该联要出现的字段键集合。</param>
        /// <param name="extraFields">该联**独有**的字段键（相对其他联多出来的部分，如殡仪馆联的"殡葬热线"）。</param>
        public CopyDef(string key, string label, float width,
                       IEnumerable<string> visibleFields, IEnumerable<string> extraFields)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (key.Length == 0) throw new ArgumentException("联 key 不能为空。", nameof(key));
            if (label == null) throw new ArgumentNullException(nameof(label));
            if (width <= WidthEpsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(width),
                    "联 \"" + key + "\" 的宽必须大于 0，实际为 " + width.ToString(System.Globalization.CultureInfo.InvariantCulture) + "。");
            }

            Key = key;
            Label = label;
            Width = width;

            List<string> visible = ToList(visibleFields, nameof(visibleFields), key);
            List<string> extra = ToList(extraFields, nameof(extraFields), key);

            // "独有"字段不得同时出现在共有字段里：这是防止联间差异被写错的关键约束，
            // 也让 ShowsField 可以安全地取并集。相交就当场抛，不留到绘制期。
            List<string> conflicts = new List<string>();
            HashSet<string> visibleSet = new HashSet<string>(visible, StringComparer.Ordinal);
            foreach (string field in extra)
            {
                if (visibleSet.Contains(field)) conflicts.Add(field);
            }
            if (conflicts.Count > 0)
            {
                throw new ArgumentException(
                    "联 \"" + key + "\" 的 extraFields 与 visibleFields 必须不相交，冲突字段：" +
                    string.Join(", ", conflicts) + "。", nameof(extraFields));
            }

            // 只读包装统一用 List<T>.AsReadOnly()（ReadOnlyCollection<T> 在三套 TFM 都实现 IReadOnlyList<T>）。
            VisibleFields = visible.AsReadOnly();
            ExtraFields = extra.AsReadOnly();

            _fieldSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (string field in visible) _fieldSet.Add(field);
            foreach (string field in extra) _fieldSet.Add(field);
        }

        /// <summary>稳定标识（Ordinal 唯一）。</summary>
        public string Key { get; }

        /// <summary>联名，版面层逐字竖排显示。</summary>
        public string Label { get; }

        /// <summary>该联在切分方向上的尺寸（96dpi 逻辑像素）：横排即宽，纵叠即高。</summary>
        public float Width { get; }

        /// <summary>该联要出现的共有字段键。</summary>
        public IReadOnlyList<string> VisibleFields { get; }

        /// <summary>该联独有的字段键（与其他联的差异部分）。</summary>
        public IReadOnlyList<string> ExtraFields { get; }

        /// <summary>该联是否显示某字段。visibleFields 与 extraFields 的并集判定，Ordinal 比较。</summary>
        /// <param name="fieldKey">字段键；null 或空串视为不显示。</param>
        /// <returns>该联显示此字段返回 true。</returns>
        public bool ShowsField(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) return false;
            return _fieldSet.Contains(fieldKey);
        }

        /// <summary>返回该联的联名与联宽，便于日志/断言（InvariantCulture，跨 TFM 逐字一致）。</summary>
        public override string ToString()
        {
            return Key + "(" + Label + ", " +
                   Width.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        private static List<string> ToList(IEnumerable<string> source, string paramName, string copyKey)
        {
            if (source == null) throw new ArgumentNullException(paramName);
            List<string> list = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string field in source)
            {
                if (string.IsNullOrEmpty(field))
                {
                    throw new ArgumentException(
                        "联 \"" + copyKey + "\" 的字段集合含空字段键（\"\" 或 null）。", paramName);
                }
                // 同一联里重复声明的字段键按首次出现去重（保持声明顺序），免得绘制时同一字段画两遍。
                if (seen.Add(field)) list.Add(field);
            }
            return list;
        }

        /// <summary>把该联的全部字段键（共有 + 独有，保持声明顺序）读出来。</summary>
        internal List<string> AllFieldKeys()
        {
            List<string> all = new List<string>(VisibleFields.Count + ExtraFields.Count);
            all.AddRange(VisibleFields);
            all.AddRange(ExtraFields);
            return all;
        }
    }

    /// <summary>联与联之间的骑缝/裁切形式。</summary>
    public enum PerforationStyle
    {
        /// <summary>无骑缝（各联独立排版，靠裁刀分开）。</summary>
        None,

        /// <summary>虚线裁切线（印刷裁切线，如发血单上下两联）。</summary>
        DashedCut,

        /// <summary>刀模孔（骑缝打孔，如增值税发票骑缝章位置）。</summary>
        DiceCut,
    }

    /// <summary>多联的排布方向。</summary>
    public enum CollateDirection
    {
        /// <summary>纵向堆叠：各联沿 y 累加切分（上下联，裁切线水平）。</summary>
        VerticalStack,

        /// <summary>横向条排：各联沿 x 累加切分（左右联，骑缝线竖直）。</summary>
        HorizontalStrip,
    }

    /// <summary>
    /// 一份版面切成 N 联的规格。多联次是**版面层**能力：骑缝线位置、裁切位置、联间字段差异
    /// 只能在版面求解里算准，放进导出层会绕过"所有产物来自同一个 SKPicture"这条不变量。
    /// </summary>
    public sealed class CollateSpec
    {
        /// <summary>联数上限。超出即视为误用（本引擎不支持"无限联"的流式版式）。</summary>
        public const int MaxCopies = 8;

        private readonly CopyDef[] _copies;
        private readonly List<string> _allFields;
        private readonly List<string> _commonFields;
        private readonly List<string> _differentiatingFields;

        /// <summary>构造切联规格并在当场完成配平/重名/字段冲突校验。</summary>
        /// <param name="copies">各联定义，顺序即印刷/排版顺序（第 0 联在左上）。</param>
        /// <param name="perforation">骑缝/裁切形式。</param>
        /// <param name="direction">多联排布方向。</param>
        public CollateSpec(IEnumerable<CopyDef> copies, PerforationStyle perforation, CollateDirection direction)
        {
            if (copies == null) throw new ArgumentNullException(nameof(copies));

            List<CopyDef> list = new List<CopyDef>();
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (CopyDef copy in copies)
            {
                if (copy == null) throw new ArgumentException("copies 含 null 元素。", nameof(copies));
                if (!keys.Add(copy.Key))
                {
                    throw new ArgumentException("联 key 重复：\"" + copy.Key + "\"（Ordinal 比较）。", nameof(copies));
                }
                list.Add(copy);
            }

            if (list.Count == 0) throw new ArgumentException("copies 为空：至少需要一联。", nameof(copies));
            if (list.Count > MaxCopies)
            {
                throw new ArgumentException(
                    "联数 " + list.Count + " 超过上限 " + MaxCopies + "（CollateSpec.MaxCopies）。", nameof(copies));
            }

            _copies = list.ToArray();
            Perforation = perforation;
            Direction = direction;

            _allFields = CollectAllFields(_copies);
            _commonFields = CollectCommonFields(_copies, _allFields);
            _differentiatingFields = CollectDifferentiating(_allFields, _commonFields);
        }

        /// <summary>各联定义，只读。</summary>
        public IReadOnlyList<CopyDef> Copies => _copies;

        /// <summary>联数。</summary>
        public int Count => _copies.Length;

        /// <summary>骑缝/裁切形式。</summary>
        public PerforationStyle Perforation { get; }

        /// <summary>多联排布方向。</summary>
        public CollateDirection Direction { get; }

        /// <summary>
        /// 全部联在切分方向上的尺寸之和，用于校验联宽配平。
        /// <see cref="CollateDirection.HorizontalStrip"/> 下即各联宽之和，
        /// <see cref="CollateDirection.VerticalStack"/> 下即各联高（仍记在 <see cref="CopyDef.Width"/> 上）之和。
        /// </summary>
        public float TotalExtent
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < _copies.Length; i++) sum += _copies[i].Width;
                return sum;
            }
        }

        /// <summary>联宽之和与给定总区域该方向尺寸之差的绝对值（联未配平时的偏差量，供断言用）。</summary>
        /// <param name="total">版面总区域。</param>
        /// <returns>偏差量（像素）；配平时应接近 0。</returns>
        public float ExtentDeviation(SKRect total)
        {
            float available = Direction == CollateDirection.HorizontalStrip ? total.Width : total.Height;
            float diff = TotalExtent - available;
            return diff < 0f ? -diff : diff;
        }

        /// <summary>
        /// 第 <paramref name="index"/> 联在总区域里的版面矩形。
        /// 沿切分方向累加联宽；<b>最后一联的远端边界直接取 total 的边缘</b>而不是"累加值"，
        /// 否则 0.1f 这类不可精确表示的联宽累加误差会传导到末联，让最右/最下一联与总边缘差半个像素，
        /// 三套 TFM 的栅格化结果就不再逐字节一致。
        /// </summary>
        /// <param name="index">联序号，从 0 起。</param>
        /// <param name="total">版面总区域。</param>
        /// <returns>该联的矩形。</returns>
        public SKRect CopyRect(int index, SKRect total)
        {
            if (index < 0 || index >= _copies.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "联序号 " + index + " 越界（共 " + _copies.Length + " 联）。");
            }

            float near = 0f;
            for (int i = 0; i < index; i++) near += _copies[i].Width;

            if (Direction == CollateDirection.HorizontalStrip)
            {
                float left = total.Left + near;
                float right = index == _copies.Length - 1 ? total.Right : left + _copies[index].Width;
                return new SKRect(left, total.Top, right, total.Bottom);
            }
            else
            {
                float top = total.Top + near;
                float bottom = index == _copies.Length - 1 ? total.Bottom : top + _copies[index].Width;
                return new SKRect(total.Left, top, total.Right, bottom);
            }
        }

        /// <summary>相邻两联之间的骑缝线位置（像素）。第一联之前/最后一联之后没有缝，返回 null；<see cref="PerforationStyle.None"/> 也返回 null。</summary>
        /// <param name="boundaryIndex">缝编号：位于第 boundaryIndex-1 联与第 boundaryIndex 联之间，有效取值 1..Count-1，其余返回 null。</param>
        /// <param name="total">版面总区域。</param>
        /// <returns>缝在切分方向上的坐标；无缝返回 null。</returns>
        public float? PerforationAt(int boundaryIndex, SKRect total)
        {
            if (Perforation == PerforationStyle.None) return null;
            if (boundaryIndex <= 0 || boundaryIndex >= _copies.Length) return null;

            // 与 CopyRect 用同一套累加+末联吃剩余的规则，保证缝线恰好落在两联共享的那条边上。
            SKRect next = CopyRect(boundaryIndex, total);
            return Direction == CollateDirection.HorizontalStrip ? next.Left : (float?)next.Top;
        }

        /// <summary>出现在**任意**一联上的字段键全集（去重，保持首次出现顺序）。</summary>
        public IReadOnlyList<string> AllFields => _allFields;

        /// <summary>在**所有**联上都出现的字段键全集（去重，保持首次出现顺序）。</summary>
        public IReadOnlyList<string> CommonFields => _commonFields;

        /// <summary>只在一部分联上出现的字段键（差异字段），供"每联只画自己的字段"断言取用。</summary>
        public IReadOnlyList<string> DifferentiatingFields => _differentiatingFields;

        /// <summary>按联 key 找联序号（Ordinal）；不存在返回 -1。</summary>
        /// <param name="key">联 key。</param>
        /// <returns>联序号，从 0 起；未找到返回 -1。</returns>
        public int IndexOfCopy(string key)
        {
            if (string.IsNullOrEmpty(key)) return -1;
            for (int i = 0; i < _copies.Length; i++)
            {
                if (string.Equals(_copies[i].Key, key, StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        /// <summary>按联 key 找联定义（Ordinal）；不存在返回 null。</summary>
        /// <param name="key">联 key。</param>
        /// <returns>联定义或 null。</returns>
        public CopyDef? FindCopy(string key)
        {
            int index = IndexOfCopy(key);
            return index < 0 ? null : _copies[index];
        }

        private static List<string> CollectAllFields(CopyDef[] copies)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CopyDef copy in copies)
            {
                foreach (string field in copy.AllFieldKeys())
                {
                    if (seen.Add(field)) result.Add(field);
                }
            }
            return result;
        }

        private static List<string> CollectCommonFields(CopyDef[] copies, List<string> allFields)
        {
            List<string> result = new List<string>();
            foreach (string field in allFields)
            {
                bool inEvery = true;
                foreach (CopyDef copy in copies)
                {
                    if (!copy.ShowsField(field)) { inEvery = false; break; }
                }
                if (inEvery) result.Add(field);
            }
            return result;
        }

        private static List<string> CollectDifferentiating(List<string> allFields, List<string> commonFields)
        {
            HashSet<string> common = new HashSet<string>(commonFields, StringComparer.Ordinal);
            List<string> result = new List<string>();
            foreach (string field in allFields)
            {
                if (!common.Contains(field)) result.Add(field);
            }
            return result;
        }
    }
}
