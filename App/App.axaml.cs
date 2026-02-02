using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using Storyboard.ViewModels;
using Storyboard.Views;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Configuration;
using Storyboard.Infrastructure.DependencyInjection;
using Storyboard.Infrastructure.Media;
using Storyboard.Infrastructure.Media.Providers;
using Storyboard.Infrastructure.Services;
using Storyboard.Infrastructure.Ui;
using System.IO;
using System;
using CommunityToolkit.Mvvm.Messaging;
using Velopack;
using StoryboardUpdateOptions = Storyboard.Infrastructure.Configuration.UpdateOptions;

namespace Storyboard;

public partial class App : Avalonia.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void OnFrameworkInitializationCompleted()
    {
        _ = MigrateProviderSchemaIfNeededAsync(); // Run asynchronously; do not block startup.

        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // 异步执行数据库迁移，不阻塞启动
        _ = ApplyDatabaseMigrationsAsync();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Exit += (_, __) => Log.CloseAndFlush();

            var mainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };

            mainWindow.Show();
            mainWindow.Activate();
            desktop.MainWindow = mainWindow;

            // 不再自动检查更新，用户可以在设置中手动检查
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async System.Threading.Tasks.Task MigrateProviderSchemaIfNeededAsync()
    {
        try
        {
            var loggerFactory = LoggerFactory.Create(builder => { });
            var stateLogger = loggerFactory.CreateLogger<ProviderStateStore>();
            var migratorLogger = loggerFactory.CreateLogger<ProviderSchemaMigrator>();

            var stateStore = new ProviderStateStore(stateLogger);
            var migrator = new ProviderSchemaMigrator(migratorLogger, stateStore);

            await System.Threading.Tasks.Task.Run(() => migrator.MigrateIfNeeded());
        }
        catch
        {
            // Migration failure should not block startup.
        }
    }

    private async System.Threading.Tasks.Task ApplyDatabaseMigrationsAsync()
    {
        try
        {
            using var scope = Services.CreateScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<Infrastructure.Persistence.StoryboardDbContext>>();
            var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<App>>();

            await using var context = await contextFactory.CreateDbContextAsync();
            await Infrastructure.Migrations.DatabaseMigrationHelper.ApplyIncrementalMigrationsAsync(context, logger);

            Log.Information("数据库迁移成功完成");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "数据库迁移失败");
            // 不抛出异常，允许应用继续启动
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // ========== V2 配置架构 ==========

        // 1. 应用配置（只读，Logging/Update）
        var appConfigPath = ConfigurationPaths.AppConfigPath;
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appConfigPath, optional: true, reloadOnChange: true)  // optional: true 防止文件不存在时崩溃
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<StoryboardUpdateOptions>(configuration.GetSection("Update"));

        // 2. 用户设置存储器
        services.AddSingleton<UserSettingsStore>();
        services.AddSingleton<UserAIOverridesStore>();
        services.AddSingleton<ProviderStateStore>();

        // 3. AI 配置合成器
        services.AddSingleton<AIConfigurationComposer>();

        // 3.1. 提供 AIServicesConfiguration（通过合成器动态获取）
        services.AddSingleton<IOptionsMonitor<AIServicesConfiguration>>(sp =>
        {
            var composer = sp.GetRequiredService<AIConfigurationComposer>();
            return new SimpleOptionsMonitor<AIServicesConfiguration>(composer);
        });

        // 4. 加载用户设置
        var userSettingsStore = new UserSettingsStore();
        var userSettings = userSettingsStore.Load();
        services.AddSingleton(userSettingsStore);
        services.AddSingleton(userSettings);

        // 5. 从用户设置中提取 StorageOptions（兼容旧代码）
        var storageOptions = new StorageOptions
        {
            DataDirectory = userSettings.Storage.DataDirectory,
            OutputDirectory = userSettings.Storage.OutputDirectory,
            UseCustomLocation = userSettings.Storage.UseCustomLocation
        };
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(storageOptions));

        // Storage Path Service - 统一管理数据存储路径
        services.AddSingleton<StoragePathService>();

        // Messenger for ViewModel communication
        services.AddSingleton<IMessenger>(WeakReferenceMessenger.Default);

        // Persistence (SQLite + EF Core) - 使用 StoragePathService 获取数据库路径
        var storagePathService = new StoragePathService(
            Microsoft.Extensions.Options.Options.Create(storageOptions)
        );
        var dbPath = storagePathService.GetDatabasePath();
        services.AddStoryboardPersistence(dbPath);

        // Logging
        var logPath = Path.Combine(ConfigurationPaths.UserDataDirectory, "logs", "app-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.Debug()
            .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();
        services.AddLogging(builder =>
        {
            builder.AddSerilog(Log.Logger, dispose: true);
        });

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<ApiKeyViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<UpdateNotificationViewModel>();
        services.AddSingleton<ViewModels.Resources.ResourceLibraryViewModel>();

        // 新的子 ViewModels
        services.AddTransient<ViewModels.Project.ProjectManagementViewModel>();
        services.AddTransient<ViewModels.Queue.JobQueueViewModel>();
        services.AddTransient<ViewModels.Import.VideoImportViewModel>();
        services.AddTransient<ViewModels.Import.FrameExtractionViewModel>();
        services.AddTransient<ViewModels.Shot.ShotListViewModel>();
        services.AddTransient<ViewModels.Shot.BatchInsertShotViewModel>();
        services.AddTransient<ViewModels.Shot.TimelineViewModel>();
        services.AddTransient<ViewModels.Generation.AiAnalysisViewModel>();
        services.AddTransient<ViewModels.Generation.ImageGenerationViewModel>();
        services.AddTransient<ViewModels.Generation.VideoGenerationViewModel>();
        services.AddTransient<ViewModels.Generation.ExportViewModel>();
        services.AddTransient<ViewModels.Shared.HistoryViewModel>();
        services.AddTransient<ViewModels.Batch.BatchOperationViewModel>();

        // Timeline Editor ViewModels
        services.AddTransient<ViewModels.Timeline.TimelinePlaybackViewModel>();
        services.AddTransient<ViewModels.Timeline.TimelineEditorViewModel>();

        // Services - 保持现有业务逻辑
        services.AddSingleton<VideoAnalysisService>();
        services.AddSingleton<IVideoAnalysisService>(sp => sp.GetRequiredService<VideoAnalysisService>());
        services.AddSingleton<IVideoMetadataService>(sp => sp.GetRequiredService<VideoAnalysisService>());
        services.AddSingleton<ShotTimelineSyncService>();
        services.AddSingleton<IFrameExtractionService, FrameExtractionService>();
        services.AddSingleton<ISmartStoryboardService, SmartStoryboardService>();
        services.AddSingleton<IAiShotService, AiShotService>();
        services.AddSingleton<IImageGenerationProvider, VolcengineImageGenerationProvider>();
        services.AddSingleton<IImageGenerationService, ImageGenerationService>();
        services.AddSingleton<IVideoGenerationProvider, VolcengineVideoGenerationProvider>();
        services.AddSingleton<IVideoGenerationService, VideoGenerationService>();
        services.AddSingleton<IFinalRenderService, FinalRenderService>();
        services.AddSingleton<ICapCutExportService, CapCutExportService>();
        services.AddSingleton<IDraftManager, DraftManager>();
        services.AddSingleton<ITimelineInteractionService, TimelineInteractionService>();

        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<IJobQueueService>(sp =>
            new JobQueueService(sp.GetRequiredService<IUiDispatcher>(), maxConcurrency: 2));

        // Update Service
        services.AddSingleton<UpdateService>();

        // Data Migration Service
        services.AddSingleton<DataMigrationService>();

        // AI Services
        services.AddSingleton<AI.Prompts.PromptManagementService>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.QwenServiceProvider>();
        services.AddSingleton<AI.Core.IAIServiceProvider, AI.Providers.VolcengineServiceProvider>();

        services.AddSingleton<AIServiceManager>();
    }
}
