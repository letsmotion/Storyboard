using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Storyboard.Application.Services;

/// <summary>
/// appsettings.json 配置合并服务
/// 在应用更新时，智能合并新版本的配置字段，同时保留用户的自定义配置
/// </summary>
public class AppSettingsMergeService
{
    private readonly ILogger<AppSettingsMergeService> _logger;
    private readonly string _userSettingsPath;
    private readonly string _defaultSettingsPath;

    public AppSettingsMergeService(ILogger<AppSettingsMergeService> logger)
    {
        _logger = logger;
        _userSettingsPath = Infrastructure.Configuration.AppSettingsPaths.UserSettingsFilePath;
        _defaultSettingsPath = Infrastructure.Configuration.AppSettingsPaths.DefaultSettingsFilePath;
    }

    /// <summary>
    /// 合并配置文件
    /// 如果存在 appsettings.default.json（新版本的默认配置），则将其与用户的 appsettings.json 合并
    /// </summary>
    public async Task MergeSettingsAsync()
    {
        try
        {
            var defaultPath = File.Exists(_defaultSettingsPath)
                ? _defaultSettingsPath
                : Infrastructure.Configuration.AppSettingsPaths.BundledSettingsFilePath;

            var shouldDeleteDefault = string.Equals(defaultPath, _defaultSettingsPath, StringComparison.OrdinalIgnoreCase);

            // 如果不存在默认配置文件，说明不需要合并
            if (!File.Exists(defaultPath))
            {
                _logger.LogDebug("未找到默认配置文件，跳过合并");
                Infrastructure.Configuration.AppSettingsPaths.EnsureUserSettingsFile();
                return;
            }

            // 如果用户配置文件不存在，直接使用默认配置
            if (!File.Exists(_userSettingsPath))
            {
                _logger.LogInformation("用户配置文件不存在，使用默认配置");
                Directory.CreateDirectory(Path.GetDirectoryName(_userSettingsPath)!);
                File.Copy(defaultPath, _userSettingsPath, overwrite: true);
                if (shouldDeleteDefault)
                {
                    File.Delete(defaultPath);
                }
                return;
            }

            _logger.LogInformation("开始合并配置文件..");

            // 读取两个配置文件
            var userSettingsJson = await File.ReadAllTextAsync(_userSettingsPath);
            var defaultSettingsJson = await File.ReadAllTextAsync(defaultPath);

            var userSettings = JsonNode.Parse(userSettingsJson) as JsonObject;
            var defaultSettings = JsonNode.Parse(defaultSettingsJson) as JsonObject;

            if (userSettings == null || defaultSettings == null)
            {
                _logger.LogWarning("配置文件解析失败，跳过合并");
                return;
            }

            // 深度合并配置
            MergeJsonObjects(userSettings, defaultSettings);

            // 保存合并后的配置
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var mergedJson = JsonSerializer.Serialize(userSettings, options);
            await File.WriteAllTextAsync(_userSettingsPath, mergedJson);

            // 删除默认配置文件（已完成合并）
            if (shouldDeleteDefault)
            {
                File.Delete(defaultPath);
            }

            _logger.LogInformation("配置文件合并完成");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "合并配置文件时出错");
        }
    }

    /// <summary>
    /// 深度合并两个 JSON 对象
    /// 规则：
    /// 1. 如果用户配置中不存在某个字段，则添加默认值
    /// 2. 如果用户配置中已存在某个字段，则保留用户的值
    /// 3. 对于嵌套对象，递归合并
    /// </summary>
    private void MergeJsonObjects(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            var key = property.Key;
            var sourceValue = property.Value;

            // 如果目标对象中不存在这个键，直接添加
            if (!target.ContainsKey(key))
            {
                target[key] = sourceValue?.DeepClone();
                _logger.LogDebug("添加新配置字段: {Key}", key);
                continue;
            }

            // 如果两者都是对象，递归合并
            if (sourceValue is JsonObject sourceObj && target[key] is JsonObject targetObj)
            {
                MergeJsonObjects(targetObj, sourceObj);
            }
            // 如果两者都是数组，需要特殊处理
            else if (sourceValue is JsonArray sourceArray && target[key] is JsonArray targetArray)
            {
                // 对于数组，我们检查是否需要添加新的默认项
                // 这里采用简单策略：如果用户数组为空，使用默认数组
                if (targetArray.Count == 0 && sourceArray.Count > 0)
                {
                    target[key] = sourceArray.DeepClone();
                    _logger.LogDebug("使用默认数组值: {Key}", key);
                }
                // 否则保留用户的数组配置
            }
            // 其他情况：保留用户的值
            else
            {
                _logger.LogDebug("保留用户配置: {Key}", key);
            }
        }
    }

    /// <summary>
    /// 创建默认配置文件的备份
    /// 在发布新版本时，将新的 appsettings.json 重命名为 appsettings.default.json
    /// </summary>
    public static void PrepareDefaultSettings(string sourceSettingsPath, string targetDefaultPath)
    {
        if (File.Exists(sourceSettingsPath))
        {
            File.Copy(sourceSettingsPath, targetDefaultPath, overwrite: true);
        }
    }
}
