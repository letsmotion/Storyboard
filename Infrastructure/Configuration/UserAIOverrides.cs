using System.Collections.Generic;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// User AI overrides. Only explicitly changed values are stored.
/// </summary>
public class UserAIOverrides
{
    public Dictionary<string, ProviderUserConfig> Providers { get; set; } = new();
}

/// <summary>
/// User overrides for a single provider.
/// </summary>
public class ProviderUserConfig
{
    public string? ApiKey { get; set; }
    public bool? Enabled { get; set; }
    public string? Endpoint { get; set; }
    public int? TimeoutSeconds { get; set; }
    public string? AppId { get; set; }
    public string? Cluster { get; set; }
}
