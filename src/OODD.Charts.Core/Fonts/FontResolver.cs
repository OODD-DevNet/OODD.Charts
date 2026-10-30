using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace OODD.Charts.Fonts
{
    /// <summary>字体请求键。值语义，可作字典键。</summary>
    public readonly struct FontKey : IEquatable<FontKey>
    {
        public FontKey(string? family, float size, bool bold = false)
        {
            Family = family;
            Size = size;
            Bold = bold;
        }

        /// <summary>首选字族；null 表示由回退链决定。</summary>
        public string? Family { get; }

        /// <summary>字号（逻辑像素，非 pt）。</summary>
        public float Size { get; }

        public bool Bold { get; }

        public bool Equals(FontKey other)
        {
            return string.Equals(Family, other.Family, StringComparison.Ordinal)
                && Size == other.Size
                && Bold == other.Bold;
        }

        public override bool Equals(object? obj)
        {
            return obj is FontKey && Equals((FontKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int h = Family != null ? Family.GetHashCode() : 0;
                h = (h * 397) ^ Size.GetHashCode();
                h = (h * 397) ^ Bold.GetHashCode();
                return h;
            }
        }

        public override string ToString()
        {
            return (Family ?? "<auto>") + " " + Size + (Bold ? " bold" : string.Empty);
        }
    }

    /// <summary>
    /// 把 <see cref="FontKey"/> 解析成 <see cref="SKTypeface"/>。
    /// 解析结果由 resolver 持有并缓存，<b>调用方不得 Dispose</b>。
    /// </summary>
    public interface IFontResolver : IDisposable
    {
        SKTypeface Resolve(FontKey key);
    }

    /// <summary>可嵌入的字体流集合（Linux / Android 无中文字体时使用）。</summary>
    public sealed class FontCollection
    {
        private readonly List<Stream> _streams = new List<Stream>();
        private readonly List<SKTypeface> _loaded = new List<SKTypeface>();

        /// <summary>登记一个字体流；由集合负责释放。</summary>
        public void Add(Stream fontStream)
        {
            if (fontStream == null)
            {
                throw new ArgumentNullException(nameof(fontStream));
            }
            _streams.Add(fontStream);
            _loaded.Add(SKTypeface.FromStream(fontStream));
        }

        internal IReadOnlyList<SKTypeface> Loaded => _loaded;

        public void Dispose()
        {
            foreach (SKTypeface typeface in _loaded)
            {
                typeface.Dispose();
            }
            _loaded.Clear();
            foreach (Stream stream in _streams)
            {
                stream.Dispose();
            }
            _streams.Clear();
        }
    }

    /// <summary>
    /// 默认解析器：四级回退链
    /// ① 指定字族 → ② CJK 候选表 → ③ 按字符匹配系统字体 → ④ SKTypeface.Default。
    /// 保证任何平台上都不会返回 null，也避免因缺字体而画豆腐块。
    /// </summary>
    public sealed class SystemFontResolver : IFontResolver
    {
        /// <summary>回退候选字族，覆盖 Windows / macOS / Linux / Android 常见中文字体。</summary>
        public static readonly string[] FallbackFamilies =
        {
            "Microsoft YaHei",
            "SimSun",
            "PingFang SC",
            "Hiragino Sans GB",
            "Noto Sans CJK SC",
            "Noto Sans SC",
            "Source Han Sans SC",
            "WenQuanYi Micro Hei",
            "Droid Sans Fallback",
            "Segoe UI",
            "Roboto",
            "Helvetica Neue",
            "Arial",
        };

        /// <summary>用于探测 CJK 覆盖能力的样例字。</summary>
        public const char ProbeCharacter = '体';

        private readonly Dictionary<FontKey, SKTypeface> _cache = new Dictionary<FontKey, SKTypeface>();
        private readonly object _cacheSync = new object();
        private readonly FontCollection? _embedded;
        private bool _disposed;

        public SystemFontResolver()
        {
        }

        public SystemFontResolver(FontCollection embedded)
        {
            _embedded = embedded;
        }

        /// <summary>可被外部替换的候选表；null 用内置 <see cref="FallbackFamilies"/>。</summary>
        public string[]? PreferredFamilies { get; set; }

        public SKTypeface Resolve(FontKey key)
        {
            // 解析器实例是进程级共享的（Chart.SharedFonts），多线程同时出图是正常用法，
            // 因此缓存必须加锁：无锁 Dictionary 在并发写入时会损坏内部状态并抛
            // InvalidOperationException（实测由并行测试触发）。
            lock (_cacheSync)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(SystemFontResolver));
                }

                SKTypeface? cached;
                if (_cache.TryGetValue(key, out cached))
                {
                    return cached;
                }

                SKTypeface resolved = Create(key);
                _cache[key] = resolved;
                return resolved;
            }
        }

        private SKTypeface Create(FontKey key)
        {
            SKFontStyle style = key.Bold ? SKFontStyle.Bold : SKFontStyle.Normal;

            if (key.Family != null)
            {
                SKTypeface? exact = TryFamily(key.Family, style);
                if (exact != null)
                {
                    return exact;
                }
            }

            string[] candidates = key.Family == null
                ? (PreferredFamilies ?? FallbackFamilies)
                : Combine(key.Family, PreferredFamilies ?? FallbackFamilies);

            foreach (string family in candidates)
            {
                SKTypeface? match = TryFamily(family, style);
                if (match != null)
                {
                    return match;
                }
            }

            // 按字符匹配：这是 CJK 最可靠的兜底，绕开字体族名差异。
            using (SKFontManager manager = SKFontManager.Default)
            {
                SKTypeface? byChar = manager.MatchCharacter(ProbeCharacter);
                if (byChar != null)
                {
                    return byChar;
                }
            }

            return SKTypeface.Default;
        }

        private SKTypeface? TryFamily(string family, SKFontStyle style)
        {
            SKTypeface? typeface = SKTypeface.FromFamilyName(family, style);
            if (typeface != null && string.Equals(typeface.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            {
                return typeface;
            }
            if (typeface != null)
            {
                typeface.Dispose();
            }
            return null;
        }

        private static string[] Combine(string first, string[] rest)
        {
            string[] result = new string[rest.Length + 1];
            result[0] = first;
            Array.Copy(rest, 0, result, 1, rest.Length);
            return result;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _cache.Clear();
            if (_embedded != null)
            {
                _embedded.Dispose();
            }
        }
    }
}
