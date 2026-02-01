using Microsoft.Extensions.Logging;
using Storyboard.Models;
using Storyboard.Models.CapCut;
using Storyboard.Infrastructure.Media;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// CapCut 瀵煎嚭鏈嶅姟 - 灏嗘椂闂磋酱瀵煎嚭涓?CapCut 鑽夌鏍煎紡
/// </summary>
public interface ICapCutExportService
{
    /// <summary>
    /// 瀵煎嚭涓?CapCut 鑽夌
    /// </summary>
    /// <param name="shots">闀滃ご鍒楄〃</param>
    /// <param name="outputDirectory">杈撳嚭鐩綍</param>
    /// <param name="projectName">椤圭洰鍚嶇О</param>
    /// <param name="cancellationToken">鍙栨秷浠ょ墝</param>
    /// <returns>鑽夌鐩綍璺緞</returns>
    Task<string> ExportToCapCutAsync(
        List<ShotItem> shots,
        string outputDirectory,
        string projectName,
        CancellationToken cancellationToken = default);
}

public class CapCutExportService : ICapCutExportService
{
    private readonly ILogger<CapCutExportService> _logger;
    private const long MICROSECONDS_PER_SECOND = 1_000_000;
    private const long DEFAULT_PHOTO_DURATION = 10_800_000_000; // 3 hours in microseconds
    private const int TEXT_RENDER_INDEX = 15000;

    public CapCutExportService(ILogger<CapCutExportService> logger)
    {
        _logger = logger;
    }

