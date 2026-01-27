using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Storyboard.Models;
using Storyboard.ViewModels;
using LibVLCSharp.Shared;
using LibVLCSharp.Avalonia;
using System;
using System.IO;

namespace Storyboard.Views;

public partial class ShotCardView : UserControl
{
    private const double DragThreshold = 4;

    private bool _isPointerDownOnHandle;
    private bool _isDragging;
    private Point _dragStartPoint;

    public ShotCardView()
    {
        InitializeComponent();
    }

    public void SetPlaceholderState(bool isPlaceholder)
    {
        // Keep layout space but visually empty.
        RootBorder.Opacity = isPlaceholder ? 0 : 1;
        RootBorder.IsHitTestVisible = !isPlaceholder;
    }

    public void SetDraggingState(bool isDragging)
    {
        if (isDragging)
            RootBorder.Classes.Add("dragging");
        else
            RootBorder.Classes.Remove("dragging");

        if (DraggingPill != null)
            DraggingPill.Opacity = isDragging ? 1 : 0;

        // Cursor feedback: keep it obvious that we're dragging.
        try
        {
            RootBorder.Cursor = new Cursor(isDragging ? StandardCursorType.SizeAll : StandardCursorType.Hand);
        }
        catch
        {
            // Some platforms may not support all cursor types; ignore.
        }
    }

