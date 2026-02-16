namespace Jellyfin.Plugin.Shirarium.Contracts;

/// <summary>
/// Request payload for filename parsing in the engine service.
/// </summary>
public sealed class ParseFilenameRequest
{
    /// <summary>
    /// Gets the media file path or filename to parse.
    /// </summary>
    public required string Path { get; init; }
}
