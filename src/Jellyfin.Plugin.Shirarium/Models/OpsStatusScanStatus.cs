namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Latest scan status summary and observability buckets.
/// </summary>
public sealed class OpsStatusScanStatus
{
    /// <summary>
    /// Gets a value indicating whether a usable scan snapshot exists.
    /// </summary>
    public bool HasScan { get; init; }

    /// <summary>
    /// Gets the scan generation timestamp when available.
    /// </summary>
    public DateTimeOffset? GeneratedAtUtc { get; init; }

    /// <summary>
    /// Gets whether the latest scan ran in dry-run mode.
    /// </summary>
    public bool DryRunMode { get; init; } = true;

    /// <summary>
    /// Gets the number of media items examined for eligibility.
    /// </summary>
    public int ExaminedCount { get; init; }

    /// <summary>
    /// Gets the number of candidate items.
    /// </summary>
    public int CandidateCount { get; init; }

    /// <summary>
    /// Gets the number of parsed suggestions accepted.
    /// </summary>
    public int ParsedCount { get; init; }

    /// <summary>
    /// Gets the number of accepted suggestions persisted.
    /// </summary>
    public int SuggestionCount { get; init; }

    /// <summary>
    /// Gets the number of candidates skipped by max item limit.
    /// </summary>
    public int SkippedByLimitCount { get; init; }

    /// <summary>
    /// Gets the number of candidates skipped by confidence threshold.
    /// </summary>
    public int SkippedByConfidenceCount { get; init; }

    /// <summary>
    /// Gets the number of parse failures.
    /// </summary>
    public int EngineFailureCount { get; init; }

    /// <summary>
    /// Gets candidate reason counts.
    /// </summary>
    public OpsStatusCountBucket[] CandidateReasonCounts { get; init; } = [];

    /// <summary>
    /// Gets parser source counts.
    /// </summary>
    public OpsStatusCountBucket[] ParserSourceCounts { get; init; } = [];

    /// <summary>
    /// Gets confidence bucket counts.
    /// </summary>
    public OpsStatusCountBucket[] ConfidenceBucketCounts { get; init; } = [];
}

