namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Server-side filtered and paged organization-plan review view.
/// </summary>
public sealed class OrganizationPlanViewResponse
{
    /// <summary>
    /// Gets UTC timestamp when this view payload was generated.
    /// </summary>
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets current plan fingerprint.
    /// </summary>
    public string PlanFingerprint { get; init; } = string.Empty;

    /// <summary>
    /// Gets total number of plan entries before filtering.
    /// </summary>
    public int TotalEntries { get; init; }

    /// <summary>
    /// Gets total number of entries after filtering and before paging.
    /// </summary>
    public int FilteredEntries { get; init; }

    /// <summary>
    /// Gets number of entries that currently have overrides.
    /// </summary>
    public int OverrideCount { get; init; }

    /// <summary>
    /// Gets current page number.
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Gets current page size.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Gets sort key used.
    /// </summary>
    public string SortBy { get; init; } = "sourcePath";

    /// <summary>
    /// Gets sort direction used.
    /// </summary>
    public string SortDirection { get; init; } = "asc";

    /// <summary>
    /// Gets paged review entries.
    /// </summary>
    public OrganizationPlanViewEntry[] Entries { get; init; } = [];
}

