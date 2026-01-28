using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Infrastructure.Services;
using Storyboard.Messages;
using Storyboard.Models;
using Storyboard.Models.CapCut;
using Storyboard.Models.Timeline;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Timeline;

/// <summary>
/// 时间轴编辑器主 ViewModel - 基于 CapCut 草稿格式
/// </summary>
public partial class TimelineEditorViewModel : ObservableObject
{
    private readonly IDraftManager _draftManager;
    private readonly IMessenger _messenger;
    private readonly ILogger<TimelineEditorViewModel> _logger;

    // 核心数据：CapCut 草稿
    private DraftContent? _draftContent;
    private DraftMetaInfo? _draftMetaInfo;
    private string? _currentProjectPath;

    // 子 ViewModels
    public TimelinePlaybackViewModel Playback { get; }

    // 轨道集合（UI 视图）
    [ObservableProperty]
    private ObservableCollection<TimelineTrack> _tracks = new();

    // 时间轴缩放
    [ObservableProperty]
    private double _pixelsPerSecond = 50; // 10-200

    [ObservableProperty]
    private double _timelineWidth;

    [ObservableProperty]
    private double _totalDuration;

    // 播放头位置
    [ObservableProperty]
    private double _playheadPosition; // 像素位置

    [ObservableProperty]
    private double _playheadTime; // 时间（秒）

    // 选中的片段
    [ObservableProperty]
    private TimelineClip? _selectedClip;

    // 时间标记
    [ObservableProperty]
    private ObservableCollection<Models.TimeMarker> _timeMarkers = new();

    // 草稿是否已加载
    [ObservableProperty]
    private bool _isDraftLoaded;

    // 自动保存定时器
    private System.Timers.Timer? _autoSaveTimer;
    private bool _isDirty;

    public TimelineEditorViewModel(
        TimelinePlaybackViewModel playback,
        IDraftManager draftManager,
        IMessenger messenger,
        ILogger<TimelineEditorViewModel> logger)
    {
        Playback = playback;
        _draftManager = draftManager;
        _messenger = messenger;
        _logger = logger;

        // 订阅 Shot 消息
        _messenger.Register<ShotAddedMessage>(this, OnShotChanged);
        _messenger.Register<ShotDeletedMessage>(this, OnShotChanged);
        _messenger.Register<ShotUpdatedMessage>(this, OnShotChanged);
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);
        _messenger.Register<ClipSelectedMessage>(this, OnClipSelected);

