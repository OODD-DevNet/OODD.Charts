using System;
using System.Collections.Generic;
using OODD.Charts.Fonts;
using OODD.Charts.Geometry;
using OODD.Charts.Model;
using OODD.Charts.Render;
using OODD.Charts.Style;
using OODD.Charts.Templates;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// T214：图样填充与 <see cref="Painter"/> 的路径/裁剪/旋转/箭头原语。
    /// <para>
    /// 判据全部落在像素上（docs/06 §3.3 的验收行）：
    /// <list type="bullet">
    /// <item><description>图样按真实 mm 密排——<b>两个 DPI 下 tile 数与逻辑周期一致</b>；</description></item>
    /// <item><description>高 DPI 下纹线由位图 1:1 落墨，不靠放大凑（放大必然产生半覆盖的灰像素）；</description></item>
    /// <item><description>clip 栈配对，<see cref="Painter.ClipDepth"/> 归零，未配对时 <see cref="Painter.Dispose"/> 抛；</description></item>
    /// <item><description><see cref="Painter.RotateAround"/> 只转几何、出来就把画布状态还原，
    /// 所以闭包之后的文字不会被连带旋转（A17）。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 两条渲染路径分别取证：<b>宿主直绘</b>（宿主画布已带设备缩放，WPF/MAUI 那条）与
    /// <b>Chart 光栅管线</b>（PNG/PDF 那条，先录 <see cref="SKPicture"/> 再 <c>canvas.Scale</c>）。
    /// 两者的 shader 生成时机不同，图样在高 DPI 下的表现也不同，所以分开钉。
    /// </para>
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class PainterPrimitiveTests
    {
        /// <summary>图样间距（逻辑像素）：取整数是为了让纹线正好落在设备像素边界上，判据才干净。</summary>
        private const int SpacingPx = 16;

        /// <summary>纹线线粗（逻辑像素）：偶数，1.5 倍缩放后仍落在整像素边界。</summary>
        private const int StrokePx = 4;

        /// <summary>间距的 mm 声明（≈4.2333mm）——图样是按实物毫米写的，不是按像素写的。</summary>
        private const float SpacingMm = SpacingPx / Unit.PxPerMm;

        /// <summary>线粗的 mm 声明（≈1.0583mm）。</summary>
        private const float StrokeMm = StrokePx / Unit.PxPerMm;

        private static readonly PatternFill Hatch =
            PatternFill.Of(PatternKind.Horizontal, SpacingMm, StrokeMm, SKColors.Black);

        /// <summary>
        /// 第二个间距：<b>故意取不成整除的 11 像素</b>——只有 tile 边长在设备缩放下要取整，
        /// "按设备重画"与"放大低分辨率 tile"才会在像素上留下可测的差别。
        /// </summary>
        private const int FineSpacingPx = 11;

        private static readonly PatternFill FineHatch =
            PatternFill.Of(PatternKind.Horizontal, FineSpacingPx / Unit.PxPerMm, StrokeMm, SKColors.Black);

        /// <summary>图样带：逻辑坐标 (0,2)-(200,158)，上下边都落在空白间隙里，免得边界与纹线粘连。</summary>
        private static readonly SKRect PatchRect = new SKRect(0f, 2f, 200f, 158f);

        private const float PatchWidth = 200f;
        private const float PatchHeight = 160f;

        /// <summary>期望的纹线中心（逻辑像素）：tile 内 cy=8、周期 16，故 8、24、40…</summary>
        private static IReadOnlyList<double> ExpectedCenters()
        {
            List<double> centers = new List<double>();
            for (double c = SpacingPx / 2d; c <= PatchRect.Bottom - StrokePx / 2d; c += SpacingPx)
            {
                centers.Add(c);
            }
            return centers;
        }

        // ------------------------------------------------------------ 宿主直绘路径

        [Fact]
        public void 图样_两DPI下tile数与逻辑周期一致()
        {
            IReadOnlyList<double> expected = ExpectedCenters();
            Assert.True(expected.Count >= 8, "版面太小，测不出周期：期望纹线 " + expected.Count + " 条");

            using (ImageProbe nineSix = RenderDirect(96f, Hatch))
            using (ImageProbe oneFortyFour = RenderDirect(144f, Hatch))
            {
                IReadOnlyList<double> a = LogicalBandCenters(nineSix, 96f);
                IReadOnlyList<double> b = LogicalBandCenters(oneFortyFour, 144f);

                // tile 数一致：这是"间距是实物毫米"的直接含义。
                Assert.True(a.Count == expected.Count,
                            "96dpi 纹线数 " + a.Count + "，期望 " + expected.Count + "：" + Join(a));
                Assert.True(b.Count == expected.Count,
                            "144dpi 纹线数 " + b.Count + "，期望 " + expected.Count + "：" + Join(b));

                AssertPositions(a, expected, "96dpi 直绘");
                AssertPositions(b, expected, "144dpi 直绘");
            }
        }

        [Fact]
        public void 图样_直绘高DPI下纹线一比一落墨不靠放大()
        {
            // tile 位图按设备缩放重画，所以放大倍率进了位图、没进采样：
            // 全程不存在半覆盖像素（灰阶）。若改成"低分辨率 tile + 放大"，边缘必然出现灰行。
            using (ImageProbe oneFortyFour = RenderDirect(144f, Hatch))
            {
                int gray = MidGrayPixels(oneFortyFour, 144f);
                Assert.True(gray == 0, "144dpi 直绘出现 " + gray + " 个半覆盖像素：图样被放大了，不是重新密排");
            }
        }

        [Fact]
        public void 图样_线粗在逻辑像素口径上不随DPI变粗()
        {
            using (ImageProbe nineSix = RenderDirect(96f, Hatch))
            using (ImageProbe oneFortyFour = RenderDirect(144f, Hatch))
            {
                double a = MedianLogicalRunWidth(nineSix, 96f);
                double b = MedianLogicalRunWidth(oneFortyFour, 144f);

                Assert.True(Math.Abs(a - StrokePx) <= 0.6, "96dpi 逻辑线粗 " + a + "，声明 " + StrokePx);
                Assert.True(Math.Abs(b - StrokePx) <= 0.6, "144dpi 逻辑线粗 " + b + "，声明 " + StrokePx);
            }
        }

        // ------------------------------------------------------------ Chart 光栅管线

        [Fact]
        public void 图样_经Chart光栅管线两DPI逻辑周期一致()
        {
            IReadOnlyList<double> expected = ExpectedCenters();
            using (ImageProbe nineSix = RenderChart(96f, Hatch))
            using (ImageProbe oneFortyFour = RenderChart(144f, Hatch))
            {
                AssertPositions(LogicalBandCenters(nineSix, 96f), expected, "96dpi 管线");
                AssertPositions(LogicalBandCenters(oneFortyFour, 144f), expected, "144dpi 管线");
            }
        }

        [Fact]
        public void 图样_经Chart光栅管线高DPI同样不放大()
        {
            // Chart.RenderImage 是"先录 SKPicture（此时 CTM 为单位阵）再 canvas.Scale"，
            // 所以 tile 只有在录制时就按目标设备缩放生成，高 DPI 才谈得上 1:1。
            // 这条用例钉住管线没有退化成放大位图。
            using (ImageProbe oneFortyFour = RenderChart(144f, Hatch))
            {
                int gray = MidGrayPixels(oneFortyFour, 144f);
                Assert.True(gray == 0, "144dpi 管线出现 " + gray + " 个半覆盖像素：录制的 tile 没有按目标 DPI 重画");
            }
        }

        [Fact]
        public void 图样_高DPI导出按设备分辨率重画tile而不是放大96dpi的tile()
        {
            // 判据来自实测标定，不是推理：300dpi 下同一份斜纹的墨迹占比，
            //   按设备重画 tile ≈ 0.326，放大 96dpi 的 tile ≈ 0.302（少 8%——阶梯被整数放大后量化掉了细节）。
            // 为什么不用周期或线粗当裁判：最近邻采样下两条路只差一个采样栅格相位，
            // 正交特征的偏差天生小于一个像素（实测 11.0 vs 10.88），钉不住这件事。
            const float Dpi = 300f;
            PatternFill diagonal = PatternFill.Of(PatternKind.Diagonal, SpacingMm, StrokeMm, SKColors.Black);

            using (ImageProbe direct = RenderDirect(Dpi, diagonal))
            using (ImageProbe pipeline = RenderChart(Dpi, diagonal))
            {
                double a = InkRatio(direct, Dpi);
                double b = InkRatio(pipeline, Dpi);

                Assert.True(a > 0.2 && a < 0.45, "直绘的斜纹占比异常，取样窗口没对准图样：" + a);
                Assert.True(Math.Abs(a - b) <= 0.01,
                            "300dpi 下两条路的斜纹墨迹占比不同（宿主直绘 " + a + " vs Chart 光栅管线 " + b
                            + "）：管线用的仍是 96dpi 生成的 tile，说明录制 SKPicture 时没把目标 DPI 传给 Painter");
            }
        }

        [Fact]
        public void 图样_高DPI下逻辑周期仍是声明的毫米间距()
        {
            // 间距取不成整除的 11 像素：300dpi 下 tile 边长要取整（round(34.375)=34），
            // 于是逻辑周期会带上一格设备量化——断言它仍锁在声明值附近，而不是随 DPI 漂移。
            const float Dpi = 300f;
            using (ImageProbe direct = RenderDirect(Dpi, FineHatch))
            using (ImageProbe pipeline = RenderChart(Dpi, FineHatch))
            {
                double a = AverageLogicalPeriod(direct, Dpi);
                double b = AverageLogicalPeriod(pipeline, Dpi);
                Assert.True(Math.Abs(a - FineSpacingPx) <= 0.15, "300dpi 直绘逻辑周期 " + a + "，声明 " + FineSpacingPx);
                Assert.True(Math.Abs(b - FineSpacingPx) <= 0.15, "300dpi 管线逻辑周期 " + b + "，声明 " + FineSpacingPx);
            }
        }

        private static double InkRatio(ImageProbe probe, float dpi)
        {
            float scale = dpi / 96f;
            int left = (int)Math.Round(4f * scale);
            int right = (int)Math.Round(196f * scale);
            int top = (int)Math.Round(6f * scale);
            int bottom = (int)Math.Round(154f * scale);
            long ink = 0;
            long total = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    if (probe.IsInk(x, y))
                    {
                        ink++;
                    }
                    total++;
                }
            }
            return (double)ink / total;
        }

        // ------------------------------------------------------------ shader 缓存

        [Fact]
        public void 图样_同一样式只生成一份tile()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                using (SKPath path = new SKPath())
                {
                    path.AddRect(PatchRect);
                    painter.FillPathWithPattern(path, Hatch);
                    painter.FillPathWithPattern(path, Hatch);
                    painter.FillPathWithPattern(path, PatternFill.Of(
                        PatternKind.Horizontal, SpacingMm, StrokeMm, SKColors.Black));

                    Assert.Equal(1, painter.ShaderCount);

                    painter.FillPathWithPattern(path, PatternFill.Of(
                        PatternKind.Diagonal, SpacingMm, StrokeMm, SKColors.Black));
                    Assert.Equal(2, painter.ShaderCount);

                    painter.FillPathWithPattern(path, PatternFill.Of(
                        PatternKind.Horizontal, SpacingMm, StrokeMm, SKColors.Red));
                    Assert.Equal(3, painter.ShaderCount);
                }
            }
        }

        [Fact]
        public void 图样_毫米令牌浮点尾数不打散缓存()
        {
            // 版面里 mm 由令牌换算而来，尾数会漂；缓存键量化到 1e-6，
            // 否则同一图样生成两份 tile，导出版本之间出现无意义差异。
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                using (SKPath path = new SKPath())
                {
                    path.AddRect(PatchRect);
                    painter.FillPathWithPattern(path, PatternFill.Of(
                        PatternKind.Horizontal, SpacingMm, StrokeMm, SKColors.Black));
                    painter.FillPathWithPattern(path, PatternFill.Of(
                        PatternKind.Horizontal, SpacingMm + 1e-7f, StrokeMm + 1e-7f, SKColors.Black));

                    Assert.Equal(1, painter.ShaderCount);
                }
            }
        }

        [Fact]
        public void 图样_声明校验()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PatternFill.Of(PatternKind.Dot, 0f, 0.2f, SKColors.Black));
            Assert.Throws<ArgumentOutOfRangeException>(() => PatternFill.Of(PatternKind.Dot, -1f, 0.2f, SKColors.Black));
            Assert.Throws<ArgumentOutOfRangeException>(() => PatternFill.Of(PatternKind.Dot, float.NaN, 0.2f, SKColors.Black));
            Assert.Throws<ArgumentOutOfRangeException>(() => PatternFill.Of(PatternKind.Dot, 2f, 0f, SKColors.Black));
            Assert.Throws<ArgumentOutOfRangeException>(() => PatternFill.Of(PatternKind.Dot, 2f, float.PositiveInfinity, SKColors.Black));

            PatternFill a = PatternFill.Of(PatternKind.Cross, 2f, 0.3f, SKColors.Blue);
            PatternFill b = PatternFill.Of(PatternKind.Cross, 2f, 0.3f, SKColors.Blue);
            Assert.True(a.Equals(b));
            Assert.True(a.Equals((object)b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.False(a.Equals(PatternFill.Of(PatternKind.Cross, 2f, 0.3f, SKColors.Red)));
            Assert.Equal(2f, a.SpacingMm);
            Assert.Equal(Unit.Mm(2f), a.SpacingPixels, 4);
            Assert.Equal(Unit.Mm(0.3f), a.StrokePixels, 4);
        }

        [Fact]
        public void 图样_BuildShader走Painter缓存()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    SKShader first = Hatch.BuildShader(painter);
                    SKShader second = Hatch.BuildShader(painter);
                    Assert.True(ReferenceEquals(first, second));
                    Assert.Equal(1, painter.ShaderCount);
                    Assert.NotNull(first);
                }
            }
        }

        [Fact]
        public void 图样_空参数拒绝()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                using (SKPath path = new SKPath())
                {
                    path.AddRect(PatchRect);
                    Assert.Throws<ArgumentNullException>(() => painter.FillPathWithPattern(null!, Hatch));
                    Assert.Throws<ArgumentNullException>(() => painter.FillPathWithPattern(path, null!));
                    Assert.Throws<ArgumentNullException>(() => painter.FillPath(null!, SKColors.Black));
                    Assert.Throws<ArgumentNullException>(() => painter.StrokePath(null!, SKColors.Black, 1f));
                    Assert.Throws<ArgumentNullException>(() => painter.RotateAround(90f, new SKPoint(), null!));
                    Assert.Throws<ArgumentNullException>(() => Hatch.BuildShader(null!));
                }
            }
        }

        // ------------------------------------------------------------ clip 栈

        [Fact]
        public void 裁剪_带内落墨带外干净()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.PushClipRect(new SKRect(20f, 20f, 80f, 60f));
                    painter.FillRect(new SKRect(0f, 0f, 160f, 120f), SKColors.Black);
                    Assert.Equal(1, painter.ClipDepth);
                    painter.Pop();
                    Assert.Equal(0, painter.ClipDepth);
                }

                using (SKImage image = surface.Snapshot())
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (ImageProbe probe = ImageProbe.FromPng(data.ToArray()))
                {
                    Assert.True(probe.IsInk(50, 40), "裁剪区内应有墨");
                    Assert.False(probe.IsInk(10, 40), "裁剪区左外侧不该有墨");
                    Assert.False(probe.IsInk(120, 40), "裁剪区右外侧不该有墨");
                    Assert.False(probe.IsInk(50, 10), "裁剪区上外侧不该有墨");
                    Assert.False(probe.IsInk(50, 90), "裁剪区下外侧不该有墨");
                }
            }
        }

        [Fact]
        public void 裁剪_嵌套两层后逐层弹出()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.PushClipRect(new SKRect(0f, 0f, 100f, 100f));
                    painter.PushClipRect(new SKRect(20f, 20f, 60f, 60f));
                    Assert.Equal(2, painter.ClipDepth);
                    painter.Pop();
                    Assert.Equal(1, painter.ClipDepth);
                    painter.Pop();
                    Assert.Equal(0, painter.ClipDepth);
                }
            }
        }

        [Fact]
        public void 裁剪_未配对时Dispose抛异常()
        {
            // 裁剪忘配对会污染同一画布后续所有绘制，且极难在成品上定位，
            // 所以这里让 Dispose 直接抛，而不是留一颗哑雷。
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts);
                painter.PushClipRect(new SKRect(0f, 0f, 10f, 10f));
                Assert.Equal(1, painter.ClipDepth);
                Assert.Throws<InvalidOperationException>(() => painter.Dispose());
                painter.Pop();
                painter.Dispose();
            }
        }

        [Fact]
        public void 裁剪_空栈Pop抛异常()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    Assert.Throws<InvalidOperationException>(() => painter.Pop());
                    Assert.Equal(0, painter.ClipDepth);
                }
            }
        }

        // ------------------------------------------------------------ 旋转

        [Fact]
        public void 旋转_绕支点把横线转成竖线且状态还原()
        {
            SKPoint pivot = new SKPoint(100f, 60f);
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                SKMatrix before = canvas.TotalMatrix;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    // 支点处一条 60px 的水平线，绕支点转 90° 后应落在支点正下方成竖线。
                    painter.RotateAround(90f, pivot, () => painter.Line(70f, 60f, 130f, 60f, SKColors.Black, 2f));
                    Assert.Equal(before.ScaleX, canvas.TotalMatrix.ScaleX, 5);
                    Assert.Equal(before.TransX, canvas.TotalMatrix.TransX, 5);
                    Assert.Equal(before.TransY, canvas.TotalMatrix.TransY, 5);
                    Assert.Equal(0, painter.ClipDepth);
                }

                using (SKImage image = surface.Snapshot())
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                using (ImageProbe probe = ImageProbe.FromPng(data.ToArray()))
                {
                    IReadOnlyList<double> verticals = probe.VerticalRulesAt(60, 60, 140);
                    Assert.True(verticals.Count == 1, "支点行上应只有一条竖线，实到 " + verticals.Count + "：" + Join(verticals));
                    Assert.True(Math.Abs(verticals[0] - 100f) <= 1.5, "竖线应在支点 x=100，实到 " + verticals[0]);

                    Assert.False(probe.HasInkInBox(60, 55, 95, 65), "横线不该残留在支点左侧");
                    IReadOnlyList<double> columns = probe.HorizontalRulesAt(100, 20, 100);
                    Assert.True(columns.Count == 1, "竖线在 x=100 上应是一段连续墨，实到 " + columns.Count + "：" + Join(columns));
                    Assert.True(Math.Abs(columns[0] - 60f) <= 2.0,
                                "绕支点旋转不改变支点到线段中点的距离：竖线中点应仍在支点行 y=60，实到 " + columns[0]);
                }
            }
        }

        [Fact]
        public void 旋转_闭包之后的文字不受旋转影响()
        {
            // A17：旋转只准作用于几何。这里证明 RotateAround 出来就把画布状态还原干净——
            // 同一段文字在"闭包里画过几何的画布"与"从没转过的画布"上落位必须一致。
            string text = "体温38.5";
            FontKey key = new FontKey(null, 14f);
            SKPoint pivot = new SKPoint(20f, 20f);

            int[] withRotation;
            int[] withoutRotation;

            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.RotateAround(-90f, pivot, () => painter.Line(10f, 80f, 10f, 115f, SKColors.Red, 2f));
                    painter.Text(text, 30f, 60f, key, SKColors.Black);
                }
                using (ImageProbe probe = Snapshot(surface))
                {
                    // 闭包里那条竖线被转到了 y≈30 的横线上：先确认几何真的落墨，
                    // 否则"文字没被转走"可能只是什么都没画。
                    Assert.True(probe.HasRedInBox(60, 22, 130, 40), "旋转后的几何没落到画布上，用例失去意义");
                    withRotation = InkBounds(probe, 0, 42, 120, 70);
                }
            }

            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.Text(text, 30f, 60f, key, SKColors.Black);
                }
                using (ImageProbe probe = Snapshot(surface))
                {
                    withoutRotation = InkBounds(probe, 0, 42, 120, 70);
                }
            }

            Assert.True(withoutRotation[0] >= 0, "对照图里一个字都没落到，取样窗口没对准文字");
            Assert.True(string.Join(",", withRotation) == string.Join(",", withoutRotation),
                        "文字落位被旋转污染：" + string.Join(",", withRotation) + " vs " + string.Join(",", withoutRotation));
        }

        [Fact]
        public void 旋转_闭包抛异常也会还原()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    SKMatrix before = canvas.TotalMatrix;
                    Assert.Throws<ApplicationException>(() => painter.RotateAround(45f, new SKPoint(1f, 1f), () =>
                    {
                        throw new ApplicationException("模板内部炸了");
                    }));
                    Assert.Equal(before.ScaleX, canvas.TotalMatrix.ScaleX, 5);
                    Assert.Equal(before.TransX, canvas.TotalMatrix.TransX, 5);
                    Assert.Equal(before.TransY, canvas.TotalMatrix.TransY, 5);
                }
            }
        }

        // ------------------------------------------------------------ 路径与箭头

        [Fact]
        public void 多边形_内部实心外部干净()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.FillPolygon(new[]
                    {
                        new SKPoint(20f, 20f),
                        new SKPoint(80f, 20f),
                        new SKPoint(20f, 80f),
                    }, SKColors.Black);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    Assert.True(probe.IsInk(30, 30), "直角三角形内部应有墨");
                    Assert.True(probe.IsInk(25, 25));
                    Assert.False(probe.IsInk(70, 70), "斜边外侧不该有墨");
                    Assert.False(probe.IsInk(5, 5), "多边形外左上不该有墨");
                }
            }
        }

        [Fact]
        public void 多边形_空点集不落墨()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.FillPolygon(new SKPoint[0], SKColors.Black);
                    painter.FillPolygon(new[] { new SKPoint(40f, 40f) }, SKColors.Black);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    Assert.False(probe.HasInkInBox(0, 0, probe.Width, probe.Height),
                                 "零/单点不该形成填充（也不该抛，缺测数据是常态）");
                }
            }
        }

        [Fact]
        public void 路径描边_只落轮廓不填内部()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                using (SKPath path = new SKPath())
                {
                    path.AddRect(new SKRect(20f, 20f, 90f, 60f));
                    painter.StrokePath(path, SKColors.Black, 2f);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    Assert.True(probe.IsInk(20, 40), "左边框应有墨");
                    Assert.True(probe.IsInk(55, 20), "上边框应有墨");
                    Assert.False(probe.IsInk(55, 40), "矩形内部不该被描边填满");
                    Assert.False(probe.IsInk(120, 40), "框外不该有墨");
                }
            }
        }

        [Fact]
        public void 路径填充_实心覆盖内部()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                using (SKPath path = new SKPath())
                {
                    path.AddRect(new SKRect(20f, 20f, 90f, 60f));
                    painter.FillPath(path, SKColors.Black);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    Assert.True(probe.IsInk(55, 40), "填充应覆盖内部");
                    Assert.False(probe.IsInk(120, 40), "框外不该有墨");
                }
            }
        }

        [Fact]
        public void 箭头_杆身加实心三角箭尖落在终点()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.ArrowSegment(new SKPoint(20f, 40f), new SKPoint(120f, 40f), 10f, SKColors.Black, 1f, 0.5f);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    // 杆身：箭头底边在 b 前 head=10 处（x=110），所以 x=109 上只剩一条细线。
                    IReadOnlyList<double> shaft = probe.HorizontalRulesAt(30, 30, 50);
                    Assert.True(shaft.Count == 1, "杆身应是一段连续横墨，实到 " + shaft.Count + "：" + Join(shaft));
                    Assert.True(Math.Abs(shaft[0] - 40d) <= 1d, "杆身应落在 y=40，实到 " + shaft[0]);
                    Assert.True(RunLength(probe, 109, 30, 50) <= 3,
                                "箭头底边前不该有展开的三角：" + RunLength(probe, 109, 30, 50));

                    // 箭尖：在 b 处仍有墨，且往后不再有。
                    Assert.True(probe.IsInk(119, 40), "箭尖应落在终点");
                    Assert.False(probe.IsInk(122, 40), "箭尖之外不该有墨");

                    // 实心三角由粗收细：半宽 = head × baseRatio = 5，故 x 处的竖向墨长 ≈ 2×5×(120−x)/10。
                    int atBase = RunLength(probe, 111, 30, 50);
                    int midHead = RunLength(probe, 115, 30, 50);
                    int nearTip = RunLength(probe, 118, 30, 50);
                    Assert.True(atBase > midHead && midHead >= nearTip,
                                "三角箭头应由粗收细，实到 " + atBase + " / " + midHead + " / " + nearTip);
                    Assert.True(Math.Abs(atBase - 9) <= 1.5, "底边处竖向墨长应约 9px（2×5×9/10），实到 " + atBase);
                    Assert.True(Math.Abs(nearTip - 2) <= 1.5, "近尖处竖向墨长应约 2px（2×5×2/10），实到 " + nearTip);
                }
            }
        }

        [Fact]
        public void 箭头_零长杆不落墨()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    SKPoint p = new SKPoint(60f, 60f);
                    painter.ArrowSegment(p, p, 8f, SKColors.Black, 1f);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    Assert.False(probe.HasInkInBox(0, 0, probe.Width, probe.Height),
                                 "零长杆方向未定，落任何墨都是猜方向");
                }
            }
        }

        [Fact]
        public void 箭头_线粗为零只画箭头()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    painter.ArrowSegment(new SKPoint(20f, 40f), new SKPoint(120f, 40f), 10f, SKColors.Black, 0f);
                }

                using (ImageProbe probe = Snapshot(surface))
                {
                    Assert.False(probe.HasInkInBox(20, 38, 105, 42), "杆身不该落墨");
                    Assert.True(probe.HasInkInBox(108, 36, 121, 44), "箭头该在终点附近");
                }
            }
        }

        [Fact]
        public void 箭头_箭长非法时拒绝()
        {
            using (SKSurface? surface = MakeSurface())
            {
                SKCanvas canvas = surface!.Canvas;
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        painter.ArrowSegment(new SKPoint(0f, 0f), new SKPoint(10f, 0f), -1f, SKColors.Black, 1f));
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        painter.ArrowSegment(new SKPoint(0f, 0f), new SKPoint(10f, 0f), float.NaN, SKColors.Black, 1f));
                }
            }
        }

        // ------------------------------------------------------------ 取像素的公共件

        /// <summary>
        /// 宿主直绘：画布已带设备缩放（等价于 WPF/MAUI 在 144dpi 显示器上的那条路）。
        /// </summary>
        private static ImageProbe RenderDirect(float dpi, PatternFill pattern)
        {
            float scale = dpi / 96f;
            SKImageInfo info = new SKImageInfo(
                (int)Math.Round(PatchWidth * scale), (int)Math.Round(PatchHeight * scale),
                SKColorType.Bgra8888, SKAlphaType.Premul);

            using (SKSurface? surface = SKSurface.Create(info))
            {
                Assert.NotNull(surface);
                SKCanvas canvas = surface!.Canvas;
                canvas.Clear(SKColors.White);
                canvas.Scale(scale);
                using (Painter painter = new Painter(canvas, new ChartStyle(), Chart.SharedFonts))
                {
                    DrawPatch(painter, pattern);
                }

                using (SKImage image = surface.Snapshot())
                using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
                {
                    return ImageProbe.FromPng(data.ToArray());
                }
            }
        }

        /// <summary>Chart 光栅管线：录 SKPicture，再按 dpi 缩放落到位图。</summary>
        private static ImageProbe RenderChart(float dpi, PatternFill pattern)
        {
            using (Chart chart = new Chart(new ChartDocument(), new PatternPatchTemplate(pattern), new ChartStyle()))
            {
                return ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, dpi, 100));
            }
        }

        private static void DrawPatch(Painter painter, PatternFill pattern)
        {
            using (SKPath path = new SKPath())
            {
                path.AddRect(PatchRect);
                painter.FillPathWithPattern(path, pattern);
            }
        }

        private static SKSurface? MakeSurface()
        {
            return SKSurface.Create(new SKImageInfo(160, 120, SKColorType.Bgra8888, SKAlphaType.Premul));
        }

        private static ImageProbe Snapshot(SKSurface surface)
        {
            using (SKImage image = surface.Snapshot())
            using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
            {
                return ImageProbe.FromPng(data.ToArray());
            }
        }

        /// <summary>某一列上纹线段的中心，换算回 96dpi 逻辑像素。窗口用逻辑 y 给，避开被带边切断的纹线。</summary>
        private static IReadOnlyList<double> LogicalBandCenters(ImageProbe probe, float dpi,
                                                                double fromLogicalY = 0d, double toLogicalY = double.MaxValue)
        {
            float scale = dpi / 96f;
            int x = (int)Math.Round(10f * scale);
            double to = toLogicalY == double.MaxValue ? probe.Height : toLogicalY * scale;
            List<double> logical = new List<double>();
            foreach (double deviceY in probe.HorizontalRulesAt(x, fromLogicalY * scale, to))
            {
                logical.Add(deviceY / scale);
            }
            return logical;
        }

        /// <summary>
        /// 平均逻辑周期：取 (末条 − 首条) / 间隔数。首尾各留 10 逻辑像素的余量，
        /// 免得被带边切断的那半条纹线把端点拉偏。
        /// </summary>
        private static double AverageLogicalPeriod(ImageProbe probe, float dpi)
        {
            IReadOnlyList<double> centers = LogicalBandCenters(probe, dpi, 12d, PatchRect.Bottom - 10d);
            Assert.True(centers.Count >= 8, "读到的纹线太少，平均周期不可信：" + centers.Count);
            return (centers[centers.Count - 1] - centers[0]) / (centers.Count - 1);
        }

        /// <summary>某一列上纹线段的最长连续墨迹长度（设备像素），换算回逻辑像素。</summary>
        private static double MedianLogicalRunWidth(ImageProbe probe, float dpi)
        {
            float scale = dpi / 96f;
            int x = (int)Math.Round(10f * scale);
            List<double> widths = new List<double>();
            int run = 0;
            for (int y = 0; y < probe.Height; y++)
            {
                if (probe.IsInk(x, y))
                {
                    run++;
                }
                else if (run > 0)
                {
                    widths.Add(run / (double)scale);
                    run = 0;
                }
            }
            if (run > 0)
            {
                widths.Add(run / (double)scale);
            }

            Assert.True(widths.Count > 0, "没读到纹线段");
            widths.Sort();
            return widths[widths.Count / 2];
        }

        /// <summary>
        /// 区域里"半覆盖像素"的个数：三个通道都在 26..229 之间。
        /// 图样若靠放大位图凑分辨率，纹线边缘必然出现这种灰行。
        /// </summary>
        private static int MidGrayPixels(ImageProbe probe, float dpi)
        {
            float scale = dpi / 96f;
            int left = (int)Math.Round(2f * scale);
            int right = (int)Math.Round(198f * scale);
            int top = (int)Math.Round(3f * scale);
            int bottom = (int)Math.Round(157f * scale);
            int gray = 0;
            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    SKColor c = probe.Pixel(x, y);
                    if (IsMidGray(c.Red) && IsMidGray(c.Green) && IsMidGray(c.Blue))
                    {
                        gray++;
                    }
                }
            }
            return gray;
        }

        private static bool IsMidGray(byte channel)
        {
            return channel > 25 && channel < 230;
        }

        /// <summary>窗口内墨迹的外接框，返回 {minX, maxX, minY, maxY}；无墨时四项都是 -1。</summary>
        private static int[] InkBounds(ImageProbe probe, int left, int top, int right, int bottom)
        {
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (int y = Math.Max(0, top); y < Math.Min(probe.Height, bottom); y++)
            {
                for (int x = Math.Max(0, left); x < Math.Min(probe.Width, right); x++)
                {
                    if (!probe.IsInk(x, y))
                    {
                        continue;
                    }
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }

            return minX > maxX ? new[] { -1, -1, -1, -1 } : new[] { minX, maxX, minY, maxY };
        }

        /// <summary>某列上一段连续墨迹的长度（像素）。</summary>
        private static int RunLength(ImageProbe probe, int x, int fromY, int toY)
        {
            int max = 0;
            int run = 0;
            for (int y = fromY; y < toY; y++)
            {
                if (probe.IsInk(x, y))
                {
                    run++;
                    max = Math.Max(max, run);
                }
                else
                {
                    run = 0;
                }
            }
            return max;
        }

        private static void AssertPositions(IReadOnlyList<double> actual, IReadOnlyList<double> expected, string label)
        {
            Assert.True(actual.Count == expected.Count,
                        label + "：纹线数 " + actual.Count + "，期望 " + expected.Count + "：" + Join(actual));
            for (int i = 0; i < expected.Count; i++)
            {
                double delta = Math.Abs(actual[i] - expected[i]);
                Assert.True(delta <= 0.6, label + "：第 " + i + " 条形中心 " + actual[i]
                                        + "，期望 " + expected[i] + "（偏差 " + delta.ToString("0.###") + " 逻辑像素）");
            }
        }

        private static string Join(IReadOnlyList<double> values)
        {
            return string.Join(", ", System.Linq.Enumerable.Select(values, v => v.ToString("0.##")));
        }

        // ------------------------------------------------------------ 走管线用的小模板

        /// <summary>
        /// 最小模板：只为把 <see cref="Painter"/> 的图样填充接进 <c>Chart.Record → RenderImage</c>
        /// 这条真实链路，不做任何版面含义。
        /// </summary>
        private sealed class PatternPatchTemplate : IChartTemplate
        {
            private readonly PatternFill _pattern;

            public PatternPatchTemplate(PatternFill pattern)
            {
                _pattern = pattern;
            }

            public string Key => "test.pattern-patch";

            public string DisplayName => "图样填充试版";

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                return new ChartGeometry(
                    new SKRect(0f, 0f, PatchWidth, PatchHeight),
                    new SlotAxis(0f, 10f, 1, 1),
                    new LinearAxis("rule", 0d, 10d, 0f, PatchHeight, 1d, 5d),
                    new LinearAxis[0],
                    new Band("header", new SKRect(0f, 0f, PatchWidth, 2f), new BandRow[0]),
                    new Band("date", new SKRect(0f, 2f, PatchWidth, 4f), new BandRow[0]),
                    PatchRect,
                    new LabelColumn[0],
                    new Band("cells", PatchRect, new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", new SKRect(0f, PatchHeight - 2f, PatchWidth, PatchHeight), new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                DrawPatch(painter, _pattern);
            }
        }
    }
}
