namespace Jellyfin.Plugin.Shirarium.Contracts;

public sealed class ParseFilenameRequest
{
    public required string Path { get; init; }
}

