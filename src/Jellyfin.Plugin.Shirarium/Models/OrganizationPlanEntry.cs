namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// One source-to-target organization decision in a dry-run organization plan.
/// </summary>
public sealed class OrganizationPlanEntry
{
    /// <summary>
    /// Gets the Jellyfin item identifier.
    /// </summary>
    public string ItemId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source media path.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the planned target path.
    /// </summary>
    public string? TargetPath { get; set; }

    /// <summary>
    /// Gets the suggested strategy used to build this path.
    /// </summary>
    public string Strategy { get; set; } = "unknown";

    /// <summary>
    /// Gets the action for this entry: move, none, skip, or conflict.
    /// </summary>
    public string Action { get; set; } = "skip";

    /// <summary>
    /// Gets a reason explaining the decision for this entry.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets the parse confidence from the source suggestion.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the source suggested title.
    /// </summary>
    public string SuggestedTitle { get; init; } = "Unknown Title";

    /// <summary>
    /// Gets the source suggested media type.
    /// </summary>
    public string SuggestedMediaType { get; init; } = "unknown";

    /// <summary>
    /// Gets the associated file moves (NFOs, subtitles, images) planned for this entry.
    /// </summary>
    public AssociatedFileMove[] AssociatedFiles { get; set; } = [];
}
