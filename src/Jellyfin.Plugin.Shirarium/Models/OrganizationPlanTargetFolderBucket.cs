namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Top-level target folder count bucket for plan summaries.
/// </summary>
public sealed class OrganizationPlanTargetFolderBucket
{
    /// <summary>
    /// Gets the top-level target folder key.
    /// </summary>
    public string Folder { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of entries targeting this folder.
    /// </summary>
    public int Count { get; init; }
}
