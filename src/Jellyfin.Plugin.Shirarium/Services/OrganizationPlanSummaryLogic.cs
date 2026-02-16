using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationPlanSummaryLogic
{
    internal static OrganizationPlanSummaryResponse Build(OrganizationPlanSnapshot plan)
    {
        return new OrganizationPlanSummaryResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            PlanFingerprint = plan.PlanFingerprint,
            RootPath = plan.RootPath,
            TotalEntries = plan.Entries.Length,
            SourceSuggestionCount = plan.SourceSuggestionCount,
            PlannedCount = plan.PlannedCount,
            NoopCount = plan.NoopCount,
            SkippedCount = plan.SkippedCount,
            ConflictCount = plan.ConflictCount,
            ActionCounts = BuildCounts(plan.Entries.Select(entry => entry.Action)),
            StrategyCounts = BuildCounts(plan.Entries.Select(entry => entry.Strategy)),
            ReasonCounts = BuildCounts(plan.Entries.Select(entry => entry.Reason)),
            TopTargetFolders = BuildTopTargetFolders(plan)
        };
    }

    private static OrganizationPlanCountBucket[] BuildCounts(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrganizationPlanCountBucket
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OrganizationPlanTargetFolderBucket[] BuildTopTargetFolders(OrganizationPlanSnapshot plan)
    {
        return plan.Entries
            .Where(entry =>
                string.Equals(entry.Action, "move", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(entry.TargetPath))
            .Select(entry => GetTopTargetFolder(entry.TargetPath!, plan.RootPath))
            .Where(folder => !string.IsNullOrWhiteSpace(folder))
            .GroupBy(folder => folder, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OrganizationPlanTargetFolderBucket
            {
                Folder = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Folder, StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToArray();
    }

    private static string GetTopTargetFolder(string targetPath, string rootPath)
    {
        try
        {
            var fullTarget = Path.GetFullPath(targetPath);
            var fullRoot = string.IsNullOrWhiteSpace(rootPath) ? string.Empty : Path.GetFullPath(rootPath);

            if (!string.IsNullOrWhiteSpace(fullRoot))
            {
                var relative = Path.GetRelativePath(fullRoot, fullTarget);
                if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                {
                    var segments = SplitPath(relative);
                    if (segments.Length > 0)
                    {
                        return segments[0];
                    }
                }
            }

            var targetDirectory = Path.GetDirectoryName(fullTarget);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                var leaf = Path.GetFileName(targetDirectory);
                if (!string.IsNullOrWhiteSpace(leaf))
                {
                    return leaf;
                }
            }
        }
        catch
        {
        }

        return "(unknown)";
    }

    private static string[] SplitPath(string path)
    {
        return path
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
