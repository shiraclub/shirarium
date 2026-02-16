namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Immutable reviewed snapshot used for deterministic apply-by-review-id operations.
/// </summary>
public sealed class ReviewLockSnapshot
{
    /// <summary>
    /// Gets the immutable review lock id.
    /// </summary>
    public string ReviewId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the UTC timestamp when this review lock was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the plan fingerprint bound to this lock.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets the plan root path bound to this lock.
    /// </summary>
    public string PlanRootPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets selected source paths included in this lock.
    /// </summary>
    public string[] SelectedSourcePaths { get; init; } = [];

    /// <summary>
    /// Gets the effective reviewed plan frozen by this lock.
    /// </summary>
    public OrganizationPlanSnapshot EffectivePlan { get; init; } = new();

    /// <summary>
    /// Gets the override snapshot used to build <see cref="EffectivePlan"/>.
    /// </summary>
    public OrganizationPlanOverridesSnapshot OverridesSnapshot { get; init; } = new();

    /// <summary>
    /// Gets the apply run id that consumed this lock, when already applied.
    /// </summary>
    public string? AppliedRunId { get; set; }

    /// <summary>
    /// Gets the UTC timestamp when this lock was consumed, when already applied.
    /// </summary>
    public DateTimeOffset? AppliedAtUtc { get; set; }
}
