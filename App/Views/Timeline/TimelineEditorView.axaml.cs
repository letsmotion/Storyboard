using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Storyboard.ViewModels.Timeline;
using System;
using System.IO;
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
        try
        {
            // macOS: 尝试使用系统安装的 VLC
            if (OperatingSystem.IsMacOS())
            {
                var systemVlcPaths = new[]
                {
                    "/Applications/VLC.app/Contents/MacOS/lib",  // 系统安装的 VLC
                    "/opt/homebrew/lib",                          // Homebrew ARM64
                    "/usr/local/lib",                             // Homebrew Intel
                    "/opt/homebrew/Cellar/libvlc/3.0.21/lib"     // Homebrew libvlc 包
                };

                foreach (var path in systemVlcPaths)
                {
                    // 检查目录和关键库文件是否存在
                    var libvlcPath = Path.Combine(path, "libvlc.dylib");
                    var libvlccorePath = Path.Combine(path, "libvlccore.dylib");

                    if (File.Exists(libvlcPath) && File.Exists(libvlccorePath))
                    {
                        try
                        {
                            Core.Initialize(path);
                            System.Diagnostics.Debug.WriteLine($"[LibVLC] 使用系统 VLC: {path}");
                            return;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LibVLC] 初始化失败 ({path}): {ex.Message}");
                            continue;
                        }
                    }
                }

                // macOS 上未找到系统 VLC，抛出友好错误
                throw new InvalidOperationException(
                    "未找到 VLC 安装。请运行: brew install --cask vlc\n" +
                    $"已检查路径: {string.Join(", ", systemVlcPaths)}");
            }

            // Windows: 使用默认初始化（从 NuGet 包）
            Core.Initialize();
        }
        catch (InvalidOperationException ex)
        {
            // Core 已经初始化过，或者是我们抛出的友好错误
            if (ex.Message.Contains("VLC"))
            {
                throw; // 重新抛出 VLC 未找到的错误
            }
            // 否则忽略（已初始化）
        }
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
