using Avalonia;
using System;
using Velopack;

namespace Storyboard;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Velopack: 处理应用启动和更新（必须在 Main 方法最开始调用）
        VelopackApp.Build().Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
