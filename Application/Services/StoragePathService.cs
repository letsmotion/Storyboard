using Microsoft.Extensions.Options;
using Storyboard.Infrastructure.Configuration;

namespace Storyboard.Application.Services;

/// <summary>
/// 存储路径管理服务
/// 统一管理数据库和输出文件的存储位置
/// </summary>
public class StoragePathService
{
    private readonly StorageOptions _options;
    private readonly string _defaultDataDir;
    private readonly string _defaultOutputDir;

    public StoragePathService(IOptions<StorageOptions> options)
    {
        _options = options.Value;

        // 使用用户目录作为默认位置，避免更新时被覆盖
        // Windows: C:\Users\用户名\AppData\Local\Storyboard
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appStoragePath = Path.Combine(appDataPath, "Storyboard");

        _defaultDataDir = Path.Combine(appStoragePath, "Data");
        _defaultOutputDir = Path.Combine(appStoragePath, "output");
    }

    /// <summary>
    /// 获取数据库存储目录
    /// </summary>
    public string GetDataDirectory()
    {
        if (_options.UseCustomLocation && !string.IsNullOrWhiteSpace(_options.DataDirectory))
        {
            return _options.DataDirectory;
        }
        return _defaultDataDir;
    }

    /// <summary>
    /// 获取输出文件存储目录
    /// </summary>
    public string GetOutputDirectory()
    {
        if (_options.UseCustomLocation && !string.IsNullOrWhiteSpace(_options.OutputDirectory))
        {
            return _options.OutputDirectory;
        }
        return _defaultOutputDir;
    }

    /// <summary>
    /// 获取数据库文件路径
    /// </summary>
    public string GetDatabasePath()
    {
        var dataDir = GetDataDirectory();
        EnsureDirectoryExists(dataDir);
        return Path.Combine(dataDir, "storyboard.db");
    }

    /// <summary>
    /// 获取图片输出目录
    /// </summary>
    public string GetImagesOutputDirectory()
    {
        var outputDir = GetOutputDirectory();
        var imagesDir = Path.Combine(outputDir, "images");
        EnsureDirectoryExists(imagesDir);
        return imagesDir;
    }

    /// <summary>
    /// 获取视频输出目录
    /// </summary>
    public string GetVideosOutputDirectory()
    {
        var outputDir = GetOutputDirectory();
        var videosDir = Path.Combine(outputDir, "videos");
        EnsureDirectoryExists(videosDir);
        return videosDir;
    }

    /// <summary>
    /// 获取分镜输出目录
    /// </summary>
    public string GetShotsOutputDirectory()
    {
        var outputDir = GetOutputDirectory();
        var shotsDir = Path.Combine(outputDir, "shots");
        EnsureDirectoryExists(shotsDir);
        return shotsDir;
    }

    /// <summary>
    /// 获取帧提取输出目录
    /// </summary>
    public string GetFramesOutputDirectory(string projectId)
    {
        var outputDir = GetOutputDirectory();
        var framesDir = Path.Combine(outputDir, "frames", projectId);
        EnsureDirectoryExists(framesDir);
        return framesDir;
    }

    /// <summary>
    /// 获取项目视频缩略图目录
    /// </summary>
    public string GetProjectVideoThumbnailsDirectory(string projectId)
    {
        var outputDir = GetOutputDirectory();
        var thumbDir = Path.Combine(outputDir, "projects", projectId, "video-thumbnails");
        EnsureDirectoryExists(thumbDir);
        return thumbDir;
    }

    /// <summary>
    /// 获取最终渲染输出目录
    /// </summary>
    public string GetFinalRenderOutputDirectory()
    {
        var outputDir = GetOutputDirectory();
        EnsureDirectoryExists(outputDir);
        return outputDir;
    }

    /// <summary>
    /// 确保目录存在
    /// </summary>
    private void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// 是否使用自定义存储位置
    /// </summary>
    public bool IsUsingCustomLocation => _options.UseCustomLocation;

    /// <summary>
    /// 获取存储位置信息（用于显示）
    /// </summary>
    public (string DataLocation, string OutputLocation) GetStorageLocations()
    {
        return (GetDataDirectory(), GetOutputDirectory());
    }
}