    public async Task<string> ExportToCapCutAsync(
        List<ShotItem> shots,
        string outputDirectory,
        string projectName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("寮€濮嬪鍑?CapCut 鑽夌: {ProjectName}, 闀滃ご鏁? {ShotCount}", projectName, shots.Count);

            // 鍒涘缓鑽夌鐩綍
            var draftId = Guid.NewGuid().ToString("N").ToUpper();
            var draftDirectory = Path.Combine(outputDirectory, $"CapCut_Draft_{projectName}_{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(draftDirectory);

            // 鍔犺浇妯℃澘
            var templateDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "templates", "capcut");
            var contentTemplate = await LoadTemplateAsync<DraftContent>(
                Path.Combine(templateDirectory, "draft_content_template.json"));
            var metaTemplate = await LoadTemplateAsync<DraftMetaInfo>(
                Path.Combine(templateDirectory, "draft_meta_info.json"));

            // 鏋勫缓鑽夌鍐呭
            var draftContent = BuildDraftContent(contentTemplate, shots, projectName, draftId);
            var draftMetaInfo = BuildDraftMetaInfo(metaTemplate, shots, projectName, draftId, draftDirectory);

            // 澶嶅埗瑙嗛鏂囦欢鍒拌崏绋跨洰褰?
            var materialsDirectory = Path.Combine(draftDirectory, "materials");
            Directory.CreateDirectory(materialsDirectory);
            await CopyVideoMaterialsAsync(materialsDirectory, draftContent, cancellationToken);
            await CopyAudioMaterialsAsync(materialsDirectory, draftContent, cancellationToken);

            // 淇濆瓨鑽夌鏂囦欢
            await SaveDraftFilesAsync(draftDirectory, draftContent, draftMetaInfo, cancellationToken);

            _logger.LogInformation("CapCut 鑽夌瀵煎嚭鎴愬姛: {DraftDirectory}", draftDirectory);
            return draftDirectory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "瀵煎嚭 CapCut 鑽夌澶辫触");
            throw;
        }
    }

    /// <summary>
    /// 鍔犺浇妯℃澘鏂囦欢
    /// </summary>
    private async Task<T> LoadTemplateAsync<T>(string templatePath) where T : new()
    {
        if (!File.Exists(templatePath))
        {
            _logger.LogWarning("妯℃澘鏂囦欢涓嶅瓨鍦? {TemplatePath}, 浣跨敤榛樿妯℃澘", templatePath);
            return new T();
        }

        var json = await File.ReadAllTextAsync(templatePath);
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new T();
    }

    /// <summary>
    /// 鏋勫缓鑽夌鍐呭
    /// </summary>
    private DraftContent BuildDraftContent(DraftContent template, List<ShotItem> shots, string projectName, string draftId)
    {
        var content = template;
        content.Id = draftId;
        content.Name = projectName;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        content.CreateTime = now;
        content.UpdateTime = now;
        content.RenderIndexTrackModeOn = true;
        content.Path = string.Empty;
        content.CanvasConfig.Width = 1920;
        content.CanvasConfig.Height = 1080;
        content.CanvasConfig.Ratio = "original";

        content.Tracks.Clear();
        content.Materials.Videos.Clear();
        content.Materials.Speeds.Clear();
        content.Materials.Audios.Clear();
        content.Materials.Texts.Clear();

        var videoTrack = new Track
        {
            Id = Guid.NewGuid().ToString("N").ToUpper(),
            Type = "video",
            Attribute = 0,
            Flag = 0,
            Name = "video",
            IsDefaultName = false,
            Segments = new List<Segment>()
        };

        var audioTrack = new Track
        {
            Id = Guid.NewGuid().ToString("N").ToUpper(),
            Type = "audio",
            Attribute = 0,
            Flag = 0,
            Name = "audio",
            IsDefaultName = false,
            Segments = new List<Segment>()
        };

        var textTrack = new Track
        {
            Id = Guid.NewGuid().ToString("N").ToUpper(),
            Type = "text",
            Attribute = 0,
            Flag = 3,
            Name = "text",
            IsDefaultName = false,
            Segments = new List<Segment>()
        };

        var hasAudio = false;
        var hasText = false;

        long currentTime = 0;
        foreach (var shot in shots.OrderBy(s => s.ShotNumber))
        {
            var videoPath = shot.GeneratedVideoPath;
            var hasVideo = !string.IsNullOrWhiteSpace(videoPath) && File.Exists(videoPath);
            var imagePath = !hasVideo ? TryGetImagePath(shot) : null;

            if (!hasVideo && string.IsNullOrWhiteSpace(imagePath))
            {
                _logger.LogWarning("璺宠繃鏈敓鎴愮殑闀滃ご: Shot #{ShotNumber}", shot.ShotNumber);
                continue;
            }

            var materialId = Guid.NewGuid().ToString("N").ToUpper();
            var fallbackDurationMicroseconds = (long)(shot.Duration * MICROSECONDS_PER_SECOND);
            var shotStartTime = currentTime;
            var speedId = Guid.NewGuid().ToString("N").ToUpper();

            var materialPath = hasVideo ? videoPath! : imagePath!;
            var fileName = Path.GetFileName(materialPath);
            var segmentDurationMicroseconds = fallbackDurationMicroseconds;
            var materialDurationMicroseconds = fallbackDurationMicroseconds;
            var materialWidth = 1920;
            var materialHeight = 1080;
            var materialType = "video";

            if (hasVideo)
            {
                var probe = TryProbeMedia(materialPath);
                if (probe != null)
                {
                    segmentDurationMicroseconds = (long)(probe.DurationSeconds * MICROSECONDS_PER_SECOND);
                    materialDurationMicroseconds = segmentDurationMicroseconds;
                    if (probe.Width > 0) materialWidth = probe.Width;
                    if (probe.Height > 0) materialHeight = probe.Height;
                }
            }
            else
            {
                materialType = "photo";
                materialDurationMicroseconds = DEFAULT_PHOTO_DURATION;
                var (imageWidth, imageHeight) = TryGetImageSize(materialPath);
                if (imageWidth > 0) materialWidth = imageWidth;
                if (imageHeight > 0) materialHeight = imageHeight;
            }

            content.Materials.Videos.Add(new VideoMaterial
            {
                Id = materialId,
                MaterialId = materialId,
                LocalMaterialId = string.Empty,
                MaterialName = fileName,
                Path = materialPath,
                Type = materialType,
                Duration = materialDurationMicroseconds,
                Width = materialWidth,
                Height = materialHeight,
                CategoryId = string.Empty,
                CategoryName = "local",
                MediaPath = string.Empty,
                CheckFlag = 63487,
                Crop = new Crop(),
                CropRatio = "free",
                CropScale = 1.0,
                AudioFade = null
            });

            content.Materials.Speeds.Add(new SpeedMaterial
            {
                Id = speedId,
                Speed = 1.0
            });

            videoTrack.Segments.Add(new Segment
            {
                Id = Guid.NewGuid().ToString("N").ToUpper(),
                MaterialId = materialId,
                TargetTimerange = new TimeRange
                {
                    Start = currentTime,
                    Duration = segmentDurationMicroseconds
                },
                SourceTimerange = new TimeRange
                {
                    Start = 0,
                    Duration = segmentDurationMicroseconds
                },
                Speed = 1.0,
                Volume = 1.0,
                ExtraMaterialRefs = new List<string> { speedId },
                RenderIndex = 0,
                TrackRenderIndex = 0,
                Clip = new Clip
                {
                    Alpha = 1.0,
                    Rotation = 0.0,
                    Scale = new Scale { X = 1.0, Y = 1.0 },
                    Transform = new Transform { X = 0.0, Y = 0.0 },
                    Flip = new Flip { Horizontal = false, Vertical = false }
                },
                UniformScale = new UniformScale { On = true, Value = 1.0 },
                HdrSettings = new HdrSettings { Intensity = 1.0, Mode = 1, Nits = 1000 }
            });

            if (hasVideo)
            {
                var audioPath = TryGetAudioPath(shot);
                if (!string.IsNullOrWhiteSpace(audioPath) && File.Exists(audioPath))
                {
                    hasAudio = true;
                    var audioId = Guid.NewGuid().ToString("N").ToUpper();
                    var audioSpeedId = Guid.NewGuid().ToString("N").ToUpper();
                    var audioName = Path.GetFileName(audioPath);
                    var audioProbe = TryProbeMedia(audioPath);
                    var audioDurationMicroseconds = audioProbe != null
                        ? (long)(audioProbe.DurationSeconds * MICROSECONDS_PER_SECOND)
                        : segmentDurationMicroseconds;

                    content.Materials.Audios.Add(BuildAudioMaterial(audioId, audioName, audioPath, audioDurationMicroseconds));
                    content.Materials.Speeds.Add(new SpeedMaterial
                    {
                        Id = audioSpeedId,
                        Speed = 1.0
                    });

                    audioTrack.Segments.Add(new Segment
                    {
                        Id = Guid.NewGuid().ToString("N").ToUpper(),
                        MaterialId = audioId,
                        TargetTimerange = new TimeRange
                        {
                            Start = shotStartTime,
                            Duration = segmentDurationMicroseconds
                        },
                        SourceTimerange = new TimeRange
                        {
                            Start = 0,
                            Duration = segmentDurationMicroseconds
                        },
                        Speed = 1.0,
                        Volume = 1.0,
                        ExtraMaterialRefs = new List<string> { audioSpeedId },
                        RenderIndex = 0,
                        TrackRenderIndex = 0,
                        Clip = null,
                        UniformScale = null,
                        HdrSettings = null
                    });
                }
            }

            foreach (var subtitle in GetSubtitleSegments(shot))
            {
                hasText = true;
                var textMaterialId = Guid.NewGuid().ToString("N").ToUpper();
                var textSpeedId = Guid.NewGuid().ToString("N").ToUpper();
                content.Materials.Texts.Add(BuildTextMaterial(textMaterialId, subtitle.Text));

                var subtitleStart = shotStartTime + (long)(subtitle.StartSeconds * MICROSECONDS_PER_SECOND);
                var subtitleDuration = (long)((subtitle.EndSeconds - subtitle.StartSeconds) * MICROSECONDS_PER_SECOND);
                if (subtitleDuration <= 0)
                {
                    continue;
                }

                textTrack.Segments.Add(new Segment
                {
                    Id = Guid.NewGuid().ToString("N").ToUpper(),
                    MaterialId = textMaterialId,
                    TargetTimerange = new TimeRange
                    {
                        Start = subtitleStart,
                        Duration = subtitleDuration
                    },
                    SourceTimerange = null,
                    Speed = 1.0,
                    Volume = 1.0,
                    ExtraMaterialRefs = new List<string> { textSpeedId },
                    RenderIndex = TEXT_RENDER_INDEX,
                    TrackRenderIndex = 0,
                    Clip = new Clip
                    {
                        Alpha = 1.0,
                        Rotation = 0.0,
                        Scale = new Scale { X = 1.0, Y = 1.0 },
                        Transform = new Transform { X = 0.0, Y = -0.8 },
                        Flip = new Flip { Horizontal = false, Vertical = false }
                    },
                    UniformScale = new UniformScale { On = true, Value = 1.0 },
                    HdrSettings = null
                });
            }

            currentTime += segmentDurationMicroseconds;
        }

        content.Duration = currentTime;
        content.Tracks.Add(videoTrack);
        if (hasAudio)
        {
            content.Tracks.Add(audioTrack);
        }
        content.Tracks.Add(textTrack);

        _logger.LogInformation("鏋勫缓鑽夌鍐呭瀹屾垚: 杞ㄩ亾鏁?{TrackCount}, 鐗囨鏁?{SegmentCount}",
            content.Tracks.Count, videoTrack.Segments.Count);

        return content;
    }

    /// <summary>
    /// 鏋勫缓鑽夌鍏冧俊鎭?
    /// </summary>
    private DraftMetaInfo BuildDraftMetaInfo(
        DraftMetaInfo template,
        List<ShotItem> shots,
        string projectName,
        string draftId,
        string draftDirectory)
    {
        var metaInfo = template;
        metaInfo.DraftId = draftId;
        metaInfo.DraftName = projectName;
        metaInfo.DraftFoldPath = draftDirectory;
        metaInfo.DraftRootPath = Path.GetDirectoryName(draftDirectory) ?? draftDirectory;
        metaInfo.DraftRemovableStorageDevice = (Path.GetPathRoot(draftDirectory) ?? string.Empty)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        metaInfo.DraftNeedRenameFolder = false;
        metaInfo.DraftTimelineMaterialsSize = 0;

        var totalDurationSeconds = shots.Sum(s => s.Duration);
        metaInfo.TmDuration = (long)(totalDurationSeconds * MICROSECONDS_PER_SECOND);

        var nowMicro = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
        metaInfo.TmDraftCreate = nowMicro;
        metaInfo.TmDraftModified = nowMicro;

        return metaInfo;
    }

    /// <summary>
    /// 澶嶅埗瑙嗛绱犳潗鍒拌崏绋跨洰褰?
    /// </summary>
    private async Task CopyVideoMaterialsAsync(
        string materialsDirectory,
        DraftContent draftContent,
        CancellationToken cancellationToken)
    {
        foreach (var material in draftContent.Materials.Videos)
        {
            if (string.IsNullOrWhiteSpace(material.Path) || !File.Exists(material.Path))
                continue;

            var fileName = Path.GetFileName(material.Path);
            var destPath = Path.Combine(materialsDirectory, fileName);

            await Task.Run(() => File.Copy(material.Path, destPath, overwrite: true), cancellationToken);
            material.Path = destPath;

            _logger.LogDebug("澶嶅埗瑙嗛/鍥剧墖绱犳潗: {FileName}", fileName);
        }
    }

    private async Task CopyAudioMaterialsAsync(
        string materialsDirectory,
        DraftContent draftContent,
        CancellationToken cancellationToken)
    {
        foreach (var audioMaterial in draftContent.Materials.Audios)
        {
            if (audioMaterial is not Dictionary<string, object?> dict)
                continue;

            if (!dict.TryGetValue("path", out var pathObj))
                continue;

            if (pathObj is not string sourcePath || string.IsNullOrWhiteSpace(sourcePath))
                continue;

            if (!File.Exists(sourcePath))
                continue;

            var fileName = Path.GetFileName(sourcePath);
            var destPath = Path.Combine(materialsDirectory, fileName);

            await Task.Run(() => File.Copy(sourcePath, destPath, overwrite: true), cancellationToken);
            dict["path"] = destPath;

            _logger.LogDebug("澶嶅埗闊抽绱犳潗: {FileName}", fileName);
        }
    }

    /// <summary>
    /// 淇濆瓨鑽夌鏂囦欢
    /// </summary>
    private async Task SaveDraftFilesAsync(
        string draftDirectory,
        DraftContent draftContent,
        DraftMetaInfo draftMetaInfo,
        CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
        };

        // 淇濆瓨 draft_content.json
        var contentPath = Path.Combine(draftDirectory, "draft_content.json");
        var contentJson = JsonSerializer.Serialize(draftContent, options);
        await File.WriteAllTextAsync(contentPath, contentJson, cancellationToken);

        // 淇濆瓨 draft_meta_info.json
        var metaPath = Path.Combine(draftDirectory, "draft_meta_info.json");
        var metaJson = JsonSerializer.Serialize(draftMetaInfo, options);
        await File.WriteAllTextAsync(metaPath, metaJson, cancellationToken);

        _logger.LogInformation("鑽夌鏂囦欢淇濆瓨瀹屾垚: {ContentPath}, {MetaPath}", contentPath, metaPath);
    }

    private static string? TryGetAudioPath(ShotItem shot)
    {
        var fromProperty = TryGetStringProperty(shot, "GeneratedAudioPath", "AudioPath", "AudioFilePath", "VoicePath", "VoiceoverPath");
        if (!string.IsNullOrWhiteSpace(fromProperty) && File.Exists(fromProperty))
        {
            return fromProperty;
        }

        if (!string.IsNullOrWhiteSpace(shot.GeneratedVideoPath))
        {
            var basePath = Path.Combine(Path.GetDirectoryName(shot.GeneratedVideoPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(shot.GeneratedVideoPath));
            var candidates = new[] { ".wav", ".mp3", ".m4a", ".aac" };
            foreach (var ext in candidates)
            {
                var audioPath = basePath + ext;
                if (File.Exists(audioPath))
                {
                    return audioPath;
                }
            }
        }

        return null;
    }

    private static string? TryGetImagePath(ShotItem shot)
    {
        var fromProperty = TryGetStringProperty(shot, "ImagePath", "FirstFrameImagePath", "LastFrameImagePath");
        if (!string.IsNullOrWhiteSpace(fromProperty) && File.Exists(fromProperty))
        {
            return fromProperty;
        }

        return null;
    }

    private MediaProbe? TryProbeMedia(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        var args = $"-v error -print_format json -show_format -show_streams \"{path}\"";
        var ffprobePath = FfmpegLocator.GetFfprobePath();

        if (!TryRunProcess(ffprobePath, args, out var stdout, out var stderr) &&
            !TryRunProcess("ffprobe", args, out stdout, out stderr))
        {
            _logger.LogWarning("ffprobe 失败，无法解析媒体信息: {Path}. {Error}", path, stderr);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            double duration = 0;
            if (doc.RootElement.TryGetProperty("format", out var format) &&
                format.TryGetProperty("duration", out var durEl) &&
                double.TryParse(durEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
            {
                duration = dur;
            }

            int width = 0;
            int height = 0;
            if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in streams.EnumerateArray())
                {
                    if (s.TryGetProperty("codec_type", out var typeEl) && typeEl.GetString() == "video")
                    {
                        if (s.TryGetProperty("width", out var wEl) && wEl.TryGetInt32(out var w)) width = w;
                        if (s.TryGetProperty("height", out var hEl) && hEl.TryGetInt32(out var h)) height = h;
                        break;
                    }
                }
            }

            if (duration <= 0)
                return null;

            return new MediaProbe(duration, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "解析媒体信息失败: {Path}", path);
            return null;
        }
    }

    private static (int Width, int Height) TryGetImageSize(string path)
    {
        try
        {
            using var bitmap = SKBitmap.Decode(path);
            if (bitmap == null)
                return (0, 0);

            return (bitmap.Width, bitmap.Height);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static bool TryRunProcess(string fileName, string arguments, out string stdout, out string stderr)
    {
        stdout = string.Empty;
        stderr = string.Empty;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                stderr = "无法启动 ffprobe 进程";
                return false;
            }

            stdout = proc.StandardOutput.ReadToEnd();
            stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return proc.ExitCode == 0;
        }
        catch (Exception ex)
        {
            stderr = ex.Message;
            return false;
        }
    }

    private static List<SubtitleSegment> GetSubtitleSegments(ShotItem shot)
    {
        var segments = TryGetSubtitleSegmentsFromProperty(shot);
        if (segments.Count > 0)
        {
            return segments;
        }

        var subtitlePath = TryGetStringProperty(shot, "SubtitlePath", "CaptionPath", "SrtPath", "VttPath");
        if (string.IsNullOrWhiteSpace(subtitlePath) && !string.IsNullOrWhiteSpace(shot.GeneratedVideoPath))
        {
            var basePath = Path.Combine(Path.GetDirectoryName(shot.GeneratedVideoPath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(shot.GeneratedVideoPath));
            var srtPath = basePath + ".srt";
            var vttPath = basePath + ".vtt";
            if (File.Exists(srtPath))
            {
                subtitlePath = srtPath;
            }
            else if (File.Exists(vttPath))
            {
                subtitlePath = vttPath;
            }
        }

        if (string.IsNullOrWhiteSpace(subtitlePath) || !File.Exists(subtitlePath))
        {
            return new List<SubtitleSegment>();
        }

        var extension = Path.GetExtension(subtitlePath).ToLowerInvariant();
        return extension == ".vtt" ? ParseVtt(subtitlePath) : ParseSrt(subtitlePath);
    }

    private static List<SubtitleSegment> TryGetSubtitleSegmentsFromProperty(ShotItem shot)
    {
        var segments = new List<SubtitleSegment>();
        var source = TryGetObjectProperty(shot, "SubtitleSegments", "Subtitles", "Captions", "CaptionSegments");
        if (source == null)
        {
            return segments;
        }

        if (source is System.Collections.IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item == null)
                {
                    continue;
                }

                var text = TryGetStringProperty(item, "Text", "Content", "Caption", "Subtitle");
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var start = TryGetTimeValue(item, "Start", "StartTime", "StartSeconds", "StartMs");
                var end = TryGetTimeValue(item, "End", "EndTime", "EndSeconds", "EndMs");
                if (end <= start)
                {
                    var duration = TryGetTimeValue(item, "Duration", "DurationSeconds", "DurationMs");
                    if (duration > 0)
                    {
                        end = start + duration;
                    }
                }

                if (end > start)
                {
                    segments.Add(new SubtitleSegment(start, end, text));
                }
            }
        }

        return segments;
    }

    private static Dictionary<string, object?> BuildAudioMaterial(string id, string name, string path, long duration)
    {
        return new Dictionary<string, object?>
        {
            ["app_id"] = 0,
            ["category_id"] = "",
            ["category_name"] = "local",
            ["check_flag"] = 1,
            ["copyright_limit_type"] = "none",
            ["duration"] = duration,
            ["effect_id"] = "",
            ["formula_id"] = "",
            ["id"] = id,
            ["intensifies_path"] = "",
            ["is_ai_clone_tone"] = false,
            ["is_text_edit_overdub"] = false,
            ["is_ugc"] = false,
            ["local_material_id"] = id,
            ["music_id"] = id,
            ["name"] = name,
            ["path"] = path,
            ["query"] = "",
            ["request_id"] = "",
            ["resource_id"] = "",
            ["search_id"] = "",
            ["source_from"] = "",
            ["source_platform"] = 0,
            ["team_id"] = "",
            ["text_id"] = "",
            ["tone_category_id"] = "",
            ["tone_category_name"] = "",
            ["tone_effect_id"] = "",
            ["tone_effect_name"] = "",
            ["tone_platform"] = "",
            ["tone_second_category_id"] = "",
            ["tone_second_category_name"] = "",
            ["tone_speaker"] = "",
            ["tone_type"] = "",
            ["type"] = "extract_music",
            ["video_id"] = "",
            ["wave_points"] = new List<object>()
        };
    }

    private static Dictionary<string, object?> BuildTextMaterial(string id, string text)
    {
        var contentPayload = new Dictionary<string, object?>
        {
            ["styles"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["fill"] = new Dictionary<string, object?>
                    {
                        ["alpha"] = 1.0,
                        ["content"] = new Dictionary<string, object?>
                        {
                            ["render_type"] = "solid",
                            ["solid"] = new Dictionary<string, object?>
                            {
                                ["alpha"] = 1.0,
                                ["color"] = new[] { 1.0, 1.0, 1.0 }
                            }
                        }
                    },
                    ["range"] = new[] { 0, text.Length },
                    ["size"] = 8.0,
                    ["bold"] = false,
                    ["italic"] = false,
                    ["underline"] = false,
                    ["strokes"] = Array.Empty<object>()
                }
            },
            ["text"] = text
        };

        var contentJson = JsonSerializer.Serialize(contentPayload, new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return new Dictionary<string, object?>
        {
            ["id"] = id,
            ["content"] = contentJson,
            ["typesetting"] = 0,
            ["alignment"] = 0,
            ["letter_spacing"] = 0.0,
            ["line_spacing"] = 0.02,
            ["line_feed"] = 1,
            ["line_max_width"] = 0.82,
            ["force_apply_line_max_width"] = false,
            ["check_flag"] = 7,
            ["type"] = "text"
        };
    }

    private static string? TryGetStringProperty(object target, params string[] names)
    {
        var type = target.GetType();
        foreach (var name in names)
        {
            var prop = type.GetProperty(name);
            if (prop == null || prop.PropertyType != typeof(string))
            {
                continue;
            }

            var value = prop.GetValue(target) as string;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static object? TryGetObjectProperty(object target, params string[] names)
    {
        var type = target.GetType();
        foreach (var name in names)
        {
            var prop = type.GetProperty(name);
            if (prop == null)
            {
                continue;
            }

            var value = prop.GetValue(target);
            if (value != null)
            {
                return value;
            }
        }

        return null;
    }

    private static double TryGetTimeValue(object target, params string[] names)
    {
        var type = target.GetType();
        foreach (var name in names)
        {
            var prop = type.GetProperty(name);
            if (prop == null)
            {
                continue;
            }

            var value = prop.GetValue(target);
            if (value == null)
            {
                continue;
            }

            if (value is TimeSpan ts)
            {
                return ts.TotalSeconds;
            }

            if (value is int i)
            {
                return NormalizeTimeValue(name, i);
            }

            if (value is long l)
            {
                return NormalizeTimeValue(name, l);
            }

            if (value is double d)
            {
                return NormalizeTimeValue(name, d);
            }

            if (value is float f)
            {
                return NormalizeTimeValue(name, (double)f);
            }
        }

        return 0;
    }

    private static double NormalizeTimeValue(string name, double value)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("ms"))
        {
            return value / 1000.0;
        }

        return value;
    }

    private static List<SubtitleSegment> ParseSrt(string path)
    {
        var lines = File.ReadAllLines(path);
        var segments = new List<SubtitleSegment>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            if (int.TryParse(line, out _))
            {
                index++;
                if (index >= lines.Length)
                {
                    break;
                }
                line = lines[index].Trim();
            }

            var match = Regex.Match(line, @"(?<start>\\d{1,2}:\\d{2}:\\d{2}[,\\.]\\d{1,3})\\s*-->\\s*(?<end>\\d{1,2}:\\d{2}:\\d{2}[,\\.]\\d{1,3})");
            if (!match.Success)
            {
                index++;
                continue;
            }

            var start = ParseTimestamp(match.Groups["start"].Value);
            var end = ParseTimestamp(match.Groups["end"].Value);
            index++;

            var textLines = new List<string>();
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
            {
                textLines.Add(lines[index]);
                index++;
            }

            var text = string.Join("\n", textLines).Trim();
            if (!string.IsNullOrWhiteSpace(text) && end > start)
            {
                segments.Add(new SubtitleSegment(start, end, text));
            }
        }

        return segments;
    }

    private static List<SubtitleSegment> ParseVtt(string path)
    {
        var lines = File.ReadAllLines(path);
        var segments = new List<SubtitleSegment>();
        var index = 0;

        while (index < lines.Length)
        {
            var line = lines[index].Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                continue;
            }

            var match = Regex.Match(line, @"(?<start>\\d{1,2}:\\d{2}(?::\\d{2})?[\\.]\\d{1,3})\\s*-->\\s*(?<end>\\d{1,2}:\\d{2}(?::\\d{2})?[\\.]\\d{1,3})");
            if (!match.Success)
            {
                index++;
                continue;
            }

            var start = ParseTimestamp(match.Groups["start"].Value);
            var end = ParseTimestamp(match.Groups["end"].Value);
            index++;

            var textLines = new List<string>();
            while (index < lines.Length && !string.IsNullOrWhiteSpace(lines[index]))
            {
                textLines.Add(lines[index]);
                index++;
            }

            var text = string.Join("\n", textLines).Trim();
            if (!string.IsNullOrWhiteSpace(text) && end > start)
            {
                segments.Add(new SubtitleSegment(start, end, text));
            }
        }

        return segments;
    }

    private static double ParseTimestamp(string raw)
    {
        var normalized = raw.Replace(',', '.');
        var parts = normalized.Split(':');
        if (parts.Length == 3)
        {
            var hours = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var minutes = int.Parse(parts[1], CultureInfo.InvariantCulture);
            var seconds = double.Parse(parts[2], CultureInfo.InvariantCulture);
            return hours * 3600 + minutes * 60 + seconds;
        }

        if (parts.Length == 2)
        {
            var minutes = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var seconds = double.Parse(parts[1], CultureInfo.InvariantCulture);
            return minutes * 60 + seconds;
        }

        return 0;
    }

    private sealed record MediaProbe(double DurationSeconds, int Width, int Height);

    private sealed record SubtitleSegment(double StartSeconds, double EndSeconds, string Text);
}
