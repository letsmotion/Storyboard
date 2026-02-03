using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class QwenImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<QwenImageGenerationProvider> _logger;

    public QwenImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<QwenImageGenerationProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.Qwen;
    private QwenImageConfig ImageConfig => _configMonitor.CurrentValue.Image.Qwen;

    public ImageProviderType ProviderType => ImageProviderType.Qwen;
    public string DisplayName => "Qwen";
    public bool IsConfigured => ProviderConfig.Enabled
        && !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey)
        && !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "qwen-image-max",
        "qwen-image-max-2025-12-30",
        "qwen-image-plus",
        "qwen-image-plus-2026-01-09",
        "qwen-image",
        "wan2.6-t2i"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.ImageGeneration, "Size: model-specific", "image/png")
    };

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Qwen image generation is not configured.");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("Image prompt is empty.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Image
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No image model configured for Qwen.");

        var size = ResolveSize(request, model);
        var isQwenImage = IsQwenImageModel(model);
        var parameters = new Dictionary<string, object?>
        {
            ["size"] = size,
            ["watermark"] = request.Watermark || ImageConfig.Watermark,
            ["prompt_extend"] = ImageConfig.PromptExtend
        };

        var n = request.MaxImages.HasValue && request.MaxImages.Value > 0
            ? request.MaxImages.Value
            : Math.Max(1, ImageConfig.Images);
        parameters["n"] = isQwenImage ? 1 : Math.Clamp(n, 1, 4);

        if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
            parameters["negative_prompt"] = request.NegativePrompt.Trim();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input"] = new Dictionary<string, object?>
            {
                ["messages"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["role"] = "user",
                        ["content"] = BuildContentParts(request)
                    }
                }
            },
            ["parameters"] = parameters
        };

        using var httpClient = CreateHttpClient(ProviderConfig);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("api/v1/services/aigc/multimodal-generation/generation", content, cancellationToken)
            .ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Qwen image generation response. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Qwen image generation failed: {responseBody}");

        var result = await TryParseImageResultAsync(responseBody, model, cancellationToken).ConfigureAwait(false);
        if (result != null)
            return result;

        if (TryExtractTaskId(responseBody, out var taskId) && !string.IsNullOrWhiteSpace(taskId))
        {
            var taskBody = await PollTaskAsync(httpClient, taskId, ProviderConfig.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
            var taskStatus = ExtractTaskStatus(taskBody);
            if (!IsSuccessStatus(taskStatus))
                throw new InvalidOperationException($"Qwen image generation did not succeed. Status: {taskStatus}");

            result = await TryParseImageResultAsync(taskBody, model, cancellationToken).ConfigureAwait(false);
            if (result != null)
                return result;
        }

        throw new InvalidOperationException("Qwen image generation returned no image content.");
    }

    private static List<Dictionary<string, object?>> BuildContentParts(ImageGenerationRequest request)
    {
        var content = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["text"] = request.Prompt.Trim()
            }
        };

        return content;
    }

    private string ResolveSize(ImageGenerationRequest request, string model)
    {
        var isQwenImage = IsQwenImageModel(model);
        var isWanImage = IsWanImageModel(model);
        var fallback = isQwenImage ? DefaultQwenImageSize : DefaultWanImageSize;

        if (!string.IsNullOrWhiteSpace(request.Size))
        {
            var normalized = NormalizeSize(request.Size.Trim());
            return ValidateSize(normalized, isQwenImage, isWanImage, fallback);
        }

        if (request.Width > 0 && request.Height > 0)
        {
            var normalized = $"{request.Width}*{request.Height}";
            return ValidateSize(normalized, isQwenImage, isWanImage, fallback);
        }

        if (!string.IsNullOrWhiteSpace(ImageConfig.Size))
        {
            var normalized = NormalizeSize(ImageConfig.Size.Trim());
            return ValidateSize(normalized, isQwenImage, isWanImage, fallback);
        }

        return fallback;
    }

    private static string NormalizeSize(string size)
    {
        if (string.IsNullOrWhiteSpace(size))
            return "1024*1024";

        var normalized = size.Trim();
        if (string.Equals(normalized, "1K", StringComparison.OrdinalIgnoreCase))
            return "1024*1024";
        if (string.Equals(normalized, "2K", StringComparison.OrdinalIgnoreCase))
            return "2048*2048";
        if (string.Equals(normalized, "4K", StringComparison.OrdinalIgnoreCase))
            return "4096*4096";

        normalized = normalized.Replace('x', '*').Replace('X', '*').Replace('×', '*');
        return normalized;
    }

    private static string ValidateSize(string size, bool isQwenImage, bool isWanImage, string fallback)
    {
        if (isQwenImage)
            return AllowedQwenImageSizes.Contains(size) ? size : fallback;
        if (isWanImage)
            return IsValidWanSize(size) ? size : fallback;
        return size;
    }

    private static bool IsValidWanSize(string size)
    {
        if (!TryParseSize(size, out var width, out var height))
            return false;

        var totalPixels = width * height;
        var minPixels = 1280 * 1280;
        var maxPixels = 1440 * 1440;
        if (totalPixels < minPixels || totalPixels > maxPixels)
            return false;

        var ratio = width / (double)height;
        return ratio >= 0.25 && ratio <= 4.0;
    }

    private static bool TryParseSize(string size, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(size))
            return false;

        var normalized = NormalizeSize(size);
        var parts = normalized.Split('*');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out width) &&
            int.TryParse(parts[1], out height) &&
            width > 0 && height > 0)
        {
            return true;
        }

        width = 0;
        height = 0;
        return false;
    }

    private static bool IsQwenImageModel(string model)
        => model.StartsWith("qwen-image", StringComparison.OrdinalIgnoreCase);

    private static bool IsWanImageModel(string model)
        => model.StartsWith("wan", StringComparison.OrdinalIgnoreCase);

    private const string DefaultQwenImageSize = "1664*928";
    private const string DefaultWanImageSize = "1280*1280";

    private static readonly HashSet<string> AllowedQwenImageSizes = new(StringComparer.OrdinalIgnoreCase)
    {
        "1664*928",
        "1472*1104",
        "1328*1328",
        "1104*1472",
        "928*1664"
    };

    private static async Task<ImageGenerationResult?> TryParseImageResultAsync(
        string responseBody,
        string modelUsed,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(responseBody);

        if (TryExtractImageString(doc.RootElement, out var imageValue) &&
            !string.IsNullOrWhiteSpace(imageValue))
        {
            var (bytes, extension) = await ResolveImageBytesAsync(imageValue, cancellationToken).ConfigureAwait(false);
            return new ImageGenerationResult(bytes, extension, modelUsed);
        }

        return null;
    }

    private static bool TryExtractImageString(JsonElement root, out string? value)
    {
        value = null;

        if (root.TryGetProperty("output", out var output))
        {
            if (TryExtractFromChoices(output, out value))
                return true;

            if (TryExtractFromResults(output, out value))
                return true;

            if (TryReadString(output, "image", out value))
                return true;
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (TryReadString(item, "b64_json", out value))
                    return true;
                if (TryReadString(item, "url", out value))
                    return true;
            }
        }

        return false;
    }

    private static bool TryExtractFromChoices(JsonElement output, out string? value)
    {
        value = null;
        if (!output.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message))
                continue;

            if (!message.TryGetProperty("content", out var content))
                continue;

            if (content.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (TryReadString(item, "image", out value))
                        return true;
                    if (TryReadString(item, "url", out value))
                        return true;
                    if (TryReadString(item, "b64_json", out value))
                        return true;
                    if (TryReadImageUrl(item, out value))
                        return true;
                }
            }
            else if (content.ValueKind == JsonValueKind.Object)
            {
                if (TryReadString(content, "image", out value))
                    return true;
                if (TryReadImageUrl(content, out value))
                    return true;
            }
        }

        return false;
    }

    private static bool TryExtractFromResults(JsonElement output, out string? value)
    {
        value = null;
        if (!output.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var item in results.EnumerateArray())
        {
            if (TryReadString(item, "url", out value))
                return true;
            if (TryReadString(item, "image", out value))
                return true;
            if (TryReadString(item, "b64_json", out value))
                return true;
        }

        return false;
    }

    private static bool TryReadImageUrl(JsonElement element, out string? value)
    {
        value = null;
        if (!element.TryGetProperty("image_url", out var imageUrl))
            return false;

        if (imageUrl.ValueKind == JsonValueKind.String)
        {
            value = imageUrl.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        if (imageUrl.ValueKind == JsonValueKind.Object && TryReadString(imageUrl, "url", out value))
            return true;

        return false;
    }

    private static bool TryReadString(JsonElement element, string property, out string? value)
    {
        value = null;
        if (element.TryGetProperty(property, out var data) && data.ValueKind == JsonValueKind.String)
        {
            value = data.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static async Task<(byte[] Bytes, string Extension)> ResolveImageBytesAsync(string value, CancellationToken cancellationToken)
    {
        if (value.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = await DownloadBytesAsync(value, cancellationToken).ConfigureAwait(false);
            var extension = ResolveExtensionFromUrl(value) ?? ".png";
            return (bytes, extension);
        }

        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var (bytes, extension) = ParseDataUrl(value);
            return (bytes, extension);
        }

        var raw = Convert.FromBase64String(value);
        return (raw, ".png");
    }

    private static (byte[] Bytes, string Extension) ParseDataUrl(string dataUrl)
    {
        var commaIndex = dataUrl.IndexOf(',');
        if (commaIndex < 0)
            throw new InvalidOperationException("Invalid data URL for image content.");

        var header = dataUrl.Substring(5, commaIndex - 5);
        var base64 = dataUrl[(commaIndex + 1)..];

        var mime = header.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "image/png";
        var bytes = Convert.FromBase64String(base64);
        var extension = ResolveExtensionFromMime(mime);
        return (bytes, extension);
    }

    private static string ResolveExtensionFromMime(string mime)
    {
        var normalized = mime.ToLowerInvariant();
        return normalized switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            "image/bmp" => ".bmp",
            "image/gif" => ".gif",
            "image/tiff" => ".tiff",
            _ => ".png"
        };
    }

    private static string? ResolveExtensionFromUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(ext))
                return ext;
        }

        return null;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryExtractTaskId(string responseBody, out string? taskId)
    {
        taskId = null;
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("output", out var output) &&
                output.TryGetProperty("task_id", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String)
            {
                taskId = idElement.GetString();
            }
        }
        catch
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(taskId);
    }

    private static async Task<string> PollTaskAsync(
        HttpClient httpClient,
        string taskId,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(30, timeoutSeconds));
        var start = DateTimeOffset.UtcNow;
        var lastStatus = string.Empty;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            var body = await GetTaskAsync(httpClient, taskId, cancellationToken).ConfigureAwait(false);
            var status = ExtractTaskStatus(body);
            lastStatus = status;

            if (IsTerminalStatus(status))
                return body;
        }

        throw new TimeoutException($"Qwen image generation task {taskId} timed out. Last status: {lastStatus}.");
    }

    private static bool IsTerminalStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var normalized = status.ToLowerInvariant();
        return normalized is "succeeded" or "success" or "succeed" or "failed" or "fail" or "error" or "canceled" or "cancelled" or "expired";
    }

    private static bool IsSuccessStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var normalized = status.ToLowerInvariant();
        return normalized is "succeeded" or "success" or "succeed" or "completed" or "finished";
    }

    private static string ExtractTaskStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("output", out var output) &&
                output.TryGetProperty("task_status", out var statusElement) &&
                statusElement.ValueKind == JsonValueKind.String)
            {
                return statusElement.GetString() ?? string.Empty;
            }

            if (doc.RootElement.TryGetProperty("task_status", out var rootStatus) &&
                rootStatus.ValueKind == JsonValueKind.String)
            {
                return rootStatus.GetString() ?? string.Empty;
            }
        }
        catch
        {
            return string.Empty;
        }

        return string.Empty;
    }

    private static async Task<string> GetTaskAsync(HttpClient httpClient, string taskId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/v1/tasks/{taskId}", cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Uri BuildBaseAddress(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Endpoint is required.");

        var normalized = endpoint.TrimEnd('/');
        if (normalized.EndsWith("/compatible-mode/v1", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^"/compatible-mode/v1".Length];
        if (normalized.EndsWith("/api/v1/services/aigc", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^"/api/v1/services/aigc".Length];
        if (normalized.EndsWith("/api/v1/services", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^"/api/v1/services".Length];
        if (normalized.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^"/api/v1".Length];

        return new Uri($"{normalized}/");
    }

    private static HttpClient CreateHttpClient(AIProviderConfiguration providerConfig)
    {
        return new HttpClient
        {
            BaseAddress = BuildBaseAddress(providerConfig.Endpoint),
            Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds)
        };
    }

    private static string ToDataUrl(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var mime = GetMimeType(filePath);
        return $"data:{mime};base64,{base64}";
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}
