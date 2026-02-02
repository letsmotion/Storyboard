using Storyboard.Models.Timeline;

namespace Storyboard.Messages;

/// <summary>
/// 片段被选中消息
/// </summary>
public record ClipSelectedMessage(TimelineClip? Clip);

/// <summary>
/// 片段被移动消息
/// </summary>
public record ClipMovedMessage(TimelineClip Clip, double OldStartTime, double NewStartTime);

/// <summary>
/// 轨道被添加消息
/// </summary>
public record TrackAddedMessage(TimelineTrack Track);

/// <summary>
/// 轨道被删除消息
/// </summary>
public record TrackDeletedMessage(TimelineTrack Track);

/// <summary>
/// 播放头位置变化消息
/// </summary>
public record PlayheadPositionChangedMessage(double Time, double Position);

/// <summary>
/// 片段开始拖动消息
/// </summary>
public record ClipDragStartedMessage(TimelineClip Clip);

/// <summary>
/// 片段拖动更新消息
/// </summary>
public record ClipDragUpdatedMessage(TimelineClip Clip, double NewPosition);

/// <summary>
/// 片段拖动结束消息
/// </summary>
public record ClipDragEndedMessage(TimelineClip Clip, double FinalPosition);

/// <summary>
/// 片段被修剪消息
/// </summary>
public record ClipTrimmedMessage(TimelineClip Clip, double OldDuration, double NewDuration);

/// <summary>
/// 片段被分割消息
/// </summary>
public record ClipsSplitMessage(TimelineClip OriginalClip, TimelineClip NewClip);

/// <summary>
/// 片段被删除消息
/// </summary>
public record ClipsDeletedMessage(List<TimelineClip> Clips, bool RippleDelete);

/// <summary>
/// 查询视频导入信息
/// </summary>
public class GetVideoImportInfoQuery
{
    public string? VideoPath { get; set; }
    public string? VideoDuration { get; set; }
    public double VideoDurationSeconds { get; set; }
}

/// <summary>
/// 从时间轴重建分镜列表消息
/// </summary>
public record RebuildShotsFromTimelineMessage();
