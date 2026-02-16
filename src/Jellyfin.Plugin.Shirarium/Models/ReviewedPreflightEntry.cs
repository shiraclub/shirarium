namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// One issued reviewed-preflight token entry.
/// </summary>
public sealed class ReviewedPreflightEntry
{
    /// <summary>
    /// Gets snapshot schema version for storage compatibility.
    /// </summary>
    public int SchemaVersion { get; init; } = SnapshotSchemaVersions.ReviewedPreflight;

    /// <summary>
    /// Gets one-time token value.
    /// </summary>
    public string Token { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets UTC timestamp when this token was issued.
    /// </summary>
    public DateTimeOffset IssuedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets UTC timestamp when this token expires.
    /// </summary>
    public DateTimeOffset ExpiresAtUtc { get; init; }

    /// <summary>
    /// Gets plan fingerprint this token is bound to.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets deterministic hash of selected source paths.
    /// </summary>
    public string SelectedSourceHash { get; init; } = string.Empty;
}
