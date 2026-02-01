using Storyboard.Models;
using Storyboard.Models.CapCut;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// 鏁版嵁閫傞厤鍣?- 鍦?ShotItem 鍜?CapCut 鏍煎紡涔嬮棿杞崲
/// </summary>
public static class DraftAdapter
{
    private const long MICROSECONDS_PER_SECOND = 1_000_000;

    /// <summary>
    /// 灏?ShotItem 鍒楄〃杞崲涓?DraftContent
    /// </summary>
    public static void SyncShotsToDraft(List<ShotItem> shots, DraftContent draft)
    {
        draft.Tracks.Clear();
        draft.Materials.Videos.Clear();
        draft.Materials.Speeds.Clear();

        if (shots == null || shots.Count == 0)
        {
            draft.Duration = 0;
            return;
        }

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

        long currentTime = 0;
        double totalDurationSeconds = 0;

        foreach (var shot in shots.OrderBy(s => s.ShotNumber))
        {
            if (string.IsNullOrEmpty(shot.GeneratedVideoPath) || !File.Exists(shot.GeneratedVideoPath))
            {
                continue;
            }

            var materialId = Guid.NewGuid().ToString("N").ToUpper();
            var durationMicroseconds = (long)(shot.Duration * MICROSECONDS_PER_SECOND);
            var fileName = Path.GetFileName(shot.GeneratedVideoPath);
            var speedId = Guid.NewGuid().ToString("N").ToUpper();

            draft.Materials.Videos.Add(new VideoMaterial
            {
                Id = materialId,
                MaterialId = materialId,
                LocalMaterialId = string.Empty,
                MaterialName = fileName,
                Path = shot.GeneratedVideoPath,
                Type = "video",
                Duration = durationMicroseconds,
                Width = 1920,
                Height = 1080,
                CategoryId = string.Empty,
                CategoryName = "local",
                MediaPath = string.Empty,
                CheckFlag = 63487,
                Crop = new Crop(),
                CropRatio = "free",
                CropScale = 1.0,
                AudioFade = null
            });

            draft.Materials.Speeds.Add(new SpeedMaterial
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
                    Duration = durationMicroseconds
                },
                SourceTimerange = new TimeRange
                {
                    Start = 0,
                    Duration = durationMicroseconds
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

            currentTime += durationMicroseconds;
            totalDurationSeconds += shot.Duration;
        }

        draft.Tracks.Add(videoTrack);
        draft.Duration = (long)(totalDurationSeconds * MICROSECONDS_PER_SECOND);
    }

    /// <summary>
    /// 浠?DraftContent 鎻愬彇鏃堕棿杞翠俊鎭?
    /// </summary>
    public static TimelineInfo ExtractTimelineInfo(DraftContent draft)
    {
        var info = new TimelineInfo
        {
            TotalDurationSeconds = draft.Duration / (double)MICROSECONDS_PER_SECOND,
            Fps = draft.Fps,
            Width = draft.CanvasConfig.Width,
            Height = draft.CanvasConfig.Height,
            Tracks = new List<TrackInfo>()
        };

        foreach (var track in draft.Tracks)
        {
            var trackInfo = new TrackInfo
            {
                Id = track.Id,
                Type = track.Type,
                Segments = new List<SegmentInfo>()
            };

            foreach (var segment in track.Segments)
            {
                var material = draft.Materials.Videos.FirstOrDefault(v => v.MaterialId == segment.MaterialId || v.Id == segment.MaterialId);
                var sourceRange = segment.SourceTimerange ?? segment.TargetTimerange;

                trackInfo.Segments.Add(new SegmentInfo
                {
                    Id = segment.Id,
                    MaterialId = segment.MaterialId,
                    StartTimeSeconds = segment.TargetTimerange.Start / (double)MICROSECONDS_PER_SECOND,
                    DurationSeconds = segment.TargetTimerange.Duration / (double)MICROSECONDS_PER_SECOND,
                    SourceStartSeconds = sourceRange.Start / (double)MICROSECONDS_PER_SECOND,
                    SourceDurationSeconds = sourceRange.Duration / (double)MICROSECONDS_PER_SECOND,
                    VideoPath = material?.Path ?? string.Empty,
                    Width = material?.Width ?? 1920,
                    Height = material?.Height ?? 1080
                });
            }

            info.Tracks.Add(trackInfo);
        }

        return info;
    }

    /// <summary>
    /// 娣诲姞鐗囨鍒拌崏绋?
    /// </summary>
    public static void AddSegmentToDraft(DraftContent draft, ShotItem shot, double startTimeSeconds)
    {
        if (string.IsNullOrEmpty(shot.GeneratedVideoPath) || !File.Exists(shot.GeneratedVideoPath))
        {
            return;
        }

        var videoTrack = draft.Tracks.FirstOrDefault(t => t.Type == "video");
        if (videoTrack == null)
        {
            videoTrack = new Track
            {
                Id = Guid.NewGuid().ToString("N").ToUpper(),
                Type = "video",
                Attribute = 0,
                Flag = 0,
                Name = "video",
                IsDefaultName = false,
                Segments = new List<Segment>()
            };
            draft.Tracks.Add(videoTrack);
        }

        var materialId = Guid.NewGuid().ToString("N").ToUpper();
        var durationMicroseconds = (long)(shot.Duration * MICROSECONDS_PER_SECOND);
        var startMicroseconds = (long)(startTimeSeconds * MICROSECONDS_PER_SECOND);
        var fileName = Path.GetFileName(shot.GeneratedVideoPath);
        var speedId = Guid.NewGuid().ToString("N").ToUpper();

        draft.Materials.Videos.Add(new VideoMaterial
        {
            Id = materialId,
            MaterialId = materialId,
            LocalMaterialId = string.Empty,
            MaterialName = fileName,
            Path = shot.GeneratedVideoPath,
            Type = "video",
            Duration = durationMicroseconds,
            Width = 1920,
            Height = 1080,
            CategoryId = string.Empty,
            CategoryName = "local",
            MediaPath = string.Empty,
            CheckFlag = 63487,
            Crop = new Crop(),
            CropRatio = "free",
            CropScale = 1.0,
            AudioFade = null
        });

        draft.Materials.Speeds.Add(new SpeedMaterial
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
                Start = startMicroseconds,
                Duration = durationMicroseconds
            },
            SourceTimerange = new TimeRange
            {
                Start = 0,
                Duration = durationMicroseconds
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

        UpdateDraftDuration(draft);
    }

    /// <summary>
    /// 浠庤崏绋夸腑鍒犻櫎鐗囨
    /// </summary>
    public static void RemoveSegmentFromDraft(DraftContent draft, string segmentId)
    {
        foreach (var track in draft.Tracks)
        {
            var segment = track.Segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                track.Segments.Remove(segment);

                var material = draft.Materials.Videos.FirstOrDefault(v => v.MaterialId == segment.MaterialId || v.Id == segment.MaterialId);
                if (material != null)
                {
                    draft.Materials.Videos.Remove(material);
                }

                if (segment.ExtraMaterialRefs != null && segment.ExtraMaterialRefs.Count > 0)
                {
                    draft.Materials.Speeds.RemoveAll(s => segment.ExtraMaterialRefs.Contains(s.Id));
                }

                UpdateDraftDuration(draft);
                break;
            }
        }
    }

    /// <summary>
    /// 鏇存柊鐗囨浣嶇疆
    /// </summary>
    public static void UpdateSegmentPosition(DraftContent draft, string segmentId, double newStartTimeSeconds)
    {
        foreach (var track in draft.Tracks)
        {
            var segment = track.Segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.TargetTimerange.Start = (long)(newStartTimeSeconds * MICROSECONDS_PER_SECOND);
                UpdateDraftDuration(draft);
                break;
            }
        }
    }

