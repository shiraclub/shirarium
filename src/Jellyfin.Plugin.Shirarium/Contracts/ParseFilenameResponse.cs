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
}
