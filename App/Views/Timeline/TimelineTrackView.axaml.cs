using Avalonia.Controls;
using Avalonia.LogicalTree;
using Storyboard.Models.Timeline;
using System.Collections.Specialized;

namespace Storyboard.Views.Timeline;

public partial class TimelineTrackView : UserControl
{
    public TimelineTrackView()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is TimelineTrack track)
        {
            // 清除现有片段
            ClipsCanvas.Children.Clear();

            // 监听 Clips 集合变化
            if (track.Clips is INotifyCollectionChanged observable)
            {
                observable.CollectionChanged -= OnClipsCollectionChanged;
                observable.CollectionChanged += OnClipsCollectionChanged;
            }

            // 渲染现有片段
            RenderClips(track);
        }
    }

    private void OnClipsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is TimelineTrack track)
        {
            RenderClips(track);
        }
    }

    private void RenderClips(TimelineTrack track)
    {
        ClipsCanvas.Children.Clear();

        foreach (var clip in track.Clips)
        {
            var clipView = new TimelineClipView
            {
                DataContext = clip,
                Width = clip.PixelWidth,
                Height = 120
            };

            Canvas.SetLeft(clipView, clip.PixelPosition);
            ClipsCanvas.Children.Add(clipView);
        }
    }
}
