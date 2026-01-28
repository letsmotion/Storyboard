using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Media;
using Storyboard.Models.Timeline;
using System;

namespace Storyboard.Controls;

/// <summary>
/// Draws background grid lines for the timeline
/// </summary>
public class TimelineGridControl : Control
{
    public static readonly StyledProperty<double> PixelsPerSecondProperty =
        AvaloniaProperty.Register<TimelineGridControl, double>(nameof(PixelsPerSecond), 50.0);

    public static readonly StyledProperty<AvaloniaList<TimelineTrack>?> TracksProperty =
        AvaloniaProperty.Register<TimelineGridControl, AvaloniaList<TimelineTrack>?>(nameof(Tracks));

    public double PixelsPerSecond
    {
        get => GetValue(PixelsPerSecondProperty);
        set => SetValue(PixelsPerSecondProperty, value);
    }

    public AvaloniaList<TimelineTrack>? Tracks
    {
        get => GetValue(TracksProperty);
        set => SetValue(TracksProperty, value);
    }

    static TimelineGridControl()
    {
        AffectsRender<TimelineGridControl>(PixelsPerSecondProperty, TracksProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 0 || Bounds.Height <= 0)
            return;

        // Draw horizontal track separators
        DrawTrackSeparators(context);

        // Draw vertical time grid lines
        DrawTimeGridLines(context);
    }

    private void DrawTrackSeparators(DrawingContext context)
    {
        if (Tracks == null || Tracks.Count == 0)
            return;

        var pen = new Pen(new SolidColorBrush(Color.Parse("#27272a")), 1);
        var y = 0.0;

        foreach (var track in Tracks)
        {
            y += track.Height;
            context.DrawLine(pen, new Point(0, y), new Point(Bounds.Width, y));
        }
    }

    private void DrawTimeGridLines(DrawingContext context)
    {
        // Calculate interval for vertical lines (same logic as ruler major ticks)
        var targetPixels = 120.0;
        var idealInterval = targetPixels / PixelsPerSecond;

        double[] niceIntervals = { 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300, 600 };
        var interval = SnapToNiceInterval(idealInterval, niceIntervals);

        // Draw major lines (more visible)
        var majorPen = new Pen(new SolidColorBrush(Color.Parse("#27272a")), 1);

        for (var time = 0.0; time * PixelsPerSecond <= Bounds.Width; time += interval)
        {
            var x = time * PixelsPerSecond;
            context.DrawLine(majorPen, new Point(x, 0), new Point(x, Bounds.Height));
        }

        // Draw minor lines (subtle)
        var minorInterval = interval / 5;
        var minorPen = new Pen(new SolidColorBrush(Color.Parse("#1a1a1a")), 1);

        for (var time = 0.0; time * PixelsPerSecond <= Bounds.Width; time += minorInterval)
        {
            // Skip if it's a major line
            if (Math.Abs(time % interval) < 0.001)
                continue;

            var x = time * PixelsPerSecond;
            context.DrawLine(minorPen, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }

    private double SnapToNiceInterval(double value, double[] intervals)
    {
        var closest = intervals[0];
        var minDiff = Math.Abs(value - closest);

        foreach (var interval in intervals)
        {
            var diff = Math.Abs(value - interval);
            if (diff < minDiff)
            {
                minDiff = diff;
                closest = interval;
            }
        }

        return closest;
    }
}