    public void FlashDrop()
    {
        RootBorder.Classes.Add("drop-flash");
        var _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await System.Threading.Tasks.Task.Delay(160);
            RootBorder.Classes.Remove("drop-flash");
        });
    }

    private void OnCardClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot)
        {
            // Find the MainWindow and set the selected shot
            var mainWindow = this.FindAncestorOfType<Window>();
            if (mainWindow?.DataContext is MainViewModel viewModel)
            {
                viewModel.SelectedShot = shot;
            }
        }
    }

    private void OnDragHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPointerDownOnHandle = true;
            _isDragging = false;
            _dragStartPoint = e.GetPosition(this);

            e.Pointer.Capture(sender as IInputElement);
            e.Handled = true;
        }
    }

    private void OnDragHandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDownOnHandle)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isPointerDownOnHandle = false;
            return;
        }

        var pos = e.GetPosition(this);
        var dx = pos.X - _dragStartPoint.X;
        var dy = pos.Y - _dragStartPoint.Y;
        var listView = this.FindAncestorOfType<ShotListView>();
        if (listView == null)
            return;

        if (!_isDragging)
        {
            if (Math.Abs(dx) < DragThreshold && Math.Abs(dy) < DragThreshold)
                return;

            if (DataContext is not ShotItem shot)
                return;

            _isDragging = true;
            SetDraggingState(true);
            listView.BeginInternalDrag(this, shot, e);
        }
        // Once dragging begins, ShotListView owns pointer events (via pointer capture).
    }

    private void OnDragHandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        var listView = this.FindAncestorOfType<ShotListView>();
        if (_isDragging && listView != null)
        {
            listView.EndInternalDrag(e);
        }

        _isPointerDownOnHandle = false;
        _isDragging = false;
        e.Pointer.Capture(null);
        SetDraggingState(false);
        e.Handled = true;
    }

    private void OnDragHandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        // Capture will intentionally transfer to ShotListView when dragging starts.
        // Do not cancel here; ShotListView will cancel/end reliably.
        _isPointerDownOnHandle = false;
    }

    private void OnMaterialImagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot && !string.IsNullOrEmpty(shot.MaterialFilePath))
        {
            ShowImageDialog(shot.MaterialFilePath, "素材图片");
        }
    }

    private void OnFirstFrameImagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot && !string.IsNullOrEmpty(shot.FirstFrameImagePath))
        {
            ShowImageDialog(shot.FirstFrameImagePath, "首帧图片");
        }
    }

    private void OnLastFrameImagePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot && !string.IsNullOrEmpty(shot.LastFrameImagePath))
        {
            ShowImageDialog(shot.LastFrameImagePath, "尾帧图片");
        }
    }

    private void OnVideoThumbnailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShotItem shot && !string.IsNullOrEmpty(shot.GeneratedVideoPath))
        {
            ShowVideoPlayerDialog(shot.GeneratedVideoPath, $"分镜 #{shot.ShotNumber} - 视频播放");
        }
    }

    private async void ShowVideoPlayerDialog(string videoPath, string title)
    {
        try
        {
            if (!File.Exists(videoPath))
            {
                System.Diagnostics.Debug.WriteLine($"[ShotCardView] Video file not found: {videoPath}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ShotCardView] Opening video player for: {videoPath}");

            // Create a simple video player window
            var playerWindow = new Window
            {
                Title = title,
                Width = 1280,
                Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = true
            };

            // Create VLC player
            LibVLC? libVLC = null;
            MediaPlayer? mediaPlayer = null;
            Media? media = null;

            try
            {
                // Ensure we're on UI thread
                await Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Initializing LibVLC...");

                        // Initialize LibVLC with minimal options
                        libVLC = new LibVLC(enableDebugLogs: false);

                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Creating MediaPlayer...");
                        mediaPlayer = new MediaPlayer(libVLC);

                        // Create VideoView
                        var videoView = new VideoView
                        {
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                            Cursor = new Cursor(StandardCursorType.Hand)
                        };

                        playerWindow.Content = videoView;

                        // Show window first to ensure it's rendered
                        var mainWindow = this.FindAncestorOfType<Window>();
                        if (mainWindow != null)
                        {
                            playerWindow.Show(mainWindow);
                        }
                        else
                        {
                            playerWindow.Show();
                        }

                        // Wait for window to be fully rendered
                        await System.Threading.Tasks.Task.Delay(500);

                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Binding MediaPlayer to VideoView...");

                        // Bind media player to video view AFTER window is shown
                        videoView.MediaPlayer = mediaPlayer;

                        // Click video to play/pause
                        videoView.PointerPressed += (s, e) =>
                        {
                            try
                            {
                                if (mediaPlayer != null)
                                {
                                    if (mediaPlayer.IsPlaying)
                                        mediaPlayer.Pause();
                                    else
                                        mediaPlayer.Play();
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error toggling playback: {ex.Message}");
                            }
                        };

                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Loading media...");

                        // Load video
                        media = new Media(libVLC, videoPath, FromType.FromPath);
                        mediaPlayer.Media = media;

                        // Cleanup on window close
                        playerWindow.Closed += async (s, e) =>
                        {
                            await Dispatcher.UIThread.InvokeAsync(async () =>
                            {
                                try
                                {
                                    System.Diagnostics.Debug.WriteLine("[ShotCardView] Cleaning up VLC resources...");

                                    // Step 1: Stop playback
                                    if (mediaPlayer != null)
                                    {
                                        try
                                        {
                                            mediaPlayer.Stop();
                                            System.Diagnostics.Debug.WriteLine("[ShotCardView] MediaPlayer stopped");
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error stopping player: {ex.Message}");
                                        }
                                    }

                                    // Step 2: Wait a bit for VLC to finish stopping
                                    await System.Threading.Tasks.Task.Delay(100);

                                    // Step 3: Unbind MediaPlayer from VideoView
                                    if (videoView != null)
                                    {
                                        try
                                        {
                                            videoView.MediaPlayer = null;
                                            System.Diagnostics.Debug.WriteLine("[ShotCardView] VideoView unbound");
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error unbinding VideoView: {ex.Message}");
                                        }
                                    }

                                    // Step 4: Wait a bit more
                                    await System.Threading.Tasks.Task.Delay(100);

                                    // Step 5: Dispose in correct order
                                    try
                                    {
                                        media?.Dispose();
                                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Media disposed");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error disposing media: {ex.Message}");
                                    }

                                    try
                                    {
                                        mediaPlayer?.Dispose();
                                        System.Diagnostics.Debug.WriteLine("[ShotCardView] MediaPlayer disposed");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error disposing mediaPlayer: {ex.Message}");
                                    }

                                    try
                                    {
                                        libVLC?.Dispose();
                                        System.Diagnostics.Debug.WriteLine("[ShotCardView] LibVLC disposed");
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error disposing libVLC: {ex.Message}");
                                    }

                                    System.Diagnostics.Debug.WriteLine("[ShotCardView] VLC cleanup complete");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ShotCardView] Cleanup error: {ex.Message}");
                                }
                            });
                        };

                        // Auto-play after a short delay
                        await System.Threading.Tasks.Task.Delay(300);

                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Starting playback...");
                        mediaPlayer?.Play();
                        System.Diagnostics.Debug.WriteLine("[ShotCardView] Video player initialized successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error in UI thread: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[ShotCardView] Stack trace: {ex.StackTrace}");
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error creating VLC player: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ShotCardView] Exception type: {ex.GetType().Name}");

                // Cleanup on error
                try
                {
                    mediaPlayer?.Dispose();
                    media?.Dispose();
                    libVLC?.Dispose();
                }
                catch { }

                // Close the window if it was created
                try
                {
                    playerWindow?.Close();
                }
                catch { }

                // Fallback to system player
                System.Diagnostics.Debug.WriteLine("[ShotCardView] Falling back to system player...");
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = videoPath,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShotCardView] Fallback player error: {fallbackEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ShotCardView] Error opening video: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ShotCardView] Stack trace: {ex.StackTrace}");
        }
    }

    private void ShowImageDialog(string imagePath, string title)
    {
        try
        {
            var bitmap = new Avalonia.Media.Imaging.Bitmap(imagePath);
            var window = new Window
            {
                Title = title,
                Width = 800,
                Height = 600,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new Image
                {
                    Source = bitmap,
                    Stretch = Avalonia.Media.Stretch.Uniform
                }
            };

            var mainWindow = this.FindAncestorOfType<Window>();
            if (mainWindow != null)
            {
                window.ShowDialog(mainWindow);
            }
            else
            {
                window.Show();
            }
        }
        catch (Exception ex)
        {
            // Handle error, perhaps show a message
            Console.WriteLine($"Error loading image: {ex.Message}");
        }
    }
}
