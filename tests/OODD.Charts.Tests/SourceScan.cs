using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 源码级判据的共用扫描器：先剥掉注释再匹配，这样"注释里解释为什么不能用"
    /// 不会被误判成违规，而真代码里的调用一个都跑不掉。
    /// </summary>
    internal static class SourceScan
    {
        /// <summary>仓库 <c>src</c> 目录下的全部 <c>.cs</c> 文件（跳过 bin/obj）。</summary>
        public static List<string> FilesUnder(string subPath)
        {
            string root = Path.Combine(TestDocuments.RepoRoot, subPath);
            Assert.True(Directory.Exists(root), "找不到源码目录：" + root);

            List<string> files = new List<string>();
            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)
                    || file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                {
                    continue;
                }
                files.Add(file);
            }
            Assert.True(files.Count > 0, subPath + " 下没扫到任何 .cs，路径判断有误");
            return files;
        }

        /// <summary>返回 code 中每个 needle 的命中位置（"needle (第 N 行)"）。</summary>
        public static List<string> Hits(string code, IEnumerable<string> needles)
        {
            List<string> hits = new List<string>();
            foreach (string needle in needles)
            {
                int from = 0;
                while (true)
                {
                    int index = code.IndexOf(needle, from, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        break;
                    }
                    int line = code.Substring(0, index).Split('\n').Length;
                    hits.Add(needle + " (第 " + line + " 行)");
                    from = index + needle.Length;
                }
            }
            return hits;
        }

        /// <summary>
        /// 剥掉 <c>//</c> 行注释与 <c>/* */</c> 块注释；字符串与字符字面量原样保留
        /// （字符串里出现被禁的标识符同样是真调用风险，不放过）。
        /// </summary>
        public static string CodeOnly(string source)
        {
            StringBuilder code = new StringBuilder(source.Length);
            bool inLineComment = false;
            bool inBlockComment = false;
            bool inString = false;
            bool inChar = false;
            bool verbatim = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char next = i + 1 < source.Length ? source[i + 1] : '\0';

                if (inLineComment)
                {
                    if (c == '\n')
                    {
                        inLineComment = false;
                        code.Append(c);
                    }
                    continue;
                }

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    continue;
                }

                if (!inString && !inChar)
                {
                    if (c == '/' && next == '/')
                    {
                        inLineComment = true;
                        i++;
                        continue;
                    }
                    if (c == '/' && next == '*')
                    {
                        inBlockComment = true;
                        i++;
                        continue;
                    }
                }

                if (inString)
                {
                    code.Append(c);
                    if (verbatim && c == '"' && next == '"')
                    {
                        code.Append(next);
                        i++;
                    }
                    else if (!verbatim && c == '\\')
                    {
                        code.Append(next);
                        i++;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                        verbatim = false;
                    }
                    continue;
                }

                if (inChar)
                {
                    code.Append(c);
                    if (c == '\\')
                    {
                        code.Append(next);
                        i++;
                    }
                    else if (c == '\'')
                    {
                        inChar = false;
                    }
                    continue;
                }

                code.Append(c);
                if (c == '@' && next == '"')
                {
                    code.Append(next);
                    i++;
                    inString = true;
                    verbatim = true;
                }
                else if (c == '"')
                {
                    inString = true;
                }
                else if (c == '\'')
                {
                    inChar = true;
                }
            }
            return code.ToString();
        }

        /// <summary>读文件的剥注释内容。</summary>
        public static string CodeOf(string file)
        {
            return CodeOnly(File.ReadAllText(file, Encoding.UTF8));
        }
    }
}
