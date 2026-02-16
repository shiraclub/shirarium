namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Selection/apply response for filtered organization plan execution.
/// </summary>
public sealed class ApplyPlanByFilterResponse
{
    /// <summary>
    /// Gets the UTC timestamp when this response was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the matched plan fingerprint.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether this run was preview-only.
    /// </summary>
    public bool DryRunOnly { get; init; } = true;

    /// <summary>
    /// Gets the number of move candidates before filter evaluation.
    /// </summary>
    public int MoveCandidateCount { get; init; }

    /// <summary>
    /// Gets the number of selected source paths after filtering.
    /// </summary>
    public int SelectedCount { get; init; }

    /// <summary>
    /// Gets the number of candidates excluded by filters and limit.
    /// </summary>
    public int FilteredOutCount { get; init; }

    /// <summary>
    /// Gets selected source paths in deterministic order.
    /// </summary>
    public string[] SelectedSourcePaths { get; init; } = [];

    /// <summary>
    /// Gets apply results when <see cref="DryRunOnly"/> is false and execution occurred.
    /// </summary>
    public ApplyOrganizationPlanResult? ApplyResult { get; init; }
}
