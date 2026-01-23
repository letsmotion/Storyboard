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
/// 查询视频导入信息
/// </summary>
public class GetVideoImportInfoQuery
{
    public string? VideoPath { get; set; }
    public string? VideoDuration { get; set; }
    public double VideoDurationSeconds { get; set; }
}
