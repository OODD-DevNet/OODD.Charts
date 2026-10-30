// 本文件大量用例是"故意传 null/NaN 看源码拒不拒"，可空引用注解会在 net8 目标上刷出
// 一批与断言无关的 CS86xx；测试断言的是运行期行为，不是编译期流分析，故整文件关掉可空上下文。
#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// T219：多联次切分（<see cref="CollateSpec"/> / <see cref="CopyDef"/> / <see cref="PerforationStyle"/>）
    /// 与开面分页（<see cref="BookletImposition"/> / <see cref="Spread"/>）的验收测试。
    /// <para>
    /// docs/07 T219 的验收行原文是「联宽之和 == 版面宽；每联只画 visibleFields，extraFields 只在该联出现；
    /// PrintOrderOf 对全部逻辑页构成排列且开面配对可枚举」。四条判据分别落在：
    /// <list type="bullet">
    /// <item><description>联宽配平：求和 == 版面宽、逐联边界连续、整数对齐；不配平时的实际拒绝/不拒绝行为；</description></item>
    /// <item><description>字段可见性：每联的显示集 == visibleFields ∪ extraFields(该联)，双向集合差都证（既不漏也不串）；</description></item>
    /// <item><description>印序排列：长度相等、无重复、无遗漏，与 FlattenedPrintOrder 的非空白槽位逐项比对；</description></item>
    /// <item><description>开面配对可枚举：每个逻辑页恰好属于一个开面，奇偶（正面/背面）与首尾相接配对按源码公式复算。</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 像素用例走真实管线（私有嵌套 <see cref="IChartTemplate"/> + <c>Chart.Encode</c>），按源码语义把骑缝线
    /// 画在 <see cref="CollateSpec.PerforationAt"/> 解算出的位置上，再用 <see cref="ImageProbe"/> 反读：
    /// 缝线中心 == 解算坐标、每联只在属于自己的字段行落墨、别联的字段行必须无墨。
    /// </para>
    /// </summary>
    public sealed class CollateImpositionTests
    {
        // -------------------------------------------------------------- 版面布局常量（绘制端与断言端共用）

        /// <summary>字段行相对联矩形左缘的缩进（像素）：留出骑缝线的位置，缝线与字段互不污染。</summary>
        private const float TextPadX = 8f;

        /// <summary>联名文字的基线相对联矩形顶缘的偏移。</summary>
        private const float LabelBaseline = 20f;

        /// <summary>第一条字段行相对联矩形顶缘的偏移（给联名留一行）。</summary>
        private const float RowsTop = 34f;

        /// <summary>字段行距。</summary>
        private const float RowPitch = 24f;

        /// <summary>字段行（判墨窗口）高。</summary>
        private const float RowHeight = 18f;

        /// <summary>
        /// 字段行判墨窗口宽。测量过程：11 号字（<c>new FontKey(null, 11f)</c>）下 9 个字段键里最长的
        /// "funeral-hotline 42" 实测宽约 96px（Painter.TextWidth 口径），取 120 留足余量，
        /// 同时保证窗口右缘不越过 198px 联宽上的下一条缝。
        /// </summary>
        private const float BoxWidth = 120f;

        /// <summary>裁切线线粗：取 2 且缝位置是整数，描边正好覆盖 x-1..x+1 三列，像素中心解算无半像素歧义。</summary>
        private const float SeamStroke = 2f;

        /// <summary>刀模孔半径（纵叠用例）。</summary>
        private const float HoleRadius = 3f;

        /// <summary>第一个孔相对版面起点的偏移。</summary>
        private const float HoleFirst = 14f;

        /// <summary>孔距。</summary>
        private const float HolePitch = 12f;

        /// <summary>版面末缘前不留孔的距离（保证孔不贴边，判墨窗口干净）。</summary>
        private const float HoleEndMargin = 8f;

        /// <summary>
        /// 9 个字段行（最底一行下缘 34 + 8*24 + 18 = 244）之下、版面底缘 300 之上的空白带：
        /// 竖排用例里这条带只有缝线会落墨，缝线检测因此不受文字干扰。
        /// </summary>
        private const float SeamScanTop = 252f;

        private const float SeamScanBottom = 298f;

        // -------------------------------------------------------------- 字段键（ASCII，保证字体回退一定有字形）

        private const string FieldName = "patient-name";
        private const string FieldSex = "sex";
        private const string FieldAge = "age";
        private const string FieldDeathDate = "death-date";
        private const string FieldCause = "cause";
        private const string FieldPoliceSeal = "police-seal";
        private const string FieldFuneralHotline = "funeral-hotline";
        private const string FieldHearseNo = "hearse-no";
        private const string FieldStatCode = "stat-code";
        private const string FieldDonor = "donor-name";
        private const string FieldReceiver = "receiver-name";
        private const string FieldVolume = "volume-ml";
        private const string FieldBagNo = "bag-no";

        /// <summary>四联共有的字段键（每联的 visibleFields 都是它）。</summary>
        private static List<string> CommonFields()
        {
            return new List<string> { FieldName, FieldSex, FieldAge, FieldDeathDate, FieldCause };
        }

        private static List<string> NoExtra()
        {
            return new List<string>();
        }

        // ================================================================ 一、联宽之和 == 版面宽

        [Fact]
        public void 联宽_整数四联求和等于版面宽且边界逐联连续()
        {
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                198f, 198f, 198f, 198f);
            SKRect total = new SKRect(0f, 0f, 792f, 300f);

            Assert.Equal(4, spec.Count);
            Assert.Equal(792f, spec.TotalExtent);
            Assert.Equal(total.Width, spec.TotalExtent);
            Assert.Equal(0f, spec.ExtentDeviation(total));

            float cursor = total.Left;
            for (int i = 0; i < spec.Count; i++)
            {
                SKRect rect = spec.CopyRect(i, total);

                // 近端 = 前面各联宽之和（源码 CopyRect 的累加口径）。
                Assert.Equal(cursor, rect.Left);
                Assert.Equal(total.Top, rect.Top);
                Assert.Equal(total.Bottom, rect.Bottom);
                Assert.Equal(spec.Copies[i].Width, rect.Width);

                // 整数配平：边界全部落在整数像素上，栅格化不会出现半像素灰线（三套 TFM 才可逐字比对）。
                Assert.True(rect.Left == (float)Math.Truncate(rect.Left),
                    "第 " + i + " 联左缘 " + rect.Left + " 不是整数像素，栅格化会发虚");

                cursor = rect.Right;
                if (i < spec.Count - 1)
                {
                    // 相邻联共享同一条边（无缝隙、无重叠）。
                    Assert.Equal(cursor, spec.CopyRect(i + 1, total).Left);
                    // 缝线坐标 == 该共享边，即 PerforationAt 与 CopyRect 同源。
                    Assert.Equal(cursor, spec.PerforationAt(i + 1, total).Value);
                }
            }

            Assert.Equal(total.Right, cursor);
            Assert.Equal(total.Right, spec.CopyRect(spec.Count - 1, total).Right);
        }

        [Fact]
        public void 联宽_毫米幅面配平时末联远端直接取版面边缘()
        {
            // 210mm 幅面切四联：60/60/50/40mm。毫米换算成 96dpi 像素是不可精确表示的小数，
            // 正是源码注释里「末联吃剩余」要处理的场景。
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                Unit.Mm(60f), Unit.Mm(60f), Unit.Mm(50f), Unit.Mm(40f));
            SKRect total = new SKRect(0f, 0f, Unit.Mm(210f), 300f);

            AssertClose(Unit.Mm(210f), spec.TotalExtent, 0.01f, "四联毫米宽之和");
            Assert.True(spec.ExtentDeviation(total) < 0.01f,
                "配平时偏差量应为 0，实到 " + spec.ExtentDeviation(total));

            SKRect last = spec.CopyRect(3, total);
            Assert.Equal(total.Right, last.Right);                       // 精确相等：不是累加值，是版面边缘
            Assert.True(last.Width >= 0f, "末联矩形被反转了：" + last);
            AssertClose(Unit.Mm(40f), last.Width, 0.01f, "末联实际宽");

            // 非末联仍然是"左缘 + 声明宽"，不吃剩余。
            for (int i = 0; i < 3; i++)
            {
                AssertClose(spec.Copies[i].Width, spec.CopyRect(i, total).Width, 0.001f, "第 " + i + " 联宽");
            }

            // 毫米口径下缝位置同样是"下一联的近端"，与联矩形共享同一条边。
            Assert.Equal(spec.CopyRect(1, total).Left, spec.PerforationAt(1, total).Value);
            Assert.Equal(spec.CopyRect(3, total).Left, spec.PerforationAt(3, total).Value);
        }

        [Fact]
        public void 联宽_不配平时源码不拒绝只由ExtentDeviation与矩形报出()
        {
            // 【现状，非期望】CollateSpec 不校验"联宽之和 == 版面宽"：
            // 构造期没有配平检查，CopyRect 也只用"末联远端取版面边缘"这一条规则消化偏差，
            // 所以欠配让末联变胖、超配让末联被压扁、极端超配给出反向矩形（Width 为负），全程不抛。
            // ExtentDeviation 是调用方唯一的自查出口（docs/06 §3.7 的"联宽配平"因此不是硬约束）。
            SKRect total = new SKRect(0f, 0f, 792f, 300f);

            // 欠配：300+200+100+100 = 700，缺 92px。
            CollateSpec under = DeathCertificate(PerforationStyle.None, CollateDirection.HorizontalStrip,
                                                 300f, 200f, 100f, 100f);
            Assert.Equal(700f, under.TotalExtent);
            Assert.Equal(92f, under.ExtentDeviation(total));
            SKRect underLast = under.CopyRect(3, total);
            Assert.Equal(600f, underLast.Left);
            Assert.Equal(total.Right, underLast.Right);
            Assert.Equal(192f, underLast.Width);
            Assert.True(underLast.Width > under.Copies[3].Width,
                "欠配时末联吃掉剩余（现状：声明 100，实际 " + underLast.Width + "，无异常）");

            // 轻微超配：300+200+250+100 = 850，多 58px，末联被压到 792-750。
            CollateSpec over = DeathCertificate(PerforationStyle.None, CollateDirection.HorizontalStrip,
                                                300f, 200f, 250f, 100f);
            Assert.Equal(850f, over.TotalExtent);
            Assert.Equal(58f, over.ExtentDeviation(total));
            SKRect overLast = over.CopyRect(3, total);
            Assert.Equal(750f, overLast.Left);
            Assert.Equal(42f, overLast.Width);
            Assert.True(overLast.Width < over.Copies[3].Width,
                "超配时末联被压缩（现状：声明 100，实际 " + overLast.Width + "，无异常）");

            // 严重超配：400+400+400+100 = 1300，非末联不被钳住（越过版面右缘），
            // 末联左缘 1200 已在版面右缘 792 之外，于是拿到一个反向退化矩形。
            CollateSpec gross = DeathCertificate(PerforationStyle.None, CollateDirection.HorizontalStrip,
                                                 400f, 400f, 400f, 100f);
            Assert.Equal(1300f, gross.TotalExtent);
            Assert.Equal(508f, gross.ExtentDeviation(total));
            SKRect middle = gross.CopyRect(2, total);
            Assert.Equal(1200f, middle.Right);
            Assert.True(middle.Right > total.Right,
                "【现状】非末联越出版面右缘也不拦（第 2 联右缘 " + middle.Right + " 对版面 " + total.Right + "）");
            SKRect last = gross.CopyRect(3, total);
            Assert.True(last.Left > last.Right && last.Width < 0f,
                "【现状】严重超配时末联矩形反向（Left=" + last.Left + ", Right=" + last.Right +
                ", Width=" + last.Width + "），源码未拦截：见回报「疑似缺陷 D1」");

            // 为负 / 为零：CopyDef 当场拒绝（WidthEpsilon 容差）。
            ArgumentOutOfRangeException zero = Assert.Throws<ArgumentOutOfRangeException>(
                () => new CopyDef("k", "联名", 0f, CommonFields(), NoExtra()));
            Assert.Equal("width", zero.ParamName);
            ArgumentOutOfRangeException negative = Assert.Throws<ArgumentOutOfRangeException>(
                () => new CopyDef("k", "联名", -12f, CommonFields(), NoExtra()));
            Assert.Equal("width", negative.ParamName);
            Assert.True(zero.Message.Contains("必须大于 0"), "拒绝消息要能定位到联：" + zero.Message);
        }

        [Fact]
        public void 联宽_非有限数被当作合法宽放行()
        {
            // 【现状，非期望】守卫条件只问"width 是否不大于 WidthEpsilon"，NaN 与 +Infinity 都判不出
            // "小于等于 0"，于是这两种宽度都能构造成功；求和与解算一路把 NaN 传下去，
            // 直到绘制期落笔才炸。见回报「疑似缺陷 D2」。
            SKRect total = new SKRect(0f, 0f, 792f, 300f);

            // NaN 落在唯一一联上时反而"看不出问题"：它既是首联也是末联，
            // 累加循环不进、远端又直接取版面边缘，于是给出一个完全正常的矩形。
            CollateSpec singleNan = new CollateSpec(
                new List<CopyDef> { new CopyDef("nan", "NaN 宽", float.NaN, CommonFields(), NoExtra()) },
                PerforationStyle.None, CollateDirection.HorizontalStrip);
            Assert.True(float.IsNaN(singleNan.TotalExtent), "现状：NaN 联宽直接进入求和");
            Assert.True(float.IsNaN(singleNan.ExtentDeviation(total)),
                "现状：偏差量随之变成 NaN，配平检查失效（NaN 与任何数比都既不大于也不小于）");
            SKRect only = singleNan.CopyRect(0, total);
            Assert.Equal(total.Left, only.Left);
            Assert.Equal(total.Right, only.Right);
            Assert.False(float.IsNaN(only.Width),
                "现状：NaN 藏在唯一联（同时也是末联）时被「边缘吸附」完全掩盖，解算期看不出任何异常");

            // NaN 出现在中间联才会扩散：自身远端 NaN，其后所有联的近端都 NaN。
            CollateSpec middle = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                   198f, float.NaN, 198f, 198f);
            SKRect head = middle.CopyRect(0, total);
            Assert.Equal(198f, head.Right);                       // 第一联不受影响
            Assert.True(float.IsNaN(middle.CopyRect(1, total).Right),
                "现状：NaN 联的远端边界是 NaN");
            Assert.True(float.IsNaN(middle.CopyRect(2, total).Left),
                "现状：NaN 之后的联近端也是 NaN，联界彻底失效");
            Assert.True(float.IsNaN(middle.CopyRect(3, total).Left) &&
                        middle.CopyRect(3, total).Right == total.Right,
                "现状：末联近端 NaN、远端却被吸附回版面边缘，矩形既不是 NaN 也不合法");
            Assert.Equal(198f, middle.PerforationAt(1, total).Value);
            Assert.True(float.IsNaN(middle.PerforationAt(2, total).Value),
                "现状：缝位置同样是 NaN，落笔坐标无从校验");

            // +Infinity 同样通过守卫，求和与偏差都成 Inf。
            CollateSpec infinity = DeathCertificate(PerforationStyle.None, CollateDirection.HorizontalStrip,
                                                     198f, float.PositiveInfinity, 198f, 198f);
            Assert.True(float.IsPositiveInfinity(infinity.TotalExtent), "现状：+Infinity 通过 width 守卫并进入求和");
            Assert.True(float.IsPositiveInfinity(infinity.ExtentDeviation(total)));
        }

        [Fact]
        public void 联宽_联序号越界与联集合非法输入被拒绝()
        {
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                198f, 198f, 198f, 198f);
            SKRect total = new SKRect(0f, 0f, 792f, 300f);

            ArgumentOutOfRangeException low = Assert.Throws<ArgumentOutOfRangeException>(
                () => spec.CopyRect(-1, total));
            Assert.Equal("index", low.ParamName);
            ArgumentOutOfRangeException high = Assert.Throws<ArgumentOutOfRangeException>(
                () => spec.CopyRect(spec.Count, total));
            Assert.Equal("index", high.ParamName);
            Assert.True(high.Message.Contains("越界"), "越界消息要写明联数：" + high.Message);

            // copies 为空 / 为 null / 含 null 元素 / key 重名 / 超上限。
            Assert.Throws<ArgumentException>(() => new CollateSpec(new CopyDef[0], PerforationStyle.None,
                                                                   CollateDirection.HorizontalStrip));
            Assert.Throws<ArgumentNullException>(() => new CollateSpec(null, PerforationStyle.None,
                                                                       CollateDirection.HorizontalStrip));
            Assert.Throws<ArgumentException>(() => new CollateSpec(
                new CopyDef[] { new CopyDef("a", "甲", 100f, CommonFields(), NoExtra()), null },
                PerforationStyle.None, CollateDirection.HorizontalStrip));
            ArgumentException duplicate = Assert.Throws<ArgumentException>(() => new CollateSpec(
                new CopyDef[] { new CopyDef("a", "甲", 100f, CommonFields(), NoExtra()),
                                new CopyDef("a", "乙", 100f, CommonFields(), NoExtra()) },
                PerforationStyle.None, CollateDirection.HorizontalStrip));
            Assert.True(duplicate.Message.Contains("重复"), "重名消息要能指出是 key 冲突：" + duplicate.Message);

            Assert.Equal(8, CollateSpec.MaxCopies);
            List<CopyDef> nine = new List<CopyDef>();
            for (int i = 0; i < 9; i++)
            {
                nine.Add(new CopyDef("c" + i, "联" + i, 10f, CommonFields(), NoExtra()));
            }
            ArgumentException tooMany = Assert.Throws<ArgumentException>(
                () => new CollateSpec(nine, PerforationStyle.DiceCut, CollateDirection.VerticalStack));
            Assert.True(tooMany.Message.Contains("上限"), "超上限消息要写明 MaxCopies：" + tooMany.Message);

            // 恰好 8 联（上限本身）合法。
            List<CopyDef> eight = new List<CopyDef>();
            for (int i = 0; i < CollateSpec.MaxCopies; i++)
            {
                eight.Add(new CopyDef("c" + i, "联" + i, 1f, CommonFields(), NoExtra()));
            }
            Assert.Equal(8, new CollateSpec(eight, PerforationStyle.None, CollateDirection.HorizontalStrip).Count);
        }

        [Fact]
        public void 缝位置_None形式与越界编号一律无缝()
        {
            SKRect total = new SKRect(0f, 0f, 792f, 300f);
            CollateSpec none = DeathCertificate(PerforationStyle.None, CollateDirection.HorizontalStrip,
                                                198f, 198f, 198f, 198f);

            // PerforationStyle.None：任何编号都返回 null（绘制期据此不画缝线）。
            for (int b = -1; b <= none.Count; b++)
            {
                Assert.False(none.PerforationAt(b, total).HasValue, "None 形式在 " + b + " 处给出了缝");
            }

            // 有缝形式：首联之前与末联之后仍无缝，只有内部 N-1 条。
            CollateSpec dashed = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                   198f, 198f, 198f, 198f);
            Assert.False(dashed.PerforationAt(0, total).HasValue);
            Assert.False(dashed.PerforationAt(dashed.Count, total).HasValue);
            Assert.False(dashed.PerforationAt(dashed.Count + 1, total).HasValue);
            Assert.False(dashed.PerforationAt(-5, total).HasValue);

            Assert.Equal(198f, dashed.PerforationAt(1, total).Value);
            Assert.Equal(396f, dashed.PerforationAt(2, total).Value);
            Assert.Equal(594f, dashed.PerforationAt(3, total).Value);

            // DiceCut 与 DashedCut 只在"怎么画"上不同，缝的坐标解算同源。
            CollateSpec dice = DeathCertificate(PerforationStyle.DiceCut, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);
            for (int b = 1; b < dice.Count; b++)
            {
                Assert.Equal(dashed.PerforationAt(b, total), dice.PerforationAt(b, total));
            }

            // 纵叠方向：缝沿 y 累加，坐标取下一联的 Top（CopyDef.Width 在此方向记的是"该联的高"）。
            CollateSpec stack = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.VerticalStack,
                                                  150f, 150f, 150f, 150f);
            SKRect tallTotal = new SKRect(0f, 0f, 420f, 600f);
            Assert.Equal(600f, stack.TotalExtent);
            Assert.Equal(0f, stack.ExtentDeviation(tallTotal));
            Assert.Equal(150f, stack.PerforationAt(1, tallTotal).Value);
            Assert.Equal(300f, stack.PerforationAt(2, tallTotal).Value);
            Assert.Equal(450f, stack.PerforationAt(3, tallTotal).Value);
            Assert.Equal(300f, stack.CopyRect(2, tallTotal).Top);     // 近端 = 前两联高之和
            Assert.Equal(150f, stack.CopyRect(1, tallTotal).Top);
            Assert.Equal(tallTotal.Bottom, stack.CopyRect(3, tallTotal).Bottom);

            // 方向决定比的是哪个尺寸：纵叠拿 Height 比，所以同一块 792x300 的区域
            // 对同一份 4x150 的规格报出 300 的偏差（600-300），而横排拿 Width 比报 192（600-792）。
            Assert.Equal(300f, stack.ExtentDeviation(total));
            CollateSpec horizontal = DeathCertificate(PerforationStyle.DashedCut,
                                                      CollateDirection.HorizontalStrip, 150f, 150f, 150f, 150f);
            Assert.Equal(192f, horizontal.ExtentDeviation(total));

            // 同一份 4x150 的规格换成横排（欠配 192px）时，末联仍按"远端取版面边缘"吃下剩余：
            // 左缘 = 150+150+150 = 450，右缘 = 版面右缘 792，实际宽 342（声明 150）。
            SKRect horizontalLast = horizontal.CopyRect(3, total);
            Assert.Equal(450f, horizontalLast.Left);
            Assert.Equal(total.Right, horizontalLast.Right);
            Assert.Equal(342f, horizontalLast.Width);
        }

        // ================================================================ 二、每联只画自己的字段

        [Fact]
        public void 字段_每联显示集等于共有加本联独有()
        {
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);

            for (int i = 0; i < spec.Count; i++)
            {
                CopyDef copy = spec.Copies[i];

                // 期望集 = visibleFields ∪ extraFields(该联)。
                HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal);
                foreach (string field in copy.VisibleFields) { expected.Add(field); }
                foreach (string field in copy.ExtraFields) { expected.Add(field); }

                // 「不漏」：期望里的每个字段都必须 ShowsField。
                List<string> missing = new List<string>();
                foreach (string field in expected)
                {
                    if (!copy.ShowsField(field)) { missing.Add(field); }
                }
                Assert.True(missing.Count == 0, copy.Key + " 少画了字段：" + Join(missing));

                // 「不串」：在全字段域里扫，凡 ShowsField 的都必须落在期望集内
                // ——即别联的 extraFields 一律不许出现在本联。
                List<string> leaked = new List<string>();
                foreach (string field in spec.AllFields)
                {
                    if (copy.ShowsField(field) && !expected.Contains(field)) { leaked.Add(field); }
                }
                Assert.True(leaked.Count == 0, copy.Key + " 串进了别联的独有字段：" + Join(leaked));

                // 并集规模也直接复算（同联 visible/extra 互斥，不重不漏时 |并| = |A| + |B|）。
                Assert.Equal(copy.VisibleFields.Count + copy.ExtraFields.Count, expected.Count);
                int shown = 0;
                foreach (string field in spec.AllFields)
                {
                    if (copy.ShowsField(field)) { shown++; }
                }
                Assert.Equal(expected.Count, shown);
            }
        }

        [Fact]
        public void 字段_共有集与差异集与全集按声明顺序可复算()
        {
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);

            // 首次出现顺序：医师存根的五项共有 -> 户籍联独有 -> 殡仪馆联两项独有 -> 卫生统计联独有。
            string[] allOrder = new[] { FieldName, FieldSex, FieldAge, FieldDeathDate, FieldCause,
                                         FieldPoliceSeal, FieldFuneralHotline, FieldHearseNo, FieldStatCode };
            Assert.Equal(9, spec.AllFields.Count);
            for (int i = 0; i < allOrder.Length; i++)
            {
                Assert.Equal(allOrder[i], spec.AllFields[i]);
            }

            Assert.Equal(5, spec.CommonFields.Count);
            Assert.Equal(FieldName, spec.CommonFields[0]);
            Assert.Equal(FieldCause, spec.CommonFields[4]);

            Assert.Equal(4, spec.DifferentiatingFields.Count);
            Assert.Equal(FieldPoliceSeal, spec.DifferentiatingFields[0]);
            Assert.Equal(FieldFuneralHotline, spec.DifferentiatingFields[1]);
            Assert.Equal(FieldHearseNo, spec.DifferentiatingFields[2]);
            Assert.Equal(FieldStatCode, spec.DifferentiatingFields[3]);

            // 差异集 = 全集 - 共有集（顺序保持）。
            HashSet<string> common = new HashSet<string>(spec.CommonFields, StringComparer.Ordinal);
            List<string> recomputed = new List<string>();
            foreach (string field in spec.AllFields)
            {
                if (!common.Contains(field)) { recomputed.Add(field); }
            }
            Assert.Equal(spec.DifferentiatingFields.Count, recomputed.Count);
            for (int i = 0; i < recomputed.Count; i++)
            {
                Assert.Equal(recomputed[i], spec.DifferentiatingFields[i]);
            }

            // 殡仪馆联逐字段点名（死亡证明口径：这两项只有殡葬联有）。
            CopyDef funeral = spec.FindCopy("funeral-copy");
            Assert.Equal(2, funeral.ExtraFields.Count);
            Assert.True(funeral.ShowsField(FieldFuneralHotline));
            Assert.True(funeral.ShowsField(FieldHearseNo));
            Assert.False(funeral.ShowsField(FieldPoliceSeal));
            Assert.False(funeral.ShowsField(FieldStatCode));
        }

        [Fact]
        public void 字段_一联独有可能与别联共有同名且跨联不互斥()
        {
            // 增值税发票场景：备注项在发票联是"多出的一栏"、在记账联是"共有栏位"。
            // 源码只校验**同一联内** visible/extra 互斥，跨联不校验，所以这种写法合法；
            // ShowsField 取并集后语义仍然正确（两联都出现，因此它不再是差异字段）。
            CopyDef invoice = new CopyDef("invoice", "发票联", 100f,
                new List<string> { FieldName, FieldSex }, new List<string> { FieldCause });
            CopyDef bookkeeping = new CopyDef("bookkeeping", "记账联", 100f,
                new List<string> { FieldName, FieldSex, FieldCause }, NoExtra());

            CollateSpec cross = new CollateSpec(new List<CopyDef> { invoice, bookkeeping },
                                                PerforationStyle.DashedCut, CollateDirection.HorizontalStrip);

            Assert.Equal(3, cross.AllFields.Count);
            Assert.Equal(3, cross.CommonFields.Count);          // cause 在两联都出现，计入共有
            Assert.Contains(FieldCause, cross.CommonFields);
            Assert.Empty(cross.DifferentiatingFields);
            Assert.True(invoice.ShowsField(FieldCause) && bookkeeping.ShowsField(FieldCause));

            // 【现状，非期望】"独有"这一语义没有被校验：同一个键可以同时写进两联的 extraFields。
            // 结果仍然是"两联都显示"，但 DifferentiatingFields 会把它算作差异字段，
            // 与 extraFields 的文档口径（该联多出来的部分）不符。见回报「疑似缺陷 D3」。
            CopyDef withRemark = new CopyDef("copy-a", "甲联", 100f,
                new List<string> { FieldName }, new List<string> { FieldPoliceSeal });
            CopyDef alsoRemark = new CopyDef("copy-b", "乙联", 100f,
                new List<string> { FieldName }, new List<string> { FieldPoliceSeal });
            CopyDef without = new CopyDef("copy-c", "丙联", 100f, new List<string> { FieldName }, NoExtra());

            CollateSpec duplicated = new CollateSpec(new List<CopyDef> { withRemark, alsoRemark, without },
                                                     PerforationStyle.None, CollateDirection.HorizontalStrip);
            Assert.Single(duplicated.DifferentiatingFields);
            Assert.Equal(FieldPoliceSeal, duplicated.DifferentiatingFields[0]);
            Assert.True(withRemark.ShowsField(FieldPoliceSeal) && alsoRemark.ShowsField(FieldPoliceSeal) &&
                        !without.ShowsField(FieldPoliceSeal),
                    "现状：同一字段被声明为两联的 extraFields 时不报错，两联都会画它");
        }

        [Fact]
        public void 字段_同联相交与空键与重复键当场拒绝()
        {
            // 同一联里 extra 与 visible 相交：当场抛，不留到绘制期（源码注释写明这是关键约束）。
            ArgumentException conflict = Assert.Throws<ArgumentException>(() => new CopyDef(
                "a", "甲", 100f,
                new List<string> { FieldName, FieldSex },
                new List<string> { FieldSex, FieldPoliceSeal }));
            Assert.Equal("extraFields", conflict.ParamName);
            Assert.True(conflict.Message.Contains(FieldSex), "冲突消息要列出冲突字段：" + conflict.Message);

            // 空字段键（空串）在 visible 与 extra 两侧都拒绝。
            ArgumentException emptyVisible = Assert.Throws<ArgumentException>(() => new CopyDef(
                "a", "甲", 100f, new List<string> { FieldName, string.Empty }, NoExtra()));
            Assert.Equal("visibleFields", emptyVisible.ParamName);
            ArgumentException emptyExtra = Assert.Throws<ArgumentException>(() => new CopyDef("a", "甲", 100f,
                new List<string> { FieldName }, new List<string> { string.Empty }));
            Assert.Equal("extraFields", emptyExtra.ParamName);

            // null 集合与 null key/label。
            Assert.Throws<ArgumentNullException>(() => new CopyDef("a", "甲", 100f, null, NoExtra()));
            Assert.Throws<ArgumentNullException>(() => new CopyDef("a", "甲", 100f, CommonFields(), null));
            Assert.Throws<ArgumentNullException>(() => new CopyDef(null, "甲", 100f, CommonFields(), NoExtra()));
            Assert.Throws<ArgumentNullException>(() => new CopyDef("a", null, 100f, CommonFields(), NoExtra()));
            ArgumentException emptyKey = Assert.Throws<ArgumentException>(
                () => new CopyDef(string.Empty, "甲", 100f, CommonFields(), NoExtra()));
            Assert.Equal("key", emptyKey.ParamName);

            // 同一联内重复声明的字段按键首次出现去重（保持声明顺序），避免画两遍。
            CopyDef dedup = new CopyDef("a", "甲", 100f,
                new List<string> { FieldName, FieldSex, FieldName },
                new List<string> { FieldPoliceSeal, FieldPoliceSeal });
            Assert.Equal(2, dedup.VisibleFields.Count);
            Assert.Equal(FieldName, dedup.VisibleFields[0]);
            Assert.Equal(FieldSex, dedup.VisibleFields[1]);
            Assert.Single(dedup.ExtraFields);
        }

        [Fact]
        public void 字段_查找与判定按Ordinal且空键为假()
        {
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);

            Assert.Equal(0, spec.IndexOfCopy("doctor-stub"));
            Assert.Equal(3, spec.IndexOfCopy("health-stat"));
            Assert.Equal(-1, spec.IndexOfCopy("no-such-copy"));
            Assert.Equal(-1, spec.IndexOfCopy(null));
            Assert.Equal(-1, spec.IndexOfCopy(string.Empty));
            Assert.Null(spec.FindCopy("no-such-copy"));
            Assert.Equal("police-copy", spec.FindCopy("police-copy").Key);

            // Ordinal：大小写与前后空格都算不同键。
            Assert.Equal(-1, spec.IndexOfCopy("Health-Stat"));
            Assert.Equal(-1, spec.IndexOfCopy(" health-stat"));
            CopyDef stub = spec.Copies[0];
            Assert.False(stub.ShowsField(FieldName.ToUpperInvariant()), "ShowsField 必须区分大小写");
            Assert.False(stub.ShowsField(" " + FieldName), "ShowsField 不允许忽略空白差异");
            Assert.False(stub.ShowsField(null));
            Assert.False(stub.ShowsField(string.Empty));

            // 大小写不同的两联不会触发重名校验（Ordinal 比较），字段集也互不影响。
            CollateSpec caseSplit = new CollateSpec(new List<CopyDef>
            {
                new CopyDef("copy-A", "甲", 100f, CommonFields(), NoExtra()),
                new CopyDef("COPY-A", "乙", 100f, CommonFields(), new List<string> { FieldStatCode }),
            }, PerforationStyle.None, CollateDirection.HorizontalStrip);
            Assert.Equal(0, caseSplit.IndexOfCopy("copy-A"));
            Assert.Equal(1, caseSplit.IndexOfCopy("COPY-A"));
            Assert.False(caseSplit.Copies[0].ShowsField(FieldStatCode));
            Assert.True(caseSplit.Copies[1].ShowsField(FieldStatCode));
            Assert.Equal(6, caseSplit.AllFields.Count);
            Assert.Equal(5, caseSplit.CommonFields.Count);
        }

        [Fact]
        public void 联与开面的文本表示用InvariantCulture可跨TFM比对()
        {
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                 198f, 198.425f, 198.5f, 198f);
            Assert.Equal("doctor-stub(医师存根, 198)", spec.Copies[0].ToString());

            // "0.###"：最多三位小数、末尾零抹掉，小数点固定 "."（不随系统区域变成逗号）。
            Assert.Equal("police-copy(户籍联, 198.425)", spec.Copies[1].ToString());
            Assert.Equal("funeral-copy(殡仪馆联, 198.5)", spec.Copies[2].ToString());

            Spread spread = new Spread(3, 4, 0);
            Assert.Equal("spread#0: L=3 R=4", spread.ToString());
            Assert.Equal(3, spread.LeftLogicalPage);
            Assert.Equal(4, spread.RightLogicalPage);
            Assert.Equal(0, spread.SpreadIndex);
            Assert.False(spread.IsEmptyOnLeft);
            Assert.False(spread.IsEmptyOnRight);
            Assert.True(new Spread(0, 5, 1).IsEmptyOnLeft);
            Assert.True(new Spread(5, 0, 1).IsEmptyOnRight);
            Assert.Equal("spread#1: L=0 R=5", new Spread(0, 5, 1).ToString());
        }

        // ================================================================ 三、PrintOrderOf 构成排列

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(12)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(60)]
        [InlineData(101)]
        public void 印序_对全部逻辑页构成排列(int totalLogicalPages)
        {
            BookletImposition booklet = BookletImposition.SaddleStitch(totalLogicalPages);

            // 长度相等：逻辑页 1..N 各给出一个印刷序。
            List<int> orders = new List<int>();
            for (int page = 1; page <= totalLogicalPages; page++)
            {
                orders.Add(booklet.PrintOrderOf(page));
            }
            Assert.Equal(totalLogicalPages, orders.Count);

            // 无重复。
            Assert.Equal(totalLogicalPages, orders.Distinct().Count());

            // 无遗漏：排序后必须逐项等于"配对表里非空白槽位号（1 起）"排序后的序列。
            // 有空白页位时印序不是 1..N 的连续段，所以基准取配对表自身而不是硬写 1..N。
            List<int> expected = NonBlankPrintPositions(booklet).OrderBy(x => x).ToList();
            List<int> sorted = orders.OrderBy(x => x).ToList();
            Assert.Equal(expected.Count, sorted.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.True(expected[i] == sorted[i],
                            "N=" + totalLogicalPages + " 第 " + i + " 项印序应为 " + expected[i] +
                            "（来自配对表非空白槽位），实到 " + sorted[i] + "；全序=" + JoinInt(sorted));
            }

            // 上界：所有印序都落在 1..PhysicalPages 内。
            foreach (int order in orders)
            {
                Assert.True(order >= 1 && order <= booklet.PhysicalPages,
                            "N=" + totalLogicalPages + " 印序 " + order + " 越出 1.." + booklet.PhysicalPages);
            }

            // 无空白（N 是 4 的整数倍）时排列恰好就是 1..N。
            if (totalLogicalPages % 4 == 0)
            {
                Assert.Equal(totalLogicalPages, booklet.PhysicalPages);
                for (int i = 0; i < sorted.Count; i++)
                {
                    Assert.True(sorted[i] == i + 1,
                        "N=" + totalLogicalPages + " 无空白时应为 1..N 的排列，第 " + i + " 项实到 " + sorted[i]);
                }
            }

            // 排列的另一半：配对表里出现过的逻辑页集合恰好是 1..N 各一次。
            IReadOnlyList<int> flat = booklet.FlattenedPrintOrder;
            Assert.Equal(booklet.PhysicalPages, flat.Count);
            int[] seen = new int[totalLogicalPages + 1];
            int blanks = 0;
            for (int i = 0; i < flat.Count; i++)
            {
                if (flat[i] == 0) { blanks++; continue; }
                Assert.True(flat[i] >= 1 && flat[i] <= totalLogicalPages,
                            "N=" + totalLogicalPages + " 配对表含非法页号 " + flat[i]);
                seen[flat[i]]++;
            }
            for (int page = 1; page <= totalLogicalPages; page++)
            {
                Assert.True(seen[page] == 1, "N=" + totalLogicalPages + " 逻辑页 " + page + " 出现 " +
                                            seen[page] + " 次，应恰好 1 次");
                // 印序 == 该页在配对表里的槽位号（1 起）：两侧同源，不允许两套实现。
                Assert.Equal(IndexInFlat(flat, page) + 1, booklet.PrintOrderOf(page));
            }
            Assert.Equal(booklet.PhysicalPages - booklet.TotalLogicalPages, blanks);
        }

        [Fact]
        public void 印序_越界逻辑页被拒绝()
        {
            BookletImposition booklet = BookletImposition.SaddleStitch(8);

            // PrintSheetOf 不认空白页位（0），PrintOrderOf 认。
            ArgumentOutOfRangeException sheetZero = Assert.Throws<ArgumentOutOfRangeException>(
                () => booklet.PrintSheetOf(0));
            Assert.Equal("logicalPage", sheetZero.ParamName);
            Assert.Throws<ArgumentOutOfRangeException>(() => booklet.PrintSheetOf(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => booklet.PrintSheetOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => booklet.PrintOrderOf(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => booklet.PrintOrderOf(9));

            // 满册（无空白）时逻辑页 0 无槽可给，按源码抛 InvalidOperationException。
            Assert.Throws<InvalidOperationException>(() => booklet.PrintOrderOf(0));

            Assert.Equal(8, booklet.TotalLogicalPages);
            Assert.Equal(8, booklet.PhysicalPages);
            Assert.Equal(2, booklet.SheetCount);
            Assert.Equal(2, booklet.PagesPerSpread);
        }

        // ================================================================ 四、开面配对可枚举

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(12)]
        [InlineData(17)]
        [InlineData(101)]
        public void 开面_配对可枚举且每页恰属一开面(int totalLogicalPages)
        {
            BookletImposition booklet = BookletImposition.SaddleStitch(totalLogicalPages);
            IReadOnlyList<Spread> spreads = booklet.Spreads;

            // 可枚举：开面数 = 物理页数 / 每开面页数，序号严格 0..count-1 递增。
            Assert.Equal(booklet.PhysicalPages / 2, spreads.Count);
            Assert.Equal(spreads.Count * 2, booklet.PhysicalPages);
            for (int k = 0; k < spreads.Count; k++)
            {
                Assert.Equal(k, spreads[k].SpreadIndex);
            }

            // 配对表与开面展平同源（[L0,R0,L1,R1,...]）。
            for (int k = 0; k < spreads.Count; k++)
            {
                Assert.Equal(spreads[k].LeftLogicalPage, booklet.FlattenedPrintOrder[2 * k]);
                Assert.Equal(spreads[k].RightLogicalPage, booklet.FlattenedPrintOrder[2 * k + 1]);
            }

            // 每个逻辑页恰好属于一个开面（空白页位 0 不计）。
            int[] owner = new int[totalLogicalPages + 1];
            for (int i = 1; i <= totalLogicalPages; i++) { owner[i] = -1; }
            int blankSides = 0;
            for (int k = 0; k < spreads.Count; k++)
            {
                ClaimPage(owner, spreads[k].LeftLogicalPage, k, totalLogicalPages, ref blankSides);
                ClaimPage(owner, spreads[k].RightLogicalPage, k, totalLogicalPages, ref blankSides);
            }
            for (int page = 1; page <= totalLogicalPages; page++)
            {
                Assert.True(owner[page] >= 0, "N=" + totalLogicalPages + " 逻辑页 " + page + " 不属于任何开面");
            }
            Assert.Equal(booklet.PhysicalPages - totalLogicalPages, blankSides);

            // 奇偶与开面语义（源码 BookletImposition 第 87-91 行的注释）：
            //   第 k 个开面取首尾相接的一对页位 a = physical - k、b = k + 1；
            //   k 为偶数（每张纸正面）左大右小 (a, b)，k 为奇数（背面）左小右大 (b, a)；
            //   页位编号越过内容页数的落成空白 0。
            // 该规则按**页位编号**（映射为逻辑页之前）判大小，映射后才可能出现 0。
            int physical = booklet.PhysicalPages;
            for (int k = 0; k < spreads.Count; k++)
            {
                Spread actual = spreads[k];
                Spread expected = ExpectedSpread(physical, totalLogicalPages, k);
                Assert.True(expected.LeftLogicalPage == actual.LeftLogicalPage &&
                            expected.RightLogicalPage == actual.RightLogicalPage,
                            "N=" + totalLogicalPages + " " + actual + " 与配对公式给出的 " + expected + " 不符");

                int rawA = physical - k;
                int rawB = k + 1;
                bool front = (k & 1) == 0;
                Assert.True(rawA > rawB,
                    "N=" + totalLogicalPages + " 开面 #" + k + " 的首尾页位不再相接：" + rawA + "/" + rawB);
                if (front)
                {
                    // 正面：左半取"大页位"（册子末尾那端），右半取"小页位"（册子开头那端）。
                    Assert.True(actual.LeftLogicalPage >= actual.RightLogicalPage ||
                                actual.IsEmptyOnLeft || actual.IsEmptyOnRight,
                                "正面开面 #" + k + " 应当左大右小，实到 " + actual);
                }
                else
                {
                    Assert.True(actual.LeftLogicalPage <= actual.RightLogicalPage ||
                                actual.IsEmptyOnLeft || actual.IsEmptyOnRight,
                                "背面开面 #" + k + " 应当左小右大，实到 " + actual);
                }

                // 一张纸正反各一个开面：#2i 与 #2i+1 同纸，纸号 = k/2。
                int sheet = k / 2;
                Assert.Equal(booklet.SheetCount, (spreads.Count + 1) / 2);
                if (!actual.IsEmptyOnLeft) { Assert.Equal(sheet, booklet.PrintSheetOf(actual.LeftLogicalPage)); }
                if (!actual.IsEmptyOnRight) { Assert.Equal(sheet, booklet.PrintSheetOf(actual.RightLogicalPage)); }
            }
        }

        [Fact]
        public void 开面_四页八页十二页的鞍装表逐项钉死()
        {
            // 由源码配对公式手工复算后钉死（k 为偶数取 (physical-k, k+1)，奇数取 (k+1, physical-k)，越界补 0）：
            // N=4：physical=4，2 个开面
            //   k=0: a=Map(4)=4, b=Map(1)=1, 正面 -> (4,1)
            //   k=1: a=Map(3)=3, b=Map(2)=2, 背面 -> (2,3)
            BookletImposition four = BookletImposition.SaddleStitch(4);
            AssertSpreadTable(four, new[] { 4, 1, 2, 3 });
            Assert.Equal(1, four.SheetCount);
            Assert.Equal(1, four.PrintOrderOf(4));
            Assert.Equal(2, four.PrintOrderOf(1));
            Assert.Equal(3, four.PrintOrderOf(2));
            Assert.Equal(4, four.PrintOrderOf(3));

            // N=8：physical=8，4 个开面（标准骑马钉表）
            //   k=0 (8,1) | k=1 (2,7) | k=2 (6,3) | k=3 (4,5)
            BookletImposition eight = BookletImposition.SaddleStitch(8);
            AssertSpreadTable(eight, new[] { 8, 1, 2, 7, 6, 3, 4, 5 });
            Assert.Equal(2, eight.SheetCount);

            // 第一张纸正面"末页 + 首页"、背面"2 + 末-1"；纸内页位逐页可查。
            Assert.Equal(0, eight.PrintSheetOf(8));
            Assert.Equal(0, eight.PrintSheetOf(1));
            Assert.Equal(0, eight.PrintSheetOf(2));
            Assert.Equal(0, eight.PrintSheetOf(7));
            Assert.Equal(1, eight.PrintSheetOf(6));
            Assert.Equal(1, eight.PrintSheetOf(3));
            Assert.Equal(1, eight.PrintSheetOf(4));
            Assert.Equal(1, eight.PrintSheetOf(5));
            Assert.Equal(1, eight.PrintOrderOf(8));    // 末页最先上机
            Assert.Equal(2, eight.PrintOrderOf(1));
            Assert.Equal(3, eight.PrintOrderOf(2));
            Assert.Equal(4, eight.PrintOrderOf(7));
            Assert.Equal(5, eight.PrintOrderOf(6));
            Assert.Equal(6, eight.PrintOrderOf(3));
            Assert.Equal(7, eight.PrintOrderOf(4));
            Assert.Equal(8, eight.PrintOrderOf(5));

            // N=12：physical=12，6 个开面
            //   k=0 a=12 b=1 -> (12,1)；k=1 a=11 b=2 背面 -> (2,11)
            //   k=2 a=10 b=3 -> (10,3)；k=3 a=9 b=4 背面 -> (4,9)
            //   k=4 a=8 b=5 -> (8,5)；  k=5 a=7 b=6 背面 -> (6,7)
            BookletImposition twelve = BookletImposition.SaddleStitch(12);
            AssertSpreadTable(twelve, new[] { 12, 1, 2, 11, 10, 3, 4, 9, 8, 5, 6, 7 });
            Assert.Equal(3, twelve.SheetCount);

            // 印序表（= 页位所在槽序号）：1..12 的一个排列；纸号 = 槽序号整除 4。
            int[] expectedOrders = new[] { 2, 3, 6, 7, 10, 11, 12, 9, 8, 5, 4, 1 };
            for (int page = 1; page <= 12; page++)
            {
                Assert.Equal(expectedOrders[page - 1], twelve.PrintOrderOf(page));
                Assert.Equal(SheetOf(twelve, page), twelve.PrintSheetOf(page));
            }

            // 满册时每张纸正反四个页位；同一张纸的两个开面是 #2i 与 #2i+1。
            Assert.Equal(0, twelve.PrintSheetOf(12));
            Assert.Equal(0, twelve.PrintSheetOf(1));
            Assert.Equal(0, twelve.PrintSheetOf(2));
            Assert.Equal(0, twelve.PrintSheetOf(11));
            Assert.Equal(1, twelve.PrintSheetOf(10));
            Assert.Equal(1, twelve.PrintSheetOf(3));
            Assert.Equal(1, twelve.PrintSheetOf(4));
            Assert.Equal(1, twelve.PrintSheetOf(9));
            Assert.Equal(2, twelve.PrintSheetOf(8));
            Assert.Equal(2, twelve.PrintSheetOf(5));
            Assert.Equal(2, twelve.PrintSheetOf(6));
            Assert.Equal(2, twelve.PrintSheetOf(7));
        }

        [Fact]
        public void 开面_奇数页补空白与单页边界()
        {
            // 单页：一张纸 4 个页位，3 个空白；页 1 落在 #0 开面右半（正面右侧）。
            BookletImposition one = BookletImposition.SaddleStitch(1);
            Assert.Equal(1, one.TotalLogicalPages);
            Assert.Equal(4, one.PhysicalPages);
            Assert.Equal(1, one.SheetCount);
            Assert.Equal(2, one.Spreads.Count);
            Assert.Equal("spread#0: L=0 R=1", one.Spreads[0].ToString());
            Assert.Equal("spread#1: L=0 R=0", one.Spreads[1].ToString());
            Assert.True(one.Spreads[0].IsEmptyOnLeft);
            Assert.True(one.Spreads[1].IsEmptyOnLeft && one.Spreads[1].IsEmptyOnRight);
            Assert.Equal(2, one.PrintOrderOf(1));
            Assert.Equal(0, one.PrintSheetOf(1));
            Assert.Equal(1, one.PrintOrderOf(0));        // 逻辑页 0 = 第一个空白槽位
            Assert.Equal(3, CountBlanks(one));

            // 3 页：补 1 个空白（不足半页的那一侧）。
            BookletImposition three = BookletImposition.SaddleStitch(3);
            Assert.Equal(4, three.PhysicalPages);
            Assert.Equal(1, three.SheetCount);
            AssertSpreadTable(three, new[] { 0, 1, 2, 3 });
            Assert.Equal(1, CountBlanks(three));
            Assert.Equal(1, three.PrintOrderOf(0));

            // 5 页：physical=8，空白 3 个。
            //   k=0 a=Map(8)=0 b=1 -> (0,1)；k=1 a=Map(7)=0 b=2 背面 -> (2,0)
            //   k=2 a=Map(6)=0 b=3 -> (0,3)；k=3 a=5 b=4 背面 -> (4,5)
            BookletImposition five = BookletImposition.SaddleStitch(5);
            Assert.Equal(8, five.PhysicalPages);
            Assert.Equal(2, five.SheetCount);
            AssertSpreadTable(five, new[] { 0, 1, 2, 0, 0, 3, 4, 5 });
            Assert.Equal(3, CountBlanks(five));
            Assert.Equal(1, five.PrintOrderOf(0));
            Assert.Equal(2, five.PrintOrderOf(1));
            Assert.Equal(6, five.PrintOrderOf(3));
            Assert.True(five.Spreads[0].IsEmptyOnLeft, "空白应当补在外侧（封面之外的左半）");

            // 页数 == 4 的整数倍：不补空白，物理页数 == 逻辑页数。
            int[] multiples = new[] { 4, 8, 12, 16, 20 };
            foreach (int n in multiples)
            {
                BookletImposition booklet = BookletImposition.SaddleStitch(n);
                Assert.Equal(n, booklet.PhysicalPages);
                Assert.Equal(n / 4, booklet.SheetCount);
                Assert.Equal(n / 2, booklet.Spreads.Count);
                Assert.Equal(0, CountBlanks(booklet));
                Assert.Throws<InvalidOperationException>(() => booklet.PrintOrderOf(0));
                foreach (Spread spread in booklet.Spreads)
                {
                    Assert.False(spread.IsEmptyOnLeft || spread.IsEmptyOnRight,
                                 "N=" + n + " 无空白却出现空白半页：" + spread);
                }
            }

            // 偶数但非 4 的倍数（6, 10）：补到 4 的整数倍，空白恰为 2。
            BookletImposition six = BookletImposition.SaddleStitch(6);
            Assert.Equal(8, six.PhysicalPages);
            Assert.Equal(2, CountBlanks(six));
            BookletImposition ten = BookletImposition.SaddleStitch(10);
            Assert.Equal(12, ten.PhysicalPages);
            Assert.Equal(2, CountBlanks(ten));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void 开面_零页与负页被拒绝(int totalLogicalPages)
        {
            ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
                () => new BookletImposition(totalLogicalPages, 2));
            Assert.Equal("totalLogicalPages", error.ParamName);
            Assert.True(error.Message.Contains("必须 >= 1"), "消息要写明下界：" + error.Message);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(0)]
        [InlineData(-2)]
        public void 开面_非二页每开面目前不支持(int pagesPerSpread)
        {
            ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(
                () => new BookletImposition(8, pagesPerSpread));
            Assert.Equal("pagesPerSpread", error.ParamName);
            Assert.True(error.Message.Contains("只支持"), "消息要写明当前只实现 2 页/开面：" + error.Message);
        }

        // ================================================================ 五、像素用例（真实管线）

        [Fact]
        public void 像素_竖排裁切线落在解算位置且是断线不是实线()
        {
            SKRect total = new SKRect(0f, 0f, 792f, 300f);
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);

            using (Chart chart = Render(spec, total))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.Equal(792, probe.Width);
                Assert.Equal(300, probe.Height);

                // 空白带里只有缝线会落墨，所以扫到的竖线数必须正好是 N-1 条。
                IReadOnlyList<double> seams = probe.StrongVerticalLines(
                    total.Left, total.Right, SeamScanTop, SeamScanBottom, 0.45);
                Assert.Equal(spec.Count - 1, seams.Count);

                for (int b = 1; b < spec.Count; b++)
                {
                    float solved = spec.PerforationAt(b, total).Value;
                    Assert.True(Math.Abs(seams[b - 1] - solved) <= 0.6,
                        "第 " + b + " 道缝的像素中心 " + seams[b - 1] + " 与解算位置 " + solved + " 不符");

                    // 缝线压在两联共享的那条边上（几何 == 像素 == 解算，三者同源）。
                    Assert.Equal(solved, spec.CopyRect(b, total).Left);
                    Assert.Equal(solved, spec.CopyRect(b - 1, total).Right);
                    Assert.True(Math.Abs(seams[b - 1] - spec.CopyRect(b, total).Left) <= 0.6,
                        "缝线没有落在两联共享边上：像素 " + seams[b - 1] +
                        "，联界 " + spec.CopyRect(b, total).Left);
                }

                // 版面外缘不该出现贯通线：缝只有内部 N-1 条，逐条已在上面点名。
                Assert.Equal(3, seams.Count);

                // 断线证据：虚线参数取自 ChartStyle 默认令牌 Series.DashInterval=4 / DashPhase=3，
                // 即"4 落 3 空"，故整列覆盖率应为 4/7≈0.571、最长连续墨段约 4px（+抗锯齿 1 行）。
                // 实线的话覆盖率会是 1.0、最长墨段 300px。实测值见断言消息。
                int seamColumn = (int)Math.Round(seams[1]);
                double coverage = InkCoverageInColumn(probe, seamColumn, 0, probe.Height);
                Assert.True(coverage > 0.45 && coverage < 0.8,
                    "缝列覆盖率 " + coverage.ToString("0.000") + " 不在 4/3 虚线应有的区间内");
                int maxRun = MaxInkRunInColumn(probe, seamColumn, 0, probe.Height);
                Assert.True(maxRun <= 8, "缝列最长墨段 " + maxRun + "px，虚线没起作用（虚线参数 [4,3]）");
                Assert.True(probe.HasInkInBox(seamColumn - 1, 40, seamColumn + 2, 250), "第二条缝整段没有墨");
            }
        }

        [Fact]
        public void 像素_每联只在属于自己的字段行落墨别联字段行无墨()
        {
            SKRect total = new SKRect(0f, 0f, 792f, 300f);
            CollateSpec spec = DeathCertificate(PerforationStyle.DashedCut, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);

            using (Chart chart = Render(spec, total))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                List<string> wrong = new List<string>();
                int inked = 0;

                for (int i = 0; i < spec.Count; i++)
                {
                    SKRect copyRect = spec.CopyRect(i, total);
                    for (int r = 0; r < spec.AllFields.Count; r++)
                    {
                        string field = spec.AllFields[r];
                        SKRect box = FieldBox(copyRect, r);

                        // 版面不变量：字段行必须整段落在本联矩形内（不许越缝串到邻联）。
                        Assert.True(box.Left >= copyRect.Left && box.Right <= copyRect.Right,
                            field + " 在本联的判墨窗口越出了联矩形");
                        Assert.True(box.Bottom <= copyRect.Bottom,
                            field + " 在本联的判墨窗口越出了联底缘");

                        bool shown = spec.Copies[i].ShowsField(field);
                        bool hasInk = probe.HasInkInBox((int)Math.Round(box.Left), (int)Math.Round(box.Top),
                                                        (int)Math.Round(box.Right), (int)Math.Round(box.Bottom));
                        if (shown != hasInk)
                        {
                            wrong.Add("联" + i + "(" + spec.Copies[i].Key + ") 字段 " + field +
                                      " 应显示=" + shown + "，像素有墨=" + hasInk +
                                      " 窗口(" + box.Left + "," + box.Top + "," + box.Right + "," + box.Bottom + ")");
                        }
                        if (hasInk) { inked++; }
                    }
                }

                Assert.True(wrong.Count == 0, "字段落墨与 ShowsField 不一致：\r\n" + string.Join("\r\n", wrong.ToArray()));

                // 该有墨的数量可复算：共有 5 项 × 4 联 + 独有 4 项 = 24 个字段行。
                Assert.Equal(spec.Count * spec.CommonFields.Count + spec.DifferentiatingFields.Count, inked);
                Assert.Equal(24, inked);

                // 逐联点名独有字段：殡仪馆联的"殡葬热线"在自己联内有墨，
                // 且在别的联的同一行号位置完全无墨。
                int hotlineRow = IndexOf(spec.AllFields, FieldFuneralHotline);
                SKRect ownBox = FieldBox(spec.CopyRect(2, total), hotlineRow);
                Assert.True(probe.HasInkInBox((int)ownBox.Left, (int)ownBox.Top, (int)ownBox.Right, (int)ownBox.Bottom),
                    "殡葬热线在殡仪馆联没落墨");
                int[] otherCopies = new[] { 0, 1, 3 };
                foreach (int other in otherCopies)
                {
                    SKRect foreign = FieldBox(spec.CopyRect(other, total), hotlineRow);
                    Assert.False(probe.HasInkInBox((int)foreign.Left, (int)foreign.Top,
                            (int)foreign.Right, (int)foreign.Bottom),
                        "殡葬热线串进了联 " + other + "（" + spec.Copies[other].Key + "）");
                }

                // 缝位置与字段窗口互不侵占：缝在 x=198/396/594，窗口都缩进 8px。
                Assert.True(TextPadX > SeamStroke, "缩进必须大于线粗，否则缝线会落进字段窗口");
            }
        }

        [Fact]
        public void 像素_纵叠刀模孔逐孔枚举且缝在联界上()
        {
            // 发血单式上下两联：纵叠方向下 CopyDef.Width 记的是"该联的高"。
            SKRect total = new SKRect(0f, 0f, 420f, 400f);
            CollateSpec spec = BloodIssueTwoCopies(PerforationStyle.DiceCut, CollateDirection.VerticalStack,
                                                    200f, 200f);

            using (Chart chart = Render(spec, total))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.Equal(400f, spec.TotalExtent);
                Assert.Equal(0f, spec.ExtentDeviation(total));

                float seam = spec.PerforationAt(1, total).Value;
                Assert.Equal(200f, seam);
                Assert.Equal(spec.CopyRect(0, total).Bottom, seam);
                Assert.Equal(spec.CopyRect(1, total).Top, seam);

                // 孔位与绘制循环同一公式复算：p 从 Left+HoleFirst 起，步长 HolePitch，止于 Right-HoleEndMargin。
                List<float> centers = new List<float>();
                for (float p = total.Left + HoleFirst; p < total.Right - HoleEndMargin; p += HolePitch)
                {
                    centers.Add(p);
                }
                Assert.Equal(34, centers.Count);   // (420-8-14)/12 = 33.17 -> 33 个完整步长 + 首孔 = 34，实测同值
                Assert.Equal(14f, centers[0]);
                Assert.Equal(410f, centers[centers.Count - 1]);

                int seamTop = (int)Math.Round(seam) - 2;    // 孔半径 3，取中段 5 行判墨最稳
                int seamBottom = (int)Math.Round(seam) + 3;
                foreach (float center in centers)
                {
                    int cx = (int)Math.Round(center);
                    Assert.True(probe.HasInkInBox(cx - 1, seamTop, cx + 2, seamBottom),
                        "孔位 " + center + " 在缝线行上没有墨，孔没落在解算出的缝上");
                }

                // 孔间必须无墨（刀模孔不是实线）。
                for (int i = 0; i + 1 < centers.Count; i++)
                {
                    int cx = (int)Math.Round(centers[i]);
                    Assert.False(probe.HasInkInBox(cx + 5, seamTop, cx + 8, seamBottom),
                        "孔 " + centers[i] + " 与 " + centers[i + 1] + " 之间出现了墨，孔被连成了线");
                }

                // 孔只贴在缝线上：上下各留一条干净带，且没有水平贯通线冒充裁切线。
                Assert.False(probe.HasInkInBox(20, 192, 412, 196), "缝线上方 192..195 出现了墨");
                Assert.False(probe.HasInkInBox(20, 204, 412, 206), "缝线下方 204..205 出现了墨");
                IReadOnlyList<double> rules = probe.StrongHorizontalLines(190, 210, 20, 412, 0.6);
                Assert.Empty(rules);

                // 纵叠下字段行按联偏移：上联独有项不能出现在下联，共有项在两联各自落墨。
                int donorRow = IndexOf(spec.AllFields, FieldDonor);
                Assert.Equal(2, donorRow);
                SKRect upper = FieldBox(spec.CopyRect(0, total), donorRow);
                SKRect lower = FieldBox(spec.CopyRect(1, total), donorRow);
                Assert.True(probe.HasInkInBox((int)upper.Left, (int)upper.Top, (int)upper.Right, (int)upper.Bottom),
                    "供血者项在上联没落墨");
                Assert.False(probe.HasInkInBox((int)lower.Left, (int)lower.Top, (int)lower.Right, (int)lower.Bottom),
                    "供血者项串到了下联");

                int commonRow = IndexOf(spec.AllFields, FieldName);
                SKRect a = FieldBox(spec.CopyRect(0, total), commonRow);
                SKRect b = FieldBox(spec.CopyRect(1, total), commonRow);
                Assert.True(probe.HasInkInBox((int)a.Left, (int)a.Top, (int)a.Right, (int)a.Bottom),
                    "共有字段在上联无墨");
                Assert.True(probe.HasInkInBox((int)b.Left, (int)b.Top, (int)b.Right, (int)b.Bottom),
                    "共有字段在下联无墨");

                // 下联独有项也在位（bag-no 是 AllFields 第 5 个）。
                int bagRow = IndexOf(spec.AllFields, FieldBagNo);
                Assert.Equal(4, bagRow);
                SKRect bag = FieldBox(spec.CopyRect(1, total), bagRow);
                Assert.True(probe.HasInkInBox((int)bag.Left, (int)bag.Top, (int)bag.Right, (int)bag.Bottom),
                    "血袋号在下联没落墨");
                SKRect bagUpper = FieldBox(spec.CopyRect(0, total), bagRow);
                Assert.False(probe.HasInkInBox((int)bagUpper.Left, (int)bagUpper.Top, (int)bagUpper.Right,
                    (int)bagUpper.Bottom), "血袋号串到了上联");
            }
        }

        [Fact]
        public void 像素_无骑缝形式时联界上不出现任何缝线()
        {
            SKRect total = new SKRect(0f, 0f, 792f, 300f);
            CollateSpec spec = DeathCertificate(PerforationStyle.None, CollateDirection.HorizontalStrip,
                                                 198f, 198f, 198f, 198f);

            using (Chart chart = Render(spec, total))
            using (ImageProbe probe = ImageProbe.FromPng(chart.Encode(ChartImageFormat.Png, 96f, 100)))
            {
                Assert.False(spec.PerforationAt(1, total).HasValue);

                // 空白带里一条竖线都不该有（缝不画，字段文字都在这条带之上）。
                IReadOnlyList<double> seams = probe.StrongVerticalLines(
                    total.Left, total.Right, SeamScanTop, SeamScanBottom, 0.45);
                Assert.Empty(seams);

                // 但联仍然被切开了：本联字段在自己联内落墨，别联字段行无墨。
                int sealRow = IndexOf(spec.AllFields, FieldPoliceSeal);
                SKRect here = FieldBox(spec.CopyRect(1, total), sealRow);
                Assert.True(probe.HasInkInBox((int)here.Left, (int)here.Top, (int)here.Right, (int)here.Bottom),
                    "户籍联的派出所盖章项没落墨");
                SKRect there = FieldBox(spec.CopyRect(2, total), sealRow);
                Assert.False(probe.HasInkInBox((int)there.Left, (int)there.Top, (int)there.Right, (int)there.Bottom),
                    "派出所盖章项串到了殡仪馆联");
                SKRect stubBox = FieldBox(spec.CopyRect(0, total), sealRow);
                Assert.False(probe.HasInkInBox((int)stubBox.Left, (int)stubBox.Top, (int)stubBox.Right,
                    (int)stubBox.Bottom), "派出所盖章项串到了医师存根");
            }
        }

        // ====================================================================== 构件与工具

        /// <summary>死亡医学证明四联（docs/06 §3.7 的旗舰场景）。</summary>
        private static CollateSpec DeathCertificate(PerforationStyle perforation, CollateDirection direction,
                                                    float w0, float w1, float w2, float w3)
        {
            return new CollateSpec(new List<CopyDef>
            {
                new CopyDef("doctor-stub", "医师存根", w0, CommonFields(), NoExtra()),
                new CopyDef("police-copy", "户籍联", w1, CommonFields(), new List<string> { FieldPoliceSeal }),
                new CopyDef("funeral-copy", "殡仪馆联", w2, CommonFields(),
                            new List<string> { FieldFuneralHotline, FieldHearseNo }),
                new CopyDef("health-stat", "卫生统计联", w3, CommonFields(), new List<string> { FieldStatCode }),
            }, perforation, direction);
        }

        /// <summary>发血单上下两联：上联多出供血者，下联多出领血者与血袋号。</summary>
        private static CollateSpec BloodIssueTwoCopies(PerforationStyle perforation, CollateDirection direction,
                                                       float h0, float h1)
        {
            List<string> shared = new List<string> { FieldName, FieldVolume };
            return new CollateSpec(new List<CopyDef>
            {
                new CopyDef("blood-stub", "血库存根", h0, shared, new List<string> { FieldDonor }),
                new CopyDef("clinic-copy", "临床联", h1, shared, new List<string> { FieldReceiver, FieldBagNo }),
            }, perforation, direction);
        }

        private static Chart Render(CollateSpec spec, SKRect total)
        {
            ChartDocument document = new ChartDocument();
            document.HospitalName = "示例医院";
            document.FormTitle = "多联次切分试版";
            return new Chart(document, new CollateTemplate(spec, total), new ChartStyle());
        }

        /// <summary>第 row 行字段（相对联矩形）的判墨窗口；绘制端与断言端共用同一函数，杜绝两套坐标。</summary>
        private static SKRect FieldBox(SKRect copyRect, int row)
        {
            float top = copyRect.Top + RowsTop + row * RowPitch;
            return new SKRect(copyRect.Left + TextPadX, top, copyRect.Left + TextPadX + BoxWidth, top + RowHeight);
        }

        private static int IndexOf(IReadOnlyList<string> list, string value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], value, StringComparison.Ordinal)) { return i; }
            }
            return -1;
        }

        private static double InkCoverageInColumn(ImageProbe probe, int x, int fromY, int toY)
        {
            int ink = 0;
            for (int y = fromY; y < toY; y++)
            {
                if (probe.IsInk(x, y)) { ink++; }
            }
            return (double)ink / (toY - fromY);
        }

        private static int MaxInkRunInColumn(ImageProbe probe, int x, int fromY, int toY)
        {
            int max = 0;
            int run = 0;
            for (int y = fromY; y < toY; y++)
            {
                if (probe.IsInk(x, y))
                {
                    run++;
                    if (run > max) { max = run; }
                }
                else
                {
                    run = 0;
                }
            }
            return max;
        }

        private static List<int> NonBlankPrintPositions(BookletImposition booklet)
        {
            List<int> positions = new List<int>();
            IReadOnlyList<int> flat = booklet.FlattenedPrintOrder;
            for (int i = 0; i < flat.Count; i++)
            {
                if (flat[i] != 0) { positions.Add(i + 1); }
            }
            return positions;
        }

        private static int IndexInFlat(IReadOnlyList<int> flat, int logicalPage)
        {
            for (int i = 0; i < flat.Count; i++)
            {
                if (flat[i] == logicalPage) { return i; }
            }
            return -1;
        }

        private static int CountBlanks(BookletImposition booklet)
        {
            int blanks = 0;
            IReadOnlyList<int> flat = booklet.FlattenedPrintOrder;
            for (int i = 0; i < flat.Count; i++)
            {
                if (flat[i] == 0) { blanks++; }
            }
            return blanks;
        }

        /// <summary>按配对表回查某逻辑页所在纸号（供交叉验证用）。</summary>
        private static int SheetOf(BookletImposition booklet, int logicalPage)
        {
            int index = IndexInFlat(booklet.FlattenedPrintOrder, logicalPage);
            return index / 4;
        }

        /// <summary>展平序列即开面逐页拼接：[L0,R0,L1,R1,...]。</summary>
        private static void AssertSpreadTable(BookletImposition booklet, int[] flatExpected)
        {
            Assert.Equal(flatExpected.Length, booklet.FlattenedPrintOrder.Count);
            for (int i = 0; i < flatExpected.Length; i++)
            {
                Assert.True(flatExpected[i] == booklet.FlattenedPrintOrder[i],
                            "N=" + booklet.TotalLogicalPages + " 第 " + i + " 个页位应为 " + flatExpected[i] +
                            "，实到 " + booklet.FlattenedPrintOrder[i] + "；全表=" +
                            JoinInt(booklet.FlattenedPrintOrder));
            }
            for (int k = 0; k < booklet.Spreads.Count; k++)
            {
                Assert.Equal(flatExpected[2 * k], booklet.Spreads[k].LeftLogicalPage);
                Assert.Equal(flatExpected[2 * k + 1], booklet.Spreads[k].RightLogicalPage);
            }
        }

        private static void ClaimPage(int[] owner, int logicalPage, int spreadIndex, int totalLogicalPages,
                                      ref int blankSides)
        {
            if (logicalPage == 0)
            {
                blankSides++;
                return;
            }
            Assert.True(logicalPage >= 1 && logicalPage <= totalLogicalPages,
                        "开面 #" + spreadIndex + " 含非法页号 " + logicalPage);
            Assert.True(owner[logicalPage] < 0,
                        "逻辑页 " + logicalPage + " 同时属于开面 #" + owner[logicalPage] + " 与 #" + spreadIndex);
            owner[logicalPage] = spreadIndex;
        }

        /// <summary>按源码配对公式复算第 k 个开面：页位 physical-k 与 k+1 首尾相接，偶数开面左大右小。</summary>
        private static Spread ExpectedSpread(int physical, int totalLogicalPages, int k)
        {
            int a = MapSlot(physical - k, totalLogicalPages);
            int b = MapSlot(k + 1, totalLogicalPages);
            return (k & 1) == 0 ? new Spread(a, b, k) : new Spread(b, a, k);
        }

        private static int MapSlot(int slot, int totalLogicalPages)
        {
            return slot <= totalLogicalPages ? slot : 0;
        }

        private static void AssertClose(float expected, float actual, float tolerance, string what)
        {
            Assert.True(Math.Abs(expected - actual) <= tolerance,
                        what + " 应为 " + expected.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) +
                        "，实到 " + actual.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture) +
                        "（容差 " + tolerance + "）");
        }

        private static string JoinInt(IEnumerable<int> values)
        {
            return string.Join(",", values.Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture))
                                           .ToArray());
        }

        private static string Join(List<string> values)
        {
            return string.Join(", ", values.ToArray());
        }

        // ------------------------------------------------------------ 走管线用的最小模板

        /// <summary>
        /// 最小切联模板：只把 <see cref="CollateSpec"/> 解算出的联矩形、字段可见性与缝位置落到像素上，
        /// 不做任何医学表单语义。绘制端全部通过 <see cref="CollateSpec.CopyRect"/> /
        /// <see cref="CollateSpec.PerforationAt"/> / <see cref="CopyDef.ShowsField"/> 取位置，
        /// 所以像素判据就是源码解算判据本身。
        /// </summary>
        private sealed class CollateTemplate : IChartTemplate
        {
            private readonly CollateSpec _spec;
            private readonly SKRect _total;
            private readonly List<string> _fields;

            public CollateTemplate(CollateSpec spec, SKRect total)
            {
                _spec = spec;
                _total = total;
                _fields = new List<string>(spec.AllFields);
            }

            public string Key => "test.collate-imposition";

            public string DisplayName => "多联次切分试版";

            public ChartGeometry Measure(ChartDocument document, ChartStyle style, IFontResolver fonts)
            {
                return new ChartGeometry(
                    _total,
                    new SlotAxis(_total.Left, 20f, 1, 1),
                    new LinearAxis("rule", 0d, 1d, _total.Top, _total.Bottom, 1d, 1d),
                    new LinearAxis[0],
                    new Band("header", _total, new BandRow[0]),
                    new Band("date", _total, new BandRow[0]),
                    _total,
                    new LabelColumn[0],
                    new Band("cells", _total, new BandRow[0]),
                    new CellRow[0],
                    new Band("footer", _total, new BandRow[0]));
            }

            public void DrawContent(Painter painter, ChartGeometry geometry, ChartDocument document)
            {
                FontKey labelFont = new FontKey(null, 12f);
                FontKey fieldFont = new FontKey(null, 11f);

                for (int i = 0; i < _spec.Count; i++)
                {
                    CopyDef copy = _spec.Copies[i];
                    SKRect rect = _spec.CopyRect(i, _total);

                    painter.Text("COPY" + i, rect.Left + TextPadX, rect.Top + LabelBaseline,
                                 labelFont, painter.Style.Colors.Text);

                    // 每个字段键在本联占一行固定行号（行号由 AllFields 顺序决定），
                    // 只有 ShowsField 为真才落墨——"串联"会在无墨断言的窗口里现形。
                    for (int r = 0; r < _fields.Count; r++)
                    {
                        string field = _fields[r];
                        if (!copy.ShowsField(field))
                        {
                            continue;
                        }
                        SKRect box = FieldBox(rect, r);
                        painter.Text(field + " 42", box.Left + 1f, box.Top + 14f, fieldFont,
                                     painter.Style.Colors.Text);
                    }
                }

                for (int b = 1; b < _spec.Count; b++)
                {
                    float? seam = _spec.PerforationAt(b, _total);
                    if (!seam.HasValue)
                    {
                        continue;
                    }
                    DrawPerforation(painter, seam.Value);
                }
            }

            private void DrawPerforation(Painter painter, float position)
            {
                bool horizontalStrip = _spec.Direction == CollateDirection.HorizontalStrip;
                if (_spec.Perforation == PerforationStyle.DashedCut)
                {
                    if (horizontalStrip)
                    {
                        painter.DashedLine(position, _total.Top, position, _total.Bottom,
                                           painter.Style.Colors.Grid, SeamStroke);
                    }
                    else
                    {
                        painter.DashedLine(_total.Left, position, _total.Right, position,
                                           painter.Style.Colors.Grid, SeamStroke);
                    }
                    return;
                }

                // DiceCut：沿缝密排刀模孔，孔位从版面起点偏移 HoleFirst，步长 HolePitch，末缘前留 HoleEndMargin。
                float from = horizontalStrip ? _total.Top : _total.Left;
                float to = horizontalStrip ? _total.Bottom : _total.Right;
                for (float p = from + HoleFirst; p < to - HoleEndMargin; p += HolePitch)
                {
                    SKPoint center = horizontalStrip ? new SKPoint(position, p) : new SKPoint(p, position);
                    painter.FillCircle(center, HoleRadius, painter.Style.Colors.Grid);
                }
            }
        }
    }
}
