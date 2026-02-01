using Avalonia.Threading;
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
using System.Globalization;
using System.Collections.ObjectModel;
using System.IO;
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
    private const double MinClipDurationSeconds = 0.1;
    private double _playbackBaseTime;
    private bool _isAutoAdvancing;
    private const double AutoAdvanceThresholdSeconds = 0.05;
    private Guid? _loadedClipId;
    private double? _loadedMediaStartSeconds;
    private LibVLCSharp.Shared.MediaPlayer? _autoAdvancePlayer;
    private double? _pendingSeekSeconds;
    private Guid? _pendingSeekClipId;
    private bool _isApplyingPendingSeek;
    private DispatcherTimer? _seekRetryTimer;
    private int _seekRetryCount;
    private const int MaxSeekRetries = 50;
    private const double SeekToleranceSeconds = 0.05;

    // 撤销/重做栈
    private readonly Stack<ITimelineAction> _undoStack = new();
    private readonly Stack<ITimelineAction> _redoStack = new();
    private const int MaxUndoStackSize = 50;

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

        // 订阅 Tracks 集合变化，确保 TracksHeight 更新
        Tracks.CollectionChanged += (s, e) =>
        {
            RecalculateTrackPositions();
            OnPropertyChanged(nameof(TracksHeight));
        };

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
                var newPlayheadTime = _playbackBaseTime + Playback.State.CurrentTime;
                PlayheadTime = Math.Clamp(newPlayheadTime, 0, TotalDuration);
                PlayheadPosition = PlayheadTime * PixelsPerSecond;
                TryAutoAdvancePlayback();
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
        var hasOriginalShot = shots.Any(s => s.ShotNumber == 0 && !string.IsNullOrWhiteSpace(s.GeneratedVideoPath));

        // 提取时间轴信息
        var timelineInfo = DraftAdapter.ExtractTimelineInfo(_draftContent);

        // 更新总时长
        TotalDuration = timelineInfo.TotalDurationSeconds;
        TimelineWidth = TotalDuration * PixelsPerSecond;
        Playback.State.TotalDuration = TotalDuration;

        // 创建轨道
        foreach (var trackInfo in timelineInfo.Tracks)
        {
            if (trackInfo.Segments.Count == 0)
            {
                continue;
            }

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

            // 先添加轨道到 Tracks，然后再添加片段
            Tracks.Add(track);

            // 创建片段
            foreach (var segmentInfo in trackInfo.Segments)
            {
                var sourceStart = segmentInfo.SourceStartSeconds;
                var sourceDuration = segmentInfo.SourceDurationSeconds > 0
                    ? segmentInfo.SourceDurationSeconds
                    : segmentInfo.DurationSeconds;

                var clip = new TimelineClip
                {
                    Id = Guid.Parse(segmentInfo.Id),
                    TrackId = track.Id,
                    StartTime = segmentInfo.StartTimeSeconds,
                    Duration = segmentInfo.DurationSeconds,
                    SourceStart = sourceStart,
                    SourceDuration = sourceDuration,
                    PixelsPerSecond = PixelsPerSecond,
                    Status = ClipStatus.Generated,
                    VideoPath = segmentInfo.VideoPath,
                    ThumbnailPath = null
                };

                // 初始化拖动位置
                clip.DragOffsetX = clip.PixelPosition;

                var normalizedPath = NormalizePath(segmentInfo.VideoPath);
                if (!string.IsNullOrEmpty(normalizedPath) && shotLookup.TryGetValue(normalizedPath, out var shot))
                {
                    clip.ShotNumber = shot.ShotNumber;
                    clip.ThumbnailPath = shot.FirstFrameImagePath;
                    clip.Status = TimelineClip.GetStatusFromShot(shot);
                }
                else if (!hasOriginalShot)
                {
                    continue;
                }

                // 诊断日志：输出片段信息
                _logger.LogInformation("片段创建: ID={ClipId}, StartTime={StartTime}s, Duration={Duration}s, " +
                    "PixelPosition={PixelPosition}px, PixelWidth={PixelWidth}px, ThumbnailPath={ThumbnailPath}",
                    clip.Id, clip.StartTime, clip.Duration, clip.PixelPosition, clip.PixelWidth, clip.ThumbnailPath);

                track.Clips.Add(clip);
            }

            if (track.Clips.Count == 0)
            {
                Tracks.Remove(track);
                continue;
            }

            // 诊断日志：输出轨道和片段信息
            _logger.LogInformation("轨道添加: TrackId={TrackId}, Name={Name}, ClipsCount={ClipsCount}",
                track.Id, track.Name, track.Clips.Count);
        }

        // 生成时间标记
        GenerateTimeMarkers();

        // 诊断日志：验证 Tracks 集合
        _logger.LogInformation("Tracks 集合验证: Count={Count}", Tracks.Count);
        foreach (var t in Tracks)
        {
            _logger.LogInformation("  轨道: {Name}, Clips={ClipsCount}", t.Name, t.Clips.Count);
            foreach (var c in t.Clips)
            {
                _logger.LogInformation("    片段: StartTime={StartTime}s, Duration={Duration}s, PixelPos={PixelPos}px, PixelWidth={PixelWidth}px",
                    c.StartTime, c.Duration, c.PixelPosition, c.PixelWidth);
            }
        }

        // 诊断日志：输出时间轴尺寸信息
        _logger.LogInformation("时间轴尺寸: TimelineWidth={TimelineWidth}px, TracksHeight={TracksHeight}px, " +
            "Tracks.Count={TrackCount}, TotalDuration={TotalDuration}s, PixelsPerSecond={PixelsPerSecond}",
            TimelineWidth, TracksHeight, Tracks.Count, TotalDuration, PixelsPerSecond);

        _logger.LogInformation("时间轴构建完成: {TrackCount} 个轨道, 总时长 {Duration:F2}s",
            Tracks.Count, TotalDuration);

        _playbackBaseTime = 0;
        _loadedClipId = null;
        _loadedMediaStartSeconds = null;
        PlayheadTime = 0;
        PlayheadPosition = 0;
        Playback.State.CurrentTime = 0;

        // 手动触发布局更新，确保 TracksHeight 通知到 UI
        RecalculateTrackPositions();
        OnPropertyChanged(nameof(TracksHeight));

        _logger.LogInformation("布局更新完成: TracksHeight={TracksHeight}px", TracksHeight);
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
        var desiredPlayhead = Math.Clamp(time, 0, TotalDuration);
        PlayheadTime = desiredPlayhead;
        PlayheadPosition = PlayheadTime * PixelsPerSecond;

        var clipAtTime = FindClipAtTime(PlayheadTime);
        if (clipAtTime == null)
        {
            if (SelectedClip != null &&
                PlayheadTime >= SelectedClip.StartTime &&
                PlayheadTime <= SelectedClip.EndTime &&
                IsClipPlayable(SelectedClip))
            {
                if (!IsClipMediaLoaded(SelectedClip))
                {
                    var mediaStartSeconds = CalcMediaSeconds(SelectedClip, PlayheadTime);
                    LoadClipVideo(SelectedClip, mediaStartSeconds);
                    PlayheadTime = desiredPlayhead;
                    PlayheadPosition = PlayheadTime * PixelsPerSecond;
                }

                _playbackBaseTime = SelectedClip.StartTime;
                var mediaSeconds = CalcMediaSeconds(SelectedClip, PlayheadTime);
                QueuePendingSeek(mediaSeconds, SelectedClip.Id);
                if (TrySeekMedia(mediaSeconds))
                {
                    ClearPendingSeek();
                }
            }

            _messenger.Send(new PlayheadPositionChangedMessage(PlayheadTime, PlayheadPosition));
            return;
        }

        if (clipAtTime != null)
        {
            if (SelectedClip != clipAtTime)
            {
                if (SelectedClip != null)
                {
                    SelectedClip.IsSelected = false;
                }

                SelectedClip = clipAtTime;
                SelectedClip.IsSelected = true;
            }

            if (IsClipPlayable(clipAtTime))
            {
                if (!IsClipMediaLoaded(clipAtTime))
                {
                    var mediaStartSeconds = CalcMediaSeconds(clipAtTime, PlayheadTime);
                    LoadClipVideo(clipAtTime, mediaStartSeconds);
                    PlayheadTime = desiredPlayhead;
                    PlayheadPosition = PlayheadTime * PixelsPerSecond;
                }

                _playbackBaseTime = clipAtTime.StartTime;
                var mediaSeconds = CalcMediaSeconds(clipAtTime, PlayheadTime);
                QueuePendingSeek(mediaSeconds, clipAtTime.Id);
                if (TrySeekMedia(mediaSeconds))
                {
                    ClearPendingSeek();
                }
            }
        }

        _messenger.Send(new PlayheadPositionChangedMessage(PlayheadTime, PlayheadPosition));
    }

    [RelayCommand]
    private void PlayFromTimeline()
    {
        var desiredPlayhead = PlayheadTime;
        var clip = SelectedClip;
        var clipAtPlayhead = FindClipAtTime(PlayheadTime);
        if (clipAtPlayhead != null)
        {
            if (clip == null || PlayheadTime < clip.StartTime || PlayheadTime >= clip.EndTime)
            {
                clip = clipAtPlayhead;
            }
        }
        else
        {
            var nextClip = FindNextPlayableClipAfterTime(PlayheadTime);
            if (nextClip != null)
            {
                clip = nextClip;
                desiredPlayhead = nextClip.StartTime;
            }
        }

        if (clip == null)
        {
            clip = FindFirstPlayableClip();
        }

        if (clip == null)
        {
            return;
        }

        if (!EnsureClipLoadedForPlayback(clip, desiredPlayhead))
        {
            var fallback = FindFirstPlayableClip();
            if (fallback == null || !EnsureClipLoadedForPlayback(fallback, fallback.StartTime))
            {
                return;
            }

            clip = fallback;
            desiredPlayhead = clip.StartTime;
        }

        EnsurePlaybackStart(clip, desiredPlayhead);
        Playback.PlayCommand.Execute(null);
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

        _logger.LogInformation("OnTracksChanged 触发: Tracks.Count={Count}", value?.Count ?? 0);
        if (value != null)
        {
            foreach (var track in value)
            {
                _logger.LogInformation("  轨道: {Name}, Clips.Count={ClipsCount}", track.Name, track.Clips.Count);
            }
        }
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

    private TimelineClip? FindClipAtTime(double time)
    {
        if (Tracks.Count == 0)
        {
            return null;
        }

        foreach (var track in Tracks.OrderBy(t => t.Order))
        {
            var clip = track.Clips.FirstOrDefault(c => time >= c.StartTime && time < c.EndTime && IsClipPlayable(c));
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private TimelineClip? FindFirstPlayableClip()
    {
        foreach (var track in Tracks.OrderBy(t => t.Order))
        {
            var clip = track.Clips.OrderBy(c => c.StartTime).FirstOrDefault(IsClipPlayable);
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private TimelineClip? FindNextClip(TimelineClip current)
    {
        var track = FindTrackForClip(current);
        if (track == null)
        {
            return null;
        }

        return track.Clips
            .Where(c => c.StartTime > current.StartTime && IsClipPlayable(c))
            .OrderBy(c => c.StartTime)
            .FirstOrDefault();
    }

    private TimelineClip? FindNextPlayableClipAfterTime(double time)
    {
        return Tracks
            .SelectMany(t => t.Clips)
            .Where(IsClipPlayable)
            .Where(c => c.StartTime > time)
            .OrderBy(c => c.StartTime)
            .FirstOrDefault();
    }

    private bool IsClipPlayable(TimelineClip clip)
    {
        return !string.IsNullOrWhiteSpace(clip.VideoPath) && File.Exists(clip.VideoPath);
    }

    private bool IsClipMediaLoaded(TimelineClip clip)
    {
        if (_loadedClipId != clip.Id)
        {
            return false;
        }

        var mediaPlayer = Playback.GetMediaPlayer();
        return mediaPlayer?.Media != null;
    }

    private double CalcMediaSeconds(TimelineClip clip, double timelineSeconds)
    {
        var local = timelineSeconds - clip.StartTime;
        local = Math.Clamp(local, 0, Math.Max(0, clip.Duration - 0.001));

        var media = clip.SourceStart + local;
        var maxMedia = clip.SourceStart + Math.Max(0, clip.SourceDuration - 0.001);
        media = Math.Clamp(media, clip.SourceStart, maxMedia);

        return media;
    }

    private bool EnsureClipLoadedForPlayback(TimelineClip clip, double? desiredPlayhead = null)
    {
        if (SelectedClip != clip)
        {
            if (SelectedClip != null)
            {
                SelectedClip.IsSelected = false;
            }

            SelectedClip = clip;
            SelectedClip.IsSelected = true;
        }

        if (_loadedClipId == clip.Id)
        {
            var mediaPlayer = Playback.GetMediaPlayer();
            if (mediaPlayer?.Media != null)
            {
                if (!Playback.State.IsPlaying && desiredPlayhead.HasValue)
                {
                    var desiredPlayheadValue = desiredPlayhead.Value;
                    if (desiredPlayheadValue >= clip.StartTime && desiredPlayheadValue < clip.EndTime)
                    {
                        var desiredMediaStartSeconds = CalcMediaSeconds(clip, desiredPlayheadValue);
                        if (!_loadedMediaStartSeconds.HasValue ||
                            Math.Abs(_loadedMediaStartSeconds.Value - desiredMediaStartSeconds) > SeekToleranceSeconds)
                        {
                            LoadClipVideo(clip, desiredMediaStartSeconds);
                        }
                    }
                }

                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(clip.VideoPath) || !System.IO.File.Exists(clip.VideoPath))
        {
            _logger.LogWarning("片段视频路径无效: {Path}", clip.VideoPath);
            return false;
        }

        double? mediaStartSeconds = null;
        var targetPlayhead = desiredPlayhead;
        if (targetPlayhead.HasValue)
        {
            if (targetPlayhead.Value >= clip.StartTime && targetPlayhead.Value < clip.EndTime)
            {
                mediaStartSeconds = CalcMediaSeconds(clip, targetPlayhead.Value);
            }
        }
        else if (PlayheadTime >= clip.StartTime && PlayheadTime < clip.EndTime)
        {
            mediaStartSeconds = CalcMediaSeconds(clip, PlayheadTime);
        }
        else
        {
            mediaStartSeconds = CalcMediaSeconds(clip, clip.StartTime);
        }

        LoadClipVideo(clip, mediaStartSeconds);
        return true;
    }

    private void EnsurePlaybackStart(TimelineClip clip, double? desiredPlayhead = null)
    {
        if (clip.Duration <= 0)
        {
            return;
        }

        var threshold = Math.Max(AutoAdvanceThresholdSeconds, 0.01);
        var targetPlayhead = desiredPlayhead ?? PlayheadTime;
        var needsReset = targetPlayhead < clip.StartTime || targetPlayhead >= clip.EndTime - threshold;

        _playbackBaseTime = clip.StartTime;

        if (needsReset)
        {
            targetPlayhead = clip.StartTime;
            PlayheadTime = targetPlayhead;
            PlayheadPosition = PlayheadTime * PixelsPerSecond;
            ClearPendingSeek();
            StopSeekRetry();
            Playback.SeekTo(0);
            return;
        }

        PlayheadTime = targetPlayhead;
        PlayheadPosition = PlayheadTime * PixelsPerSecond;
        var mediaSeconds = CalcMediaSeconds(clip, targetPlayhead);
        QueuePendingSeek(mediaSeconds, clip.Id);
        StopSeekRetry();
        StartSeekRetry();
    }

    private void TryAutoAdvancePlayback(bool force = false)
    {
        if (_isAutoAdvancing)
        {
            return;
        }

        if (!Playback.State.IsPlaying || Playback.State.TotalDuration <= 0)
        {
            return;
        }

        if (!force && Playback.State.CurrentTime < Playback.State.TotalDuration - AutoAdvanceThresholdSeconds)
        {
            return;
        }

        if (SelectedClip == null)
        {
            return;
        }

        var nextClip = FindNextClip(SelectedClip);
        if (nextClip == null)
        {
            _playbackBaseTime = SelectedClip.EndTime;
            Playback.StopPlayback();
            ClearPendingSeek();
            return;
        }

        _isAutoAdvancing = true;
        try
        {
            if (!EnsureClipLoadedForPlayback(nextClip, nextClip.StartTime))
            {
                Playback.StopPlayback();
                return;
            }

            Playback.PlayCommand.Execute(null);
        }
        finally
        {
            _isAutoAdvancing = false;
        }
    }

    private void EnsureAutoAdvanceHook(LibVLCSharp.Shared.MediaPlayer player)
    {
        if (_autoAdvancePlayer == player)
        {
            return;
        }

        if (_autoAdvancePlayer != null)
        {
            _autoAdvancePlayer.EndReached -= OnMediaEndReached;
            _autoAdvancePlayer.Playing -= OnMediaPlaying;
            _autoAdvancePlayer.LengthChanged -= OnMediaLengthChanged;
            _autoAdvancePlayer.SeekableChanged -= OnMediaSeekableChanged;
        }

        _autoAdvancePlayer = player;
        _autoAdvancePlayer.EndReached += OnMediaEndReached;
        _autoAdvancePlayer.Playing += OnMediaPlaying;
        _autoAdvancePlayer.LengthChanged += OnMediaLengthChanged;
        _autoAdvancePlayer.SeekableChanged += OnMediaSeekableChanged;
    }

    private void OnMediaEndReached(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => TryAutoAdvancePlayback(force: true));
    }

    private void OnMediaPlaying(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(ApplyPendingSeekIfAny);
    }

    private void OnMediaLengthChanged(object? sender, LibVLCSharp.Shared.MediaPlayerLengthChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(ApplyPendingSeekIfAny);
    }

    private void OnMediaSeekableChanged(object? sender, LibVLCSharp.Shared.MediaPlayerSeekableChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(ApplyPendingSeekIfAny);
    }

    private void QueuePendingSeek(double seconds, Guid clipId)
    {
        _pendingSeekSeconds = seconds;
        _pendingSeekClipId = clipId;
        StartSeekRetry();
    }

    private void ClearPendingSeek()
    {
        _pendingSeekSeconds = null;
        _pendingSeekClipId = null;
        StopSeekRetry();
    }

    private void ApplyPendingSeekIfAny()
    {
        if (_isApplyingPendingSeek || _pendingSeekSeconds == null)
        {
            return;
        }

        if (_pendingSeekClipId.HasValue && _loadedClipId.HasValue && _pendingSeekClipId != _loadedClipId)
        {
            return;
        }

        _isApplyingPendingSeek = true;
        try
        {
            if (TrySeekMedia(_pendingSeekSeconds.Value))
            {
                ClearPendingSeek();
            }
        }
        finally
        {
            _isApplyingPendingSeek = false;
        }
    }

    private void StartSeekRetry()
    {
        if (_pendingSeekSeconds == null)
        {
            return;
        }

        _seekRetryCount = 0;
        if (_seekRetryTimer == null)
        {
            _seekRetryTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(60), DispatcherPriority.Background, (s, e) =>
            {
                if (_pendingSeekSeconds == null)
                {
                    StopSeekRetry();
                    return;
                }

                var mediaPlayer = Playback.GetMediaPlayer();
                if (mediaPlayer != null && mediaPlayer.Length > 0)
                {
                    var current = mediaPlayer.Time / 1000.0;
                    if (Math.Abs(current - _pendingSeekSeconds.Value) <= SeekToleranceSeconds)
                    {
                        ClearPendingSeek();
                        return;
                    }
                }

                TrySeekMedia(_pendingSeekSeconds.Value);

                _seekRetryCount++;
                if (_seekRetryCount >= MaxSeekRetries)
                {
                    ClearPendingSeek();
                }
            });
        }

        _seekRetryTimer.Start();
    }

    private void StopSeekRetry()
    {
        if (_seekRetryTimer != null)
        {
            _seekRetryTimer.Stop();
        }
        _seekRetryCount = 0;
    }

    private bool TrySeekMedia(double seconds)
    {
        var mediaPlayer = Playback.GetMediaPlayer();
        if (mediaPlayer == null)
        {
            return false;
        }

        try
        {
            var targetMs = (long)(Math.Max(0, seconds) * 1000);

            if (mediaPlayer.Length > 0)
            {
                var lengthSeconds = mediaPlayer.Length / 1000.0;
                if (lengthSeconds > 0)
                {
                    var position = Math.Clamp(seconds / lengthSeconds, 0, 1);
                    mediaPlayer.Position = (float)position;
                }
            }

            mediaPlayer.Time = targetMs;

            var currentSeconds = mediaPlayer.Time / 1000.0;
            return Math.Abs(currentSeconds - seconds) <= SeekToleranceSeconds;
        }
        catch
        {
            return false;
        }
    }

    private TimelineTrack? FindTrackForClip(TimelineClip clip)
    {
        return Tracks.FirstOrDefault(t => t.Id == clip.TrackId);
    }

    private void UpdateTimelineDurationFromTracks()
    {
        var maxEndTime = 0.0;
        foreach (var track in Tracks)
        {
            foreach (var clip in track.Clips)
            {
                if (clip.EndTime > maxEndTime)
                {
                    maxEndTime = clip.EndTime;
                }
            }
        }

        TotalDuration = maxEndTime;
        TimelineWidth = TotalDuration * PixelsPerSecond;
        Playback.State.TotalDuration = TotalDuration;
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
        if (message.Clip == null)
        {
            if (SelectedClip != null)
            {
                SelectedClip.IsSelected = false;
            }
            SelectedClip = null;
            _playbackBaseTime = 0;
            _loadedClipId = null;
            _loadedMediaStartSeconds = null;
            ClearPendingSeek();
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
        var clipStartSeconds = SelectedClip != null
            ? (PlayheadTime >= SelectedClip.StartTime && PlayheadTime < SelectedClip.EndTime
                ? CalcMediaSeconds(SelectedClip, PlayheadTime)
                : CalcMediaSeconds(SelectedClip, SelectedClip.StartTime))
            : (double?)null;

        LoadClipVideo(SelectedClip, clipStartSeconds);
    }

    /// <summary>
    /// 加载片段视频
    /// </summary>
    private void LoadClipVideo(TimelineClip clip, double? mediaStartSeconds = null)
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
            var startSeconds = mediaStartSeconds ?? clip.SourceStart;
            if (startSeconds > 0)
            {
                media.AddOption($":start-time={startSeconds.ToString(CultureInfo.InvariantCulture)}");
            }
            mediaPlayer.Media = media;
            EnsureAutoAdvanceHook(mediaPlayer);

            // 更新播放状态
            Playback.State.TotalDuration = clip.Duration;
            _playbackBaseTime = clip.StartTime;
            _loadedClipId = clip.Id;
            _loadedMediaStartSeconds = startSeconds;
            if (PlayheadTime < clip.StartTime || PlayheadTime >= clip.EndTime)
            {
                PlayheadTime = clip.StartTime;
                PlayheadPosition = PlayheadTime * PixelsPerSecond;
                Playback.State.CurrentTime = 0;
            }
            ApplyPendingSeekIfAny();

            _logger.LogInformation("加载视频: {Path}, 时长: {Duration:F2}s, Start: {Start:F3}s", clip.VideoPath, clip.Duration, startSeconds);
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

    #region TrimAndSplit

    private bool IsClipRangeAvailable(TimelineTrack track, TimelineClip clip, double newStartTime, double newDuration)
    {
        if (newStartTime < 0 || newDuration < MinClipDurationSeconds)
        {
            return false;
        }

        var newEndTime = newStartTime + newDuration;
        foreach (var otherClip in track.Clips)
        {
            if (otherClip.Id == clip.Id)
            {
                continue;
            }

            if (newStartTime < otherClip.EndTime && otherClip.StartTime < newEndTime)
            {
                return false;
            }
        }

        return true;
    }

    public void BeginClipTrim(TimelineClip clip)
    {
        if (clip == null)
        {
            return;
        }

        _autoSaveTimer?.Stop();
        clip.IsBeingTrimmed = true;
    }

    public bool TryPreviewClipTrim(
        TimelineClip clip,
        double newStartTime,
        double newDuration,
        double newSourceStart,
        double newSourceDuration)
    {
        if (clip == null)
        {
            return false;
        }

        var track = FindTrackForClip(clip);
        if (track == null)
        {
            return false;
        }

        if (newDuration < MinClipDurationSeconds ||
            newSourceDuration < MinClipDurationSeconds ||
            newStartTime < 0 ||
            newSourceStart < 0)
        {
            return false;
        }

        if (!IsClipRangeAvailable(track, clip, newStartTime, newDuration))
        {
            return false;
        }

        clip.StartTime = newStartTime;
        clip.Duration = newDuration;
        clip.SourceStart = newSourceStart;
        clip.SourceDuration = newSourceDuration;
        clip.DragOffsetX = clip.PixelPosition;
        return true;
    }

    public async Task EndClipTrim(
        TimelineClip clip,
        double oldStartTime,
        double oldDuration,
        double oldSourceStart,
        double oldSourceDuration)
    {
        if (clip == null)
        {
            return;
        }

        clip.IsBeingTrimmed = false;

        if (Math.Abs(clip.StartTime - oldStartTime) < 0.0001 &&
            Math.Abs(clip.Duration - oldDuration) < 0.0001)
        {
            _autoSaveTimer?.Start();
            return;
        }

        if (_draftContent != null)
        {
            try
            {
                var updated = DraftAdapter.UpdateSegmentTrim(
                    _draftContent,
                    clip.Id.ToString("N").ToUpper(),
                    clip.StartTime,
                    clip.Duration,
                    clip.SourceStart,
                    clip.SourceDuration);

                if (!updated)
                {
                    throw new InvalidOperationException("Segment not found for trim.");
                }

                _isDirty = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update DraftContent trim");
                clip.StartTime = oldStartTime;
                clip.Duration = oldDuration;
                clip.SourceStart = oldSourceStart;
                clip.SourceDuration = oldSourceDuration;
                clip.DragOffsetX = clip.PixelPosition;
                _autoSaveTimer?.Start();
                return;
            }
        }

        UpdateTimelineDurationFromTracks();
        _autoSaveTimer?.Start();
        await SaveDraftAsync();

        _messenger.Send(new ClipTrimmedMessage(clip, oldDuration, clip.Duration));
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            _logger.LogInformation("撤销栈为空，无法撤销");
            return;
        }

        var action = _undoStack.Pop();
        _logger.LogInformation("执行撤销操作: {Description}", action.Description);

        action.Undo();
        _redoStack.Push(action);

        _logger.LogInformation("撤销操作完成，撤销栈剩余: {Count}", _undoStack.Count);
        _isDirty = true;

        // 通知命令状态变化
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool CanUndo() => _undoStack.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            _logger.LogInformation("重做栈为空，无法重做");
            return;
        }

        var action = _redoStack.Pop();
        _logger.LogInformation("执行重做操作: {Description}", action.Description);

        action.Execute();
        _undoStack.Push(action);

        _logger.LogInformation("重做操作完成，重做栈剩余: {Count}", _redoStack.Count);
        _isDirty = true;

        // 通知命令状态变化
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool CanRedo() => _redoStack.Count > 0;

    /// <summary>
    /// 添加操作到撤销栈
    /// </summary>
    private void PushUndoAction(ITimelineAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear(); // 新操作会清空重做栈

        _logger.LogInformation("添加操作到撤销栈: {Description}，当前栈大小: {Count}", action.Description, _undoStack.Count);

        // 限制撤销栈大小
        if (_undoStack.Count > MaxUndoStackSize)
        {
            var items = _undoStack.ToList();
            items.RemoveAt(items.Count - 1);
            _undoStack.Clear();
            foreach (var item in items.AsEnumerable().Reverse())
            {
                _undoStack.Push(item);
            }
            _logger.LogInformation("撤销栈已达到最大值，移除最旧的操作");
        }

        // 通知命令状态变化
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task DeleteSelectedClips()
    {
        if (SelectedClip == null && SelectedClips.Count == 0)
        {
            _logger.LogInformation("没有选中的片段可删除");
            return;
        }

        try
        {
            var clipsToDelete = SelectedClips.Count > 0
                ? SelectedClips.ToList()
                : new List<TimelineClip> { SelectedClip! };

            // 创建片段快照用于撤销
            var snapshots = clipsToDelete.Select(ClipSnapshot.FromClip).ToList();

            foreach (var clip in clipsToDelete)
            {
                // 从轨道中移除片段
                var track = Tracks.FirstOrDefault(t => t.Clips.Contains(clip));
                if (track != null)
                {
                    track.Clips.Remove(clip);
                    _logger.LogInformation("从轨道 {TrackName} 删除片段: {ClipId}", track.Name, clip.Id);
                }

                // 从草稿中移除对应的 segment
                if (_draftContent?.Tracks != null)
                {
                    foreach (var draftTrack in _draftContent.Tracks)
                    {
                        var segment = draftTrack.Segments?.FirstOrDefault(s => s.Id == clip.Id.ToString());
                        if (segment != null)
                        {
                            draftTrack.Segments?.Remove(segment);
                            _logger.LogInformation("从草稿轨道删除 segment: {SegmentId}", segment.Id);
                        }
                    }
                }
            }

            // 清空选中状态
            SelectedClip = null;
            SelectedClips.Clear();

            // 重新计算时间轴
            RecalculateTrackPositions();
            _isDirty = true;

            // 添加到撤销栈
            var deleteAction = new DeleteClipsAction(snapshots, RestoreDeletedClips);
            PushUndoAction(deleteAction);

            _logger.LogInformation("成功删除 {Count} 个片段", clipsToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除片段失败");
        }
    }

    /// <summary>
    /// 恢复已删除的片段（用于撤销删除操作）
    /// </summary>
    private void RestoreDeletedClips(List<ClipSnapshot> snapshots)
    {
        foreach (var snapshot in snapshots)
        {
            var track = Tracks.FirstOrDefault(t => t.Id == snapshot.TrackId);
            if (track != null)
            {
                var clip = snapshot.ToClip(PixelsPerSecond);
                track.Clips.Add(clip);
                _logger.LogInformation("恢复片段到轨道 {TrackName}: {ClipId}", track.Name, clip.Id);
            }
        }

        RecalculateTrackPositions();
        _isDirty = true;
    }

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (Playback.State.IsPlaying)
        {
            Playback.PauseCommand.Execute(null);
        }
        else
        {
            Playback.PlayCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task SplitSelectedClip()
    {
        _logger.LogInformation("开始分割片段操作");

        if (SelectedClip == null)
        {
            _logger.LogWarning("分割失败: 没有选中的片段");
            return;
        }

        var clip = SelectedClip;
        var splitTime = PlayheadTime;

        _logger.LogInformation("分割片段: ClipId={ClipId}, StartTime={StartTime}s, EndTime={EndTime}s, PlayheadTime={PlayheadTime}s",
            clip.Id, clip.StartTime, clip.EndTime, splitTime);

        if (splitTime <= clip.StartTime + MinClipDurationSeconds ||
            splitTime >= clip.EndTime - MinClipDurationSeconds)
        {
            _logger.LogWarning("分割失败: 播放头位置不在片段范围内或太接近边缘 (MinDuration={MinDuration}s)", MinClipDurationSeconds);
            return;
        }

        var track = FindTrackForClip(clip);
        if (track == null)
        {
            _logger.LogWarning("分割失败: 找不到片段所在的轨道");
            return;
        }

        var splitOffset = splitTime - clip.StartTime;
        var rightDuration = clip.Duration - splitOffset;

        _logger.LogInformation("分割偏移: {SplitOffset}s, 左侧时长: {LeftDuration}s, 右侧时长: {RightDuration}s",
            splitOffset, splitOffset, rightDuration);

        if (splitOffset < MinClipDurationSeconds || rightDuration < MinClipDurationSeconds)
        {
            _logger.LogWarning("分割失败: 分割后的片段太短 (左侧={Left}s, 右侧={Right}s, 最小={Min}s)",
                splitOffset, rightDuration, MinClipDurationSeconds);
            return;
        }

        var oldDuration = clip.Duration;
        var oldSourceStart = clip.SourceStart;
        var oldSourceDuration = clip.SourceDuration;

        // 保存原始状态用于撤销
        var originalClipSnapshot = ClipSnapshot.FromClip(clip);

        string? newSegmentId = null;
        if (_draftContent != null)
        {
            try
            {
                _logger.LogInformation("在草稿中分割 segment: {SegmentId}", clip.Id.ToString("N").ToUpper());

                newSegmentId = DraftAdapter.SplitSegment(
                    _draftContent,
                    clip.Id.ToString("N").ToUpper(),
                    splitTime);

                if (string.IsNullOrEmpty(newSegmentId))
                {
                    _logger.LogWarning("分割失败: DraftAdapter.SplitSegment 返回空 ID");
                    return;
                }

                _logger.LogInformation("草稿分割成功，新 segment ID: {NewSegmentId}", newSegmentId);
                _isDirty = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "分割草稿 segment 失败");
                return;
            }
        }

        _autoSaveTimer?.Stop();

        clip.Duration = splitOffset;
        clip.SourceDuration = Math.Min(oldSourceDuration, splitOffset);
        clip.DragOffsetX = clip.PixelPosition;

        var newClip = new TimelineClip
        {
            Id = !string.IsNullOrEmpty(newSegmentId) ? Guid.Parse(newSegmentId) : Guid.NewGuid(),
            TrackId = clip.TrackId,
            ShotNumber = clip.ShotNumber,
            StartTime = splitTime,
            Duration = rightDuration,
            SourceStart = oldSourceStart + splitOffset,
            SourceDuration = Math.Max(0, oldSourceDuration - splitOffset),
            PixelsPerSecond = PixelsPerSecond,
            Status = clip.Status,
            ThumbnailPath = clip.ThumbnailPath,
            VideoPath = clip.VideoPath
        };

        newClip.DragOffsetX = newClip.PixelPosition;

        var insertIndex = track.Clips.IndexOf(clip);
        if (insertIndex >= 0)
        {
            track.Clips.Insert(insertIndex + 1, newClip);
        }
        else
        {
            track.Clips.Add(newClip);
        }

        _logger.LogInformation("分割成功: 原片段时长={OldDuration}s -> {NewDuration}s, 新片段 ID={NewClipId}, 时长={NewClipDuration}s",
            oldDuration, clip.Duration, newClip.Id, newClip.Duration);

        // 添加到撤销栈
        var splitAction = new SplitClipAction(
            originalClipSnapshot,
            newClip.Id,
            (originalSnapshot) => UndoSplitClip(originalSnapshot),
            (newClipId) => DeleteClipById(newClipId));
        PushUndoAction(splitAction);

        UpdateTimelineDurationFromTracks();
        _autoSaveTimer?.Start();
        await SaveDraftAsync();

        _messenger.Send(new ClipsSplitMessage(clip, newClip));
    }

    /// <summary>
    /// 撤销分割操作：恢复原始片段，删除新片段
    /// </summary>
    private void UndoSplitClip(ClipSnapshot originalSnapshot)
    {
        var track = Tracks.FirstOrDefault(t => t.Id == originalSnapshot.TrackId);
        if (track == null)
        {
            _logger.LogWarning("撤销分割失败: 找不到轨道 {TrackId}", originalSnapshot.TrackId);
            return;
        }

        // 找到被分割的片段（现在是左侧片段）
        var leftClip = track.Clips.FirstOrDefault(c => c.Id == originalSnapshot.ClipId);
        if (leftClip != null)
        {
            // 恢复原始时长
            leftClip.Duration = originalSnapshot.Duration;
            leftClip.SourceDuration = originalSnapshot.SourceDuration;
            leftClip.DragOffsetX = leftClip.PixelPosition;
            _logger.LogInformation("恢复原始片段时长: {ClipId}, Duration={Duration}s", leftClip.Id, leftClip.Duration);
        }

        RecalculateTrackPositions();
        _isDirty = true;
    }

    /// <summary>
    /// 根据 ID 删除片段
    /// </summary>
    private void DeleteClipById(Guid clipId)
    {
        foreach (var track in Tracks)
        {
            var clip = track.Clips.FirstOrDefault(c => c.Id == clipId);
            if (clip != null)
            {
                track.Clips.Remove(clip);
                _logger.LogInformation("删除片段: {ClipId}", clipId);

                // 从草稿中删除
                if (_draftContent?.Tracks != null)
                {
                    foreach (var draftTrack in _draftContent.Tracks)
                    {
                        var segment = draftTrack.Segments?.FirstOrDefault(s => s.Id == clipId.ToString());
                        if (segment != null)
                        {
                            draftTrack.Segments?.Remove(segment);
                        }
                    }
                }

                RecalculateTrackPositions();
                _isDirty = true;
                return;
            }
        }
    }

    #endregion

    #endregion
}
