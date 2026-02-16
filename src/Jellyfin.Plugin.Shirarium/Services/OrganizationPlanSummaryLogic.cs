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
        var normalizedTarget = NormalizeLogicalPath(targetPath);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return "(unknown)";
        }

        var normalizedRoot = NormalizeLogicalPath(rootPath);
        if (!string.IsNullOrWhiteSpace(normalizedRoot)
            && TryGetRelativePath(normalizedTarget, normalizedRoot, out var relativePath))
        {
            var relativeSegments = SplitPath(relativePath);
            if (relativeSegments.Length > 0)
            {
                return relativeSegments[0];
            }
        }

        var segments = SplitPath(normalizedTarget);
        if (segments.Length == 0)
        {
            return "(unknown)";
        }

        var startIndex = LooksLikeDrivePrefix(segments[0]) ? 1 : 0;
        if (startIndex >= segments.Length)
        {
            return "(unknown)";
        }

        var fileSegmentIndex = segments.Length - 1;
        if (Path.HasExtension(segments[fileSegmentIndex]) && fileSegmentIndex > startIndex)
        {
            return segments[fileSegmentIndex - 1];
        }

        return segments[fileSegmentIndex];
    }

    private static string[] SplitPath(string path)
    {
        return path
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryGetRelativePath(string targetPath, string rootPath, out string relativePath)
    {
        relativePath = string.Empty;

        if (string.Equals(targetPath, rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedRoot = rootPath.EndsWith("/", StringComparison.Ordinal)
            ? rootPath
            : rootPath + "/";

        if (!targetPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = targetPath[normalizedRoot.Length..];
        return true;
    }

    private static bool LooksLikeDrivePrefix(string segment)
    {
        return segment.Length == 2
            && char.IsLetter(segment[0])
            && segment[1] == ':';
    }

    private static string NormalizeLogicalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path
            .Trim()
            .Replace('\\', '/');

        while (normalized.Contains("//", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("//", "/", StringComparison.Ordinal);
        }

        if (normalized.Length > 1)
        {
            normalized = normalized.TrimEnd('/');
        }

        if (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
        {
            normalized = char.ToUpperInvariant(normalized[0]) + normalized[1..];
        }

        return normalized;
    }
}
