namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 用户设置（用户偏好配置）
/// 只保存用户真正修改过的值
/// </summary>
public class UserSettings
{
    public StorageSettings Storage { get; set; } = new();
    public UiSettings UI { get; set; } = new();
    public DefaultProviderSettings DefaultProviders { get; set; } = new();
}

/// <summary>
/// 存储位置设置
/// </summary>
public class StorageSettings
{
    [System.Text.Json.Serialization.JsonPropertyName("_Configured")]
    public bool Configured { get; set; } = false;

    public string DataDirectory { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public bool UseCustomLocation { get; set; } = false;
}

/// <summary>
/// UI 设置
/// </summary>
public class UiSettings
{
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "zh-CN";
}

/// <summary>
/// 默认 Provider 选择
/// </summary>
public class DefaultProviderSettings
{
    public string TextProvider { get; set; } = "Volcengine";
    public string ImageProvider { get; set; } = "Volcengine";
    public string VideoProvider { get; set; } = "Volcengine";
}
