namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Response payload for immutable review lock creation.
/// </summary>
public sealed class CreateReviewLockResponse
{
    /// <summary>
    /// Gets the immutable review lock id.
    /// </summary>
    public string ReviewId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the UTC timestamp when this lock was created.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the plan fingerprint locked by this snapshot.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets how many reviewed move entries were available when the lock was created.
    /// </summary>
    public int MoveCandidateCount { get; init; }

    /// <summary>
    /// Gets how many source paths were selected into this lock.
    /// </summary>
    public int SelectedCount { get; init; }
}
