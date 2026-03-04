using Avalonia;
using System;
using System.Threading;
using Velopack;

namespace Storyboard;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            // 配置 Velopack 更新钩子
            VelopackApp.Build()
                .WithFirstRun(v => {
                    // 首次运行时的处理
                    Console.WriteLine($"First run with version {v}");
                })
                .WithAfterUpdateFastCallback(v => {
                    // 更新后的快速回调（在应用启动前）
                    Console.WriteLine($"Updated to version {v}");
                    // 给系统一点时间完成文件操作
                    Thread.Sleep(500);
                })
                .Run();
        }
        catch (Exception ex)
        {
            // 如果 Velopack 初始化失败（例如开发环境），继续运行应用
            Console.WriteLine($"Velopack initialization skipped: {ex.Message}");
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
