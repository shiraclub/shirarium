namespace Jellyfin.Plugin.Shirarium.Models;

/// <summary>
/// Request payload for testing a path template against a sample filename.
/// </summary>
public sealed class TestTemplateRequest
{
    /// <summary>
    /// Gets or sets the sample filename or path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the movie path template.
    /// </summary>
    public string? MoviePathTemplate { get; set; }

    /// <summary>
    /// Gets or sets the episode path template.
    /// </summary>
    public string? EpisodePathTemplate { get; set; }

    /// <summary>
    /// Gets or sets the root organization path.
    /// </summary>
    public string? RootPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to normalize path segments.
    /// </summary>
    public bool? NormalizePathSegments { get; set; }
}
