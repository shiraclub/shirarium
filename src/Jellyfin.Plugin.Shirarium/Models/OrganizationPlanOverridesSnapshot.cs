namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Persisted review overrides associated with one plan fingerprint.
/// </summary>
public sealed class OrganizationPlanOverridesSnapshot
{
    /// <summary>
    /// Gets the plan fingerprint these overrides target.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp when this override snapshot was updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets per-entry overrides for the plan.
    /// </summary>
    public OrganizationPlanEntryOverride[] Entries { get; init; } = [];
}

