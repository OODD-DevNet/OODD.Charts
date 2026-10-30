using System;
using OODD.Charts.Geometry;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 域与带验收（docs/07 T213）：多边形命中对顶点顺序与重复首尾点的免疫，
    /// 以及时段带的包含与重叠。
    /// <para>
    /// 边界语义按 docs/06 §3.2 是<b>先定后测</b>的："顶点和边上的点算内"。
    /// 这不是实现顺手的结果，而是合规判定的取向——恰好压在包线边界上的配载必须判为合格，
    /// 所以这里每个边界用例都是这条约定的钉子，改动判据会立刻红。
    /// </para>
    /// </summary>
    public sealed class DomainBandTests
    {
        // ------------------------------------------------------ T213 EnvelopePolygon

        private static EnvelopePolygon Rectangle(string key = "rect", bool closed = true)
        {
            return new EnvelopePolygon(key, "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(100f, 0f),
                new SKPoint(100f, 50f),
                new SKPoint(0f, 50f),
            }, closed);
        }

        [Fact]
        public void 包线_顶点与边上的点算内()
        {
            EnvelopePolygon rect = Rectangle();

            // 四个顶点
            Assert.True(rect.Contains(0d, 0d));
            Assert.True(rect.Contains(100d, 0d));
            Assert.True(rect.Contains(100d, 50d));
            Assert.True(rect.Contains(0d, 50d));

            // 四条边的中点
            Assert.True(rect.Contains(50d, 0d));
            Assert.True(rect.Contains(100d, 25d));
            Assert.True(rect.Contains(50d, 50d));
            Assert.True(rect.Contains(0d, 25d));

            // 内部
            Assert.True(rect.Contains(50d, 25d));

            // 外部（四方向各一个）
            Assert.False(rect.Contains(150d, 25d));
            Assert.False(rect.Contains(-1d, 25d));
            Assert.False(rect.Contains(50d, -1d));
            Assert.False(rect.Contains(50d, 51d));
        }

        [Fact]
        public void 包线_斜边上的点算内()
        {
            EnvelopePolygon triangle = new EnvelopePolygon("tri", "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(10f, 0f),
                new SKPoint(0f, 10f),
            });

            Assert.True(triangle.Contains(0d, 0d));
            Assert.True(triangle.Contains(5d, 5d), "斜边 (10,0)-(0,10) 的中点必须算内");
            Assert.True(triangle.Contains(1d, 1d));
            Assert.False(triangle.Contains(6d, 6d));
            Assert.False(triangle.Contains(11d, 0d));
        }

        [Fact]
        public void 包线_顶点顺序反转后判定不变()
        {
            EnvelopePolygon clockwise = new EnvelopePolygon("cw", "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(100f, 0f),
                new SKPoint(100f, 50f),
                new SKPoint(0f, 50f),
            });
            EnvelopePolygon counter = new EnvelopePolygon("ccw", "", new[]
            {
                new SKPoint(0f, 50f),
                new SKPoint(100f, 50f),
                new SKPoint(100f, 0f),
                new SKPoint(0f, 0f),
            });

            double[] xs = { -10d, 0d, 50d, 100d, 110d };
            double[] ys = { -10d, 0d, 25d, 50d, 60d };
            foreach (double x in xs)
            {
                foreach (double y in ys)
                {
                    Assert.Equal(clockwise.Contains(x, y), counter.Contains(x, y));
                }
            }

            // 起点旋转（同一环从不同顶点起写）也必须同判
            EnvelopePolygon rotated = new EnvelopePolygon("rot", "", new[]
            {
                new SKPoint(100f, 50f),
                new SKPoint(0f, 50f),
                new SKPoint(0f, 0f),
                new SKPoint(100f, 0f),
            });
            Assert.True(rotated.Contains(50d, 25d));
            Assert.True(rotated.Contains(100d, 25d));
            Assert.False(rotated.Contains(50d, 60d));
        }

        [Fact]
        public void 包线_重复首尾点不退化()
        {
            // 画闭合环时常见的写法：首点在尾部再写一遍
            EnvelopePolygon repeated = new EnvelopePolygon("rect", "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(100f, 0f),
                new SKPoint(100f, 50f),
                new SKPoint(0f, 50f),
                new SKPoint(0f, 0f),
            });

            Assert.Equal(4, repeated.VertexCount);
            Assert.True(repeated.Contains(50d, 25d));
            Assert.True(repeated.Contains(0d, 0d), "首点重复后顶点判定仍是内");
            Assert.True(repeated.Contains(100d, 25d));
            Assert.False(repeated.Contains(150d, 25d));

            // 与不重复的写法逐点同判
            EnvelopePolygon plain = Rectangle();
            double[] xs = { 0d, 50d, 100d };
            double[] ys = { 0d, 25d, 50d };
            foreach (double x in xs)
            {
                foreach (double y in ys)
                {
                    Assert.Equal(plain.Contains(x, y), repeated.Contains(x, y));
                }
            }
        }

        [Fact]
        public void 包线_凹形用射线法判定()
        {
            // L 形：缺掉右上象限
            EnvelopePolygon lShape = new EnvelopePolygon("l", "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(40f, 0f),
                new SKPoint(40f, 20f),
                new SKPoint(20f, 20f),
                new SKPoint(20f, 40f),
                new SKPoint(0f, 40f),
            });

            Assert.True(lShape.Contains(10d, 10d));
            Assert.True(lShape.Contains(35d, 10d));
            Assert.True(lShape.Contains(10d, 35d));
            Assert.False(lShape.Contains(35d, 35d), "凹口不算内");
            Assert.True(lShape.Contains(20d, 30d), "凹口的竖直边上的点算内");

            // 外接矩形覆盖凹口，说明判定不是只看包围盒
            Assert.Equal(new SKRect(0f, 0f, 40f, 40f), lShape.Bounds);
        }

        [Fact]
        public void 包线_未闭合的限值折线没有内部()
        {
            EnvelopePolygon polyline = Rectangle("limit", false);

            Assert.False(polyline.Closed);
            Assert.False(polyline.Contains(50d, 25d));
            Assert.False(polyline.Contains(0d, 0d));

            // 但折线依然可用作描边：顶点与范围都在
            Assert.Equal(4, polyline.VertexCount);
            Assert.Equal(new SKRect(0f, 0f, 100f, 50f), polyline.Bounds);
        }

        [Fact]
        public void 包线_非法构造被拒绝()
        {
            // 闭合域少于 3 个不重复顶点没有内部
            Assert.Throws<ArgumentException>(() => new EnvelopePolygon("x", "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(10f, 10f),
                new SKPoint(0f, 0f),
            }));
            Assert.Throws<ArgumentNullException>(() => new EnvelopePolygon("x", "", null!));
            Assert.Throws<ArgumentException>(() => new EnvelopePolygon("", "", new[]
            {
                new SKPoint(0f, 0f),
                new SKPoint(1f, 0f),
                new SKPoint(0f, 1f),
            }));
        }

        [Fact]
        public void 包线_坐标传点与传值同判()
        {
            EnvelopePolygon rect = Rectangle();

            Assert.True(rect.Contains(new SKPoint(50f, 25f)));
            Assert.False(rect.Contains(new SKPoint(50f, 200f)));
        }

        // ---------------------------------------------------------- T213 SpanBand

        [Fact]
        public void 时段带_跨度与端点包含()
        {
            SpanBand window = new SpanBand("sky", 100d, 160d, null, null, BandFill.Hatch);

            Assert.Equal(60d, window.Span, 9);
            Assert.Equal(BandFill.Hatch, window.Fill);

            // 天窗起止时刻本身算封轨时间
            Assert.True(window.CoversDomain(100d));
            Assert.True(window.CoversDomain(160d));
            Assert.True(window.CoversDomain(130d));
            Assert.False(window.CoversDomain(99d));
            Assert.False(window.CoversDomain(161d));
        }

        [Fact]
        public void 时段带_分类区间与域区间各自独立()
        {
            SpanBand mixed = new SpanBand("mixed", 90d, 150d, 2, 4);

            Assert.True(mixed.CoversCategory(2));
            Assert.True(mixed.CoversCategory(4));
            Assert.True(mixed.CoversCategory(3));
            Assert.False(mixed.CoversCategory(1));
            Assert.False(mixed.CoversCategory(5));

            // 纯域带没有分类语义
            SpanBand pure = new SpanBand("pure", 0d, 10d);
            Assert.False(pure.CoversCategory(0));
            Assert.Equal(BandFill.Solid, pure.Fill);
            Assert.Null(pure.FromCategory);
            Assert.Null(pure.ToCategory);
        }

        [Fact]
        public void 时段带_重叠判定对称且端点相接算重叠()
        {
            SpanBand a = new SpanBand("a", 100d, 160d);
            SpanBand touching = new SpanBand("b", 160d, 200d);
            SpanBand inside = new SpanBand("c", 120d, 140d);
            SpanBand covering = new SpanBand("d", 50d, 300d);
            SpanBand disjoint = new SpanBand("e", 161d, 200d);
            SpanBand before = new SpanBand("f", 50d, 100d);
            SpanBand far = new SpanBand("g", 40d, 90d);

            Assert.True(a.Overlaps(touching), "端点相接算重叠");
            Assert.True(touching.Overlaps(a));
            Assert.True(a.Overlaps(inside));
            Assert.True(inside.Overlaps(a));
            Assert.True(a.Overlaps(covering));
            Assert.True(covering.Overlaps(a));
            Assert.True(a.Overlaps(before), "端点相接算重叠");
            Assert.False(a.Overlaps(disjoint));
            Assert.False(disjoint.Overlaps(a));
            Assert.False(a.Overlaps(far));
            Assert.False(far.Overlaps(a));
        }

        [Fact]
        public void 时段带_非法构造被拒绝()
        {
            Assert.Throws<ArgumentException>(() => new SpanBand("", 0d, 10d));
            Assert.Throws<ArgumentException>(() => new SpanBand("x", 10d, 10d));
            Assert.Throws<ArgumentException>(() => new SpanBand("x", 10d, 5d));
            Assert.Throws<ArgumentException>(() => new SpanBand("x", double.NaN, 5d));
            Assert.Throws<ArgumentException>(() => new SpanBand("x", 0d, double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => new SpanBand("x", 0d, 10d, 4, 2));
            Assert.Throws<ArgumentNullException>(() => new SpanBand("x", 0d, 10d).Overlaps(null!));
        }

        // ---------------------------------------------------------- T213 毫米令牌与时段带定位

        [Fact]
        public void 域带_毫米令牌与像素换算在带定位上一致()
        {
            // 250mm 图头这类要求：带用 mm 声明、落笔用 px 算，两者往返必须一致。
            float pixels = Unit.Mm(250f);
            Assert.True(Math.Abs(pixels - 250f * 96f / 25.4f) < 1e-3f);
            Assert.True(Math.Abs(Unit.MmFromPx(pixels) - 250f) < 1e-4f);

            SpanBand band = new SpanBand("head", 0d, Unit.MmFromPx(pixels));
            Assert.True(band.CoversDomain(250d - 0.001d));
            Assert.False(band.CoversDomain(250d + 0.001d));
        }
    }
}
