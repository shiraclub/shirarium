namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for applying selected organization plan entries.
/// </summary>
public sealed class ApplyOrganizationPlanRequest
{
    /// <summary>
    /// Gets the expected plan fingerprint that must match the latest stored plan.
    /// </summary>
    public string ExpectedPlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source paths to apply from the latest organization plan snapshot.
    /// </summary>
    public string[] SourcePaths { get; init; } = [];
}
