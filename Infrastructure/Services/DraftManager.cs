using Microsoft.Extensions.Logging;
using Storyboard.Models.CapCut;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// CapCut 草稿管理服务 - 负责加载、保存和管理草稿文件
/// </summary>
public interface IDraftManager
{
    /// <summary>
    /// 创建新草稿
    /// </summary>
    Task<(DraftContent content, DraftMetaInfo meta)> CreateNewDraftAsync(string projectName, string projectPath);

    /// <summary>
    /// 加载草稿
    /// </summary>
    Task<(DraftContent content, DraftMetaInfo meta)> LoadDraftAsync(string draftDirectory);

    /// <summary>
    /// 保存草稿
    /// </summary>
    Task SaveDraftAsync(string draftDirectory, DraftContent content, DraftMetaInfo meta, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取草稿路径
    /// </summary>
    string GetDraftDirectory(string projectPath);
}

public class DraftManager : IDraftManager
{
    private readonly ILogger<DraftManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public DraftManager(ILogger<DraftManager> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };
    }

    public async Task<(DraftContent content, DraftMetaInfo meta)> CreateNewDraftAsync(string projectName, string projectPath)
    {
        try
        {
            _logger.LogInformation("创建新草稿: {ProjectName}", projectName);

            var draftId = Guid.NewGuid().ToString("N").ToUpper();
            var draftDirectory = GetDraftDirectory(projectPath);

            // 创建草稿目录
            Directory.CreateDirectory(draftDirectory);
            Directory.CreateDirectory(Path.Combine(draftDirectory, "materials"));

            // 加载模板
            var templateDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "templates", "capcut");
            var content = await LoadTemplateAsync<DraftContent>(Path.Combine(templateDirectory, "draft_content_template.json"));
            var meta = await LoadTemplateAsync<DraftMetaInfo>(Path.Combine(templateDirectory, "draft_meta_info.json"));

            // 初始化基本信息
            content.Id = draftId;
            content.Name = projectName;
            content.CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            content.UpdateTime = content.CreateTime;

            meta.DraftId = draftId;
            meta.DraftName = projectName;
            meta.DraftFoldPath = draftDirectory;
            meta.DraftRootPath = Path.GetDirectoryName(draftDirectory) ?? draftDirectory;
            meta.DraftRemovableStorageDevice = (Path.GetPathRoot(draftDirectory) ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            meta.DraftNeedRenameFolder = false;
            meta.DraftTimelineMaterialsSize = 0;
            var nowMicro = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
            meta.TmDraftCreate = nowMicro;
            meta.TmDraftModified = nowMicro;

            // 保存初始草稿
            await SaveDraftAsync(draftDirectory, content, meta);

            _logger.LogInformation("草稿创建成功: {DraftDirectory}", draftDirectory);
            return (content, meta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "创建草稿失败: {ProjectName}", projectName);
            throw;
        }
    }

    public async Task<(DraftContent content, DraftMetaInfo meta)> LoadDraftAsync(string draftDirectory)
    {
        try
        {
            _logger.LogInformation("加载草稿: {DraftDirectory}", draftDirectory);

            var contentPath = Path.Combine(draftDirectory, "draft_content.json");
            var metaPath = Path.Combine(draftDirectory, "draft_meta_info.json");

            if (!File.Exists(contentPath) || !File.Exists(metaPath))
            {
                throw new FileNotFoundException("草稿文件不存在");
            }

            var contentJson = await File.ReadAllTextAsync(contentPath);
            var metaJson = await File.ReadAllTextAsync(metaPath);

            var content = JsonSerializer.Deserialize<DraftContent>(contentJson, _jsonOptions)
                ?? throw new InvalidOperationException("无法解析 draft_content.json");
            var meta = JsonSerializer.Deserialize<DraftMetaInfo>(metaJson, _jsonOptions)
                ?? throw new InvalidOperationException("无法解析 draft_meta_info.json");

            _logger.LogInformation("草稿加载成功: {DraftId}, 轨道数: {TrackCount}", content.Id, content.Tracks.Count);
            return (content, meta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载草稿失败: {DraftDirectory}", draftDirectory);
            throw;
        }
    }

    public async Task SaveDraftAsync(string draftDirectory, DraftContent content, DraftMetaInfo meta, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("保存草稿: {DraftDirectory}", draftDirectory);

            // 确保目录存在
            Directory.CreateDirectory(draftDirectory);

            // 更新时间戳
            content.UpdateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            meta.TmDraftModified = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;

            // 保存 draft_content.json
            var contentPath = Path.Combine(draftDirectory, "draft_content.json");
            var contentJson = JsonSerializer.Serialize(content, _jsonOptions);
            await File.WriteAllTextAsync(contentPath, contentJson, cancellationToken);

            // 保存 draft_meta_info.json
            var metaPath = Path.Combine(draftDirectory, "draft_meta_info.json");
            var metaJson = JsonSerializer.Serialize(meta, _jsonOptions);
            await File.WriteAllTextAsync(metaPath, metaJson, cancellationToken);

            _logger.LogDebug("草稿保存成功: {DraftId}", content.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存草稿失败: {DraftDirectory}", draftDirectory);
            throw;
        }
    }

    public string GetDraftDirectory(string projectPath)
    {
        // 在项目目录下创建 draft 子目录
        return Path.Combine(projectPath, "draft");
    }

    private async Task<T> LoadTemplateAsync<T>(string templatePath) where T : new()
    {
        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("模板文件不存在: {TemplatePath}, 使用默认模板", templatePath);
            return new T();
        }

        var json = await File.ReadAllTextAsync(templatePath);
        return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
    }
}
