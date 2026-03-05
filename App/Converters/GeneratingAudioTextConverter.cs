using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

public class GeneratingAudioTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isGenerating)
        {
            return isGenerating ? "生成中..." : "生成配音";
        }
        return "生成配音";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
