namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Response payload for persisted organization plan override revisions.
/// </summary>
public sealed class OrganizationPlanOverridesHistoryResponse
{
    /// <summary>
    /// Gets total persisted revision count before limit is applied.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Gets returned override revisions.
    /// </summary>
    public OrganizationPlanOverridesSnapshot[] Items { get; init; } = [];
}
