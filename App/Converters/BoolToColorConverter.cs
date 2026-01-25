using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Storyboard.Converters;

/// <summary>
/// 布尔值转颜色转换器
/// ConverterParameter格式: "TrueColor|FalseColor"
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string colorPair)
            return null;

        var colors = colorPair.Split('|');
        if (colors.Length != 2)
            return null;

        var colorString = boolValue ? colors[0] : colors[1];
        return Color.Parse(colorString);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
