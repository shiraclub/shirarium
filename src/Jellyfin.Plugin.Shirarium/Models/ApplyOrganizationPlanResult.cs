namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregate result for applying selected entries from an organization plan snapshot.
/// </summary>
public sealed class ApplyOrganizationPlanResult
{
    /// <summary>
    /// Gets the UTC timestamp when this apply run was executed.
    /// </summary>
    public DateTimeOffset AppliedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the number of unique selected paths requested.
    /// </summary>
    public int RequestedCount { get; init; }

    /// <summary>
    /// Gets the number of successfully applied moves.
    /// </summary>
    public int AppliedCount { get; init; }

    /// <summary>
    /// Gets the number of skipped selections.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets the number of failed selections.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets per-item apply results.
    /// </summary>
    public ApplyOrganizationPlanItemResult[] Results { get; init; } = [];
}
