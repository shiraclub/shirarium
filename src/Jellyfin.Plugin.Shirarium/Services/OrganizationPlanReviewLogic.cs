using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanReviewLogic
{
    private static readonly HashSet<string> SupportedActions =
    [
        "move",
        "skip",
        "none",
        "conflict"
    ];

    internal static string? NormalizeAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return null;
        }

        var normalized = action.Trim().ToLowerInvariant();
        return SupportedActions.Contains(normalized) ? normalized : null;
    }

    internal static bool IsSupportedAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        return SupportedActions.Contains(action.Trim().ToLowerInvariant());
    }

    internal static Dictionary<string, OrganizationPlanEntryOverride> BuildOverrideMap(
        OrganizationPlanOverridesSnapshot overridesSnapshot)
    {
        return overridesSnapshot.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.SourcePath))
            .GroupBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    internal static OrganizationPlanSnapshot BuildEffectivePlan(
        OrganizationPlanSnapshot basePlan,
        OrganizationPlanOverridesSnapshot overridesSnapshot)
    {
        var overrideMap = BuildOverrideMap(overridesSnapshot);
        var entries = basePlan.Entries
            .Select(entry => ApplyOverride(entry, overrideMap))
            .ToArray();

        return new OrganizationPlanSnapshot
        {
            GeneratedAtUtc = basePlan.GeneratedAtUtc,
            PlanFingerprint = basePlan.PlanFingerprint,
            RootPath = basePlan.RootPath,
            DryRunMode = basePlan.DryRunMode,
            SourceSuggestionCount = basePlan.SourceSuggestionCount,
            PlannedCount = entries.Count(entry => string.Equals(entry.Action, "move", StringComparison.OrdinalIgnoreCase)),
            NoopCount = entries.Count(entry => string.Equals(entry.Action, "none", StringComparison.OrdinalIgnoreCase)),
            SkippedCount = entries.Count(entry => string.Equals(entry.Action, "skip", StringComparison.OrdinalIgnoreCase)),
            ConflictCount = entries.Count(entry => string.Equals(entry.Action, "conflict", StringComparison.OrdinalIgnoreCase)),
            Entries = entries
        };
    }

    internal static OrganizationPlanEntry ApplyOverride(
        OrganizationPlanEntry entry,
        IReadOnlyDictionary<string, OrganizationPlanEntryOverride> overrideMap)
    {
        overrideMap.TryGetValue(entry.SourcePath, out var entryOverride);
        return ApplyOverride(entry, entryOverride);
    }

    internal static OrganizationPlanEntry ApplyOverride(
        OrganizationPlanEntry entry,
        OrganizationPlanEntryOverride? entryOverride)
    {
        var action = entry.Action;
        var targetPath = entry.TargetPath;

        if (entryOverride is not null)
        {
            var normalizedAction = NormalizeAction(entryOverride.Action);
            if (!string.IsNullOrWhiteSpace(normalizedAction))
            {
                action = normalizedAction;
            }

            if (entryOverride.TargetPath is not null)
            {
                targetPath = string.IsNullOrWhiteSpace(entryOverride.TargetPath)
                    ? null
                    : entryOverride.TargetPath.Trim();
            }
        }

        return new OrganizationPlanEntry
        {
            ItemId = entry.ItemId,
            SourcePath = entry.SourcePath,
            TargetPath = targetPath,
            Strategy = entry.Strategy,
            Action = action,
            Reason = entry.Reason,
            Confidence = entry.Confidence,
            SuggestedTitle = entry.SuggestedTitle,
            SuggestedMediaType = entry.SuggestedMediaType
        };
    }
}
