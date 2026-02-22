namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregate result for applying selected entries from an organization plan snapshot.
/// </summary>
public sealed class ApplyOrganizationPlanResult
{
    /// <summary>
    /// Gets the unique identifier of this apply run.
    /// </summary>
    public string RunId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the UTC timestamp when this apply run was executed.
    /// </summary>
    public DateTimeOffset AppliedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the organization root path used for preflight validation in this apply run.
    /// </summary>
    public string PlanRootPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the plan fingerprint validated for this apply run.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of unique selected paths requested.
    /// </summary>
    public int RequestedCount { get; init; }

    /// <summary>
    /// Gets the number of successfully applied moves.
    /// </summary>
    public int AppliedCount { get; init; }

    /// <summary>
    /// Gets the number of skipped selections.
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Gets the number of failed selections.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Gets per-item apply results.
    /// </summary>
    public ApplyOrganizationPlanItemResult[] Results { get; init; } = [];

    /// <summary>
    /// Gets inverse move operations that can restore this run.
    /// </summary>
    public ApplyUndoMoveOperation[] UndoOperations { get; init; } = [];

    /// <summary>
    /// Gets the paths of empty parent directories that were cleaned up.
    /// </summary>
    public string[] DeletedDirectories { get; init; } = [];

    /// <summary>
    /// Gets the undo run id that restored this apply run, when available.
    /// </summary>
    public string? UndoneByRunId { get; set; }

    /// <summary>
    /// Gets the UTC timestamp when this apply run was restored, when available.
    /// </summary>
    public DateTimeOffset? UndoneAtUtc { get; set; }
}
