using Storyboard.Models.Timeline;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// Timeline交互服务接口
/// </summary>
public interface ITimelineInteractionService
{
    /// <summary>
    /// 检查片段是否可以移动到指定位置（碰撞检测）
    /// </summary>
    bool CanMoveClip(TimelineClip clip, double newStartTime, TimelineTrack track);

    /// <summary>
    /// 获取吸附后的位置
    /// </summary>
    double GetSnappedPosition(double position, TimelineClip? excludeClip, TimelineTrack track,
        double playheadTime, double threshold = 5.0);

    /// <summary>
    /// 关闭间隙（用于ripple delete）
    /// </summary>
    void CloseGap(TimelineTrack track, double gapStart, double gapDuration);

    /// <summary>
    /// 批量移动片段组
    /// </summary>
    void MoveClipGroup(List<TimelineClip> clips, double deltaTime, TimelineTrack track);
}

/// <summary>
/// Timeline交互服务实现
/// </summary>
public class TimelineInteractionService : ITimelineInteractionService
{
    /// <summary>
    /// 检查片段是否可以移动到指定位置（碰撞检测）
    /// </summary>
    public bool CanMoveClip(TimelineClip clip, double newStartTime, TimelineTrack track)
    {
        if (newStartTime < 0)
            return false;

        var newEndTime = newStartTime + clip.Duration;

        // 检查是否与其他片段重叠
        foreach (var otherClip in track.Clips)
        {
            // 跳过自己
            if (otherClip.Id == clip.Id)
                continue;

            // 检查时间范围是否重叠
            if (IsOverlapping(newStartTime, newEndTime, otherClip.StartTime, otherClip.EndTime))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 获取吸附后的位置
    /// </summary>
    public double GetSnappedPosition(double position, TimelineClip? excludeClip, TimelineTrack track,
        double playheadTime, double threshold = 5.0)
    {
        var snapPoints = new List<double>();

        // 添加播放头位置作为吸附点
        snapPoints.Add(playheadTime);

        // 添加轨道起始位置
        snapPoints.Add(0);

        // 添加其他片段的边缘作为吸附点
        foreach (var clip in track.Clips)
        {
            if (excludeClip != null && clip.Id == excludeClip.Id)
                continue;

            snapPoints.Add(clip.StartTime);
            snapPoints.Add(clip.EndTime);
        }

        // 将像素位置转换为时间（假设使用第一个片段的PixelsPerSecond）
        var pixelsPerSecond = excludeClip?.PixelsPerSecond ?? 50.0;
        var timePosition = position / pixelsPerSecond;
        var timeThreshold = threshold / pixelsPerSecond;

        // 查找最近的吸附点
        foreach (var snapPoint in snapPoints)
        {
            if (Math.Abs(timePosition - snapPoint) < timeThreshold)
            {
                return snapPoint * pixelsPerSecond; // 转换回像素位置
            }
        }

        return position; // 没有找到吸附点，返回原位置
    }

    /// <summary>
    /// 关闭间隙（用于ripple delete）
    /// </summary>
    public void CloseGap(TimelineTrack track, double gapStart, double gapDuration)
    {
        // 找到所有在间隙之后的片段
        var clipsAfter = track.Clips
            .Where(c => c.StartTime >= gapStart)
            .OrderBy(c => c.StartTime)
            .ToList();

        // 将它们向左移动
        foreach (var clip in clipsAfter)
        {
            clip.StartTime -= gapDuration;
        }
    }

    /// <summary>
    /// 批量移动片段组
    /// </summary>
    public void MoveClipGroup(List<TimelineClip> clips, double deltaTime, TimelineTrack track)
    {
        if (clips.Count == 0)
            return;

        // 按开始时间排序
        var sortedClips = clips.OrderBy(c => c.StartTime).ToList();

        // 检查所有片段是否都可以移动
        foreach (var clip in sortedClips)
        {
            var newStartTime = clip.StartTime + deltaTime;
            if (!CanMoveClip(clip, newStartTime, track))
                return; // 如果有任何片段不能移动，则取消整个操作
        }

        // 移动所有片段
        foreach (var clip in sortedClips)
        {
            clip.StartTime += deltaTime;
        }
    }

    /// <summary>
    /// 检查两个时间范围是否重叠
    /// </summary>
    private bool IsOverlapping(double start1, double end1, double start2, double end2)
    {
        // 两个范围重叠的条件：start1 < end2 && start2 < end1
        return start1 < end2 && start2 < end1;
    }
}
