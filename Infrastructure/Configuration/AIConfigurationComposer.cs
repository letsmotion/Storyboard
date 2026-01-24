using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// AI 配置合成器
/// 核心公式: Effective = Validate(Schema, Defaults + UserOverrides)
/// </summary>
public class AIConfigurationComposer
{
    private readonly ILogger<AIConfigurationComposer> _logger;
    private readonly UserAIOverridesStore _overridesStore;
    private readonly UserSettingsStore _userSettingsStore;

    private AIDefaults? _defaults;
    private ProviderCapabilitySchema? _schema;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AIConfigurationComposer(
        ILogger<AIConfigurationComposer> logger,
        UserAIOverridesStore overridesStore,
        UserSettingsStore userSettingsStore)
    {
        _logger = logger;
        _overridesStore = overridesStore;
        _userSettingsStore = userSettingsStore;
    }

    /// <summary>
    /// 加载 AI 配置（合成最终有效配置）
    /// </summary>
    public AIServicesConfiguration LoadConfiguration()
    {
        // 1. 加载 Schema（结构定义）
        LoadSchema();

        // 2. 加载 Defaults（默认值）
        LoadDefaults();

        // 3. 加载 User Overrides（用户覆盖）
        var userOverrides = _overridesStore.Load();

        // 4. 合成最终配置
        return ComposeConfiguration(userOverrides);
    }

    /// <summary>
    /// 保存用户修改的配置(只保存差异)
    /// </summary>
    public void SaveUserConfiguration(string providerName, ProviderUserConfig userConfig)
    {
        // 确保 defaults 已加载
        if (_defaults == null)
        {
            LoadDefaults();
        }

        var overrides = _overridesStore.Load();

        // 获取默认配置
        ProviderDefaultConfig? defaultConfig = null;
        var hasDefaults = _defaults?.Providers.TryGetValue(providerName, out defaultConfig) == true;

        // 创建差异配置（只保存与默认值不同的部分）
        var diffConfig = new ProviderUserConfig();

        // ApiKey 始终保存（敏感信息，不在 defaults 中）
        if (!string.IsNullOrWhiteSpace(userConfig.ApiKey))
        {
            diffConfig.ApiKey = userConfig.ApiKey;
        }

        // 只保存与默认值不同的 Enabled
        if (userConfig.Enabled.HasValue &&
            (!hasDefaults || userConfig.Enabled.Value != defaultConfig!.Enabled))
        {
            diffConfig.Enabled = userConfig.Enabled;
        }

        // 只保存与默认值不同的 Endpoint
        if (!string.IsNullOrWhiteSpace(userConfig.Endpoint) &&
            (!hasDefaults || userConfig.Endpoint != defaultConfig!.Endpoint))
        {
            diffConfig.Endpoint = userConfig.Endpoint;
        }

        // 只保存与默认值不同的 TimeoutSeconds
        if (userConfig.TimeoutSeconds.HasValue &&
            (!hasDefaults || userConfig.TimeoutSeconds.Value != defaultConfig!.TimeoutSeconds))
        {
            diffConfig.TimeoutSeconds = userConfig.TimeoutSeconds;
        }

        // 只保存与默认值不同的 DefaultModels
        if (userConfig.DefaultModels != null && userConfig.DefaultModels.Count > 0)
        {
            diffConfig.DefaultModels = new Dictionary<string, string>();

            foreach (var (modelType, modelValue) in userConfig.DefaultModels)
            {
                // 检查是否与默认值不同
                var isDifferent = !hasDefaults ||
                                  !defaultConfig!.DefaultModels.TryGetValue(modelType, out var defaultModel) ||
                                  modelValue != defaultModel;

                if (isDifferent && !string.IsNullOrWhiteSpace(modelValue))
                {
                    diffConfig.DefaultModels[modelType] = modelValue;
                }
            }

            // 如果没有差异，不保存 DefaultModels
            if (diffConfig.DefaultModels.Count == 0)
            {
                diffConfig.DefaultModels = null;
            }
        }

        // 如果所有字段都为空（除了 ApiKey），则完全移除该 Provider 配置
        if (string.IsNullOrWhiteSpace(diffConfig.ApiKey) &&
            !diffConfig.Enabled.HasValue &&
            string.IsNullOrWhiteSpace(diffConfig.Endpoint) &&
            !diffConfig.TimeoutSeconds.HasValue &&
            diffConfig.DefaultModels == null)
        {
            overrides.Providers.Remove(providerName);
            _logger.LogInformation("用户配置已移除 (无差异): {Provider}", providerName);
        }
        else
        {
            overrides.Providers[providerName] = diffConfig;
            _logger.LogInformation("用户配置已保存 (仅差异): {Provider}", providerName);
        }

        _overridesStore.Save(overrides);
    }

