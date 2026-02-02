using Microsoft.Extensions.Logging;
using Storyboard.Domain.Entities;
using Storyboard.Models;
using Storyboard.Shared.Time;

namespace Storyboard.Application.Services;

/// <summary>
/// Centralized service for shot-timeline synchronization logic
/// </summary>
public class ShotTimelineSyncService
{
    private readonly ILogger<ShotTimelineSyncService> _logger;

    public ShotTimelineSyncService(ILogger<ShotTimelineSyncService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Determines if a shot should sync to timeline based on sync mode
    /// </summary>
    public bool ShouldSyncShotToTimeline(SyncMode syncMode, ShotItem shot)
    {
        return syncMode switch
        {
            SyncMode.ForwardOnly => shot.IsSyncedToTimeline,
            SyncMode.Bidirectional => shot.IsSyncedToTimeline,
            SyncMode.TimelineOnly => false, // Never sync shots to timeline in this mode
            _ => false
        };
    }

    /// <summary>
    /// Determines if timeline changes should update shots
    /// </summary>
    public bool ShouldSyncTimelineToShot(SyncMode syncMode, ShotItem shot)
    {
        return syncMode switch
        {
            SyncMode.ForwardOnly => false, // Ignore timeline changes
            SyncMode.Bidirectional => shot.IsSyncedToTimeline,
            SyncMode.TimelineOnly => true, // Always sync timeline to shots
            _ => false
        };
    }

    /// <summary>
    /// Apply timeline duration change to shot
    /// </summary>
    public void ApplyTimelineDurationToShot(ShotItem shot, long newDurationTick)
    {
        shot.ActualDurationTick = newDurationTick;
        shot.PlannedDurationTick = newDurationTick;
        shot.TimingSource = ShotTimingSource.TimelineActual;

        _logger.LogInformation(
            "Applied timeline duration to shot #{ShotNumber}: {Duration}ms",
            shot.ShotNumber,
            TimeTick.ToMilliseconds(newDurationTick));
    }
}
