using SkiaSharp;

namespace OODD.Charts.Medical.Forms.DeathCertificate
{
    /// <summary>
    /// 居民死亡医学证明（四联）专属令牌。
    /// <para>
    /// 未校准：四联等宽（每联 250px）与行高按 A4 横向四等分的版式关系取整，
    /// 不宣称与某版实物逐毫米对齐；改 <see cref="CopyWidth"/> 即整体缩放。
    /// </para>
    /// </summary>
    public sealed class DeathCertificateStyle
    {
        /// <summary>单联宽（96dpi 逻辑像素）。四联总宽 = 4 × 本值，即版面宽。</summary>
        public float CopyWidth = 250f;

        public float HospitalRowHeight = 22f;
        public float TitleRowHeight = 24f;
        public float NoteRowHeight = 16f;
        public float FooterHeight = 22f;

        /// <summary>联内左右留白。</summary>
        public float CopyPadding = 8f;

        /// <summary>联名竖排条宽（在联的左缘）。</summary>
        public float CopyLabelStripWidth = 16f;

        /// <summary>行政区划代码栅格的行高（= 格高）。</summary>
        public float AdminBoxSize = 14f;

        /// <summary>编号栅格的行高（= 格高）。</summary>
        public float SerialBoxSize = 12f;

        /// <summary>字段行高。A18 断言用它证明落点由令牌驱动。</summary>
        public float RowHeight = 22f;

        /// <summary>字段行内标签列宽。</summary>
        public float FieldLabelWidth = 62f;

        /// <summary>死因链与普通字段之间的分隔留白。</summary>
        public float CauseGap = 8f;

        public int AdminBoxCount = 6;
        public int SerialBoxCount = 17;

        /// <summary>行政区划代码栅格与编号栅格之间的间隙。</summary>
        public float BoxRowGap = 6f;

        /// <summary>栅格与首个字段行之间的留白。</summary>
        public float BoxBlockGap = 10f;

        /// <summary>死因链行标签（I.(a)–(d)）。</summary>
        public float CauseLabelWidth = 44f;

        /// <summary>裁切虚线颜色（表单令牌；不在跨表单 ChartStyle 里）。</summary>
        public SKColor SeamColor = new SKColor(90, 90, 90);

        public DeathCertificateStyle Clone() => (DeathCertificateStyle)MemberwiseClone();
    }
}
