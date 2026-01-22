namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 存储配置选项
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// 数据库存储目录（为空则使用默认位置：应用程序目录/Data）
    /// </summary>
    public string DataDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 输出文件存储目录（为空则使用默认位置：应用程序目录/output）
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 是否使用自定义存储位置
    /// </summary>
    public bool UseCustomLocation { get; set; } = false;
}
