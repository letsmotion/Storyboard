using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;
using Storyboard.Messages;
using Storyboard.Models.Timeline;
using Storyboard.ViewModels.Timeline;
using System;
using System.ComponentModel;

namespace Storyboard.Views.Timeline;

public partial class TimelineClipView : UserControl
{
    private Point? _dragStartPoint;
    private double _initialClipPosition;
    private bool _isDragging;
    private bool _isResizing;
    private ResizeMode _resizeMode;
    private TimelineEditorViewModel? _viewModel;
    private System.Timers.Timer? _autoScrollTimer;
    private ScrollViewer? _scrollViewer;
    private const double AutoScrollEdgeThreshold = 50; // pixels from edge
    private const double AutoScrollSpeed = 10; // pixels per tick

    private enum ResizeMode
    {
        None,
        Left,
        Right
    }

    public TimelineClipView()
    {
        InitializeComponent();

        // 诊断日志
        System.Diagnostics.Debug.WriteLine($"TimelineClipView 构造函数被调用");

        DataContextChanged += OnDataContextChanged;

        // 添加指针事件处理
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
    }

    protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // 为调整大小手柄添加事件处理
        var leftHandle = this.FindControl<Border>("LeftResizeHandle");
        var rightHandle = this.FindControl<Border>("RightResizeHandle");

        if (leftHandle != null)
        {
            leftHandle.PointerPressed += OnLeftResizeHandlePressed;
        }

