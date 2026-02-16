namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Response payload for reviewed-plan preflight simulation.
/// </summary>
public sealed class PreflightReviewedPlanResponse
{
    /// <summary>
    /// Gets the UTC timestamp when this preview was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the reviewed plan fingerprint used for this preview.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets one-time token required by <c>apply-reviewed-plan</c>.
    /// </summary>
    public string PreflightToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets UTC expiry timestamp for <see cref="PreflightToken"/>.
    /// </summary>
    public DateTimeOffset PreflightTokenExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets the count of reviewed entries marked as move.
    /// </summary>
    public int MoveCandidateCount { get; init; }

    /// <summary>
    /// Gets selected source paths used for this preview.
    /// </summary>
    public string[] SelectedSourcePaths { get; init; } = [];

    /// <summary>
    /// Gets per-item preview outcomes.
    /// </summary>
    public ApplyOrganizationPlanResult PreviewResult { get; init; } = new();
}
