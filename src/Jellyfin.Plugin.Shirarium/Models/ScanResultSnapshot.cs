namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregate result emitted by a Shirarium dry-run scan.
/// </summary>
public sealed class ScanResultSnapshot
{
    /// <summary>
    /// Gets the UTC timestamp when this snapshot was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether this run was non-destructive.
    /// </summary>
    public bool DryRunMode { get; init; } = true;

    /// <summary>
    /// Gets the number of media items examined for eligibility.
    /// </summary>
    public int ExaminedCount { get; init; }

    /// <summary>
    /// Gets the number of items selected as parse candidates.
    /// </summary>
    public int CandidateCount { get; init; }

    /// <summary>
    /// Gets the number of candidates successfully parsed and accepted.
    /// </summary>
    public int ParsedCount { get; init; }

    /// <summary>
    /// Gets the number of candidates skipped because the run limit was reached.
    /// </summary>
    public int SkippedByLimitCount { get; init; }

    /// <summary>
    /// Gets the number of candidates skipped due to confidence threshold.
    /// </summary>
    public int SkippedByConfidenceCount { get; init; }

    /// <summary>
    /// Gets the number of candidates skipped because parse calls failed.
    /// </summary>
    public int ParseFailureCount { get; init; }

    /// <summary>
    /// Gets accepted suggestion entries for this run.
    /// </summary>
    public ScanSuggestion[] Suggestions { get; init; } = [];

    /// <summary>
    /// Gets candidate reason counts across all candidate items considered in this run.
    /// </summary>
    public ScanCountBucket[] CandidateReasonCounts { get; init; } = [];

    /// <summary>
    /// Gets parser source counts observed from parse responses.
    /// </summary>
    public ScanCountBucket[] ParserSourceCounts { get; init; } = [];

    /// <summary>
    /// Gets confidence bucket counts observed from parse responses.
    /// </summary>
    public ScanCountBucket[] ConfidenceBucketCounts { get; init; } = [];
}
