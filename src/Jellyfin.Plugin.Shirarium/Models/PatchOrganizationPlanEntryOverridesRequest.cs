namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for updating persisted organization-plan entry overrides.
/// </summary>
public sealed class PatchOrganizationPlanEntryOverridesRequest
{
    /// <summary>
    /// Gets the expected plan fingerprint that must match the latest stored plan.
    /// </summary>
    public string ExpectedPlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets patch operations to apply.
    /// </summary>
    public OrganizationPlanEntryOverridePatch[] Patches { get; init; } = [];
}

