using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// Provider 状态存储器
/// </summary>
public class ProviderStateStore
{
    private readonly string _statePath;
    private readonly ILogger<ProviderStateStore>? _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public ProviderStateStore(ILogger<ProviderStateStore>? logger = null)
    {
        _statePath = ConfigurationPaths.ProviderStatePath;
        _logger = logger;
    }

    /// <summary>
    /// 加载 Provider 状态
    /// </summary>
    public ProviderState Load()
    {
        ConfigurationPaths.EnsureUserDataDirectory();

        if (!File.Exists(_statePath))
        {
            _logger?.LogInformation("Provider 状态文件不存在,返回初始状态 (SchemaVersion=0,需要迁移)");
            return new ProviderState
            {
                SchemaVersion = 0,  // 首次运行,需要执行迁移
                LastMigrationTime = string.Empty
            };
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var state = JsonSerializer.Deserialize<ProviderState>(json, JsonOptions);
            return state ?? new ProviderState();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "加载 Provider 状态失败");
            return new ProviderState();
        }
    }

    /// <summary>
    /// 保存 Provider 状态
    /// </summary>
    public void Save(ProviderState state)
    {
        try
        {
            ConfigurationPaths.EnsureUserDataDirectory();
            state.LastMigrationTime = DateTime.UtcNow.ToString("O");
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(_statePath, json);
            _logger?.LogInformation("Provider 状态已保存,版本: {Version}", state.SchemaVersion);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "保存 Provider 状态失败");
        }
    }
}
