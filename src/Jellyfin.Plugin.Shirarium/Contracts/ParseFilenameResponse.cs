namespace Jellyfin.Plugin.Shirarium.Contracts;

public sealed class ParseFilenameResponse
{
    public string Title { get; init; } = "Unknown Title";

    public string MediaType { get; init; } = "unknown";

    public int? Year { get; init; }

    public int? Season { get; init; }

    public int? Episode { get; init; }

    public double Confidence { get; init; }

    public string Source { get; init; } = "heuristic";

    public IReadOnlyList<string> RawTokens { get; init; } = Array.Empty<string>();
}

