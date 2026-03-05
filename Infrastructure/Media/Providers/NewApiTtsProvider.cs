using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class NewApiTtsProvider : ITtsProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<NewApiTtsProvider> _logger;

    public NewApiTtsProvider(
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<NewApiTtsProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.NewApi;
    private NewApiTtsConfig TtsConfig => _configMonitor.CurrentValue.Tts.NewApi;

    public TtsProviderType ProviderType => TtsProviderType.NewApi;
    public string DisplayName => "New API";

    public bool IsConfigured =>
        ProviderConfig.Enabled &&
        !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey) &&
        !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "tts-1",
        "tts-1-hd"
    };

    public IReadOnlyList<string> SupportedVoices => new[]
    {
        "alloy",
        "echo",
        "fable",
        "onyx",
        "nova",
        "shimmer"
    };

    public IReadOnlyList<string> SupportedFormats => new[]
    {
        "mp3",
        "opus",
        "aac",
        "flac",
        "wav",
        "pcm"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.TextToSpeech, "OpenAI-compatible /v1/audio/speech", "audio/mpeg")
    };

    public async Task<TtsGenerationResult> GenerateAsync(
        TtsGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("NewApi TTS is not configured.");
        if (string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException("TTS text is empty.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Tts
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "tts-1"; // 默认模型
        }

        var voice = string.IsNullOrWhiteSpace(request.Voice)
            ? TtsConfig.Voice
            : request.Voice;

        var speed = request.Speed > 0 ? request.Speed : TtsConfig.Speed;
        speed = Math.Clamp(speed, 0.25, 4.0);

        var responseFormat = string.IsNullOrWhiteSpace(request.ResponseFormat)
            ? TtsConfig.ResponseFormat
            : request.ResponseFormat;

        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["input"] = request.Text,
            ["voice"] = voice,
            ["speed"] = speed,
            ["response_format"] = responseFormat
        };

        if (!string.IsNullOrWhiteSpace(TtsConfig.ProviderHint))
        {
            payload["provider"] = TtsConfig.ProviderHint!.Trim();
        }

        using var httpClient = CreateHttpClient();
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "audio/speech")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        _logger.LogInformation("Generating TTS audio: Model={Model}, Voice={Voice}, Speed={Speed}, Format={Format}, TextLength={Length}",
            model, voice, speed, responseFormat, request.Text.Length);

        var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("NewApi TTS generation failed with status {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"NewApi TTS generation failed with status {response.StatusCode}: {errorBody}");
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var extension = ResolveExtension(responseFormat);

        // 如果指定了输出路径，保存文件
        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            var directory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(request.OutputPath, audioBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("TTS audio saved to {Path}, Size={Size} bytes", request.OutputPath, audioBytes.Length);
        }

        // 估算音频时长（粗略估算：中文约 3-4 字/秒，英文约 2-3 词/秒）
        var estimatedDuration = EstimateDuration(request.Text, speed);

        return new TtsGenerationResult(audioBytes, extension, model, estimatedDuration);
    }

    private HttpClient CreateHttpClient()
    {
        var baseAddress = ProviderConfig.Endpoint?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new InvalidOperationException("Endpoint is required for NewApi TTS.");

        // Ensure endpoint includes /v1 for OpenAI-compatible API
        if (!baseAddress.Contains("/v1"))
        {
            baseAddress = baseAddress + "/v1";
        }

        var client = new HttpClient
        {
            BaseAddress = new Uri($"{baseAddress}/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(60, ProviderConfig.TimeoutSeconds))
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        return client;
    }

    private static string ResolveExtension(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mp3" => ".mp3",
            "opus" => ".opus",
            "aac" => ".aac",
            "flac" => ".flac",
            "wav" => ".wav",
            "pcm" => ".pcm",
            _ => ".mp3"
        };
    }

    private static double EstimateDuration(string text, double speed)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // 粗略估算：中文约 3.5 字/秒，英文约 2.5 词/秒
        // 这里简化处理，按字符数估算
        var charCount = text.Length;
        var baseDuration = charCount / 3.5; // 假设平均 3.5 字/秒
        return baseDuration / speed; // 考虑语速
    }
}
