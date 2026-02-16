namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for selecting and optionally applying plan entries by filters.
/// </summary>
public sealed class ApplyPlanByFilterRequest
{
    /// <summary>
    /// Gets the expected plan fingerprint that must match the latest stored plan.
    /// </summary>
    public string ExpectedPlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets strategy filters (for example: movie, episode).
    /// </summary>
    public string[] Strategies { get; init; } = [];

    /// <summary>
    /// Gets reason filters (for example: Planned, PlannedWithSuffix).
    /// </summary>
    public string[] Reasons { get; init; } = [];

    /// <summary>
    /// Gets an optional source-path prefix filter.
    /// </summary>
    public string? PathPrefix { get; init; }

    /// <summary>
    /// Gets an optional minimum confidence filter in the inclusive range [0, 1].
    /// </summary>
    public double? MinConfidence { get; init; }

    /// <summary>
    /// Gets an optional maximum number of selected entries.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// Gets a value indicating whether to preview selection without applying file moves.
    /// </summary>
    public bool DryRunOnly { get; init; } = true;
}
