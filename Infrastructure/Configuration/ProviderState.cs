namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// Provider 状态（用户数据，包含版本号）
/// </summary>
public class ProviderState
{
    /// <summary>
    /// 当前 Schema 版本号 (默认为 0 表示需要迁移)
    /// </summary>
    public int SchemaVersion { get; set; } = 0;

    /// <summary>
    /// 最后迁移时间
    /// </summary>
    public string LastMigrationTime { get; set; } = string.Empty;
}
