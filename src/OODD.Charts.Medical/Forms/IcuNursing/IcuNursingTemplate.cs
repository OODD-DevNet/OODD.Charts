using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;

namespace OODD.Charts.Medical.Forms.IcuNursing
{
    /// <summary>ICU 护理记录单的求解结果。</summary>
    public sealed class IcuNursingLayout
    {
        internal IcuNursingLayout(SKRect bounds, SKRect headerBand, SKRect tableHeaderRect, SKRect dataRect,
            HeaderTableLayout header, SideAttachedTable sideTable, SKRect sidePlaced,
            IReadOnlyList<SKRect> mainRows, float sideLabelWidth)
        {
            Bounds = bounds;
            HeaderBand = headerBand;
            TableHeaderRect = tableHeaderRect;
            DataRect = dataRect;
            Header = header;
            SideTable = sideTable;
            SidePlaced = sidePlaced;
            MainRows = mainRows;
            SideLabelWidth = sideLabelWidth;
        }

        public SKRect Bounds { get; }
        public SKRect HeaderBand { get; }

        /// <summary>三级表头区域。</summary>
        public SKRect TableHeaderRect { get; }

        /// <summary>主表数据行区域（不含表头）。</summary>
        public SKRect DataRect { get; }

        public HeaderTableLayout Header { get; }
        public ColumnHeaderNode Root => Header.Root;

        public SideAttachedTable SideTable { get; }
        public SKRect SidePlaced { get; }

        public IReadOnlyList<SKRect> MainRows { get; }
        public float SideLabelWidth { get; }

        /// <summary>主表第 row 行、第 columnKey 列的单元格。</summary>
        public SKRect CellRect(int row, string columnKey)
        {
            if (row < 0 || row >= MainRows.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(row));
            }
            ColumnHeaderNode leaf = Header.FindNode(columnKey);
            int index = Header.LeafIndexOf(leaf);
            SKRect bounds = MainRows[row];
            return new SKRect(Header.LeafBounds[index], bounds.Top,
                Header.LeafBounds[index + 1], bounds.Bottom);
        }

        /// <summary>侧表第 index 行。</summary>
        public SKRect SideRowRect(int index)
        {
            return SideTable.RowRect(index, SidePlaced);
        }

