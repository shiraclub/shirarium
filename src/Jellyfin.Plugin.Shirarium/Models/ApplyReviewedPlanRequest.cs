namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for applying reviewed organization-plan entries with persisted overrides.
/// </summary>
public sealed class ApplyReviewedPlanRequest
{
    /// <summary>
    /// Gets the expected plan fingerprint that must match the latest stored plan.
    /// </summary>
    public string ExpectedPlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional source paths to apply. When omitted, all reviewed move entries are applied.
    /// </summary>
    public string[] SourcePaths { get; init; } = [];
}

