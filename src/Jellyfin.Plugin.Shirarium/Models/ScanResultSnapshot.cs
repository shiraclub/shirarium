namespace Jellyfin.Plugin.Shirarium.Models;

public sealed class ScanResultSnapshot
{
    public DateTimeOffset GeneratedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public bool DryRunMode { get; init; } = true;

    public int ExaminedCount { get; init; }

    public int CandidateCount { get; init; }

    public int ParsedCount { get; init; }

    public int SkippedByLimitCount { get; init; }

    public int SkippedByConfidenceCount { get; init; }

    public int EngineFailureCount { get; init; }

    public ScanSuggestion[] Suggestions { get; init; } = [];
}
