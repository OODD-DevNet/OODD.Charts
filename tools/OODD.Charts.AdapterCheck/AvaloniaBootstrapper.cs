using System;
using Avalonia;
using Avalonia.Headless;

namespace OODD.Charts.AdapterCheck
{
    /// <summary>
    /// Avalonia 无窗口宿主。单独成文件是因为 <c>UseSkia()</c> 是扩展方法，必须在导入了
    /// <c>Avalonia</c> 命名空间的编译单元里才解析得到，而 Program.cs 同时导入 WPF/WinForms 的
    /// <c>Size</c>/<c>Vector</c>，两边撞名。
    /// </summary>
    internal static class AvaloniaBootstrapper
    {
        /// <summary>
        /// 起一个真 Skia 渲染后端。<c>UseHeadlessDrawing</c> 必须为 <c>false</c>：默认的 headless
        /// 绘制给的是空壳位图，写进帧缓冲的像素读不回来，会让逐字节比对假绿。
        /// </summary>
        public static void InitializeHeadless()
        {
            AppBuilder.Configure<Application>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
        }
    }
}
