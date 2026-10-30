using System;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 逐字符方格框：死亡证明的"行政区划代码 6 格 / 编号 17 格"这类法定栅格。
    /// 符号只画方格；字符由模板用 <see cref="Painter.Text"/> 逐格落位，保证 SVG/PDF 里可检索。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 尺寸口径：<c>size</c> 就是<strong>单格高度</strong>（也是模板落字时应用的字号），
    /// 单格宽度 = size × <see cref="BoxWidthFactor"/>（默认 0.62，约等于一个数字的宽），
    /// 所以整排宽度由格数推出，缩放 size 时整排与字号同步走，不会出现"格子变了字没变"。
    /// </para>
    /// <para>
    /// 锚点语义：<see cref="Draw"/> 与 *Of 系列默认把 anchor 当作<strong>整排的几何中心</strong>；
    /// 表格式排版更常用左上角，故另给 <see cref="RowRectFromLeft"/> 与 <see cref="BoxCenterFromLeft"/> 一组。
    /// </para>
    /// </remarks>
    public sealed class DigitBox : IGlyph
    {
        private readonly int _boxCount;
        private readonly float _boxWidthFactor;
        private readonly float _gapFactor;
        private readonly bool _lastWider;

        /// <summary>构造逐字符方格框。</summary>
        /// <param name="boxCount">格数（如行政区划代码 6、编号 17），必须 ≥ 1。</param>
        /// <param name="boxWidthFactor">单格宽相对 size（格高）的因子，必须为正。</param>
        /// <param name="gapFactor">相邻格间隙相对 size 的因子；0 表示格与格共用边线（法定栅格的样子）。</param>
        /// <param name="lastWider">末格是否加宽（末位常放校验位/字母）。</param>
        public DigitBox(int boxCount, float boxWidthFactor = 0.62f, float gapFactor = 0f, bool lastWider = false)
        {
            if (boxCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(boxCount), "格数必须 ≥ 1。");
            }
            if (boxWidthFactor <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(boxWidthFactor), "单格宽因子必须为正。");
            }
            if (gapFactor < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(gapFactor), "间隙因子不能为负。");
            }

            _boxCount = boxCount;
            _boxWidthFactor = boxWidthFactor;
            _gapFactor = gapFactor;
            _lastWider = lastWider;
        }

        /// <summary>格数。</summary>
        public int BoxCount { get { return _boxCount; } }

        /// <summary>单格宽相对 size 的因子。</summary>
        public float BoxWidthFactor { get { return _boxWidthFactor; } }

        /// <summary>相邻格间隙相对 size 的因子。</summary>
        public float GapFactor { get { return _gapFactor; } }

        /// <summary>末格是否加宽。</summary>
        public bool LastWider { get { return _lastWider; } }

        /// <summary>末格宽度相对单格宽度的放大因子（仅 <see cref="LastWider"/> 为 true 时生效）。</summary>
        public float LastBoxWidthFactor { get; set; } = 1.5f;

        /// <summary>格线宽度相对 size 的因子。</summary>
        public float StrokeWidthFactor { get; set; } = 0.07f;

        /// <summary>单格宽（像素）。</summary>
        /// <param name="size">标称尺寸（格高）。</param>
        public float BoxWidthOf(float size)
        {
            return size * _boxWidthFactor;
        }

        /// <summary>第 index 格的宽度（像素；末格加宽时与之不同）。</summary>
        /// <param name="size">标称尺寸（格高）。</param>
        /// <param name="index">格序号，从 0 起。</param>
        public float BoxWidthOf(float size, int index)
        {
            ValidateIndex(index);
            bool last = _lastWider && index == _boxCount - 1;
            return BoxWidthOf(size) * (last ? LastBoxWidthFactor : 1f);
        }

        /// <summary>整排宽度（含间隙，像素）。</summary>
        /// <param name="size">标称尺寸（格高）。</param>
        public float RowWidthOf(float size)
        {
            float total = 0f;
            for (int i = 0; i < _boxCount; i++)
            {
                total += BoxWidthOf(size, i);
            }
            return total + _gapFactor * size * (_boxCount - 1);
        }

        /// <summary>画整排方格。不落任何字符。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="anchor">整排几何中心。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        /// <param name="color">主色。</param>
        public void Draw(Painter painter, SKPoint anchor, float size, SKColor color)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (size <= 0f)
            {
                return;
            }

            float stroke = size * StrokeWidthFactor;
            SKRect row = RowRect(anchor, size);

            if (_gapFactor <= 0f)
            {
                // 间隙为 0 时相邻格共用边线：画整排上下两条横线 + 每条竖边界，
                // 逐格描框会把共用边画成两条紧贴的线，栅格看起来会发糊。
                painter.Line(row.Left, row.Top, row.Right, row.Top, color, stroke);
                painter.Line(row.Left, row.Bottom, row.Right, row.Bottom, color, stroke);

                float x = row.Left;
                for (int i = 0; i < _boxCount; i++)
                {
                    painter.Line(x, row.Top, x, row.Bottom, color, stroke);
                    x += BoxWidthOf(size, i);
                }
                painter.Line(row.Right, row.Top, row.Right, row.Bottom, color, stroke);
                return;
            }

            for (int i = 0; i < _boxCount; i++)
            {
                painter.Frame(BoxRectOf(row.Left, anchor.Y, size, i), color, stroke);
            }
        }

        /// <summary>整排方格的外接矩形（anchor 为整排中心）。</summary>
        /// <param name="anchor">整排几何中心。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        public SKRect RowRect(SKPoint anchor, float size)
        {
            float width = RowWidthOf(size);
            return new SKRect(anchor.X - width * 0.5f, anchor.Y - size * 0.5f,
                              anchor.X + width * 0.5f, anchor.Y + size * 0.5f);
        }

        /// <summary>第 index 格中心（供模板落字），越界抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
        /// <param name="anchor">整排几何中心。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        /// <param name="index">格序号，从 0 起。</param>
        public SKPoint BoxCenterOf(SKPoint anchor, float size, int index)
        {
            ValidateIndex(index);
            SKRect row = RowRect(anchor, size);
            return CenterAt(row.Left, anchor.Y, size, index);
        }

        /// <summary>第 index 格矩形（anchor 为整排几何中心）。</summary>
        /// <param name="anchor">整排几何中心。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        /// <param name="index">格序号，从 0 起。</param>
        public SKRect BoxRect(SKPoint anchor, float size, int index)
        {
            ValidateIndex(index);
            SKRect row = RowRect(anchor, size);
            return BoxRectOf(row.Left, anchor.Y, size, index);
        }

        /// <summary>整排左起排列（anchor 为左上角）的外接矩形。</summary>
        /// <param name="topLeft">整排左上角。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        public SKRect RowRectFromLeft(SKPoint topLeft, float size)
        {
            return new SKRect(topLeft.X, topLeft.Y, topLeft.X + RowWidthOf(size), topLeft.Y + size);
        }

        /// <summary>整排左起排列时第 index 格中心（供模板落字）。</summary>
        /// <param name="topLeft">整排左上角。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        /// <param name="index">格序号，从 0 起。</param>
        public SKPoint BoxCenterFromLeft(SKPoint topLeft, float size, int index)
        {
            ValidateIndex(index);
            return CenterAt(topLeft.X, topLeft.Y + size * 0.5f, size, index);
        }

        /// <summary>整排左起排列时第 index 格矩形。</summary>
        /// <param name="topLeft">整排左上角。</param>
        /// <param name="size">标称尺寸（格高）。</param>
        /// <param name="index">格序号，从 0 起。</param>
        public SKRect BoxRectFromLeft(SKPoint topLeft, float size, int index)
        {
            ValidateIndex(index);
            return BoxRectOf(topLeft.X, topLeft.Y + size * 0.5f, size, index);
        }

        private SKPoint CenterAt(float rowLeft, float centerY, float size, int index)
        {
            SKRect box = BoxRectOf(rowLeft, centerY, size, index);
            return new SKPoint(box.MidX, box.MidY);
        }

        private SKRect BoxRectOf(float rowLeft, float centerY, float size, int index)
        {
            float gap = _gapFactor * size;
            float left = rowLeft;
            for (int i = 0; i < index; i++)
            {
                left += BoxWidthOf(size, i) + gap;
            }
            float width = BoxWidthOf(size, index);
            return new SKRect(left, centerY - size * 0.5f, left + width, centerY + size * 0.5f);
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _boxCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "格序号必须在 [0, BoxCount) 内。");
            }
        }
    }
}
