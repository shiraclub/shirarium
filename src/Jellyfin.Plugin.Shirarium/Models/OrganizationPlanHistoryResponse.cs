namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Response payload for persisted organization plan revisions.
/// </summary>
public sealed class OrganizationPlanHistoryResponse
{
    /// <summary>
    /// Gets total persisted revision count before limit is applied.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets returned plan revisions.
    /// </summary>
    public OrganizationPlanSnapshot[] Items { get; init; } = [];
}
