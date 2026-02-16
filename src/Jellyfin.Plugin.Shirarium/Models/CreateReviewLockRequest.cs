namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for creating an immutable review lock snapshot.
/// </summary>
public sealed class CreateReviewLockRequest
{
    /// <summary>
    /// Gets the expected plan fingerprint that must match the latest stored plan.
    /// </summary>
    public string ExpectedPlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets optional reviewed source paths to lock. When omitted, all reviewed move entries are locked.
    /// </summary>
    public string[] SourcePaths { get; init; } = [];
}
