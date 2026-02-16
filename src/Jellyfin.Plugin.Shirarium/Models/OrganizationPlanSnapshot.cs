namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregate output of a dry-run physical file organization planning pass.
/// </summary>
public sealed class OrganizationPlanSnapshot
{
    /// <summary>
    /// Gets the UTC timestamp when this plan was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a deterministic fingerprint for this plan payload.
    /// </summary>
    public string PlanFingerprint { get; set; } = string.Empty;

    /// <summary>
    /// Gets the root path used when generating planned target paths.
    /// </summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this plan is non-destructive.
    /// </summary>
    public bool DryRunMode { get; init; } = true;

    /// <summary>
    /// Gets the number of source suggestions evaluated.
    /// </summary>
    public int SourceSuggestionCount { get; init; }

    /// <summary>
    /// Gets the number of entries planned as moves.
    /// </summary>
    public int PlannedCount { get; init; }

    /// <summary>
    /// Gets the number of entries that were already in the planned location.
    /// </summary>
    public int NoopCount { get; init; }

    /// <summary>
    /// Gets the number of skipped entries.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets the number of entries marked as conflicts.
    /// </summary>
    public int ConflictCount { get; init; }

    /// <summary>
    /// Gets all generated plan entries.
    /// </summary>
    public OrganizationPlanEntry[] Entries { get; init; } = [];
}
