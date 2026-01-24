using System;
using System.IO;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// 新配置系统的路径管理（V2 架构）
/// </summary>
public static class ConfigurationPaths
{
    private const string AppFolderName = "Storyboard";

    // ========== 应用程序目录（只读配置） ==========

    /// <summary>
    /// 应用配置文件路径（只读，日志/更新等非用户配置）
    /// </summary>
    public static string AppConfigPath => Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    /// <summary>
    /// AI Providers 能力定义（Schema，描述结构）
    /// </summary>
    public static string ProviderCapabilitySchemaPath =>
        Path.Combine(AppContext.BaseDirectory, "ai.providers.schema.json");

    /// <summary>
    /// AI 默认参数配置（开发者推荐值）
    /// </summary>
    public static string AIDefaultsPath =>
        Path.Combine(AppContext.BaseDirectory, "ai.defaults.json");

    // ========== 用户数据目录（可写） ==========

    /// <summary>
    /// 用户数据根目录
    /// </summary>
    public static string UserDataDirectory
    {
        get
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appDataPath, AppFolderName);
        }
    }

    /// <summary>
    /// 用户设置文件（用户偏好配置）
    /// </summary>
    public static string UserSettingsPath =>
        Path.Combine(UserDataDirectory, "user.settings.json");

    /// <summary>
    /// 用户 AI 配置覆盖（只保存用户修改的值）
    /// </summary>
    public static string UserAIOverridesPath =>
        Path.Combine(UserDataDirectory, "user.ai.settings.json");

    /// <summary>
    /// Provider 状态文件（版本号 + 迁移状态）
    /// </summary>
    public static string ProviderStatePath =>
        Path.Combine(UserDataDirectory, "providers.state.json");

    /// <summary>
    /// 确保用户数据目录存在
    /// </summary>
    public static void EnsureUserDataDirectory()
    {
        Directory.CreateDirectory(UserDataDirectory);
    }
}
