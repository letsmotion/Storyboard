using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Storyboard.Models.Timeline;
using System;

namespace Storyboard.ViewModels.Timeline;

/// <summary>
/// 时间轴播放控制 ViewModel
/// </summary>
public partial class TimelinePlaybackViewModel : ObservableObject
{
    private readonly ILogger<TimelinePlaybackViewModel> _logger;
    private MediaPlayer? _mediaPlayer;
    private LibVLC? _libVLC;
    private System.Timers.Timer? _playbackTimer;

    [ObservableProperty]
    private PlaybackState _state = new();

    [ObservableProperty]
    private bool _canPlay;

    [ObservableProperty]
    private bool _canPause;

    public TimelinePlaybackViewModel(ILogger<TimelinePlaybackViewModel> logger)
    {
        _logger = logger;
        InitializePlaybackTimer();
    }

    /// <summary>
    /// 设置 MediaPlayer（从 View 传入）
    /// </summary>
    public void SetMediaPlayer(MediaPlayer? player)
    {
        _mediaPlayer = player;
        CanPlay = player != null;
        _logger.LogInformation("MediaPlayer 已设置");
    }

    /// <summary>
    /// 获取 MediaPlayer 实例
    /// </summary>
    public MediaPlayer? GetMediaPlayer() => _mediaPlayer;

    /// <summary>
    /// 获取 LibVLC 实例（从 MediaPlayer 获取）
    /// </summary>
    public LibVLC? GetLibVLC()
    {
        if (_libVLC == null && _mediaPlayer != null)
        {
            // 从 MediaPlayer 的 Media 获取 LibVLC 实例
            // 或者需要在 SetMediaPlayer 时同时传入 LibVLC
            _logger.LogWarning("LibVLC 实例未设置");
        }
        return _libVLC;
    }

    /// <summary>
    /// 设置 LibVLC 实例
    /// </summary>
    public void SetLibVLC(LibVLC? libVLC)
    {
        _libVLC = libVLC;
    }

    /// <summary>
    /// 播放
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        if (_mediaPlayer == null)
        {
            _logger.LogWarning("MediaPlayer 未初始化");
            return;
        }

        _mediaPlayer.Play();
        State.IsPlaying = true;
        CanPlay = false;
        CanPause = true;
        _playbackTimer?.Start();

        _logger.LogInformation("开始播放");
    }

    /// <summary>
    /// 暂停
    /// </summary>
    [RelayCommand]
    private void Pause()
    {
        if (_mediaPlayer == null) return;

        _mediaPlayer.Pause();
        State.IsPlaying = false;
        CanPlay = true;
        CanPause = false;
        _playbackTimer?.Stop();

        _logger.LogInformation("暂停播放");
    }

    /// <summary>
    /// 停止
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        StopPlayback();
    }

    /// <summary>
    /// 停止播放（公共方法）
    /// </summary>
    public void StopPlayback()
    {
        if (_mediaPlayer == null) return;

        _mediaPlayer.Stop();
        State.IsPlaying = false;
        State.CurrentTime = 0;
        CanPlay = true;
        CanPause = false;
        _playbackTimer?.Stop();

        _logger.LogInformation("停止播放");
    }

    /// <summary>
    /// 跳转到指定时间
    /// </summary>
    public void SeekTo(double time)
    {
        if (_mediaPlayer == null || State.TotalDuration <= 0) return;

        State.CurrentTime = Math.Clamp(time, 0, State.TotalDuration);
        var position = (float)(State.CurrentTime / State.TotalDuration);
        _mediaPlayer.Position = position;

        _logger.LogDebug("跳转到 {Time:F2}s (Position: {Position:F2})", time, position);
    }

    /// <summary>
    /// 初始化播放定时器（用于更新当前时间）
    /// </summary>
    private void InitializePlaybackTimer()
    {
        _playbackTimer = new System.Timers.Timer(33); // ~30fps
        _playbackTimer.Elapsed += (s, e) =>
        {
            if (_mediaPlayer != null && State.IsPlaying && State.TotalDuration > 0)
            {
                var newTime = _mediaPlayer.Position * State.TotalDuration;
                if (Math.Abs(newTime - State.CurrentTime) > 0.01) // 避免频繁更新
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (_mediaPlayer == null || !State.IsPlaying || State.TotalDuration <= 0)
                            return;

                        if (Math.Abs(newTime - State.CurrentTime) > 0.01)
                            State.CurrentTime = newTime;
                    });
                }
            }
        };
    }

    /// <summary>
    /// 清理资源
    /// </summary>
    public void Dispose()
    {
        _playbackTimer?.Stop();
        _playbackTimer?.Dispose();
        _playbackTimer = null;
    }
}
