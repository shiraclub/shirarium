namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Compact metadata view for one persisted review lock.
/// </summary>
public sealed class ReviewLockSummary
{
    /// <summary>
    /// Gets immutable review id.
    /// </summary>
    public string ReviewId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC lock creation timestamp.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>
    /// Gets the plan fingerprint bound to this lock.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets selected source path count.
    /// </summary>
    public int SelectedCount { get; init; }

    /// <summary>
    /// Gets apply run id that consumed this lock, when available.
    /// </summary>
    public string? AppliedRunId { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this lock was consumed, when available.
    /// </summary>
    public DateTimeOffset? AppliedAtUtc { get; init; }
}