    /// <summary>
    /// 加载 Schema
    /// </summary>
    private void LoadSchema()
    {
        var schemaPath = ConfigurationPaths.ProviderCapabilitySchemaPath;

        if (!File.Exists(schemaPath))
        {
            _logger.LogWarning("Provider Capability Schema 文件不存在: {Path}", schemaPath);
            _schema = new ProviderCapabilitySchema { SchemaVersion = 1 };
            return;
        }

        try
        {
            var json = File.ReadAllText(schemaPath);
            _schema = JsonSerializer.Deserialize<ProviderCapabilitySchema>(json, JsonOptions)
                      ?? new ProviderCapabilitySchema();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 Provider Capability Schema 失败");
            _schema = new ProviderCapabilitySchema();
        }
    }

    /// <summary>
    /// 加载 Defaults
    /// </summary>
    private void LoadDefaults()
    {
        var defaultsPath = ConfigurationPaths.AIDefaultsPath;

        if (!File.Exists(defaultsPath))
        {
            _logger.LogWarning("AI Defaults 文件不存在: {Path}", defaultsPath);
            _defaults = new AIDefaults();
            return;
        }

        try
        {
            var json = File.ReadAllText(defaultsPath);
            _defaults = JsonSerializer.Deserialize<AIDefaults>(json, JsonOptions)
                        ?? new AIDefaults();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载 AI Defaults 失败");
            _defaults = new AIDefaults();
        }
    }

    /// <summary>
    /// 合成最终配置
    /// </summary>
    private AIServicesConfiguration ComposeConfiguration(UserAIOverrides userOverrides)
    {
        var config = new AIServicesConfiguration();

        if (_defaults == null)
        {
            _logger.LogWarning("Defaults 未加载");
            return config;
        }

        // 验证用户覆盖 (基于 Schema)
        if (_schema != null)
        {
            ValidateUserOverrides(userOverrides);
        }

        // 1. 合成每个 Provider 的配置
        var qwenConfig = ComposeProviderConfig("Qwen", userOverrides);
        var volcengineConfig = ComposeProviderConfig("Volcengine", userOverrides);

        config.Providers = new AIProvidersConfiguration
        {
            Qwen = qwenConfig,
            Volcengine = volcengineConfig
        };

        // 2. 设置 Defaults（Text/Image/Video 默认 Provider）
        var userSettings = _userSettingsStore.Load();
        var textProviderName = ResolveProviderName(userSettings.DefaultProviders.TextProvider, "Qwen");
        var imageProviderName = ResolveProviderName(userSettings.DefaultProviders.ImageProvider, "Volcengine");
        var videoProviderName = ResolveProviderName(userSettings.DefaultProviders.VideoProvider, "Volcengine");

        config.Defaults = new AIServiceDefaults
        {
            Text = new AIServiceDefaultSelection
            {
                Provider = ParseProviderType(textProviderName),
                Model = GetProviderDefaultModel(textProviderName, "Text", userOverrides) ?? ""
            },
            Image = new AIServiceDefaultSelection
            {
                Provider = ParseProviderType(imageProviderName),
                Model = GetProviderDefaultModel(imageProviderName, "Image", userOverrides) ?? ""
            },
            Video = new AIServiceDefaultSelection
            {
                Provider = ParseProviderType(videoProviderName),
                Model = GetProviderDefaultModel(videoProviderName, "Video", userOverrides) ?? ""
            }
        };

        // 3. 合并 Image/Video 默认参数（见后续修复）
        config.Image = ComposeImageConfiguration();
        config.Video = ComposeVideoConfiguration();

        return config;
    }

    /// <summary>
    /// 验证用户覆盖配置（基于 Schema）
    /// </summary>
    private void ValidateUserOverrides(UserAIOverrides userOverrides)
    {
        if (_schema == null)
            return;

        // 移除未知的 Provider
        var unknownProviders = userOverrides.Providers.Keys
            .Where(p => !_schema.Providers.ContainsKey(p))
            .ToList();

        foreach (var unknownProvider in unknownProviders)
        {
            _logger.LogWarning("移除未知的 Provider 配置: {Provider}", unknownProvider);
            userOverrides.Providers.Remove(unknownProvider);
        }

        // 验证每个 Provider 的必填字段
        foreach (var (providerName, userConfig) in userOverrides.Providers)
        {
            if (!_schema.Providers.TryGetValue(providerName, out var providerDef))
                continue;

            // 检查 ApiKey 是否存在（这是最关键的必填字段）
            if (providerDef.RequiredFields.Contains("ApiKey") &&
                string.IsNullOrWhiteSpace(userConfig.ApiKey))
            {
                _logger.LogWarning("{Provider} 缺少必填字段: ApiKey", providerName);
            }
        }
    }

