using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class NewApiVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<NewApiVideoGenerationProvider> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const int PollIntervalSeconds = 5;
    private const int MaxPollIterations = 120; // ~10 分钟

    public NewApiVideoGenerationProvider(
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<NewApiVideoGenerationProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.NewApi;
    private NewApiVideoConfig VideoConfig => _configMonitor.CurrentValue.Video.NewApi;

    public VideoProviderType ProviderType => VideoProviderType.NewApi;
    public string DisplayName => "New API";

    public bool IsConfigured =>
        ProviderConfig.Enabled &&
        !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey) &&
        !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => Array.Empty<string>();

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "OpenAI-compatible /v1/videos", "video/mp4")
    };

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("NewApi video generation is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Video
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No video model configured for NewApi.");

        var shot = request.Shot;
        var prompt = BuildPrompt(shot);
        var durationSeconds = ResolveDurationSeconds(shot);

        using var httpClient = CreateHttpClient();
        using var form = BuildMultipartForm(shot, request, prompt, model, durationSeconds);

        var response = await httpClient.PostAsync("videos", form, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("NewApi video generation failed with status {StatusCode}: {Body}", response.StatusCode, responseBody);
            throw new InvalidOperationException($"NewApi video generation failed with status {response.StatusCode}: {responseBody}");
        }

        // Check if response looks like JSON
        var trimmedBody = responseBody.TrimStart();
        if (string.IsNullOrWhiteSpace(trimmedBody) || (!trimmedBody.StartsWith("{") && !trimmedBody.StartsWith("[")))
        {
            _logger.LogError("NewApi returned non-JSON response for video generation. Content-Type: {ContentType}, Body preview: {BodyPreview}",
                response.Content.Headers.ContentType?.ToString() ?? "unknown",
                trimmedBody.Length > 200 ? trimmedBody.Substring(0, 200) : trimmedBody);
            throw new InvalidOperationException($"NewApi returned non-JSON response. Check endpoint URL configuration. Response starts with: {(trimmedBody.Length > 50 ? trimmedBody.Substring(0, 50) : trimmedBody)}");
        }

        VideoStatusResponse createResponse;
        try
        {
            createResponse = JsonSerializer.Deserialize<VideoStatusResponse>(responseBody, JsonOptions)
                             ?? throw new InvalidOperationException("Unable to parse NewApi video generation response.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize NewApi video generation response. Body preview: {BodyPreview}",
                responseBody.Length > 500 ? responseBody.Substring(0, 500) : responseBody);
            throw new InvalidOperationException($"Failed to parse NewApi response as JSON. This usually means the endpoint URL is incorrect or the API is not OpenAI-compatible.", ex);
        }
        var taskId = createResponse.Id;
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("NewApi video generation did not return a task id.");

        VideoStatusResponse finalStatus;
        if (IsCompleted(createResponse.Status))
        {
            finalStatus = createResponse;
        }
        else
        {
            finalStatus = await PollVideoStatusAsync(httpClient, taskId, cancellationToken).ConfigureAwait(false);
        }

        await DownloadVideoAsync(httpClient, taskId, finalStatus.Url, request.OutputPath, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("NewApi video saved to {Path}", request.OutputPath);
    }

    private HttpClient CreateHttpClient()
    {
        var endpoint = ProviderConfig.Endpoint?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Endpoint is required for NewApi video generation.");

        // Ensure endpoint includes /v1 for OpenAI-compatible API
        if (!endpoint.Contains("/v1"))
        {
            endpoint = endpoint + "/v1";
        }

        var client = new HttpClient
        {
            BaseAddress = new Uri($"{endpoint}/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(60, ProviderConfig.TimeoutSeconds))
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        return client;
    }

    private MultipartFormDataContent BuildMultipartForm(
        ShotItem shot,
        VideoGenerationRequest request,
        string prompt,
        string model,
        int durationSeconds)
    {
        var form = new MultipartFormDataContent($"----StoryboardBoundary{Guid.NewGuid():N}");

        var referenceImage = TryLoadReferenceImage(shot);
        var hasReferenceImage = referenceImage.HasValue;

        // Determine task type based on whether we have a reference image
        var taskType = hasReferenceImage ? "i2v" : "t2v";

        _logger.LogInformation("Building video generation request: Model={Model}, TaskType={TaskType}, HasReferenceImage={HasReference}, FirstFramePath={FirstFrame}, LastFramePath={LastFrame}",
            model, taskType, hasReferenceImage, shot.FirstFrameImagePath ?? "null", shot.LastFrameImagePath ?? "null");

        form.Add(new StringContent(taskType), "task_type");

        form.Add(new StringContent(model), "model");
        form.Add(new StringContent(prompt, Encoding.UTF8), "prompt");
        form.Add(new StringContent(durationSeconds.ToString(CultureInfo.InvariantCulture)), "seconds");
        form.Add(new StringContent($"{request.Width}x{request.Height}"), "size");

        if (!string.IsNullOrWhiteSpace(VideoConfig.Ratio))
        {
            form.Add(new StringContent(VideoConfig.Ratio), "ratio");
        }

        if (shot.GenerateAudio)
        {
            form.Add(new StringContent("true"), "generate_audio");
        }

        if (!string.IsNullOrWhiteSpace(shot.VideoNegativePrompt))
        {
            form.Add(new StringContent(shot.VideoNegativePrompt.Trim(), Encoding.UTF8), "negative_prompt");
        }

        var metadata = BuildMetadata(shot);
        if (metadata.Count > 0)
        {
            form.Add(new StringContent(JsonSerializer.Serialize(metadata), Encoding.UTF8, "application/json"), "metadata");
        }

        if (hasReferenceImage)
        {
            var (bytes, fileName) = referenceImage.Value;
            var imageContent = new ByteArrayContent(bytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(imageContent, "input_reference", fileName);
            _logger.LogInformation("Added reference image: {FileName}, Size={Size} bytes", fileName, bytes.Length);
        }

        return form;
    }

    private Dictionary<string, object> BuildMetadata(ShotItem shot)
    {
        var metadata = new Dictionary<string, object>();

        if (shot.Watermark || VideoConfig.Watermark)
        {
            metadata["watermark"] = true;
        }

        if (VideoConfig.ReturnLastFrame)
        {
            metadata["return_last_frame"] = true;
        }

        if (!string.IsNullOrWhiteSpace(VideoConfig.ProviderHint))
        {
            metadata["provider"] = VideoConfig.ProviderHint!.Trim();
        }

        if (!string.IsNullOrWhiteSpace(shot.VideoRatio))
        {
            metadata["aspect_ratio"] = shot.VideoRatio;
        }

        return metadata;
    }

    private static (byte[] Bytes, string FileName)? TryLoadReferenceImage(ShotItem shot)
    {
        string? path = null;

        // Priority 1: Explicitly set paths
        if (!string.IsNullOrWhiteSpace(shot.FirstFrameImagePath))
        {
            path = shot.FirstFrameImagePath;
        }
        else if (!string.IsNullOrWhiteSpace(shot.LastFrameImagePath))
        {
            path = shot.LastFrameImagePath;
        }
        // Priority 2: If UseFirstFrameReference is checked, try to get from FirstFrameAssets
        else if (shot.UseFirstFrameReference && shot.FirstFrameAssets.Count > 0)
        {
            var firstAsset = shot.FirstFrameAssets.FirstOrDefault();
            if (firstAsset != null && !string.IsNullOrWhiteSpace(firstAsset.FilePath))
            {
                path = firstAsset.FilePath;
            }
        }
        // Priority 3: If UseLastFrameReference is checked, try to get from LastFrameAssets
        else if (shot.UseLastFrameReference && shot.LastFrameAssets.Count > 0)
        {
            var lastAsset = shot.LastFrameAssets.FirstOrDefault();
            if (lastAsset != null && !string.IsNullOrWhiteSpace(lastAsset.FilePath))
            {
                path = lastAsset.FilePath;
            }
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var bytes = File.ReadAllBytes(path);
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "reference.png";
        }

        return (bytes, fileName);
    }

    private async Task<VideoStatusResponse> PollVideoStatusAsync(HttpClient httpClient, string taskId, CancellationToken cancellationToken)
    {
        for (var i = 0; i < MaxPollIterations; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), cancellationToken).ConfigureAwait(false);

            var response = await httpClient.GetAsync($"videos/{taskId}", cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"NewApi video polling failed: {body}");
            }

            var status = JsonSerializer.Deserialize<VideoStatusResponse>(body, JsonOptions)
                         ?? throw new InvalidOperationException("Unable to parse NewApi polling response.");

            if (IsCompleted(status.Status))
            {
                return status;
            }

            if (IsFailed(status.Status))
            {
                throw new InvalidOperationException($"NewApi video generation failed: {body}");
            }
        }

        throw new TimeoutException("NewApi video generation polling timed out.");
    }

    private async Task DownloadVideoAsync(
        HttpClient httpClient,
        string taskId,
        string? downloadUrl,
        string outputPath,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage downloadResponse = !string.IsNullOrWhiteSpace(downloadUrl)
            ? await httpClient.GetAsync(downloadUrl, cancellationToken).ConfigureAwait(false)
            : await httpClient.GetAsync($"videos/{taskId}/content", HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

        downloadResponse.EnsureSuccessStatusCode();
        await using var fileStream = File.Create(outputPath);
        await downloadResponse.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPrompt(ShotItem shot)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
            builder.AppendLine(shot.VideoPrompt);

        if (!string.IsNullOrWhiteSpace(shot.SceneDescription))
            builder.AppendLine($"场景: {shot.SceneDescription}");
        if (!string.IsNullOrWhiteSpace(shot.ActionDescription))
            builder.AppendLine($"动作: {shot.ActionDescription}");
        if (!string.IsNullOrWhiteSpace(shot.StyleDescription))
            builder.AppendLine($"风格: {shot.StyleDescription}");
        if (!string.IsNullOrWhiteSpace(shot.CoreContent))
            builder.AppendLine($"核心: {shot.CoreContent}");
        if (!string.IsNullOrWhiteSpace(shot.CameraMovement))
            builder.AppendLine($"镜头运动: {shot.CameraMovement}");
        if (!string.IsNullOrWhiteSpace(shot.ShootingStyle))
            builder.AppendLine($"拍摄手法: {shot.ShootingStyle}");
        if (!string.IsNullOrWhiteSpace(shot.VideoEffect))
            builder.AppendLine($"视觉效果: {shot.VideoEffect}");

        var prompt = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(prompt) ? "Create a cinematic storyboard shot." : prompt;
    }

    private int ResolveDurationSeconds(ShotItem shot)
    {
        var duration = (int)Math.Round(shot.EffectiveGeneratedDurationSeconds);
        if (duration <= 0)
            duration = Math.Max(4, VideoConfig.DurationSeconds);
        duration = Math.Clamp(duration, 1, 20);
        if (VideoConfig.DurationSeconds > 0)
            duration = Math.Min(duration, VideoConfig.DurationSeconds);
        return duration;
    }

    private static bool IsCompleted(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        status = status.ToLowerInvariant();
        return status is "succeeded" or "finished" or "completed";
    }

    private static bool IsFailed(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;
        var lower = status.ToLowerInvariant();
        return lower.Contains("fail") || lower.Contains("error");
    }

    private sealed class VideoStatusResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
