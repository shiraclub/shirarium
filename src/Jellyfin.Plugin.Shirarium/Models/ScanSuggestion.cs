namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Suggested metadata result for one scanned media item.
/// </summary>
public sealed class ScanSuggestion
{
    /// <summary>
    /// Gets the Jellyfin item identifier.
    /// </summary>
    public string ItemId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the original library item display name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the source media path.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Gets the suggested title from parse output.
    /// </summary>
    public string SuggestedTitle { get; init; } = "Unknown Title";

    /// <summary>
    /// Gets the suggested media type from parse output.
    /// </summary>
    public string SuggestedMediaType { get; init; } = "unknown";

    /// <summary>
    /// Gets the suggested release year.
    /// </summary>
    public int? SuggestedYear { get; init; }

    /// <summary>
    /// Gets the suggested season number.
    /// </summary>
    public int? SuggestedSeason { get; init; }

    /// <summary>
    /// Gets the suggested episode number.
    /// </summary>
    public int? SuggestedEpisode { get; init; }

    /// <summary>
    /// Gets the confidence score associated with this suggestion.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the parse source that produced this suggestion.
    /// </summary>
    public string Source { get; init; } = "heuristic";

    /// <summary>
    /// Gets heuristic reasons why the item was considered a candidate.
    /// </summary>
    public string[] CandidateReasons { get; init; } = [];

    /// <summary>
    /// Gets raw parser tokens from the original filename.
    /// </summary>
    public string[] RawTokens { get; init; } = [];

    /// <summary>
    /// Gets the UTC timestamp when this suggestion was generated.
    /// </summary>
    public DateTimeOffset ScannedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the video resolution (e.g., 1080p, 2160p).
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Gets the video codec (e.g., HEVC, x264).
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Gets the video bit depth (e.g., 8bit, 10bit).
    /// </summary>
    public string? VideoBitDepth { get; init; }

    /// <summary>
    /// Gets the audio codec (e.g., DTS, AAC, AC3).
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Gets the number of audio channels (e.g., 5.1, 2.0).
    /// </summary>
    public string? AudioChannels { get; init; }

    /// <summary>
    /// Gets the release group (e.g., YTS, RARBG).
    /// </summary>
    public string? ReleaseGroup { get; init; }

    /// <summary>
    /// Gets the media source (e.g., BluRay, WEBRip, DVD).
    /// </summary>
    public string? MediaSource { get; init; }

    /// <summary>
    /// Gets the edition (e.g., Extended Cut, Unrated).
    /// </summary>
    public string? Edition { get; init; }
}
