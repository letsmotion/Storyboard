using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 用户 AI 配置覆盖存储器
/// </summary>
public class UserAIOverridesStore
{
    private readonly string _overridesPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull // 不保存 null 值
    };

    public UserAIOverridesStore()
    {
        _overridesPath = ConfigurationPaths.UserAIOverridesPath;
    }

    /// <summary>
    /// 加载用户 AI 配置覆盖
    /// </summary>
    public UserAIOverrides Load()
    {
        ConfigurationPaths.EnsureUserDataDirectory();

        if (!File.Exists(_overridesPath))
        {
            return new UserAIOverrides();
        }

        try
        {
            var json = File.ReadAllText(_overridesPath);
            return JsonSerializer.Deserialize<UserAIOverrides>(json, JsonOptions)
                   ?? new UserAIOverrides();
        }
        catch
        {
            return new UserAIOverrides();
        }
    }

    /// <summary>
    /// 保存用户 AI 配置覆盖
    /// </summary>
    public void Save(UserAIOverrides overrides)
    {
        ConfigurationPaths.EnsureUserDataDirectory();
        var json = JsonSerializer.Serialize(overrides, JsonOptions);
        File.WriteAllText(_overridesPath, json);
    }
}
