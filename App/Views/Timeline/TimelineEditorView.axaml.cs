using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LibVLCSharp.Shared;
using Microsoft.Extensions.Logging;
using Storyboard.Views;
using Storyboard.ViewModels.Timeline;
using Storyboard.Behaviors;
using System;
using System.Reactive.Linq;
using System.Threading;

namespace Storyboard.Views.Timeline;

public partial class TimelineEditorView : UserControl, IDisposable
{
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private bool _isInitialized;
    private bool _isDisposed;
    private readonly object _playerLock = new();
    private TimelineScrollSynchronizer? _scrollSynchronizer;
    private IDisposable? _visibilitySubscription;

    // 静态初始化 LibVLC Core
    static TimelineEditorView()
    {
        VlcCoreInitializer.EnsureInitialized();
    }

    public TimelineEditorView()
    {
        InitializeComponent();

        // 注册键盘事件（隧道路由，保证子控件获得焦点时仍可响应）
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        // 注册鼠标滚轮事件（用于 Ctrl+滚轮缩放）
        PointerWheelChanged += OnPointerWheelChanged;

        // 确保控件可以接收焦点
        Focusable = true;

        _visibilitySubscription = this.GetObservable(IsVisibleProperty).Subscribe(isVisible =>
        {
            if (isVisible)
            {
                Dispatcher.UIThread.Post(() => Focus());
            }
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (!_isInitialized && !_isDisposed)
        {
            InitializeVLC();
            InitializeScrollSynchronization();
        }

        // 自动获取焦点以接收键盘事件
        Dispatcher.UIThread.Post(() => Focus());
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
    /// 初始化滚动同步
    /// </summary>
    private void InitializeScrollSynchronization()
    {
        try
        {
            if (TimelineScrollViewer != null &&
                TrackHeadersScrollViewer != null &&
                TimelineRuler != null)
            {
                _scrollSynchronizer = new TimelineScrollSynchronizer();
                _scrollSynchronizer.Initialize(
                    TimelineScrollViewer,
                    TrackHeadersScrollViewer,
                    TimelineRuler);

                LogMessage("滚动同步初始化成功");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"滚动同步初始化失败: {ex.Message}");
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
    /// 键盘事件处理
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TimelineEditorViewModel vm)
            return;

        var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        switch (e.Key)
        {
            case Key.Space:
                // 空格键：播放/暂停
                vm.TogglePlayPauseCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Delete:
                // Delete 键：删除选中的片段
                vm.DeleteSelectedClipsCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.K when isCtrlPressed:
                // Ctrl+K：分割片段（改用 K 键，避免与 Ctrl+B 冲突）
                LogMessage("检测到 Ctrl+K 按键");
                if (vm.SplitSelectedClipCommand != null)
                {
                    LogMessage($"SplitSelectedClipCommand 存在，CanExecute: {vm.SplitSelectedClipCommand.CanExecute(null)}");
                    vm.SplitSelectedClipCommand.Execute(null);
                    LogMessage("已执行分割命令");
                    e.Handled = true;
                }
                else
                {
                    LogMessage("SplitSelectedClipCommand 为 null");
                }
                break;

            case Key.Z when isCtrlPressed:
                // Ctrl+Z：撤销
                LogMessage("检测到 Ctrl+Z 按键");
                if (vm.UndoCommand != null)
                {
                    LogMessage($"UndoCommand 存在，CanExecute: {vm.UndoCommand.CanExecute(null)}");
                    if (vm.UndoCommand.CanExecute(null))
                    {
                        vm.UndoCommand.Execute(null);
                        LogMessage("已执行撤销命令");
                        e.Handled = true;
                    }
                    else
                    {
                        LogMessage("撤销命令无法执行（可能撤销栈为空）");
                    }
                }
                else
                {
                    LogMessage("UndoCommand 为 null");
                }
                break;

            case Key.Y when isCtrlPressed:
                // Ctrl+Y：重做
                LogMessage("检测到 Ctrl+Y 按键");
                if (vm.RedoCommand != null)
                {
                    LogMessage($"RedoCommand 存在，CanExecute: {vm.RedoCommand.CanExecute(null)}");
                    if (vm.RedoCommand.CanExecute(null))
                    {
                        vm.RedoCommand.Execute(null);
                        LogMessage("已执行重做命令");
                        e.Handled = true;
                    }
                    else
                    {
                        LogMessage("重做命令无法执行（可能重做栈为空）");
                    }
                }
                else
                {
                    LogMessage("RedoCommand 为 null");
                }
                break;
        }
    }

    /// <summary>
    /// 鼠标滚轮事件处理（Ctrl+滚轮缩放）
    /// </summary>
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not TimelineEditorViewModel vm)
            return;

        // 检查是否按下 Ctrl 键
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        // 获取滚轮增量（正值向上滚，负值向下滚）
        var delta = e.Delta.Y;

        // 计算新的缩放值
        var currentZoom = vm.PixelsPerSecond;
        var zoomStep = 5.0; // 每次滚动改变 5 像素/秒
        var newZoom = currentZoom + (delta * zoomStep);

        // 限制在 10-200 范围内
        newZoom = Math.Clamp(newZoom, 10, 200);

        // 应用新的缩放值
        vm.PixelsPerSecond = newZoom;

        // 标记事件已处理，防止页面滚动
        e.Handled = true;

        LogMessage($"时间轴缩放: {newZoom:F0}px/s");
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

        // 注销事件处理器
        RemoveHandler(KeyDownEvent, OnKeyDown);
        PointerWheelChanged -= OnPointerWheelChanged;

        // 释放滚动同步
        _scrollSynchronizer?.Dispose();
        _scrollSynchronizer = null;
        _visibilitySubscription?.Dispose();
        _visibilitySubscription = null;

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
