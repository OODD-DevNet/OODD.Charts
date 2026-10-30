using System;
using System.Collections.Generic;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 崩溃回归（T68）：SkiaSharp 3.116.1 上 <c>SKImage.FromPicture</c> 会抛不可捕获的
    /// AccessViolationException，进程直接没了，任何 try/catch 都拦不住。
    /// 因此这条禁令必须被自动化盯住——注释里可以提到它（解释为什么不用），代码里不许出现。
    /// </summary>
    public sealed class ForbiddenApiTests
    {
        private static readonly string[] Forbidden =
        {
            "SKImage.FromPicture",
        };

        [Fact]
        public void 源码剥掉注释后不含禁用API()
        {
            List<string> hits = new List<string>();
            foreach (string file in SourceScan.FilesUnder("src"))
            {
                foreach (string hit in SourceScan.Hits(SourceScan.CodeOf(file), Forbidden))
                {
                    hits.Add(file + " → " + hit);
                }
            }
            Assert.True(hits.Count == 0, "出现禁用 API：" + string.Join("; ", hits.ToArray()));
        }

        [Fact]
        public void 扫描器能穿透字符串与注释()
        {
            // 自检：如果扫描器只是"整文件 grep"，注释里的说明文字会让上面那条永远红；
            // 如果它连真代码都看不见，这条会永远绿。两个方向都要钉住。
            string[] lines =
            {
                "// 不要用 SKImage.FromPicture，见 docs/02",
                "/* 块注释里也提一次 SKImage.FromPicture */",
                "string why = \"注释掉的写法：SKImage.FromPicture\";",
                "SKImage image = SKImage.FromPicture(picture, size); // 真代码",
            };
            string source = string.Join("\n", lines);
            List<string> hits = SourceScan.Hits(SourceScan.CodeOnly(source), Forbidden);

            // 两处注释里的提及要消失；字符串与真代码里的两处都要留下——
            // 字面量里出现禁用写法通常意味着有人在拼代码，宁可多抓。
            Assert.True(hits.Count == 2, "只该抓到字符串与真代码各一次：" + string.Join(" | ", hits.ToArray()));
            Assert.True(hits[0].IndexOf("第 3 行", StringComparison.Ordinal) >= 0
                        && hits[1].IndexOf("第 4 行", StringComparison.Ordinal) >= 0,
                "命中行号不对：" + string.Join(" | ", hits.ToArray()));

            // 反向：把两行真代码也注释掉后必须零命中，注释里再多次提及都不算。
            string commentsOnly = string.Join("\n", new[]
            {
                lines[0],
                lines[1],
                "// string why = \"注释掉的写法：SKImage.FromPicture\";",
                "// SKImage image = SKImage.FromPicture(picture, size); // 真代码",
            });
            Assert.True(SourceScan.Hits(SourceScan.CodeOnly(commentsOnly), Forbidden).Count == 0, "注释里的提及被误判成代码");
        }
    }
}
