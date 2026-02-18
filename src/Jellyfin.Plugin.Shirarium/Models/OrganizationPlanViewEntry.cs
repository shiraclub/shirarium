namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// One organization-plan entry rendered for review UI/API consumption.
/// </summary>
public sealed class OrganizationPlanViewEntry
{
    /// <summary>
    /// Gets the source path.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets Jellyfin item id.
    /// </summary>
    public string ItemId { get; init; } = string.Empty;

    /// <summary>
    /// Gets suggested title.
    /// </summary>
    public string SuggestedTitle { get; init; } = string.Empty;

    /// <summary>
    /// Gets suggested media type.
    /// </summary>
    public string SuggestedMediaType { get; init; } = string.Empty;

    /// <summary>
    /// Gets strategy from the plan entry.
    /// </summary>
    public string Strategy { get; init; } = string.Empty;

    /// <summary>
    /// Gets reason from the plan entry.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Gets confidence from the plan entry.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets original action from stored plan entry.
    /// </summary>
    public string BaseAction { get; init; } = string.Empty;

    /// <summary>
    /// Gets effective action after applying override.
    /// </summary>
    public string EffectiveAction { get; init; } = string.Empty;

    /// <summary>
    /// Gets original target path from stored plan entry.
    /// </summary>
    public string? BaseTargetPath { get; init; }

    /// <summary>
    /// Gets effective target path after applying override.
    /// </summary>
    public string? EffectiveTargetPath { get; init; }

    /// <summary>
    /// Gets a value indicating whether this entry has an override.
    /// </summary>
    public bool HasOverride { get; init; }

    /// <summary>
    /// Gets optional action override value.
    /// </summary>
    public string? OverrideAction { get; init; }

    /// <summary>
    /// Gets optional target-path override value.
    /// </summary>
    public string? OverrideTargetPath { get; init; }

    /// <summary>
    /// Gets the video resolution.
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Gets the video codec.
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Gets the video bit depth.
    /// </summary>
    public string? VideoBitDepth { get; init; }

    /// <summary>
    /// Gets the audio codec.
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Gets the audio channels.
    /// </summary>
    public string? AudioChannels { get; init; }

    /// <summary>
    /// Gets the release group.
    /// </summary>
    public string? ReleaseGroup { get; init; }

    /// <summary>
    /// Gets the media source.
    /// </summary>
    public string? MediaSource { get; init; }

    /// <summary>
    /// Gets the edition.
    /// </summary>
    public string? Edition { get; init; }

    /// <summary>
    /// Gets the number of associated files.
    /// </summary>
    public int AssociatedFilesCount { get; init; }
}

