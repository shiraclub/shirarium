namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Planned move for an associated file (NFO, subtitle, image) linked to a media item.
/// </summary>
public sealed class AssociatedFileMove
{
    /// <summary>
    /// Gets the source associated file path.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the planned target path for the associated file.
    /// </summary>
    public string TargetPath { get; init; } = string.Empty;
}
