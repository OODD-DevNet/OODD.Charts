using System;
using System.Collections.Generic;
using System.Globalization;
using OODD.Charts.Geometry;
using SkiaSharp;

namespace OODD.Charts.Transit.LoadBalance
{
    /// <summary>
    /// 载重平衡图版面：双数值轴 + 若干重心包线 + 与图共用同一重心轴的载重表。
    /// <para>
    /// 包线判定与包线填充用的是<b>同一批</b> <see cref="EnvelopePolygon"/> 实例（A13），
    /// 表列中心与图 x 网格用的是<b>同一个</b> <see cref="NumericAxis"/> 实例。
    /// </para>
    /// </summary>
    public sealed class LoadBalanceLayout
    {
        public const string CgAxisKey = "cg";
        public const string WeightAxisKey = "weight";

        private readonly List<EnvelopePolygon> _envelopes;

        private LoadBalanceLayout(LoadBalanceDocument document, LoadBalanceStyle tokens,
                                  SKRect bounds, SKRect titleRect, SKRect noteRect, SKRect plotRect,
                                  SKRect weightAxisTitleRect, SKRect tableRect, SKRect footerRect,
                                  NumericAxis cgAxis, NumericAxis weightAxis,
                                  List<EnvelopePolygon> envelopes,
                                  IReadOnlyList<AxisMark> cgMarks, IReadOnlyList<AxisMark> weightMarks)
        {
            Document = document;
            Tokens = tokens;
            Bounds = bounds;
            TitleRect = titleRect;
            NoteRect = noteRect;
            PlotRect = plotRect;
            WeightAxisTitleRect = weightAxisTitleRect;
            TableRect = tableRect;
            FooterRect = footerRect;
            CgAxis = cgAxis;
            WeightAxis = weightAxis;
            _envelopes = envelopes;
            CgMarks = cgMarks;
            WeightMarks = weightMarks;
        }

        public LoadBalanceDocument Document { get; }
        public LoadBalanceStyle Tokens { get; }

        public SKRect Bounds { get; }
        public SKRect TitleRect { get; }
        public SKRect NoteRect { get; }
        public SKRect PlotRect { get; }

        /// <summary>重量轴标题「重量（kg）」的独占带：绘图区上缘之上，斜纹与网格都不进这一带。</summary>
        public SKRect WeightAxisTitleRect { get; }

        public SKRect TableRect { get; }
        public SKRect FooterRect { get; }

        public NumericAxis CgAxis { get; }
        public NumericAxis WeightAxis { get; }

        /// <summary>包线（数据坐标），判定与填充同源。</summary>
        public IReadOnlyList<EnvelopePolygon> Envelopes => _envelopes;

        public IReadOnlyList<AxisMark> CgMarks { get; }
        public IReadOnlyList<AxisMark> WeightMarks { get; }

        public static LoadBalanceLayout Compute(LoadBalanceDocument document, LoadBalanceStyle tokens)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }
            if (!(document.CgMax > document.CgMin))
            {
                throw new InvalidOperationException("重心轴上下限无效：[" + document.CgMin + "," + document.CgMax + "]");
            }
            if (!(document.WeightMax > document.WeightMin))
            {
                throw new InvalidOperationException("重量轴上下限无效：[" + document.WeightMin + "," + document.WeightMax + "]");
            }

            TransitStyle frame = tokens.Frame;
            float width = frame.Width;
            float height = frame.Height;
            float left = frame.Margin + frame.BindingMargin;
            float right = width - frame.Margin;

            SKRect bounds = new SKRect(0f, 0f, width, height);
            SKRect titleRect = new SKRect(left, frame.Margin, right, frame.Margin + frame.TitleHeight);
            SKRect noteRect = new SKRect(left, titleRect.Bottom, right, titleRect.Bottom + frame.SubtitleSize * 2.4f);

            float plotLeft = left + tokens.AxisWidth;
            float plotRight = right - tokens.AxisWidth;
            float plotTop = noteRect.Bottom + tokens.AxisTitleBand;
            float plotBottom = plotTop + tokens.PlotHeight;
            SKRect plotRect = new SKRect(plotLeft, plotTop, plotRight, plotBottom);
            SKRect weightAxisTitleRect = new SKRect(plotLeft, plotTop - tokens.AxisTitleBand, plotRight, plotTop);

