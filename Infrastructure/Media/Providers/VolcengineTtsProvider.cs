using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<VolcengineTtsProvider> _logger;

    private const string DefaultEndpoint = "https://openspeech.bytedance.com/api/v1";

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
    public string DisplayName => "Volcengine";

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
        new ProviderCapabilityDeclaration(AIProviderCapability.TextToSpeech, "Volcengine TTS API", "audio/mpeg")
    };

    public async Task<TtsGenerationResult> GenerateAsync(
        TtsGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Volcengine TTS is not configured.");

        if (string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException("TTS text is empty.");

        if (string.IsNullOrWhiteSpace(TtsConfig.AppId))
            throw new InvalidOperationException("Volcengine TTS requires Tts.Volcengine.AppId in configuration.");

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

        var cluster = ResolveCluster();
        var payload = new Dictionary<string, object?>
        {
            ["app"] = new Dictionary<string, object?>
            {
                ["appid"] = TtsConfig.AppId,
                ["token"] = ProviderConfig.ApiKey,
                ["cluster"] = cluster
            },
            ["user"] = new Dictionary<string, object?>
            {
                ["uid"] = "storyboard"
            },
            ["audio"] = new Dictionary<string, object?>
            {
                ["voice_type"] = voice,
                ["encoding"] = ResolveEncoding(responseFormat),
                ["rate"] = ResolveSampleRate(responseFormat),
                ["speed_ratio"] = speed
            },
            ["request"] = new Dictionary<string, object?>
            {
                ["reqid"] = Guid.NewGuid().ToString("D"),
                ["text"] = request.Text,
                ["text_type"] = "plain",
                ["operation"] = "query"
            }
        };

        using var httpClient = CreateHttpClient();
        _logger.LogInformation(
            "Volcengine TTS request: Model={Model}, Voice={Voice}, Speed={Speed}, Format={Format}, Cluster={Cluster}, TextLength={Length}, BaseAddress={BaseAddress}",
            model,
            voice,
            speed,
            responseFormat,
            cluster,
            request.Text.Length,
            httpClient.BaseAddress);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "tts")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Volcengine TTS HTTP error {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Volcengine TTS failed with HTTP {response.StatusCode}: {errorBody}");
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var apiResponse = JsonSerializer.Deserialize<VolcengineTtsResponse>(responseBody, JsonOptions);
        if (apiResponse == null)
            throw new InvalidOperationException("Volcengine TTS returned an empty response.");

        if (apiResponse.Code != 3000)
        {
            _logger.LogError("Volcengine TTS API error {Code}: {Message}", apiResponse.Code, apiResponse.Message);
            throw new InvalidOperationException($"Volcengine TTS failed with code {apiResponse.Code}: {apiResponse.Message}");
        }

        if (string.IsNullOrWhiteSpace(apiResponse.Data))
            throw new InvalidOperationException("Volcengine TTS returned empty audio data.");

        var audioBytes = Convert.FromBase64String(apiResponse.Data);
        var extension = ResolveExtension(responseFormat);

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            var directory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(request.OutputPath, audioBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Volcengine TTS audio saved to {Path}, Size={Size} bytes", request.OutputPath, audioBytes.Length);
        }

        var duration = TryResolveDurationSeconds(apiResponse.Addition?.Duration) ?? EstimateDuration(request.Text, speed);
        return new TtsGenerationResult(audioBytes, extension, model, duration);
    }

    private HttpClient CreateHttpClient()
    {
        var baseAddress = ProviderConfig.Endpoint;
        if (string.IsNullOrWhiteSpace(baseAddress) ||
            baseAddress.Contains("ark.", StringComparison.OrdinalIgnoreCase) ||
            baseAddress.Contains("volces.com", StringComparison.OrdinalIgnoreCase))
        {
            baseAddress = DefaultEndpoint;
        }

        baseAddress = baseAddress.TrimEnd('/');

        if (!baseAddress.Contains("/api/v1", StringComparison.OrdinalIgnoreCase))
            baseAddress += "/api/v1";

        var client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress + "/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(60, ProviderConfig.TimeoutSeconds))
        };

        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer; {ProviderConfig.ApiKey}");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");

        return client;
    }

    private string ResolveCluster()
    {
        return string.IsNullOrWhiteSpace(TtsConfig.Cluster) ? "volcano_tts" : TtsConfig.Cluster;
    }

    private static string ResolveEncoding(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mp3" => "mp3",
            "wav" => "wav",
            "pcm" => "pcm",
            "opus" => "ogg_opus",
            _ => "mp3"
        };
    }

    private static int ResolveSampleRate(string format)
    {
        return format.Equals("pcm", StringComparison.OrdinalIgnoreCase) ? 16000 : 24000;
    }

    private static string ResolveExtension(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mp3" => ".mp3",
            "wav" => ".wav",
            "pcm" => ".pcm",
            "opus" => ".opus",
            _ => ".mp3"
        };
    }

    private static double? TryResolveDurationSeconds(string? durationMilliseconds)
    {
        if (!double.TryParse(durationMilliseconds, out var milliseconds))
            return null;

        return milliseconds / 1000d;
    }

    private static double EstimateDuration(string text, double speed)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var charCount = text.Length;
        var baseDuration = charCount / 3.5;
        return baseDuration / speed;
    }

    private sealed class VolcengineTtsResponse
    {
        public int Code { get; set; }
        public string? Message { get; set; }
        public string? Data { get; set; }
        public VolcengineTtsAddition? Addition { get; set; }
    }

    private sealed class VolcengineTtsAddition
    {
        public string? Duration { get; set; }
    }
}
