using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class PlanFingerprint
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    internal static string Compute(OrganizationPlanSnapshot snapshot)
    {
        var payload = new FingerprintPayload
        {
            RootPath = snapshot.RootPath,
            DryRunMode = snapshot.DryRunMode,
            SourceSuggestionCount = snapshot.SourceSuggestionCount,
            PlannedCount = snapshot.PlannedCount,
            NoopCount = snapshot.NoopCount,
            SkippedCount = snapshot.SkippedCount,
            ConflictCount = snapshot.ConflictCount,
            Entries = snapshot.Entries
                .OrderBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.TargetPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.ItemId, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new FingerprintEntry
                {
                    ItemId = entry.ItemId,
                    SourcePath = entry.SourcePath,
                    TargetPath = entry.TargetPath,
                    Strategy = entry.Strategy,
                    Action = entry.Action,
                    Reason = entry.Reason,
                    Confidence = entry.Confidence,
                    SuggestedTitle = entry.SuggestedTitle,
                    SuggestedMediaType = entry.SuggestedMediaType
                })
                .ToArray()
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private sealed class FingerprintPayload
    {
        public string RootPath { get; init; } = string.Empty;

        public bool DryRunMode { get; init; }

        public int SourceSuggestionCount { get; init; }

        public int PlannedCount { get; init; }

        public int NoopCount { get; init; }

        public int SkippedCount { get; init; }

        public int ConflictCount { get; init; }

        public FingerprintEntry[] Entries { get; init; } = [];
    }

    private sealed class FingerprintEntry
    {
        public string ItemId { get; init; } = string.Empty;

        public string SourcePath { get; init; } = string.Empty;

        public string? TargetPath { get; init; }

        public string Strategy { get; init; } = "unknown";

        public string Action { get; init; } = "skip";

        public string Reason { get; init; } = string.Empty;

        public double Confidence { get; init; }

        public string SuggestedTitle { get; init; } = "Unknown Title";

        public string SuggestedMediaType { get; init; } = "unknown";
    }
}
