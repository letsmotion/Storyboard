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

    private const string DefaultEndpoint = "https://openspeech.bytedance.com";

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
        "zh_male_cuishi_0001",
        "zh_female_guanting_001",
        "zh_male_jingying_001",
        "zh_female_shaonv_001",
        "zh_male_badao_001",
        "zh_female_tianmei_001",
        "zh_male_daxiong_001",
        "zh_female_yujie_001",
        "zh_male_chengshu_001",
        "zh_female_aona_001",
        "zh_male_bohiren_001",
        "zh_female_aoyun_001",
        "zh_male_yunifeng_001",
        "zh_female_xiaochao_001",
        "zh_male_xunlei_001",
        "zh_female_zhitong_001",
        "zh_male_xiaobing_001",
        "zh_female_ailu_001",
        "zh_male_dahuang_001",
        "zh_female_jinglun_001",
        "zh_male_yue_001",
        "zh_female_bushuo_001",
        "zh_male_chengdu_001",
        "zh_female_yina_001",
        "zh_male_xiaozhoucheng_001",
        "zh_female_zhangli_001",
        "zh_male_wangwang_001",
        "zh_female_yuebai_001",
        "zh_male_zhenzhuren_001",
        "zh_female_xiongan_001",
        "zh_male_liuhe_001",
        "zh_female_xiaoyuan_001",
        "zh_male_xiaohei_001",
        "zh_female_xiaoxuan_001",
        "zh_male_yaoya_001",
        "zh_female_zhili_001",
        "zh_male_shenmimao_001",
        "zh_female_luolita_001",
        "zh_male_xiaoxiong_001"
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
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "doubao-e2-audio-160k";
        }

        var voice = string.IsNullOrWhiteSpace(request.Voice)
            ? TtsConfig.Voice
            : request.Voice;

        var speed = request.Speed > 0 ? request.Speed : TtsConfig.Speed;
        speed = Math.Clamp(speed, 0.5, 2.0);

        var responseFormat = string.IsNullOrWhiteSpace(request.ResponseFormat)
            ? TtsConfig.ResponseFormat
            : request.ResponseFormat;

        var appId = TtsConfig.AppId ?? string.Empty;

        var payload = new Dictionary<string, object>
        {
            ["app"] = new Dictionary<string, object>
            {
                ["appid"] = appId,
                ["token"] = ProviderConfig.ApiKey,
                ["cluster"] = "volcengine_streaming_common"
            },
            ["user"] = new Dictionary<string, string>
            {
                ["uid"] = Guid.NewGuid().ToString()
            },
            ["audio"] = new Dictionary<string, object>
            {
                ["format"] = responseFormat,
                ["rate"] = 24000,
                ["bits"] = 16,
                ["channel"] = 1,
                ["codec"] = "raw"
            },
            ["request"] = new Dictionary<string, object>
            {
                ["reqid"] = Guid.NewGuid().ToString(),
                ["sequence"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["text"] = request.Text,
                        ["voice"] = voice,
                        ["speed"] = (int)(speed * 100),
                        ["vol"] = 100,
                        ["pitch"] = 0,
                        ["audio_type"] = 100
                    }
                }
            }
        };

        using var httpClient = CreateHttpClient();
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "api/v3/tts")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        _logger.LogInformation("Generating Volcengine TTS audio: Model={Model}, Voice={Voice}, Speed={Speed}, Format={Format}, TextLength={Length}",
            model, voice, speed, responseFormat, request.Text.Length);

        var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError("Volcengine TTS generation failed with status {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Volcengine TTS generation failed with status {response.StatusCode}: {errorBody}");
        }

        var audioBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

        if (audioBytes.Length > 0 && audioBytes[0] == '{')
        {
            var errorBody = Encoding.UTF8.GetString(audioBytes);
            _logger.LogError("Volcengine TTS returned error: {Body}", errorBody);
            throw new InvalidOperationException($"Volcengine TTS returned error: {errorBody}");
        }

        var extension = ResolveExtension(responseFormat);

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            var directory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(request.OutputPath, audioBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Volcengine TTS audio saved to {Path}, Size={Size} bytes", request.OutputPath, audioBytes.Length);
        }

        var estimatedDuration = EstimateDuration(request.Text, speed);

        return new TtsGenerationResult(audioBytes, extension, model, estimatedDuration);
    }

    private HttpClient CreateHttpClient()
    {
        var baseAddress = ProviderConfig.Endpoint;
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            baseAddress = DefaultEndpoint;
        }

        baseAddress = baseAddress.TrimEnd('/');

        var client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress + "/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(60, ProviderConfig.TimeoutSeconds))
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json", 1.0));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*", 0.8));

        if (!string.IsNullOrWhiteSpace(TtsConfig.AppId))
        {
            client.DefaultRequestHeaders.Add("X-Api-App-Id", TtsConfig.AppId);
        }

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
