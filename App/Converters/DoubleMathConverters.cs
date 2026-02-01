using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Storyboard.Converters;

public sealed class SumDoubleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var sum = 0.0;
        if (values == null)
            return sum;

        foreach (var value in values)
        {
            sum += ToDouble(value);
        }

        return sum;
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double ToDouble(object? value)
    {
        return value switch
        {
            null => 0.0,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            _ => 0.0
        };
    }
}

public sealed class SubtractDoubleConverter : IMultiValueConverter
{
    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values == null || values.Count < 2)
            return 0.0;

        var left = ToDouble(values[0]);
        var right = ToDouble(values[1]);
        return left - right;
    }

    public object? ConvertBack(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double ToDouble(object? value)
    {
        return value switch
        {
            null => 0.0,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            _ => 0.0
        };
    }
}
