namespace Jellyfin.Plugin.Shirarium.Models;

public sealed class ScanSuggestion
{
    public string ItemId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public string SuggestedTitle { get; init; } = "Unknown Title";

    public string SuggestedMediaType { get; init; } = "unknown";

    public int? SuggestedYear { get; init; }

    public int? SuggestedSeason { get; init; }

    public int? SuggestedEpisode { get; init; }

    public double Confidence { get; init; }

    public string Source { get; init; } = "heuristic";

    public string[] CandidateReasons { get; init; } = [];

    public string[] RawTokens { get; init; } = [];

    public DateTimeOffset ScannedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
