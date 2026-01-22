using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Storyboard.ViewModels;

/// <summary>
/// 设置 ViewModel
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly UpdateService _updateService;
    private readonly StoragePathService _storagePathService;
    private readonly DataMigrationService _dataMigrationService;
    private readonly IOptions<StorageOptions> _storageOptions;

    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private bool _isFirstLaunch;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private string _currentUpdateSource = string.Empty;

    // 存储位置设置
    [ObservableProperty]
    private bool _useCustomLocation;

    [ObservableProperty]
    private string _dataDirectory = string.Empty;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _databaseSize = "计算中...";

    [ObservableProperty]
    private string _outputSize = "计算中...";

    // 数据迁移
    [ObservableProperty]
    private bool _isMigrationDialogOpen;

    [ObservableProperty]
    private string _newDataDirectory = string.Empty;

    [ObservableProperty]
    private string _newOutputDirectory = string.Empty;

    [ObservableProperty]
    private bool _isMigrating;

    [ObservableProperty]
    private int _migrationProgress;

    [ObservableProperty]
    private string _migrationStatus = string.Empty;

    // 更新设置
    [ObservableProperty]
    private bool _autoCheckUpdates = true;

    [ObservableProperty]
    private int _checkDelaySeconds = 3;

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        UpdateService updateService,
        StoragePathService storagePathService,
        DataMigrationService dataMigrationService,
        IOptions<StorageOptions> storageOptions)
    {
        _logger = logger;
        _updateService = updateService;
        _storagePathService = storagePathService;
        _dataMigrationService = dataMigrationService;
        _storageOptions = storageOptions;

        LoadSettings();
    }

    private void LoadSettings()
    {
        // 加载版本信息
        CurrentVersion = _updateService.GetCurrentVersion();
        CurrentUpdateSource = _updateService.GetCurrentSourceName();

        // 加载存储位置设置
        var options = _storageOptions.Value;
        UseCustomLocation = options.UseCustomLocation;
        DataDirectory = _storagePathService.GetDataDirectory();
        OutputDirectory = _storagePathService.GetOutputDirectory();

        // 计算存储大小
        _ = CalculateStorageSizeAsync();
    }

    [RelayCommand]
    private void ShowDialog()
    {
        IsDialogOpen = true;
        LoadSettings();
    }

    [RelayCommand]
    private void CloseDialog()
    {
        IsDialogOpen = false;
    }

    [RelayCommand]
    private async Task CalculateStorageSizeAsync()
    {
        try
        {
            DatabaseSize = "计算中...";
            OutputSize = "计算中...";

            await Task.Run(() =>
            {
                // 计算数据库大小
                var dataDir = _storagePathService.GetDataDirectory();
                if (Directory.Exists(dataDir))
                {
                    var dbPath = _storagePathService.GetDatabasePath();
                    if (File.Exists(dbPath))
                    {
                        var dbSize = new FileInfo(dbPath).Length;
                        DatabaseSize = FormatFileSize(dbSize);
                    }
                    else
                    {
                        DatabaseSize = "0 B";
                    }
                }
                else
                {
                    DatabaseSize = "0 B";
                }

                // 计算输出文件夹大小
                var outputDir = _storagePathService.GetOutputDirectory();
                if (Directory.Exists(outputDir))
                {
                    var totalSize = CalculateDirectorySize(outputDir);
                    OutputSize = FormatFileSize(totalSize);
                }
                else
                {
                    OutputSize = "0 B";
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "计算存储大小失败");
            DatabaseSize = "计算失败";
            OutputSize = "计算失败";
        }
    }

    private long CalculateDirectorySize(string directory)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directory);
            long size = 0;

            // 计算所有文件大小
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }

            return size;
        }
        catch
        {
            return 0;
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    [RelayCommand]
    private void ShowMigrationDialog()
    {
        NewDataDirectory = DataDirectory;
        NewOutputDirectory = OutputDirectory;
        IsMigrationDialogOpen = true;
    }

    [RelayCommand]
    private void CloseMigrationDialog()
    {
        if (!IsMigrating)
        {
            IsMigrationDialogOpen = false;
        }
    }

    [RelayCommand]
    private async Task StartMigrationAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDataDirectory) || string.IsNullOrWhiteSpace(NewOutputDirectory))
        {
            _logger.LogWarning("迁移失败：目标路径为空");
            MigrationStatus = "错误：请选择有效的目标路径";
            return;
        }

        try
        {
            IsMigrating = true;
            MigrationProgress = 0;
            MigrationStatus = "准备迁移...";

            var progress = new Progress<(int progress, string status)>(p =>
            {
                MigrationProgress = p.progress;
                MigrationStatus = p.status;
            });

            var success = await _dataMigrationService.MigrateDataAsync(
                DataDirectory,
                OutputDirectory,
                NewDataDirectory,
                NewOutputDirectory,
                progress);

            if (success)
            {
                MigrationStatus = "迁移完成！应用将重启以应用更改。";
                _logger.LogInformation("数据迁移成功，准备重启应用");

                // 等待 2 秒让用户看到成功消息
                await Task.Delay(2000);

                // 重启应用
                RestartApplication();
            }
            else
            {
                MigrationStatus = "迁移失败，请查看日志了解详情";
                _logger.LogError("数据迁移失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据迁移过程中发生错误");
            MigrationStatus = $"错误：{ex.Message}";
        }
        finally
        {
            IsMigrating = false;
        }
    }

    [RelayCommand]
    private async Task BrowseDataDirectoryAsync()
    {
        try
        {
            var dialog = new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择数据库存储位置",
                AllowMultiple = false
            };

            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var storageProvider = desktop.MainWindow?.StorageProvider;
                if (storageProvider != null)
                {
                    var result = await storageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                    {
                        Title = "选择数据库存储位置",
                        AllowMultiple = false
                    });

                    if (result.Count > 0)
                    {
                        DataDirectory = result[0].Path.LocalPath;
                        UseCustomLocation = true;  // 自动启用自定义位置
                        _logger.LogInformation("用户选择数据库位置: {Path}", DataDirectory);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "浏览数据库目录失败");
        }
    }

    [RelayCommand]
    private async Task BrowseOutputDirectoryAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var storageProvider = desktop.MainWindow?.StorageProvider;
                if (storageProvider != null)
                {
                    var result = await storageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                    {
                        Title = "选择输出文件存储位置",
                        AllowMultiple = false
                    });

                    if (result.Count > 0)
                    {
                        OutputDirectory = result[0].Path.LocalPath;
                        UseCustomLocation = true;  // 自动启用自定义位置
                        _logger.LogInformation("用户选择输出目录: {Path}", OutputDirectory);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "浏览输出目录失败");
        }
    }

    [RelayCommand]
    private void OpenDataDirectory()
    {
        OpenDirectory(DataDirectory);
    }

    [RelayCommand]
    private void OpenOutputDirectory()
    {
        OpenDirectory(OutputDirectory);
    }

    private void OpenDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            else
            {
                _logger.LogWarning("目录不存在: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开目录失败: {Path}", path);
        }
    }

    private void RestartApplication()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                System.Diagnostics.Process.Start(exePath);
                Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重启应用失败");
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            // 保存设置到 appsettings.json
            var settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(settingsPath))
            {
                var json = await File.ReadAllTextAsync(settingsPath);
                var root = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject;

                if (root != null)
                {
                    // 更新 Storage 配置
                    var storage = new System.Text.Json.Nodes.JsonObject
                    {
                        ["UseCustomLocation"] = UseCustomLocation,
                        ["DataDirectory"] = DataDirectory,
                        ["OutputDirectory"] = OutputDirectory,
                        ["_Configured"] = true // 标记已配置
                    };

                    root["Storage"] = storage;

                    // 保存配置
                    var options = new System.Text.Json.JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    };

                    var updatedJson = System.Text.Json.JsonSerializer.Serialize(root, options);
                    await File.WriteAllTextAsync(settingsPath, updatedJson);

                    _logger.LogInformation("设置已保存: UseCustom={UseCustom}, DataPath={DataPath}, OutputPath={OutputPath}",
                        UseCustomLocation, DataDirectory, OutputDirectory);

                    // 提示用户需要重启应用
                    await ShowRestartConfirmationAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存设置失败");
        }
    }

    private async Task ShowRestartConfirmationAsync()
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dialog = new Avalonia.Controls.Window
                {
                    Title = "需要重启",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
                    CanResize = false
                };

                var content = new Avalonia.Controls.StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 20
                };

                // 提示文本
                var message = new Avalonia.Controls.TextBlock
                {
                    Text = "存储位置配置已保存。\n\n为使新配置生效，需要重启应用。\n\n是否立即重启？",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 14
                };
                content.Children.Add(message);

                // 按钮面板
                var buttonPanel = new Avalonia.Controls.StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10
                };

                var restartButton = new Avalonia.Controls.Button
                {
                    Content = "立即重启",
                    Width = 100
                };
                restartButton.Click += (s, e) =>
                {
                    dialog.Close(true);
                };

                var laterButton = new Avalonia.Controls.Button
                {
                    Content = "稍后重启",
                    Width = 100
                };
                laterButton.Click += (s, e) =>
                {
                    dialog.Close(false);
                };

                buttonPanel.Children.Add(laterButton);
                buttonPanel.Children.Add(restartButton);
                content.Children.Add(buttonPanel);

                dialog.Content = content;

                var result = await dialog.ShowDialog<bool>(desktop.MainWindow!);

                if (result)
                {
                    // 用户选择立即重启
                    _logger.LogInformation("用户选择立即重启应用");
                    RestartApplication();
                }
                else
                {
                    // 用户选择稍后重启
                    _logger.LogInformation("用户选择稍后重启应用");
                    CloseDialog();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示重启确认对话框失败");
            CloseDialog();
        }
    }
}
