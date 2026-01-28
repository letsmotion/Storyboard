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
using System.Collections.Generic;
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
    private readonly ITimelineInteractionService _interactionService;

    // 核心数据：CapCut 草稿
    private DraftContent? _draftContent;
    private DraftMetaInfo? _draftMetaInfo;
    private string? _currentProjectPath;
    private List<ShotItem> _shotsCache = new();

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

    // 多选片段集合
    [ObservableProperty]
    private ObservableCollection<TimelineClip> _selectedClips = new();

    // 时间标记
    [ObservableProperty]
    private ObservableCollection<Models.TimeMarker> _timeMarkers = new();

    // 轨道总高度（用于 Canvas 高度和播放头高度）
    public double TracksHeight
    {
        get
        {
            if (Tracks == null || Tracks.Count == 0)
                return 0;
            return Tracks.Sum(t => t.Height);
        }
    }

    // 吸附线位置（拖动时显示）
    [ObservableProperty]
    private double? _snapLinePosition;

    // 吸附反馈信息
    [ObservableProperty]
    private bool _isSnapFeedbackVisible;

    [ObservableProperty]
    private string _snapFeedbackText = string.Empty;

    [ObservableProperty]
    private double _snapFeedbackX;

    [ObservableProperty]
    private double _snapFeedbackY;

    // 拖动提示框
    [ObservableProperty]
    private double _dragTooltipX;

    [ObservableProperty]
    private double _dragTooltipY;

    [ObservableProperty]
    private string _dragTooltipTime = string.Empty;

    [ObservableProperty]
    private bool _isDragTooltipVisible;

    // 拖动幽灵预览
    [ObservableProperty]
    private bool _isGhostPreviewVisible;

    [ObservableProperty]
    private double _ghostPreviewX;

    [ObservableProperty]
    private double _ghostPreviewY;

    [ObservableProperty]
    private double _ghostPreviewWidth;

    [ObservableProperty]
    private string? _ghostPreviewThumbnail;

    [ObservableProperty]
    private string _ghostPreviewLabel = string.Empty;

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
        ILogger<TimelineEditorViewModel> logger,
        ITimelineInteractionService interactionService)
    {
        Playback = playback;
        _draftManager = draftManager;
        _messenger = messenger;
        _logger = logger;
        _interactionService = interactionService;

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

        var shots = query.Shots?.ToList() ?? new List<ShotItem>();
        _shotsCache = shots;

        if (shots.Count == 0)
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
        DraftAdapter.SyncShotsToDraft(shots, _draftContent);

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
        SelectedClip = null;
        SelectedClips.Clear();

        var shots = GetShotsSnapshot();
        var shotLookup = BuildShotLookup(shots);

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

                var normalizedPath = NormalizePath(segmentInfo.VideoPath);
                if (!string.IsNullOrEmpty(normalizedPath) && shotLookup.TryGetValue(normalizedPath, out var shot))
                {
                    clip.ShotNumber = shot.ShotNumber;
                    clip.ThumbnailPath = shot.FirstFrameImagePath;
                    clip.Status = TimelineClip.GetStatusFromShot(shot);
                }

                // 诊断日志：输出片段信息
                _logger.LogInformation("片段创建: ID={ClipId}, StartTime={StartTime}s, Duration={Duration}s, " +
                    "PixelPosition={PixelPosition}px, PixelWidth={PixelWidth}px, ThumbnailPath={ThumbnailPath}",
                    clip.Id, clip.StartTime, clip.Duration, clip.PixelPosition, clip.PixelWidth, clip.ThumbnailPath);

                track.Clips.Add(clip);
            }

            Tracks.Add(track);
        }

        // 生成时间标记
        GenerateTimeMarkers();

        // 诊断日志：输出时间轴尺寸信息
        _logger.LogInformation("时间轴尺寸: TimelineWidth={TimelineWidth}px, TracksHeight={TracksHeight}px, " +
            "Tracks.Count={TrackCount}, TotalDuration={TotalDuration}s, PixelsPerSecond={PixelsPerSecond}",
            TimelineWidth, TracksHeight, Tracks.Count, TotalDuration, PixelsPerSecond);

        _logger.LogInformation("时间轴构建完成: {TrackCount} 个轨道, 总时长 {Duration:F2}s",
            Tracks.Count, TotalDuration);
    }

    private IReadOnlyList<ShotItem> GetShotsSnapshot()
    {
        if (_shotsCache.Count > 0)
        {
            return _shotsCache;
        }

        var query = new GetAllShotsQuery();
        _messenger.Send(query);

        _shotsCache = query.Shots?.ToList() ?? new List<ShotItem>();
        return _shotsCache;
    }

    private static Dictionary<string, ShotItem> BuildShotLookup(IEnumerable<ShotItem> shots)
    {
        var lookup = new Dictionary<string, ShotItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var shot in shots)
        {
            var key = NormalizePath(shot.GeneratedVideoPath);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            lookup[key] = shot;
        }

        return lookup;
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var normalized = System.IO.Path.GetFullPath(path);
            return normalized.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar)
                             .ToLowerInvariant();
        }
        catch
        {
            return path.Trim().ToLowerInvariant();
        }
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
    /// 格式化时间显示（带毫秒）
    /// </summary>
    private string FormatTimeWithMilliseconds(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        var milliseconds = (int)(ts.Milliseconds / 10); // 显示两位毫秒（百分之一秒）

        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{milliseconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}.{milliseconds:D2}";
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

    /// <summary>
    /// 轨道集合变化时重新计算布局
    /// </summary>
    partial void OnTracksChanged(ObservableCollection<TimelineTrack> value)
    {
        RecalculateTrackPositions();
        OnPropertyChanged(nameof(TracksHeight));
    }

    /// <summary>
    /// 重新计算轨道垂直位置
    /// </summary>
    private void RecalculateTrackPositions()
    {
        var currentY = 0.0;
        foreach (var track in Tracks)
        {
            track.VerticalOffset = currentY;
            currentY += track.Height;
        }
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
        if (message.Clip == null)
        {
            if (SelectedClip != null)
            {
                SelectedClip.IsSelected = false;
            }
            SelectedClip = null;
            return;
        }

        if (SelectedClip != null && SelectedClip != message.Clip)
        {
            SelectedClip.IsSelected = false;
        }

        SelectedClip = message.Clip;
        SelectedClip.IsSelected = true;
        _logger.LogInformation("片段已选中: {ClipId}", SelectedClip.Id);

        // 加载并播放选中片段的视频
        LoadClipVideo(SelectedClip);
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

    #region 拖动相关方法

    /// <summary>
    /// 开始拖动片段
    /// </summary>
    public void BeginClipDrag(TimelineClip clip)
    {
        if (clip == null) return;

        // 暂停自动保存
        _autoSaveTimer?.Stop();

        clip.IsDragging = true;
        clip.DragOffsetX = clip.PixelPosition;

        // 显示幽灵预览
        IsGhostPreviewVisible = true;
        GhostPreviewX = clip.PixelPosition;
        GhostPreviewWidth = clip.PixelWidth;
        GhostPreviewThumbnail = clip.ThumbnailPath;
        GhostPreviewLabel = clip.ShotNumber == 0 ? "原视频" : $"镜头 {clip.ShotNumber}";

        // 计算垂直位置（找到片段所在轨道的垂直偏移）
        var track = Tracks.FirstOrDefault(t => t.Id == clip.TrackId);
        GhostPreviewY = track != null ? track.VerticalOffset + 8 : 8;

        _logger.LogDebug("开始拖动片段: {ClipId}", clip.Id);
        _messenger.Send(new ClipDragStartedMessage(clip));
    }

    /// <summary>
    /// 更新拖动位置
    /// </summary>
    public void UpdateClipDragPosition(TimelineClip clip, double newPixelPosition)
    {
        if (clip == null || !clip.IsDragging) return;

        var track = Tracks.FirstOrDefault(t => t.Id == clip.TrackId);
        if (track == null)
        {
            _logger.LogWarning("找不到片段所在的轨道: {ClipId}", clip.Id);
            return;
        }

        // 确保位置不为负
        newPixelPosition = Math.Max(0, newPixelPosition);

        // 获取吸附后的位置
        var snappedPosition = _interactionService.GetSnappedPosition(
            newPixelPosition,
            clip,
            track,
            PlayheadTime,
            threshold: 5.0);

        // 检查是否发生了吸附
        bool isSnapped = Math.Abs(snappedPosition - newPixelPosition) > 0.1;

        // 显示吸附线和反馈
        if (isSnapped)
        {
            SnapLinePosition = snappedPosition;

            // 确定吸附到了什么
            var snappedTime = snappedPosition / PixelsPerSecond;
            string snapTarget = "";

            // 检查是否吸附到播放头
            if (Math.Abs(snappedTime - PlayheadTime) < 0.01)
            {
                snapTarget = $"吸附到: 播放头 ({FormatTimeWithMilliseconds(PlayheadTime)})";
            }
            // 检查是否吸附到时间轴起点
            else if (Math.Abs(snappedTime) < 0.01)
            {
                snapTarget = "吸附到: 时间轴起点 (00:00.00)";
            }
            // 检查是否吸附到其他片段
            else
            {
                foreach (var otherClip in track.Clips)
                {
                    if (otherClip.Id == clip.Id) continue;

                    if (Math.Abs(snappedTime - otherClip.StartTime) < 0.01)
                    {
                        var label = otherClip.ShotNumber == 0 ? "原视频" : $"镜头 {otherClip.ShotNumber}";
                        snapTarget = $"吸附到: {label} 开始";
                        break;
                    }
                    else if (Math.Abs(snappedTime - otherClip.EndTime) < 0.01)
                    {
                        var label = otherClip.ShotNumber == 0 ? "原视频" : $"镜头 {otherClip.ShotNumber}";
                        snapTarget = $"吸附到: {label} 结束";
                        break;
                    }
                }
            }

            // 显示吸附反馈
            if (!string.IsNullOrEmpty(snapTarget))
            {
                IsSnapFeedbackVisible = true;
                SnapFeedbackText = snapTarget;
                SnapFeedbackX = snappedPosition;
                SnapFeedbackY = 60; // 在拖动提示框下方
            }
        }
        else
        {
            SnapLinePosition = null;
            IsSnapFeedbackVisible = false;
        }

        // 转换为时间
        var newStartTime = snappedPosition / PixelsPerSecond;

        // 更新拖动提示框
        IsDragTooltipVisible = true;
        DragTooltipX = snappedPosition;
        DragTooltipY = 100; // 固定在顶部附近
        DragTooltipTime = FormatTimeWithMilliseconds(newStartTime);

        // 更新幽灵预览位置
        GhostPreviewX = snappedPosition;

        // 检查碰撞
        if (!_interactionService.CanMoveClip(clip, newStartTime, track))
        {
            // 保持在当前拖动位置，不更新
            return;
        }

        // 更新视觉位置（不修改实际StartTime）
        clip.DragOffsetX = snappedPosition;
    }

    /// <summary>
    /// 结束拖动片段
    /// </summary>
    public async Task EndClipDrag(TimelineClip clip, double finalPixelPosition)
    {
        if (clip == null || !clip.IsDragging)
        {
            return;
        }

        // 隐藏吸附线和提示框
        SnapLinePosition = null;
        IsDragTooltipVisible = false;
        IsGhostPreviewVisible = false;
        IsSnapFeedbackVisible = false;

        var track = Tracks.FirstOrDefault(t => t.Id == clip.TrackId);
        if (track == null)
        {
            _logger.LogError("找不到片段所在的轨道: {ClipId}", clip.Id);
            clip.IsDragging = false;
            _autoSaveTimer?.Start();
            return;
        }

        // 计算最终时间
        var finalStartTime = finalPixelPosition / PixelsPerSecond;
        var oldStartTime = clip.StartTime;

        // 如果位置没有变化，直接取消拖动
        if (Math.Abs(finalStartTime - oldStartTime) < 0.01)
        {
            clip.IsDragging = false;
            _autoSaveTimer?.Start();
            return;
        }

        // 再次检查碰撞
        if (!_interactionService.CanMoveClip(clip, finalStartTime, track))
        {
            _logger.LogWarning("片段无法移动到目标位置: {Time:F2}s", finalStartTime);
            clip.IsDragging = false;
            clip.DragOffsetX = clip.PixelPosition;
            _autoSaveTimer?.Start();
            return;
        }

        // 更新片段的实际StartTime
        clip.StartTime = finalStartTime;
        clip.IsDragging = false;

        // 更新DraftContent
        if (_draftContent != null)
        {
            try
            {
                DraftAdapter.UpdateSegmentPosition(_draftContent, clip.Id.ToString("N").ToUpper(), finalStartTime);
                _isDirty = true;

                _logger.LogInformation("片段拖动完成: {ClipId}, 从 {OldTime:F2}s 移动到 {NewTime:F2}s",
                    clip.Id, oldStartTime, finalStartTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "更新DraftContent失败");
                // 回滚
                clip.StartTime = oldStartTime;
            }
        }

        // 重启自动保存
        _autoSaveTimer?.Start();

        // 保存草稿
        await SaveDraftAsync();

        _messenger.Send(new ClipMovedMessage(clip, oldStartTime, finalStartTime));
    }

    /// <summary>
    /// 取消拖动
    /// </summary>
    public void CancelClipDrag(TimelineClip clip)
    {
        if (clip == null || !clip.IsDragging) return;

        clip.IsDragging = false;
        clip.DragOffsetX = clip.PixelPosition;

        // 重启自动保存
        _autoSaveTimer?.Start();

        _logger.LogDebug("取消拖动片段: {ClipId}", clip.Id);
    }

    #endregion
}
