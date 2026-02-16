namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Stored reviewed-preflight token snapshot.
/// </summary>
public sealed class ReviewedPreflightSnapshot
{
    /// <summary>
    /// Gets issued token entries.
    /// </summary>
    public ReviewedPreflightEntry[] Entries { get; init; } = [];
}
