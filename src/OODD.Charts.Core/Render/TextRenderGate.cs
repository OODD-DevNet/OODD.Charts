using System.Threading;

namespace OODD.Charts.Render
{
    /// <summary>
    /// 进程级"文字栅格化"闸门。出图之间互不阻塞（读锁并发），SVG 导出独占（写锁）。
    /// <para>
    /// 存在的理由只有一个：SkiaSharp 3.116.1 的字形→码位反查在原生层是共享的，
    /// 别的线程同时在栅格化<b>同一字面</b>的文字时，SVG 产物里会写出内容空的
    /// <c>&lt;text&gt;</c> 节点。实测与处置见 ChartExport.WriteSvg 的注释。
    /// </para>
    /// <para>
    /// 闸门包住的是"经过本库的录制 + 回放"整段。宿主自己拿 <c>SKCanvas</c> 直接写字、
    /// 或把 <c>Chart.Record()</c> 的产物在别处回放，本闸门管不到，那一类并发需要宿主自己串行。
    /// 用法约束：读锁不可嵌套（<c>ReaderWriterLockSlim</c> 默认 NoRecursion），
    /// 所以只有组合入口（Draw / RenderImage / ChartRaster.Render / 导出）上锁，
    /// 内部的 <c>Chart.Record</c> 一律不上锁。
    /// </para>
    /// </summary>
    internal static class TextRenderGate
    {
        private static readonly ReaderWriterLockSlim Lock = new ReaderWriterLockSlim();

        /// <summary>进入一次落字（录制或回放）。多线程可同时在读锁内。</summary>
        public static void EnterRender()
        {
            Lock.EnterReadLock();
        }

        public static void ExitRender()
        {
            Lock.ExitReadLock();
        }

        /// <summary>独占进入 SVG 导出：等待所有在飞的落字结束，并挡住新的落字。</summary>
        public static void EnterExport()
        {
            Lock.EnterWriteLock();
        }

        public static void ExitExport()
        {
            Lock.ExitWriteLock();
        }
    }
}
