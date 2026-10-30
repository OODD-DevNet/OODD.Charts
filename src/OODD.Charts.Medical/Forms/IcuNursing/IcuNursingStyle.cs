namespace OODD.Charts.Medical.Forms.IcuNursing
{
    /// <summary>
    /// ICU 护理记录单专属令牌。
    /// <para>
    /// 未校准：三级表头行高、主表行高与旁挂子表行高按"侧表必须独立行距"的结构关系取值，
    /// 不宣称与某院实物一致。改 <see cref="DataRowHeight"/> 即改主表行距（侧表不跟随）。
    /// </para>
    /// </summary>
    public sealed class IcuNursingStyle
    {
        /// <summary>版面宽（96dpi 逻辑像素）。未校准。</summary>
        public float PaperWidth = 800f;

        public float Margin = 8f;
        public float HospitalRowHeight = 22f;
        public float TitleRowHeight = 22f;

        /// <summary>表头每层行高（三级即 3 × 本值）。</summary>
        public float HeaderLevelHeight = 22f;

        /// <summary>表头与数据行之间的留白。</summary>
        public float HeaderGap = 6f;

        /// <summary>主表数据行高。侧挂子表不跟随此值。</summary>
        public float DataRowHeight = 24f;

        /// <summary>主表行标签列宽（在表头树左侧）。</summary>
        public float RowLabelWidth = 46f;

        /// <summary>侧挂交班小结子表宽。</summary>
        public float SideTableWidth = 140f;

        /// <summary>侧表与主表的间隔。</summary>
        public float SideTableGap = 8f;

        /// <summary>侧表自己的行高（故意不等于 <see cref="DataRowHeight"/>）。</summary>
        public float SideRowHeight = 30f;

        /// <summary>侧表内标签列宽。</summary>
        public float SideLabelWidth = 58f;

        public float FooterHeight = 20f;

        public IcuNursingStyle Clone() => (IcuNursingStyle)MemberwiseClone();
    }
}
