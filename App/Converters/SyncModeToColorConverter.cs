using Avalonia.Data.Converters;
using Avalonia.Media;
using Storyboard.Domain.Entities;
using System;
using System.Globalization;

namespace Storyboard.Converters;

public class SyncModeToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not SyncMode currentMode || parameter is not string targetModeStr)
            return new SolidColorBrush(Color.Parse("#a1a1aa")); // Default gray

        if (!Enum.TryParse<SyncMode>(targetModeStr, out var targetMode))
            return new SolidColorBrush(Color.Parse("#a1a1aa"));

        return currentMode == targetMode
            ? new SolidColorBrush(Color.Parse("#3b82f6")) // Blue when active
            : new SolidColorBrush(Color.Parse("#a1a1aa")); // Gray when inactive
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
