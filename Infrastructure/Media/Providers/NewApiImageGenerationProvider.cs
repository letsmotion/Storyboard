using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class NewApiImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<NewApiImageGenerationProvider> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public NewApiImageGenerationProvider(
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<NewApiImageGenerationProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.NewApi;
    private NewApiImageConfig ImageConfig => _configMonitor.CurrentValue.Image.NewApi;

    public ImageProviderType ProviderType => ImageProviderType.NewApi;
    public string DisplayName => "New API";

    public bool IsConfigured =>
        ProviderConfig.Enabled &&
        !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey) &&
        !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => Array.Empty<string>();

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.ImageGeneration, "OpenAI-compatible /v1/images/generations", "image/png")
    };

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("NewApi image generation is not configured.");
        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("Image prompt is empty.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Image
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No image model configured for NewApi.");

        var size = ResolveSize(request);
        var responseFormat = string.IsNullOrWhiteSpace(ImageConfig.ResponseFormat)
            ? "b64_json"
            : ImageConfig.ResponseFormat;
        var n = request.MaxImages ?? Math.Max(1, ImageConfig.Images);
        var enrichedPrompt = BuildPrompt(request);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["prompt"] = enrichedPrompt,
            ["size"] = size,
            ["n"] = Math.Clamp(n, 1, 4),
            ["response_format"] = responseFormat
        };

        if (request.Watermark || ImageConfig.Watermark)
        {
            payload["watermark"] = true;
        }

        if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
        {
            payload["negative_prompt"] = request.NegativePrompt.Trim();
        }

        if (!string.IsNullOrWhiteSpace(ImageConfig.ProviderHint))
        {
            payload["provider"] = ImageConfig.ProviderHint!.Trim();
        }

        var references = BuildReferenceImages(request.ReferenceImagePaths);
        if (references.Count > 0)
        {
            payload["images"] = references;
        }

        using var httpClient = CreateHttpClient();
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "images/generations")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await httpClient.SendAsync(requestMessage, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("NewApi image generation failed: {Body}", responseBody);
            throw new InvalidOperationException($"NewApi image generation failed: {responseBody}");
        }

        var result = JsonSerializer.Deserialize<ImageGenerationResponse>(responseBody, JsonOptions);
        if (result?.Data == null || result.Data.Count == 0)
        {
            throw new InvalidOperationException("NewApi image generation returned no data.");
        }

        var first = result.Data[0];
        byte[] imageBytes;
        string extension = ".png";

        if (!string.IsNullOrWhiteSpace(first.B64Json))
        {
            imageBytes = Convert.FromBase64String(first.B64Json);
            extension = ".png";
        }
        else if (!string.IsNullOrWhiteSpace(first.Url))
        {
            var download = await httpClient.GetAsync(first.Url, cancellationToken).ConfigureAwait(false);
            download.EnsureSuccessStatusCode();
            imageBytes = await download.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            extension = ResolveExtension(download.Content.Headers.ContentType?.MediaType);
        }
        else
        {
            throw new InvalidOperationException("NewApi image generation returned an empty result.");
        }

        return new ImageGenerationResult(imageBytes, extension, model);
    }

    private HttpClient CreateHttpClient()
    {
        var baseAddress = ProviderConfig.Endpoint?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new InvalidOperationException("Endpoint is required for NewApi image generation.");

        var client = new HttpClient
        {
            BaseAddress = new Uri($"{baseAddress.TrimEnd('/')}/"),
            Timeout = TimeSpan.FromSeconds(ProviderConfig.TimeoutSeconds)
        };
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        return client;
    }

    private static string ResolveSize(ImageGenerationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Size))
            return request.Size;
        if (request.Width > 0 && request.Height > 0)
            return $"{request.Width}x{request.Height}";
        return "1024x1024";
    }

    private static string BuildPrompt(ImageGenerationRequest request)
    {
        var parts = new List<string> { request.Prompt };
        if (!string.IsNullOrWhiteSpace(request.ShotType))
            parts.Add($"景别: {request.ShotType}");
        if (!string.IsNullOrWhiteSpace(request.Composition))
            parts.Add($"构图: {request.Composition}");
        if (!string.IsNullOrWhiteSpace(request.LightingType))
            parts.Add($"光线: {request.LightingType}");
        if (!string.IsNullOrWhiteSpace(request.TimeOfDay))
            parts.Add($"时间: {request.TimeOfDay}");
        if (!string.IsNullOrWhiteSpace(request.ColorStyle))
            parts.Add($"色调: {request.ColorStyle}");

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static List<object> BuildReferenceImages(List<string>? imagePaths)
    {
        var result = new List<object>();
        if (imagePaths == null || imagePaths.Count == 0)
            return result;

        foreach (var path in imagePaths)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                continue;

            var bytes = File.ReadAllBytes(path);
            result.Add(new
            {
                type = "input_image",
                data = Convert.ToBase64String(bytes)
            });
        }

        return result;
    }

    private static string ResolveExtension(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
            return ".png";

        return mediaType.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".png"
        };
    }

    private sealed class ImageGenerationResponse
    {
        [JsonPropertyName("data")]
        public List<ImageData> Data { get; set; } = new();
    }

    private sealed class ImageData
    {
        [JsonPropertyName("b64_json")]
        public string? B64Json { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
