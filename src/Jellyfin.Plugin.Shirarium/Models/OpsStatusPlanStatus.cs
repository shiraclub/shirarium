namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Latest stored organization plan status.
/// </summary>
public sealed class OpsStatusPlanStatus
{
    /// <summary>
    /// Gets a value indicating whether a usable plan snapshot exists.
    /// </summary>
    public bool HasPlan { get; init; }

    /// <summary>
    /// Gets the plan generation timestamp when available.
    /// </summary>
    public DateTimeOffset? GeneratedAtUtc { get; init; }

    /// <summary>
    /// Gets the deterministic plan fingerprint when available.
    /// </summary>
    public string? PlanFingerprint { get; init; }

    /// <summary>
    /// Gets the configured organization root path when available.
    /// </summary>
    public string? RootPath { get; init; }

    /// <summary>
    /// Gets the source suggestion count.
    /// </summary>
    public int SourceSuggestionCount { get; init; }

    /// <summary>
    /// Gets the planned move count.
    /// </summary>
    public int PlannedCount { get; init; }

    /// <summary>
    /// Gets the no-op count.
    /// </summary>
    public int NoopCount { get; init; }

    /// <summary>
    /// Gets the skipped count.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets the conflict count.
    /// </summary>
    public int ConflictCount { get; init; }

    /// <summary>
    /// Gets plan entry action counts.
    /// </summary>
    public OpsStatusCountBucket[] ActionCounts { get; init; } = [];

    /// <summary>
    /// Gets plan entry strategy counts.
    /// </summary>
    public OpsStatusCountBucket[] StrategyCounts { get; init; } = [];

    /// <summary>
    /// Gets plan entry reason counts.
    /// </summary>
    public OpsStatusCountBucket[] ReasonCounts { get; init; } = [];
}
