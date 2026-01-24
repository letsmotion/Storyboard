using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Storyboard.AI;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Configuration;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels;

public partial class ApiKeyViewModel : ObservableObject
{
    private readonly AIConfigurationComposer _configComposer;
    private readonly UserSettingsStore _userSettingsStore;
    private readonly AIServiceManager _aiManager;

    public ApiKeyViewModel(
        AIConfigurationComposer configComposer,
        UserSettingsStore userSettingsStore,
        AIServiceManager aiManager)
    {
        _configComposer = configComposer;
        _userSettingsStore = userSettingsStore;
        _aiManager = aiManager;

        AvailableProviderTypes = Enum.GetValues(typeof(AIProviderType)).Cast<AIProviderType>().ToList();

        ValidationResults.CollectionChanged += (_, __) =>
        {
            OnPropertyChanged(nameof(HasValidationResults));
            OnPropertyChanged(nameof(HasNoValidationResults));
        };

        LoadFromFile();
    }

    public IReadOnlyList<AIProviderType> AvailableProviderTypes { get; }

    public ObservableCollection<ProviderValidationResult> ValidationResults { get; } = new();
    public bool HasValidationResults => ValidationResults.Count > 0;
    public bool HasNoValidationResults => ValidationResults.Count == 0;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultTextProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private string _defaultTextModel = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultImageProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private string _defaultImageModel = string.Empty;

    [ObservableProperty]
    private AIProviderType _defaultVideoProvider = AIProviderType.Qwen;

    [ObservableProperty]
    private string _defaultVideoModel = string.Empty;

    [ObservableProperty]
    private AIProviderType _selectedProvider = AIProviderType.Qwen;

    [ObservableProperty] private bool _qwenEnabled;
    [ObservableProperty] private string _qwenApiKey = string.Empty;
    [ObservableProperty] private string _qwenEndpoint = string.Empty;
    [ObservableProperty] private int _qwenTimeoutSeconds = 120;
    [ObservableProperty] private string _qwenDefaultTextModel = string.Empty;
    [ObservableProperty] private string _qwenDefaultImageModel = string.Empty;
    [ObservableProperty] private string _qwenDefaultVideoModel = string.Empty;

    [ObservableProperty] private bool _volcengineEnabled;
    [ObservableProperty] private string _volcengineApiKey = string.Empty;
    [ObservableProperty] private string _volcengineEndpoint = string.Empty;
    [ObservableProperty] private int _volcengineTimeoutSeconds = 120;
    [ObservableProperty] private string _volcengineDefaultTextModel = string.Empty;
    [ObservableProperty] private string _volcengineDefaultImageModel = string.Empty;
    [ObservableProperty] private string _volcengineDefaultVideoModel = string.Empty;

    public bool IsQwenSelected => SelectedProvider == AIProviderType.Qwen;
    public bool IsVolcengineSelected => SelectedProvider == AIProviderType.Volcengine;

    private void LoadFromFile()
    {
        var cfg = _configComposer.LoadConfiguration();

        DefaultTextProvider = cfg.Defaults.Text.Provider;
        DefaultTextModel = cfg.Defaults.Text.Model;
        DefaultImageProvider = cfg.Defaults.Image.Provider;
        DefaultImageModel = cfg.Defaults.Image.Model;
        DefaultVideoProvider = cfg.Defaults.Video.Provider;
        DefaultVideoModel = cfg.Defaults.Video.Model;

        SelectedProvider = DefaultTextProvider;

        var qwen = cfg.Providers.Qwen;
        QwenEnabled = qwen.Enabled;
        QwenApiKey = qwen.ApiKey;
        QwenEndpoint = qwen.Endpoint;
        QwenTimeoutSeconds = qwen.TimeoutSeconds;
        QwenDefaultTextModel = qwen.DefaultModels.Text;
        QwenDefaultImageModel = qwen.DefaultModels.Image;
        QwenDefaultVideoModel = qwen.DefaultModels.Video;

        var volc = cfg.Providers.Volcengine;
        VolcengineEnabled = volc.Enabled;
        VolcengineApiKey = volc.ApiKey;
        VolcengineEndpoint = volc.Endpoint;
        VolcengineTimeoutSeconds = volc.TimeoutSeconds;
        VolcengineDefaultTextModel = volc.DefaultModels.Text;
        VolcengineDefaultImageModel = volc.DefaultModels.Image;
        VolcengineDefaultVideoModel = volc.DefaultModels.Video;
    }

    partial void OnSelectedProviderChanged(AIProviderType value)
    {
        OnPropertyChanged(nameof(IsQwenSelected));
        OnPropertyChanged(nameof(IsVolcengineSelected));
    }

    partial void OnDefaultTextProviderChanged(AIProviderType value)
    {
        DefaultTextModel = value switch
        {
            AIProviderType.Volcengine => VolcengineDefaultTextModel,
            _ => QwenDefaultTextModel
        };
    }

