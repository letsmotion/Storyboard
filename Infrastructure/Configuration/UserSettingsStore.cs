using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 用户设置存储器
/// </summary>
public class UserSettingsStore
{
    private readonly string _settingsPath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() }
    };

    public UserSettingsStore()
    {
        _settingsPath = ConfigurationPaths.UserSettingsPath;
    }

    /// <summary>
    /// 加载用户设置（不存在则创建默认）
    /// </summary>
    public UserSettings Load()
    {
        ConfigurationPaths.EnsureUserDataDirectory();

        if (!File.Exists(_settingsPath))
        {
            var defaultSettings = CreateDefault();
            Save(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);

            if (settings == null)
                return CreateDefault();

            // 确保所有嵌套对象都初始化
            ApplyDefaults(settings);
            return settings;
        }
        catch
        {
            // 配置文件损坏，返回默认值
            return CreateDefault();
        }
    }

    /// <summary>
    /// 保存用户设置
    /// </summary>
    public void Save(UserSettings settings)
    {
        ConfigurationPaths.EnsureUserDataDirectory();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    /// <summary>
    /// 创建默认设置
    /// </summary>
    private UserSettings CreateDefault()
    {
        return new UserSettings
        {
            Storage = new StorageSettings(),
            UI = new UiSettings(),
            DefaultProviders = new DefaultProviderSettings()
        };
    }

    /// <summary>
    /// 确保所有嵌套对象不为 null
    /// </summary>
    private void ApplyDefaults(UserSettings settings)
    {
        settings.Storage ??= new StorageSettings();
        settings.UI ??= new UiSettings();
        settings.UI.Layout ??= new LayoutSettings();
        settings.DefaultProviders ??= new DefaultProviderSettings();
        settings.DefaultProviders.TextModel ??= string.Empty;
        settings.DefaultProviders.ImageModel ??= string.Empty;
        settings.DefaultProviders.VideoModel ??= string.Empty;
        settings.DefaultProviders.TtsModel ??= string.Empty;
    }
}
