using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Application.Abstractions;

/// <summary>
/// TTS 语音合成服务接口
/// </summary>
public interface ITtsService
{
    /// <summary>
    /// 生成语音
    /// </summary>
    Task<TtsGenerationResult> GenerateAsync(
        string text,
        string? model = null,
        string? voice = null,
        double speed = 1.0,
        string? responseFormat = null,
        string? outputPath = null,
        TtsProviderType? providerType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 为镜头生成配音
    /// </summary>
    Task<string> GenerateForShotAsync(
        long shotId,
        string text,
        string? voice = null,
        double speed = 1.0,
        TtsProviderType? providerType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 批量为镜头生成配音
    /// </summary>
    Task<Dictionary<long, string>> GenerateBatchAsync(
        Dictionary<long, string> shotTexts,
        string? voice = null,
        double speed = 1.0,
        TtsProviderType? providerType = null,
        IProgress<(int Current, int Total, long ShotId)>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取可用的提供商
    /// </summary>
    IReadOnlyList<ITtsProvider> GetAvailableProviders();

    /// <summary>
    /// 获取指定提供商
    /// </summary>
    ITtsProvider? GetProvider(TtsProviderType providerType);

    /// <summary>
    /// 获取默认提供商
    /// </summary>
    ITtsProvider GetDefaultProvider();
}
