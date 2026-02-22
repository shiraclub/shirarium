using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class UndoApplyLogic
{
    private const string TargetConflictPolicyFail = "fail";
    private const string TargetConflictPolicySkip = "skip";
    private const string TargetConflictPolicySuffix = "suffix";

    internal static bool IsSupportedTargetConflictPolicy(string? policy)
    {
        var normalized = NormalizeTargetConflictPolicy(policy);
        return normalized.Equals(TargetConflictPolicyFail, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(TargetConflictPolicySkip, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(TargetConflictPolicySuffix, StringComparison.OrdinalIgnoreCase);
    }

    internal static UndoApplyResult UndoRun(
        ApplyOrganizationPlanResult sourceRun,
        string? targetConflictPolicy,
        IEnumerable<string>? protectedPaths = null,
        CancellationToken cancellationToken = default)
    {
        return UndoRun(
            sourceRun,
            targetConflictPolicy,
            path => File.Exists(path) || Directory.Exists(path),
            path => _ = Directory.CreateDirectory(path),
            MovePath,
            protectedPaths,
            cancellationToken);
    }

    private static void MovePath(string source, string target)
    {
        if (Directory.Exists(source))
        {
            try
            {
                Directory.Move(source, target);
            }
            catch (IOException)
            {
                // Fallback for cross-volume directory move
                CopyDirectory(source, target);
                Directory.Delete(source, true);
            }
        }
        else
        {
            try
            {
                File.Move(source, target);
            }
            catch (IOException)
            {
                // Fallback for cross-volume file move
                File.Copy(source, target);
                File.Delete(source);
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");
        }

        var dirs = dir.GetDirectories();
        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }

    internal static UndoApplyResult UndoRun(
        ApplyOrganizationPlanResult sourceRun,
        string? targetConflictPolicy,
        Func<string, bool> pathExists,
        Action<string> ensureDirectory,
        Action<string, string> movePath,
        IEnumerable<string>? protectedPaths = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedConflictPolicy = NormalizeTargetConflictPolicy(targetConflictPolicy);
        var operations = sourceRun.UndoOperations
            .Where(operation =>
                !string.IsNullOrWhiteSpace(operation.FromPath)
                && !string.IsNullOrWhiteSpace(operation.ToPath))
            .Reverse()
            .ToArray();

        var itemResults = new List<UndoApplyItemResult>(operations.Length);
        var deletedDirectories = new HashSet<string>(PathComparison.Comparer);
        var appliedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var conflictResolvedCount = 0;

        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (PathEquals(operation.FromPath, operation.ToPath))
            {
                skippedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "skipped",
                    Reason = "NoopUndoPath"
                });
                continue;
            }

            if (!pathExists(operation.FromPath))
            {
                skippedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "skipped",
                    Reason = "UndoSourceMissing"
                });
                continue;
            }

            string? conflictMovedToPath = null;
            var conflictResolvedForOperation = false;
            if (pathExists(operation.ToPath))
            {
                if (normalizedConflictPolicy.Equals(TargetConflictPolicySkip, StringComparison.OrdinalIgnoreCase))
                {
                    skippedCount++;
                    itemResults.Add(new UndoApplyItemResult
                    {
                        FromPath = operation.FromPath,
                        ToPath = operation.ToPath,
                        Status = "skipped",
                        Reason = "UndoTargetAlreadyExists"
                    });
                    continue;
                }

                if (normalizedConflictPolicy.Equals(TargetConflictPolicySuffix, StringComparison.OrdinalIgnoreCase))
                {
                    var suffixTargetPath = ResolveSuffixTargetPath(operation.ToPath, pathExists);
                    if (suffixTargetPath is null)
                    {
                        failedCount++;
                        itemResults.Add(new UndoApplyItemResult
                        {
                            FromPath = operation.FromPath,
                            ToPath = operation.ToPath,
                            Status = "failed",
                            Reason = "UndoUnableToResolveTargetSuffix"
                        });
                        continue;
                    }

                    try
                    {
                        movePath(operation.ToPath, suffixTargetPath);
                        conflictMovedToPath = suffixTargetPath;
                        conflictResolvedForOperation = true;
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        itemResults.Add(new UndoApplyItemResult
                        {
                            FromPath = operation.FromPath,
                            ToPath = operation.ToPath,
                            Status = "failed",
                            Reason = $"UndoConflictMoveAsideFailed:{ex.GetType().Name}",
                            ConflictMovedToPath = suffixTargetPath
                        });
                        continue;
                    }
                }
                else
                {
                    failedCount++;
                    itemResults.Add(new UndoApplyItemResult
                    {
                        FromPath = operation.FromPath,
                        ToPath = operation.ToPath,
                        Status = "failed",
                        Reason = "UndoTargetAlreadyExists"
                    });
                    continue;
                }
            }

            var targetDirectory = Path.GetDirectoryName(operation.ToPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                failedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "failed",
                    Reason = "UndoMissingTargetDirectory",
                    ConflictMovedToPath = conflictMovedToPath
                });
                continue;
            }

            try
            {
                ensureDirectory(targetDirectory);
                movePath(operation.FromPath, operation.ToPath);
                appliedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "applied",
                    Reason = conflictMovedToPath is null ? "Moved" : "MovedAfterConflictSuffix",
                    ConflictMovedToPath = conflictMovedToPath
                });
                if (conflictResolvedForOperation)
                {
                    conflictResolvedCount++;
                }

                // Cleanup empty parent directories, excluding protected paths (roots)
                CleanupEmptyParentDirectories(Path.GetDirectoryName(operation.FromPath), protectedPaths, deletedDirectories);
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(conflictMovedToPath)
                    && pathExists(conflictMovedToPath)
                    && !pathExists(operation.ToPath))
                {
                    try
                    {
                        movePath(conflictMovedToPath, operation.ToPath);
                    }
                    catch
                    {
                    }
                }

                failedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "failed",
                    Reason = $"UndoMoveFailed:{ex.GetType().Name}",
                    ConflictMovedToPath = conflictMovedToPath
                });
            }
        }

        return new UndoApplyResult
        {
            SourceApplyRunId = sourceRun.RunId,
            UndoneAtUtc = DateTimeOffset.UtcNow,
            RequestedCount = operations.Length,
            AppliedCount = appliedCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            ConflictResolvedCount = conflictResolvedCount,
            Results = itemResults.ToArray(),
            DeletedDirectories = deletedDirectories.OrderBy(d => d, PathComparison.Comparer).ToArray()
        };
    }

    private static void CleanupEmptyParentDirectories(string? directoryPath, IEnumerable<string>? protectedPaths, HashSet<string> deletedDirectories)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            // Do not delete root or system directories
            if (directoryPath.Length <= 3) return;

            // Do not delete protected paths (library roots, org roots)
            if (protectedPaths != null && protectedPaths.Any(p => PathEquals(p, directoryPath)))
            {
                return;
            }

            if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
            {
                Directory.Delete(directoryPath);
                deletedDirectories.Add(directoryPath);
                CleanupEmptyParentDirectories(Path.GetDirectoryName(directoryPath), protectedPaths, deletedDirectories);
            }
        }
        catch
        {
            // Best effort
        }
    }

    private static string NormalizeTargetConflictPolicy(string? targetConflictPolicy)
    {
        return string.IsNullOrWhiteSpace(targetConflictPolicy)
            ? TargetConflictPolicyFail
            : targetConflictPolicy.Trim().ToLowerInvariant();
    }

    private static string? ResolveSuffixTargetPath(
        string targetPath,
        Func<string, bool> fileExists)
    {
        var directory = Path.GetDirectoryName(targetPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var extension = Path.GetExtension(targetPath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(targetPath);
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        for (var index = 2; index <= 999; index++)
        {
            var candidate = Path.Combine(
                directory,
                $"{fileNameWithoutExtension} (undo-conflict {index}){extension}");
            if (!fileExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool PathEquals(string left, string right)
    {
        try
        {
            var leftFull = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rightFull = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return PathComparison.Equals(leftFull, rightFull);
        }
        catch
        {
            return PathComparison.Equals(left, right);
        }
    }
}
