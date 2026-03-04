using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// AI 分析 ViewModel - 负责 AI 解析镜头
/// </summary>
public partial class AiAnalysisViewModel : ObservableObject
{
    private readonly IAiShotService _aiShotService;
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;
    private readonly ILogger<AiAnalysisViewModel> _logger;

    public AiAnalysisViewModel(
        IAiShotService aiShotService,
        IJobQueueService jobQueue,
        IMessenger messenger,
        ILogger<AiAnalysisViewModel> logger)
    {
        _aiShotService = aiShotService;
        _jobQueue = jobQueue;
        _messenger = messenger;
        _logger = logger;

        // 订阅 AI 解析请求消息
        _messenger.Register<AiParseRequestedMessage>(this, OnAiParseRequested);
    }

    [RelayCommand]
    private async Task AIAnalyzeAll()
    {
        _logger.LogInformation("开始批量 AI 分析");

        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            _logger.LogWarning("没有镜头可分析");
            return;
        }

        // 只处理勾选的镜头
        var checkedShots = shots.Where(s => s.IsChecked).ToList();

        if (checkedShots.Count == 0)
        {
            _logger.LogWarning("没有勾选的镜头可分析");
            return;
        }

        // 检查是否需要询问AI写入模式
        var needMode = checkedShots.Any(NeedsAiWriteMode);
        AiWriteMode? mode = null;

        if (needMode)
        {
            mode = await RequestAiWriteModeAsync();
            if (mode == null)
            {
                _logger.LogInformation("用户取消了批量AI分析");
                return;
            }
        }

