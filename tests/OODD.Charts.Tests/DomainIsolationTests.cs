using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A19：域程序集互相隔离（docs/06 §2.3、§4 的"新域不互引"）。
    /// <para>
    /// 判据读 <c>csproj</c> 原文而不是内存里的引用图：隔离规则是工程层的，
    /// 只有 csproj 才是它的唯一真相；一条 <c>ProjectReference</c> 加错就再也拆不开。
    /// </para>
    /// </summary>
    public sealed class DomainIsolationTests
    {
        /// <summary>存量域同样受"不许互引"约束，所以五个域一起查。</summary>
        private static readonly string[] AllDomainProjects =
        {
            "OODD.Charts.Medical",
            "OODD.Charts.Radar",
            "OODD.Charts.Engineering",
            "OODD.Charts.Transit",
            "OODD.Charts.Geoscience",
            "OODD.Charts.Tickets",
        };

        /// <summary>便携矩阵（docs/02 §9.1）：域程序集必须与 Core 同档。</summary>
        private const string PortableTfms = "net462;netstandard2.0;net8.0";

        [Theory]
        [InlineData("OODD.Charts.Engineering")]
        [InlineData("OODD.Charts.Transit")]
        [InlineData("OODD.Charts.Geoscience")]
        [InlineData("OODD.Charts.Tickets")]
        public void 域工程只引用Core且TFM为便携矩阵(string project)
        {
            string csproj = CsprojOf(project);
            string text = File.ReadAllText(csproj);

            Match tfm = Regex.Match(text, "<TargetFrameworks>([^<]*)</TargetFrameworks>");
            Assert.True(tfm.Success, project + " 没有 <TargetFrameworks>，三档便携矩阵是硬要求");
            Assert.True(tfm.Groups[1].Value == PortableTfms,
                        project + " 的 TFM 是 " + tfm.Groups[1].Value + "，应为 " + PortableTfms);

            List<string> references = ProjectReferences(text);
            Assert.True(references.Count == 1,
                        project + " 只允许引用 Core，实际引用了 " + references.Count + " 个：" + Join(references));
            Assert.True(references[0] == "OODD.Charts.Core",
                        project + " 引用的不是 Core，而是 " + references[0]);
        }

        [Fact]
        public void 域工程之间不存在任何方向的互相引用()
        {
            // 交叉引用一旦形成就再也拆不开（A19 存在的理由），所以每个域单独查一遍。
            List<string> violations = new List<string>();
            foreach (string project in AllDomainProjects)
            {
                List<string> references = ProjectReferences(CsprojOf(project));
                foreach (string reference in references)
                {
                    if (reference == "OODD.Charts.Core")
                    {
                        continue;
                    }
                    if (Array.IndexOf(AllDomainProjects, reference) >= 0)
                    {
                        violations.Add(project + " → " + reference);
                    }
                }
            }

            Assert.True(violations.Count == 0,
                        "域程序集互相引用：" + Join(violations)
                        + "。共享的原语必须下沉 Core（判据见 docs/06 §2.3：≥2 个域使用才进 Core），"
                        + "否则就留在自己的域里。");
        }

        [Theory]
        [InlineData("OODD.Charts.Engineering")]
        [InlineData("OODD.Charts.Transit")]
        [InlineData("OODD.Charts.Geoscience")]
        [InlineData("OODD.Charts.Tickets")]
        public void 域工程有自己的样式与配色令牌文件(string project)
        {
            string directory = Path.Combine(TestDocuments.RepoRoot, "src", project);
            Assert.True(Directory.Exists(directory), "域工程目录不存在：" + directory);

            string[] sources = Directory.GetFiles(directory, "*.cs");
            List<string> styleLike = new List<string>();
            foreach (string file in sources)
            {
                string text = File.ReadAllText(file);
                if (text.Contains("Palette") && text.Contains("Style"))
                {
                    styleLike.Add(Path.GetFileName(file));
                }
            }
            Assert.True(styleLike.Count > 0, project + " 没有同时含 Style 与 Palette 的令牌文件（T231）");
        }

        /// <summary>解析 csproj 里全部 <c>ProjectReference</c> 的相对路径末段（工程名）。</summary>
        private static List<string> ProjectReferences(string csprojText)
        {
            List<string> names = new List<string>();
            MatchCollection matches = Regex.Matches(csprojText,
                "<ProjectReference\\s+Include=\"([^\"]+)\"");
            foreach (Match match in matches)
            {
                string include = match.Groups[1].Value.Replace('\\', '/');
                string name = System.IO.Path.GetFileNameWithoutExtension(include);
                names.Add(name);
            }
            return names;
        }

        private static string CsprojOf(string project)
        {
            string path = Path.Combine(TestDocuments.RepoRoot, "src", project, project + ".csproj");
            Assert.True(File.Exists(path), "找不到工程文件：" + path);
            return path;
        }

        private static string Join(List<string> items)
        {
            return string.Join("、", items.ToArray());
        }
    }
}
