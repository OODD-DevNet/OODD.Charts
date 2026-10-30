using System;

namespace OODD.Charts.Geometry
{
    /// <summary>时段带的填法。</summary>
    public enum BandFill
    {
        /// <summary>实色（淡色底）。</summary>
        Solid = 0,

        /// <summary>斜纹。天窗带、禁动区用这一种，因为它压在网格线上仍可辨。</summary>
        Hatch = 1,

        /// <summary>只描边界，不填。</summary>
        None = 2,
    }

    /// <summary>
    /// 横向贯通的时段带：施工天窗、超温时段、生长百分位评价带。
    /// <para>
    /// 它只描述"哪一段域要带、带要填成什么样"，不关心像素——像素由模板在落笔时
    /// 用当时的轴换算。这样同一条带可以画在线性轴也可以画在对数轴上。
    /// </para>
    /// </summary>
    public sealed class SpanBand
    {
        /// <param name="key">稳定标识。</param>
        /// <param name="fromDomain">起始域值（连续量：时间、里程…）。</param>
        /// <param name="toDomain">结束域值，必须大于起始值。</param>
        /// <param name="fromCategory">起始分类下标；轴上没有分类语义时传 null。
        /// 给定时它必须与域值同序，用于"从第 3 站起的 90 分钟天窗"这类混合定位。</param>
        /// <param name="toCategory">结束分类下标（含）。</param>
        /// <param name="fill">填充方式。</param>
        public SpanBand(string key, double fromDomain, double toDomain,
                        int? fromCategory = null, int? toCategory = null,
                        BandFill fill = BandFill.Solid)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("时段带必须有 key。", nameof(key));
            }
            if (double.IsNaN(fromDomain) || double.IsNaN(toDomain)
                || double.IsInfinity(fromDomain) || double.IsInfinity(toDomain))
            {
                throw new ArgumentException("时段带的端点必须是有限值：" + key, nameof(fromDomain));
            }
            if (!(toDomain > fromDomain))
            {
                throw new ArgumentException("时段带终点必须大于起点：" + key + " ["
                                            + fromDomain + "," + toDomain + "]", nameof(toDomain));
            }
            if (fromCategory != null && toCategory != null && toCategory.Value < fromCategory.Value)
            {
                throw new ArgumentException("时段带的分类下标倒置：" + key, nameof(toCategory));
            }

            Key = key;
            FromDomain = fromDomain;
            ToDomain = toDomain;
            FromCategory = fromCategory;
            ToCategory = toCategory;
            Fill = fill;
        }

        public string Key { get; }
        public double FromDomain { get; }
        public double ToDomain { get; }

        /// <summary>起始分类下标；无分类语义时为 null。</summary>
        public int? FromCategory { get; }

        /// <summary>结束分类下标（含）；无分类语义时为 null。</summary>
        public int? ToCategory { get; }

        public BandFill Fill { get; }

        /// <summary>域跨度。</summary>
        public double Span => ToDomain - FromDomain;

        /// <summary>该域值是否落在带内（含两端——天窗起止时刻本身算封轨时间）。</summary>
        public bool CoversDomain(double domain)
        {
            double eps = 1e-9 * Math.Max(1d, Math.Max(Math.Abs(domain), Math.Abs(ToDomain)));
            return domain >= FromDomain - eps && domain <= ToDomain + eps;
        }

        /// <summary>该分类下标是否落在带内；带未给分类区间时返回 false。</summary>
        public bool CoversCategory(int categoryIndex)
        {
            if (FromCategory == null || ToCategory == null)
            {
                return false;
            }

            return categoryIndex >= FromCategory.Value && categoryIndex <= ToCategory.Value;
        }

        /// <summary>两条带是否有重叠（含端点相接）。用于检查天窗排布是否自相冲突。</summary>
        public bool Overlaps(SpanBand other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other.FromDomain <= ToDomain && other.ToDomain >= FromDomain;
        }

        public override string ToString() => Key + " [" + FromDomain + "," + ToDomain + "] " + Fill;
    }
}
