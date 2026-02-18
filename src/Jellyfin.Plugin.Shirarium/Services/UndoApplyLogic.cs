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
        CancellationToken cancellationToken = default)
    {
        return UndoRun(
            sourceRun,
            targetConflictPolicy,
            File.Exists,
            path => _ = Directory.CreateDirectory(path),
            File.Move,
            cancellationToken);
    }

    internal static UndoApplyResult UndoRun(
        ApplyOrganizationPlanResult sourceRun,
        string? targetConflictPolicy,
        Func<string, bool> fileExists,
        Action<string> ensureDirectory,
        Action<string, string> moveFile,
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

            if (!fileExists(operation.FromPath))
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
            if (fileExists(operation.ToPath))
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
                    var suffixTargetPath = ResolveSuffixTargetPath(operation.ToPath, fileExists);
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
                        moveFile(operation.ToPath, suffixTargetPath);
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
                moveFile(operation.FromPath, operation.ToPath);
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
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(conflictMovedToPath)
                    && fileExists(conflictMovedToPath)
                    && !fileExists(operation.ToPath))
                {
                    try
                    {
                        moveFile(conflictMovedToPath, operation.ToPath);
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
            Results = itemResults.ToArray()
        };
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
