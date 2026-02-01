using System;
using System.Collections.Generic;

namespace Storyboard.Models.Timeline;

/// <summary>
/// 时间轴操作接口（用于撤销/重做）
/// </summary>
public interface ITimelineAction
{
    /// <summary>
    /// 执行操作
    /// </summary>
    void Execute();

    /// <summary>
    /// 撤销操作
    /// </summary>
    void Undo();

    /// <summary>
    /// 操作描述
    /// </summary>
    string Description { get; }
}

/// <summary>
/// 删除片段操作
/// </summary>
public class DeleteClipsAction : ITimelineAction
{
    private readonly Action<List<ClipSnapshot>> _restoreAction;
    private readonly List<ClipSnapshot> _deletedClips;

    public string Description => $"删除 {_deletedClips.Count} 个片段";

    public DeleteClipsAction(List<ClipSnapshot> deletedClips, Action<List<ClipSnapshot>> restoreAction)
    {
        _deletedClips = deletedClips;
        _restoreAction = restoreAction;
    }

    public void Execute()
    {
        // 执行已在外部完成
    }

    public void Undo()
    {
        _restoreAction(_deletedClips);
    }
}

/// <summary>
/// 移动片段操作
/// </summary>
public class MoveClipAction : ITimelineAction
{
    private readonly Guid _clipId;
    private readonly double _oldStartTime;
    private readonly double _newStartTime;
    private readonly Guid _oldTrackId;
    private readonly Guid _newTrackId;
    private readonly Action<Guid, double, Guid> _moveAction;

    public string Description => "移动片段";

    public MoveClipAction(
        Guid clipId,
        double oldStartTime,
        double newStartTime,
        Guid oldTrackId,
        Guid newTrackId,
        Action<Guid, double, Guid> moveAction)
    {
        _clipId = clipId;
        _oldStartTime = oldStartTime;
        _newStartTime = newStartTime;
        _oldTrackId = oldTrackId;
        _newTrackId = newTrackId;
        _moveAction = moveAction;
    }

    public void Execute()
    {
        _moveAction(_clipId, _newStartTime, _newTrackId);
    }

    public void Undo()
    {
        _moveAction(_clipId, _oldStartTime, _oldTrackId);
    }
}

/// <summary>
/// 分割片段操作
/// </summary>
public class SplitClipAction : ITimelineAction
{
    private readonly ClipSnapshot _originalClipSnapshot;
    private readonly Guid _newClipId;
    private readonly Action<ClipSnapshot> _undoSplitAction;
    private readonly Action<Guid> _deleteNewClipAction;

    public string Description => "分割片段";

    public SplitClipAction(
        ClipSnapshot originalClipSnapshot,
        Guid newClipId,
        Action<ClipSnapshot> undoSplitAction,
        Action<Guid> deleteNewClipAction)
    {
        _originalClipSnapshot = originalClipSnapshot;
        _newClipId = newClipId;
        _undoSplitAction = undoSplitAction;
        _deleteNewClipAction = deleteNewClipAction;
    }

    public void Execute()
    {
        // 重做时不需要做任何事，因为分割已经在原始操作中完成
        // 如果需要完整的重做支持，需要保存更多状态
    }

    public void Undo()
    {
        // 删除新创建的片段
        _deleteNewClipAction(_newClipId);
        // 恢复原始片段的状态
        _undoSplitAction(_originalClipSnapshot);
    }
}

/// <summary>
/// 片段快照（用于撤销/重做）
/// </summary>
public class ClipSnapshot
{
    public Guid ClipId { get; set; }
    public Guid TrackId { get; set; }
    public int ShotNumber { get; set; }
    public double StartTime { get; set; }
    public double Duration { get; set; }
    public double SourceStart { get; set; }
    public double SourceDuration { get; set; }
    public ClipStatus Status { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? VideoPath { get; set; }

    public static ClipSnapshot FromClip(TimelineClip clip)
    {
        return new ClipSnapshot
        {
            ClipId = clip.Id,
            TrackId = clip.TrackId,
            ShotNumber = clip.ShotNumber,
            StartTime = clip.StartTime,
            Duration = clip.Duration,
            SourceStart = clip.SourceStart,
            SourceDuration = clip.SourceDuration,
            Status = clip.Status,
            ThumbnailPath = clip.ThumbnailPath,
            VideoPath = clip.VideoPath
        };
    }

    public TimelineClip ToClip(double pixelsPerSecond)
    {
        return new TimelineClip
        {
            Id = ClipId,
            TrackId = TrackId,
            ShotNumber = ShotNumber,
            StartTime = StartTime,
            Duration = Duration,
            SourceStart = SourceStart,
            SourceDuration = SourceDuration,
            Status = Status,
            ThumbnailPath = ThumbnailPath,
            VideoPath = VideoPath,
            PixelsPerSecond = pixelsPerSecond
        };
    }
}
