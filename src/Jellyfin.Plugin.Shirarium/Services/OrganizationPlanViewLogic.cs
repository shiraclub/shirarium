using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanViewLogic
{
    internal static string? ValidateRequest(OrganizationPlanViewRequest request)
    {
        if (request.MinConfidence is < 0 or > 1)
        {
            return "MinConfidence must be within [0, 1].";
        }

        if (request.Page <= 0)
        {
            return "Page must be greater than 0.";
        }

        if (request.PageSize <= 0 || request.PageSize > 1000)
        {
            return "PageSize must be within [1, 1000].";
        }

        var normalizedSortBy = NormalizeSortBy(request.SortBy);
        if (string.IsNullOrWhiteSpace(normalizedSortBy))
        {
            return "SortBy must be one of: sourcePath, targetPath, confidence, strategy, action, reason.";
        }

        var normalizedDirection = NormalizeSortDirection(request.SortDirection);
        if (string.IsNullOrWhiteSpace(normalizedDirection))
        {
            return "SortDirection must be asc or desc.";
        }

        return null;
    }

    internal static OrganizationPlanViewResponse Build(
        OrganizationPlanSnapshot plan,
        OrganizationPlanOverridesSnapshot overridesSnapshot,
        OrganizationPlanViewRequest request,
        ScanResultSnapshot? scanSnapshot = null)
    {
        var overrideMap = OrganizationPlanReviewLogic.BuildOverrideMap(overridesSnapshot);
        var suggestionMap = scanSnapshot?.Suggestions.ToDictionary(s => s.SourcePath, s => s, PathComparison.Comparer) 
            ?? new Dictionary<string, ScanSuggestion>(PathComparison.Comparer);
            
        var strategySet = BuildSet(request.Strategies);
        var actionSet = BuildSet(request.Actions);
        var reasonSet = BuildSet(request.Reasons);
        var pathPrefix = NormalizePathPrefix(request.PathPrefix);

        var entries = plan.Entries
            .Select(entry =>
            {
                overrideMap.TryGetValue(entry.SourcePath, out var entryOverride);
                suggestionMap.TryGetValue(entry.SourcePath, out var suggestion);
                var effectiveEntry = OrganizationPlanReviewLogic.ApplyOverride(entry, entryOverride);

                return new OrganizationPlanViewEntry
                {
                    SourcePath = entry.SourcePath,
                    ItemId = entry.ItemId,
                    SuggestedTitle = entry.SuggestedTitle,
                    SuggestedMediaType = entry.SuggestedMediaType,
                    Strategy = entry.Strategy,
                    Reason = entry.Reason,
                    Confidence = entry.Confidence,
                    BaseAction = entry.Action,
                    EffectiveAction = effectiveEntry.Action,
                    BaseTargetPath = entry.TargetPath,
                    EffectiveTargetPath = effectiveEntry.TargetPath,
                    HasOverride = entryOverride is not null,
                    OverrideAction = entryOverride?.Action,
                    OverrideTargetPath = entryOverride?.TargetPath,
                    Resolution = suggestion?.Resolution,
                    VideoCodec = suggestion?.VideoCodec,
                    VideoBitDepth = suggestion?.VideoBitDepth,
                    AudioCodec = suggestion?.AudioCodec,
                    AudioChannels = suggestion?.AudioChannels,
                    ReleaseGroup = suggestion?.ReleaseGroup,
                    MediaSource = suggestion?.MediaSource,
                    Edition = suggestion?.Edition,
                    AssociatedFilesCount = entry.AssociatedFiles?.Length ?? 0
                };
            })
            .Where(viewEntry =>
            {
                if (strategySet.Count > 0 && !strategySet.Contains(viewEntry.Strategy))
                {
                    return false;
                }

                if (actionSet.Count > 0 && !actionSet.Contains(viewEntry.EffectiveAction))
                {
                    return false;
                }

                if (reasonSet.Count > 0 && !reasonSet.Contains(viewEntry.Reason))
                {
                    return false;
                }

                if (request.MinConfidence.HasValue && viewEntry.Confidence < request.MinConfidence.Value)
                {
                    return false;
                }

                if (request.OverridesOnly && !viewEntry.HasOverride)
                {
                    return false;
                }

                if (request.MovesOnly && !string.Equals(viewEntry.EffectiveAction, "move", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(pathPrefix)
                    && !NormalizePathPrefix(viewEntry.SourcePath).StartsWith(pathPrefix, PathComparison.Comparison))
                {
                    return false;
                }

                return true;
            });

        entries = Sort(entries, NormalizeSortBy(request.SortBy)!, NormalizeSortDirection(request.SortDirection)!);
        var filteredEntries = entries.ToArray();

        var skip = (request.Page - 1) * request.PageSize;
        var pageEntries = filteredEntries
            .Skip(skip)
            .Take(request.PageSize)
            .ToArray();

        return new OrganizationPlanViewResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            PlanFingerprint = plan.PlanFingerprint,
            TotalEntries = plan.Entries.Length,
            FilteredEntries = filteredEntries.Length,
            OverrideCount = overrideMap.Count,
            Page = request.Page,
            PageSize = request.PageSize,
            SortBy = NormalizeSortBy(request.SortBy)!,
            SortDirection = NormalizeSortDirection(request.SortDirection)!,
            Entries = pageEntries
        };
    }

    private static IEnumerable<OrganizationPlanViewEntry> Sort(
        IEnumerable<OrganizationPlanViewEntry> entries,
        string sortBy,
        string sortDirection)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);

        IOrderedEnumerable<OrganizationPlanViewEntry> ordered = sortBy switch
        {
            "targetPath" => descending
                ? entries.OrderByDescending(entry => entry.EffectiveTargetPath, PathComparison.Comparer)
                : entries.OrderBy(entry => entry.EffectiveTargetPath, PathComparison.Comparer),
            "confidence" => descending
                ? entries.OrderByDescending(entry => entry.Confidence)
                : entries.OrderBy(entry => entry.Confidence),
            "strategy" => descending
                ? entries.OrderByDescending(entry => entry.Strategy, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Strategy, StringComparer.OrdinalIgnoreCase),
            "action" => descending
                ? entries.OrderByDescending(entry => entry.EffectiveAction, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.EffectiveAction, StringComparer.OrdinalIgnoreCase),
            "reason" => descending
                ? entries.OrderByDescending(entry => entry.Reason, StringComparer.OrdinalIgnoreCase)
                : entries.OrderBy(entry => entry.Reason, StringComparer.OrdinalIgnoreCase),
            _ => descending
                ? entries.OrderByDescending(entry => entry.SourcePath, PathComparison.Comparer)
                : entries.OrderBy(entry => entry.SourcePath, PathComparison.Comparer)
        };

        return ordered.ThenBy(entry => entry.ItemId, StringComparer.OrdinalIgnoreCase);
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

    private static string? NormalizeSortBy(string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return "sourcePath";
        }

        var normalized = sortBy.Trim();
        if (normalized.Equals("sourcePath", StringComparison.OrdinalIgnoreCase))
        {
            return "sourcePath";
        }

        if (normalized.Equals("targetPath", StringComparison.OrdinalIgnoreCase))
        {
            return "targetPath";
        }

        if (normalized.Equals("confidence", StringComparison.OrdinalIgnoreCase))
        {
            return "confidence";
        }

        if (normalized.Equals("strategy", StringComparison.OrdinalIgnoreCase))
        {
            return "strategy";
        }

        if (normalized.Equals("action", StringComparison.OrdinalIgnoreCase))
        {
            return "action";
        }

        if (normalized.Equals("reason", StringComparison.OrdinalIgnoreCase))
        {
            return "reason";
        }

        return null;
    }

    private static string? NormalizeSortDirection(string? direction)
    {
        if (string.IsNullOrWhiteSpace(direction))
        {
            return "asc";
        }

        var normalized = direction.Trim().ToLowerInvariant();
        return normalized is "asc" or "desc" ? normalized : null;
    }
}
