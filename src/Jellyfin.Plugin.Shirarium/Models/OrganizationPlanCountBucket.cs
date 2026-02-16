namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Count bucket for plan summaries.
/// </summary>
public sealed class OrganizationPlanCountBucket
{
    /// <summary>
    /// Gets the bucket key.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of entries in this bucket.
    /// </summary>
    public int Count { get; init; }
}
