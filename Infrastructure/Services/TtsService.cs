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

    public TtsService(
        IEnumerable<ITtsProvider> providers,
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<TtsService> logger)
    {
        _providers = providers;
        _configMonitor = configMonitor;
        _logger = logger;
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
        var effectiveProviderType = providerType ?? _configMonitor.CurrentValue.Tts.DefaultProvider;
        var provider = providerType.HasValue
            ? GetProvider(providerType.Value)
            : GetDefaultProvider();

        if (provider == null)
        {
            _logger.LogError("TTS provider {ProviderType} is not available", providerType);
            throw new InvalidOperationException($"TTS provider {providerType} is not available.");
        }

        var effectiveModel = model;
        if (string.IsNullOrWhiteSpace(effectiveModel))
        {
            effectiveModel = _configMonitor.CurrentValue.Defaults.Tts.Model;
            _logger.LogInformation("Using default TTS model from config: {Model}", effectiveModel ?? "none");
        }

        var effectiveVoice = voice;
        if (string.IsNullOrWhiteSpace(effectiveVoice))
        {
            effectiveVoice = GetDefaultVoiceForProvider(effectiveProviderType);
            _logger.LogInformation("Using default voice for provider {Provider}: {Voice}", effectiveProviderType, effectiveVoice);
        }

        var request = new Infrastructure.Media.TtsGenerationRequest(
            text,
            effectiveModel ?? string.Empty,
            effectiveVoice,
            speed,
            responseFormat ?? "mp3",
            outputPath);

        try
        {
            _logger.LogInformation("Starting TTS generation: Provider={Provider}, Model={Model}, Voice={Voice}, TextLength={TextLength}",
                provider.DisplayName, request.Model, request.Voice, text.Length);

            var result = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("TTS generation succeeded: Provider={Provider}, Model={Model}, Size={Size} bytes",
                provider.DisplayName, result.ModelUsed, result.AudioBytes.Length);

            return new TtsGenerationResult(
                result.AudioBytes,
                result.FileExtension,
                result.ModelUsed,
                result.DurationSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS generation failed: Provider={Provider}, Model={Model}, Voice={Voice}, TextLength={TextLength}",
                provider.DisplayName, request.Model, request.Voice, text.Length);
            throw;
        }
    }

    private static string GetDefaultVoiceForProvider(TtsProviderType providerType)
    {
        return providerType switch
        {
            TtsProviderType.Qwen => "alexa",
            TtsProviderType.Volcengine => "zh_female_vv_yingjian_soungis",
            TtsProviderType.NewApi => "alloy",
            _ => "alloy"
        };
    }

    public async Task<string> GenerateForShotAsync(
        long shotId,
        string text,
        string? model = null,
        string? voice = null,
        double speed = 1.0,
        TtsProviderType? providerType = null,
        CancellationToken cancellationToken = default)
    {
        // 生成输出路径
        var outputDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Storyboard",
            "output",
            "audio");

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        var outputPath = Path.Combine(outputDir, $"shot_{shotId}_audio.mp3");

        _logger.LogInformation("Generating TTS audio for shot {ShotId}: {OutputPath}, Model={Model}", shotId, outputPath, model ?? "default");

        var result = await GenerateAsync(
            text,
            model,
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
                    model: null,
                    voice: voice,
                    speed: speed,
                    providerType: providerType,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

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
