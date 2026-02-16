namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Per-entry review override for a stored organization plan.
/// </summary>
public sealed class OrganizationPlanEntryOverride
{
    /// <summary>
    /// Gets the source path identifying the plan entry.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional action override.
    /// </summary>
    public string? Action { get; init; }

    /// <summary>
    /// Gets the optional target path override.
    /// </summary>
    public string? TargetPath { get; init; }
}

