namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Query options for server-side organization-plan view rendering.
/// </summary>
public sealed class OrganizationPlanViewRequest
{
    /// <summary>
    /// Gets optional strategy filters.
    /// </summary>
    public string[] Strategies { get; set; } = [];

    /// <summary>
    /// Gets optional effective action filters.
    /// </summary>
    public string[] Actions { get; set; } = [];

    /// <summary>
    /// Gets optional reason filters.
    /// </summary>
    public string[] Reasons { get; set; } = [];

    /// <summary>
    /// Gets optional source path prefix filter.
    /// </summary>
    public string? PathPrefix { get; set; }

    /// <summary>
    /// Gets optional minimum confidence filter in inclusive range [0, 1].
    /// </summary>
    public double? MinConfidence { get; set; }

    /// <summary>
    /// Gets a value indicating whether only overridden entries should be returned.
    /// </summary>
    public bool OverridesOnly { get; set; }

    /// <summary>
    /// Gets a value indicating whether only effective move entries should be returned.
    /// </summary>
    public bool MovesOnly { get; set; }

    /// <summary>
    /// Gets the 1-based page number.
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public int PageSize { get; set; } = 100;

    /// <summary>
    /// Gets sort key: sourcePath, targetPath, confidence, strategy, action, reason.
    /// </summary>
    public string SortBy { get; set; } = "sourcePath";

    /// <summary>
    /// Gets sort direction: asc or desc.
    /// </summary>
    public string SortDirection { get; set; } = "asc";
}
