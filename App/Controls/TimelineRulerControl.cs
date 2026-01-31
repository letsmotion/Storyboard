using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Windows.Input;

namespace Storyboard.Controls;

/// <summary>
/// Custom timeline ruler control that draws time markers with adaptive density
/// </summary>
public class TimelineRulerControl : Control
{
    public static readonly StyledProperty<double> PixelsPerSecondProperty =
        AvaloniaProperty.Register<TimelineRulerControl, double>(nameof(PixelsPerSecond), 50.0);

    public static readonly StyledProperty<double> TotalDurationProperty =
        AvaloniaProperty.Register<TimelineRulerControl, double>(nameof(TotalDuration), 0.0);

    public static readonly StyledProperty<double> ViewportOffsetXProperty =
        AvaloniaProperty.Register<TimelineRulerControl, double>(nameof(ViewportOffsetX), 0.0);

    public static readonly StyledProperty<double> PlayheadPositionProperty =
        AvaloniaProperty.Register<TimelineRulerControl, double>(nameof(PlayheadPosition), 0.0);

    public static readonly StyledProperty<ICommand?> SeekCommandProperty =
        AvaloniaProperty.Register<TimelineRulerControl, ICommand?>(nameof(SeekCommand));

    public double PixelsPerSecond
    {
        get => GetValue(PixelsPerSecondProperty);
        set => SetValue(PixelsPerSecondProperty, value);
    }

    public double TotalDuration
    {
        get => GetValue(TotalDurationProperty);
        set => SetValue(TotalDurationProperty, value);
    }

    public double ViewportOffsetX
    {
        get => GetValue(ViewportOffsetXProperty);
        set => SetValue(ViewportOffsetXProperty, value);
    }

    public double PlayheadPosition
    {
        get => GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    public ICommand? SeekCommand
    {
        get => GetValue(SeekCommandProperty);
        set => SetValue(SeekCommandProperty, value);
    }

    private bool _isDragging;

    static TimelineRulerControl()
    {
        AffectsRender<TimelineRulerControl>(
            PixelsPerSecondProperty,
            TotalDurationProperty,
            ViewportOffsetXProperty,
            PlayheadPositionProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0 || PixelsPerSecond <= 0)
            return;

        // Background - fill the entire control width
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0f0f0f")), new Rect(0, 0, w, h));

        // Calculate visible time range based on viewport offset
        var visibleStartTime = ViewportOffsetX / PixelsPerSecond;
        var visibleEndTime = (ViewportOffsetX + w) / PixelsPerSecond;

        // Determine tick interval based on zoom level
        var (majorInterval, minorInterval) = CalculateTickIntervals(PixelsPerSecond);

        // Draw minor ticks
        DrawTicks(context, visibleStartTime, visibleEndTime, minorInterval, false, w, h);

        // Draw major ticks with labels
        DrawTicks(context, visibleStartTime, visibleEndTime, majorInterval, true, w, h);

        // Draw playhead indicator
        DrawPlayhead(context, w, h);
    }

    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        e.Pointer.Capture(this);
        SeekToPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isDragging || e.Pointer.Captured != this)
        {
            return;
        }

        SeekToPointer(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnPointerReleased(Avalonia.Input.PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }
        e.Handled = true;
    }

    private void SeekToPointer(double x)
    {
        if (PixelsPerSecond <= 0)
        {
            return;
        }

        var time = (x + ViewportOffsetX) / PixelsPerSecond;
        if (SeekCommand?.CanExecute(time) == true)
        {
            SeekCommand.Execute(time);
        }
    }

    private (double major, double minor) CalculateTickIntervals(double pps)
    {
        // Adaptive intervals based on pixels per second
        // Goal: major ticks every ~100-150 pixels, minor ticks every ~20-30 pixels

        var targetMajorPixels = 120.0;
        var targetMinorPixels = 24.0;

        // Calculate ideal intervals in seconds
        var idealMajorInterval = targetMajorPixels / pps;
        var idealMinorInterval = targetMinorPixels / pps;

        // Snap to nice numbers: 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300, 600
        double[] niceIntervals = { 0.1, 0.2, 0.5, 1, 2, 5, 10, 30, 60, 120, 300, 600 };

        var majorInterval = SnapToNiceInterval(idealMajorInterval, niceIntervals);
        var minorInterval = SnapToNiceInterval(idealMinorInterval, niceIntervals);

        // Ensure minor is smaller than major
        if (minorInterval >= majorInterval)
        {
            var majorIndex = Array.IndexOf(niceIntervals, majorInterval);
            if (majorIndex > 0)
                minorInterval = niceIntervals[majorIndex - 1];
            else
                minorInterval = majorInterval / 5;
        }

        return (majorInterval, minorInterval);
    }

    private double SnapToNiceInterval(double value, double[] intervals)
    {
        // Find closest nice interval
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

    private void DrawTicks(DrawingContext context, double startTime, double endTime, double interval, bool isMajor, double w, double h)
    {
        if (interval <= 0) return;

        // Start from first tick before visible area
        var firstTick = Math.Floor(startTime / interval) * interval;

        var tickHeight = isMajor ? 12.0 : 6.0;
        var tickColor = isMajor ? Color.Parse("#71717a") : Color.Parse("#3f3f46");
        var pen = new Pen(new SolidColorBrush(tickColor), 1);

        var typeface = new Typeface("Segoe UI");
        var textBrush = new SolidColorBrush(Color.Parse("#a1a1aa"));

        for (var time = firstTick; time <= endTime + interval; time += interval)
        {
            if (time < 0) continue;

            // Calculate position and adjust for viewport offset
            var x = (time * PixelsPerSecond) - ViewportOffsetX;

            if (x < -10 || x > w + 10)
                continue;

            // Draw tick line
            context.DrawLine(pen, new Point(x, h - tickHeight), new Point(x, h));

            // Draw label for major ticks
            if (isMajor)
            {
                var label = FormatTime(time);
                var formattedText = new FormattedText(
                    label,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    11,
                    textBrush);

                var textX = x - formattedText.Width / 2;
                // Clamp to prevent labels from being cut off at edges
                textX = Math.Clamp(textX, 0, w - formattedText.Width);
                var textY = 4;

                context.DrawText(formattedText, new Point(textX, textY));
            }
        }
    }

    private void DrawPlayhead(DrawingContext context, double w, double h)
    {
        // Calculate position and adjust for viewport offset
        var x = PlayheadPosition - ViewportOffsetX;

        if (x < 0 || x > w)
            return;

        // Draw playhead line
        var pen = new Pen(new SolidColorBrush(Color.Parse("#8b5cf6")), 2);
        context.DrawLine(pen, new Point(x, 0), new Point(x, h));

        // Draw playhead circle at top
        var circleBrush = new SolidColorBrush(Color.Parse("#8b5cf6"));
        context.DrawEllipse(circleBrush, null, new Point(x, 7), 7, 7);
    }

    private string FormatTime(double seconds)
    {
        if (seconds < 60)
        {
            return $"{seconds:F1}s";
        }
        else if (seconds < 3600)
        {
            var minutes = (int)(seconds / 60);
            var secs = seconds % 60;
            return $"{minutes}:{secs:00.0}";
        }
        else
        {
            var hours = (int)(seconds / 3600);
            var minutes = (int)((seconds % 3600) / 60);
            var secs = seconds % 60;
            return $"{hours}:{minutes:00}:{secs:00}";
        }
    }
}
