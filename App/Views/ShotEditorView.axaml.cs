using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Storyboard.Models;
using Storyboard.Application.Services;
using Storyboard.Domain.Entities;
using Storyboard.Infrastructure.Media;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.ViewModels;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using LibVLCSharp.Shared;
using LibVLCSharp.Avalonia;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.VisualTree;
using System.Linq;

namespace Storyboard.Views;

public partial class ShotEditorView : UserControl, IDisposable
{
    private ShotItem? _shot;
    private LibVLC? _libVLC;
    private MediaPlayer? _mediaPlayer;
    private Media? _currentMedia;
    private string? _currentVideoPath;
    private LibVLC? _audioLibVLC;
    private MediaPlayer? _audioPlayer;
    private Media? _currentAudioMedia;
    private string? _currentAudioPath;
    private DispatcherTimer? _audioProgressTimer;
    private bool _isDisposed = false;
    private bool _isInitialized = false;
    private bool _viewReady = false;
    private readonly object _playerLock = new object();
    private CancellationTokenSource? _loadCancellationTokenSource;
    private bool _pendingPlay = false;
    private int _clearing = 0;
    private bool _rebindDoneForThisMedia = false;

    // 添加静态初始化确保 Core.Initialize() 只调用一次
    static ShotEditorView()
    {
        VlcCoreInitializer.EnsureInitialized();
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] LibVLCSharp core ensured (static)");
    }

    public ShotEditorView()
    {
        InitializeComponent();

        // Monitor DataContext changes
        this.GetObservable(DataContextProperty).Subscribe(OnDataContextChangedObservable);

        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        ResetAudioPlaybackUi();
    }

    private void EnsureAudioPlayerInitialized()
    {
        if (_audioPlayer != null)
            return;

        _audioLibVLC = new LibVLC(
            "--no-video-title-show",
            "--no-video",
            "--quiet");

        _audioPlayer = new MediaPlayer(_audioLibVLC);
        _audioPlayer.EndReached += OnAudioPlaybackEnded;
        _audioPlayer.EncounteredError += OnAudioPlaybackError;

        _audioProgressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _audioProgressTimer.Tick += OnAudioProgressTimerTick;
    }

    private void OnToggleAudioPlayClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShotItem shot)
            return;

        var path = shot.GeneratedAudioPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            shot.AudioStatusMessage = "音频文件不存在";
            StopAudioPlayback(resetProgress: true, clearMedia: true);
            return;
        }

        try
        {
            lock (_playerLock)
            {
                EnsureAudioPlayerInitialized();
                if (_audioPlayer == null)
                {
                    shot.AudioStatusMessage = "内置播放器初始化失败";
                    return;
                }

                var normalizedPath = Path.GetFullPath(path);
                if (!string.Equals(_currentAudioPath, normalizedPath, StringComparison.OrdinalIgnoreCase) ||
                    _audioPlayer.Media == null)
                {
                    if (!TryLoadAudio(normalizedPath))
                    {
                        shot.AudioStatusMessage = "音频加载失败";
                        return;
                    }
                }

                if (_audioPlayer.IsPlaying)
                {
                    _audioPlayer.Pause();
                    StopAudioProgressTimer();
                    SetAudioPlayPauseUi(isPlaying: false);
                    shot.AudioStatusMessage = "已暂停播放";
                }
                else
                {
                    var played = _audioPlayer.Play();
                    if (!played)
                    {
                        shot.AudioStatusMessage = "播放失败：无法启动播放器";
                        return;
                    }

                    StartAudioProgressTimer();
                    SetAudioPlayPauseUi(isPlaying: true);
                    shot.AudioStatusMessage = "正在播放音频...";
                }
            }
        }
        catch (Exception ex)
        {
            shot.AudioStatusMessage = $"播放失败：{ex.Message}";
        }
    }

    private void OnStopAudioClicked(object? sender, RoutedEventArgs e)
    {
        StopAudioPlayback(resetProgress: true, clearMedia: false);
        if (DataContext is ShotItem shot)
            shot.AudioStatusMessage = "已停止播放";
    }

    private bool TryLoadAudio(string normalizedPath)
    {
        if (_audioPlayer == null || _audioLibVLC == null || !File.Exists(normalizedPath))
            return false;

        _audioPlayer.Stop();
        _audioPlayer.Media = null;
        SafeDisposeCurrentAudioMedia();

        _currentAudioMedia = new Media(_audioLibVLC, normalizedPath, FromType.FromPath);
        _audioPlayer.Media = _currentAudioMedia;
        _currentAudioPath = normalizedPath;

        UpdateAudioPlaybackUi();
        return true;
    }

    private void OnAudioProgressTimerTick(object? sender, EventArgs e)
    {
        UpdateAudioPlaybackUi();
    }

    private void OnAudioPlaybackEnded(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopAudioProgressTimer();
            SetAudioPlayPauseUi(isPlaying: false);
            UpdateAudioPlaybackUi(forceToEnd: true);

            if (DataContext is ShotItem shot)
                shot.AudioStatusMessage = "播放完成";
        });
    }

    private void OnAudioPlaybackError(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            StopAudioProgressTimer();
            SetAudioPlayPauseUi(isPlaying: false);

            if (DataContext is ShotItem shot)
                shot.AudioStatusMessage = "播放失败：内置播放器遇到错误";
        });
    }

    private void StartAudioProgressTimer()
    {
        if (_audioProgressTimer != null && !_audioProgressTimer.IsEnabled)
            _audioProgressTimer.Start();
    }

    private void StopAudioProgressTimer()
    {
        if (_audioProgressTimer != null && _audioProgressTimer.IsEnabled)
            _audioProgressTimer.Stop();
    }

    private void StopAudioPlayback(bool resetProgress, bool clearMedia)
    {
        lock (_playerLock)
        {
            try
            {
                _audioPlayer?.Stop();
            }
            catch
            {
                // Ignore stop failures so UI can still recover.
            }

            StopAudioProgressTimer();
            SetAudioPlayPauseUi(isPlaying: false);

            if (clearMedia && _audioPlayer != null)
            {
                _audioPlayer.Media = null;
                SafeDisposeCurrentAudioMedia();
                _currentAudioPath = null;
            }
        }

        if (resetProgress)
            ResetAudioPlaybackUi();
        else
            UpdateAudioPlaybackUi();
    }

    private void SafeDisposeCurrentAudioMedia()
    {
        try
        {
            if (_currentAudioMedia == null)
                return;

            if (_audioPlayer != null && _audioPlayer.Media == _currentAudioMedia)
            {
                _audioPlayer.Stop();
                _audioPlayer.Media = null;
            }

            _currentAudioMedia.Dispose();
            _currentAudioMedia = null;
        }
        catch
        {
            _currentAudioMedia = null;
        }
    }

    private void UpdateAudioPlaybackUi(bool forceToEnd = false)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => UpdateAudioPlaybackUi(forceToEnd));
            return;
        }

        if (AudioPlaybackProgressBar == null || AudioPlaybackTimeText == null)
            return;

        var currentMs = Math.Max(0L, _audioPlayer?.Time ?? 0);
        var lengthMs = Math.Max(0L, _audioPlayer?.Length ?? 0);

        if (lengthMs <= 0 && DataContext is ShotItem shotByDuration && shotByDuration.AudioDuration > 0)
            lengthMs = (long)(shotByDuration.AudioDuration * 1000);

        if (forceToEnd && lengthMs > 0)
            currentMs = lengthMs;

        var progress = lengthMs > 0 ? Math.Clamp((double)currentMs / lengthMs, 0d, 1d) : 0d;
        AudioPlaybackProgressBar.Value = progress;
        AudioPlaybackTimeText.Text = $"{FormatTime(currentMs)} / {FormatTime(lengthMs)}";
    }

    private void ResetAudioPlaybackUi()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ResetAudioPlaybackUi);
            return;
        }

        if (AudioPlaybackProgressBar != null)
            AudioPlaybackProgressBar.Value = 0;

        SetAudioPlayPauseUi(isPlaying: false);

        var totalMs = 0L;
        if (DataContext is ShotItem shot && shot.AudioDuration > 0)
            totalMs = (long)(shot.AudioDuration * 1000);

        if (AudioPlaybackTimeText != null)
            AudioPlaybackTimeText.Text = $"00:00 / {FormatTime(totalMs)}";
    }

    private void SetAudioPlayPauseUi(bool isPlaying)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => SetAudioPlayPauseUi(isPlaying));
            return;
        }

        if (AudioPlayPauseText != null)
            AudioPlayPauseText.Text = isPlaying ? "暂停" : "播放";

        if (AudioPlayPauseIcon != null)
            AudioPlayPauseIcon.Data = Geometry.Parse(isPlaying
                ? "M6 4h4v16H6zm8 0h4v16h-4z"
                : "M8 5v14l11-7z");
    }

    private static string FormatTime(long milliseconds)
    {
        if (milliseconds <= 0)
            return "00:00";

        var time = TimeSpan.FromMilliseconds(milliseconds);
        return time.TotalHours >= 1
            ? $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"
            : $"{time.Minutes:D2}:{time.Seconds:D2}";
    }

    private void HookLayoutEvents()
    {
        if (VideoPlayer == null) return;
        VideoPlayer.LayoutUpdated -= OnVideoLayoutUpdated;
        VideoPlayer.LayoutUpdated += OnVideoLayoutUpdated;
    }

    private void OnVideoLayoutUpdated(object? sender, EventArgs e)
    {
        if (!_pendingPlay) return;
        if (_mediaPlayer?.Media == null || VideoPlayer == null) return;

        var laidOut = (VideoPlayer as Avalonia.Layout.Layoutable)?.IsArrangeValid == true;
        var sizeOk = VideoPlayer.Bounds.Width > 0 && VideoPlayer.Bounds.Height > 0;

        if (laidOut && sizeOk && VideoPlayer.IsAttachedToVisualTree() && VideoPlayer.IsVisible)
        {
            _pendingPlay = false;
            lock (_playerLock)
            {
                try
                {
                    if (_mediaPlayer?.Media != null && !_isDisposed && _viewReady)
                    {
                        _mediaPlayer.Play();
                        System.Diagnostics.Debug.WriteLine("[PlayPending] Layout ready -> Play");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PlayPending] Play error: {ex.Message}");
                }
            }
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (_isDisposed) return;

        System.Diagnostics.Debug.WriteLine("[ShotEditorView] OnAttachedToVisualTree - view attaching");

        Dispatcher.UIThread.Post(() =>
        {
            // 延迟初始化确保UI完全渲染
            Task.Delay(200).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    _viewReady = true;
                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] View is now ready");

                    // Hook layout events for pending play
                    HookLayoutEvents();

                    // If already initialized, just ensure binding
                    if (_isInitialized)
                    {
                        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Player already initialized, ensuring binding");
                        EnsureVideoViewBinding();

                        // Reload current video if exists
                        if (_shot?.GeneratedVideoPath != null && File.Exists(_shot.GeneratedVideoPath))
                        {
                            Task.Delay(100).ContinueWith(__ =>
                            {
                                Dispatcher.UIThread.Post(() =>
                                    LoadVideo(_shot.GeneratedVideoPath!, false));
                            });
                        }
                    }
                    else
                    {
                        // First time initialization
                        InitializeVLC();
                    }
                });
            });
        });
    }

    private void InitializeVLC()
    {
        lock (_playerLock)
        {
            if (_isDisposed || _isInitialized) return;

            try
            {
                if (VideoPlayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] VideoPlayer control not found");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[ShotEditorView] Initializing VLC...");

                // Create LibVLC instance with options to prevent native window
                _libVLC = new LibVLC(
                    "--no-video-title-show",
                    "--input-fast-seek",
                    "--no-audio",
                    "--no-xlib",
                    "--no-snapshot-preview",
                    "--no-video-deco"  // 防止原生窗口装饰
                );

                // Create media player - ONLY ONCE
                _mediaPlayer = new MediaPlayer(_libVLC);

                // 重要：订阅错误事件
                _mediaPlayer.EncounteredError += (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] MediaPlayer encountered error");
                };

                // CRITICAL: Bind media player to VideoView IMMEDIATELY
                VideoPlayer.MediaPlayer = _mediaPlayer;

                _isInitialized = true;

                System.Diagnostics.Debug.WriteLine("[ShotEditorView] VLC initialized successfully");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] VideoPlayer.MediaPlayer bound: {VideoPlayer.MediaPlayer != null}");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] MediaPlayer instance: {_mediaPlayer.GetHashCode()}");

                // 如果有视频要加载，延迟一点确保播放器准备好
                if (_shot?.GeneratedVideoPath != null && File.Exists(_shot.GeneratedVideoPath))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Task.Delay(300).ContinueWith(_ =>
                        {
                            Dispatcher.UIThread.Post(() =>
                                LoadVideo(_shot.GeneratedVideoPath!, false));
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] VLC initialization failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Stack trace: {ex.StackTrace}");

                // 清理失败的部分
                CleanupFailedInitialization();
            }
        }
    }

    private void CleanupFailedInitialization()
    {
        try
        {
            _mediaPlayer?.Dispose();
            _mediaPlayer = null;

            _libVLC?.Dispose();
            _libVLC = null;

            _isInitialized = false;
        }
        catch { /* 忽略清理异常 */ }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] OnDetachedFromVisualTree called");

        _viewReady = false;

        if (_shot != null)
            _shot.PropertyChanged -= OnShotPropertyChanged;

        // CRITICAL: Only stop playback, don't dispose MediaPlayer/LibVLC
        // They will be reused when view is re-attached
        if (_mediaPlayer != null)
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Media = null;
        }

        SafeDisposeCurrentMedia();
        _currentVideoPath = null;
        StopAudioPlayback(resetProgress: false, clearMedia: false);

        System.Diagnostics.Debug.WriteLine("[ShotEditorView] View detached, playback stopped but player kept alive");
    }

    // 实现 IDisposable
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        lock (_playerLock)
        {
            try
            {
                // 取消任何正在进行的加载
                _loadCancellationTokenSource?.Cancel();

                // 停止播放
                _mediaPlayer?.Stop();

                // 先释放 Media
                SafeDisposeCurrentMedia();

                // 然后释放 MediaPlayer
                _mediaPlayer?.Dispose();
                _mediaPlayer = null;

                // 最后释放 LibVLC
                _libVLC?.Dispose();
                _libVLC = null;

                // 释放音频播放器
                if (_audioProgressTimer != null)
                {
                    _audioProgressTimer.Tick -= OnAudioProgressTimerTick;
                    _audioProgressTimer.Stop();
                    _audioProgressTimer = null;
                }

                if (_audioPlayer != null)
                {
                    _audioPlayer.EndReached -= OnAudioPlaybackEnded;
                    _audioPlayer.EncounteredError -= OnAudioPlaybackError;
                    _audioPlayer.Stop();
                    _audioPlayer.Media = null;
                    _audioPlayer.Dispose();
                    _audioPlayer = null;
                }

                SafeDisposeCurrentAudioMedia();

                _audioLibVLC?.Dispose();
                _audioLibVLC = null;
                _currentAudioPath = null;

                _isInitialized = false;

                System.Diagnostics.Debug.WriteLine("[ShotEditorView] VLC resources disposed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Dispose error: {ex.Message}");
            }
        }

        // 移除事件处理程序
        AttachedToVisualTree -= OnAttachedToVisualTree;
        DetachedFromVisualTree -= OnDetachedFromVisualTree;

        if (_shot != null)
            _shot.PropertyChanged -= OnShotPropertyChanged;

        GC.SuppressFinalize(this);
    }

    private void OnDataContextChangedObservable(object? context)
    {
        if (_isDisposed) return;

        lock (_playerLock)
        {
            try
            {
                // 取消任何正在进行的加载
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource = null;

                if (_shot != null)
                    _shot.PropertyChanged -= OnShotPropertyChanged;

                var previousShot = _shot;
                _shot = context as ShotItem;

                if (_shot != null)
                    _shot.PropertyChanged += OnShotPropertyChanged;

                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] DataContext changed from Shot #{previousShot?.ShotNumber ?? 0} to Shot #{_shot?.ShotNumber ?? 0}");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] New shot GeneratedVideoPath: {_shot?.GeneratedVideoPath ?? "null"}");

                // 立即清空当前视频路径
                _currentVideoPath = null;
                StopAudioPlayback(resetProgress: true, clearMedia: true);

                // 如果播放器未初始化，设置标记稍后加载
                if (!_isInitialized)
                {
                    return;
                }

                // 清除当前播放
                ClearPlayerImmediate();

                // 加载新视频
                if (_shot?.GeneratedVideoPath != null && File.Exists(_shot.GeneratedVideoPath))
                {
                    // 使用 CancellationToken 防止竞态条件
                    _loadCancellationTokenSource = new CancellationTokenSource();
                    var token = _loadCancellationTokenSource.Token;

                    Task.Delay(100).ContinueWith(_ =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    LoadVideo(_shot.GeneratedVideoPath!, false);
                                }
                            });
                        }
                    }, token);
                }
                else
                {
                    // 完全清空播放器
                    ClearPlayerImmediate();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] OnDataContextChangedObservable error: {ex.Message}");
            }
        }
    }

    private void ClearPlayerImmediate()
    {
        // CRITICAL: 防止重复调用（幂等性保护）
        if (Interlocked.Exchange(ref _clearing, 1) == 1)
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] ClearPlayerImmediate already in progress, skipping");
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] ClearPlayerImmediate called");

            // CRITICAL: 1) 先停，断开 libvlc 内部播放/输出链路（关键）
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Media = null;
            }

            // CRITICAL: 2) 再解绑 VideoView，强制它释放旧的宿主输出目标
            if (VideoPlayer != null)
            {
                VideoPlayer.MediaPlayer = null;
                System.Diagnostics.Debug.WriteLine("[ShotEditorView] VideoPlayer.MediaPlayer unbound");
            }

            // CRITICAL: 3) 再释放当前 Media 对象
            SafeDisposeCurrentMedia();

            _currentVideoPath = null;

            System.Diagnostics.Debug.WriteLine("[ShotEditorView] Player cleared completely");
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] After Clear: VideoPlayer.MediaPlayer is null? {VideoPlayer?.MediaPlayer is null}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] ClearPlayerImmediate error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _clearing, 0);
        }
    }

    private void EnsureVideoViewBinding()
    {
        if (VideoPlayer == null || _mediaPlayer == null)
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] EnsureVideoViewBinding: VideoPlayer or MediaPlayer is null");
            return;
        }

        // CRITICAL: Only rebind if not already bound (真正的 "ensure")
        if (!ReferenceEquals(VideoPlayer.MediaPlayer, _mediaPlayer))
        {
            VideoPlayer.MediaPlayer = _mediaPlayer;
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] EnsureVideoViewBinding: Bound MediaPlayer to VideoView");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] EnsureVideoViewBinding: Already bound");
        }
    }

    private void OnShotPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isDisposed) return;

        if (e.PropertyName == nameof(ShotItem.GeneratedVideoPath))
        {
            if (!_isInitialized)
                return;

            lock (_playerLock)
            {
                var videoPath = _shot?.GeneratedVideoPath;
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] GeneratedVideoPath changed: {videoPath}");

                // 取消之前的加载
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource = new CancellationTokenSource();
                var token = _loadCancellationTokenSource.Token;

                Dispatcher.UIThread.Post(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        if (videoPath != null && File.Exists(videoPath))
                        {
                            LoadVideo(videoPath, false);
                        }
                        else
                        {
                            ClearPlayerImmediate();
                        }
                    }
                });
            }
        }
        else if (e.PropertyName == nameof(ShotItem.GeneratedAudioPath))
        {
            StopAudioPlayback(resetProgress: true, clearMedia: true);
        }
        else if (e.PropertyName == nameof(ShotItem.AudioDuration))
        {
            UpdateAudioPlaybackUi();
        }
    }

    private void OnTogglePlayClicked(object? sender, RoutedEventArgs e)
    {
        if (_isDisposed || !_isInitialized) return;

        lock (_playerLock)
        {
            var videoPath = _shot?.GeneratedVideoPath;

            if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
                return;

            // CRITICAL: Diagnose VideoView state
            try
            {
                System.Diagnostics.Debug.WriteLine($"[PlayClick] VideoPlayer is null? {VideoPlayer == null}");
                System.Diagnostics.Debug.WriteLine($"[PlayClick] VideoPlayer.IsVisible: {VideoPlayer?.IsVisible}");
                System.Diagnostics.Debug.WriteLine($"[PlayClick] VideoPlayer.Bounds: {VideoPlayer?.Bounds}");
                System.Diagnostics.Debug.WriteLine($"[PlayClick] VideoPlayer.IsAttachedToVisualTree: {VideoPlayer?.IsAttachedToVisualTree()}");
                System.Diagnostics.Debug.WriteLine($"[PlayClick] VideoPlayer.MediaPlayer is null? {VideoPlayer?.MediaPlayer == null}");
                System.Diagnostics.Debug.WriteLine($"[PlayClick] MediaPlayer instance: {_mediaPlayer?.GetHashCode()}");
                System.Diagnostics.Debug.WriteLine($"[PlayClick] MediaPlayer.Media is null? {_mediaPlayer?.Media == null}");

                if (VideoPlayer?.MediaPlayer != _mediaPlayer)
                {
                    System.Diagnostics.Debug.WriteLine("[PlayClick] WARNING: VideoPlayer.MediaPlayer is not bound to our MediaPlayer!");
                }

                // CRITICAL: Check if Bounds are invalid (X/Y = -1 means not arranged yet)
                // NOTE: X/Y = -1 is actually NORMAL for VideoView, don't use it as a check
                if (VideoPlayer?.Bounds.X == -1 || VideoPlayer?.Bounds.Y == -1)
                {
                    System.Diagnostics.Debug.WriteLine("[PlayClick] INFO: VideoPlayer has X/Y=-1 (this is normal for VideoView)");
                }

                // Check actual layout state
                var layoutable = VideoPlayer as Avalonia.Layout.Layoutable;
                System.Diagnostics.Debug.WriteLine($"[PlayClick] IsArrangeValid: {layoutable?.IsArrangeValid}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlayClick] Failed to diagnose: {ex.Message}");
            }

            var path = Path.GetFullPath(videoPath);

            // 如果不同视频或未加载，加载它
            if (string.IsNullOrEmpty(_currentVideoPath) ||
                !string.Equals(_currentVideoPath, path, StringComparison.OrdinalIgnoreCase))
            {
                LoadVideo(path, true);
                return;
            }

            // 切换播放/暂停
            _ = TogglePlayPauseAsync();
        }
    }

    private async Task TogglePlayPauseAsync()
    {
        if (_mediaPlayer == null || _mediaPlayer.Media == null)
        {
            System.Diagnostics.Debug.WriteLine("[TogglePlayPause] No media loaded");
            return;
        }

        if (_mediaPlayer.IsPlaying)
        {
            _mediaPlayer.Pause();
            System.Diagnostics.Debug.WriteLine("[TogglePlayPause] Paused");
        }
        else
        {
            // Use PlaySafeEmbed to ensure embedded output
            await PlaySafeEmbedAsync();
        }
    }

    private async Task PlaySafeEmbedAsync()
    {
        if (VideoPlayer == null || _mediaPlayer?.Media == null)
        {
            System.Diagnostics.Debug.WriteLine("[PlaySafeEmbed] Cannot play - player/media/view is null");
            return;
        }

        // 不依赖 X/Y（X/Y=-1 对 VideoView 是正常的）
        var laidOut = (VideoPlayer as Avalonia.Layout.Layoutable)?.IsArrangeValid == true;
        var sizeOk = VideoPlayer.Bounds.Width > 0 && VideoPlayer.Bounds.Height > 0;

        if (!VideoPlayer.IsAttachedToVisualTree() || !VideoPlayer.IsVisible || !laidOut || !sizeOk)
        {
            System.Diagnostics.Debug.WriteLine($"[PlaySafeEmbed] Not ready: attached={VideoPlayer.IsAttachedToVisualTree()}, visible={VideoPlayer.IsVisible}, arrange={laidOut}, bounds={VideoPlayer.Bounds}");

            // Not ready, set pending and wait for layout
            _pendingPlay = true;
            HookLayoutEvents();
            System.Diagnostics.Debug.WriteLine($"[PlaySafeEmbed] Not ready -> pending");
            return;
        }

        // CRITICAL: 保险重绑 - 每个"当前 media"只做一次，避免每次点播放都闪动/重置
        if (!_rebindDoneForThisMedia)
        {
            _rebindDoneForThisMedia = true;

            System.Diagnostics.Debug.WriteLine("[PlaySafeEmbed] Performing rebind to refresh embed output");

            // 断开再绑定，触发 VideoView 把嵌入式输出目标重新交给 libvlc
            VideoPlayer.MediaPlayer = null;
            await Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Render);

            VideoPlayer.MediaPlayer = _mediaPlayer;
            await Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Render);

            System.Diagnostics.Debug.WriteLine("[PlaySafeEmbed] Rebind done (embed output refreshed)");
        }

        lock (_playerLock)
        {
            try
            {
                if (_mediaPlayer?.Media != null && !_isDisposed && _viewReady)
                {
                    _mediaPlayer.Play();
                    System.Diagnostics.Debug.WriteLine("[PlaySafeEmbed] Play()");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlaySafeEmbed] Play error: {ex.Message}");
            }
        }
    }

    private void LoadVideo(string videoPath, bool autoplay)
    {
        if (_isDisposed || !_isInitialized || !_viewReady)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Cannot load video - disposed:{_isDisposed}, initialized:{_isInitialized}, viewReady:{_viewReady}");
            return;
        }

        // CRITICAL: Ensure we're on UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] LoadVideo called from non-UI thread, dispatching to UI thread");
            Dispatcher.UIThread.Post(() => LoadVideo(videoPath, autoplay));
            return;
        }

        lock (_playerLock)
        {
            try
            {
                if (_mediaPlayer == null || _libVLC == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] Media player not initialized");
                    return;
                }

                if (VideoPlayer == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] VideoPlayer control not available");
                    return;
                }

                // CRITICAL: Ensure binding BEFORE any operation
                EnsureVideoViewBinding();

                // 验证文件
                if (!IsPlayableFile(videoPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[ShotEditorView] File not playable: {videoPath}");
                    return;
                }

                var normalizedPath = Path.GetFullPath(videoPath);
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Loading video: {normalizedPath}");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] MediaPlayer instance: {_mediaPlayer.GetHashCode()}");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Thread: {Environment.CurrentManagedThreadId}, IsUIThread: {Dispatcher.UIThread.CheckAccess()}");

                // 停止当前播放
                _mediaPlayer.Stop();

                // CRITICAL: Clear Media reference before disposing
                _mediaPlayer.Media = null;

                // 等待一小段时间确保完全停止
                Thread.Sleep(50);

                // CRITICAL: 安全释放当前媒体
                SafeDisposeCurrentMedia();

                // 再次确保绑定（防止在 Stop/Dispose 过程中断开）
                EnsureVideoViewBinding();

                // 创建新媒体 - 不使用 using，持有引用
                _currentMedia = new Media(_libVLC, normalizedPath, FromType.FromPath);

                // 设置媒体到播放器
                _mediaPlayer.Media = _currentMedia;

                _currentVideoPath = normalizedPath;

                // CRITICAL: 重置 rebind 标记，每个新 media 都需要做一次保险重绑
                _rebindDoneForThisMedia = false;

                if (autoplay)
                {
                    // CRITICAL: Use PlaySafeEmbed to wait for layout completion and ensure embedded output
                    _ = PlaySafeEmbedAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] Video loaded (paused)");
                }
            }
            catch (AccessViolationException ave)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] AccessViolationException in LoadVideo: {ave.Message}");
                HandleAccessViolation();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] LoadVideo failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Stack trace: {ex.StackTrace}");

                // 出错时清理
                SafeDisposeCurrentMedia();
                _currentVideoPath = null;
            }
        }
    }

    private void SafeDisposeCurrentMedia()
    {
        try
        {
            if (_currentMedia != null)
            {
                System.Diagnostics.Debug.WriteLine("[ShotEditorView] SafeDisposeCurrentMedia: disposing media");

                // 先分离（如果还没分离）
                if (_mediaPlayer != null && _mediaPlayer.Media == _currentMedia)
                {
                    _mediaPlayer.Stop();

                    // 等待一小段时间
                    Thread.Sleep(30);

                    // 清空引用
                    _mediaPlayer.Media = null;
                }

                // 然后释放
                _currentMedia.Dispose();
                _currentMedia = null;

                System.Diagnostics.Debug.WriteLine("[ShotEditorView] Current media safely disposed");
            }
        }
        catch (AccessViolationException ave)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] AccessViolationException in SafeDisposeCurrentMedia: {ave.Message}");
            // 继续清理其他资源
            _currentMedia = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] SafeDisposeCurrentMedia error: {ex.Message}");
            _currentMedia = null;
        }
    }

    private async Task PlaySafeAsync()
    {
        if (_mediaPlayer == null || _mediaPlayer.Media == null || VideoPlayer == null)
        {
            System.Diagnostics.Debug.WriteLine("[PlaySafe] Cannot play - player/media/view is null");
            return;
        }

        // 给 2~3 帧 Render 时间（很多时候 1 帧不够）
        for (int i = 0; i < 3; i++)
            await Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Render);

        // 等待布局完成 (不要检查 X/Y，它们可能一直是 -1)
        for (int i = 0; i < 120; i++) // ~2s
        {
            var v = VideoPlayer;

            // CRITICAL: X/Y 不可靠，不要拿它当"是否布局完成"的依据
            var laidOut = (v is Avalonia.Layout.Layoutable l && l.IsArrangeValid);
            var sizeOk = v.Bounds.Width > 0 && v.Bounds.Height > 0;
            var attached = v.IsAttachedToVisualTree();
            var visible = v.IsVisible;

            if (attached && visible && laidOut && sizeOk)
            {
                lock (_playerLock)
                {
                    try
                    {
                        // 再次检查所有条件
                        if (_mediaPlayer?.Media != null &&
                            !_isDisposed &&
                            _viewReady &&
                            VideoPlayer?.MediaPlayer == _mediaPlayer)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PlaySafe] OK arrangeValid={laidOut} bounds={v.Bounds}, starting playback");
                            _mediaPlayer.Play();
                            System.Diagnostics.Debug.WriteLine("[PlaySafe] Video playing");
                            return;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[PlaySafe] Conditions not met, skipping play");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[PlaySafe] Play error: {ex.Message}");
                        return;
                    }
                }
            }

            await Task.Delay(16);
        }

        var finalLayoutable = VideoPlayer as Avalonia.Layout.Layoutable;
        System.Diagnostics.Debug.WriteLine($"[PlaySafe] Abort: not ready after 2s. attached={VideoPlayer.IsAttachedToVisualTree}, visible={VideoPlayer.IsVisible}, arrangeValid={finalLayoutable?.IsArrangeValid}, bounds={VideoPlayer.Bounds}");
    }

    private void HandleAccessViolation()
    {
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Handling AccessViolation - resetting player");

        lock (_playerLock)
        {
            try
            {
                // 标记为未初始化，防止在重置期间使用
                _isInitialized = false;

                // 完全重置
                _mediaPlayer?.Stop();
                _currentMedia = null;
                _currentVideoPath = null;

                // 延迟重新初始化
                Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        lock (_playerLock)
                        {
                            try
                            {
                                if (!_isDisposed && _viewReady && VideoPlayer != null)
                                {
                                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] Recreating MediaPlayer after AccessViolation");

                                    // 清理旧的
                                    if (_mediaPlayer != null)
                                    {
                                        try
                                        {
                                            _mediaPlayer.Stop();
                                            _mediaPlayer.Media = null;
                                            _mediaPlayer.Dispose();
                                        }
                                        catch { /* 忽略清理异常 */ }
                                    }

                                    if (_libVLC != null)
                                    {
                                        try
                                        {
                                            _libVLC.Dispose();
                                        }
                                        catch { /* 忽略清理异常 */ }
                                    }

                                    // 重新创建
                                    _libVLC = new LibVLC(
                                        "--no-video-title-show",
                                        "--no-snapshot-preview",
                                        "--no-video-deco"
                                    );
                                    _mediaPlayer = new MediaPlayer(_libVLC);

                                    // CRITICAL: 立即绑定到 VideoView
                                    VideoPlayer.MediaPlayer = _mediaPlayer;

                                    _isInitialized = true;

                                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] Player reset after AccessViolation");
                                    System.Diagnostics.Debug.WriteLine($"[ShotEditorView] New MediaPlayer instance: {_mediaPlayer.GetHashCode()}");

                                    // 重新加载当前视频
                                    if (_shot?.GeneratedVideoPath != null && File.Exists(_shot.GeneratedVideoPath))
                                    {
                                        Task.Delay(300).ContinueWith(__ =>
                                        {
                                            Dispatcher.UIThread.Post(() =>
                                                LoadVideo(_shot.GeneratedVideoPath!, false));
                                        });
                                    }
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine("[ShotEditorView] Cannot reset player - view not ready");
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Failed to reset player: {ex.Message}");
                            }
                        }
                    });
                });
            }
            catch { /* 忽略所有异常 */ }
        }
    }

    // Old synchronous version - kept for compatibility but should not be used
    private void TogglePlayPause()
    {
        _ = TogglePlayPauseAsync();
    }

    private bool IsPlayableFile(string path)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] File does not exist: {path}");
                return false;
            }

            if (fi.Length < 1024)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] File too small: {fi.Length} bytes");
                return false;
            }

            // 尝试打开文件
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] File not playable: {ex.Message}");
            return false;
        }
    }

    private async void OnUploadFirstFrameClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShotItem shot)
            return;

        var path = await PickImagePathAsync("选择首帧图片");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var imported = await ImportToLibraryAsync(path);
        ApplyImageToShot(shot, imported ?? path, isFirstFrame: true);
    }

    private async void OnPickFirstFrameFromLibraryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShotItem shot)
            return;

        var libraryDir = GetResourceLibraryDirectory();
        var path = await PickImagePathAsync("从资源库选择首帧", libraryDir);
        if (string.IsNullOrWhiteSpace(path))
            return;

        ApplyImageToShot(shot, path, isFirstFrame: true);
    }

    private async void OnUploadLastFrameClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShotItem shot)
            return;

        var path = await PickImagePathAsync("选择尾帧图片");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var imported = await ImportToLibraryAsync(path);
        ApplyImageToShot(shot, imported ?? path, isFirstFrame: false);
    }

    private async void OnPickLastFrameFromLibraryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShotItem shot)
            return;

        var libraryDir = GetResourceLibraryDirectory();
        var path = await PickImagePathAsync("从资源库选择尾帧", libraryDir);
        if (string.IsNullOrWhiteSpace(path))
            return;

        ApplyImageToShot(shot, path, isFirstFrame: false);
    }

    private async Task<string?> PickImagePathAsync(string title, string? initialDirectory = null)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("图片文件")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }

    private string GetResourceLibraryDirectory()
    {
        try
        {
            var storagePathService = App.Services.GetRequiredService<StoragePathService>();
            return storagePathService.GetResourceLibraryDirectory();
        }
        catch
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "StoryboardLibrary");
        }
    }

    private async Task<string?> ImportToLibraryAsync(string sourcePath)
    {
        try
        {
            var libraryDir = GetResourceLibraryDirectory();
            Directory.CreateDirectory(libraryDir);

            var fileName = Path.GetFileName(sourcePath);
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var destPath = Path.Combine(libraryDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}");

            await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: false));
            return destPath;
        }
        catch
        {
            return null;
        }
    }

    private static void ApplyImageToShot(ShotItem shot, string path, bool isFirstFrame)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var assets = isFirstFrame ? shot.FirstFrameAssets : shot.LastFrameAssets;
        var existing = assets.FirstOrDefault(a =>
            string.Equals(a.FilePath, path, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            existing = new ShotAssetItem
            {
                FilePath = path,
                ThumbnailPath = path,
                Type = isFirstFrame ? ShotAssetType.FirstFrameImage : ShotAssetType.LastFrameImage,
                CreatedAt = DateTimeOffset.Now,
                IsSelected = true
            };
            assets.Add(existing);
        }

        foreach (var asset in assets)
            asset.IsSelected = ReferenceEquals(asset, existing);

        if (isFirstFrame)
            shot.FirstFrameImagePath = path;
        else
            shot.LastFrameImagePath = path;
    }

    private async void OnUploadVideoClicked(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] OnUploadVideoClicked called");

        if (DataContext is not ShotItem shot)
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] DataContext is not ShotItem");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Opening video file picker...");
        var path = await PickVideoPathAsync("选择视频文件");
        if (string.IsNullOrWhiteSpace(path))
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] No video file selected");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Selected video: {path}");
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Importing to library...");
        var imported = await ImportToLibraryAsync(path);
        var videoPath = imported ?? path;
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Video path after import: {videoPath}");

        // Generate thumbnail for the video
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Generating thumbnail...");
        var thumbnailPath = await GenerateVideoThumbnailAsync(videoPath);
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Thumbnail path: {thumbnailPath ?? "null"}");

        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Applying video to shot...");
        ApplyVideoToShot(shot, videoPath, thumbnailPath);
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Video upload complete");
    }

    private async void OnUploadAudioClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ShotItem shot)
            return;

        var path = await PickAudioPathAsync("选择音频文件");
        if (string.IsNullOrWhiteSpace(path))
            return;

        var importedPath = await ImportAudioForShotAsync(shot, path);
        if (string.IsNullOrWhiteSpace(importedPath))
        {
            shot.AudioStatusMessage = "音频上传失败";
            return;
        }

        shot.GeneratedAudioPath = importedPath;
        shot.AudioDuration = await TryGetAudioDurationAsync(importedPath);
        shot.AudioStatusMessage = $"已上传音频：{Path.GetFileName(importedPath)}";
        shot.NotifyPropertyChanged(nameof(shot.HasGeneratedAudio));
        StopAudioPlayback(resetProgress: true, clearMedia: true);
    }

    private async void OnPickVideoFromLibraryClicked(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] OnPickVideoFromLibraryClicked called");

        if (DataContext is not ShotItem shot)
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] DataContext is not ShotItem");
            return;
        }

        var libraryDir = GetResourceLibraryDirectory();
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Library directory: {libraryDir}");
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Opening video file picker from library...");

        var path = await PickVideoPathAsync("从资源库选择视频", libraryDir);
        if (string.IsNullOrWhiteSpace(path))
        {
            System.Diagnostics.Debug.WriteLine("[ShotEditorView] No video file selected from library");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Selected video from library: {path}");

        // Generate thumbnail for the video
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Generating thumbnail...");
        var thumbnailPath = await GenerateVideoThumbnailAsync(path);
        System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Thumbnail path: {thumbnailPath ?? "null"}");

        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Applying video to shot...");
        ApplyVideoToShot(shot, path, thumbnailPath);
        System.Diagnostics.Debug.WriteLine("[ShotEditorView] Video selection from library complete");
    }

    private async Task<string?> PickVideoPathAsync(string title, string? initialDirectory = null)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("视频文件")
                {
                    Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.webm", "*.flv", "*.wmv" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> PickAudioPathAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("音频文件")
                {
                    Patterns = new[] { "*.mp3", "*.wav", "*.m4a", "*.aac", "*.flac", "*.opus", "*.ogg", "*.wma", "*.pcm" }
                },
                new FilePickerFileType("所有文件")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return files?.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> ImportAudioForShotAsync(ShotItem shot, string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                return null;

            var targetDir = ResolveProjectAudioDirectory();
            Directory.CreateDirectory(targetDir);

            var ext = Path.GetExtension(sourcePath);
            var fileName = $"shot_{shot.ShotNumber}_upload_{DateTime.Now:yyyyMMdd_HHmmssfff}{ext}";
            var destinationPath = Path.Combine(targetDir, fileName);

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
                return sourcePath;

            await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite: false));
            return destinationPath;
        }
        catch
        {
            return null;
        }
    }

    private string ResolveProjectAudioDirectory()
    {
        var projectId = TryGetCurrentProjectId();
        if (!string.IsNullOrWhiteSpace(projectId))
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Storyboard",
                "output",
                "projects",
                projectId,
                "audio");
        }

        return Path.Combine(GetResourceLibraryDirectory(), "audio");
    }

    private string? TryGetCurrentProjectId()
    {
        if (TopLevel.GetTopLevel(this) is Window window &&
            window.DataContext is MainViewModel mainViewModel &&
            !string.IsNullOrWhiteSpace(mainViewModel.CurrentProjectId))
        {
            return mainViewModel.CurrentProjectId;
        }

        return null;
    }

    private async Task<double> TryGetAudioDurationAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return 0;

        try
        {
            var ffprobePath = FfmpegLocator.GetFfprobePath();
            var args = $"-v error -print_format json -show_format \"{path}\"";

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffprobePath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return 0;

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
                return 0;

            using var doc = JsonDocument.Parse(stdout);
            if (doc.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var durationElement) &&
                double.TryParse(durationElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var duration) &&
                duration > 0)
            {
                return duration;
            }
        }
        catch
        {
            // Ignore probing failures and keep duration as 0.
        }

        return 0;
    }

    private async Task<string?> GenerateVideoThumbnailAsync(string videoPath)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] GenerateVideoThumbnailAsync started for: {videoPath}");

            if (!File.Exists(videoPath))
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Video file does not exist: {videoPath}");
                return null;
            }

            var libraryDir = GetResourceLibraryDirectory();
            Directory.CreateDirectory(libraryDir);
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Library directory: {libraryDir}");

            var fileName = Path.GetFileNameWithoutExtension(videoPath);
            var thumbnailPath = Path.Combine(libraryDir, $"{fileName}_thumb_{DateTime.Now:yyyyMMdd_HHmmssfff}.jpg");
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Target thumbnail path: {thumbnailPath}");

            // Use FFmpeg to extract a frame at 1 second
            var ffmpegPath = FfmpegLocator.GetFfmpegPath();
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg path: {ffmpegPath}");

            if (!File.Exists(ffmpegPath) && ffmpegPath != "ffmpeg")
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg not found at: {ffmpegPath}");
                return null;
            }

            var arguments = $"-i \"{videoPath}\" -ss 00:00:01 -vframes 1 -q:v 2 \"{thumbnailPath}\"";
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg arguments: {arguments}");

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            System.Diagnostics.Debug.WriteLine("[ShotEditorView] Starting FFmpeg process...");
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg process started, PID: {process.Id}");

                // Read output asynchronously
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();
                System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg process exited with code: {process.ExitCode}");

                var output = await outputTask;
                var error = await errorTask;

                if (!string.IsNullOrWhiteSpace(output))
                    System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg stdout: {output}");
                if (!string.IsNullOrWhiteSpace(error))
                    System.Diagnostics.Debug.WriteLine($"[ShotEditorView] FFmpeg stderr: {error}");

                if (File.Exists(thumbnailPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Thumbnail generated successfully: {thumbnailPath}");
                    return thumbnailPath;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Thumbnail file was not created");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[ShotEditorView] Failed to start FFmpeg process");
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Failed to generate thumbnail: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShotEditorView] Stack trace: {ex.StackTrace}");
            return null;
        }
    }

    private static void ApplyVideoToShot(ShotItem shot, string path, string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var assets = shot.VideoAssets;
        var existing = assets.FirstOrDefault(a =>
            string.Equals(a.FilePath, path, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            existing = new ShotAssetItem
            {
                FilePath = path,
                ThumbnailPath = thumbnailPath ?? path,
                Type = ShotAssetType.GeneratedVideo,
                CreatedAt = DateTimeOffset.Now,
                IsSelected = true
            };
            assets.Add(existing);
        }
        else
        {
            // Update thumbnail if a new one was generated
            if (!string.IsNullOrWhiteSpace(thumbnailPath))
                existing.ThumbnailPath = thumbnailPath;
        }

        foreach (var asset in assets)
            asset.IsSelected = ReferenceEquals(asset, existing);

        shot.GeneratedVideoPath = path;
    }
}
