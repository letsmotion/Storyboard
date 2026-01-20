using Avalonia.Controls;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
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

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _newVersion = string.Empty;

    [ObservableProperty]
    private string _currentVersion = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private UpdateInfo? _updateInfo;

    public UpdateNotificationViewModel()
    {
        _updateService = App.Services.GetRequiredService<UpdateService>();
        _messenger = App.Services.GetRequiredService<IMessenger>();

        CurrentVersion = _updateService.GetCurrentVersion();

        // 监听更新消息
        _messenger.Register<UpdateAvailableMessage>(this, OnUpdateAvailable);
        _messenger.Register<UpdateDownloadProgressMessage>(this, OnDownloadProgress);
    }

    private void OnUpdateAvailable(object recipient, UpdateAvailableMessage message)
    {
        _updateInfo = message.UpdateInfo;
        NewVersion = message.UpdateInfo.TargetFullRelease.Version.ToString();
        IsUpdateAvailable = true;
        StatusMessage = $"发现新版本 {NewVersion}";
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
            StatusMessage = "开始下载更新...";

            var progress = new Progress<int>(p =>
            {
                DownloadProgress = p;
                _messenger.Send(new UpdateDownloadProgressMessage(p));
            });

            var success = await _updateService.DownloadUpdatesAsync(_updateInfo, progress);

            if (success)
            {
                StatusMessage = "下载完成，准备安装...";
                await Task.Delay(500);

                // 应用更新并重启
                _updateService.ApplyUpdatesAndRestart(_updateInfo);
            }
            else
            {
                StatusMessage = "下载失败，请稍后重试";
                IsDownloading = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"更新失败: {ex.Message}";
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void DismissUpdate()
    {
        IsUpdateAvailable = false;
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        try
        {
            StatusMessage = "检查更新中...";
            var updateInfo = await _updateService.CheckForUpdatesAsync();

            if (updateInfo != null)
            {
                _updateInfo = updateInfo;
                NewVersion = updateInfo.TargetFullRelease.Version.ToString();
                IsUpdateAvailable = true;
                StatusMessage = $"发现新版本 {NewVersion}";
            }
            else
            {
                StatusMessage = "当前已是最新版本";
                await Task.Delay(2000);
                StatusMessage = string.Empty;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"检查更新失败: {ex.Message}";
            await Task.Delay(3000);
            StatusMessage = string.Empty;
        }
    }
}
