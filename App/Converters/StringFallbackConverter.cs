using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace Storyboard.Converters;

public sealed class StringFallbackConverter : IValueConverter
{
    public static readonly StringFallbackConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
            return parameter?.ToString() ?? string.Empty;
        return text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
