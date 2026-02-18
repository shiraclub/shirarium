using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanOverridesLogic
{
    internal static string? ValidateRequest(
        PatchOrganizationPlanEntryOverridesRequest request,
        OrganizationPlanSnapshot currentPlan)
    {
        if (request is null)
        {
            return "Request body is required.";
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return "ExpectedPlanFingerprint is required.";
        }

        if (!request.ExpectedPlanFingerprint.Equals(currentPlan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return "PlanFingerprintMismatch";
        }

        if (request.Patches.Length == 0)
        {
            return "At least one patch is required.";
        }

        var sourcePathSet = new HashSet<string>(PathComparison.Comparer);
        foreach (var patch in request.Patches)
        {
            if (string.IsNullOrWhiteSpace(patch.SourcePath))
            {
                return "Patch source path is required.";
            }

            if (!sourcePathSet.Add(patch.SourcePath))
            {
                return $"Duplicate patch source path: {patch.SourcePath}";
            }

            if (patch.Remove)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(patch.Action)
                && !OrganizationPlanReviewLogic.IsSupportedAction(patch.Action))
            {
                return $"Unsupported action override: {patch.Action}";
            }

            if (patch.Action is null && patch.TargetPath is null)
            {
                return $"Patch for {patch.SourcePath} must set Action or TargetPath, or Remove.";
            }
        }

        return null;
    }

    internal static PatchResult ApplyPatches(
        OrganizationPlanOverridesSnapshot currentSnapshot,
        PatchOrganizationPlanEntryOverridesRequest request,
        string planFingerprint)
    {
        var overrideMap = OrganizationPlanReviewLogic.BuildOverrideMap(currentSnapshot);
        var updatedCount = 0;
        var removedCount = 0;

        foreach (var patch in request.Patches)
        {
            if (patch.Remove)
            {
                if (overrideMap.Remove(patch.SourcePath))
                {
                    removedCount++;
                }

                continue;
            }

            overrideMap.TryGetValue(patch.SourcePath, out var existing);

            var action = existing?.Action;
            if (patch.Action is not null)
            {
                action = OrganizationPlanReviewLogic.NormalizeAction(patch.Action);
            }

            var targetPath = existing?.TargetPath;
            if (patch.TargetPath is not null)
            {
                targetPath = string.IsNullOrWhiteSpace(patch.TargetPath)
                    ? null
                    : patch.TargetPath.Trim();
            }

            if (string.IsNullOrWhiteSpace(action) && string.IsNullOrWhiteSpace(targetPath))
            {
                if (overrideMap.Remove(patch.SourcePath))
                {
                    removedCount++;
                }

                continue;
            }

            overrideMap[patch.SourcePath] = new OrganizationPlanEntryOverride
            {
                SourcePath = patch.SourcePath,
                Action = action,
                TargetPath = targetPath
            };
            updatedCount++;
        }

        var updatedSnapshot = new OrganizationPlanOverridesSnapshot
        {
            PlanFingerprint = planFingerprint,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            Entries = overrideMap.Values
                .OrderBy(entry => entry.SourcePath, PathComparison.Comparer)
                .ToArray()
        };

        return new PatchResult
        {
            Snapshot = updatedSnapshot,
            UpdatedCount = updatedCount,
            RemovedCount = removedCount
        };
    }

    internal sealed class PatchResult
    {
        public OrganizationPlanOverridesSnapshot Snapshot { get; init; } = new();

        public int UpdatedCount { get; init; }

        public int RemovedCount { get; init; }
    }
}