        // 使用滑动窗口批量处理
        await AIAnalyzeAllWithSlidingWindow(checkedShots, mode);
    }

    /// <summary>
    /// 使用滑动窗口上下文进行批量AI分析
    /// 3个镜头一组并行处理，每组携带前面镜头的分析结果作为上下文
    /// </summary>
    private async Task AIAnalyzeAllWithSlidingWindow(List<ShotItem> shots, AiWriteMode? mode)
    {
        const int batchSize = 3; // 每批处理3个镜头
        const int contextSize = 3; // 携带前3个镜头的上下文

        var validShots = shots.Where(s =>
            !string.IsNullOrWhiteSpace(s.MaterialFilePath) &&
            System.IO.File.Exists(s.MaterialFilePath) &&
            !s.IsAiParsing).ToList();

        if (validShots.Count == 0)
        {
            _logger.LogWarning("没有有效的镜头可分析");
            return;
        }

        _logger.LogInformation("开始滑动窗口批量分析: {Total} 个镜头, 每批 {BatchSize} 个",
            validShots.Count, batchSize);

        // 存储已分析镜头的上下文摘要
        var contextHistory = new System.Collections.Generic.Queue<string>();

        // 分批处理
        for (int i = 0; i < validShots.Count; i += batchSize)
        {
            var batch = validShots.Skip(i).Take(batchSize).ToList();

            // 构建上下文摘要
            var contextSummary = BuildContextSummary(contextHistory);

            _logger.LogInformation("处理批次 {BatchIndex}: 镜头 {Start}-{End}, 上下文: {ContextCount} 个",
                i / batchSize + 1, i + 1, Math.Min(i + batchSize, validShots.Count), contextHistory.Count);

            // 并行处理当前批次
            var tasks = batch.Select(shot =>
                Task.Run(async () =>
                {
                    try
                    {
                        // 发送AI解析请求消息，携带上下文
                        _messenger.Send(new AiParseRequestedMessage(shot, contextSummary));

                        // 等待解析完成（通过轮询状态）
                        await WaitForAnalysisComplete(shot);

                        // 返回分析结果摘要
                        return BuildShotSummary(shot);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "分析镜头 {ShotNumber} 失败", shot.ShotNumber);
                        return null;
                    }
                })
            ).ToList();

            // 等待当前批次完成
            var results = await Task.WhenAll(tasks);

            // 更新上下文历史
            foreach (var result in results.Where(r => r != null))
            {
                contextHistory.Enqueue(result!);

                // 只保留最近N个镜头的上下文
                if (contextHistory.Count > contextSize)
                {
                    contextHistory.Dequeue();
                }
            }

            _logger.LogInformation("批次完成，当前上下文大小: {ContextSize}", contextHistory.Count);
        }

        _logger.LogInformation("滑动窗口批量分析完成");
    }

    /// <summary>
    /// 构建上下文摘要
    /// </summary>
    private string BuildContextSummary(System.Collections.Generic.Queue<string> contextHistory)
    {
        if (contextHistory.Count == 0)
        {
            return "这是第一批镜头分析，请根据项目意图进行分析。";
        }

        var summary = "前面镜头的分析结果摘要（用于保持风格连贯）:\n\n";
        summary += string.Join("\n\n", contextHistory);
        summary += "\n\n请保持与上述镜头相似的风格、基调和描述方式。";

        return summary;
    }

    /// <summary>
    /// 构建单个镜头的分析摘要
    /// </summary>
    private string BuildShotSummary(ShotItem shot)
    {
        var summary = $"镜头 {shot.ShotNumber}:\n";

        if (!string.IsNullOrWhiteSpace(shot.CoreContent))
            summary += $"- 核心内容: {shot.CoreContent.Substring(0, Math.Min(100, shot.CoreContent.Length))}\n";

        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
            summary += $"- 视频提示词: {shot.VideoPrompt.Substring(0, Math.Min(100, shot.VideoPrompt.Length))}\n";

        if (!string.IsNullOrWhiteSpace(shot.ShotType))
            summary += $"- 镜头类型: {shot.ShotType}\n";

        return summary;
    }

    /// <summary>
    /// 等待镜头分析完成
    /// </summary>
    private async Task WaitForAnalysisComplete(ShotItem shot, int maxWaitSeconds = 60)
    {
        var startTime = DateTime.Now;

        while (shot.IsAiParsing && (DateTime.Now - startTime).TotalSeconds < maxWaitSeconds)
        {
            await Task.Delay(500); // 每500ms检查一次
        }

        if (shot.IsAiParsing)
        {
            _logger.LogWarning("镜头 {ShotNumber} 分析超时", shot.ShotNumber);
        }
    }

    [RelayCommand]
    private async Task AIAnalyzeAllLegacy()
    {
        _logger.LogInformation("开始传统批量 AI 分析（无上下文）");

        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            _logger.LogWarning("没有镜头可分析");
            return;
        }

        // 只处理勾选的镜头
        var checkedShots = shots.Where(s => s.IsChecked).ToList();

        if (checkedShots.Count == 0)
        {
            _logger.LogWarning("没有勾选的镜头可分析");
            return;
        }

        // 检查是否需要询问AI写入模式
        var needMode = checkedShots.Any(NeedsAiWriteMode);
        AiWriteMode? mode = null;

        if (needMode)
        {
            mode = await RequestAiWriteModeAsync();
            if (mode == null)
            {
                _logger.LogInformation("用户取消了批量AI分析");
                return;
            }
        }

        var queuedCount = 0;
        var skippedCount = 0;

        foreach (var shot in checkedShots)
        {
            // 跳过没有素材图片的镜头
            if (string.IsNullOrWhiteSpace(shot.MaterialFilePath) || !System.IO.File.Exists(shot.MaterialFilePath))
            {
                _logger.LogInformation("跳过缺少素材的镜头: Shot {ShotNumber}", shot.ShotNumber);
                skippedCount++;
                continue;
            }

            // 跳过正在解析的镜头
            if (shot.IsAiParsing)
            {
                _logger.LogInformation("跳过正在解析的镜头: Shot {ShotNumber}", shot.ShotNumber);
                skippedCount++;
                continue;
            }

            // 发送AI解析请求消息
            _messenger.Send(new AiParseRequestedMessage(shot));
            queuedCount++;
        }

        _logger.LogInformation("批量AI分析: 已加入队列 {Queued} 个镜头, 跳过 {Skipped} 个镜头", queuedCount, skippedCount);
    }

    private static bool NeedsAiWriteMode(ShotItem shot)
    {
        return !string.IsNullOrWhiteSpace(shot.ShotType)
            || !string.IsNullOrWhiteSpace(shot.CoreContent)
            || !string.IsNullOrWhiteSpace(shot.ActionCommand)
            || !string.IsNullOrWhiteSpace(shot.SceneSettings)
            || !string.IsNullOrWhiteSpace(shot.FirstFramePrompt)
            || !string.IsNullOrWhiteSpace(shot.LastFramePrompt);
    }

    private async Task<AiWriteMode?> RequestAiWriteModeAsync()
    {
        // 确保在UI线程上执行
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            return await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(RequestAiWriteModeOnUiThreadAsync);
        }

        return await RequestAiWriteModeOnUiThreadAsync();
    }

    private async Task<AiWriteMode?> RequestAiWriteModeOnUiThreadAsync()
    {
        var lifetime = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
        var owner = lifetime?.MainWindow;
        var dialog = new Views.AiWriteModeDialog();

        if (owner == null)
            return AiWriteMode.Overwrite;

        return await dialog.ShowDialog<AiWriteMode?>(owner);
    }

    /// <summary>
    /// 根据文本提示生成分镜列表
    /// </summary>
    public async Task<IReadOnlyList<AiShotDescription>> GenerateShotsFromTextAsync(
        string prompt,
        int? shotCount = null,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null)
    {
        try
        {
            _logger.LogInformation("开始文本生成分镜");
            var result = await _aiShotService.GenerateShotsFromTextAsync(
                prompt,
                shotCount,
                creativeGoal,
                targetAudience,
                videoTone,
                keyMessage);
            _logger.LogInformation("文本生成分镜完成，生成了 {Count} 个分镜", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "文本生成分镜失败");
            throw;
        }
    }

    private async void OnAiParseRequested(object recipient, AiParseRequestedMessage message)
    {
        var shot = message.Shot;
        var contextSummary = message.ContextSummary;

        try
        {
            shot.AiParseStatusMessage = null;
            shot.IsAiParsing = true;

            if (string.IsNullOrWhiteSpace(shot.MaterialFilePath) || !File.Exists(shot.MaterialFilePath))
            {
                shot.AiParseStatusMessage = "请先关联素材图片或截图，才能进行 AI 解析。";
                shot.IsAiParsing = false;
                _messenger.Send(new AiParseCompletedMessage(shot, false));
                return;
            }

            // 创建 AI 解析任务
            _jobQueue.Enqueue(
                GenerationJobType.AiParse,
                shot.ShotNumber,
                async (ct, progress) =>
                {
                    try
                    {
                        // 创建 AI 分析请求 - 使用素材图片进行分析，携带上下文
                        var request = new AiShotAnalysisRequest(
                            MaterialImagePath: shot.MaterialFilePath,
                            ExistingShotType: shot.ShotType,
                            ExistingCoreContent: shot.CoreContent,
                            ExistingActionCommand: shot.ActionCommand,
                            ExistingSceneSettings: shot.SceneSettings,
                            ExistingFirstFramePrompt: shot.FirstFramePrompt,
                            ExistingLastFramePrompt: shot.LastFramePrompt,
                            ContextSummary: contextSummary  // 传递上下文摘要
                        );

                        // 执行 AI 解析
                        var result = await _aiShotService.AnalyzeShotAsync(request, ct);

                        // 在 UI 线程上更新结果
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (result != null)
                            {
                                // 记录 AI 解析结果（完整版）
                                _logger.LogInformation("AI 解析结果 - Shot {ShotNumber}:\n" +
                                    "  基础: ShotType={ShotType}, CoreContent={CoreContent}\n" +
                                    "  提示词: FirstFrame={FirstFrame}..., LastFrame={LastFrame}...\n" +
                                    "  图片参数: Composition={Composition}, Lighting={Lighting}, TimeOfDay={TimeOfDay}, ColorStyle={ColorStyle}, NegativePrompt={NegativePrompt}, ImageSize={ImageSize}\n" +
                                    "  视频参数: VideoPrompt={VideoPrompt}..., Scene={Scene}..., Action={Action}..., Style={Style}..., Camera={Camera}, Shooting={Shooting}, Effect={Effect}, VideoNegative={VideoNegative}, Resolution={Resolution}, Ratio={Ratio}",
                                    shot.ShotNumber,
                                    result.ShotType,
                                    result.CoreContent?.Substring(0, Math.Min(30, result.CoreContent?.Length ?? 0)),
                                    result.FirstFramePrompt?.Substring(0, Math.Min(30, result.FirstFramePrompt?.Length ?? 0)),
                                    result.LastFramePrompt?.Substring(0, Math.Min(30, result.LastFramePrompt?.Length ?? 0)),
                                    result.Composition ?? "null",
                                    result.LightingType ?? "null",
                                    result.TimeOfDay ?? "null",
                                    result.ColorStyle ?? "null",
                                    result.NegativePrompt?.Substring(0, Math.Min(20, result.NegativePrompt?.Length ?? 0)) ?? "null",
                                    result.ImageSize ?? "null",
                                    result.VideoPrompt?.Substring(0, Math.Min(30, result.VideoPrompt?.Length ?? 0)) ?? "null",
                                    result.SceneDescription?.Substring(0, Math.Min(30, result.SceneDescription?.Length ?? 0)) ?? "null",
                                    result.ActionDescription?.Substring(0, Math.Min(30, result.ActionDescription?.Length ?? 0)) ?? "null",
                                    result.StyleDescription?.Substring(0, Math.Min(30, result.StyleDescription?.Length ?? 0)) ?? "null",
                                    result.CameraMovement ?? "null",
                                    result.ShootingStyle ?? "null",
                                    result.VideoEffect ?? "null",
                                    result.VideoNegativePrompt?.Substring(0, Math.Min(20, result.VideoNegativePrompt?.Length ?? 0)) ?? "null",
                                    result.VideoResolution ?? "null",
                                    result.VideoRatio ?? "null");

                                // 使用批量更新方法，避免多次触发 PropertyChanged
                                shot.ApplyAiAnalysisResult(result);
                                shot.AiParseStatusMessage = null;

                                _messenger.Send(new AiParseCompletedMessage(shot, true));
                                _logger.LogInformation("AI 解析完成并已应用到 Shot {ShotNumber}", shot.ShotNumber);
                            }
                            else
                            {
                                _messenger.Send(new AiParseCompletedMessage(shot, false));
                                _logger.LogWarning("AI 解析失败: Shot {ShotNumber}", shot.ShotNumber);
                                shot.AiParseStatusMessage = "AI 解析未返回结果，请稍后再试。";
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "AI 解析任务执行异常: Shot {ShotNumber}", shot.ShotNumber);
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            _messenger.Send(new AiParseCompletedMessage(shot, false));
                            shot.AiParseStatusMessage = ex.Message;
                        });
                        throw;
                    }
                    finally
                    {
                        // 在 UI 线程上重置状态
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            shot.IsAiParsing = false;
                        });
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI 解析入队异常: Shot {ShotNumber}", shot.ShotNumber);
            _messenger.Send(new AiParseCompletedMessage(shot, false));
            shot.IsAiParsing = false;
            shot.AiParseStatusMessage = ex.Message;
        }
    }
}
