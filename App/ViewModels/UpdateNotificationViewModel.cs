using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Services;
using Storyboard.Messages;
using System;
using System.Threading.Tasks;
using Velopack;

namespace Storyboard.ViewModels;

/// <summary>
/// 更新通知 ViewModel
/// </summary>
public partial class UpdateNotificationViewModel : ObservableObject
{
    private readonly UpdateService _updateService;
    private readonly IMessenger _messenger;
    private readonly ILogger<UpdateNotificationViewModel> _logger;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _newVersion = string.Empty;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private string _updateSize = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _downloadCompleted;

    [ObservableProperty]
    private bool _downloadFailed;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _isDeltaUpdate;

    private UpdateInfo? _updateInfo;

    public UpdateNotificationViewModel(
        UpdateService updateService,
        IMessenger messenger,
        ILogger<UpdateNotificationViewModel> logger)
    {
        _updateService = updateService;
        _messenger = messenger;
        _logger = logger;

        CurrentVersion = _updateService.GetCurrentVersion();

        // 监听更新消息
        _messenger.Register<UpdateAvailableMessage>(this, OnUpdateAvailable);
        _messenger.Register<UpdateDownloadProgressMessage>(this, OnDownloadProgress);
    }

    private void OnUpdateAvailable(object recipient, UpdateAvailableMessage message)
    {
        _updateInfo = message.UpdateInfo;
        NewVersion = message.UpdateInfo.TargetFullRelease.Version.ToString();

        // 计算更新大小
        UpdateSize = FormatFileSize(message.UpdateInfo.TargetFullRelease.Size);

        // 检查是否为增量更新
        IsDeltaUpdate = message.UpdateInfo.DeltasToTarget?.Any() ?? false;

        IsUpdateAvailable = true;
        StatusMessage = IsDeltaUpdate
            ? $"发现新版本 {NewVersion} (增量更新，仅 {UpdateSize})"
            : $"发现新版本 {NewVersion} (完整安装包，{UpdateSize})";

        _logger.LogInformation("发现新版本: {Version}, 大小: {Size}, 增量更新: {IsDelta}",
            NewVersion, UpdateSize, IsDeltaUpdate);
    }

    private void OnDownloadProgress(object recipient, UpdateDownloadProgressMessage message)
    {
        DownloadProgress = message.Progress;
        StatusMessage = $"下载中... {message.Progress}%";
    }

    [RelayCommand]
    private async Task DownloadAndInstallAsync()
    {
        if (_updateInfo == null) return;

        try
        {
            IsDownloading = true;
            DownloadCompleted = false;
            DownloadFailed = false;
            DownloadProgress = 0;
            StatusMessage = "准备下载更新...";

            _logger.LogInformation("开始下载更新: {Version}", NewVersion);

            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                StatusMessage = $"下载中... {p}%";
                _messenger.Send(new UpdateDownloadProgressMessage(p));
            });

            var success = await _updateService.DownloadUpdatesAsync(_updateInfo, progress);

            if (success)
            {
                DownloadCompleted = true;
                StatusMessage = "下载完成！准备安装...";
                _logger.LogInformation("更新下载完成");

                // 等待 1 秒让用户看到完成消息
                await Task.Delay(1000);

                // 使用 WaitExitThenApplyUpdatesAsync 而不是 ApplyUpdatesAndRestart
                // 这样更新程序会等待应用完全退出后再替换文件，避免文件占用问题
                StatusMessage = "更新将在应用关闭后自动安装...";
                await _updateService.ApplyUpdatesAsync(_updateInfo);

                // 通知用户关闭应用
                StatusMessage = "更新已准备就绪，请关闭应用以完成安装";
                _logger.LogInformation("更新已准备就绪，等待应用退出");

                // 可选：自动关闭应用
                await Task.Delay(2000);
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
                {
                    desktop.Shutdown();
                }
            }
            else
            {
                DownloadFailed = true;
                StatusMessage = "下载失败";
                ErrorMessage = "下载更新失败，请稍后重试或手动下载安装包";
                _logger.LogError("更新下载失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载更新时发生错误");
            DownloadFailed = true;
            StatusMessage = "下载失败";
            ErrorMessage = $"错误: {ex.Message}";
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        if (!IsDownloading)
        {
            IsUpdateAvailable = false;
            StatusMessage = string.Empty;
            _logger.LogInformation("用户关闭了更新通知");
        }
    }

    [RelayCommand]
    private void RemindLater()
    {
        IsUpdateAvailable = false;
        StatusMessage = string.Empty;
        _logger.LogInformation("用户选择稍后更新");
    }

    [RelayCommand]
    private void OpenDownloadPage()
    {
        try
        {
            var url = "https://github.com/BroderQi/Storyboard/releases/latest";
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
            _logger.LogInformation("打开下载页面: {Url}", url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打开下载页面失败");
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            StatusMessage = "检查更新中...";
            _logger.LogInformation("手动检查更新");

            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo != null)
            {
                _updateInfo = updateInfo;
                NewVersion = updateInfo.TargetFullRelease.Version.ToString();
                UpdateSize = FormatFileSize(updateInfo.TargetFullRelease.Size);
                IsDeltaUpdate = updateInfo.DeltasToTarget?.Any() ?? false;

                IsUpdateAvailable = true;
                StatusMessage = IsDeltaUpdate
                    ? $"发现新版本 {NewVersion} (增量更新，仅 {UpdateSize})"
                    : $"发现新版本 {NewVersion} (完整安装包，{UpdateSize})";

                _logger.LogInformation("发现新版本: {Version}", NewVersion);
            }
            else
            {
                StatusMessage = "当前已是最新版本";
                _logger.LogInformation("当前已是最新版本");
                await Task.Delay(2000);
                StatusMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查更新失败");
            StatusMessage = $"检查更新失败: {ex.Message}";
            await Task.Delay(3000);
            StatusMessage = string.Empty;
        }
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
