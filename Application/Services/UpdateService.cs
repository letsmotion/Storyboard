using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace Storyboard.Application.Services;

/// <summary>
/// 自动更新服务
/// </summary>
public class UpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private readonly UpdateManager? _updateManager;
    private const string GitHubRepoUrl = "https://github.com/YOUR_USERNAME/YOUR_REPO"; // 请替换为你的 GitHub 仓库地址

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;

        try
        {
            // 尝试初始化 UpdateManager
            // 如果应用不是通过 Velopack 安装的（例如开发环境），这里会失败
            _updateManager = new UpdateManager(new GithubSource(GitHubRepoUrl, null, false));
            _logger.LogInformation("UpdateManager 初始化成功");
        }
        catch (Exception ex)
        {
            _logger.LogInformation("应用未通过 Velopack 安装，自动更新功能不可用");
            _logger.LogDebug(ex, "UpdateManager 初始化失败详情");
        }
    }

    /// <summary>
    /// 检查是否有可用更新
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
            return null;
        }
    }

    /// <summary>
    /// 下载更新
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
    /// 是否通过安装程序安装
    /// </summary>
    public bool IsInstalled => _updateManager != null;
}
