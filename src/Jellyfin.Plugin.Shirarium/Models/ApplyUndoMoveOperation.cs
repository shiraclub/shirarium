namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Inverse move operation recorded for rollback.
/// </summary>
public sealed class ApplyUndoMoveOperation
{
    /// <summary>
    /// Gets the current path to move from during undo.
    /// </summary>
    public string FromPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the destination path to restore during undo.
    /// </summary>
    public string ToPath { get; init; } = string.Empty;
}
