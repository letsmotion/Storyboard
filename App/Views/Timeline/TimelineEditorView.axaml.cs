using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Storyboard.Views;
using Storyboard.ViewModels.Timeline;
using System;
using System.Threading;

namespace Storyboard.Views.Timeline;

public partial class TimelineEditorView : UserControl, IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private bool _isInitialized;
    private bool _isDisposed;
    private readonly object _playerLock = new();

    // 静态初始化 LibVLC Core
    static TimelineEditorView()
    {
        VlcCoreInitializer.EnsureInitialized();
    }

    public TimelineEditorView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_isInitialized && !_isDisposed)
        {
            InitializeVLC();
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        // 停止播放但保留播放器
        lock (_playerLock)
        {
            _mediaPlayer?.Stop();
        }
    }

    /// <summary>
    /// 初始化 LibVLC
    /// </summary>
    private void InitializeVLC()
    {
        lock (_playerLock)
        {
            if (_isInitialized || _isDisposed)
                return;

            try
            {
                // 创建 LibVLC 实例
                _libVLC = new LibVLC(
                    "--no-video-title-show",
                    "--input-fast-seek",
                    "--no-xlib",
                    "--no-snapshot-preview",
                    "--no-video-deco"
                );

                // 创建 MediaPlayer
                _mediaPlayer = new MediaPlayer(_libVLC);

                // 订阅错误事件
                _mediaPlayer.EncounteredError += OnMediaPlayerError;

                // 绑定到 VideoView
                if (VideoPlayer != null)
                {
                    VideoPlayer.MediaPlayer = _mediaPlayer;
                }

                // 传递给 ViewModel
                if (DataContext is TimelineEditorViewModel vm)
                {
                    vm.Playback.SetMediaPlayer(_mediaPlayer);
                    vm.Playback.SetLibVLC(_libVLC);
                }

                _isInitialized = true;

                LogMessage("LibVLC 初始化成功");
            }
            catch (Exception ex)
            {
                LogMessage($"LibVLC 初始化失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// MediaPlayer 错误处理
    /// </summary>
    private void OnMediaPlayerError(object? sender, EventArgs e)
    {
        LogMessage("MediaPlayer 遇到错误");
    }

    /// <summary>
    /// 日志输出
    /// </summary>
    private void LogMessage(string message)
    {
        System.Diagnostics.Debug.WriteLine($"[TimelineEditorView] {message}");
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        lock (_playerLock)
        {
            try
            {
                // 停止播放
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Stop();
                    _mediaPlayer.Media = null;
                    Thread.Sleep(50);
                }

                // 解绑 VideoView
                if (VideoPlayer != null)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        VideoPlayer.MediaPlayer = null;
                    });
                }

                // 释放 MediaPlayer
                _mediaPlayer?.Dispose();
                _mediaPlayer = null;

                // 释放 LibVLC
                _libVLC?.Dispose();
                _libVLC = null;

                LogMessage("资源已释放");
            }
            catch (Exception ex)
            {
                LogMessage($"释放资源时出错: {ex.Message}");
            }
        }

        // 释放 Playback ViewModel
        if (DataContext is TimelineEditorViewModel vm)
        {
            vm.Playback.Dispose();
        }
    }
}
