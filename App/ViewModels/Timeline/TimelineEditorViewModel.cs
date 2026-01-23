using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Messages;
using Storyboard.Models;
using Storyboard.Models.Timeline;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Storyboard.ViewModels.Timeline;

/// <summary>
/// 时间轴编辑器主 ViewModel
/// </summary>
public partial class TimelineEditorViewModel : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly ILogger<TimelineEditorViewModel> _logger;

    // 子 ViewModels
    public TimelinePlaybackViewModel Playback { get; }

    // 轨道集合
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

    // 上一个选中的片段（用于取消选中状态）
    private TimelineClip? _previousSelectedClip;

    public TimelineEditorViewModel(
        TimelinePlaybackViewModel playback,
        IMessenger messenger,
        ILogger<TimelineEditorViewModel> logger)
    {
        Playback = playback;
        _messenger = messenger;
        _logger = logger;

        // 订阅 Shot 消息
        _messenger.Register<ShotAddedMessage>(this, OnShotAdded);
        _messenger.Register<ShotDeletedMessage>(this, OnShotDeleted);
        _messenger.Register<ShotUpdatedMessage>(this, OnShotUpdated);
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);

        // 订阅片段选中消息
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

        // 初始化默认轨道
        InitializeDefaultTracks();
    }

    /// <summary>
    /// 初始化默认轨道
    /// </summary>
    private void InitializeDefaultTracks()
    {
        // 不再自动添加默认轨道，等待 BuildTimelineFromShots 时添加
        _logger.LogInformation("初始化轨道系统");
    }

    /// <summary>
    /// 从 Shots 构建时间轴
    /// </summary>
    [RelayCommand]
    public void BuildTimelineFromShots()
    {
        var query = new GetAllShotsQuery();
        _messenger.Send(query);

        if (query.Shots == null || query.Shots.Count == 0)
        {
            _logger.LogWarning("没有可用的 Shots");
            TotalDuration = 0;
            TimelineWidth = 0;
            TimeMarkers.Clear();
            return;
        }

        // 清空现有轨道
        Tracks.Clear();

        // 添加原视频轨道（如果有导入的视频）
        AddOriginalVideoTrack();

        // 创建视频轨道并添加 Shots
        var videoTrack = new TimelineTrack(TrackType.Video, "生成视频轨道");
        Tracks.Add(videoTrack);

        double currentTime = 0;
        foreach (var shot in query.Shots.OrderBy(s => s.ShotNumber))
        {
            var clip = TimelineClip.FromShotItem(shot, videoTrack.Id, currentTime, PixelsPerSecond);
            videoTrack.Clips.Add(clip);
            _logger.LogInformation("添加片段: Shot #{ShotNumber}, StartTime={StartTime}s, Duration={Duration}s, PixelPosition={PixelPosition}px, PixelWidth={PixelWidth}px",
                clip.ShotNumber, clip.StartTime, clip.Duration, clip.PixelPosition, clip.PixelWidth);
            currentTime += shot.Duration;
        }

        TotalDuration = currentTime;
        TimelineWidth = TotalDuration * PixelsPerSecond;
        Playback.State.TotalDuration = TotalDuration;

        // 生成时间标记
        GenerateTimeMarkers();

        _logger.LogInformation("时间轴构建完成: {ClipCount} 个片段, 总时长 {Duration:F2}s, 轨道数 {TrackCount}, 视频轨道片段数 {VideoTrackClipCount}",
            videoTrack.Clips.Count, TotalDuration, Tracks.Count, videoTrack.Clips.Count);
    }

    /// <summary>
    /// 添加原视频轨道
    /// </summary>
    private void AddOriginalVideoTrack()
    {
        // 查询视频导入信息
        var videoQuery = new GetVideoImportInfoQuery();
        _messenger.Send(videoQuery);

        if (string.IsNullOrEmpty(videoQuery.VideoPath) || !System.IO.File.Exists(videoQuery.VideoPath))
        {
            _logger.LogInformation("没有导入的原视频，跳过原视频轨道创建");
            return;
        }

        // 创建原视频轨道
        var originalTrack = new TimelineTrack(TrackType.OriginalVideo, "原视频");
        Tracks.Add(originalTrack);

        // 创建原视频片段
        var originalClip = new TimelineClip
        {
            Id = Guid.NewGuid(),
            TrackId = originalTrack.Id,
            ShotNumber = 0, // 原视频不是 shot
            StartTime = 0,
            Duration = videoQuery.VideoDurationSeconds,
            PixelsPerSecond = PixelsPerSecond,
            Status = ClipStatus.Generated,
            VideoPath = videoQuery.VideoPath,
            ThumbnailPath = null // 可以后续添加缩略图
        };

        originalTrack.Clips.Add(originalClip);

        _logger.LogInformation("添加原视频轨道: 时长={Duration:F2}s, 路径={Path}",
            originalClip.Duration, videoQuery.VideoPath);
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
    /// 添加轨道
    /// </summary>
    [RelayCommand]
    private void AddTrack(TrackType type)
    {
        var trackNumber = Tracks.Count(t => t.Type == type) + 1;
        var name = type switch
        {
            TrackType.OriginalVideo => "原视频",
            TrackType.Video => $"视频轨道 {trackNumber}",
            TrackType.Audio => $"音频轨道 {trackNumber}",
            TrackType.Subtitle => $"字幕轨道 {trackNumber}",
            _ => $"轨道 {trackNumber}"
        };

        var track = new TimelineTrack(type, name) { Order = Tracks.Count };
        Tracks.Add(track);

        _messenger.Send(new TrackAddedMessage(track));
        _logger.LogInformation("添加轨道: {Name}", name);
    }

    /// <summary>
    /// 删除轨道
    /// </summary>
    [RelayCommand]
    private void DeleteTrack(TimelineTrack? track)
    {
        if (track == null || Tracks.Count <= 1)
        {
            _logger.LogWarning("无法删除轨道：轨道为空或只剩一条轨道");
            return;
        }

        Tracks.Remove(track);
        _messenger.Send(new TrackDeletedMessage(track));
        _logger.LogInformation("删除轨道: {Name}", track.Name);
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

        // 重新生成时间标记
        GenerateTimeMarkers();
    }

    /// <summary>
    /// 重新计算总时长
    /// </summary>
    private void RecalculateDuration()
    {
        var maxEndTime = Tracks
            .SelectMany(t => t.Clips)
            .Select(c => c.EndTime)
            .DefaultIfEmpty(0)
            .Max();

        TotalDuration = maxEndTime;
        TimelineWidth = TotalDuration * PixelsPerSecond;
        Playback.State.TotalDuration = TotalDuration;

        GenerateTimeMarkers();
    }

    // 消息处理
    private void OnShotAdded(object recipient, ShotAddedMessage message)
    {
        BuildTimelineFromShots();
    }

    private void OnShotDeleted(object recipient, ShotDeletedMessage message)
    {
        BuildTimelineFromShots();
    }

    private void OnShotUpdated(object recipient, ShotUpdatedMessage message)
    {
        // 更新对应的 Clip
        var clip = Tracks
            .SelectMany(t => t.Clips)
            .FirstOrDefault(c => c.ShotNumber == message.Shot.ShotNumber);

        if (clip != null)
        {
            clip.Duration = message.Shot.Duration;
            clip.Status = TimelineClip.FromShotItem(message.Shot, clip.TrackId, clip.StartTime, PixelsPerSecond).Status;
            clip.ThumbnailPath = message.Shot.FirstFrameImagePath;
            clip.VideoPath = message.Shot.GeneratedVideoPath;

            RecalculateDuration();
            _logger.LogDebug("更新 Clip: Shot #{ShotNumber}", message.Shot.ShotNumber);
        }
    }

    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        // 项目加载后自动构建时间轴
        BuildTimelineFromShots();
    }

    /// <summary>
    /// 处理片段选中消息
    /// </summary>
    private void OnClipSelected(object recipient, ClipSelectedMessage message)
    {
        // 取消上一个片段的选中状态
        if (_previousSelectedClip != null && _previousSelectedClip != message.Clip)
        {
            _previousSelectedClip.IsSelected = false;
        }

        // 设置新的选中片段
        SelectedClip = message.Clip;

        if (SelectedClip != null)
        {
            SelectedClip.IsSelected = true;
            _previousSelectedClip = SelectedClip;
            _logger.LogInformation("片段已选中: Shot #{ShotNumber}", SelectedClip.ShotNumber);

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
}
