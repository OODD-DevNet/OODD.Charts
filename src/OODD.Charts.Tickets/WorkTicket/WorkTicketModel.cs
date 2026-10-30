using System;
using System.Collections.Generic;
using OODD.Charts.Render;
using SkiaSharp;

namespace OODD.Charts.Tickets.WorkTicket
{
    /// <summary>
    /// 作业票的印章状态。三态与 <see cref="OODD.Charts.Render.TriStateBox"/> 的三态不是同一个概念：
    /// 这里是整张票的终态（未执行 / 已执行 / 作废），那里是单条措施"符合 / 不涉及"的确认态。
    /// </summary>
    public enum WorkTicketStampState
    {
        /// <summary>未执行：不盖红章，只画一枚浅色空圈与"未执行"字样。</summary>
        NotExecuted = 0,

        /// <summary>已执行：红章。</summary>
        Executed = 1,

        /// <summary>作废：红章 + 章内交叉划线。</summary>
        Voided = 2,
    }

    /// <summary>
    /// 票面上的一行"标签 + 值"字段。
    /// <para>
    /// 字段是<b>视图数据</b>：库不判断工作票该有哪些栏位，只按模板给的键序排版。
    /// 栏位次序属 docs/05 §5「版面待取证」项（D22）：本类是结构容器，不代表任何票样。
    /// </para>
    /// </summary>
    public sealed class TicketField
    {
        /// <summary>构造一个字段。</summary>
        /// <param name="key">稳定键（Ordinal），联次可见性按它判定。</param>
        /// <param name="label">栏目标签。</param>
        /// <param name="value">栏位值；null 表示留空待手填。</param>
        public TicketField(string key, string label, string? value = null)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("字段键不能为空。", nameof(key));
            }
            Key = key;
            Label = label ?? string.Empty;
            Value = value;
        }

        /// <summary>稳定键。</summary>
        public string Key { get; }

        /// <summary>栏目标签。</summary>
        public string Label { get; }

        /// <summary>栏位值（可空）。</summary>
        public string? Value { get; set; }

        /// <summary>票面文字：标签 + 全角冒号 + 值。值空时仍留出冒号，表示"该栏待填"。</summary>
        public string Text
        {
            get { return string.IsNullOrEmpty(Value) ? Label + "：" : Label + "：" + Value; }
        }

        /// <summary>返回栏目标签，便于日志/断言。</summary>
        public override string ToString()
        {
            return Key + "(" + Text + ")";
        }
    }

    /// <summary>
    /// 安全措施（或"作业条件确认"）表里的一行：文本 + 三态确认。
    /// 三态由 <see cref="TriStateBox"/> 只画几何（勾/叉都是线段），文字由模板落笔。
    /// </summary>
    public sealed class TicketMeasureRow
    {
        /// <summary>构造一条措施。</summary>
        /// <param name="text">措施文本。</param>
        /// <param name="state">三态确认结果。</param>
        public TicketMeasureRow(string text, TriState state)
        {
            Text = text ?? string.Empty;
            State = state;
        }

        /// <summary>措施文本。</summary>
        public string Text { get; }

        /// <summary>三态确认结果。</summary>
        public TriState State { get; }
    }

    /// <summary>
    /// 按条目数生长的行组（<c>AppendableRowGroup</c>）。
    /// <para>
    /// <b>落地差异登记</b>：docs/05 §2 与 docs/07 T280 把 <c>AppendableRowGroup</c> 列为 Core 原语，
    /// 但 Core 里<b>没有</b>这个类型（<c>src/OODD.Charts.Core</c> 全树检索无命中）。本任务无权改 Core，
    /// 故在凭证域内以等价实现交付：条目数在 <see cref="WorkTicketSheet.Build"/> 里定死行数与行高，
    /// 行组高度 = 条目数 × 行高，**条目数变则整页版面变**（不是绘制期临时撑开）。
    /// 待 Core 补上同名类型后，本类可直接替换为其轻量包装。
    /// </para>
    /// </summary>
    public sealed class AppendableRowGroup
    {
        private readonly SKRect[] _rows;

        /// <summary>构造行组。</summary>
        /// <param name="key">稳定键。</param>
        /// <param name="count">条目数（决定行数），必须 ≥ 0。</param>
        /// <param name="rowHeight">单行高，必须为正。</param>
        /// <param name="left">行左缘。</param>
        /// <param name="top">首行上缘。</param>
        /// <param name="right">行右缘，必须大于左缘。</param>
        public AppendableRowGroup(string key, int count, float rowHeight, float left, float top, float right)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("行组键不能为空。", nameof(key));
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "行数不能为负，实到 " + count + "。");
            }
            if (!(rowHeight > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(rowHeight), "行高必须为正，实到 " + rowHeight + "。");
            }
            if (!(right > left))
            {
                throw new ArgumentException("行右缘必须大于左缘。", nameof(right));
            }

            Key = key;
            RowHeight = rowHeight;
            Count = count;
            _rows = new SKRect[count];
            for (int i = 0; i < count; i++)
            {
                _rows[i] = new SKRect(left, top + i * rowHeight, right, top + (i + 1) * rowHeight);
            }
            Bounds = new SKRect(left, top, right, top + count * rowHeight);
        }

        /// <summary>稳定键。</summary>
        public string Key { get; }

        /// <summary>行数。</summary>
        public int Count { get; }

        /// <summary>单行高。</summary>
        public float RowHeight { get; }

        /// <summary>整组外接矩形；0 行时高度为 0（上缘 == 下缘）。</summary>
        public SKRect Bounds { get; }

        /// <summary>逐行矩形，只读。</summary>
        public IReadOnlyList<SKRect> Rows
        {
            get { return _rows; }
        }

        /// <summary>取第 index 行矩形，越界抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
        /// <param name="index">行序号，从 0 起。</param>
        public SKRect Row(int index)
        {
            if (index < 0 || index >= _rows.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "行序号 " + index + " 越界（共 " + _rows.Length + " 行）。");
            }
            return _rows[index];
        }
    }

    /// <summary>
    /// 两张作业票共用的版面令牌。
    /// <para>
    /// <b>全部是结构尺寸，未按票样校准</b>（docs/05 §5 / D22）。票面栏位与联名不得据此声称对齐
    /// GB 26860 附录 A 或 GB 30871 附录 A —— 两处票样均未取证。
    /// </para>
    /// </summary>
    public sealed class WorkTicketGridStyle
    {
        /// <summary>编号行高（编号与单位同排）。</summary>
        public float SerialHeight = 15f;

        /// <summary>副标题行高（作业类别等；无副标题时不占高）。</summary>
        public float SubtitleHeight = 16f;

        /// <summary>联名竖排带的宽度。</summary>
        public float CopyLabelWidth = 18f;

        /// <summary>字段标签列宽。</summary>
        public float FieldLabelWidth = 148f;

        /// <summary>区段小标题行高（"安全措施"这类）。</summary>
        public float SectionTitleHeight = 18f;

        /// <summary>措施表行高（AppendableRowGroup 的生长步长）。</summary>
        public float MeasureRowHeight = 20f;

        /// <summary>措施表序号列宽。</summary>
        public float MeasureIndexWidth = 22f;

        /// <summary>措施表三态确认列宽。</summary>
        public float MeasureStateWidth = 44f;

        /// <summary>副表（危害辨识一类）行高。</summary>
        public float SecondaryRowHeight = 18f;

        /// <summary>副表序号列宽。</summary>
        public float SecondaryIndexWidth = 22f;

        /// <summary>签名行高。</summary>
        public float SignatureHeight = 30f;

        /// <summary>区段之间的空隙。</summary>
        public float SectionGap = 6f;

        /// <summary>单联最小高度（内容不足时的下限，避免薄票）。</summary>
        public float MinCopyHeight = 520f;

        /// <summary>印章圆心在内容区宽度上的相对位置（0=左缘，1=右缘）。</summary>
        public float StampXFactor = 0.70f;

        /// <summary>印章下方留白（防止章压出联框）。</summary>
        public float StampBottomPad = 8f;

        /// <summary>章体倾角（度，顺时针）。只作用于章体几何，章内文字一律直立。</summary>
        public float StampRotationDegrees = 6f;

        /// <summary>章心是否画五角星。</summary>
        public bool StampWithStar = true;

        /// <summary>章内是否画横杠。</summary>
        public bool StampWithBar = false;

        /// <summary>章内文字的基线相对圆心的纵向偏移（相对直径的因子）。</summary>
        public float StampTextOffsetFactor = 0.30f;

        /// <summary>章外圈描边宽相对直径的因子。</summary>
        public float StampStrokeFactor = 0.038f;

        /// <summary>联与联之间的骑缝形式。</summary>
        public PerforationStyle Perforation = PerforationStyle.DashedCut;

        /// <summary>骑缝线线粗。</summary>
        public float PerforationStroke = 1f;

        /// <summary>单元格内文字留白。</summary>
        public float CellPadding = 3f;

        /// <summary>标签与值之间的间距。</summary>
        public float LabelValueGap = 4f;

        /// <summary>浅拷贝（字段都是值类型，够用）。</summary>
        public WorkTicketGridStyle Clone()
        {
            return (WorkTicketGridStyle)MemberwiseClone();
        }
    }

    /// <summary>一联的定义（键、联名、该联显示的字段键）。</summary>
    public sealed class WorkTicketCopySpec
    {
        /// <summary>构造一联。</summary>
        /// <param name="key">稳定键（Ordinal 唯一）。</param>
        /// <param name="label">联名，竖排在装订带里。</param>
        /// <param name="fieldKeys">该联要显示的字段键（按声明顺序）。</param>
        public WorkTicketCopySpec(string key, string label, IReadOnlyList<string> fieldKeys)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("联 key 不能为空。", nameof(key));
            }
            if (fieldKeys == null)
            {
                throw new ArgumentNullException(nameof(fieldKeys));
            }
            if (fieldKeys.Count == 0)
            {
                throw new ArgumentException("联 \"" + key + "\" 至少要显示一个字段。", nameof(fieldKeys));
            }

            Key = key;
            Label = label ?? string.Empty;
            FieldKeys = fieldKeys;
        }

        /// <summary>稳定键。</summary>
        public string Key { get; }

        /// <summary>联名。</summary>
        public string Label { get; }

        /// <summary>该联显示的字段键集合。</summary>
        public IReadOnlyList<string> FieldKeys { get; }
    }

    /// <summary>副表（危害辨识 / 作业步骤一类）的条目块：小标题 + 若干文本行。</summary>
    public sealed class WorkTicketRowBlock
    {
        /// <summary>构造条目块。</summary>
        /// <param name="title">小标题。</param>
        /// <param name="rows">条目文本。</param>
        public WorkTicketRowBlock(string title, IReadOnlyList<string> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }
            Title = title ?? string.Empty;
            Rows = rows;
        }

        /// <summary>小标题。</summary>
        public string Title { get; }

        /// <summary>条目文本。</summary>
        public IReadOnlyList<string> Rows { get; }
    }

    /// <summary>
    /// 一次作业票版面求解的输入：票名、编号、字段目录、措施行、签名列、联次与印章状态。
    /// 全部是值容器，<see cref="WorkTicketSheet.Build"/> 不修改其中任何一项。
    /// </summary>
    public sealed class WorkTicketSheetSpec
    {
        /// <summary>构造版面输入。</summary>
        /// <param name="title">票名。</param>
        /// <param name="sectionTitle">措施区小标题。</param>
        /// <param name="fields">字段目录（键→标签/值）。</param>
        /// <param name="measures">措施行。</param>
        /// <param name="signatures">签名列标签。</param>
        /// <param name="copies">联次定义。</param>
        /// <param name="stampState">印章状态。</param>
        public WorkTicketSheetSpec(
            string title,
            string sectionTitle,
            IReadOnlyList<TicketField> fields,
            IReadOnlyList<TicketMeasureRow> measures,
            IReadOnlyList<string> signatures,
            IReadOnlyList<WorkTicketCopySpec> copies,
            WorkTicketStampState stampState)
        {
            Title = title ?? string.Empty;
            SectionTitle = sectionTitle ?? string.Empty;
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
            Measures = measures ?? throw new ArgumentNullException(nameof(measures));
            Signatures = signatures ?? throw new ArgumentNullException(nameof(signatures));
            Copies = copies ?? throw new ArgumentNullException(nameof(copies));
            StampState = stampState;
        }

        /// <summary>票名。</summary>
        public string Title { get; }

        /// <summary>措施区小标题。</summary>
        public string SectionTitle { get; }

        /// <summary>字段目录。</summary>
        public IReadOnlyList<TicketField> Fields { get; }

        /// <summary>措施行。</summary>
        public IReadOnlyList<TicketMeasureRow> Measures { get; }

        /// <summary>签名列标签。</summary>
        public IReadOnlyList<string> Signatures { get; }

        /// <summary>联次定义。</summary>
        public IReadOnlyList<WorkTicketCopySpec> Copies { get; }

        /// <summary>印章状态。</summary>
        public WorkTicketStampState StampState { get; }

        /// <summary>副标题（如"作业类别：动火作业"）；null 表示不占行。</summary>
        public string? Subtitle { get; set; }

        /// <summary>单位行（编号行左侧）。</summary>
        public string? UnitLine { get; set; }

        /// <summary>编号（编号行右侧）。</summary>
        public string? SerialText { get; set; }

        /// <summary>章内文字；null 时按 <see cref="StampState"/> 取默认值。</summary>
        public string? StampText { get; set; }

        /// <summary>副表（危害辨识一类）；null 表示整块不出现。</summary>
        public WorkTicketRowBlock? Secondary { get; set; }
    }

    /// <summary>字段行版面：行矩形 + 该联是否显示该字段。</summary>
    public sealed class WorkTicketFieldRow
    {
        /// <summary>构造字段行。</summary>
        public WorkTicketFieldRow(int index, string key, string label, string? value, SKRect rect, bool shown)
        {
            Index = index;
            Key = key;
            Label = label;
            Value = value;
            Rect = rect;
            Shown = shown;
        }

        /// <summary>行号（= <see cref="CollateSpec.AllFields"/> 的下标）。</summary>
        public int Index { get; }

        /// <summary>字段键。</summary>
        public string Key { get; }

        /// <summary>标签。</summary>
        public string Label { get; }

        /// <summary>值。</summary>
        public string? Value { get; }

        /// <summary>行矩形（整行，含标签列与值列）。</summary>
        public SKRect Rect { get; }

        /// <summary>该联是否显示本行。</summary>
        public bool Shown { get; }
    }

    /// <summary>措施行版面：行矩形、序号/文本/三态框的落点与状态。</summary>
    public sealed class WorkTicketMeasureLayout
    {
        /// <summary>构造措施行。</summary>
        public WorkTicketMeasureLayout(int index, string text, TriState state, SKRect row,
            SKRect indexRect, SKRect textRect, SKRect stateRect, SKRect boxRect)
        {
            Index = index;
            Text = text;
            State = state;
            Row = row;
            IndexRect = indexRect;
            TextRect = textRect;
            StateRect = stateRect;
            BoxRect = boxRect;
        }

        /// <summary>行号。</summary>
        public int Index { get; }

        /// <summary>措施文本。</summary>
        public string Text { get; }

        /// <summary>三态确认结果。</summary>
        public TriState State { get; }

        /// <summary>整行矩形。</summary>
        public SKRect Row { get; }

        /// <summary>序号列矩形。</summary>
        public SKRect IndexRect { get; }

        /// <summary>文本列矩形。</summary>
        public SKRect TextRect { get; }

        /// <summary>三态确认列矩形。</summary>
        public SKRect StateRect { get; }

        /// <summary>三态方框的外接矩形（边长 = <see cref="TicketStyle.CheckBoxSize"/>）。</summary>
        public SKRect BoxRect { get; }
    }

    /// <summary>副表的文本行版面。</summary>
    public sealed class WorkTicketTextRow
    {
        /// <summary>构造文本行。</summary>
        public WorkTicketTextRow(int index, string text, SKRect row, SKRect indexRect, SKRect textRect)
        {
            Index = index;
            Text = text;
            Row = row;
            IndexRect = indexRect;
            TextRect = textRect;
        }

        /// <summary>行号。</summary>
        public int Index { get; }

        /// <summary>文本。</summary>
        public string Text { get; }

        /// <summary>整行矩形。</summary>
        public SKRect Row { get; }

        /// <summary>序号列矩形。</summary>
        public SKRect IndexRect { get; }

        /// <summary>文本列矩形。</summary>
        public SKRect TextRect { get; }
    }

    /// <summary>单联版面：外框、装订带、字段行、措施表、签名行、印章。</summary>
    public sealed class WorkTicketCopyLayout
    {
        /// <summary>构造单联版面。</summary>
        public WorkTicketCopyLayout(int index, string copyKey, string copyLabel, SKRect rect, SKRect labelRect,
            SKRect innerRect, SKRect titleRect, SKRect subtitleRect, SKRect serialRect, SKRect sectionTitleRect,
            SKRect signatureRect, SKPoint stampCenter, SKRect stampBounds,
            IReadOnlyList<WorkTicketFieldRow> fields, AppendableRowGroup measureGroup,
            IReadOnlyList<WorkTicketMeasureLayout> measures, SKRect? secondaryTitleRect,
            AppendableRowGroup? secondaryGroup, IReadOnlyList<WorkTicketTextRow> secondaryRows,
            IReadOnlyList<SKRect> signatureCells)
        {
            Index = index;
            CopyKey = copyKey;
            CopyLabel = copyLabel;
            Rect = rect;
            LabelRect = labelRect;
            InnerRect = innerRect;
            TitleRect = titleRect;
            SubtitleRect = subtitleRect;
            SerialRect = serialRect;
            SectionTitleRect = sectionTitleRect;
            SignatureRect = signatureRect;
            StampCenter = stampCenter;
            StampBounds = stampBounds;
            Fields = fields;
            MeasureGroup = measureGroup;
            Measures = measures;
            SecondaryTitleRect = secondaryTitleRect;
            SecondaryGroup = secondaryGroup;
            SecondaryRows = secondaryRows;
            SignatureCells = signatureCells;
        }

        /// <summary>联序号。</summary>
        public int Index { get; }

        /// <summary>联键。</summary>
        public string CopyKey { get; }

        /// <summary>联名。</summary>
        public string CopyLabel { get; }

        /// <summary>整联外框。</summary>
        public SKRect Rect { get; }

        /// <summary>装订带（联名竖排）。</summary>
        public SKRect LabelRect { get; }

        /// <summary>内容区（去掉四周留白与装订带）。</summary>
        public SKRect InnerRect { get; }

        /// <summary>票名行。</summary>
        public SKRect TitleRect { get; }

        /// <summary>副标题行；无副标题时为零高矩形（Top == Bottom）。</summary>
        public SKRect SubtitleRect { get; }

        /// <summary>单位/编号行。</summary>
        public SKRect SerialRect { get; }

        /// <summary>措施区小标题行。</summary>
        public SKRect SectionTitleRect { get; }

        /// <summary>签名行。</summary>
        public SKRect SignatureRect { get; }

        /// <summary>印章圆心。</summary>
        public SKPoint StampCenter { get; }

        /// <summary>印章外接矩形（含描边）。</summary>
        public SKRect StampBounds { get; }

        /// <summary>字段行（按 <see cref="CollateSpec.AllFields"/> 顺序）。</summary>
        public IReadOnlyList<WorkTicketFieldRow> Fields { get; }

        /// <summary>措施行组（AppendableRowGroup）。</summary>
        public AppendableRowGroup MeasureGroup { get; }

        /// <summary>措施行版面。</summary>
        public IReadOnlyList<WorkTicketMeasureLayout> Measures { get; }

        /// <summary>副表小标题行；无副表时为 null。</summary>
        public SKRect? SecondaryTitleRect { get; }

        /// <summary>副表行组；无副表时为 null。</summary>
        public AppendableRowGroup? SecondaryGroup { get; }

        /// <summary>副表文本行。</summary>
        public IReadOnlyList<WorkTicketTextRow> SecondaryRows { get; }

        /// <summary>签名列矩形（等宽切分）。</summary>
        public IReadOnlyList<SKRect> SignatureCells { get; }
    }

    /// <summary>
    /// 一张作业票的完整版面。绘制端只读这里，不再重算（与 Radar 同一口径：
    /// Measure 与 DrawContent 共用同一个版面对象，改令牌后未失效也不会错位）。
    /// </summary>
    public sealed class WorkTicketLayout
    {
        /// <summary>构造版面。</summary>
        public WorkTicketLayout(SKRect bounds, CollateSpec collate, IReadOnlyList<WorkTicketCopyLayout> copies,
            string title, string sectionTitle, IReadOnlyList<string> signatures,
            WorkTicketStampState stampState, string stampText, float copyHeight, int fieldRowCount,
            int measureRowCount, int secondaryRowCount,
            string? subtitle, string? unitLine, string? serialText, string? secondaryTitle)
        {
            Bounds = bounds;
            Collate = collate;
            Copies = copies;
            Title = title;
            SectionTitle = sectionTitle;
            Signatures = signatures;
            StampState = stampState;
            StampText = stampText;
            CopyHeight = copyHeight;
            FieldRowCount = fieldRowCount;
            MeasureRowCount = measureRowCount;
            SecondaryRowCount = secondaryRowCount;
            Subtitle = subtitle;
            UnitLine = unitLine;
            SerialText = serialText;
            SecondaryTitle = secondaryTitle;
        }

        /// <summary>整张多联票的外接矩形。</summary>
        public SKRect Bounds { get; }

        /// <summary>切联规格（联宽在此记录的是"联高"，方向为纵叠）。</summary>
        public CollateSpec Collate { get; }

        /// <summary>逐联版面。</summary>
        public IReadOnlyList<WorkTicketCopyLayout> Copies { get; }

        /// <summary>票名。</summary>
        public string Title { get; }

        /// <summary>措施区小标题。</summary>
        public string SectionTitle { get; }

        /// <summary>签名列标签。</summary>
        public IReadOnlyList<string> Signatures { get; }

        /// <summary>印章状态。</summary>
        public WorkTicketStampState StampState { get; }

        /// <summary>章内文字。</summary>
        public string StampText { get; }

        /// <summary>单联高（各联等高）。</summary>
        public float CopyHeight { get; }

        /// <summary>字段行数（= <see cref="CollateSpec.AllFields"/> 数量）。</summary>
        public int FieldRowCount { get; }

        /// <summary>措施行数。</summary>
        public int MeasureRowCount { get; }

        /// <summary>副表行数。</summary>
        public int SecondaryRowCount { get; }

        /// <summary>副标题（无则 null）。</summary>
        public string? Subtitle { get; }

        /// <summary>单位行。</summary>
        public string? UnitLine { get; }

        /// <summary>编号。</summary>
        public string? SerialText { get; }

        /// <summary>副表小标题。</summary>
        public string? SecondaryTitle { get; }

        /// <summary>取第 index 联版面。</summary>
        /// <param name="index">联序号，从 0 起。</param>
        public WorkTicketCopyLayout Copy(int index)
        {
            if (index < 0 || index >= Copies.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index),
                    "联序号 " + index + " 越界（共 " + Copies.Count + " 联）。");
            }
            return Copies[index];
        }
    }
}
