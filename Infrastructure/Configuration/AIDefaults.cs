using System.Collections.Generic;

namespace Storyboard.Infrastructure.Configuration;

/// <summary>
/// AI 默认参数配置（开发者维护）
/// </summary>
public class AIDefaults
{
    public Dictionary<string, ProviderDefaultConfig> Providers { get; set; } = new();
    public ImageGenerationDefaults Image { get; set; } = new();
    public VideoGenerationDefaults Video { get; set; } = new();
}

/// <summary>
/// Provider 默认配置
/// </summary>
public class ProviderDefaultConfig
{
    public string Endpoint { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public bool Enabled { get; set; } = true;
    public Dictionary<string, string> DefaultModels { get; set; } = new();
}

/// <summary>
/// 图像生成默认参数
/// </summary>
public class ImageGenerationDefaults
{
    public Dictionary<string, ImageProviderDefaults> Providers { get; set; } = new();
}

public class ImageProviderDefaults
{
    public string Size { get; set; } = "2K";
    public string ResponseFormat { get; set; } = "b64_json";
    public bool Watermark { get; set; } = false;
    public bool Stream { get; set; } = false;
}

/// <summary>
/// 视频生成默认参数
/// </summary>
public class VideoGenerationDefaults
{
    public Dictionary<string, VideoProviderDefaults> Providers { get; set; } = new();
}

public class VideoProviderDefaults
{
    public string Resolution { get; set; } = "1080p";
    public string Ratio { get; set; } = string.Empty;
    public int DurationSeconds { get; set; } = 0;
    public bool Watermark { get; set; } = false;
    public bool ReturnLastFrame { get; set; } = false;
    public string ServiceTier { get; set; } = "default";
    public bool GenerateAudio { get; set; } = false;
    public bool Draft { get; set; } = false;
}
