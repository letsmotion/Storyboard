using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Messaging;
using Storyboard.Messages;
using Storyboard.Models.Timeline;
using System.ComponentModel;

namespace Storyboard.Views.Timeline;

public partial class TimelineClipView : UserControl
{
    public TimelineClipView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is TimelineClip clip)
        {
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
                e.PropertyName == nameof(TimelineClip.Status))
            {
                UpdateClipAppearance(clip);
            }
        }
    }

    private void UpdateClipAppearance(TimelineClip clip)
    {
        // 根据选中状态和状态设置边框
        if (clip.IsSelected)
        {
            // 选中时使用高亮颜色和更粗的边框
            ClipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#60a5fa"));
            ClipBorder.BorderThickness = new Avalonia.Thickness(3);
            ClipBorder.Background = new SolidColorBrush(Color.Parse("#3f3f46"));
        }
        else
        {
            // 原视频片段使用绿色边框
            if (clip.ShotNumber == 0)
            {
                ClipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#10b981")); // 绿色 - 原视频
                ClipBorder.BorderThickness = new Avalonia.Thickness(2);
                ClipBorder.Background = new SolidColorBrush(Color.Parse("#27272a"));
            }
            else
            {
                // 未选中时根据状态设置边框颜色
                var borderBrush = clip.Status switch
                {
                    ClipStatus.Generated => new SolidColorBrush(Color.Parse("#3b82f6")),    // 蓝色 - 已生成
                    ClipStatus.Placeholder => new SolidColorBrush(Color.Parse("#71717a")),  // 灰色 - 占位符
                    ClipStatus.Generating => new SolidColorBrush(Color.Parse("#f59e0b")),   // 橙色 - 生成中
                    ClipStatus.Error => new SolidColorBrush(Color.Parse("#ef4444")),        // 红色 - 错误
                    _ => new SolidColorBrush(Color.Parse("#71717a"))
                };

                ClipBorder.BorderBrush = borderBrush;
                ClipBorder.BorderThickness = new Avalonia.Thickness(2);
                ClipBorder.Background = new SolidColorBrush(Color.Parse("#27272a"));
            }
        }
    }

    private void OnClipPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is TimelineClip clip)
        {
            // 发送片段选中消息
            WeakReferenceMessenger.Default.Send(new ClipSelectedMessage(clip));
        }
    }
}
