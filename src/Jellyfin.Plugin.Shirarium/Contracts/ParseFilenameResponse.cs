namespace Jellyfin.Plugin.Shirarium.Contracts;

/// <summary>
/// Structured parse result returned by the engine service.
/// </summary>
public sealed record class ParseFilenameResponse
{
    /// <summary>
    /// Gets the inferred title.
    /// </summary>
    public string Title { get; init; } = "Unknown Title";

    /// <summary>
    /// Gets the inferred media type.
    /// </summary>
    public string MediaType { get; init; } = "unknown";

    /// <summary>
    /// Gets the inferred release year for movie-like content.
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Gets the inferred season number for episodic content.
    /// </summary>
    public int? Season { get; init; }

    /// <summary>
    /// Gets the inferred episode number for episodic content.
    /// </summary>
    public int? Episode { get; init; }

    /// <summary>
    /// Gets the confidence score from <c>0.0</c> to <c>1.0</c>.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// Gets the parser source that produced this result.
    /// </summary>
    public string Source { get; init; } = "heuristic";

    /// <summary>
    /// Gets raw tokenized fragments extracted from the filename.
    /// </summary>
    public IReadOnlyList<string> RawTokens { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the inferred video resolution (e.g., 1080p, 720p).
    /// </summary>
    public string? Resolution { get; init; }

    /// <summary>
    /// Gets the inferred video codec (e.g., x264, h265).
    /// </summary>
    public string? VideoCodec { get; init; }

    /// <summary>
    /// Gets the inferred audio codec (e.g., AAC, DTS).
    /// </summary>
    public string? AudioCodec { get; init; }

    /// <summary>
    /// Gets the inferred audio channels (e.g., 5.1, 2.0).
    /// </summary>
    public string? AudioChannels { get; init; }

    /// <summary>
    /// Gets the inferred release group.
    /// </summary>
    public string? ReleaseGroup { get; init; }

    /// <summary>
    /// Gets the inferred media source (e.g., BluRay, WEB-DL).
    /// </summary>
    public string? MediaSource { get; init; }

    /// <summary>
    /// Gets the inferred edition (e.g., Extended, Director's Cut).
    /// </summary>
    public string? Edition { get; init; }
}
