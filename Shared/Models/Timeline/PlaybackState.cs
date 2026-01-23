using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace Storyboard.Models.Timeline;

/// <summary>
/// 播放状态模型
/// </summary>
public partial class PlaybackState : ObservableObject
{
    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private double _currentTime;

    [ObservableProperty]
    private double _totalDuration;

    [ObservableProperty]
    private TimelineClip? _currentClip;

    [ObservableProperty]
    private double _playbackSpeed = 1.0;

    /// <summary>
    /// 播放进度（0.0 - 1.0）
    /// </summary>
    public double Progress => TotalDuration > 0 ? CurrentTime / TotalDuration : 0;

    /// <summary>
    /// 当前时间码（格式：MM:SS.FF）
    /// </summary>
    public string TimeCode => FormatTimeCode(CurrentTime);

    /// <summary>
    /// 总时长时间码（格式：MM:SS.FF）
    /// </summary>
    public string TotalTimeCode => FormatTimeCode(TotalDuration);

    /// <summary>
    /// 格式化时间码
    /// </summary>
    private static string FormatTimeCode(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
    }

    /// <summary>
    /// 当 CurrentTime 或 TotalDuration 变化时，通知计算属性更新
    /// </summary>
    partial void OnCurrentTimeChanged(double value)
    {
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(TimeCode));
    }

    partial void OnTotalDurationChanged(double value)
    {
        OnPropertyChanged(nameof(Progress));
        OnPropertyChanged(nameof(TotalTimeCode));
    }
}