    /// <summary>
    /// 淇壀鐗囨锛堝悓鏃舵洿鏂扮洰鏍囧拰婧愭椂闂磋寖鍥达級
    /// </summary>
    public static bool UpdateSegmentTrim(
        DraftContent draft,
        string segmentId,
        double newStartTimeSeconds,
        double newDurationSeconds,
        double newSourceStartSeconds,
        double newSourceDurationSeconds)
    {
        foreach (var track in draft.Tracks)
        {
            var segment = track.Segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                segment.TargetTimerange.Start = (long)(newStartTimeSeconds * MICROSECONDS_PER_SECOND);
                segment.TargetTimerange.Duration = (long)(newDurationSeconds * MICROSECONDS_PER_SECOND);
                segment.SourceTimerange ??= new TimeRange();
                segment.SourceTimerange.Start = (long)(newSourceStartSeconds * MICROSECONDS_PER_SECOND);
                segment.SourceTimerange.Duration = (long)(newSourceDurationSeconds * MICROSECONDS_PER_SECOND);

                UpdateDraftDuration(draft);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 鍒囧壊鐗囨锛堣繑鍥炴柊鐗囨 ID锛?    /// </summary>
    public static string? SplitSegment(DraftContent draft, string segmentId, double splitTimeSeconds)
    {
        foreach (var track in draft.Tracks)
        {
            var index = track.Segments.FindIndex(s => s.Id == segmentId);
            if (index < 0)
            {
                continue;
            }

            var segment = track.Segments[index];
            var segmentStartSeconds = segment.TargetTimerange.Start / (double)MICROSECONDS_PER_SECOND;
            var segmentDurationSeconds = segment.TargetTimerange.Duration / (double)MICROSECONDS_PER_SECOND;
            var splitOffsetSeconds = splitTimeSeconds - segmentStartSeconds;

            if (splitOffsetSeconds <= 0 || splitOffsetSeconds >= segmentDurationSeconds)
            {
                return null;
            }

            var newSegmentId = Guid.NewGuid().ToString("N").ToUpper();
            var remainingDurationSeconds = segmentDurationSeconds - splitOffsetSeconds;

            segment.TargetTimerange.Duration = (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND);
            if (segment.SourceTimerange != null)
            {
                segment.SourceTimerange.Duration = (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND);
            }

            var newSegment = new Segment
            {
                Id = newSegmentId,
                MaterialId = segment.MaterialId,
                TargetTimerange = new TimeRange
                {
                    Start = segment.TargetTimerange.Start + (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND),
                    Duration = (long)(remainingDurationSeconds * MICROSECONDS_PER_SECOND)
                },
                SourceTimerange = segment.SourceTimerange == null ? null : new TimeRange
                {
                    Start = segment.SourceTimerange.Start + (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND),
                    Duration = (long)(remainingDurationSeconds * MICROSECONDS_PER_SECOND)
                },
                Speed = segment.Speed,
                Volume = segment.Volume,
                ExtraMaterialRefs = segment.ExtraMaterialRefs != null ? new List<string>(segment.ExtraMaterialRefs) : new List<string>(),
                RenderIndex = segment.RenderIndex,
                TrackRenderIndex = segment.TrackRenderIndex,
                TrackAttribute = segment.TrackAttribute,
                Clip = CloneClip(segment.Clip),
                UniformScale = segment.UniformScale,
                HdrSettings = segment.HdrSettings
            };

            track.Segments.Insert(index + 1, newSegment);
            UpdateDraftDuration(draft);
            return newSegmentId;
        }

        return null;
    }

    /// <summary>
    /// 鏇存柊鑽夌鎬绘椂闀?
    /// </summary>
    private static void UpdateDraftDuration(DraftContent draft)
    {
        long maxEndTime = 0;

        foreach (var track in draft.Tracks)
        {
            foreach (var segment in track.Segments)
            {
                var endTime = segment.TargetTimerange.Start + segment.TargetTimerange.Duration;
                if (endTime > maxEndTime)
                {
                    maxEndTime = endTime;
                }
            }
        }

        draft.Duration = maxEndTime;
    }

    /// <summary>
    /// 娣辨嫹璐濈墖娈靛壀杈戜俊鎭?    /// </summary>
    private static Clip? CloneClip(Clip? clip)
    {
        if (clip == null)
            return null;

        return new Clip
        {
            Alpha = clip.Alpha,
            Rotation = clip.Rotation,
            Flip = new Flip
            {
                Horizontal = clip.Flip.Horizontal,
                Vertical = clip.Flip.Vertical
            },
            Scale = new Scale
            {
                X = clip.Scale.X,
                Y = clip.Scale.Y
            },
            Transform = new Transform
            {
                X = clip.Transform.X,
                Y = clip.Transform.Y
            }
        };
    }

    /// <summary>
    /// 绉掕浆寰
    /// </summary>
    public static long SecondsToMicroseconds(double seconds)
    {
        return (long)(seconds * MICROSECONDS_PER_SECOND);
    }

    /// <summary>
    /// 寰杞
    /// </summary>
    public static double MicrosecondsToSeconds(long microseconds)
    {
        return microseconds / (double)MICROSECONDS_PER_SECOND;
    }
}

/// <summary>
/// 鏃堕棿杞翠俊鎭紙浠?DraftContent 鎻愬彇鐨勭畝鍖栬鍥撅級
/// </summary>
public class TimelineInfo
{
    public double TotalDurationSeconds { get; set; }
    public double Fps { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<TrackInfo> Tracks { get; set; } = new();
}

/// <summary>
/// 杞ㄩ亾淇℃伅
/// </summary>
public class TrackInfo
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<SegmentInfo> Segments { get; set; } = new();
}

/// <summary>
/// 鐗囨淇℃伅
/// </summary>
public class SegmentInfo
{
    public string Id { get; set; } = string.Empty;
    public string MaterialId { get; set; } = string.Empty;
    public double StartTimeSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public double SourceStartSeconds { get; set; }
    public double SourceDurationSeconds { get; set; }
    public double EndTimeSeconds => StartTimeSeconds + DurationSeconds;
    public string VideoPath { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
}
