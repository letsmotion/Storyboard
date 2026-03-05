using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Services;

public sealed class TtsService : ITtsService
{
    private readonly IEnumerable<ITtsProvider> _providers;
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<TtsService> _logger;
    private readonly IProjectStore _projectStore;

    public TtsService(
        IEnumerable<ITtsProvider> providers,
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<TtsService> logger,
        IProjectStore projectStore)
    {
        _providers = providers;
        _configMonitor = configMonitor;
        _logger = logger;
        _projectStore = projectStore;
    }

    public async Task<Infrastructure.Media.TtsGenerationResult> GenerateAsync(
        string text,
        string? model = null,
        string? voice = null,
        double speed = 1.0,
        string? responseFormat = null,
        string? outputPath = null,
        TtsProviderType? providerType = null,
        CancellationToken cancellationToken = default)
    {
        var provider = providerType.HasValue
            ? GetProvider(providerType.Value)
            : GetDefaultProvider();

        if (provider == null)
            throw new InvalidOperationException($"TTS provider {providerType} is not available.");

        var request = new Infrastructure.Media.TtsGenerationRequest(
            text,
            model ?? string.Empty,
            voice ?? string.Empty,
            speed,
            responseFormat ?? "mp3",
            outputPath);

        var result = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        return new TtsGenerationResult(
            result.AudioBytes,
            result.FileExtension,
            result.ModelUsed,
            result.DurationSeconds);
    }

    public async Task<string> GenerateForShotAsync(
        long shotId,
        string text,
        string? voice = null,
        double speed = 1.0,
        TtsProviderType? providerType = null,
        CancellationToken cancellationToken = default)
    {
        var currentProject = _projectStore.CurrentProject;
        if (currentProject == null)
            throw new InvalidOperationException("No project is currently loaded.");

        // 生成输出路径
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Storyboard",
            "output",
            "projects",
            currentProject.Id,
            "audio");

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var outputPath = Path.Combine(outputDir, $"shot_{shotId}_audio.mp3");

        _logger.LogInformation("Generating TTS audio for shot {ShotId}: {OutputPath}", shotId, outputPath);

        var result = await GenerateAsync(
            text,
            null,
            voice,
            speed,
            "mp3",
            outputPath,
            providerType,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("TTS audio generated for shot {ShotId}: {Size} bytes, {Duration:F2}s",
            shotId, result.AudioBytes.Length, result.DurationSeconds);

        return outputPath;
    }

    public async Task<Dictionary<long, string>> GenerateBatchAsync(
        Dictionary<long, string> shotTexts,
        string? voice = null,
        double speed = 1.0,
        TtsProviderType? providerType = null,
        IProgress<(int Current, int Total, long ShotId)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<long, string>();
        var total = shotTexts.Count;
        var current = 0;

        foreach (var (shotId, text) in shotTexts)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var audioPath = await GenerateForShotAsync(
                    shotId,
                    text,
                    voice,
                    speed,
                    providerType,
                    cancellationToken).ConfigureAwait(false);

                results[shotId] = audioPath;
                current++;
                progress?.Report((current, total, shotId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate TTS audio for shot {ShotId}", shotId);
                // 继续处理其他镜头
            }
        }

        return results;
    }

    public IReadOnlyList<ITtsProvider> GetAvailableProviders()
    {
        return _providers.Where(p => p.IsConfigured).ToList();
    }

    public ITtsProvider? GetProvider(TtsProviderType providerType)
    {
        return _providers.FirstOrDefault(p => p.ProviderType == providerType && p.IsConfigured);
    }

    public ITtsProvider GetDefaultProvider()
    {
        var defaultType = _configMonitor.CurrentValue.Tts.DefaultProvider;
        var provider = GetProvider(defaultType);

        if (provider != null)
            return provider;

        // 如果默认提供商不可用，返回第一个可用的
        provider = GetAvailableProviders().FirstOrDefault();
        if (provider != null)
            return provider;

        throw new InvalidOperationException("No TTS provider is configured.");
    }
}
