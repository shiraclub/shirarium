namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Count for one machine-readable reason bucket.
/// </summary>
public sealed class OpsStatusReasonCount
{
    /// <summary>
    /// Gets the reason key.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of occurrences for this reason.
    /// </summary>
    public int Count { get; init; }
}
