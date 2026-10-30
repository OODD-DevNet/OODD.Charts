using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A5：核心库不带任何 UI 框架依赖。判据读的是 PE 元数据里的
    /// <c>AssemblyRef</c> 表（编译期真实引用），不是 csproj 文本，改注释糊不过去。
    /// </summary>
    public sealed class DependencyPurityTests
    {
        /// <summary>核心库出货的全部 TFM；每个都要查，缺哪个直接失败。</summary>
        private static readonly string[] CoreTfms = { "net462", "netstandard2.0", "net8.0" };

        /// <summary>医学库与核心库一样必须平台中立。</summary>
        private static readonly string[] MedicalTfms = { "net462", "netstandard2.0", "net8.0" };

        private static readonly string[] ForbiddenPrefixes =
        {
            "System.Drawing",
            "System.Windows.Forms",
            "System.Windows",
            "WindowsBase",
            "PresentationCore",
            "PresentationFramework",
            "Microsoft.UI",
            "Microsoft.Windows",
            "Microsoft.Maui",
            "Avalonia",
            "Eto",
            "Uno",
            "SkiaSharp.Views",
            "Mono.Android",
            "Xamarin",
        };

        [Theory]
        [InlineData("OODD.Charts.Core", "net462")]
        [InlineData("OODD.Charts.Core", "netstandard2.0")]
        [InlineData("OODD.Charts.Core", "net8.0")]
        [InlineData("OODD.Charts.Medical", "net462")]
        [InlineData("OODD.Charts.Medical", "netstandard2.0")]
        [InlineData("OODD.Charts.Medical", "net8.0")]
        public void 程序集引用表不含UI框架(string assemblyName, string tfm)
        {
            string dll = FindDll(assemblyName, tfm);
            List<string> references = AssemblyReferences(dll);

            List<string> hits = new List<string>();
            foreach (string reference in references)
            {
                foreach (string forbidden in ForbiddenPrefixes)
                {
                    if (reference.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                    {
                        hits.Add(reference);
                        break;
                    }
                }
            }

            Assert.True(hits.Count == 0,
                dll + " 引用了 UI 框架程序集：" + string.Join(", ", hits.ToArray())
                + "（全部引用：" + string.Join(", ", references.ToArray()) + "）");

            // 自检：引用表真的读到了东西，避免"空集合导致全绿"的假通过。
            Assert.True(references.Count >= 2,
                dll + " 的 AssemblyRef 只有 " + references.Count + " 项，读取方式失效");
        }

        [Fact]
        public void 判据本身能抓到禁用引用()
        {
            // 用一个确定含 UI 依赖的产物自检：适配器 DLL 必须引用对应框架，
            // 否则说明我的扫描逻辑根本不看引用表。
            string? dll = FindExistingDll("OODD.Charts.Adapters.WinForms", "net8.0-windows");
            Assert.True(dll != null, "适配器未构建，无法自检判据");
            List<string> references = AssemblyReferences(dll!);
            bool forms = references.Exists(r => r.StartsWith("System.Windows.Forms", StringComparison.OrdinalIgnoreCase)
                                                || r.Equals("System.Drawing", StringComparison.OrdinalIgnoreCase));
            Assert.True(forms, "WinForms 适配器竟然没引用 GDI+/WinForms：" + string.Join(", ", references.ToArray()));
        }

        private static string FindDll(string assemblyName, string tfm)
        {
            string project = assemblyName.EndsWith("Core", StringComparison.Ordinal)
                ? Path.Combine("src", "OODD.Charts.Core")
                : Path.Combine("src", "OODD.Charts.Medical");
            string root = Path.Combine(TestDocuments.RepoRoot, project, "bin");

            foreach (string configuration in new[] { "Debug", "Release" })
            {
                string candidate = Path.Combine(root, configuration, tfm, assemblyName + ".dll");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException(
                assemblyName + " 在 " + tfm + " 下没有构建产物。请先构建全解决方案："
                + "dotnet build OODD.Charts.slnx（查找路径 " + root + "）");
        }

        private static string? FindExistingDll(string assemblyName, string tfm)
        {
            string root = Path.Combine(TestDocuments.RepoRoot, "src", "Adapters");
            if (!Directory.Exists(root))
            {
                return null;
            }

            foreach (string project in Directory.GetDirectories(root))
            {
                foreach (string configuration in new[] { "Debug", "Release" })
                {
                    string candidate = Path.Combine(project, "bin", configuration, tfm, assemblyName + ".dll");
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        private static List<string> AssemblyReferences(string path)
        {
            List<string> names = new List<string>();
            using (FileStream stream = File.OpenRead(path))
            using (PEReader reader = new PEReader(stream))
            {
                MetadataReader metadata = reader.GetMetadataReader();
                foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
                {
                    names.Add(metadata.GetAssemblyReference(handle).GetAssemblyName().Name ?? string.Empty);
                }
            }
            return names;
        }
    }
}
