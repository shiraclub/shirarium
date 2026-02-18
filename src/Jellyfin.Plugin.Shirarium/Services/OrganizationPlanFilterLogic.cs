using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanFilterLogic
{
    internal static FilterSelectionResult Select(OrganizationPlanSnapshot plan, ApplyPlanByFilterRequest request)
    {
        var moveCandidates = plan.Entries
            .Where(entry => string.Equals(entry.Action, "move", StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.SourcePath, PathComparison.Comparer)
            .ThenBy(entry => entry.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var strategies = BuildSet(request.Strategies);
        var reasons = BuildSet(request.Reasons);
        var normalizedPrefix = NormalizePathPrefix(request.PathPrefix);

        var filtered = moveCandidates.Where(entry =>
        {
            if (strategies.Count > 0 && !strategies.Contains(entry.Strategy))
            {
                return false;
            }

            if (reasons.Count > 0 && !reasons.Contains(entry.Reason))
            {
                return false;
            }

            if (request.MinConfidence.HasValue && entry.Confidence < request.MinConfidence.Value)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(normalizedPrefix)
                && !NormalizePathPrefix(entry.SourcePath).StartsWith(normalizedPrefix, PathComparison.Comparison))
            {
                return false;
            }

            return true;
        });

        if (request.Limit is > 0)
        {
            filtered = filtered.Take(request.Limit.Value);
        }

        var selectedSourcePaths = filtered
            .Select(entry => entry.SourcePath)
            .ToArray();

        return new FilterSelectionResult
        {
            MoveCandidateCount = moveCandidates.Length,
            SelectedSourcePaths = selectedSourcePaths
        };
    }

    internal static string? Validate(ApplyPlanByFilterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return "ExpectedPlanFingerprint is required.";
        }

        if (request.MinConfidence is < 0 or > 1)
        {
            return "MinConfidence must be within [0, 1].";
        }

        if (request.Limit is <= 0)
        {
            return "Limit must be greater than 0 when provided.";
        }

        return null;
    }

    private static HashSet<string> BuildSet(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizePathPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value
            .Trim()
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
    }

    internal sealed class FilterSelectionResult
    {
        public int MoveCandidateCount { get; init; }

        public string[] SelectedSourcePaths { get; init; } = [];
    }
}
