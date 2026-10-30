using SkiaSharp;

namespace OODD.Charts.Style
{
    /// <summary>网格版面尺寸。默认值全部实测自参考图，见 docs/01-需求分析.md §3.1.1。</summary>
    public sealed class GridStyle
    {
        /// <summary>单个槽位宽（px @96dpi）。实测 20。</summary>
        public float SlotWidth = 20f;

        /// <summary>最小格高（px @96dpi），对应 RuleAxis 的一个 MinorStep。实测 15。</summary>
        public float CellHeight = 15f;

        /// <summary>多少个细格算一个粗刻度。实测 5（0.2℃ × 5 = 1℃ = 75px）。</summary>
        public int CellsPerMajor = 5;

        /// <summary>细线粗。实测 1。</summary>
        public float MinorStroke = 1f;

        /// <summary>粗线粗。实测 3。</summary>
        public float MajorStroke = 3f;

        /// <summary>最左侧空白条宽。实测 24。</summary>
        public float LeftStripWidth = 24f;

        /// <summary>第一刻度列（脉搏）宽。实测 40。</summary>
        public float FirstLabelWidth = 40f;

        /// <summary>第二刻度列（体温）宽。实测 40。</summary>
        public float SecondLabelWidth = 40f;

        /// <summary>附属表格行高（按分组合并的那些行）。实测 30。</summary>
        public float CellRowHeight = 30f;

        /// <summary>每天/每组的槽数。实测 6。</summary>
        public int SlotsPerGroup = 6;

        /// <summary>外框线粗。</summary>
        public float BorderStroke = 1.5f;

        public GridStyle Clone() => (GridStyle)MemberwiseClone();
    }

    /// <summary>文本样式令牌。</summary>
    public sealed class TextStyle
    {
        /// <summary>首选字族；null 表示交给字体回退链决定。</summary>
        public string? Family;

        public float HospitalSize = 18f;
        public float TitleSize = 16f;
        public float HeaderSize = 10f;
        public float DateSize = 9f;
        public float TimeSize = 7.5f;
        public float ScaleLabelSize = 9f;
        public float CellLabelSize = 9f;
        public float CellValueSize = 9f;
        public float LegendSize = 10f;
        public float EventSize = 9f;
        public float AnnotationSize = 8f;

        public bool HospitalBold = true;
        public bool TitleBold = true;

        public TextStyle Clone() => (TextStyle)MemberwiseClone();
    }

    /// <summary>点/线系列样式。</summary>
    public sealed class SeriesStyle
    {
        public float MarkerRadius = 3f;
        public float MarkerStroke = 1.2f;
        public float LineWidth = 1f;
        public float DashInterval = 4f;
        public float DashPhase = 3f;

        /// <summary>点旁小字与点的间距。</summary>
        public float LabelOffset = 4f;

        /// <summary>折线是否画在标记之下（先线后点）。</summary>
        public bool DrawLineUnderMarkers = true;

        public SeriesStyle Clone() => (SeriesStyle)MemberwiseClone();
    }

    /// <summary>页眉带高度。参考图被滚动裁切，这些值按国标版式给出，交付后可整体调参。</summary>
    public sealed class HeaderStyle
    {
        public float HospitalRowHeight = 26f;
        public float TitleRowHeight = 22f;
        public float PatientRowHeight = 20f;

        public float DateRowHeight = 18f;
        public float WeekRowHeight = 16f;
        public float TimeRowHeight = 14f;

        /// <summary>患者信息栏内文字左边距。</summary>
        public float PatientPadding = 4f;

        /// <summary>患者信息栏每行放几个字段，超出后自动换行；0 表示只按宽度换行。</summary>
        public int FieldsPerRow = 0;

        /// <summary>患者信息栏字段间的最小间距。</summary>
        public float FieldGap = 16f;

        public HeaderStyle Clone() => (HeaderStyle)MemberwiseClone();
    }

    /// <summary>页脚带样式。</summary>
    public sealed class FooterStyle
    {
        public float Height = 26f;
        public float ItemGap = 18f;
        public float SymbolTextGap = 4f;

        public FooterStyle Clone() => (FooterStyle)MemberwiseClone();
    }