    /// <summary>
    /// 合成单个 Provider 的配置
    /// </summary>
    private AIProviderConfiguration ComposeProviderConfig(string providerName, UserAIOverrides userOverrides)
    {
        var providerConfig = new AIProviderConfiguration();

        // 从 defaults 获取基础配置
        if (_defaults?.Providers.TryGetValue(providerName, out var defaultConfig) == true)
        {
            providerConfig.Endpoint = defaultConfig.Endpoint;
            providerConfig.TimeoutSeconds = defaultConfig.TimeoutSeconds;
            providerConfig.Enabled = defaultConfig.Enabled;
            providerConfig.DefaultModels = new AIProviderModelDefaults
            {
                Text = defaultConfig.DefaultModels.TryGetValue("Text", out var text) ? text : "",
                Image = defaultConfig.DefaultModels.TryGetValue("Image", out var image) ? image : "",
                Video = defaultConfig.DefaultModels.TryGetValue("Video", out var video) ? video : ""
            };
        }

        // 应用用户覆盖
        if (userOverrides.Providers.TryGetValue(providerName, out var userConfig))
        {
            if (userConfig.ApiKey != null)
                providerConfig.ApiKey = userConfig.ApiKey;

            if (userConfig.Enabled.HasValue)
                providerConfig.Enabled = userConfig.Enabled.Value;

            if (userConfig.Endpoint != null)
                providerConfig.Endpoint = userConfig.Endpoint;

            if (userConfig.TimeoutSeconds.HasValue)
                providerConfig.TimeoutSeconds = userConfig.TimeoutSeconds.Value;

            if (userConfig.DefaultModels != null)
            {
                if (userConfig.DefaultModels.TryGetValue("Text", out var text))
                    providerConfig.DefaultModels.Text = text;
                if (userConfig.DefaultModels.TryGetValue("Image", out var image))
                    providerConfig.DefaultModels.Image = image;
                if (userConfig.DefaultModels.TryGetValue("Video", out var video))
                    providerConfig.DefaultModels.Video = video;
            }
        }

        return providerConfig;
    }

    /// <summary>
    /// 合成 Image 配置
    /// </summary>
    private ImageServicesConfiguration ComposeImageConfiguration()
    {
        var config = new ImageServicesConfiguration();

        if (_defaults?.Image?.Providers.TryGetValue("Volcengine", out var volcDefaults) == true)
        {
            config.Volcengine = new VolcengineImageConfig
            {
                Size = volcDefaults.Size,
                ResponseFormat = volcDefaults.ResponseFormat,
                Watermark = volcDefaults.Watermark,
                Stream = volcDefaults.Stream
            };
        }

        return config;
    }

    /// <summary>
    /// 合成 Video 配置
    /// </summary>
    private VideoServicesConfiguration ComposeVideoConfiguration()
    {
        var config = new VideoServicesConfiguration();

        if (_defaults?.Video?.Providers.TryGetValue("Volcengine", out var volcDefaults) == true)
        {
            config.Volcengine = new VolcengineVideoConfig
            {
                Resolution = volcDefaults.Resolution,
                Ratio = volcDefaults.Ratio,
                DurationSeconds = volcDefaults.DurationSeconds,
                Watermark = volcDefaults.Watermark,
                ReturnLastFrame = volcDefaults.ReturnLastFrame,
                ServiceTier = volcDefaults.ServiceTier,
                GenerateAudio = volcDefaults.GenerateAudio,
                Draft = volcDefaults.Draft
            };
        }

        return config;
    }

    /// <summary>
    /// 获取 Provider 的默认模型（考虑用户覆盖）
    /// </summary>
    private string? GetProviderDefaultModel(string providerName, string modelType, UserAIOverrides userOverrides)
    {
        // 先查找用户覆盖
        if (userOverrides.Providers.TryGetValue(providerName, out var userConfig) &&
            userConfig.DefaultModels?.TryGetValue(modelType, out var userModel) == true)
        {
            return userModel;
        }

        // 回退到系统默认值
        if (_defaults?.Providers.TryGetValue(providerName, out var defaultConfig) == true &&
            defaultConfig.DefaultModels.TryGetValue(modelType, out var defaultModel))
        {
            return defaultModel;
        }

        return null;
    }

    /// <summary>
    /// 解析 Provider 类型字符串
    /// </summary>
    private string ResolveProviderName(string? providerName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(providerName))
        {
            return fallback;
        }

        if (_schema?.Providers.ContainsKey(providerName) == true)
        {
            return providerName;
        }

        if (_defaults?.Providers.ContainsKey(providerName) == true)
        {
            return providerName;
        }

        return fallback;
    }

    /// <summary>
    /// Parse provider type string.
    /// </summary>
    private AIProviderType ParseProviderType(string providerName)
    {
        return providerName switch
        {
            "Qwen" => AIProviderType.Qwen,
            "Volcengine" => AIProviderType.Volcengine,
            _ => AIProviderType.Qwen // 默认值
        };
    }
}
