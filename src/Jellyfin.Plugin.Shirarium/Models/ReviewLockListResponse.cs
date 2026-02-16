namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Paged/list response payload for persisted review locks.
/// </summary>
public sealed class ReviewLockListResponse
{
    /// <summary>
    /// Gets total persisted lock count before limit is applied.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets returned lock summaries.
    /// </summary>
    public ReviewLockSummary[] Items { get; init; } = [];
}
