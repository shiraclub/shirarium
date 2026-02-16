namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregated summary for the latest organization plan snapshot.
/// </summary>
public sealed class OrganizationPlanSummaryResponse
{
    /// <summary>
    /// Gets the UTC timestamp when this summary was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the plan fingerprint.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the organization root path.
    /// </summary>
    public string RootPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets total plan entries.
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    /// Gets source suggestion count.
    /// </summary>
    public int SourceSuggestionCount { get; init; }

    /// <summary>
    /// Gets planned move count.
    /// </summary>
    public int PlannedCount { get; init; }

    /// <summary>
    /// Gets no-op count.
    /// </summary>
    public int NoopCount { get; init; }

    /// <summary>
    /// Gets skipped count.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets conflict count.
    /// </summary>
    public int ConflictCount { get; init; }

    /// <summary>
    /// Gets counts grouped by action.
    /// </summary>
    public OrganizationPlanCountBucket[] ActionCounts { get; init; } = [];

    /// <summary>
    /// Gets counts grouped by strategy.
    /// </summary>
    public OrganizationPlanCountBucket[] StrategyCounts { get; init; } = [];

    /// <summary>
    /// Gets counts grouped by reason.
    /// </summary>
    public OrganizationPlanCountBucket[] ReasonCounts { get; init; } = [];

    /// <summary>
    /// Gets top-level target folders ranked by selected move counts.
    /// </summary>
    public OrganizationPlanTargetFolderBucket[] TopTargetFolders { get; init; } = [];
}
