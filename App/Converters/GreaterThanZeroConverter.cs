using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

/// <summary>
/// 转换器:判断数值是否大于0
/// </summary>
public sealed class GreaterThanZeroConverter : IValueConverter
{
    public static readonly GreaterThanZeroConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null)
            return false;

        if (value is int intValue)
            return intValue > 0;

        if (value is long longValue)
            return longValue > 0;

        if (value is double doubleValue)
            return doubleValue > 0;

        if (value is float floatValue)
            return floatValue > 0;

        if (value is decimal decimalValue)
            return decimalValue > 0;

        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
