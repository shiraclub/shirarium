namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for reviewed-plan preflight simulation.
/// </summary>
public sealed class PreflightReviewedPlanRequest
{
    /// <summary>
    /// Gets the expected plan fingerprint that must match the latest stored plan.
    /// </summary>
    public string ExpectedPlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional source paths to preflight. When omitted, all reviewed move entries are preflighted.
    /// </summary>
    public string[] SourcePaths { get; init; } = [];
}
