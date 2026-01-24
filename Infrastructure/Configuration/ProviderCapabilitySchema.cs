using System.Collections.Generic;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// Provider 能力定义 Schema（描述结构，不提供值）
/// </summary>
public class ProviderCapabilitySchema
{
    public int SchemaVersion { get; set; }
    public Dictionary<string, ProviderDefinition> Providers { get; set; } = new();
}

/// <summary>
/// Provider 定义
/// </summary>
public class ProviderDefinition
{
    /// <summary>
    /// 支持的能力类型
    /// </summary>
    public List<string> Supports { get; set; } = new();

    /// <summary>
    /// 必需的配置字段
    /// </summary>
    public List<string> RequiredFields { get; set; } = new();

    /// <summary>
    /// 可选的配置字段
    /// </summary>
    public List<string> OptionalFields { get; set; } = new();
}
