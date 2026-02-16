namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Latest apply run status summary.
/// </summary>
public sealed class OpsStatusApplyRunStatus
{
    /// <summary>
    /// Gets the apply run identifier.
    /// </summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the apply timestamp.
    /// </summary>
    public DateTimeOffset AppliedAtUtc { get; init; }

    /// <summary>
    /// Gets the plan fingerprint used by this apply run.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

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
    /// Gets a value indicating whether this apply run was undone.
    /// </summary>
    public bool WasUndone { get; init; }

    /// <summary>
    /// Gets the undo run id when this run was undone.
    /// </summary>
    public string? UndoneByRunId { get; init; }

    /// <summary>
    /// Gets the undo timestamp when this run was undone.
    /// </summary>
    public DateTimeOffset? UndoneAtUtc { get; init; }

    /// <summary>
    /// Gets failed reason buckets.
    /// </summary>
    public OpsStatusReasonCount[] FailedReasons { get; init; } = [];

    /// <summary>
    /// Gets skipped reason buckets.
    /// </summary>
    public OpsStatusReasonCount[] SkippedReasons { get; init; } = [];
}
