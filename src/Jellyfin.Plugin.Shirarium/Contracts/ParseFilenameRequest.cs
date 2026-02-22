namespace Jellyfin.Plugin.Shirarium.Contracts;

/// <summary>
/// Request payload for filename parsing.
/// </summary>
public sealed class ParseFilenameRequest
{
    /// <summary>
    /// Gets the media file path or filename to parse.
    /// </summary>
    public required string Path { get; init; }
}
