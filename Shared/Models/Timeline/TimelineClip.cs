using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace Storyboard.Models.Timeline;

/// <summary>
/// 时间轴片段模型
/// </summary>
public partial class TimelineClip : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private Guid _trackId;

    [ObservableProperty]
    private int _shotNumber;

    [ObservableProperty]
    private double _startTime;

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private ClipStatus _status;

    [ObservableProperty]
    private string? _thumbnailPath;

    [ObservableProperty]
    private string? _videoPath;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isHovered;

    [ObservableProperty]
    private double _pixelsPerSecond = 50; // 默认缩放比例

    // 拖动相关属性
    [ObservableProperty]
    private bool _isDragging;

    [ObservableProperty]
    private double _dragOffsetX; // 拖动时的临时像素位置

    [ObservableProperty]
    private bool _isBeingTrimmed; // 是否正在修剪

    /// <summary>
    /// 像素位置（用于 Canvas.Left）
    /// </summary>
    public double PixelPosition => StartTime * PixelsPerSecond;

    /// <summary>
    /// 视觉位置（拖动时使用临时位置，否则使用实际位置）
    /// </summary>
    public double VisualPosition => IsDragging ? DragOffsetX : PixelPosition;

    /// <summary>
    /// 像素宽度（用于 Width）
    /// </summary>
    public double PixelWidth => Duration * PixelsPerSecond;

    /// <summary>
    /// 结束时间（计算属性）
    /// </summary>
    public double EndTime => StartTime + Duration;

    // 属性变化通知
    partial void OnStartTimeChanged(double value)
    {
        OnPropertyChanged(nameof(PixelPosition));
        OnPropertyChanged(nameof(EndTime));
    }

    partial void OnDurationChanged(double value)
    {
        OnPropertyChanged(nameof(PixelWidth));
        OnPropertyChanged(nameof(EndTime));
    }

    partial void OnPixelsPerSecondChanged(double value)
    {
        OnPropertyChanged(nameof(PixelPosition));
        OnPropertyChanged(nameof(PixelWidth));
        OnPropertyChanged(nameof(VisualPosition));
    }

    partial void OnIsDraggingChanged(bool value)
    {
        OnPropertyChanged(nameof(VisualPosition));
    }

    partial void OnDragOffsetXChanged(double value)
    {
        OnPropertyChanged(nameof(VisualPosition));
    }

    /// <summary>
    /// 从 ShotItem 创建 TimelineClip
    /// </summary>
    public static TimelineClip FromShotItem(ShotItem shot, Guid trackId, double startTime, double pixelsPerSecond = 50)
    {
        return new TimelineClip
        {
            Id = Guid.NewGuid(),
            TrackId = trackId,
            ShotNumber = shot.ShotNumber,
            StartTime = startTime,
            Duration = shot.Duration,
            PixelsPerSecond = pixelsPerSecond,
            Status = GetStatusFromShot(shot),
            ThumbnailPath = shot.FirstFrameImagePath,
            VideoPath = shot.GeneratedVideoPath
        };
    }

    /// <summary>
    /// 根据 ShotItem 状态确定 ClipStatus
    /// </summary>
    public static ClipStatus GetStatusFromShot(ShotItem shot)
    {
        if (shot.IsVideoGenerating)
            return ClipStatus.Generating;

        if (!string.IsNullOrEmpty(shot.GeneratedVideoPath) && File.Exists(shot.GeneratedVideoPath))
            return ClipStatus.Generated;

        return ClipStatus.Placeholder;
    }
}

/// <summary>
/// 片段状态枚举
/// </summary>
public enum ClipStatus
{
    Placeholder,    // 未生成（显示占位符）
    Generating,     // 生成中（显示进度）
    Generated,      // 已生成（显示缩略图）
    Error          // 生成失败（显示错误图标）
}
