namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Persistent audit log snapshot of apply runs.
/// </summary>
public sealed class ApplyJournalSnapshot
{
    /// <summary>
    /// Gets all recorded apply runs in chronological order.
    /// </summary>
    public ApplyOrganizationPlanResult[] Runs { get; init; } = [];
}