        // 订阅播放状态变化
        Playback.State.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Playback.State.CurrentTime))
            {
                PlayheadTime = Playback.State.CurrentTime;
                PlayheadPosition = PlayheadTime * PixelsPerSecond;
            }
        };

        // 初始化自动保存
        InitializeAutoSave();

        _logger.LogInformation("TimelineEditorViewModel 初始化完成（基于 CapCut 草稿）");
    }

    /// <summary>
    /// 初始化自动保存
    /// </summary>
    private void InitializeAutoSave()
    {
        _autoSaveTimer = new System.Timers.Timer(5000); // 每 5 秒检查一次
        _autoSaveTimer.Elapsed += async (s, e) =>
        {
            if (_isDirty && _draftContent != null && _draftMetaInfo != null && !string.IsNullOrEmpty(_currentProjectPath))
            {
                await SaveDraftAsync();
                _isDirty = false;
            }
        };
        _autoSaveTimer.Start();
    }

    /// <summary>
    /// 加载或创建草稿
    /// </summary>
    public async Task LoadOrCreateDraftAsync(string projectPath, string projectName)
    {
        try
        {
            _currentProjectPath = projectPath;
            var draftDirectory = _draftManager.GetDraftDirectory(projectPath);

            if (System.IO.Directory.Exists(draftDirectory) &&
                System.IO.File.Exists(System.IO.Path.Combine(draftDirectory, "draft_content.json")))
            {
                // 加载现有草稿
                (_draftContent, _draftMetaInfo) = await _draftManager.LoadDraftAsync(draftDirectory);
                _logger.LogInformation("加载现有草稿: {DraftId}", _draftContent.Id);
            }
            else
            {
                // 创建新草稿
                (_draftContent, _draftMetaInfo) = await _draftManager.CreateNewDraftAsync(projectName, projectPath);
                _logger.LogInformation("创建新草稿: {DraftId}", _draftContent.Id);
            }

            IsDraftLoaded = true;

            // 从草稿构建时间轴 UI
            BuildTimelineFromDraft();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载或创建草稿失败");
            IsDraftLoaded = false;
        }
    }

    /// <summary>
    /// 从 Shots 同步到草稿
    /// </summary>
    public async Task SyncShotsToTimelineAsync()
    {
        // 如果草稿未加载，先尝试加载
        if (_draftContent == null)
        {
            _logger.LogInformation("草稿未加载，尝试自动加载...");

            // 获取项目路径
            var pathQuery = new GetCurrentProjectPathQuery();
            _messenger.Send(pathQuery);

            var projectQuery = new GetProjectInfoQuery();
            _messenger.Send(projectQuery);

            if (!string.IsNullOrEmpty(pathQuery.ProjectPath) && projectQuery.ProjectInfo != null)
            {
                await LoadOrCreateDraftAsync(pathQuery.ProjectPath, projectQuery.ProjectInfo.Name);
            }
            else
            {
                _logger.LogWarning("无法获取项目信息，跳过草稿加载");
                return;
            }
        }

        var query = new GetAllShotsQuery();
        _messenger.Send(query);

        if (query.Shots == null || query.Shots.Count == 0)
        {
            _logger.LogInformation("没有可用的 Shots，清空时间轴");
            if (_draftContent != null)
            {
                _draftContent.Tracks.Clear();
                _draftContent.Materials.Videos.Clear();
                _draftContent.Duration = 0;
                BuildTimelineFromDraft();
                await SaveDraftAsync();
            }
            return;
        }

        // 使用适配器同步数据
        DraftAdapter.SyncShotsToDraft(query.Shots.ToList(), _draftContent);

        // 标记为脏数据
        _isDirty = true;

        // 重建 UI
        BuildTimelineFromDraft();

        _logger.LogInformation("同步完成: {SegmentCount} 个片段",
            _draftContent.Tracks.SelectMany(t => t.Segments).Count());
    }

    /// <summary>
    /// 从草稿构建时间轴 UI
    /// </summary>
    private void BuildTimelineFromDraft()
    {
        if (_draftContent == null)
        {
            _logger.LogWarning("草稿未加载");
            return;
        }

        // 清空现有轨道
        Tracks.Clear();

        // 提取时间轴信息
        var timelineInfo = DraftAdapter.ExtractTimelineInfo(_draftContent);

        // 更新总时长
        TotalDuration = timelineInfo.TotalDurationSeconds;
        TimelineWidth = TotalDuration * PixelsPerSecond;
        Playback.State.TotalDuration = TotalDuration;

        // 创建轨道
        foreach (var trackInfo in timelineInfo.Tracks)
        {
            var trackType = trackInfo.Type switch
            {
                "video" => TrackType.Video,
                "audio" => TrackType.Audio,
                _ => TrackType.Video
            };

            var track = new TimelineTrack(trackType, $"{trackInfo.Type} 轨道")
            {
                Id = Guid.Parse(trackInfo.Id)
            };

            // 创建片段
            foreach (var segmentInfo in trackInfo.Segments)
            {
                var clip = new TimelineClip
                {
                    Id = Guid.Parse(segmentInfo.Id),
                    TrackId = track.Id,
                    StartTime = segmentInfo.StartTimeSeconds,
                    Duration = segmentInfo.DurationSeconds,
                    PixelsPerSecond = PixelsPerSecond,
                    Status = ClipStatus.Generated,
                    VideoPath = segmentInfo.VideoPath,
                    ThumbnailPath = null
                };

                track.Clips.Add(clip);
            }

            Tracks.Add(track);
        }

        // 生成时间标记
        GenerateTimeMarkers();

        _logger.LogInformation("时间轴构建完成: {TrackCount} 个轨道, 总时长 {Duration:F2}s",
            Tracks.Count, TotalDuration);
    }

    /// <summary>
    /// 保存草稿
    /// </summary>
    private async Task SaveDraftAsync()
    {
        if (_draftContent == null || _draftMetaInfo == null || string.IsNullOrEmpty(_currentProjectPath))
        {
            return;
        }

        try
        {
            var draftDirectory = _draftManager.GetDraftDirectory(_currentProjectPath);
            await _draftManager.SaveDraftAsync(draftDirectory, _draftContent, _draftMetaInfo);
            _logger.LogDebug("草稿自动保存成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存草稿失败");
        }
    }

    /// <summary>
    /// 生成时间标记
    /// </summary>
    private void GenerateTimeMarkers()
    {
        TimeMarkers.Clear();

        if (TotalDuration <= 0) return;

        var interval = CalculateTimeMarkerInterval(TotalDuration);
        for (double t = 0; t <= TotalDuration; t += interval)
        {
            TimeMarkers.Add(new Models.TimeMarker
            {
                Position = t * PixelsPerSecond,
                Label = FormatTime(t)
            });
        }
    }

    /// <summary>
    /// 计算时间标记间隔
    /// </summary>
    private double CalculateTimeMarkerInterval(double duration)
    {
        if (duration <= 10) return 1;
        if (duration <= 30) return 5;
        if (duration <= 60) return 10;
        if (duration <= 300) return 30;
        return 60;
    }

    /// <summary>
    /// 格式化时间显示
    /// </summary>
    private string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    /// <summary>
    /// 播放头跳转
    /// </summary>
    [RelayCommand]
    private void SeekToTime(double time)
    {
        PlayheadTime = Math.Clamp(time, 0, TotalDuration);
        PlayheadPosition = PlayheadTime * PixelsPerSecond;
        Playback.SeekTo(PlayheadTime);

        _messenger.Send(new PlayheadPositionChangedMessage(PlayheadTime, PlayheadPosition));
    }

    /// <summary>
    /// 添加轨道（暂时保留以兼容 UI，实际由 SyncShotsToTimelineAsync 管理）
    /// </summary>
    [RelayCommand]
    private void AddTrack(TrackType type)
    {
        _logger.LogInformation("添加轨道请求: {Type}，当前由自动同步管理", type);
        // 在新架构中，轨道由 SyncShotsToTimelineAsync 自动管理
        // 此命令保留以兼容现有 UI
    }

    /// <summary>
    /// 删除轨道（暂时保留以兼容 UI）
    /// </summary>
    [RelayCommand]
    private void DeleteTrack(TimelineTrack? track)
    {
        _logger.LogInformation("删除轨道请求: {TrackName}，当前由自动同步管理", track?.Name);
        // 在新架构中，轨道由 SyncShotsToTimelineAsync 自动管理
        // 此命令保留以兼容现有 UI
    }

    /// <summary>
    /// 适应窗口（自动调整缩放）
    /// </summary>
    [RelayCommand]
    private void FitToWindow(double availableWidth)
    {
        if (TotalDuration > 0 && availableWidth > 0)
        {
            PixelsPerSecond = Math.Clamp(availableWidth / TotalDuration, 10, 200);
            _logger.LogInformation("适应窗口: PixelsPerSecond = {Value:F2}", PixelsPerSecond);
        }
    }

    /// <summary>
    /// 缩放变化时重新计算布局
    /// </summary>
    partial void OnPixelsPerSecondChanged(double value)
    {
        TimelineWidth = TotalDuration * value;
        PlayheadPosition = PlayheadTime * value;

        // 更新所有片段的像素属性
        foreach (var track in Tracks)
        {
            foreach (var clip in track.Clips)
            {
                clip.PixelsPerSecond = value;
            }
        }

        // 重新生成时间标记
        GenerateTimeMarkers();
    }

    // 消息处理
    private void OnShotChanged(object recipient, object message)
    {
        // Shot 变化时重新同步
        _ = SyncShotsToTimelineAsync();
    }

    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        // 项目加载后加载草稿
        // 通过查询消息获取项目路径
        var pathQuery = new GetCurrentProjectPathQuery();
        _messenger.Send(pathQuery);

        if (!string.IsNullOrEmpty(pathQuery.ProjectPath))
        {
            var projectName = message.ProjectState.Name;
            _ = LoadOrCreateDraftAsync(pathQuery.ProjectPath, projectName);
        }
    }

    private void OnClipSelected(object recipient, ClipSelectedMessage message)
    {
        SelectedClip = message.Clip;

        if (SelectedClip != null)
        {
            SelectedClip.IsSelected = true;
            _logger.LogInformation("片段已选中: {ClipId}", SelectedClip.Id);

            // 加载并播放选中片段的视频
            LoadClipVideo(SelectedClip);
        }
    }

    /// <summary>
    /// 加载片段视频
    /// </summary>
    private void LoadClipVideo(TimelineClip clip)
    {
        if (string.IsNullOrEmpty(clip.VideoPath) || !System.IO.File.Exists(clip.VideoPath))
        {
            _logger.LogWarning("片段视频路径无效: {Path}", clip.VideoPath);
            return;
        }

        try
        {
            // 停止当前播放
            Playback.StopPlayback();

            // 加载新视频
            var libVLC = Playback.GetLibVLC();
            var mediaPlayer = Playback.GetMediaPlayer();

            if (libVLC == null || mediaPlayer == null)
            {
                _logger.LogWarning("LibVLC 或 MediaPlayer 未初始化");
                return;
            }

            var media = new LibVLCSharp.Shared.Media(libVLC, clip.VideoPath);
            mediaPlayer.Media = media;

            // 更新播放状态
            Playback.State.TotalDuration = clip.Duration;

            _logger.LogInformation("加载视频: {Path}, 时长: {Duration:F2}s", clip.VideoPath, clip.Duration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载视频失败: {Path}", clip.VideoPath);
        }
    }

    /// <summary>
    /// 获取草稿内容（用于导出）
    /// </summary>
    public DraftContent? GetDraftContent() => _draftContent;

    /// <summary>
    /// 获取草稿元信息（用于导出）
    /// </summary>
    public DraftMetaInfo? GetDraftMetaInfo() => _draftMetaInfo;
}
