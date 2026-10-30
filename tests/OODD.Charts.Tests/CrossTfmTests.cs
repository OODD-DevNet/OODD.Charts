using System;
using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// A6：同一份文档在 net462 与 net8.0 下出图字节完全相同。
    /// <para>
    /// 做法是把 PNG 的 SHA-256 钉成基准值，两个 TFM 各自跑一遍断言。
    /// 只要两边哈希相等，PNG 字节就相等（哈希相等而字节不等的概率可以忽略），
    /// 这样单条测试进程就能同时承担"跨 TFM 一致"与"版面没被无意改动"两件事。
    /// </para>
    /// </summary>
    public sealed class CrossTfmTests
    {
        /// <summary>
        /// <c>TestDocuments.Temperature()</c> 在 96dpi 下 PNG 的 SHA-256（小写）。
        /// 由 net8.0 与 net462 两轮实测取证，任一侧版面变化都会在此失败。
        /// </summary>
        private const string PngSha256 = "04D875467F76CE52D2EB9923502FCFF0BBAD30034295D18AC3102F3A561DF6A8";

        [Fact]
        public void 三测单PNG字节跨TFM一致()
        {
            string tfm = CurrentTfm;
            byte[] png = WriteEvidence(tfm);
            string hash = Sha256Hex(png);

            Assert.True(PngSha256.Length == 64 && !PngSha256.StartsWith("REPLACE"),
                "基准哈希尚未录制。当前 " + tfm + " 实测 SHA-256 = " + hash);

            Assert.True(string.Equals(hash, PngSha256, StringComparison.OrdinalIgnoreCase),
                tfm + " 出图与基准不一致：实测 " + hash + "，基准 " + PngSha256);
        }

        [Fact]
        public void 两TFM产物都在磁盘上且逐字节相同()
        {
            // 本 TFM 的产物先落盘；另一 TFM 必须已经跑过同一测试，否则没有对照物。
            WriteEvidence(CurrentTfm);
            string dir = EvidenceDir;
            string eight = Path.Combine(dir, "net8.0-temperature-96dpi.png");
            string fourSixTwo = Path.Combine(dir, "net462-temperature-96dpi.png");
            Assert.True(File.Exists(eight) && File.Exists(fourSixTwo),
                "需要两个 TFM 都跑过本测试才有对照物；缺失：" + eight + " / " + fourSixTwo);

            byte[] a = File.ReadAllBytes(eight);
            byte[] b = File.ReadAllBytes(fourSixTwo);
            Assert.True(a.Length == b.Length, "字节长度不同：" + a.Length + " vs " + b.Length);
            for (int i = 0; i < a.Length; i++)
            {
                Assert.True(a[i] == b[i], "第 " + i + " 字节起不同：" + a[i] + " vs " + b[i]);
            }
        }

        /// <summary>跑测试的运行时对应的 TFM 名。</summary>
        private static string CurrentTfm =>
            Environment.Version.Major >= 5 ? "net8.0" : "net462";

        private static string EvidenceDir => Path.Combine(TestDocuments.RepoRoot, "artifacts", "cross-tfm");

        private static byte[] WriteEvidence(string tfm)
        {
            byte[] png;
            using (Chart chart = TestDocuments.Temperature())
            {
                png = chart.Encode(ChartImageFormat.Png, 96f, 100);
            }

            string dir = EvidenceDir;
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, tfm + "-temperature-96dpi.png"), png);
            return png;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty);
            }
        }
    }
}