    /// <summary>事件带（三测单 34–35℃ 之间的红字竖排区）样式。</summary>
    public sealed class EventBandStyle
    {
        /// <summary>占多少个细格高。实测 5（= 1℃）。</summary>
        public int HeightCells = 5;

        public float LineSpacing = 1.15f;
        public float Stroke = 1f;
        public float TextInset = 2f;

        public EventBandStyle Clone() => (EventBandStyle)MemberwiseClone();
    }

    /// <summary>纸张与页边距。</summary>
    public sealed class PageStyle
    {
        /// <summary>每页容纳的分组（天）数；0 表示不分页。</summary>
        public int GroupsPerPage = 0;

        public float MarginLeft = 0f;
        public float MarginTop = 0f;
        public float MarginRight = 0f;
        public float MarginBottom = 0f;

        /// <summary>PDF 页面尺寸（pt）；为 0 表示按内容自适应。</summary>
        public float PageWidthPt = 0f;
        public float PageHeightPt = 0f;

        public PageStyle Clone() => (PageStyle)MemberwiseClone();
    }

    /// <summary>
    /// 配色令牌。名字一律<b>不绑定具体表单</b>：三测单的"脉搏红"、产程图的"警戒线红"
    /// 都是同一个语义槽位（强调色 / 参考线色），否则新增一张表单就得往引擎加颜色。
    /// 具体表单用哪个槽位，由该表单的模板决定。
    /// </summary>
    public sealed class ColorPalette
    {
        public SKColor Background = SKColors.White;
        public SKColor Grid = SKColors.Black;
        public SKColor Text = SKColors.Black;

        /// <summary>事件、破膜这类"发生在某时刻"的标记色。</summary>
        public SKColor Event = SKColors.Red;

        /// <summary>附属表格取值色。</summary>
        public SKColor CellValue = SKColors.Red;

        public SKColor Legend = SKColors.Black;

        /// <summary>参考线色（产程图警戒线）。</summary>
        public SKColor ReferenceLine = SKColors.Red;

        /// <summary>第二参考线色（产程图处理线）。</summary>
        public SKColor ReferenceLineAlt = SKColors.Red;

        /// <summary>实心方块图元色（产程图宫缩计数）。</summary>
        public SKColor Block = SKColors.Black;

        /// <summary>越界强调圈色（产程图异常胎心）。</summary>
        public SKColor Highlight = SKColors.Red;

        public ColorPalette Clone() => (ColorPalette)MemberwiseClone();
    }

    /// <summary>
    /// 一张表单的全部通用外观参数。绘制代码不得出现字面量尺寸或颜色，一律读此处。
    /// <para>
    /// 这里只放<b>跨表单共用</b>的令牌（网格、文本、页眉、页脚、配色）。
    /// 单张表单专属的尺寸（如产程图的宫口行高）由该表单模板自己的 tokens 承载，
    /// 否则新增一张表单就得改引擎，违反验收 A9。
    /// </para>
    /// </summary>
    public class ChartStyle
    {
        public GridStyle Grid { get; set; } = new GridStyle();
        public TextStyle Text { get; set; } = new TextStyle();
        public SeriesStyle Series { get; set; } = new SeriesStyle();
        public HeaderStyle Header { get; set; } = new HeaderStyle();
        public FooterStyle Footer { get; set; } = new FooterStyle();
        public EventBandStyle EventBand { get; set; } = new EventBandStyle();
        public PageStyle Page { get; set; } = new PageStyle();
        public ColorPalette Colors { get; set; } = new ColorPalette();

        /// <summary>返回一份可安全修改的副本，用于按院方模板微调。</summary>
        public ChartStyle Clone()
        {
            ChartStyle copy = (ChartStyle)MemberwiseClone();
            copy.Grid = Grid.Clone();
            copy.Text = Text.Clone();
            copy.Series = Series.Clone();
            copy.Header = Header.Clone();
            copy.Footer = Footer.Clone();
            copy.EventBand = EventBand.Clone();
            copy.Page = Page.Clone();
            copy.Colors = Colors.Clone();
            return copy;
        }
    }
}
