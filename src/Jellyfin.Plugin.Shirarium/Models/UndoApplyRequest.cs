namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for undoing a previous apply run.
/// </summary>
public sealed class UndoApplyRequest
{
    /// <summary>
    /// Gets the apply run id to undo. When empty, the latest apply run is selected.
    /// </summary>
    public string? RunId { get; init; }
}
