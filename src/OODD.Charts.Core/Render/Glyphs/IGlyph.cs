using System;
using System.Collections.Generic;
using SkiaSharp;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 一个可复用的行业图形符号。只画几何，不画文字。
    /// </summary>
    /// <remarks>
    /// 本库要导出 PDF/SVG，用 canvas 旋转落文字会让文字退化成带变换的路径、无法检索
    /// （<see cref="Painter"/> 的逐字竖排已记录同一教训），所以符号一律不写文字：
    /// 需要文字的位置由模板用直立文本单独落位，符号只提供落位用的几何锚点。
    /// 旋转只用于几何。
    /// </remarks>
    public interface IGlyph
    {
        /// <summary>把符号画在画布上。</summary>
        /// <param name="painter">落笔者。</param>
        /// <param name="anchor">符号定位锚点；各符号的锚点语义写在自己的类型注释里。</param>
        /// <param name="size">标称尺寸（像素）；符号内部所有长度都必须是 size 的倍数，
        /// 不允许出现字面量像素，否则换令牌不生效。</param>
        /// <param name="color">主色。</param>
        void Draw(Painter painter, SKPoint anchor, float size, SKColor color);
    }

    /// <summary>
    /// 符号注册表。域程序集注册自己的符号，Core 只放跨域通用的。
    /// </summary>
    /// <remarks>
    /// 注册表是进程级的，出图可以多线程并行，因此所有读写都加锁；
    /// 键按 <see cref="StringComparer.Ordinal"/> 比较，不做大小写折叠，避免域符号被意外覆盖。
    /// </remarks>
    public static class Glyphs
    {
        /// <summary>成对连线符号键（麻醉记录单的 ∨∨—∨∨ 这一族）。</summary>
        public const string PairedEventKey = "paired-event";

        /// <summary>UN 危险货物菱形标志牌结构框架键。</summary>
        public const string DiamondPlacardKey = "diamond-placard";

        /// <summary>地面天气图风向杆键。</summary>
        public const string WindBarbKey = "wind-barb";

        /// <summary>逐字符方格框键（死亡证明的行政区划代码 / 编号栅格）。</summary>
        public const string DigitBoxKey = "digit-box";

        /// <summary>三态方框键（作业票措施确认）。</summary>
        public const string TriStateBoxKey = "tri-state-box";

        /// <summary>圆形章键（工作票"已执行/作废/未执行"）。</summary>
        public const string StampRingKey = "stamp-ring";

        private static readonly Dictionary<string, IGlyph> _registry =
            new Dictionary<string, IGlyph>(StringComparer.Ordinal);

        private static readonly object _sync = new object();

        static Glyphs()
        {
            // 内置集：一律取该族符号最中性的样式（空心圆成对、正北 10 节、6 格、未勾选、正章不倾），
            // 域程序集要用别的样式，Register 同键覆盖即可。
            Register(PairedEventKey, new PairedEventGlyph(MarkerSymbol.Ring, MarkerSymbol.Ring, true));
            Register(DiamondPlacardKey, new DiamondPlacard());
            Register(WindBarbKey, new WindBarb(0f, 10, WindBarb.BarbRule.WmoStandard));
            Register(DigitBoxKey, new DigitBox(6));
            Register(TriStateBoxKey, new TriStateBox(TriState.Unchecked));
            Register(StampRingKey, new StampRing(0f));
        }

        /// <summary>登记符号；键已存在时覆盖旧实现（允许换实现）。</summary>
        /// <param name="key">符号键，不可为空。</param>
        /// <param name="glyph">符号实现，由注册表持有引用但不负责释放。</param>
        public static void Register(string key, IGlyph glyph)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("符号键不能为空。", nameof(key));
            }
            if (glyph == null)
            {
                throw new ArgumentNullException(nameof(glyph));
            }

            lock (_sync)
            {
                _registry[key] = glyph;
            }
        }

        /// <summary>按键取符号；未注册返回 null。</summary>
        /// <param name="key">符号键。</param>
        public static IGlyph? Find(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            lock (_sync)
            {
                IGlyph? found;
                return _registry.TryGetValue(key, out found) ? found : null;
            }
        }

        /// <summary>键是否已注册。</summary>
        /// <param name="key">符号键。</param>
        public static bool IsRegistered(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_sync)
            {
                return _registry.ContainsKey(key);
            }
        }

        /// <summary>注销一个符号（测试或域程序集热替换用）；返回是否确实删掉了。</summary>
        /// <param name="key">符号键。</param>
        public static bool Unregister(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            lock (_sync)
            {
                return _registry.Remove(key);
            }
        }

        /// <summary>当前已注册的键，按 Ordinal 升序，便于断言。</summary>
        public static IReadOnlyList<string> Keys
        {
            get
            {
                lock (_sync)
                {
                    List<string> keys = new List<string>(_registry.Keys);
                    keys.Sort(StringComparer.Ordinal);
                    return keys;
                }
            }
        }

        /// <summary>已注册符号的数量。</summary>
        public static int Count
        {
            get
            {
                lock (_sync)
                {
                    return _registry.Count;
                }
            }
        }
    }
}
