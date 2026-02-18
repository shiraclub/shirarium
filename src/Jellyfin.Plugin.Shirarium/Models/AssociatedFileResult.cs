namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Result for one associated file move attempt during an organization apply run.
/// </summary>
public sealed class AssociatedFileResult
{
    /// <summary>
    /// Gets the source associated file path.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target path for the associated file.
    /// </summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the status: applied or failed.
    /// </summary>
    public string Status { get; init; } = "applied";

    /// <summary>
    /// Gets the error message if the move failed.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
