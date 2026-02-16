namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregate result for undoing one apply run.
/// </summary>
public sealed class UndoApplyResult
{
    /// <summary>
    /// Gets the unique identifier of this undo run.
    /// </summary>
    public string UndoRunId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the apply run id targeted by this undo run.
    /// </summary>
    public string SourceApplyRunId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp when this undo run was executed.
    /// </summary>
    public DateTimeOffset UndoneAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the number of requested inverse moves.
    /// </summary>
    public int RequestedCount { get; init; }

    /// <summary>
    /// Gets the number of successfully applied inverse moves.
    /// </summary>
    public int AppliedCount { get; init; }

    /// <summary>
    /// Gets the number of skipped inverse moves.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets the number of failed inverse moves.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets per-item undo results.
    /// </summary>
    public UndoApplyItemResult[] Results { get; init; } = [];
}
