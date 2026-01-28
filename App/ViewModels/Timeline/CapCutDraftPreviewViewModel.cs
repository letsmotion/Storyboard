using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Storyboard.Infrastructure.Services;
using Storyboard.Models.CapCut;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Timeline;

/// <summary>
/// CapCut 草稿预览 ViewModel - 独立预览任意 CapCut 草稿
/// </summary>
public partial class CapCutDraftPreviewViewModel : ObservableObject
{
    private readonly IDraftManager _draftManager;
    private readonly ILogger<CapCutDraftPreviewViewModel> _logger;

    [ObservableProperty]
    private string? _draftDirectory;

    [ObservableProperty]
    private DraftContent? _draftContent;

    [ObservableProperty]
    private DraftMetaInfo? _draftMetaInfo;

    [ObservableProperty]
    private bool _isDraftLoaded;

    [ObservableProperty]
    private string _statusMessage = "请选择 CapCut 草稿文件夹";

    // 时间轴信息
    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private int _trackCount;

    [ObservableProperty]
    private int _segmentCount;

    public CapCutDraftPreviewViewModel(
        IDraftManager draftManager,
        ILogger<CapCutDraftPreviewViewModel> logger)
    {
        _draftManager = draftManager;
        _logger = logger;
    }

    /// <summary>
    /// 打开 CapCut 草稿文件夹
    /// </summary>
    [RelayCommand]
    private async Task OpenDraftFolder(string? folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            // 打开文件夹选择对话框
            folderPath = await SelectDraftFolderAsync();
        }

        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        await LoadDraftAsync(folderPath);
    }

    /// <summary>
    /// 加载草稿
    /// </summary>
    private async Task LoadDraftAsync(string draftDirectory)
    {
        try
        {
            StatusMessage = "正在加载草稿...";
            _logger.LogInformation("加载 CapCut 草稿: {DraftDirectory}", draftDirectory);

            // 验证文件存在
            var contentPath = Path.Combine(draftDirectory, "draft_content.json");
            var metaPath = Path.Combine(draftDirectory, "draft_meta_info.json");

            if (!File.Exists(contentPath) || !File.Exists(metaPath))
            {
                StatusMessage = "错误：不是有效的 CapCut 草稿文件夹";
                _logger.LogWarning("草稿文件不存在: {DraftDirectory}", draftDirectory);
                return;
            }

            // 加载草稿
            (DraftContent, DraftMetaInfo) = await _draftManager.LoadDraftAsync(draftDirectory);
            DraftDirectory = draftDirectory;
            IsDraftLoaded = true;

            // 提取信息
            var timelineInfo = DraftAdapter.ExtractTimelineInfo(DraftContent);
            TotalDuration = timelineInfo.TotalDurationSeconds;
            TrackCount = timelineInfo.Tracks.Count;
            SegmentCount = timelineInfo.Tracks.Sum(t => t.Segments.Count);

            StatusMessage = $"草稿加载成功：{DraftMetaInfo.DraftName}";
            _logger.LogInformation("草稿加载成功: {DraftName}, 时长: {Duration:F2}s, 轨道: {TrackCount}, 片段: {SegmentCount}",
                DraftMetaInfo.DraftName, TotalDuration, TrackCount, SegmentCount);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载失败: {ex.Message}";
            _logger.LogError(ex, "加载草稿失败: {DraftDirectory}", draftDirectory);
            IsDraftLoaded = false;
        }
    }

    /// <summary>
    /// 选择草稿文件夹
    /// </summary>
    private async Task<string?> SelectDraftFolderAsync()
    {
        // 使用 Avalonia 的文件夹选择对话框
        var dialog = new Avalonia.Controls.OpenFolderDialog
        {
            Title = "选择 CapCut 草稿文件夹"
        };

        var mainWindow = (Avalonia.Application.Current?.ApplicationLifetime as
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        if (mainWindow == null)
            return null;

        var result = await dialog.ShowAsync(mainWindow);
        return result;
    }

    /// <summary>
    /// 获取草稿信息摘要
    /// </summary>
    public string GetDraftSummary()
    {
        if (!IsDraftLoaded || DraftContent == null || DraftMetaInfo == null)
        {
            return "未加载草稿";
        }

        return $"""
            草稿名称: {DraftMetaInfo.DraftName}
            草稿 ID: {DraftContent.Id}
            总时长: {TotalDuration:F2} 秒
            分辨率: {DraftContent.CanvasConfig.Width}x{DraftContent.CanvasConfig.Height}
            帧率: {DraftContent.Fps} fps
            轨道数: {TrackCount}
            片段数: {SegmentCount}
            创建时间: {DateTimeOffset.FromUnixTimeSeconds(DraftContent.CreateTime):yyyy-MM-dd HH:mm:ss}
            更新时间: {DateTimeOffset.FromUnixTimeSeconds(DraftContent.UpdateTime):yyyy-MM-dd HH:mm:ss}
            """;
    }

    /// <summary>
    /// 导出草稿信息为文本
    /// </summary>
    [RelayCommand]
    private async Task ExportDraftInfo(string? outputPath)
    {
        if (!IsDraftLoaded || DraftContent == null)
        {
            _logger.LogWarning("无法导出：草稿未加载");
            return;
        }

        try
        {
            var summary = GetDraftSummary();

            // 添加详细的轨道和片段信息
            var detailedInfo = summary + "\n\n=== 详细信息 ===\n\n";

            var timelineInfo = DraftAdapter.ExtractTimelineInfo(DraftContent);
            foreach (var track in timelineInfo.Tracks)
            {
                detailedInfo += $"\n轨道: {track.Type}\n";
                detailedInfo += $"片段数: {track.Segments.Count}\n";

                foreach (var segment in track.Segments)
                {
                    detailedInfo += $"  - 片段 {segment.Id}\n";
                    detailedInfo += $"    开始时间: {segment.StartTimeSeconds:F2}s\n";
                    detailedInfo += $"    时长: {segment.DurationSeconds:F2}s\n";
                    detailedInfo += $"    视频: {Path.GetFileName(segment.VideoPath)}\n";
                }
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(DraftDirectory!, "draft_info.txt");
            }

            await File.WriteAllTextAsync(outputPath, detailedInfo);
            StatusMessage = $"草稿信息已导出: {outputPath}";
            _logger.LogInformation("草稿信息已导出: {OutputPath}", outputPath);
        }
        catch (Exception ex)
        {
            StatusMessage = $"导出失败: {ex.Message}";
            _logger.LogError(ex, "导出草稿信息失败");
        }
    }
}
