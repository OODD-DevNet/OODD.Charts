using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 并发出图回归：字体解析器实例是进程级共享的（<see cref="Chart.SharedFonts"/>），
    /// 两个窗口同时刷新就会并发写它的字体缓存字典。
    /// 无锁 Dictionary 在这种场景下会损坏内部状态并抛
    /// <c>InvalidOperationException</c>——本仓库的并行测试实测触发过，故钉成判据。
    /// </summary>
    public sealed class ThreadSafetyTests
    {
        private const int Threads = 8;
        private const int Iterations = 6;

        [Fact]
        public async Task 多线程同时出图不炸且字节一致()
        {
            byte[] temperatureReference = Render(false);
            byte[] partogramReference = Render(true);

            List<Task<byte[]>> jobs = new List<Task<byte[]>>();
            for (int i = 0; i < Threads; i++)
            {
                bool partogram = i % 2 == 0;
                jobs.Add(Task.Run(() =>
                {
                    byte[] last = Array.Empty<byte>();
                    for (int round = 0; round < Iterations; round++)
                    {
                        last = Render(partogram);
                    }
                    return last;
                }));
            }

            byte[][] results;
            try
            {
                results = await Task.WhenAll(jobs);
            }
            catch (AggregateException ex)
            {
                Exception inner = ex.GetBaseException();
                Assert.Fail("并发出图抛异常：" + inner.GetType().Name + " — " + inner.Message);
                return;
            }

            for (int i = 0; i < results.Length; i++)
            {
                byte[] baseline = i % 2 == 0 ? partogramReference : temperatureReference;
                Assert.True(SameBytes(baseline, results[i]),
                    "第 " + i + " 个线程的产物与串行基准不一致");
            }
        }

        [Fact]
        public void 并发判据本身抓得到无锁字典()
        {
            // 反向自检：如果 Threads 或 Iterations 被改成 1，上面那条就永远绿。
            Assert.True(Threads >= 4 && Iterations >= 4, "并发度太低，判据失去意义");
        }

        private static byte[] Render(bool partogram)
        {
            using (Chart chart = partogram ? TestDocuments.Partogram() : TestDocuments.Temperature())
            {
                return chart.Encode(ChartImageFormat.Png, 96f, 100);
            }
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
