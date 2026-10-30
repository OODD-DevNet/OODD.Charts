using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Geometry
{
    /// <summary>
    /// 表格类版面共用的"列边界 + 跨列取矩形"数学。
    /// <para>
    /// 存在的前提是两套求矩形口径以前各写各的：按权重分列的表头树
    /// （<see cref="HeaderTableLayout"/>）与要跨列合并的数据格（积载图、ICU 记录单这类模板）。
    /// 合并格一旦两处各算各的，列分界线就会在表头与数据行之间差半个像素，
    /// 而这种错位只有整版出图才看得见。
    /// </para>
    /// <para>
    /// <see cref="SlotAxis"/> 是<b>等宽</b>列轴（存量版面靠它，逐槽 <c>left + slot × width</c>），
    /// 不在本工具内：把它的乘法换成这里的 double 累加会改变末列边界的落点，
    /// 直接违反"存量三测单 PNG 逐字节不变"的验收（docs/07 T220）。
    /// 等宽列要跨列合并时，用 <see cref="RectOf"/> 配 <see cref="UniformBounds"/> 即可与权重列同一条口径。
    /// </para>
    /// </summary>
    public static class CellGeometry
    {
        /// <summary>浮点比较容差（像素），与版面求解其它处一致。</summary>
        private const float PixelEpsilon = 0.0001f;

        /// <summary>
        /// 按叶子权重把 [areaLeft, areaRight] 切成列边界，返回长度 = 权重数 + 1。
        /// <para>
        /// 边界用 double 累加后<b>一次性</b>转 float，且最后一条直接取 <paramref name="areaRight"/>：
        /// 0.1 这类不可精确表示的宽度逐列累加会把误差传到末列，让最右一列与区域右缘差半个像素，
        /// 三套 TFM 的栅格化就不再逐字节一致。
        /// </para>
        /// </summary>
        /// <param name="areaLeft">区域左缘（不必小于 areaRight 之外的其它约定）。</param>
        /// <param name="areaRight">区域右缘，必须大于左缘。</param>
        /// <param name="leafWeights">每个叶子列的权重，个数 ≥1，全部有限且不小于 0，总和非 0。</param>
        /// <exception cref="ArgumentException">区域宽度非正、权重个数为 0、权重含 NaN/∞/负数、
        /// 或权重总和为 0 时抛出。</exception>
        public static float[] ColumnBounds(float areaLeft, float areaRight, IReadOnlyList<double> leafWeights)
        {
            if (leafWeights == null)
            {
                throw new ArgumentNullException(nameof(leafWeights));
            }
            if (leafWeights.Count == 0)
            {
                throw new ArgumentException("列边界至少需要 1 个叶子权重。", nameof(leafWeights));
            }
            // !(x > eps) 同时挡住 NaN。
            if (!(areaRight - areaLeft > PixelEpsilon))
            {
                throw new ArgumentException(
                    "列区域宽度必须大于 0，实际为 " + (areaRight - areaLeft), nameof(areaRight));
            }

            double totalWeight = 0d;
            for (int i = 0; i < leafWeights.Count; i++)
            {
                double weight = leafWeights[i];
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0d)
                {
                    throw new ArgumentException(
                        "第 " + i + " 个列宽权重必须是不小于 0 的有限数，实际为 " + weight, nameof(leafWeights));
                }
                totalWeight += weight;
            }
            if (!(totalWeight > 0d))
            {
                throw new ArgumentException(
                    "全部列宽权重之和必须大于 0，实际为 " + totalWeight, nameof(leafWeights));
            }

            double available = (double)areaRight - areaLeft;
            float[] bounds = new float[leafWeights.Count + 1];
            bounds[0] = areaLeft;
            double accumulated = 0d;
            for (int i = 0; i < leafWeights.Count; i++)
            {
                accumulated += leafWeights[i];
                bounds[i + 1] = i == leafWeights.Count - 1
                    ? areaRight
                    : (float)(areaLeft + available * (accumulated / totalWeight));
            }
            return bounds;
        }

        /// <summary>等宽列的边界（与 <see cref="ColumnBounds"/> 同一条"末缘直接取右缘"的规则）。</summary>
        /// <param name="areaLeft">区域左缘。</param>
        /// <param name="areaRight">区域右缘。</param>
        /// <param name="leafCount">列数，必须 ≥1。</param>
        public static float[] UniformBounds(float areaLeft, float areaRight, int leafCount)
        {
            if (leafCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(leafCount),
                    "列数必须不小于 1，实际为 " + leafCount);
            }
            double[] weights = new double[leafCount];
            for (int i = 0; i < leafCount; i++)
            {
                weights[i] = 1d;
            }
            return ColumnBounds(areaLeft, areaRight, weights);
        }

        /// <summary>
        /// 从第 <paramref name="firstLeaf"/> 列起跨 <paramref name="span"/> 列的矩形。
        /// 合并格与单列格走同一个方法：单列就是 <paramref name="span"/>=1。
        /// </summary>
        /// <param name="bounds"><see cref="ColumnBounds"/> 或 <see cref="UniformBounds"/> 的结果。</param>
        /// <param name="firstLeaf">起始列序号。</param>
        /// <param name="span">跨列数，必须 ≥1。</param>
        /// <param name="top">上缘。</param>
        /// <param name="bottom">下缘。</param>
        /// <exception cref="ArgumentOutOfRangeException">起始列为负、或 <c>firstLeaf + span</c> 越过边界数组末尾。</exception>
        public static SKRect RectOf(IReadOnlyList<float> bounds, int firstLeaf, int span, float top, float bottom)
        {
            if (bounds == null)
            {
                throw new ArgumentNullException(nameof(bounds));
            }
            if (span < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(span), "跨列数必须不小于 1，实际为 " + span);
            }
            if (firstLeaf < 0 || firstLeaf + span > bounds.Count - 1)
            {
                throw new ArgumentOutOfRangeException(nameof(firstLeaf),
                    "第 " + firstLeaf + " 列起跨 " + span + " 列越界（共 " + (bounds.Count - 1) + " 列）。");
            }
            return new SKRect(bounds[firstLeaf], top, bounds[firstLeaf + span], bottom);
        }
    }
}