    partial void OnDefaultTextModelChanged(string value)
    {
        if (DefaultTextProvider == AIProviderType.Volcengine)
        {
            VolcengineDefaultTextModel = value;
        }
        else
        {
            QwenDefaultTextModel = value;
        }
    }

    partial void OnDefaultImageProviderChanged(AIProviderType value)
    {
        DefaultImageModel = value switch
        {
            AIProviderType.Volcengine => VolcengineDefaultImageModel,
            _ => QwenDefaultImageModel
        };
    }

    partial void OnDefaultImageModelChanged(string value)
    {
        if (DefaultImageProvider == AIProviderType.Volcengine)
        {
            VolcengineDefaultImageModel = value;
        }
        else
        {
            QwenDefaultImageModel = value;
        }
    }

    partial void OnDefaultVideoProviderChanged(AIProviderType value)
    {
        DefaultVideoModel = value switch
        {
            AIProviderType.Volcengine => VolcengineDefaultVideoModel,
            _ => QwenDefaultVideoModel
        };
    }

    partial void OnDefaultVideoModelChanged(string value)
    {
        if (DefaultVideoProvider == AIProviderType.Volcengine)
        {
            VolcengineDefaultVideoModel = value;
        }
        else
        {
            QwenDefaultVideoModel = value;
        }
    }

    [RelayCommand]
    private void Reload()
    {
        LoadFromFile();
        StatusMessage = "已从 user.ai.settings.json 重新加载。";
    }

    [RelayCommand]
    private void Save()
    {
        if (TrySave(out var error))
        {
            StatusMessage = "配置已保存到 user.ai.settings.json。";
            return;
        }

        StatusMessage = $"保存失败：{error}";
    }

    private bool TrySave(out string? error)
    {
        try
        {
            var qwenConfig = BuildProviderUserConfig(
                QwenApiKey,
                QwenEnabled,
                QwenEndpoint,
                QwenTimeoutSeconds,
                QwenDefaultTextModel,
                QwenDefaultImageModel,
                QwenDefaultVideoModel);

            var volcConfig = BuildProviderUserConfig(
                VolcengineApiKey,
                VolcengineEnabled,
                VolcengineEndpoint,
                VolcengineTimeoutSeconds,
                VolcengineDefaultTextModel,
                VolcengineDefaultImageModel,
                VolcengineDefaultVideoModel);

            _configComposer.SaveUserConfiguration("Qwen", qwenConfig);
            _configComposer.SaveUserConfiguration("Volcengine", volcConfig);

            var userSettings = _userSettingsStore.Load();
            userSettings.DefaultProviders.TextProvider = DefaultTextProvider.ToString();
            userSettings.DefaultProviders.ImageProvider = DefaultImageProvider.ToString();
            userSettings.DefaultProviders.VideoProvider = DefaultVideoProvider.ToString();
            _userSettingsStore.Save(userSettings);

            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static ProviderUserConfig BuildProviderUserConfig(
        string apiKey,
        bool enabled,
        string endpoint,
        int timeoutSeconds,
        string defaultTextModel,
        string defaultImageModel,
        string defaultVideoModel)
    {
        var config = new ProviderUserConfig
        {
            ApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim(),
            Enabled = enabled,
            Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim(),
            TimeoutSeconds = timeoutSeconds
        };

        var defaultModels = new Dictionary<string, string>();

        if (!string.IsNullOrWhiteSpace(defaultTextModel))
        {
            defaultModels["Text"] = defaultTextModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(defaultImageModel))
        {
            defaultModels["Image"] = defaultImageModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(defaultVideoModel))
        {
            defaultModels["Video"] = defaultVideoModel.Trim();
        }

        if (defaultModels.Count > 0)
        {
            config.DefaultModels = defaultModels;
        }

        return config;
    }

    [RelayCommand]
    private async Task ValidateAsync()
    {
        ValidationResults.Clear();

        if (!TrySave(out var saveError))
        {
            StatusMessage = $"验证前保存失败：{saveError}";
            return;
        }

        try
        {
            StatusMessage = "正在验证提供者配置...";
            var results = await _aiManager.ValidateAllProvidersAsync().ConfigureAwait(false);

            foreach (var kv in results.OrderBy(kv => kv.Key))
            {
                ValidationResults.Add(new ProviderValidationResult
                {
                    Provider = kv.Key,
                    Success = kv.Value,
                    Message = kv.Value ? "配置有效" : "配置无效",
                    Timestamp = DateTimeOffset.Now
                });
            }

            StatusMessage = "验证完成。";
        }
        catch (Exception ex)
        {
            ValidationResults.Add(new ProviderValidationResult
            {
                Provider = DefaultTextProvider,
                Success = false,
                Message = $"验证错误：{ex.Message}",
                Timestamp = DateTimeOffset.Now
            });
            StatusMessage = "验证失败。";
        }
    }
}