            float tableTop = plotBottom + tokens.TableGap;
            SKRect tableRect = new SKRect(plotLeft, tableTop, plotRight, tableTop + tokens.TableHeight);
            SKRect footerRect = new SKRect(left, height - frame.Margin - frame.FooterHeight,
                right, height - frame.Margin);

            if (!(tableRect.Bottom < footerRect.Top))
            {
                throw new InvalidOperationException(
                    "载重表压到了页脚：表底 " + tableRect.Bottom.ToString("0.#", CultureInfo.InvariantCulture)
                    + "，页脚顶 " + footerRect.Top.ToString("0.#", CultureInfo.InvariantCulture));
            }

            NumericAxis cgAxis = new NumericAxis(CgAxisKey, document.CgMin, document.CgMax,
                plotRect.Left, plotRect.Right, AxisDirection.HorizontalRight);
            NumericAxis weightAxis = new NumericAxis(WeightAxisKey, document.WeightMin, document.WeightMax,
                plotRect.Bottom, plotRect.Top, AxisDirection.VerticalUp);

            List<EnvelopePolygon> envelopes = new List<EnvelopePolygon>();
            for (int i = 0; i < document.Envelopes.Count; i++)
            {
                CgEnvelope data = document.Envelopes[i];
                envelopes.Add(new EnvelopePolygon(data.Key, data.Label, data.Vertices));
            }

            List<AxisTier> cgTiers = new List<AxisTier>
            {
                new AxisTier(tokens.CgTickStep, RulerTier.Medium, true),
            };
            List<AxisTier> weightTiers = new List<AxisTier>
            {
                new AxisTier(tokens.WeightTickStep, RulerTier.Medium, true),
            };

            return new LoadBalanceLayout(document, tokens, bounds, titleRect, noteRect, plotRect,
                weightAxisTitleRect, tableRect, footerRect, cgAxis, weightAxis, envelopes,
                AxisRuler.Marks(cgAxis, cgTiers), AxisRuler.Marks(weightAxis, weightTiers));
        }

        /// <summary>数据坐标 → 像素。</summary>
        public SKPoint PixelOf(double cgPercentMac, double weightKg)
        {
            return new SKPoint(CgAxis.PositionOf(cgPercentMac), WeightAxis.PositionOf(weightKg));
        }

        /// <summary>一条包线的像素顶点（与填充走同一批顶点）。</summary>
        public SKPoint[] PixelVerticesOf(EnvelopePolygon envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            IReadOnlyList<SKPoint> vertices = envelope.Vertices;
            SKPoint[] pixels = new SKPoint[vertices.Count];
            for (int i = 0; i < vertices.Count; i++)
            {
                pixels[i] = PixelOf(vertices[i].X, vertices[i].Y);
            }
            return pixels;
        }

        /// <summary>点是否落在任一条包线内（含边界，边界算内）。判定与填充调用同一批实例。</summary>
        public bool IsInside(double cgPercentMac, double weightKg)
        {
            for (int i = 0; i < _envelopes.Count; i++)
            {
                if (_envelopes[i].Contains(cgPercentMac, weightKg))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>载重表第 <paramref name="index"/> 列的中心 x（与图上该重心值的 x 同源）。</summary>
        public float ColumnCenter(int index)
        {
            if (index < 0 || index >= Document.TableCgValues.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return CgAxis.PositionOf(Document.TableCgValues[index]);
        }

        /// <summary>载重表表头行矩形。</summary>
        public SKRect TableHeaderRect()
        {
            return new SKRect(TableRect.Left, TableRect.Top, TableRect.Right, TableRect.Top + Tokens.TableRowHeight);
        }

        /// <summary>载重表第 <paramref name="row"/> 行（0 起，表头之后）的矩形。</summary>
        public SKRect TableRowRect(int row)
        {
            float top = TableRect.Top + Tokens.TableRowHeight * (row + 1);
            return new SKRect(TableRect.Left, top, TableRect.Right, top + Tokens.TableRowHeight);
        }

        /// <summary>载重表第 <paramref name="row"/> 行、第 <paramref name="column"/> 列的文字框。</summary>
        public SKRect TableCellRect(int row, int column)
        {
            float center = ColumnCenter(column);
            SKRect rowRect = TableRowRect(row);
            float half = Tokens.TableRowHeight * 1.1f;
            return new SKRect(center - half, rowRect.Top, center + half, rowRect.Bottom);
        }

        /// <summary>轴刻度数字（不变文化）。</summary>
        public static string Number(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }
}
