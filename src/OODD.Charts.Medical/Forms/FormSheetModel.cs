using System;
using System.Collections.Generic;
using OODD.Charts.Model;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms
{
    /// <summary>表格列定义。宽度用权重表示，实际宽度按权重占满纸张。</summary>
    public sealed class FormColumn
    {
        public FormColumn(string title, float weight, CellAlign align = CellAlign.Center, bool bold = false)
        {
            Title = title;
            Weight = weight <= 0f ? 1f : weight;
            Align = align;
            Bold = bold;
        }

        public string Title { get; set; }

        public float Weight { get; set; }

        public CellAlign Align { get; set; }

        /// <summary>列标题是否加粗（正文单元格另有 <see cref="FormCell.Bold"/>）。</summary>
        public bool Bold { get; set; }
    }

    /// <summary>一个表格单元格。空文本即空白格，用于待填写的纸质单据。</summary>
    public sealed class FormCell
    {
        public FormCell(string? text = null)
        {
            Text = text;
        }

        public string? Text { get; set; }

        public SKColor? Color { get; set; }

        public bool Bold { get; set; }

        public CellAlign? Align { get; set; }

        /// <summary>单元格字号覆盖；null 用模板令牌。</summary>
        public float? Size { get; set; }
    }

    /// <summary>表格行。高度为 null 时按令牌行高；长文本可自动换行加高。</summary>
    public sealed class FormRow
    {
        public FormRow(params FormCell[] cells)
        {
            Cells = new List<FormCell>(cells);
        }

        public FormRow(IEnumerable<FormCell> cells)
        {
            Cells = new List<FormCell>(cells);
        }

        /// <summary>行标识，供样例按名取行填值；null 时只能按序号取。</summary>
        public string? Key { get; set; }

        public List<FormCell> Cells { get; }

        public float? Height { get; set; }

        /// <summary>合计/结论行：加粗并加浅底。</summary>
        public bool IsTotal { get; set; }

        /// <summary>是否按列宽自动换行。叙述性栏目必须打开。</summary>
        public bool Wrap { get; set; } = true;
    }

    /// <summary>
    /// 表单的一个区段：标题带 + 列头 + 若干行 + 可选整段备注。
    /// 会诊单、核查表、台账、检验报告都能由若干区段拼出来。
    /// </summary>
    public sealed class FormSection
    {
        public FormSection(string title, params FormColumn[] columns)
        {
            Title = title;
            Columns = new List<FormColumn>(columns);
            Rows = new List<FormRow>();
        }

        public string Title { get; set; }

        /// <summary>稳定标识，用于从版面几何里取回本区段矩形；null 时按序号生成。</summary>
        public string? Key { get; set; }

        public List<FormColumn> Columns { get; }

        public List<FormRow> Rows { get; }

        /// <summary>整段合并备注（如“原因分析”“会诊意见”），自动换行。</summary>
        public string? Note { get; set; }

        public float? RowHeight { get; set; }

        public bool ShowColumnHeader { get; set; } = true;
    }

    /// <summary>
    /// 通用表格式单据文档。各具体单据通过派生类在构造函数里拼出自己的区段，
    /// 引擎只认区段/列/行，不认识任何具体表单名。
    /// </summary>
    public class FormSheetDocument : ChartDocument
    {
        public FormSheetDocument()
        {
            Sections = new List<FormSection>();
        }

        public IList<FormSection> Sections { get; }

        /// <summary>页脚左侧说明（如“本表一式两份”）。</summary>
        public string? FooterNote { get; set; }

        public FormSection AddSection(FormSection section)
        {
            Sections.Add(section);
            return section;
        }

        /// <summary>按稳定标识取区段；找不到返回 null。</summary>
        public FormSection? Section(string key)
        {
            foreach (FormSection section in Sections)
            {
                if (string.Equals(section.Key, key, StringComparison.Ordinal))
                {
                    return section;
                }
            }
            return null;
        }

        /// <summary>往指定区段追加一行并返回该行，供样例按名回填。行内单元格自动补齐到列数。</summary>
        public FormRow AddRow(string sectionKey, string? rowKey, params string?[] values)
        {
            FormSection section = Section(sectionKey)
                ?? throw new KeyNotFoundException("未登记的区段：" + sectionKey);
            FormRow row = new FormRow(ToCells(values)) { Key = rowKey };
            while (row.Cells.Count < section.Columns.Count)
            {
                row.Cells.Add(new FormCell());
            }
            section.Rows.Add(row);
            return row;
        }

        /// <summary>按区段 + 行标识取行；找不到返回 null。</summary>
        public FormRow? Row(string sectionKey, string rowKey)
        {
            FormSection? section = Section(sectionKey);
            if (section == null)
            {
                return null;
            }
            foreach (FormRow row in section.Rows)
            {
                if (string.Equals(row.Key, rowKey, StringComparison.Ordinal))
                {
                    return row;
                }
            }
            return null;
        }

        /// <summary>按名回填单元格；越界或行不存在返回 false。</summary>
        public bool SetCell(string sectionKey, string rowKey, int index, string? text)
        {
            FormRow? row = Row(sectionKey, rowKey);
            if (row == null || index < 0 || index >= row.Cells.Count)
            {
                return false;
            }
            row.Cells[index].Text = text;
            return true;
        }

        private static FormCell[] ToCells(string?[] values)
        {
            FormCell[] cells = new FormCell[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                cells[i] = new FormCell(values[i]);
            }
            return cells;
        }
    }

    /// <summary>表格式单据专属尺寸令牌。</summary>
    public sealed class FormSheetStyle
    {
        /// <summary>表单内容宽度（96dpi 逻辑像素）。</summary>
        public float Width = 1040f;

        public float SectionTitleHeight = 24f;
        public float ColumnHeaderHeight = 22f;
        public float RowHeight = 28f;
        public float NoteRowHeight = 56f;
        public float TextSize = 9f;
        public float TitleTextSize = 10f;
        public float CellPadding = 4f;
        public float LineSpacing = 1.35f;

        public FormSheetStyle Clone() => (FormSheetStyle)MemberwiseClone();
    }
}
