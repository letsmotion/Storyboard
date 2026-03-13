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

public sealed class QwenTtsProvider : ITtsProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<QwenTtsProvider> _logger;

    private const string DefaultEndpoint = "https://dashscope.aliyuncs.com/api/v1";

    public QwenTtsProvider(
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<QwenTtsProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.Qwen;
    private QwenTtsConfig TtsConfig => _configMonitor.CurrentValue.Tts.Qwen;

    public TtsProviderType ProviderType => TtsProviderType.Qwen;
    public string DisplayName => "千问 (Qwen)";

    public bool IsConfigured =>
        ProviderConfig.Enabled &&
        !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen-tts",
        "qwen-tts-latest",
        "qwen3-tts-flash"
    };

    public IReadOnlyList<string> SupportedVoices => new[]
    {
        "alexa",
        "arwen",
        "bethany",
        "daniel",
        "donna",
        "elisabeth",
        "emily",
        "emma",
        "erika",
        "gabriel",
        "geralt",
        "giulia",
        "hani",
        "heather",
        "helen",
        "jacob",
        "jessica",
        "jiaxi",
        "jinli",
        "julie",
        "kanying",
        "lily",
        "lucas",
        "marc",
        "maria",
        "mason",
        "meng",
        "michael",
        "mila",
        "ray",
        "rachel",
        "richard",
        "riley",
        "rose",
        "sarah",
        "seth",
        "shawn",
        "sophia",
        "stefan",
        "stella",
        "summer",
        "taylor",
        "thomas",
        "tom",
        "xiaobing",
        "xiaoxiao",
        "xiaoyi",
        "yating",
        "yunjian",
        "yunxi",
        "yunxia",
        "yunyang",
        "zhenda",
        "zhuoming"
    };

    public IReadOnlyList<string> SupportedFormats => new[]
    {
        "mp3",
        "wav",
        "opus"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.TextToSpeech, "阿里云 DashScope TTS API", "audio/mpeg")
    };

    public async Task<TtsGenerationResult> GenerateAsync(
        TtsGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Qwen TTS is not configured.");

        if (string.IsNullOrWhiteSpace(request.Text))
            throw new InvalidOperationException("TTS text is empty.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Tts
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "qwen-tts";
        }

        // Normalize legacy/incorrect model names
        model = NormalizeModelName(model);

        var voice = string.IsNullOrWhiteSpace(request.Voice)
            ? TtsConfig.Voice
            : request.Voice;

        var speed = request.Speed > 0 ? request.Speed : TtsConfig.Speed;
        speed = Math.Clamp(speed, 0.5, 2.0);

        var responseFormat = string.IsNullOrWhiteSpace(request.ResponseFormat)
            ? TtsConfig.ResponseFormat
            : request.ResponseFormat;

        var audioBytes = await GenerateViaNativeApiAsync(model, voice, speed, responseFormat, request.Text, cancellationToken).ConfigureAwait(false);

        var extension = ResolveExtension(responseFormat);

        if (!string.IsNullOrWhiteSpace(request.OutputPath))
        {
            var directory = Path.GetDirectoryName(request.OutputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            await File.WriteAllBytesAsync(request.OutputPath, audioBytes, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Qwen TTS audio saved to {Path}, Size={Size} bytes", request.OutputPath, audioBytes.Length);
        }

        var estimatedDuration = EstimateDuration(request.Text, speed);
        return new TtsGenerationResult(audioBytes, extension, model, estimatedDuration);
    }

    // Map old/incorrect model names to their correct DashScope identifiers
    private static string NormalizeModelName(string model) => model.ToLowerInvariant() switch
    {
        var m when m.StartsWith("qwen3-tts-instruct") => "qwen3-tts-flash",
        _ => model
    };

    private async Task<byte[]> GenerateViaNativeApiAsync(
        string model, string voice, double speed, string responseFormat, string text,
        CancellationToken cancellationToken)
    {
        var input = new Dictionary<string, object>
        {
            ["text"] = text,
            ["voice"] = voice
        };

        if (!string.IsNullOrWhiteSpace(TtsConfig.LanguageType))
            input["language_type"] = TtsConfig.LanguageType;

        var payload = new Dictionary<string, object>
        {
            ["model"] = model,
            ["input"] = input,
            ["parameters"] = new Dictionary<string, object>
            {
                ["speed"] = speed,
                ["response_format"] = responseFormat,
                ["stream"] = false
            }
        };

        using var httpClient = CreateHttpClient();
        // qwen3-tts-flash uses the multimodal-generation endpoint; older qwen-tts uses text2audio
        var requestPath = model.StartsWith("qwen3-", StringComparison.OrdinalIgnoreCase)
            ? "services/aigc/multimodal-generation/generation"
            : "services/aigc/text2audio/generation";
        var requestJson = JsonSerializer.Serialize(payload);
        _logger.LogInformation("Qwen TTS (native) request: URL={Url}, Body={Body}", httpClient.BaseAddress + requestPath, requestJson);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, requestPath)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await ReadErrorBodyAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError("Qwen TTS generation failed with status {StatusCode}: {Body}", response.StatusCode, errorBody);
            throw new InvalidOperationException($"Qwen TTS generation failed with status {response.StatusCode}: {errorBody}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Qwen TTS response: {Response}", responseContent);

        var audioUrl = ExtractAudioUrl(responseContent);
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            _logger.LogError("Qwen TTS response does not contain audio URL: {Body}", responseContent);
            throw new InvalidOperationException("Qwen TTS response does not contain audio URL.");
        }

        return await DownloadAudioAsync(audioUrl, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> ReadErrorBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try { return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception ex) { return $"Failed to read error body: {ex.Message}"; }
    }

    private HttpClient CreateHttpClient()
    {
        // TTS API is only available on the DashScope native API base, not the compatible-mode base.
        // The shared Qwen endpoint config points to compatible-mode/v1 (for chat/text),
        // so we always use the native API base for TTS.
        var baseAddress = ProviderConfig.Endpoint;
        if (string.IsNullOrWhiteSpace(baseAddress) || baseAddress.Contains("compatible-mode", StringComparison.OrdinalIgnoreCase))
        {
            baseAddress = DefaultEndpoint;
        }

        baseAddress = baseAddress.TrimEnd('/');

        _logger.LogInformation("Qwen TTS config: Endpoint={Endpoint}, ApiKeyConfigured={HasApiKey}", 
            baseAddress, !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey));

        var client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress + "/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(60, ProviderConfig.TimeoutSeconds))
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string ExtractAudioUrl(string responseJson)
    {
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        if (root.TryGetProperty("output", out var output) &&
            output.TryGetProperty("audio", out var audio) &&
            audio.TryGetProperty("url", out var urlElement))
        {
            return urlElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task<byte[]> DownloadAudioAsync(string url, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to download audio from {url}: {response.StatusCode}");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveExtension(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "mp3" => ".mp3",
            "wav" => ".wav",
            "opus" => ".opus",
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