        if (rightHandle != null)
        {
            rightHandle.PointerPressed += OnRightResizeHandlePressed;
        }
    }

    private void OnLeftResizeHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _dragStartPoint = e.GetPosition(this.Parent as Visual);
        _initialClipPosition = clip.PixelPosition;
        _isResizing = true;
        _resizeMode = ResizeMode.Left;

        _viewModel = FindTimelineEditorViewModel();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnRightResizeHandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _dragStartPoint = e.GetPosition(this.Parent as Visual);
        _initialClipPosition = clip.PixelPosition;
        _isResizing = true;
        _resizeMode = ResizeMode.Right;

        _viewModel = FindTimelineEditorViewModel();
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is TimelineClip clip)
        {
            System.Diagnostics.Debug.WriteLine($"TimelineClipView DataContext 设置: ClipId={clip.Id}, StartTime={clip.StartTime}s, PixelWidth={clip.PixelWidth}px");

            // 暂时注释掉以保持测试颜色
            UpdateClipAppearance(clip);

            // 订阅属性变化以更新选中状态
            clip.PropertyChanged -= OnClipPropertyChanged;
            clip.PropertyChanged += OnClipPropertyChanged;
        }
    }

    private void OnClipPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is TimelineClip clip)
        {
            if (e.PropertyName == nameof(TimelineClip.IsSelected) ||
                e.PropertyName == nameof(TimelineClip.Status) ||
                e.PropertyName == nameof(TimelineClip.IsDragging))
            {
                UpdateClipAppearance(clip);
            }
        }
    }

    private void UpdateClipAppearance(TimelineClip clip)
    {
        // 拖动时显示增强的视觉效果
        if (clip.IsDragging)
        {
            ClipBorder.Opacity = 0.7;
            ClipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#8b5cf6")); // 紫色 - 拖动中
            ClipBorder.BorderThickness = new Avalonia.Thickness(2);
            return;
        }
        else
        {
            ClipBorder.Opacity = 1.0;
        }

        // 根据选中状态和状态设置边框
        if (clip.IsSelected)
        {
            // 选中时使用高亮颜色和更粗的边框
            ClipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#60a5fa"));
            ClipBorder.BorderThickness = new Avalonia.Thickness(2);
            ClipBorder.Background = new SolidColorBrush(Color.Parse("#3f3f46"));
        }
        else
        {
            // 原视频片段使用绿色边框
            if (clip.ShotNumber == 0)
            {
                ClipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#10b981")); // 绿色 - 原视频
                ClipBorder.BorderThickness = new Avalonia.Thickness(1);
                ClipBorder.Background = new SolidColorBrush(Color.Parse("#27272a"));
            }
            else
            {
                // 未选中时根据状态设置边框颜色
                var borderBrush = clip.Status switch
                {
                    ClipStatus.Generated => new SolidColorBrush(Color.Parse("#3b82f6")),    // 蓝色 - 已生成
                    ClipStatus.Placeholder => new SolidColorBrush(Color.Parse("#52525b")),  // 灰色 - 占位符
                    ClipStatus.Generating => new SolidColorBrush(Color.Parse("#f59e0b")),   // 橙色 - 生成中
                    ClipStatus.Error => new SolidColorBrush(Color.Parse("#ef4444")),        // 红色 - 错误
                    _ => new SolidColorBrush(Color.Parse("#52525b"))
                };

                ClipBorder.BorderBrush = borderBrush;
                ClipBorder.BorderThickness = new Avalonia.Thickness(1);
                ClipBorder.Background = new SolidColorBrush(Color.Parse("#27272a"));
            }
        }
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;
        if (clip.IsDragging) return; // 拖动时不改变悬停效果

        // 悬停时增强背景亮度
        if (!clip.IsSelected)
        {
            ClipBorder.Background = new SolidColorBrush(Color.Parse("#2d2d32"));
        }
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;
        if (clip.IsDragging) return; // 拖动时不改变效果

        // 恢复正常背景
        if (!clip.IsSelected)
        {
            ClipBorder.Background = new SolidColorBrush(Color.Parse("#27272a"));
        }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;

        // 只处理左键
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        // 记录拖动起始点（在父Canvas坐标系中）
        _dragStartPoint = e.GetPosition(this.Parent as Visual);
        // 记录片段的初始位置
        _initialClipPosition = clip.PixelPosition;

        // 捕获指针
        e.Pointer.Capture(this);

        // 发送选中消息
        WeakReferenceMessenger.Default.Send(new ClipSelectedMessage(clip));

        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;
        if (_dragStartPoint == null) return;
        if (e.Pointer.Captured != this) return;

        var currentPoint = e.GetPosition(this.Parent as Visual);
        var delta = currentPoint - _dragStartPoint.Value;

        // Handle resizing
        if (_isResizing && _viewModel != null)
        {
            if (_resizeMode == ResizeMode.Left)
            {
                // Left edge resize: adjust StartTime and Duration
                var newStartTime = (_initialClipPosition + delta.X) / clip.PixelsPerSecond;
                var newDuration = clip.Duration - (delta.X / clip.PixelsPerSecond);

                // Prevent duration from going below 0.1 seconds
                if (newDuration >= 0.1)
                {
                    // TODO: Implement trim/resize through ViewModel
                    // For now, just update visual feedback
                }
            }
            else if (_resizeMode == ResizeMode.Right)
            {
                // Right edge resize: adjust Duration only
                var newDuration = clip.Duration + (delta.X / clip.PixelsPerSecond);

                // Prevent duration from going below 0.1 seconds
                if (newDuration >= 0.1)
                {
                    // TODO: Implement trim/resize through ViewModel
                    // For now, just update visual feedback
                }
            }

            e.Handled = true;
            return;
        }

        // Handle dragging
        // 检查是否超过拖动阈值（5像素）
        if (!_isDragging && Math.Abs(delta.X) > 5)
        {
            _isDragging = true;

            // 从视觉树中查找 TimelineEditorViewModel
            _viewModel = FindTimelineEditorViewModel();

            if (_viewModel != null)
            {
                _viewModel.BeginClipDrag(clip);
                Cursor = new Cursor(StandardCursorType.SizeAll);

                // Find ScrollViewer and start auto-scroll timer
                _scrollViewer = FindScrollViewer();
                StartAutoScrollTimer();
            }
            else
            {
                // 如果找不到ViewModel，取消拖动
                _dragStartPoint = null;
                e.Pointer.Capture(null);
                return;
            }
        }

        if (_isDragging && _viewModel != null)
        {
            // 修复：使用初始位置 + 鼠标移动距离，而不是当前位置 + 距离
            // 这样可以避免拖动时的跳跃问题
            var newPixelPosition = _initialClipPosition + delta.X;

            // 更新拖动位置
            _viewModel.UpdateClipDragPosition(clip, newPixelPosition);

            // Check for auto-scroll
            CheckAutoScroll(e);
        }

        e.Handled = true;
    }

    /// <summary>
    /// 检查是否需要自动滚动
    /// </summary>
    private void CheckAutoScroll(PointerEventArgs e)
    {
        if (_scrollViewer == null) return;

        var position = e.GetPosition(_scrollViewer);
        var viewport = _scrollViewer.Viewport;

        // Check horizontal edges
        if (position.X < AutoScrollEdgeThreshold)
        {
            // Near left edge - scroll left
            var offset = _scrollViewer.Offset;
            _scrollViewer.Offset = new Vector(Math.Max(0, offset.X - AutoScrollSpeed), offset.Y);
        }
        else if (position.X > viewport.Width - AutoScrollEdgeThreshold)
        {
            // Near right edge - scroll right
            var offset = _scrollViewer.Offset;
            var maxX = _scrollViewer.Extent.Width - viewport.Width;
            _scrollViewer.Offset = new Vector(Math.Min(maxX, offset.X + AutoScrollSpeed), offset.Y);
        }

        // Check vertical edges
        if (position.Y < AutoScrollEdgeThreshold)
        {
            // Near top edge - scroll up
            var offset = _scrollViewer.Offset;
            _scrollViewer.Offset = new Vector(offset.X, Math.Max(0, offset.Y - AutoScrollSpeed));
        }
        else if (position.Y > viewport.Height - AutoScrollEdgeThreshold)
        {
            // Near bottom edge - scroll down
            var offset = _scrollViewer.Offset;
            var maxY = _scrollViewer.Extent.Height - viewport.Height;
            _scrollViewer.Offset = new Vector(offset.X, Math.Min(maxY, offset.Y + AutoScrollSpeed));
        }
    }

    /// <summary>
    /// 启动自动滚动定时器
    /// </summary>
    private void StartAutoScrollTimer()
    {
        if (_autoScrollTimer != null) return;

        _autoScrollTimer = new System.Timers.Timer(50); // 20fps
        _autoScrollTimer.Elapsed += (s, e) =>
        {
            // Timer runs on background thread, but we don't need to do anything here
            // Auto-scroll is handled in CheckAutoScroll during pointer move
        };
        _autoScrollTimer.Start();
    }

    /// <summary>
    /// 停止自动滚动定时器
    /// </summary>
    private void StopAutoScrollTimer()
    {
        if (_autoScrollTimer != null)
        {
            _autoScrollTimer.Stop();
            _autoScrollTimer.Dispose();
            _autoScrollTimer = null;
        }
    }

    /// <summary>
    /// 从视觉树中查找 ScrollViewer
    /// </summary>
    private ScrollViewer? FindScrollViewer()
    {
        ILogical? current = this;
        while (current != null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
            current = current.GetLogicalParent();
        }
        return null;
    }

    /// <summary>
    /// 从逻辑树中查找 TimelineEditorViewModel
    /// </summary>
    private TimelineEditorViewModel? FindTimelineEditorViewModel()
    {
        // 向上遍历逻辑树，查找包含 TimelineEditorViewModel 的控件
        ILogical? current = this;
        while (current != null)
        {
            if (current is Control control && control.DataContext is TimelineEditorViewModel vm)
            {
                return vm;
            }
            current = current.GetLogicalParent();
        }
        return null;
    }

    private async void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not TimelineClip clip) return;

        // 释放指针捕获
        e.Pointer.Capture(null);

        // 停止自动滚动
        StopAutoScrollTimer();
        _scrollViewer = null;

        if (_isResizing)
        {
            // TODO: Implement resize completion through ViewModel
            // For now, just reset state
            _isResizing = false;
            _resizeMode = ResizeMode.None;
            _viewModel = null;
        }
        else if (_isDragging && _viewModel != null)
        {
            // 结束拖动
            await _viewModel.EndClipDrag(clip, clip.DragOffsetX);

            _isDragging = false;
            _viewModel = null;
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        _dragStartPoint = null;
        e.Handled = true;
    }
}
