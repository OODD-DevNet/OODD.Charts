using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OODD.Charts.Medical.Temperature;
using SkiaSharp;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 需要独占进程的集合。Skia 的字形/字面缓存是进程级共享的，别的测试类同时在写字时，
    /// "SVG 导出丢没丢字"的测量会被环境负载污染——库的闸门只罩得住经过本库的落字，
    /// 测试里那些直接拿 <c>Painter</c>/<c>SKCanvas</c> 写字的用例属于"宿主自己写"这条管不到的路。
    /// 因此把「断言 SVG 文字」和「直接写字」两组用例都放进本集合：
    /// ExportTests / GlyphTests / RadarTests / SpecialtyFormsTests / SvgTextIntegrityTests（断言方）
    /// 与 DimensionTests / LineFamilyTests / PainterPrimitiveTests（写字方）。
    /// xUnit 保证标了 <c>DisableParallelization</c> 的集合不与任何其它集合并行，集合内部也只串行跑。
    /// </summary>
    [CollectionDefinition(SerialSkiaCollection.Name, DisableParallelization = true)]
    public sealed class SerialSkiaCollection
    {
        public const string Name = "serial-skia";
    }

    /// <summary>
    /// SVG 文字完整性：SkiaSharp 3.116.1 的 SVG 文本序列化不是线程安全的，
    /// 库用 <c>TextRenderGate</c>（出图读锁并发 / 导出写锁独占）把它按住。
    /// 这些用例断言的是"并发下产物仍含全部文字"，关掉机制就会红（对照记录见各用例注释）。
    /// </summary>
    [Collection(SerialSkiaCollection.Name)]
    public sealed class SvgTextIntegrityTests
    {
        [Fact]
        public void 并发导出SVG时每一条产物的文字都不丢()
        {
            // 对照实测（探针，200 次/档）：去掉独占后单线程 0 例、2 线程 190 例、8 线程 197 例。
            string baseline = ConcatText(ChartExport.ToSvgString(TestDocuments.Temperature()));
            Assert.True(baseline.Length > 40, "基线里可检索的文字太少，用例没有区分度：" + Head(baseline));

            List<string> mismatches = new List<string>();
            object gate = new object();
            Parallel.For(0, 6, (int lane) =>
            {
                for (int i = 0; i < 15; i++)
                {
                    using (Chart chart = TestDocuments.Temperature())
                    {
                        string text = ConcatText(ChartExport.ToSvgString(chart));
                        if (text != baseline)
                        {
                            lock (gate)
                            {
                                mismatches.Add("丢字后：" + Head(text) + "（基线长 " + baseline.Length + "）");
                            }
                        }
                    }
                }
            });

            Assert.True(mismatches.Count == 0,
                        "并发导出把文字弄丢了的产物共 " + mismatches.Count + " 例；首例：" +
                        (mismatches.Count > 0 ? mismatches[0] : "无"));
        }

        [Fact]
        public void 别的线程在出图时SVG导出的文字节点仍不空()
        {
            // 机制：SVG 导出用 TextRenderGate 的写锁独占落字——出图之间照旧并发（读锁），
            // 但导出必须等它们排空。对照实验：把 EnterRender 注释掉（只留写锁互斥导出之间、
            // 加上"认出空节点再重绘"），本用例这份大表单一次导出几百个文字节点，
            // 连跑三轮丢字 19 / 21 / 23 例；闸门在位时连跑五轮 0 例——红的是闸门，不是量不够。
            // 探针还证明：并发道换成 Arial（不同字面）或只画线不画字都一例不丢，
            // 而让导出道/出图道各自 new 一个 SystemFontResolver 照丢——共享的是 Skia 原生字面缓存。
            const int svgCount = 120;
            const int rasterLanes = 8;
            const int perRasterLane = 25;

            List<string> corrupted = new List<string>();
            object gate = new object();
            Parallel.Invoke(
                new ParallelOptions { MaxDegreeOfParallelism = rasterLanes + 1 },
                () =>
                {
                    for (int i = 0; i < svgCount; i++)
                    {
                        using (Chart chart = TestDocuments.Temperature())
                        {
                            string svg = ChartExport.ToSvgString(chart);
                            if (HasEmptyTextNode(svg))
                            {
                                lock (gate) corrupted.Add("第 " + i + " 次导出含空 <text> 节点");
                            }
                        }
                    }
                },
                () =>
                {
                    Parallel.For(0, rasterLanes, (int lane) =>
                    {
                        for (int i = 0; i < perRasterLane; i++)
                        {
                            using (Chart chart = TestDocuments.Temperature())
                            {
                                chart.Encode(ChartImageFormat.Png, 96f, 100);
                            }
                        }
                    });
                });

            Assert.True(corrupted.Count == 0,
                        "并发出图时导出仍丢字 " + corrupted.Count + " 例；首例：" +
                        (corrupted.Count > 0 ? corrupted[0] : "无"));
        }

        [Fact]
        public void 空文本节点检测器认得出丢字也认得出正常产物()
        {
            // 正对照：上一条断言"没有空节点"，若检测器恒判"没有"，那条断言就是零墨。
            MethodInfo? detector = typeof(ChartExport).GetMethod(
                "HasEmptyTextNode", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.True(detector != null, "ChartExport 里的空节点检测器改名/删除了，用例要跟着实现走");
            Func<byte[], bool> hasEmpty = (Func<byte[], bool>)Delegate.CreateDelegate(
                typeof(Func<byte[], bool>), detector!);

            Assert.True(hasEmpty(Encoding.UTF8.GetBytes("<svg><text x=\"1\" y=\"2\"></text><text>a</text></svg>")),
                        "检测器认不出人造的空 <text> 节点 → 重绘形同虚设");
            Assert.False(hasEmpty(Encoding.UTF8.GetBytes("<svg><text x=\"1\" y=\"2\">体 39.5</text></svg>")),
                         "正常拉丁+数字产物被判成丢字");
            Assert.False(hasEmpty(Encoding.UTF8.GetBytes("<svg><text>中文多字节内容</text></svg>")),
                         "含多字节字符的正常产物被判成丢字（按字节匹配不该命中续字节）");
            Assert.False(hasEmpty(Encoding.UTF8.GetBytes("<svg><rect/></svg>")),
                         "没有文本节点的产物被判成丢字");

            using (Chart chart = TestDocuments.Temperature())
            {
                Assert.False(hasEmpty(Encoding.UTF8.GetBytes(ChartExport.ToSvgString(chart))),
                             "真实导出被判成丢字，重绘会对每张图空转");
            }
        }

        /// <summary>产物里是否存在内容空的 <c>&lt;text&gt;</c> 节点（Skia 丢字的实测形态）。</summary>
        private static bool HasEmptyTextNode(string svg)
        {
            return Regex.IsMatch(svg, "<text[^>]*>\\s*</text>");
        }

        /// <summary>把 SVG 里所有 <c>&lt;text&gt;</c> 节点内容按出现顺序拼起来。</summary>
        private static string ConcatText(string svg)
        {
            MatchCollection matches = new Regex("<text[^>]*?>([^<]*?)</text>").Matches(svg);
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < matches.Count; i++)
            {
                text.Append(matches[i].Groups[1].Value.Trim());
            }
            return text.ToString();
        }

        private static string Head(string text)
        {
            return text.Substring(0, Math.Min(40, text.Length));
        }
    }
}
