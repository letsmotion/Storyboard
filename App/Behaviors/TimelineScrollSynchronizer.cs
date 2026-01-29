using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;

namespace Storyboard.Behaviors;

/// <summary>
/// Synchronizes vertical scroll between timeline content and track headers
/// (Horizontal scroll is handled via XAML binding on ruler's ViewportOffsetX)
/// </summary>
public class TimelineScrollSynchronizer
{
    private ScrollViewer? _mainScrollViewer;
    private ScrollViewer? _trackHeadersScrollViewer;

    private bool _isSyncing;

    public void Initialize(
        ScrollViewer mainScrollViewer,
        ScrollViewer trackHeadersScrollViewer,
        Control rulerControl)
    {
        _mainScrollViewer = mainScrollViewer;
        _trackHeadersScrollViewer = trackHeadersScrollViewer;
        // rulerControl parameter kept for compatibility but not used (ruler syncs via XAML binding)

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

                // Sync vertical scroll to track headers only
                // (Horizontal scroll is handled via XAML binding: ViewportOffsetX="{Binding #TimelineScrollViewer.Offset.X}")
                if (_trackHeadersScrollViewer != null)
                {
                    _trackHeadersScrollViewer.Offset = new Vector(0, offset.Y);
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
