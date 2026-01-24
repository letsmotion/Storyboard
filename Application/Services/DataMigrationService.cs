using Microsoft.Extensions.Logging;
using Storyboard.Infrastructure.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Storyboard.Application.Services;

/// <summary>
/// 数据迁移服务 - 用于将数据和输出文件迁移到新位置
/// </summary>
public class DataMigrationService
{
    private readonly ILogger<DataMigrationService> _logger;

    public DataMigrationService(ILogger<DataMigrationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 迁移数据到新位置
    /// </summary>
    public async Task<bool> MigrateDataAsync(
        string oldDataDir,
        string oldOutputDir,
        string newDataDir,
        string newOutputDir,
        IProgress<(int progress, string status)>? progress = null)
    {
        try
        {
            _logger.LogInformation("开始数据迁移: {OldData} -> {NewData}, {OldOutput} -> {NewOutput}",
                oldDataDir, newDataDir, oldOutputDir, newOutputDir);

            progress?.Report((0, "验证目标路径..."));

            // 验证目标路径
            if (!ValidateMigrationPaths(oldDataDir, oldOutputDir, newDataDir, newOutputDir))
            {
                return false;
            }

            // 创建目标目录
            progress?.Report((5, "创建目标目录..."));
            Directory.CreateDirectory(newDataDir);
            Directory.CreateDirectory(newOutputDir);

            // 迁移数据库文件
            progress?.Report((10, "迁移数据库文件..."));
            await MigrateDataDirectoryAsync(oldDataDir, newDataDir, progress);

            // 迁移输出文件
            progress?.Report((50, "迁移输出文件..."));
            await MigrateOutputDirectoryAsync(oldOutputDir, newOutputDir, progress);

            // 更新配置文件
            progress?.Report((95, "更新配置文件..."));
            await UpdateConfigurationAsync(newDataDir, newOutputDir);

            progress?.Report((100, "迁移完成"));
            _logger.LogInformation("数据迁移成功完成");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "数据迁移失败");
            progress?.Report((0, $"迁移失败: {ex.Message}"));
            return false;
        }
    }

    private bool ValidateMigrationPaths(string oldDataDir, string oldOutputDir, string newDataDir, string newOutputDir)
    {
        // 检查源目录是否存在
        if (!Directory.Exists(oldDataDir))
        {
            _logger.LogWarning("源数据目录不存在: {Path}", oldDataDir);
        }

        if (!Directory.Exists(oldOutputDir))
        {
            _logger.LogWarning("源输出目录不存在: {Path}", oldOutputDir);
        }

        // 检查目标路径是否与源路径相同
        if (Path.GetFullPath(oldDataDir).Equals(Path.GetFullPath(newDataDir), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("数据目录路径未改变");
        }

        if (Path.GetFullPath(oldOutputDir).Equals(Path.GetFullPath(newOutputDir), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("输出目录路径未改变");
        }

        // 检查目标路径是否有足够的空间
        try
        {
            var newDataDrive = Path.GetPathRoot(newDataDir);
            var newOutputDrive = Path.GetPathRoot(newOutputDir);

            if (!string.IsNullOrEmpty(newDataDrive))
            {
                var driveInfo = new DriveInfo(newDataDrive);
                var requiredSpace = CalculateDirectorySize(oldDataDir);
                if (driveInfo.AvailableFreeSpace < requiredSpace * 1.1) // 需要 110% 的空间以确保安全
                {
                    _logger.LogError("目标磁盘空间不足: 需要 {Required} MB, 可用 {Available} MB",
                        requiredSpace / 1024 / 1024, driveInfo.AvailableFreeSpace / 1024 / 1024);
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "无法检查磁盘空间");
        }

        return true;
    }

    private async Task MigrateDataDirectoryAsync(string oldDir, string newDir, IProgress<(int progress, string status)>? progress)
    {
        if (!Directory.Exists(oldDir))
        {
            _logger.LogInformation("源数据目录不存在，跳过迁移");
            return;
        }

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(oldDir, "*", SearchOption.AllDirectories);
            var totalFiles = files.Length;
            var processedFiles = 0;

            foreach (var file in files)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(oldDir, file);
                    var targetPath = Path.Combine(newDir, relativePath);
                    var targetDir = Path.GetDirectoryName(targetPath);

                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(file, targetPath, true);
                    processedFiles++;

                    var progressPercent = 10 + (int)((processedFiles / (double)totalFiles) * 40);
                    progress?.Report((progressPercent, $"迁移数据文件: {processedFiles}/{totalFiles}"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "复制文件失败: {File}", file);
                }
            }
        });
    }

    private async Task MigrateOutputDirectoryAsync(string oldDir, string newDir, IProgress<(int progress, string status)>? progress)
    {
        if (!Directory.Exists(oldDir))
        {
            _logger.LogInformation("源输出目录不存在，跳过迁移");
            return;
        }

        await Task.Run(() =>
        {
            var files = Directory.GetFiles(oldDir, "*", SearchOption.AllDirectories);
            var totalFiles = files.Length;
            var processedFiles = 0;

            foreach (var file in files)
            {
                try
                {
                    var relativePath = Path.GetRelativePath(oldDir, file);
                    var targetPath = Path.Combine(newDir, relativePath);
                    var targetDir = Path.GetDirectoryName(targetPath);

                    if (!string.IsNullOrEmpty(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    File.Copy(file, targetPath, true);
                    processedFiles++;

                    var progressPercent = 50 + (int)((processedFiles / (double)totalFiles) * 45);
                    progress?.Report((progressPercent, $"迁移输出文件: {processedFiles}/{totalFiles}"));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "复制文件失败: {File}", file);
                }
            }
        });
    }

    private Task UpdateConfigurationAsync(string newDataDir, string newOutputDir)
    {
        try
        {
            var store = new UserSettingsStore();
            var settings = store.Load();
            settings.Storage.DataDirectory = newDataDir;
            settings.Storage.OutputDirectory = newOutputDir;
            settings.Storage.UseCustomLocation = true;
            settings.Storage.Configured = true;
            store.Save(settings);

            _logger.LogInformation("User settings updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user settings.");
            throw;
        }

        return Task.CompletedTask;
    }

    private long CalculateDirectorySize(string directory)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            var dirInfo = new DirectoryInfo(directory);
            long size = 0;

            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                size += file.Length;
            }

            return size;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 删除旧数据（在确认迁移成功后）
    /// </summary>
    public async Task<bool> CleanupOldDataAsync(string oldDataDir, string oldOutputDir)
    {
        try
        {
            _logger.LogInformation("清理旧数据: {DataDir}, {OutputDir}", oldDataDir, oldOutputDir);

            await Task.Run(() =>
            {
                if (Directory.Exists(oldDataDir))
                {
                    Directory.Delete(oldDataDir, true);
                    _logger.LogInformation("已删除旧数据目录: {Path}", oldDataDir);
                }

                if (Directory.Exists(oldOutputDir))
                {
                    Directory.Delete(oldOutputDir, true);
                    _logger.LogInformation("已删除旧输出目录: {Path}", oldOutputDir);
                }
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "清理旧数据失败");
            return false;
        }
    }
}
