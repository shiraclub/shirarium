namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Result payload for updating organization-plan entry overrides.
/// </summary>
public sealed class PatchOrganizationPlanEntryOverridesResponse
{
    /// <summary>
    /// Gets the plan fingerprint associated with stored overrides.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp when the override snapshot was updated.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the number of stored overrides after patching.
    /// </summary>
    public int StoredCount { get; init; }

    /// <summary>
    /// Gets the number of patches that created or updated an override.
    /// </summary>
    public int UpdatedCount { get; init; }

    /// <summary>
    /// Gets the number of patches that removed an override.
    /// </summary>
    public int RemovedCount { get; init; }
}

