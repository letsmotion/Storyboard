using Microsoft.Extensions.Logging;
using Storyboard.Models;
using Storyboard.Models.CapCut;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// CapCut 导出服务 - 将时间轴导出为 CapCut 草稿格式
/// </summary>
public interface ICapCutExportService
{
    /// <summary>
    /// 导出为 CapCut 草稿
    /// </summary>
    /// <param name="shots">镜头列表</param>
    /// <param name="outputDirectory">输出目录</param>
    /// <param name="projectName">项目名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>草稿目录路径</returns>
    Task<string> ExportToCapCutAsync(
        List<ShotItem> shots,
        string outputDirectory,
        string projectName,
        CancellationToken cancellationToken = default);
}

public class CapCutExportService : ICapCutExportService
{
    private readonly ILogger<CapCutExportService> _logger;
    private const long MICROSECONDS_PER_SECOND = 1_000_000;

    public CapCutExportService(ILogger<CapCutExportService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExportToCapCutAsync(
        List<ShotItem> shots,
        string outputDirectory,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("开始导出 CapCut 草稿: {ProjectName}, 镜头数: {ShotCount}", projectName, shots.Count);

            // 创建草稿目录
            var draftId = Guid.NewGuid().ToString("N").ToUpper();
            var draftDirectory = Path.Combine(outputDirectory, $"CapCut_Draft_{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(draftDirectory);

            // 加载模板
            var templateDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "templates", "capcut");
            var contentTemplate = await LoadTemplateAsync<DraftContent>(
                Path.Combine(templateDirectory, "draft_content_template.json"));
            var metaTemplate = await LoadTemplateAsync<DraftMetaInfo>(
                Path.Combine(templateDirectory, "draft_meta_info.json"));

            // 构建草稿内容
            var draftContent = BuildDraftContent(contentTemplate, shots, projectName, draftId);
            var draftMetaInfo = BuildDraftMetaInfo(metaTemplate, shots, projectName, draftId, draftDirectory);

            // 复制视频文件到草稿目录
            var materialsDirectory = Path.Combine(draftDirectory, "materials");
            Directory.CreateDirectory(materialsDirectory);
            await CopyVideoMaterialsAsync(shots, materialsDirectory, draftContent, cancellationToken);

            // 保存草稿文件
            await SaveDraftFilesAsync(draftDirectory, draftContent, draftMetaInfo, cancellationToken);

            _logger.LogInformation("CapCut 草稿导出成功: {DraftDirectory}", draftDirectory);
            return draftDirectory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "导出 CapCut 草稿失败");
            throw;
        }
    }

    /// <summary>
    /// 加载模板文件
    /// </summary>
    private async Task<T> LoadTemplateAsync<T>(string templatePath) where T : new()
    {
        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("模板文件不存在: {TemplatePath}, 使用默认模板", templatePath);
            return new T();
        }

        var json = await File.ReadAllTextAsync(templatePath);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new T();
    }

    /// <summary>
    /// 构建草稿内容
    /// </summary>
    private DraftContent BuildDraftContent(DraftContent template, List<ShotItem> shots, string projectName, string draftId)
    {
        var content = template;
        content.Id = draftId;
        content.Name = projectName;
        content.CreateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        content.UpdateTime = content.CreateTime;

        // 计算总时长（微秒）
        var totalDurationSeconds = shots.Sum(s => s.Duration);
        content.Duration = (long)(totalDurationSeconds * MICROSECONDS_PER_SECOND);

        // 创建视频轨道
        var videoTrack = new Track
        {
            Id = Guid.NewGuid().ToString("N").ToUpper(),
            Type = "video",
            Segments = new List<Segment>()
        };

        // 添加视频片段
        long currentTime = 0;
        foreach (var shot in shots.OrderBy(s => s.ShotNumber))
        {
            if (string.IsNullOrEmpty(shot.GeneratedVideoPath) || !File.Exists(shot.GeneratedVideoPath))
            {
                _logger.LogWarning("跳过未生成的镜头: Shot #{ShotNumber}", shot.ShotNumber);
                continue;
            }

            var materialId = Guid.NewGuid().ToString("N").ToUpper();
            var durationMicroseconds = (long)(shot.Duration * MICROSECONDS_PER_SECOND);

            // 添加视频素材
            content.Materials.Videos.Add(new VideoMaterial
            {
                Id = materialId,
                Path = shot.GeneratedVideoPath,
                Duration = durationMicroseconds,
                Width = 1920, // 默认分辨率，可以从视频文件读取
                Height = 1080
            });

            // 添加片段
            videoTrack.Segments.Add(new Segment
            {
                Id = Guid.NewGuid().ToString("N").ToUpper(),
                MaterialId = materialId,
                TargetTimerange = new TimeRange
                {
                    Start = currentTime,
                    Duration = durationMicroseconds
                },
                SourceTimerange = new TimeRange
                {
                    Start = 0,
                    Duration = durationMicroseconds
                }
            });

            currentTime += durationMicroseconds;
        }

        content.Tracks.Add(videoTrack);

        _logger.LogInformation("构建草稿内容完成: 轨道数={TrackCount}, 片段数={SegmentCount}",
            content.Tracks.Count, videoTrack.Segments.Count);

        return content;
    }

    /// <summary>
    /// 构建草稿元信息
    /// </summary>
    private DraftMetaInfo BuildDraftMetaInfo(
        DraftMetaInfo template,
        List<ShotItem> shots,
        string projectName,
        string draftId,
        string draftDirectory)
    {
        var metaInfo = template;
        metaInfo.DraftId = draftId;
        metaInfo.DraftName = projectName;
        metaInfo.DraftFoldPath = draftDirectory;
        metaInfo.DraftRootPath = draftDirectory;

        // 计算总时长（微秒）
        var totalDurationSeconds = shots.Sum(s => s.Duration);
        metaInfo.TmDuration = (long)(totalDurationSeconds * MICROSECONDS_PER_SECOND);

        return metaInfo;
    }

    /// <summary>
    /// 复制视频素材到草稿目录
    /// </summary>
    private async Task CopyVideoMaterialsAsync(
        List<ShotItem> shots,
        string materialsDirectory,
        DraftContent draftContent,
        CancellationToken cancellationToken)
    {
        foreach (var shot in shots.OrderBy(s => s.ShotNumber))
        {
            if (string.IsNullOrEmpty(shot.GeneratedVideoPath) || !File.Exists(shot.GeneratedVideoPath))
                continue;

            var fileName = Path.GetFileName(shot.GeneratedVideoPath);
            var destPath = Path.Combine(materialsDirectory, fileName);

            // 复制视频文件
            await Task.Run(() => File.Copy(shot.GeneratedVideoPath, destPath, overwrite: true), cancellationToken);

            // 更新素材路径为相对路径
            var material = draftContent.Materials.Videos.FirstOrDefault(v => v.Path == shot.GeneratedVideoPath);
            if (material != null)
            {
                material.Path = Path.Combine("materials", fileName);
            }

            _logger.LogDebug("复制视频素材: {FileName}", fileName);
        }
    }

    /// <summary>
    /// 保存草稿文件
    /// </summary>
    private async Task SaveDraftFilesAsync(
        string draftDirectory,
        DraftContent draftContent,
        DraftMetaInfo draftMetaInfo,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        // 保存 draft_content.json
        var contentPath = Path.Combine(draftDirectory, "draft_content.json");
        var contentJson = JsonSerializer.Serialize(draftContent, options);
        await File.WriteAllTextAsync(contentPath, contentJson, cancellationToken);

        // 保存 draft_meta_info.json
        var metaPath = Path.Combine(draftDirectory, "draft_meta_info.json");
        var metaJson = JsonSerializer.Serialize(draftMetaInfo, options);
        await File.WriteAllTextAsync(metaPath, metaJson, cancellationToken);

        _logger.LogInformation("草稿文件保存完成: {ContentPath}, {MetaPath}", contentPath, metaPath);
    }
}
