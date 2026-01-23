using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velopack;
using Velopack.Sources;
using StoryboardUpdateOptions = Storyboard.Infrastructure.Configuration.UpdateOptions;

namespace Storyboard.Application.Services;

/// <summary>
/// 自动更新服务（支持 Gitee + GitHub 双源智能切换）
/// </summary>
public class UpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly StoryboardUpdateOptions _updateOptions;
    private UpdateManager? _updateManager;
    private IUpdateSource? _currentSource;

    public UpdateService(ILogger<UpdateService> logger, IOptions<StoryboardUpdateOptions> updateOptions)
    {
        _logger = logger;
        _updateOptions = updateOptions.Value;

        try
        {
            // 尝试初始化 UpdateManager
            // 如果应用不是通过 Velopack 安装的（例如开发环境），这里会失败
            InitializeUpdateManager();
            _logger.LogInformation("UpdateManager 初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogInformation("应用未通过 Velopack 安装，自动更新功能不可用");
            _logger.LogDebug(ex, "UpdateManager 初始化失败详情");
        }
    }

    /// <summary>
    /// 初始化 UpdateManager，智能选择最佳更新源
    /// </summary>
    private void InitializeUpdateManager()
    {
        // 仅在 Windows 平台启用自动更新
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("当前平台不支持自动更新（仅 Windows 支持），请手动下载更新");
            return;
        }

        if (!_updateOptions.Enabled || _updateOptions.Sources == null || _updateOptions.Sources.Count == 0)
        {
            _logger.LogWarning("自动更新未启用或未配置更新源");
            return;
        }

        // 按优先级排序更新源
        var enabledSources = _updateOptions.Sources
            .Where(s => s.Enabled)
            .OrderBy(s => s.Priority)
            .ToList();

        if (enabledSources.Count == 0)
        {
            _logger.LogWarning("没有可用的更新源");
            return;
        }

        // 尝试使用第一个可用的源（优先级最高）
        foreach (var source in enabledSources)
        {
            try
            {
                _logger.LogInformation($"尝试使用 {source.Name} 更新源 (类型: {source.Type}): {source.Url}");

                // 根据源类型创建不同的 IUpdateSource
                _currentSource = CreateUpdateSource(source);
                _updateManager = new UpdateManager(_currentSource);
                _logger.LogInformation($"成功初始化 {source.Name} 更新源");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"{source.Name} 更新源初始化失败，尝试下一个源");
            }
        }

        _logger.LogError("所有更新源初始化失败");
    }

    /// <summary>
    /// 根据配置创建更新源
    /// </summary>
    private IUpdateSource CreateUpdateSource(Infrastructure.Configuration.UpdateSource source)
    {
        return source.Type.ToLowerInvariant() switch
        {
            "http" or "https" => new SimpleWebSource(source.Url),
            "github" => new GithubSource(source.Url, null, false),
            "gitee" => new GithubSource(source.Url, null, false), // Gitee 使用相同的 API 格式
            _ => new GithubSource(source.Url, null, false)
        };
    }

    /// <summary>
    /// 切换到备用更新源
    /// </summary>
    private bool TryFallbackSource()
    {
        if (_updateOptions.Sources == null || _updateOptions.Sources.Count <= 1)
        {
            return false;
        }

        var enabledSources = _updateOptions.Sources
            .Where(s => s.Enabled)
            .OrderBy(s => s.Priority)
            .ToList();

        // 找到当前源的索引
        var currentUrl = GetCurrentSourceUrl();
        var currentIndex = enabledSources.FindIndex(s => s.Url == currentUrl);
        if (currentIndex < 0 || currentIndex >= enabledSources.Count - 1)
        {
            return false;
        }

        // 尝试下一个源
        for (int i = currentIndex + 1; i < enabledSources.Count; i++)
        {
            var source = enabledSources[i];
            try
            {
                _logger.LogInformation($"切换到备用更新源: {source.Name} (类型: {source.Type})");
                _currentSource = CreateUpdateSource(source);
                _updateManager = new UpdateManager(_currentSource);
                _logger.LogInformation($"成功切换到 {source.Name} 更新源");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"切换到 {source.Name} 失败");
            }
        }

        return false;
    }

    /// <summary>
    /// 获取当前源的 URL
    /// </summary>
    private string GetCurrentSourceUrl()
    {
        if (_currentSource is GithubSource githubSource)
        {
            return githubSource.RepoUri.ToString();
        }
        else if (_currentSource is SimpleWebSource)
        {
            // SimpleWebSource 没有公开的 URL 属性，从配置中查找
            if (_updateOptions.Sources != null)
            {
                var httpSource = _updateOptions.Sources.FirstOrDefault(s =>
                    s.Type.Equals("Http", StringComparison.OrdinalIgnoreCase) ||
                    s.Type.Equals("Https", StringComparison.OrdinalIgnoreCase));
                if (httpSource != null)
                {
                    return httpSource.Url;
                }
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// 检查是否有可用更新（支持自动切换更新源）
    /// </summary>
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        if (_updateManager == null)
        {
            _logger.LogInformation("应用未通过安装程序安装，跳过更新检查");
            return null;
        }

        try
        {
            _logger.LogInformation("开始检查更新...");
            var updateInfo = await _updateManager.CheckForUpdatesAsync();

            if (updateInfo == null)
            {
                _logger.LogInformation("当前已是最新版本");
                return null;
            }

            _logger.LogInformation($"发现新版本: {updateInfo.TargetFullRelease.Version}");
            return updateInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "检查更新失败");

            // 尝试切换到备用更新源
            if (TryFallbackSource())
            {
                _logger.LogInformation("已切换到备用更新源，重试检查更新");
                try
                {
                    var updateInfo = await _updateManager!.CheckForUpdatesAsync();
                    if (updateInfo != null)
                    {
                        _logger.LogInformation($"发现新版本: {updateInfo.TargetFullRelease.Version}");
                    }
                    return updateInfo;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "使用备用源检查更新仍然失败");
                }
            }

            return null;
        }
    }

    /// <summary>
    /// 下载更新（支持自动切换更新源）
    /// </summary>
    public async Task<bool> DownloadUpdatesAsync(UpdateInfo updateInfo, IProgress<int>? progress = null)
    {
        if (_updateManager == null)
        {
            return false;
        }

        try
        {
            _logger.LogInformation("开始下载更新...");

            // Velopack 的 DownloadUpdatesAsync 接受 Action<int> 而不是 IProgress<int>
            Action<int>? progressAction = null;
            if (progress != null)
            {
                progressAction = p => progress.Report(p);
            }

            await _updateManager.DownloadUpdatesAsync(updateInfo, progressAction);
            _logger.LogInformation("更新下载完成");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "下载更新失败");

            // 尝试切换到备用更新源
            if (TryFallbackSource())
            {
                _logger.LogInformation("已切换到备用更新源，重试下载更新");
                try
                {
                    Action<int>? progressAction = null;
                    if (progress != null)
                    {
                        progressAction = p => progress.Report(p);
                    }

                    await _updateManager!.DownloadUpdatesAsync(updateInfo, progressAction);
                    _logger.LogInformation("更新下载完成");
                    return true;
                }
                catch (Exception retryEx)
                {
                    _logger.LogError(retryEx, "使用备用源下载更新仍然失败");
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 应用更新并重启应用
    /// </summary>
    public void ApplyUpdatesAndRestart(UpdateInfo updateInfo)
    {
        if (_updateManager == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("准备应用更新并重启...");
            _updateManager.ApplyUpdatesAndRestart(updateInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用更新失败");
        }
    }

    /// <summary>
    /// 应用更新但不重启（下次启动时生效）
    /// </summary>
    public async Task<bool> ApplyUpdatesAsync(UpdateInfo updateInfo)
    {
        if (_updateManager == null)
        {
            return false;
        }

        try
        {
            _logger.LogInformation("准备应用更新（下次启动生效）...");
            await _updateManager.WaitExitThenApplyUpdatesAsync(updateInfo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "应用更新失败");
            return false;
        }
    }

    /// <summary>
    /// 获取当前版本号
    /// </summary>
    public string GetCurrentVersion()
    {
        try
        {
            if (_updateManager != null)
            {
                return _updateManager.CurrentVersion?.ToString() ?? "Unknown";
            }

            // 开发环境返回程序集版本
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            return version?.ToString() ?? "Dev";
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <summary>
    /// 获取当前使用的更新源名称
    /// </summary>
    public string GetCurrentSourceName()
    {
        if (_currentSource == null || _updateOptions.Sources == null)
        {
            return "Unknown";
        }

        var currentUrl = GetCurrentSourceUrl();
        var source = _updateOptions.Sources.FirstOrDefault(s => s.Url == currentUrl);
        return source?.Name ?? "Unknown";
    }

    /// <summary>
    /// 是否通过安装程序安装
    /// </summary>
    public bool IsInstalled => _updateManager != null;
}
