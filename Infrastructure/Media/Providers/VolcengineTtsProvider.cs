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

public sealed class VolcengineTtsProvider : ITtsProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<VolcengineTtsProvider> _logger;

    private const string DefaultEndpoint = "https://ark.cn-beijing.volces.com/api/v3";

    public VolcengineTtsProvider(
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<VolcengineTtsProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.Volcengine;
    private VolcengineTtsConfig TtsConfig => _configMonitor.CurrentValue.Tts.Volcengine;

    public TtsProviderType ProviderType => TtsProviderType.Volcengine;
    public string DisplayName => "火山引擎 (Volcengine)";

    public bool IsConfigured =>
        ProviderConfig.Enabled &&
        !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "doubao-e2-audio-160k",
        "doubao-e2-audio-160k-2",
        "doubao-e2-audio-32k"
    };

    public IReadOnlyList<string> SupportedVoices => new[]
    {
        "zh_female_vv_yingjian_soungis",
        "zh_male_vv_yingjian_soungis",
        "zh_female_vv_shengcheng_jingying",
        "zh_male_vv_shengcheng_jingying",
        "zh_female_vv_shichang_jingying",
        "zh_male_vv_shichang_jingying",
        "zh_female_vv_xiaoyuan_jingying",
        "zh_male_vv_xiaoyuan_jingying",
        "zh_female_vv_badao_jingying",
        "zh_male_vv_badao_jingying",
        "zh_female_vv_changjiang_jingying",
        "zh_male_vv_changjiang_jingying",
        "zh_female_vv_zhongjiao_jingying",
        "zh_male_vv_zhongjiao_jingying",
        "zh_female_vv_yujie_jingying",
        "zh_male_vv_yujie_jingying"
    };

    public IReadOnlyList<string> SupportedFormats => new[]
    {
        "mp3",
        "wav",
        "pcm"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.TextToSpeech, "火山引擎豆包 TTS API", "audio/mpeg")
    };

    public async Task<TtsGenerationResult> GenerateAsync(
        TtsGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Volcengine TTS is not configured.");

        if (string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException("TTS text is empty.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Tts
            : request.Model;
        if (string.IsNullOrWhiteSpace(model) || model == "volcengine-tts")
            model = "doubao-e2-audio-160k";

        var voice = string.IsNullOrWhiteSpace(request.Voice)
            ? TtsConfig.Voice
            : request.Voice;

        var speed = request.Speed > 0 ? request.Speed : TtsConfig.Speed;
        speed = Math.Clamp(speed, 0.1, 2.0);

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

        using var httpClient = CreateHttpClient();
        _logger.LogInformation("Volcengine TTS request: Model={Model}, Voice={Voice}, Speed={Speed}, Format={Format}, TextLength={Length}, BaseAddress={BaseAddress}",
            model, voice, speed, responseFormat, request.Text.Length, httpClient.BaseAddress);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "audio/speech")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Volcengine TTS HTTP error {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Volcengine TTS failed with HTTP {response.StatusCode}: {errorBody}");
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var extension = ResolveExtension(responseFormat);

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            var directory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(request.OutputPath, audioBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Volcengine TTS audio saved to {Path}, Size={Size} bytes", request.OutputPath, audioBytes.Length);
        }

        return new TtsGenerationResult(audioBytes, extension, model, EstimateDuration(request.Text, speed));
    }

    private HttpClient CreateHttpClient()
    {
        var baseAddress = ProviderConfig.Endpoint;
        if (string.IsNullOrWhiteSpace(baseAddress))
            baseAddress = DefaultEndpoint;

        baseAddress = baseAddress.TrimEnd('/');

        // Ensure /api/v3 suffix for ARK platform
        if (!baseAddress.Contains("/v3") && !baseAddress.Contains("/v1"))
            baseAddress += "/api/v3";

        var client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress + "/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(60, ProviderConfig.TimeoutSeconds))
        };

        // ARK platform uses standard Bearer auth (space, not semicolon)
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        return client;
    }

    private static string ResolveExtension(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mp3" => ".mp3",
            "wav" => ".wav",
            "pcm" => ".pcm",
            _ => ".mp3"
        };
    }

    private static double EstimateDuration(string text, double speed)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var charCount = text.Length;
        var baseDuration = charCount / 3.5;
        return baseDuration / speed;
    }
}
