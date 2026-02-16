namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Result for one inverse move operation during undo.
/// </summary>
public sealed class UndoApplyItemResult
{
    /// <summary>
    /// Gets the source path used for this undo move.
    /// </summary>
    public string FromPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination path used for this undo move.
    /// </summary>
    public string ToPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the undo status for this item: applied, skipped, or failed.
    /// </summary>
    public string Status { get; init; } = "skipped";

    /// <summary>
    /// Gets the machine-readable reason for this item result.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the conflict path used when an existing undo target was moved aside.
    /// </summary>
    public string? ConflictMovedToPath { get; init; }
}
