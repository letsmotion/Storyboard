using CommunityToolkit.Mvvm.ComponentModel;

namespace Storyboard.Models;

/// <summary>
/// 图片导入项 - 表示待导入的单张图片
/// </summary>
public partial class ImageImportItem : ObservableObject
{
    /// <summary>
    /// 源文件路径
    /// </summary>
    [ObservableProperty]
    private string _filePath = string.Empty;

    /// <summary>
    /// 文件名
    /// </summary>
    [ObservableProperty]
    private string _fileName = string.Empty;

    /// <summary>
    /// 图片尺寸 (如 "1920x1080")
    /// </summary>
    [ObservableProperty]
    private string _imageSize = string.Empty;

    /// <summary>
    /// 文件大小 (如 "2.5 MB")
    /// </summary>
    [ObservableProperty]
    private string _fileSize = string.Empty;

    /// <summary>
    /// 是否有效
    /// </summary>
    [ObservableProperty]
    private bool _isValid = true;

    /// <summary>
    /// 验证错误信息
    /// </summary>
    [ObservableProperty]
    private string? _validationError;

    /// <summary>
    /// 将要创建的分镜编号
    /// </summary>
    [ObservableProperty]
    private int _shotNumber;
}