        /// <summary>主表行标签列（在表头树左侧）。</summary>
        public SKRect RowLabelRect(int row)
        {
            SKRect bounds = MainRows[row];
            return new SKRect(bounds.Left, bounds.Top, Header.Area.Left, bounds.Bottom);
        }
    }

    /// <summary>
    /// ICU 护理记录单 medical.icu-nursing。
    /// <para>
    /// 表头是 <see cref="ColumnHeaderNode"/> 三级分组树（根 / 组 / 叶子），由
    /// <see cref="HeaderTableLayout"/> 落到像素；其中"中心静脉压"是对角斜线划掉的叶子，
    /// 只画斜线、不写文字。主表右侧挂 <see cref="SideAttachedTable"/> 交班小结，
    /// 其行高与主表行高无关。
    /// </para>
    /// </summary>
    public sealed class IcuNursingTemplate : IChartTemplate
    {
        public const string SideTableKey = "icu-handover";

        public string Key => "medical.icu-nursing";
        public string DisplayName => "ICU 护理记录单";

        /// <summary>表单专属令牌；改令牌即改版面。</summary>
        public IcuNursingStyle Tokens { get; set; } = new IcuNursingStyle();

        public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
        {
            IcuNursingDocument doc = Cast(document);
            IcuNursingStyle t = Tokens;

            float width = t.PaperWidth;
            float y = 0f;

            float hospitalBottom = y + t.HospitalRowHeight;
            float titleBottom = hospitalBottom + t.TitleRowHeight;
            int patientRows = Math.Max(1, Primitives.HeaderFieldRowCount(
                fonts, doc.Patient.EnumerateAll(), style, width));
            float patientBottom = titleBottom + patientRows * style.Header.PatientRowHeight;
            SKRect headerBand = new SKRect(0f, 0f, width, patientBottom);
            y = patientBottom + t.HeaderGap;

            float mainLeft = t.Margin;
            float sideRight = width - t.Margin;
            float mainRight = sideRight - t.SideTableWidth - t.SideTableGap;
            float tableLeft = mainLeft + t.RowLabelWidth;

            ColumnHeaderNode root = BuildHeaderTree();
            float tableHeaderHeight = (root.Depth + 1) * t.HeaderLevelHeight;
            SKRect tableHeaderRect = new SKRect(tableLeft, y, mainRight, y + tableHeaderHeight);
            HeaderTableLayout header = new HeaderTableLayout(root, tableHeaderRect, t.HeaderLevelHeight, false);
            y = tableHeaderRect.Bottom;

            int rowCount = Math.Max(1, doc.Records.Count);
            List<SKRect> mainRows = new List<SKRect>(rowCount);
            for (int i = 0; i < rowCount; i++)
            {
                float top = y + i * t.DataRowHeight;
                mainRows.Add(new SKRect(tableLeft, top, mainRight, top + t.DataRowHeight));
            }
            SKRect dataRect = new SKRect(tableLeft, y, mainRight, y + rowCount * t.DataRowHeight);
            float dataBottom = dataRect.Bottom;

            SideAttachedTable side = new SideAttachedTable(SideTableKey,
                new[] { "交班小结", "白班", "夜班", "备注" }, t.SideRowHeight, t.SideTableGap);
            side.Width = t.SideTableWidth;
            SKRect sidePlaced = side.PlaceAt(mainRight, dataRect.Top);

            float bottom = Math.Max(dataBottom, sidePlaced.Bottom) + t.FooterHeight;

            IcuNursingLayout layout = new IcuNursingLayout(
                new SKRect(0f, 0f, width, bottom), headerBand, tableHeaderRect, dataRect,
                header, side, sidePlaced, mainRows, t.SideLabelWidth);

            return ChartGeometry.ForLayout(layout.Bounds, dataRect, layout);
        }

        /// <summary>构造三级表头树：根 → 3 个组 → 9 个叶子。</summary>
        public static ColumnHeaderNode BuildHeaderTree()
        {
            ColumnHeaderNode heartRate = new ColumnHeaderNode(IcuNursingColumns.HeartRate, "心率");
            ColumnHeaderNode respiration = new ColumnHeaderNode(IcuNursingColumns.Respiration, "呼吸/次");
            ColumnHeaderNode temperature = new ColumnHeaderNode(IcuNursingColumns.Temperature, "体温℃");

            ColumnHeaderNode systolic = new ColumnHeaderNode(IcuNursingColumns.Systolic, "收缩压");
            ColumnHeaderNode diastolic = new ColumnHeaderNode(IcuNursingColumns.Diastolic, "舒张压");
            ColumnHeaderNode cvp = new ColumnHeaderNode(IcuNursingColumns.CentralVenousPressure, "中心静脉压");
            cvp.Diagonal = true;
            cvp.DiagonalDirection = DiagonalDirection.UpperLeftToLowerRight;

            ColumnHeaderNode intake = new ColumnHeaderNode(IcuNursingColumns.Intake, "入量");
            ColumnHeaderNode output = new ColumnHeaderNode(IcuNursingColumns.Output, "出量");
            ColumnHeaderNode urine = new ColumnHeaderNode(IcuNursingColumns.Urine, "尿量");

            ColumnHeaderNode vitals = new ColumnHeaderNode("vitals", "生命体征",
                new[] { heartRate, respiration, temperature });
            ColumnHeaderNode circulation = new ColumnHeaderNode("circulation", "循环",
                new[] { systolic, diastolic, cvp });
            ColumnHeaderNode balance = new ColumnHeaderNode("balance", "出入量",
                new[] { intake, output, urine });

            return new ColumnHeaderNode("root", "项目", new[] { vitals, circulation, balance });
        }

        public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
        {
            IcuNursingDocument doc = Cast(document);
            IcuNursingLayout layout = geometry.LayoutOf<IcuNursingLayout>()
                ?? throw new InvalidOperationException("几何里没有 IcuNursingLayout。");

            ChartStyle style = painter.Style;
            IcuNursingStyle t = Tokens;
            SKColor line = style.Colors.Grid;

            DrawHeader(painter, layout, doc, style, t);
            DrawTableHeader(painter, layout, style, line);
            DrawDataRows(painter, layout, doc, style, line);
            DrawSideTable(painter, layout, doc, style, line);
            DrawFrame(painter, layout, style, line);

            painter.TextInBox("表头为三级分组；\"中心静脉压\"斜线格按本专科不适用处理，格内不写文字。",
                new SKRect(0f, layout.Bounds.Bottom - t.FooterHeight, layout.Bounds.Right, layout.Bounds.Bottom),
                new FontKey(style.Text.Family, style.Text.AnnotationSize),
                style.Colors.Text, CellAlign.Near, CellAlign.Center, t.Margin);
        }

        private static void DrawHeader(Painter p, IcuNursingLayout layout, IcuNursingDocument doc,
            ChartStyle style, IcuNursingStyle t)
        {
            float hospitalBottom = layout.HeaderBand.Top + t.HospitalRowHeight;
            float titleBottom = hospitalBottom + t.TitleRowHeight;
            p.TextInBox(doc.HospitalName ?? string.Empty,
                new SKRect(0f, layout.HeaderBand.Top, layout.Bounds.Right, hospitalBottom),
                new FontKey(style.Text.Family, style.Text.HospitalSize, style.Text.HospitalBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);
            p.TextInBox(doc.FormTitle ?? "ICU 护理记录单",
                new SKRect(0f, hospitalBottom, layout.Bounds.Right, titleBottom),
                new FontKey(style.Text.Family, style.Text.TitleSize, style.Text.TitleBold),
                style.Colors.Text, CellAlign.Center, CellAlign.Center, 0f);

            if (!string.IsNullOrEmpty(doc.HeaderRightNote))
            {
                p.TextInBox(doc.HeaderRightNote!,
                    new SKRect(0f, hospitalBottom, layout.Bounds.Right, titleBottom),
                    new FontKey(style.Text.Family, style.Text.HeaderSize),
                    style.Colors.Text, CellAlign.Far, CellAlign.Center, 6f);
            }

            Primitives.DrawHeaderFields(p, new SKRect(0f, titleBottom, layout.Bounds.Right,
                    layout.HeaderBand.Bottom), doc.Patient.EnumerateAll(), style.Colors.Text);
        }

        private static void DrawTableHeader(Painter p, IcuNursingLayout layout, ChartStyle style, SKColor line)
        {
            FontKey key = new FontKey(style.Text.Family, style.Text.CellLabelSize);
            DrawNode(p, layout.Root, layout, key, style.Colors.Text, line, style);

            for (int level = 1; level < layout.Header.LevelCount; level++)
            {
                SKRect rect = layout.Header.LevelRect(level);
                p.Line(rect.Left, rect.Top, rect.Right, rect.Top, line, style.Grid.MinorStroke);
            }
            p.Line(layout.TableHeaderRect.Left, layout.TableHeaderRect.Bottom,
                layout.TableHeaderRect.Right, layout.TableHeaderRect.Bottom, line, style.Grid.MajorStroke);
        }

        private static void DrawNode(Painter p, ColumnHeaderNode node, IcuNursingLayout layout,
            FontKey key, SKColor textColor, SKColor line, ChartStyle style)
        {
            SKRect rect = layout.Header.RectOf(node);
            p.Frame(rect, line, style.Grid.MinorStroke);

            if (node.Diagonal)
            {
                SKPoint a;
                SKPoint b;
                ColumnHeaderNode.DiagonalEnds(rect, node.DiagonalDirection, out a, out b);
                p.Line(a.X, a.Y, b.X, b.Y, line, style.Grid.MinorStroke);
            }
            else
            {
                p.TextInBox(node.Label, rect, key, textColor, CellAlign.Center, CellAlign.Center, 1f);
            }

            for (int i = 0; i < node.Children.Count; i++)
            {
                DrawNode(p, node.Children[i], layout, key, textColor, line, style);
            }
        }

        private static void DrawDataRows(Painter p, IcuNursingLayout layout, IcuNursingDocument doc,
            ChartStyle style, SKColor line)
        {
            FontKey key = new FontKey(style.Text.Family, style.Text.CellValueSize);
            for (int row = 0; row < layout.MainRows.Count; row++)
            {
                SKRect rect = layout.MainRows[row];
                p.Line(rect.Left, rect.Bottom, rect.Right, rect.Bottom, line, style.Grid.MinorStroke);

                string label = row < doc.Records.Count ? doc.Records[row].TimeLabel : string.Empty;
                p.TextInBox(label, layout.RowLabelRect(row), key, style.Colors.Text,
                    CellAlign.Center, CellAlign.Center, 1f);

                for (int col = 0; col < IcuNursingColumns.All.Count; col++)
                {
                    string columnKey = IcuNursingColumns.All[col];
                    SKRect cell = layout.CellRect(row, columnKey);
                    p.Line(cell.Right, cell.Top, cell.Right, cell.Bottom, line, style.Grid.MinorStroke);
                    if (row < doc.Records.Count)
                    {
                        p.TextInBox(doc.Records[row].Get(columnKey), cell, key, style.Colors.CellValue,
                            CellAlign.Center, CellAlign.Center, 1f);
                    }
                }
            }

            p.Line(layout.DataRect.Left, layout.DataRect.Top, layout.DataRect.Right, layout.DataRect.Top,
                line, style.Grid.MinorStroke);
        }

        private static void DrawSideTable(Painter p, IcuNursingLayout layout, IcuNursingDocument doc,
            ChartStyle style, SKColor line)
        {
            FontKey labelKey = new FontKey(style.Text.Family, style.Text.CellLabelSize);
            FontKey valueKey = new FontKey(style.Text.Family, style.Text.CellValueSize);
            p.Rect(layout.SidePlaced, line, style.Grid.MajorStroke);

            for (int row = 0; row < layout.SideTable.RowCount; row++)
            {
                SKRect rect = layout.SideRowRect(row);
                if (row > 0)
                {
                    p.Line(rect.Left, rect.Top, rect.Right, rect.Top, line, style.Grid.MinorStroke);
                }
                p.TextInBox(layout.SideTable.RowLabels[row],
                    new SKRect(rect.Left, rect.Top, rect.Left + layout.SideLabelWidth, rect.Bottom),
                    labelKey, style.Colors.Text, CellAlign.Center, CellAlign.Center, 1f);
                p.Line(rect.Left + layout.SideLabelWidth, rect.Top,
                    rect.Left + layout.SideLabelWidth, rect.Bottom, line, style.Grid.MinorStroke);
                string value = row < doc.HandoverNotes.Count ? doc.HandoverNotes[row] : string.Empty;
                p.TextInBox(value,
                    new SKRect(rect.Left + layout.SideLabelWidth, rect.Top, rect.Right, rect.Bottom),
                    valueKey, style.Colors.CellValue, CellAlign.Near, CellAlign.Center, 2f);
            }
        }

        private static void DrawFrame(Painter p, IcuNursingLayout layout, ChartStyle style, SKColor line)
        {
            p.Line(layout.TableHeaderRect.Left, layout.TableHeaderRect.Top, layout.TableHeaderRect.Left,
                layout.DataRect.Bottom, line, style.Grid.BorderStroke);
            p.Line(layout.DataRect.Left, layout.DataRect.Bottom, layout.DataRect.Right,
                layout.DataRect.Bottom, line, style.Grid.BorderStroke);
        }

        private static IcuNursingDocument Cast(ChartDocument document)
        {
            IcuNursingDocument? typed = document as IcuNursingDocument;
            if (typed == null)
            {
                throw new ArgumentException("本模板只接受 " + nameof(IcuNursingDocument) + "。",
                    nameof(document));
            }
            return typed;
        }
    }
}
