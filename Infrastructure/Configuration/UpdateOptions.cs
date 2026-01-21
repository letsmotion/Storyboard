namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 自动更新配置选项
/// </summary>
public class UpdateOptions
{
    /// <summary>
    /// 是否启用自动更新
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 是否在启动时检查更新
    /// </summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>
    /// 启动后延迟检查更新的秒数
    /// </summary>
    public int CheckDelaySeconds { get; set; } = 3;

    /// <summary>
    /// 更新源列表
    /// </summary>
    public List<UpdateSource> Sources { get; set; } = new();
}

/// <summary>
/// 更新源配置
/// </summary>
public class UpdateSource
{
    /// <summary>
    /// 源名称（如 Gitee、GitHub）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 仓库 URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 优先级（数字越小优先级越高）
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// 是否启用此源
    /// </summary>
    public bool Enabled { get; set; } = true;
}
