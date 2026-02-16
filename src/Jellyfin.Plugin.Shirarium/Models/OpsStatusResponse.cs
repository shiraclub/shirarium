namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Aggregated operational status for plan/apply/undo snapshots.
/// </summary>
public sealed class OpsStatusResponse
{
    /// <summary>
    /// Gets the UTC timestamp when this status was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets latest scan status details.
    /// </summary>
    public OpsStatusScanStatus Scan { get; init; } = new();

    /// <summary>
    /// Gets latest plan status details.
    /// </summary>
    public OpsStatusPlanStatus Plan { get; init; } = new();

    /// <summary>
    /// Gets latest apply run summary when available.
    /// </summary>
    public OpsStatusApplyRunStatus? LastApplyRun { get; init; }

    /// <summary>
    /// Gets latest undo run summary when available.
    /// </summary>
    public OpsStatusUndoRunStatus? LastUndoRun { get; init; }
}
