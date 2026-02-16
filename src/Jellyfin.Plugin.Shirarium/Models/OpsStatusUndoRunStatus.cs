namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Latest undo run status summary.
/// </summary>
public sealed class OpsStatusUndoRunStatus
{
    /// <summary>
    /// Gets the undo run identifier.
    /// </summary>
    public string UndoRunId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source apply run identifier.
    /// </summary>
    public string SourceApplyRunId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the undo timestamp.
    /// </summary>
    public DateTimeOffset UndoneAtUtc { get; init; }

    /// <summary>
    /// Gets the requested count.
    /// </summary>
    public int RequestedCount { get; init; }

    /// <summary>
    /// Gets the applied count.
    /// </summary>
    public int AppliedCount { get; init; }

    /// <summary>
    /// Gets the skipped count.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets the failed count.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets the number of undo operations that resolved target conflicts by moving existing files aside.
    /// </summary>
    public int ConflictResolvedCount { get; init; }

    /// <summary>
    /// Gets failed reason buckets.
    /// </summary>
    public OpsStatusReasonCount[] FailedReasons { get; init; } = [];

    /// <summary>
    /// Gets skipped reason buckets.
    /// </summary>
    public OpsStatusReasonCount[] SkippedReasons { get; init; } = [];
}
