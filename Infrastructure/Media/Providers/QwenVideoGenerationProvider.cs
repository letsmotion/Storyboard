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
using Storyboard.Models;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class QwenVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<QwenVideoGenerationProvider> _logger;

    public QwenVideoGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<QwenVideoGenerationProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.Qwen;
    private QwenVideoConfig VideoConfig => _configMonitor.CurrentValue.Video.Qwen;

    public VideoProviderType ProviderType => VideoProviderType.Qwen;
    public string DisplayName => "Qwen";
    public bool IsConfigured => ProviderConfig.Enabled
        && !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey)
        && !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "wan2.6-t2v",
        "wan2.5-t2v-preview",
        "wan2.2-t2v-plus",
        "wanx2.1-t2v-turbo",
        "wanx2.1-t2v-plus",
        "wan2.6-i2v-flash",
        "wan2.6-i2v",
        "wan2.5-i2v-preview",
        "wan2.2-i2v-flash",
        "wan2.2-i2v-plus",
        "wanx2.1-i2v-plus",
        "wanx2.1-i2v-turbo",
        "wan2.2-kf2v-flash",
        "wanx2.1-kf2v-plus",
        "wan2.6-r2v"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "Async task", "video/mp4")
    };

    private enum VideoRequestMode
    {
        TextToVideo,
        ImageToVideo,
        KeyframeToVideo,
        ReferenceVideo
    }

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("Qwen video generation is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? ProviderConfig.DefaultModels.Video
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No video model configured for Qwen.");

        var shot = request.Shot;
        var prompt = BuildPrompt(shot);
        var firstFrameUrl = TryCreateImageUrl(shot.FirstFrameImagePath);
        var lastFrameUrl = TryCreateImageUrl(shot.LastFrameImagePath);
        var referenceUrls = ResolveReferenceUrls(firstFrameUrl, lastFrameUrl);
        var mode = ResolveMode(model, shot);

        var input = new Dictionary<string, object?>
        {
            ["prompt"] = prompt
        };

        if (!string.IsNullOrWhiteSpace(shot.VideoNegativePrompt))
            input["negative_prompt"] = shot.VideoNegativePrompt.Trim();

        switch (mode)
        {
            case VideoRequestMode.ImageToVideo:
            {
                var imgUrl = ResolveImageUrl(shot, firstFrameUrl, lastFrameUrl);
                if (string.IsNullOrWhiteSpace(imgUrl))
                    throw new InvalidOperationException("Image-to-video requires a reference image.");
                input["img_url"] = imgUrl;
                break;
            }
            case VideoRequestMode.KeyframeToVideo:
            {
                if (string.IsNullOrWhiteSpace(firstFrameUrl))
                    throw new InvalidOperationException("Keyframe-to-video requires a first frame image.");
                input["first_frame_url"] = firstFrameUrl;
                if (!string.IsNullOrWhiteSpace(lastFrameUrl))
                    input["last_frame_url"] = lastFrameUrl;
                break;
            }
            case VideoRequestMode.ReferenceVideo:
                if (referenceUrls.Count == 0)
                    throw new InvalidOperationException("Reference-to-video requires at least one reference image or video.");
                input["reference_urls"] = referenceUrls;
                break;
        }

        var parameters = new Dictionary<string, object?>
        {
            ["watermark"] = shot.Watermark || VideoConfig.Watermark
        };

        var seed = shot.Seed ?? VideoConfig.Seed;
        if (seed.HasValue)
            parameters["seed"] = seed.Value;

        if (mode != VideoRequestMode.ReferenceVideo)
            parameters["prompt_extend"] = VideoConfig.PromptExtend;

        if (!string.IsNullOrWhiteSpace(VideoConfig.ShotType))
        {
            if (mode == VideoRequestMode.ReferenceVideo)
            {
                parameters["shot_type"] = VideoConfig.ShotType;
            }
            else if (VideoConfig.PromptExtend &&
                     (mode == VideoRequestMode.TextToVideo || mode == VideoRequestMode.ImageToVideo))
            {
                parameters["shot_type"] = VideoConfig.ShotType;
            }
        }

        if (mode is VideoRequestMode.TextToVideo or VideoRequestMode.ImageToVideo or VideoRequestMode.ReferenceVideo)
        {
            var maxDuration = mode == VideoRequestMode.ReferenceVideo ? 10 : 15;
            var duration = ResolveDurationSeconds(VideoConfig.DurationSeconds, shot.EffectiveGeneratedDurationSeconds, maxDuration);
            if (duration > 0)
                parameters["duration"] = duration;
        }

        if (mode == VideoRequestMode.ImageToVideo || mode == VideoRequestMode.KeyframeToVideo)
        {
            parameters["resolution"] = ResolveResolution(request, shot);
        }
        else if (mode == VideoRequestMode.TextToVideo || mode == VideoRequestMode.ReferenceVideo)
        {
            parameters["size"] = ResolveSize(request, shot);
        }

        if (mode == VideoRequestMode.ImageToVideo &&
            model.Contains("i2v-flash", StringComparison.OrdinalIgnoreCase))
        {
            parameters["audio"] = shot.GenerateAudio;
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input"] = input,
            ["parameters"] = parameters
        };

        var endpoint = mode == VideoRequestMode.KeyframeToVideo
            ? "api/v1/services/aigc/image2video/video-synthesis"
            : "api/v1/services/aigc/video-generation/video-synthesis";

        using var httpClient = CreateHttpClient(ProviderConfig);
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", ProviderConfig.ApiKey);
        httpClient.DefaultRequestHeaders.Add("X-DashScope-Async", "enable");

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(endpoint, content, cancellationToken)
            .ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Qwen video generation response. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Qwen video generation failed: {responseBody}");

        var taskId = ExtractTaskId(responseBody);
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("Qwen video generation did not return a task id.");

        var status = ExtractTaskStatus(responseBody);
        if (!IsSuccessStatus(status))
        {
            responseBody = await PollTaskAsync(httpClient, taskId, ProviderConfig.TimeoutSeconds, cancellationToken).ConfigureAwait(false);
            status = ExtractTaskStatus(responseBody);
        }

        if (!IsSuccessStatus(status))
            throw new InvalidOperationException($"Qwen video generation did not succeed. Status: {status}");

        var videoUrl = ExtractVideoUrl(responseBody);
        if (string.IsNullOrWhiteSpace(videoUrl))
            throw new InvalidOperationException("Qwen video generation result did not include a video url.");

        var videoBytes = await DownloadBytesAsync(videoUrl, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(request.OutputPath, videoBytes, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPrompt(ShotItem shot)
    {
        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
        {
            var parts = new List<string> { shot.VideoPrompt.Trim() };

            if (!string.IsNullOrWhiteSpace(shot.CameraMovement))
                parts.Add($"Camera: {shot.CameraMovement}");
            if (!string.IsNullOrWhiteSpace(shot.ShootingStyle))
                parts.Add($"Style: {shot.ShootingStyle}");
            if (!string.IsNullOrWhiteSpace(shot.VideoEffect))
                parts.Add($"Effect: {shot.VideoEffect}");

            return string.Join(", ", parts);
        }

        var fallbackParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(shot.SceneDescription))
            fallbackParts.Add(shot.SceneDescription);
        if (!string.IsNullOrWhiteSpace(shot.ActionDescription))
            fallbackParts.Add(shot.ActionDescription);
        if (!string.IsNullOrWhiteSpace(shot.StyleDescription))
            fallbackParts.Add(shot.StyleDescription);
        if (!string.IsNullOrWhiteSpace(shot.CoreContent))
            fallbackParts.Add(shot.CoreContent);
        if (!string.IsNullOrWhiteSpace(shot.SceneSettings))
            fallbackParts.Add(shot.SceneSettings);
        if (!string.IsNullOrWhiteSpace(shot.ActionCommand))
            fallbackParts.Add(shot.ActionCommand);

        var prompt = string.Join(", ", fallbackParts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        return string.IsNullOrWhiteSpace(prompt) ? "Cinematic video." : prompt;
    }

    private static VideoRequestMode ResolveMode(string model, ShotItem shot)
    {
        if (model.Contains("r2v", StringComparison.OrdinalIgnoreCase))
            return VideoRequestMode.ReferenceVideo;
        if (model.Contains("kf2v", StringComparison.OrdinalIgnoreCase))
            return VideoRequestMode.KeyframeToVideo;
        if (model.Contains("i2v", StringComparison.OrdinalIgnoreCase))
            return VideoRequestMode.ImageToVideo;
        if (model.Contains("t2v", StringComparison.OrdinalIgnoreCase))
            return VideoRequestMode.TextToVideo;

        if (shot.UseReferenceImages)
            return VideoRequestMode.ReferenceVideo;
        if (shot.UseFirstFrameReference && shot.UseLastFrameReference)
            return VideoRequestMode.KeyframeToVideo;
        if (shot.UseFirstFrameReference || shot.UseLastFrameReference)
            return VideoRequestMode.ImageToVideo;
        return VideoRequestMode.TextToVideo;
    }

    private static string? ResolveImageUrl(ShotItem shot, string? firstFrameUrl, string? lastFrameUrl)
    {
        if (shot.UseLastFrameReference && !string.IsNullOrWhiteSpace(lastFrameUrl))
            return lastFrameUrl;
        if (shot.UseFirstFrameReference && !string.IsNullOrWhiteSpace(firstFrameUrl))
            return firstFrameUrl;
        return !string.IsNullOrWhiteSpace(firstFrameUrl) ? firstFrameUrl : lastFrameUrl;
    }

    private static string? TryCreateImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return null;

        return ToDataUrl(imagePath);
    }

    private static List<string> ResolveReferenceUrls(string? firstFrameUrl, string? lastFrameUrl)
    {
        var urls = new List<string>();
        if (!string.IsNullOrWhiteSpace(firstFrameUrl))
            urls.Add(firstFrameUrl);
        if (!string.IsNullOrWhiteSpace(lastFrameUrl) &&
            !string.Equals(lastFrameUrl, firstFrameUrl, StringComparison.OrdinalIgnoreCase))
        {
            urls.Add(lastFrameUrl);
        }

        return urls;
    }

    private string ResolveResolution(VideoGenerationRequest request, ShotItem shot)
    {
        var resolution = !string.IsNullOrWhiteSpace(shot.VideoResolution)
            ? shot.VideoResolution
            : VideoConfig.Resolution;

        if (string.IsNullOrWhiteSpace(resolution))
            return MapResolutionFromSize(request.Width, request.Height);

        if (TryParseSize(resolution, out var width, out var height))
            return MapResolutionFromSize(width, height);

        return NormalizeResolutionValue(resolution);
    }

    private string ResolveSize(VideoGenerationRequest request, ShotItem shot)
    {
        if (!string.IsNullOrWhiteSpace(shot.VideoResolution) &&
            TryParseSize(shot.VideoResolution, out var width, out var height))
        {
            return $"{width}*{height}";
        }

        if (!string.IsNullOrWhiteSpace(VideoConfig.Size))
            return NormalizeSize(VideoConfig.Size);

        if (request.Width > 0 && request.Height > 0)
            return $"{request.Width}*{request.Height}";

        return "1280*720";
    }

    private static string NormalizeResolutionValue(string resolution)
    {
        var digits = new string(resolution.Where(char.IsDigit).ToArray());
        return digits switch
        {
            "1080" => "1080P",
            "720" => "720P",
            "480" => "480P",
            _ => "720P"
        };
    }

    private static string MapResolutionFromSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return "720P";

        var max = Math.Max(width, height);
        if (max >= 1000)
            return "1080P";
        if (max >= 700)
            return "720P";
        return "480P";
    }

    private static string NormalizeSize(string size)
    {
        if (string.IsNullOrWhiteSpace(size))
            return "1280*720";

        return size.Trim().Replace('x', '*').Replace('X', '*');
    }

    private static bool TryParseSize(string size, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (string.IsNullOrWhiteSpace(size))
            return false;

        var normalized = size.Trim().Replace('x', '*').Replace('X', '*');
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

    private static int ResolveDurationSeconds(int configDuration, double shotDuration, int maxSeconds)
    {
        var duration = configDuration > 0 ? configDuration : shotDuration;
        if (duration <= 0)
            return 0;

        var seconds = (int)Math.Round(duration);
        if (seconds < 1)
            seconds = 1;
        if (maxSeconds > 0 && seconds > maxSeconds)
            seconds = maxSeconds;
        return seconds;
    }

    private static string? ExtractTaskId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("output", out var output) &&
                output.TryGetProperty("task_id", out var taskId) &&
                taskId.ValueKind == JsonValueKind.String)
            {
                return taskId.GetString();
            }

            if (doc.RootElement.TryGetProperty("task_id", out var rootTaskId) &&
                rootTaskId.ValueKind == JsonValueKind.String)
            {
                return rootTaskId.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string ExtractTaskStatus(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("output", out var output) &&
                output.TryGetProperty("task_status", out var status) &&
                status.ValueKind == JsonValueKind.String)
            {
                return status.GetString() ?? string.Empty;
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

    private static bool IsSuccessStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var normalized = status.ToLowerInvariant();
        return normalized is "succeeded" or "success" or "succeed" or "completed" or "finished";
    }

    private static bool IsTerminalStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        var normalized = status.ToLowerInvariant();
        return normalized is "succeeded" or "success" or "succeed" or "failed" or "fail" or "error" or "canceled" or "cancelled" or "expired" or "completed" or "finished";
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

        throw new TimeoutException($"Qwen video generation task {taskId} timed out. Last status: {lastStatus}.");
    }

    private static async Task<string> GetTaskAsync(HttpClient httpClient, string taskId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"api/v1/tasks/{taskId}", cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? ExtractVideoUrl(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (TryExtractVideoUrl(doc.RootElement, out var url))
                return url;

            if (doc.RootElement.TryGetProperty("output", out var output) &&
                TryExtractVideoUrl(output, out url))
                return url;
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static bool TryExtractVideoUrl(JsonElement element, out string? url)
    {
        url = null;

        if (element.TryGetProperty("video_url", out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            url = direct.GetString();
            return !string.IsNullOrWhiteSpace(url);
        }

        if (element.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                if (item.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
                {
                    url = urlElement.GetString();
                    return !string.IsNullOrWhiteSpace(url);
                }

                if (item.TryGetProperty("video_url", out var videoElement) && videoElement.ValueKind == JsonValueKind.String)
                {
                    url = videoElement.GetString();
                    return !string.IsNullOrWhiteSpace(url);
                }
            }
        }

        if (element.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
        {
            if (result.TryGetProperty("url", out var urlElement) && urlElement.ValueKind == JsonValueKind.String)
            {
                url = urlElement.GetString();
                return !string.IsNullOrWhiteSpace(url);
            }
        }

        return false;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
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
