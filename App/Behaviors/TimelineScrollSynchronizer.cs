using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;

namespace Storyboard.Behaviors;

/// <summary>
/// Synchronizes scroll positions between timeline components
/// </summary>
public class TimelineScrollSynchronizer
{
    private ScrollViewer? _mainScrollViewer;
    private ScrollViewer? _trackHeadersScrollViewer;
    private Control? _rulerControl;

    private bool _isSyncing;

    public void Initialize(
        ScrollViewer mainScrollViewer,
        ScrollViewer trackHeadersScrollViewer,
        Control rulerControl)
    {
        _mainScrollViewer = mainScrollViewer;
        _trackHeadersScrollViewer = trackHeadersScrollViewer;
        _rulerControl = rulerControl;

        // Subscribe to main scroll viewer changes
        _mainScrollViewer.PropertyChanged += OnMainScrollChanged;
    }

    private void OnMainScrollChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_isSyncing) return;

        if (e.Property == ScrollViewer.OffsetProperty && sender is ScrollViewer scrollViewer)
        {
            _isSyncing = true;

            try
            {
                var offset = scrollViewer.Offset;

                // Sync vertical scroll to track headers
                if (_trackHeadersScrollViewer != null)
                {
                    _trackHeadersScrollViewer.Offset = new Vector(0, offset.Y);
                }

                // Sync horizontal scroll to ruler (via property)
                if (_rulerControl != null && _rulerControl is Controls.TimelineRulerControl ruler)
                {
                    ruler.ViewportOffsetX = offset.X;
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }

    public void Dispose()
    {
        if (_mainScrollViewer != null)
        {
            _mainScrollViewer.PropertyChanged -= OnMainScrollChanged;
        }
    }
}
