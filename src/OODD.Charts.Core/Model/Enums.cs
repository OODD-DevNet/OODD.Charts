namespace OODD.Charts
{
    /// <summary>点系列使用的图形符号。</summary>
    public enum MarkerSymbol
    {
        /// <summary>不画标记，只画连线。</summary>
        None = 0,
        /// <summary>实心圆 ●，三测单用于脉搏。</summary>
        Dot = 1,
        /// <summary>空心圆 ○，三测单用于腋温，产程图用于宫口扩张。</summary>
        Ring = 2,
        /// <summary>叉形 ×，三测单用于心率，产程图用于先露下降。</summary>
        Cross = 3,
        /// <summary>同心圆 ◎，口温。</summary>
        DotRing = 4,
        /// <summary>圆内十字 ⊕，肛温。</summary>
        PlusInRing = 5,
        /// <summary>实心点带竖茎 ●|，脉搏短绌时与 × 配对。</summary>
        DotStem = 6,
    }

    /// <summary>折线在缺测处的断开策略。</summary>
    public enum PointBreakRule
    {
        /// <summary>相邻有值点一律相连（旧 GDI+ demo 行为）。</summary>
        Always = 0,
        /// <summary>中间任一槽位缺值即断开。默认，符合护理文书规范。</summary>
        BreakOnGap = 1,
        /// <summary>跨天边界断开（物理降温后 30 分钟复测等场景）。</summary>
        BreakOnDayBoundary = 2,
    }

    /// <summary>单元格内文本对齐。</summary>
    public enum CellAlign
    {
        /// <summary>靠左/靠上。</summary>
        Near = 0,
        /// <summary>居中。</summary>
        Center = 1,
        /// <summary>靠右/靠下。</summary>
        Far = 2,
    }

    /// <summary>刻度线种类。</summary>
    public enum TickKind
    {
        /// <summary>细线。</summary>
        Minor = 0,
        /// <summary>粗线。</summary>
        Major = 1,
        /// <summary>粗线且带数字标签。</summary>
        Labeled = 2,
    }

    /// <summary>位图导出格式。</summary>
    public enum ChartImageFormat
    {
        /// <summary>PNG，无损。</summary>
        Png = 0,
        /// <summary>JPEG，有损。</summary>
        Jpeg = 1,
        /// <summary>WebP。</summary>
        Webp = 2,
    }

    /// <summary>事件标记的排布方向。</summary>
    public enum EventMarkOrientation
    {
        /// <summary>逐字竖排（三测单 34–35℃ 事件带）。</summary>
        Vertical = 0,
        /// <summary>横排。</summary>
        Horizontal = 1,
    }
}
