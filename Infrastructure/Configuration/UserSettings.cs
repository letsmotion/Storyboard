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
    public LayoutSettings Layout { get; set; } = new();
}

/// <summary>
/// 布局设置
/// </summary>
public class LayoutSettings
{
    public bool IsLeftSidebarVisible { get; set; } = true;
    public double LeftSidebarWidth { get; set; } = 320;
    public bool IsRightPanelVisible { get; set; } = true;
    public double RightPanelWidth { get; set; } = 384;

    // 约束常量
    public const double LeftSidebarMinWidth = 200;
    public const double LeftSidebarMaxWidth = 500;
    public const double RightPanelMinWidth = 300;
    public const double RightPanelMaxWidth = 600;
}

/// <summary>
/// 默认 Provider 选择
/// </summary>
public class DefaultProviderSettings
{
    public string TextProvider { get; set; } = "NewApi";
    public string ImageProvider { get; set; } = "NewApi";
    public string VideoProvider { get; set; } = "NewApi";
    public string TextModel { get; set; } = string.Empty;
    public string ImageModel { get; set; } = string.Empty;
    public string VideoModel { get; set; } = string.Empty;
}
