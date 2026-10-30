using System;
using System.Collections.Generic;
using System.Linq;
using OODD.Charts.Demo;
using OODD.Charts.Style;
using Xunit;

namespace OODD.Charts.Tests
{
    /// <summary>
    /// 样例登记的接线验收：目录规模、名称唯一，以及五个新域各自的 <c>Verify</c> 分发确实按名字路由。
    /// 两头夹住——陌生单据必须报错或报失败、陌生名字必须返回 0——
    /// 否则 switch 漏项会被 <c>default: return 0</c> 静默吞掉，样例"验证通过"只是没跑。
    /// </summary>
    public sealed class SampleCatalogTests
    {
        private const int BaseSampleCount = 23;

        private static IEnumerable<DemoSample> NewSamples =>
            EngineeringSamples.All
                .Concat(TransitSamples.All)
                .Concat(GeoscienceSamples.All)
                .Concat(NewMedicalSamples.All)
                .Concat(TicketSamples.All);

        [Fact]
        public void 目录_五域新样例全部登记且名称唯一()
        {
            List<DemoSample> all = DemoCatalog.All.ToList();
            int newCount = NewSamples.Count();
            Assert.True(newCount == 17, "新域样例应为 17 张，实到 " + newCount);
            Assert.True(all.Count == BaseSampleCount + newCount,
                "目录总数应为 " + (BaseSampleCount + newCount) + "，实到 " + all.Count);

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            foreach (DemoSample sample in all)
            {
                Assert.True(names.Add(sample.Name), "样例名重复：" + sample.Name);
            }
            foreach (DemoSample sample in NewSamples)
            {
                Assert.NotNull(DemoCatalog.Find(sample.Name));
            }
        }

        [Fact]
        public void 样例冒烟验证_正确单据零失败且分发表真的按名字路由()
        {
            using (Chart foreign = DemoCatalog.All[0].Create())
            {
                foreach (DemoSample sample in NewSamples)
                {
                    using (Chart chart = sample.Create())
                    {
                        int own = Verify(sample.Name, chart);
                        Assert.True(own == 0, sample.Name + " 自身冒烟验证失败 " + own + " 条");
                    }

                    int failures;
                    try
                    {
                        failures = Verify(sample.Name, foreign);
                    }
                    catch (InvalidCastException)
                    {
                        continue;
                    }
                    Assert.True(failures > 0,
                        sample.Name + " 用陌生单据验证仍返回 0：分发可能落进了 default");
                }

                Assert.True(Verify("no-such-sample", foreign) == 0, "未知名称必须返回 0");
            }
        }

        private static int Verify(string name, Chart chart)
        {
            return EngineeringSamples.Verify(name, chart)
                   + TransitSamples.Verify(name, chart)
                   + GeoscienceSamples.Verify(name, chart)
                   + NewMedicalSamples.Verify(name, chart)
                   + TicketSamples.Verify(name, chart);
        }
    }
}
