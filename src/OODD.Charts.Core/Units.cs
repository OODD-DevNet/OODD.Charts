using System;

namespace OODD.Charts
{
    /// <summary>
    /// 毫米令牌。测井成果图的 250mm 幅面、接种证的 210×145mm、图样填充的间距
    /// 都以 mm 写在文档/样式里，落笔时经此换算成 96dpi 逻辑像素。
    /// <para>
    /// 这是<b>单位令牌，不是打印承诺</b>：本库只保证"mm → 逻辑像素"的换算精确，
    /// 不宣称出图在打印机链路上满足 ±2% 的实物公差（那需要实测走纸与 DPI 校准，
    /// 见 docs/05 排除 M12 的同一理由）。
    /// </para>
    /// </summary>
    public static class Unit
    {
        /// <summary>96dpi 下 1mm 的逻辑像素数 = 96 / 25.4。</summary>
        public const float PxPerMm = 96f / 25.4f;

        /// <summary>毫米 → 96dpi 逻辑像素。</summary>
        public static float Mm(float mm)
        {
            if (float.IsNaN(mm) || float.IsInfinity(mm))
            {
                throw new ArgumentOutOfRangeException(nameof(mm), "毫米值必须是有限数，实到 " + mm);
            }

            return mm * PxPerMm;
        }

        /// <summary>96dpi 逻辑像素 → 毫米。</summary>
        public static float MmFromPx(float pixels)
        {
            if (float.IsNaN(pixels) || float.IsInfinity(pixels))
            {
                throw new ArgumentOutOfRangeException(nameof(pixels), "像素值必须是有限数，实到 " + pixels);
            }

            return pixels / PxPerMm;
        }

        /// <summary>把毫米幅面对齐到整数像素并返回对齐后的像素宽度（注意：返回的不是 mm），
        /// 用于幅面校验，如 250mm 图头对应 945px。返回绝对值，因为版面里的长度不带方向。</summary>
        public static float MmWidth(float mm)
        {
            return (float)Math.Abs(Math.Round((double)Mm(mm)));
        }
    }
}
