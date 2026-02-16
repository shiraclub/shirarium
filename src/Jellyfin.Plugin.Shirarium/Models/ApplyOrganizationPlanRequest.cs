namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for applying selected organization plan entries.
/// </summary>
public sealed class ApplyOrganizationPlanRequest
{
    /// <summary>
    /// Gets the source paths to apply from the latest organization plan snapshot.
    /// </summary>
    public string[] SourcePaths { get; init; } = [];
}
