namespace Storyboard.Domain.Entities;

/// <summary>
/// Timeline synchronization mode
/// </summary>
public enum SyncMode
{
    /// <summary>
    /// Shots → Timeline only (ignore timeline changes)
    /// </summary>
    ForwardOnly = 0,

    /// <summary>
    /// Shots ↔ Timeline bidirectional sync
    /// </summary>
    Bidirectional = 1,

    /// <summary>
    /// Timeline → Shots only (timeline is source of truth)
    /// </summary>
    TimelineOnly = 2
}
