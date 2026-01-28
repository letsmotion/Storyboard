using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;

namespace Storyboard.Models.Timeline;

/// <summary>
/// 时间轴轨道模型
/// </summary>
public partial class TimelineTrack : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private TrackType _type;

    [ObservableProperty]
    private bool _isLocked;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private int _order;

    [ObservableProperty]
    private double _height = 96; // 优化后的轨道高度（原128px）

    [ObservableProperty]
    private double _verticalOffset;

    [ObservableProperty]
    private ObservableCollection<TimelineClip> _clips = new();

    public TimelineTrack(TrackType type, string name)
    {
        Id = Guid.NewGuid();
        Type = type;
        Name = name;
    }

    [RelayCommand]
    private void ToggleLock()
    {
        IsLocked = !IsLocked;
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }
}

/// <summary>
/// 轨道类型枚举
/// </summary>
public enum TrackType
{
    OriginalVideo,  // 原视频轨道
    Video,          // 视频轨道
    Audio,          // 音频轨道
    Subtitle        // 字幕轨道
}
