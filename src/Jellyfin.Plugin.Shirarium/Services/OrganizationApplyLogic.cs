using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationApplyLogic
{
    internal static ApplyOrganizationPlanResult ApplySelected(
        OrganizationPlanSnapshot plan,
        IEnumerable<string> selectedSourcePaths,
        CancellationToken cancellationToken = default)
    {
        return ApplySelected(
            plan,
            selectedSourcePaths,
            File.Exists,
            path => _ = Directory.CreateDirectory(path),
            File.Move,
            cancellationToken);
    }

    internal static ApplyOrganizationPlanResult ApplySelected(
        OrganizationPlanSnapshot plan,
        IEnumerable<string> selectedSourcePaths,
        Func<string, bool> fileExists,
        Action<string> ensureDirectory,
        Action<string, string> moveFile,
        CancellationToken cancellationToken = default)
    {
        var rootValidation = TryGetCanonicalPath(plan.RootPath, out var canonicalRootPath);

        var normalizedSelections = new HashSet<string>(
            selectedSourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)),
            StringComparer.OrdinalIgnoreCase);

        var itemResults = new List<ApplyOrganizationPlanItemResult>(normalizedSelections.Count);
        var appliedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        foreach (var selectedPath in normalizedSelections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entry = plan.Entries.FirstOrDefault(candidate => PathEquals(candidate.SourcePath, selectedPath));
            if (entry is null)
            {
                skippedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = selectedPath,
                    Status = "skipped",
                    Reason = "NotFoundInPlan"
                });
                continue;
            }

            if (!entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase))
            {
                skippedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = entry.TargetPath,
                    Status = "skipped",
                    Reason = "NotMoveAction"
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.TargetPath))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    Status = "failed",
                    Reason = "MissingTargetPath"
                });
                continue;
            }

            if (!rootValidation)
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = entry.TargetPath,
                    Status = "failed",
                    Reason = "InvalidPlanRootPath"
                });
                continue;
            }

            if (!TryGetCanonicalPath(entry.SourcePath, out var canonicalSourcePath))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = entry.TargetPath,
                    Status = "failed",
                    Reason = "InvalidSourcePath"
                });
                continue;
            }
            var sourcePath = canonicalSourcePath!;

            if (!TryGetCanonicalPath(entry.TargetPath, out var canonicalTargetPath))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = entry.TargetPath,
                    Status = "failed",
                    Reason = "InvalidTargetPath"
                });
                continue;
            }
            var targetPath = canonicalTargetPath!;

            if (!IsUnderRoot(targetPath, canonicalRootPath!))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "failed",
                    Reason = "TargetOutsideRootPath"
                });
                continue;
            }

            if (!IsSameVolume(sourcePath, targetPath))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "failed",
                    Reason = "CrossVolumeMoveNotAllowed"
                });
                continue;
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "failed",
                    Reason = "MissingTargetDirectory"
                });
                continue;
            }

            if (!fileExists(sourcePath))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "failed",
                    Reason = "SourceMissing"
                });
                continue;
            }

            if (fileExists(targetPath))
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "failed",
                    Reason = "TargetAlreadyExists"
                });
                continue;
            }

            try
            {
                ensureDirectory(targetDirectory);
                moveFile(sourcePath, targetPath);
                appliedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "applied",
                    Reason = "Moved"
                });
            }
            catch (Exception ex)
            {
                failedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "failed",
                    Reason = $"MoveFailed:{ex.GetType().Name}"
                });
            }
        }

        return new ApplyOrganizationPlanResult
        {
            AppliedAtUtc = DateTimeOffset.UtcNow,
            PlanRootPath = plan.RootPath,
            RequestedCount = normalizedSelections.Count,
            AppliedCount = appliedCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            Results = itemResults.ToArray()
        };
    }

    private static bool TryGetCanonicalPath(string? path, out string? canonicalPath)
    {
        canonicalPath = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            canonicalPath = Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUnderRoot(string path, string rootPath)
    {
        if (path.Equals(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = rootPath + Path.DirectorySeparatorChar;
        return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSameVolume(string sourcePath, string targetPath)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        var sourceRoot = Path.GetPathRoot(sourcePath) ?? string.Empty;
        var targetRoot = Path.GetPathRoot(targetPath) ?? string.Empty;
        return sourceRoot.Equals(targetRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            var leftFull = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rightFull = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(leftFull, rightFull, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
