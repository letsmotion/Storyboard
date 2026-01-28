using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using System;
using System.Globalization;
using System.IO;

namespace Storyboard.Converters;

/// <summary>
/// 将文件路径转换为 Bitmap 对象
/// </summary>
public class PathToBitmapConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrWhiteSpace(path))
        {
            try
            {
                // 检查文件是否存在
                if (File.Exists(path))
                {
                    return new Bitmap(path);
                }
            }
            catch
            {
                // 如果加载失败，返回 null
                return null;
            }
        }

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
