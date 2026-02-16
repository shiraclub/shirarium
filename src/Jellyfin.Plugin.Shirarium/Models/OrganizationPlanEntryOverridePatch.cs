namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Patch operation for one plan entry override.
/// </summary>
public sealed class OrganizationPlanEntryOverridePatch
{
    /// <summary>
    /// Gets the source path identifying the plan entry.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional action override to set.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Gets the optional target path override to set.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether this override should be removed.
    /// </summary>
    public bool Remove { get; init; }
}

