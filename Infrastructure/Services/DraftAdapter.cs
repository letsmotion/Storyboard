using Storyboard.Models;
using Storyboard.Models.CapCut;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// 数据适配器 - 在 ShotItem 和 CapCut 格式之间转换
/// </summary>
public static class DraftAdapter
{
    private const long MICROSECONDS_PER_SECOND = 1_000_000;

    /// <summary>
    /// 将 ShotItem 列表转换为 DraftContent
    /// </summary>
    public static void SyncShotsToDraft(List<ShotItem> shots, DraftContent draft)
    {
        // 清空现有轨道
        draft.Tracks.Clear();
        draft.Materials.Videos.Clear();

        if (shots == null || shots.Count == 0)
        {
            draft.Duration = 0;
            return;
        }

        // 创建视频轨道
        var videoTrack = new Track
        {
            Id = Guid.NewGuid().ToString("N").ToUpper(),
            Type = "video",
            Segments = new List<Segment>()
        };

        long currentTime = 0;
        double totalDurationSeconds = 0;

        foreach (var shot in shots.OrderBy(s => s.ShotNumber))
        {
            // 跳过没有视频的镜头
            if (string.IsNullOrEmpty(shot.GeneratedVideoPath) || !File.Exists(shot.GeneratedVideoPath))
            {
                continue;
            }

            var materialId = Guid.NewGuid().ToString("N").ToUpper();
            var durationMicroseconds = (long)(shot.Duration * MICROSECONDS_PER_SECOND);

            // 添加视频素材
            draft.Materials.Videos.Add(new VideoMaterial
            {
                Id = materialId,
                Path = shot.GeneratedVideoPath,
                Duration = durationMicroseconds,
                Width = 1920,
                Height = 1080
            });

            // 添加片段
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
                Clip = new Clip
                {
                    Alpha = 1.0,
                    Rotation = 0.0,
                    Scale = new Scale { X = 1.0, Y = 1.0 },
                    Transform = new Transform { X = 0.0, Y = 0.0 },
                    Flip = new Flip { Horizontal = false, Vertical = false }
                }
            });

            currentTime += durationMicroseconds;
            totalDurationSeconds += shot.Duration;
        }

        draft.Tracks.Add(videoTrack);
        draft.Duration = (long)(totalDurationSeconds * MICROSECONDS_PER_SECOND);
    }

    /// <summary>
    /// 从 DraftContent 提取时间轴信息
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
                var material = draft.Materials.Videos.FirstOrDefault(v => v.Id == segment.MaterialId);

                trackInfo.Segments.Add(new SegmentInfo
                {
                    Id = segment.Id,
                    MaterialId = segment.MaterialId,
                    StartTimeSeconds = segment.TargetTimerange.Start / (double)MICROSECONDS_PER_SECOND,
                    DurationSeconds = segment.TargetTimerange.Duration / (double)MICROSECONDS_PER_SECOND,
                    SourceStartSeconds = segment.SourceTimerange.Start / (double)MICROSECONDS_PER_SECOND,
                    SourceDurationSeconds = segment.SourceTimerange.Duration / (double)MICROSECONDS_PER_SECOND,
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
    /// 添加片段到草稿
    /// </summary>
    public static void AddSegmentToDraft(DraftContent draft, ShotItem shot, double startTimeSeconds)
    {
        if (string.IsNullOrEmpty(shot.GeneratedVideoPath) || !File.Exists(shot.GeneratedVideoPath))
        {
            return;
        }

        // 获取或创建视频轨道
        var videoTrack = draft.Tracks.FirstOrDefault(t => t.Type == "video");
        if (videoTrack == null)
        {
            videoTrack = new Track
            {
                Id = Guid.NewGuid().ToString("N").ToUpper(),
                Type = "video",
                Segments = new List<Segment>()
            };
            draft.Tracks.Add(videoTrack);
        }

        var materialId = Guid.NewGuid().ToString("N").ToUpper();
        var durationMicroseconds = (long)(shot.Duration * MICROSECONDS_PER_SECOND);
        var startMicroseconds = (long)(startTimeSeconds * MICROSECONDS_PER_SECOND);

        // 添加视频素材
        draft.Materials.Videos.Add(new VideoMaterial
        {
            Id = materialId,
            Path = shot.GeneratedVideoPath,
            Duration = durationMicroseconds,
            Width = 1920,
            Height = 1080
        });

        // 添加片段
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
            }
        });

        // 更新总时长
        UpdateDraftDuration(draft);
    }

    /// <summary>
    /// 从草稿中删除片段
    /// </summary>
    public static void RemoveSegmentFromDraft(DraftContent draft, string segmentId)
    {
        foreach (var track in draft.Tracks)
        {
            var segment = track.Segments.FirstOrDefault(s => s.Id == segmentId);
            if (segment != null)
            {
                track.Segments.Remove(segment);

                // 删除对应的素材
                var material = draft.Materials.Videos.FirstOrDefault(v => v.Id == segment.MaterialId);
                if (material != null)
                {
                    draft.Materials.Videos.Remove(material);
                }

                // 更新总时长
                UpdateDraftDuration(draft);
                break;
            }
        }
    }

    /// <summary>
    /// 更新片段位置
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
    /// 修剪片段（同时更新目标和源时间范围）
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
                segment.SourceTimerange.Start = (long)(newSourceStartSeconds * MICROSECONDS_PER_SECOND);
                segment.SourceTimerange.Duration = (long)(newSourceDurationSeconds * MICROSECONDS_PER_SECOND);

                UpdateDraftDuration(draft);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 切割片段（返回新片段 ID）
    /// </summary>
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

            // Update original segment (left part)
            segment.TargetTimerange.Duration = (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND);
            segment.SourceTimerange.Duration = (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND);

            // Create right part
            var newSegment = new Segment
            {
                Id = newSegmentId,
                MaterialId = segment.MaterialId,
                TargetTimerange = new TimeRange
                {
                    Start = segment.TargetTimerange.Start + (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND),
                    Duration = (long)(remainingDurationSeconds * MICROSECONDS_PER_SECOND)
                },
                SourceTimerange = new TimeRange
                {
                    Start = segment.SourceTimerange.Start + (long)(splitOffsetSeconds * MICROSECONDS_PER_SECOND),
                    Duration = (long)(remainingDurationSeconds * MICROSECONDS_PER_SECOND)
                },
                Clip = CloneClip(segment.Clip)
            };

            track.Segments.Insert(index + 1, newSegment);
            UpdateDraftDuration(draft);
            return newSegmentId;
        }

        return null;
    }

    /// <summary>
    /// 更新草稿总时长
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
    /// 深拷贝片段剪辑信息
    /// </summary>
    private static Clip CloneClip(Clip clip)
    {
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
    /// 秒转微秒
    /// </summary>
    public static long SecondsToMicroseconds(double seconds)
    {
        return (long)(seconds * MICROSECONDS_PER_SECOND);
    }

    /// <summary>
    /// 微秒转秒
    /// </summary>
    public static double MicrosecondsToSeconds(long microseconds)
    {
        return microseconds / (double)MICROSECONDS_PER_SECOND;
    }
}

/// <summary>
/// 时间轴信息（从 DraftContent 提取的简化视图）
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
/// 轨道信息
/// </summary>
public class TrackInfo
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<SegmentInfo> Segments { get; set; } = new();
}

/// <summary>
/// 片段信息
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
