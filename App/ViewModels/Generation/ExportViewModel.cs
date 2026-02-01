using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Services;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// 导出 ViewModel - 负责最终视频合成和导出
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly IFinalRenderService _finalRenderService;
    private readonly ICapCutExportService _capCutExportService;
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;
    private readonly ILogger<ExportViewModel> _logger;

    [ObservableProperty]
    private bool _isExportDialogOpen;

    [ObservableProperty]
    private bool _canExportVideo;

    [ObservableProperty]
    private string _videoResolution = "1920x1080";

    [ObservableProperty]
    private string _videoFps = "30";

    [ObservableProperty]
    private string _videoFormat = "mp4";

    public ExportViewModel(
        IFinalRenderService finalRenderService,
        ICapCutExportService capCutExportService,
        IJobQueueService jobQueue,
        IMessenger messenger,
        ILogger<ExportViewModel> logger)
    {
        _finalRenderService = finalRenderService;
        _capCutExportService = capCutExportService;
        _jobQueue = jobQueue;
        _messenger = messenger;
        _logger = logger;

        // 订阅镜头变更消息以更新导出状态
        _messenger.Register<ShotAddedMessage>(this, (r, m) => UpdateCanExportVideo());
        _messenger.Register<ShotDeletedMessage>(this, (r, m) => UpdateCanExportVideo());
        _messenger.Register<VideoGenerationCompletedMessage>(this, (r, m) => UpdateCanExportVideo());
        _messenger.Register<ProjectDataLoadedMessage>(this, (r, m) => UpdateCanExportVideo());

        // 订阅任务队列变化以更新导出进度
        _jobQueue.Jobs.CollectionChanged += (s, e) =>
        {
            // 当添加新任务时，订阅其属性变化
            if (e.NewItems != null)
            {
                foreach (GenerationJob job in e.NewItems)
                {
                    if (job.Type == GenerationJobType.FullRender)
                    {
                        job.PropertyChanged += OnExportJobPropertyChanged;
                        UpdateExportProgress(job);
                    }
                }
            }

            // 当移除任务时，取消订阅
            if (e.OldItems != null)
            {
                foreach (GenerationJob job in e.OldItems)
                {
                    if (job.Type == GenerationJobType.FullRender)
                    {
                        job.PropertyChanged -= OnExportJobPropertyChanged;
                    }
                }
            }
        };
    }

    [RelayCommand]
    private void ShowExportDialog()
    {
        IsExportDialogOpen = true;
    }

    [RelayCommand]
    private async Task ExportVideo(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            _logger.LogWarning("导出路径为空");
            return;
        }

        try
        {
            _logger.LogInformation("开始导出视频到: {OutputPath}", outputPath);

            // 查询所有镜头
            var query = new GetAllShotsQuery();
            _messenger.Send(query);
            var shots = query.Shots;

            if (shots == null || shots.Count == 0)
            {
                _logger.LogWarning("没有镜头可导出");
                _messenger.Send(new ExportCompletedMessage(false, null, "没有镜头可导出。请先创建分镜。"));
                return;
            }

            // 获取所有已完成的镜头视频路径（跳过未完成的）
            var videoClips = shots
                .OrderBy(s => s.ShotNumber)
                .Select(s => s.GeneratedVideoPath)
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .ToList();

            if (videoClips.Count == 0)
            {
                _logger.LogWarning("没有已生成的视频可导出");
                _messenger.Send(new ExportCompletedMessage(false, null, "没有已生成的视频可导出。请先生成视频片段。"));
                return;
            }

            // 记录导出信息
            var completedCount = videoClips.Count;
            var totalCount = shots.Count;
            if (completedCount < totalCount)
            {
                _logger.LogInformation("导出已完成的分镜: {Completed}/{Total}，跳过 {Skipped} 个未完成的分镜",
                    completedCount, totalCount, totalCount - completedCount);
            }
            else
            {
                _logger.LogInformation("导出所有分镜: {Total}/{Total}", totalCount);
            }

            // 立即更新状态，告知用户导出已开始
            var statusMsg = completedCount < totalCount
                ? $"正在合成视频... ({completedCount}/{totalCount} 个片段，跳过 {totalCount - completedCount} 个未完成)"
                : $"正在合成视频... ({completedCount} 个片段)";

            // 通过发送一个特殊的消息来更新状态
            _messenger.Send(new ExportCompletedMessage(true, null, statusMsg));

            // 创建导出任务
            _jobQueue.Enqueue(
                GenerationJobType.FullRender,
                0,
                async (ct, progress) =>
                {
                    try
                    {
                        // Parse export settings
                        var fps = 30;
                        if (!int.TryParse(VideoFps, out fps) || fps <= 0)
                        {
                            _logger.LogWarning("无效的帧率值: {Fps}，使用默认值 30", VideoFps);
                            fps = 30;
                        }

                        var settings = new Application.Abstractions.VideoExportSettings(
                            Resolution: VideoResolution ?? "1920x1080",
                            Fps: fps,
                            Format: VideoFormat ?? "mp4"
                        );

                        _logger.LogInformation("导出设置: 分辨率={Resolution}, 帧率={Fps}, 格式={Format}",
                            settings.Resolution, settings.Fps, settings.Format);

                        var resultPath = await _finalRenderService.RenderAsync(
                            videoClips!,
                            ct,
                            progress,
                            settings);

                        if (!string.IsNullOrWhiteSpace(resultPath))
                        {
                            // 复制到目标路径
                            var dir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrWhiteSpace(dir))
                                Directory.CreateDirectory(dir);

                            File.Copy(resultPath, outputPath, overwrite: true);

                            _messenger.Send(new ExportCompletedMessage(true, outputPath));
                            _logger.LogInformation("视频导出成功: {OutputPath}", outputPath);

                            // 打开文件所在文件夹
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "explorer.exe",
                                    Arguments = $"/select,\"{outputPath}\"",
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        }
                        else
                        {
                            _messenger.Send(new ExportCompletedMessage(false, null, "视频导出失败：返回路径为空。"));
                            _logger.LogWarning("视频导出失败: 返回路径为空");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "视频导出失败");

                        // Extract user-friendly error message
                        var errorMsg = ex.Message;
                        if (string.IsNullOrWhiteSpace(errorMsg))
                            errorMsg = "发生未知错误，请查看日志。";

                        _messenger.Send(new ExportCompletedMessage(false, null, errorMsg));
                        throw;
                    }
                });

            IsExportDialogOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视频导出异常");
            _messenger.Send(new ExportCompletedMessage(false, null, $"视频导出异常：{ex.Message}"));
        }
    }

    /// <summary>
    /// 导出为 CapCut 草稿
    /// </summary>
    [RelayCommand]
    private async Task ExportToCapCut(string? outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            _logger.LogWarning("导出目录为空");
            return;
        }

        try
        {
            _logger.LogInformation("开始导出 CapCut 草稿到: {OutputDirectory}", outputDirectory);

            // 查询所有镜头
            var query = new GetAllShotsQuery();
            _messenger.Send(query);
            var shots = query.Shots;

            if (shots == null || shots.Count == 0)
            {
                _logger.LogWarning("没有镜头可导出");
                _messenger.Send(new ExportCompletedMessage(false, null, "没有镜头可导出到 CapCut。请先创建分镜。"));
                return;
            }

            // 获取已完成的镜头
            var completedShots = shots
                .Where(s => !string.IsNullOrWhiteSpace(s.GeneratedVideoPath) && File.Exists(s.GeneratedVideoPath))
                .ToList();

            if (completedShots.Count == 0)
            {
                _logger.LogWarning("没有已生成的视频可导出");
                _messenger.Send(new ExportCompletedMessage(false, null, "没有已生成的视频可导出到 CapCut。请先生成视频片段。"));
                return;
            }

            // 获取项目名称
            var projectQuery = new GetProjectInfoQuery();
            _messenger.Send(projectQuery);
            var projectName = projectQuery.ProjectInfo?.Name ?? "Untitled";

            // 创建导出任务
            _jobQueue.Enqueue(
                GenerationJobType.FullRender,
                0,
                async (ct, progress) =>
                {
                    try
                    {
                        var draftDirectory = await _capCutExportService.ExportToCapCutAsync(
                            completedShots,
                            outputDirectory,
                            projectName,
                            ct);

                        if (!string.IsNullOrWhiteSpace(draftDirectory))
                        {
                            _messenger.Send(new ExportCompletedMessage(true, draftDirectory));
                            _logger.LogInformation("CapCut 草稿导出成功: {DraftDirectory}", draftDirectory);

                            // 打开文件所在文件夹
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "explorer.exe",
                                    Arguments = $"\"{draftDirectory}\"",
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        }
                        else
                        {
                            _messenger.Send(new ExportCompletedMessage(false, null, "CapCut 草稿导出失败：返回路径为空。"));
                            _logger.LogWarning("CapCut 草稿导出失败: 返回路径为空");
                        }
                    }
                    catch (Exception ex)
                    {
                        var errorMsg = ex.Message;
                        if (string.IsNullOrWhiteSpace(errorMsg))
                            errorMsg = "发生未知错误";

                        _messenger.Send(new ExportCompletedMessage(false, null, $"CapCut 草稿导出失败：{errorMsg}"));
                        _logger.LogError(ex, "CapCut 草稿导出失败");
                        throw;
                    }
                });

            IsExportDialogOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CapCut 草稿导出异常");
            _messenger.Send(new ExportCompletedMessage(false, null, $"CapCut 草稿导出异常：{ex.Message}"));
        }
    }

    private void UpdateCanExportVideo()
    {
        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            CanExportVideo = false;
            return;
        }

        // 只要有至少一个镜头有生成的视频就可以导出
        var hasAnyVideo = shots.Any(s =>
            !string.IsNullOrWhiteSpace(s.GeneratedVideoPath) &&
            File.Exists(s.GeneratedVideoPath));

        CanExportVideo = hasAnyVideo;
    }

    private void OnExportJobPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is GenerationJob job && job.Type == GenerationJobType.FullRender)
        {
            // 当状态、进度或错误信息变化时更新UI
            if (e.PropertyName is nameof(GenerationJob.Status) or
                nameof(GenerationJob.Progress) or
                nameof(GenerationJob.Error))
            {
                UpdateExportProgress(job);
            }
        }
    }

    private void UpdateExportProgress(GenerationJob job)
    {
        if (job.Type != GenerationJobType.FullRender)
            return;

        string statusMsg = job.Status switch
        {
            GenerationJobStatus.Queued => "视频导出：等待处理...",
            GenerationJobStatus.Running => $"视频导出：正在合成... {job.Progress:P0}",
            GenerationJobStatus.Retrying => $"视频导出：重试中... (尝试 {job.Attempt}/{job.MaxAttempts})",
            GenerationJobStatus.Succeeded => "视频导出：合成完成",
            GenerationJobStatus.Failed => $"视频导出失败：{job.Error}",
            GenerationJobStatus.Canceled => "视频导出：已取消",
            _ => "视频导出：未知状态"
        };

        // 发送状态更新（使用特殊的消息格式）
        if (job.Status == GenerationJobStatus.Running || job.Status == GenerationJobStatus.Queued || job.Status == GenerationJobStatus.Retrying)
        {
            _messenger.Send(new ExportCompletedMessage(true, null, statusMsg));
        }
    }
}
