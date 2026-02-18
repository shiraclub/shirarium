namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Result for one selected organization plan apply attempt.
/// </summary>
public sealed class ApplyOrganizationPlanItemResult
{
    /// <summary>
    /// Gets the selected source path.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target path associated with the selected source path when available.
    /// </summary>
    public string? TargetPath { get; init; }

    /// <summary>
    /// Gets the apply status for this item: applied, skipped, or failed.
    /// </summary>
    public string Status { get; init; } = "skipped";

    /// <summary>
    /// Gets the machine-readable reason for this item result.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets the results for each associated file move attempt.
    /// </summary>
    public AssociatedFileResult[] AssociatedResults { get; set; } = [];
}
