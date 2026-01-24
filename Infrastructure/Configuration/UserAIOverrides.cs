using System.Collections.Generic;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 用户 AI 配置覆盖（只保存用户显式修改的值）
/// </summary>
public class UserAIOverrides
{
    /// <summary>
    /// 用户修改的 Provider 配置
    /// </summary>
    public Dictionary<string, ProviderUserConfig> Providers { get; set; } = new();
}

/// <summary>
/// 用户修改的 Provider 配置（只保存差异）
/// </summary>
public class ProviderUserConfig
{
    /// <summary>
    /// API Key（敏感信息）
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// 用户是否启用此 Provider
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>
    /// 用户自定义的 Endpoint
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// 用户自定义的超时时间
    /// </summary>
    public int? TimeoutSeconds { get; set; }

    /// <summary>
    /// 用户自定义的模型选择
    /// </summary>
    public Dictionary<string, string>? DefaultModels { get; set; }
}
