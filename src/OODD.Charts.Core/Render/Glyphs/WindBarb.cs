using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 天气图风向杆：从站圈外缘引出的杆 + 尾端的风速羽旗。
    /// 风向 0°＝正北＝屏幕向上，顺时针增大（与雷达 PPI 的角度口径必须一致）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 角度口径（钉死，勿改）：屏幕向上是 0°，顺时针增大，所以方向向量取
    /// <c>dx = sin(rad)</c>、<c>dy = -cos(rad)</c>；90°（东风）指向屏幕右方，180° 指向下方。
    /// 与 <c>RadarChartTemplate</c> 的方位角算法同一套口径，两域混排时不会出现镜像风杆。
    /// </para>
    /// <para>
    /// 锚点语义：anchor 是<strong>站圈圆心</strong>（本符号不画站圈，站圈由模板落，
    /// 因为圆心同时承载气温/露点等要素）。杆从站圈外缘（半径 <see cref="StationRadiusFactor"/>×size）
    /// 引出，长度 <see cref="ShaftLengthFactor"/>×size。
    /// </para>
    /// <para>
    /// 羽旗全部是几何线段与三角形，靠 <see cref="Painter.RotateAround"/> 整体旋转；
    /// 符号内不落任何文字，站旁的气温/风级数字由模板用直立文本单独落位，
    /// 避让可用 <see cref="TipOf"/> 拿到的杆末端点。
    /// </para>
    /// </remarks>
    public sealed class WindBarb : IGlyph
    {
        /// <summary>构造风向杆。</summary>
        /// <param name="windDirectionDegrees">风向（度）。0°＝正北＝屏幕向上，顺时针增大，超出 [0,360) 会归一。</param>
        /// <param name="knots">风速（节）。编码规则由 <see cref="BarbRule"/> 决定。</param>
        /// <param name="rule">羽旗编码规则。</param>
        public WindBarb(float windDirectionDegrees, int knots, BarbRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            DirectionDegrees = NormalizeDegrees(windDirectionDegrees);
            Knots = knots < 0 ? 0 : knots;
            Rule = rule;
        }

        /// <summary>风向（度，已归一到 [0,360)）。</summary>
        public float DirectionDegrees { get; }

        /// <summary>风速（节，负值按 0 处理）。</summary>
        public int Knots { get; }

        /// <summary>羽旗编码规则。</summary>
        public BarbRule Rule { get; }

        /// <summary>是否静风（风速 ≤ 0）。静风时杆与羽旗都不画，站圈外的静风圈由模板自行落位。</summary>
        public bool IsCalm
        {
            get { return Knots <= 0; }
        }

        /// <summary>站圈半径相对 size 的因子：杆的起点就在这条半径的外缘上，故必须与模板画的站圈一致。</summary>
        public float StationRadiusFactor { get; set; } = 0.28f;

        /// <summary>杆身长度相对 size 的因子（自站圈外缘起算）。</summary>
        public float ShaftLengthFactor { get; set; } = 1.0f;

        /// <summary>全旗（长线）长度相对 size 的因子。</summary>
        public float FullBarbLengthFactor { get; set; } = 0.34f;

        /// <summary>半旗长度相对全旗长度的因子，默认 0.5（半旗是全旗的一半）。</summary>
        public float HalfBarbLengthFactor { get; set; } = 0.5f;

        /// <summary>相邻羽旗沿杆方向的间距相对 size 的因子，也决定三角旗的杆向边长。</summary>
        public float BarbSpacingFactor { get; set; } = 0.16f;

        /// <summary>杆与羽旗的描边宽度相对 size 的因子。</summary>
        public float StrokeWidthFactor { get; set; } = 0.07f;

        /// <summary>羽旗在杆的哪一侧：true＝背向站圈看时的左手侧（北半球惯例），false＝右手侧（南半球惯例）。</summary>
        public bool BarbsOnLeft { get; set; } = true;

        /// <summary>方向角转单位方向向量：0°→(0,-1) 屏幕向上，90°→(1,0) 屏幕向右。</summary>
        /// <param name="degrees">角度（度，顺时针）。</param>
        public static SKPoint DirectionVectorOf(float degrees)
        {
            double radians = (double)NormalizeDegrees(degrees) * Math.PI / 180.0;
            return new SKPoint((float)Math.Sin(radians), -(float)Math.Cos(radians));
        }

        /// <summary>归一到 [0,360)。</summary>
        /// <param name="degrees">任意角度。</param>
        public static float NormalizeDegrees(float degrees)
        {
            float value = degrees % 360f;
            return value < 0f ? value + 360f : value;
        }

        /// <summary>杆根（站圈外缘上的起点）。</summary>
        /// <param name="stationCenter">站圈圆心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKPoint StartOf(SKPoint stationCenter, float size)
        {
            SKPoint unit = DirectionVectorOf(DirectionDegrees);
            float reach = size * StationRadiusFactor;
            return new SKPoint(stationCenter.X + unit.X * reach, stationCenter.Y + unit.Y * reach);
        }

        /// <summary>风杆末端点（站圈以外），供模板避让文字。</summary>
        /// <param name="stationCenter">站圈圆心。</param>
        /// <param name="size">标称尺寸。</param>
        public SKPoint TipOf(SKPoint stationCenter, float size)
        {
            SKPoint unit = DirectionVectorOf(DirectionDegrees);
            float reach = size * (StationRadiusFactor + ShaftLengthFactor);
            return new SKPoint(stationCenter.X + unit.X * reach, stationCenter.Y + unit.Y * reach);
        }

        /// <summary>当前风速按规则分解出的羽旗数量。</summary>
        public BarbCounts Counts
        {
            get { return Rule.Encode(Knots); }
        }

        /// <summary>画杆与羽旗。整组几何用 <see cref="Painter.RotateAround"/> 旋转，符号内不写文字。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="stationCenter">站圈圆心（旋转支点）。</param>
        /// <param name="size">标称尺寸。</param>
        /// <param name="color">主色。</param>
        public void Draw(Painter painter, SKPoint stationCenter, float size, SKColor color)
        {
            if (painter == null)
            {
                throw new ArgumentNullException(nameof(painter));
            }
            if (size <= 0f || IsCalm)
            {
                return;
            }

            BarbCounts counts = Rule.Encode(Knots);
            float stroke = size * StrokeWidthFactor;

            // 先按 0°（杆竖直向上）画局部几何，再由 RotateAround 转到真实风向：
            // 旋转只作用于几何，站圈与各要素数字仍在旋转之外由模板直立落位。
            painter.RotateAround(DirectionDegrees, stationCenter,
                                 () => DrawLocal(painter, stationCenter, size, color, stroke, counts));
        }

        private void DrawLocal(Painter painter, SKPoint stationCenter, float size, SKColor color,
                               float stroke, BarbCounts counts)
        {
            float baseOffset = size * StationRadiusFactor;
            float topY = stationCenter.Y - baseOffset - size * ShaftLengthFactor;
            float x = stationCenter.X;

            painter.Line(x, stationCenter.Y - baseOffset, x, topY, color, stroke);

            float side = BarbsOnLeft ? -1f : 1f;
            float full = size * FullBarbLengthFactor;
            float half = full * HalfBarbLengthFactor;
            float step = size * BarbSpacingFactor;

            // 从杆末端向站圈方向依次排：半旗 → 全旗 → 三角旗（大值靠内）。
            // 这是可调约定，不是国际标准条文；换口径改 BarbSpacingFactor 与本循环顺序即可。
            float y = topY;
            for (int i = 0; i < counts.Halves; i++)
            {
                painter.Line(x, y, x + side * half, y, color, stroke);
                y += step;
            }
            for (int i = 0; i < counts.Fulls; i++)
            {
                painter.Line(x, y, x + side * full, y, color, stroke);
                y += step;
            }
            for (int i = 0; i < counts.Pennants; i++)
            {
                List<SKPoint> pennant = new List<SKPoint>
                {
                    new SKPoint(x, y),
                    new SKPoint(x, y + step),
                    new SKPoint(x + side * full, y + step),
                };
                painter.FillPolygon(pennant, color);
                y += step;
            }
        }

        /// <summary>
        /// 羽旗数量三元组。用结构体而不是值元组：最低目标框架 net462 的框架程序集里没有
        /// System.ValueTuple 类型（本仓库也未引用该包，公开签名用值元组要在 net462 上额外加依赖），
        /// 故 Encode 返回本结构体。提供 <see cref="Deconstruct"/> 让调用方仍能用解构语法。
        /// </summary>
        public readonly struct BarbCounts : IEquatable<BarbCounts>
        {
            /// <summary>三元组。</summary>
            /// <param name="pennants">三角旗数。</param>
            /// <param name="fulls">全旗数。</param>
            /// <param name="halves">半旗数。</param>
            public BarbCounts(int pennants, int fulls, int halves)
            {
                Pennants = pennants < 0 ? 0 : pennants;
                Fulls = fulls < 0 ? 0 : fulls;
                Halves = halves < 0 ? 0 : halves;
            }

            /// <summary>三角旗数。</summary>
            public int Pennants { get; }

            /// <summary>全旗数。</summary>
            public int Fulls { get; }

            /// <summary>半旗数。</summary>
            public int Halves { get; }

            /// <summary>解构，便于 <c>var (pennants, fulls, halves) = rule.Encode(knots);</c>。</summary>
            /// <param name="pennants">三角旗数。</param>
            /// <param name="fulls">全旗数。</param>
            /// <param name="halves">半旗数。</param>
            public void Deconstruct(out int pennants, out int fulls, out int halves)
            {
                pennants = Pennants;
                fulls = Fulls;
                halves = Halves;
            }

            /// <summary>三者全为 0（静风）。</summary>
            public bool IsEmpty
            {
                get { return Pennants == 0 && Fulls == 0 && Halves == 0; }
            }

            /// <summary>逐项相等。</summary>
            /// <param name="other">另一个三元组。</param>
            public bool Equals(BarbCounts other)
            {
                return Pennants == other.Pennants && Fulls == other.Fulls && Halves == other.Halves;
            }

            /// <summary>逐项相等。</summary>
            /// <param name="obj">另一个三元组。</param>
            public override bool Equals(object? obj)
            {
                return obj is BarbCounts && Equals((BarbCounts)obj);
            }

            /// <summary>与 <see cref="Equals(BarbCounts)"/> 一致的散列。</summary>
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = Pennants.GetHashCode();
                    h = (h * 397) ^ Fulls.GetHashCode();
                    h = (h * 397) ^ Halves.GetHashCode();
                    return h;
                }
            }

            /// <summary>"P/F/H"形式的可读文本。</summary>
            public override string ToString()
            {
                return Pennants + "/" + Fulls + "/" + Halves;
            }
        }

        /// <summary>
        /// 羽旗编码规则。不同来源的编码不同（50/10/5、60/10/5、公里/小时口径等），
        /// 所以做成数据而不是写死。
        /// </summary>
        public sealed class BarbRule
        {
            private static readonly BarbRule _wmoStandard = new BarbRule(10, 5, 50);

            /// <summary>构造规则。</summary>
            /// <param name="fullBarbKnots">长线（全旗）代表多少节，必须为正。</param>
            /// <param name="halfBarbKnots">短线（半旗）代表多少节，必须为正。</param>
            /// <param name="pennantKnots">三角旗代表多少节；非正表示该规则不用三角旗。</param>
            public BarbRule(int fullBarbKnots, int halfBarbKnots, int pennantKnots)
            {
                if (fullBarbKnots <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(fullBarbKnots), "全旗代表的节数必须为正。");
                }
                if (halfBarbKnots <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(halfBarbKnots), "半旗代表的节数必须为正。");
                }

                FullBarbKnots = fullBarbKnots;
                HalfBarbKnots = halfBarbKnots;
                PennantKnots = pennantKnots < 0 ? 0 : pennantKnots;
            }

            /// <summary>长线（全旗）代表多少节。</summary>
            public int FullBarbKnots { get; }

            /// <summary>短线（半旗）代表多少节。</summary>
            public int HalfBarbKnots { get; }

            /// <summary>三角旗代表多少节；0 表示本规则不用三角旗。</summary>
            public int PennantKnots { get; }

            /// <summary>常见约定：三角旗 50 节、全旗 10 节、半旗 5 节。</summary>
            public static BarbRule WmoStandard
            {
                get { return _wmoStandard; }
            }

            /// <summary>
            /// 分解风速：返回 (三角旗数, 全旗数, 半旗数)。静风返回 (0,0,0)。
            /// <para>
            /// 取整规则：自大到小依次整除，余数不足一个半旗时不画——也就是
            /// <strong>向下取整到最小单位（半旗）</strong>，例如 47 节 → 4 全旗 + 1 半旗（表示 45 节），
            /// 44 节 → 4 全旗（表示 40 节）。这是<strong>可配置的展示约定</strong>，
            /// 不是国际标准条文；要按四舍五入或按槽位取整的口径，请在域程序集里换一条 <see cref="BarbRule"/>
            /// 或另写编码函数。
            /// </para>
            /// </summary>
            /// <param name="knots">风速（节）。</param>
            public BarbCounts Encode(int knots)
            {
                if (knots <= 0)
                {
                    return new BarbCounts(0, 0, 0);
                }

                int rest = knots;
                int pennants = 0;
                if (PennantKnots > 0)
                {
                    pennants = rest / PennantKnots;
                    rest -= pennants * PennantKnots;
                }

                int fulls = rest / FullBarbKnots;
                rest -= fulls * FullBarbKnots;

                int halves = rest / HalfBarbKnots;
                // 余下的 rest - halves*HalfBarbKnots 不足一个半旗，按约定丢弃（不画）。
                return new BarbCounts(pennants, fulls, halves);
            }
        }
    }
}
