using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class UndoApplyLogic
{
    internal static UndoApplyResult UndoRun(
        ApplyOrganizationPlanResult sourceRun,
        CancellationToken cancellationToken = default)
    {
        return UndoRun(
            sourceRun,
            File.Exists,
            path => _ = Directory.CreateDirectory(path),
            File.Move,
            cancellationToken);
    }

    internal static UndoApplyResult UndoRun(
        ApplyOrganizationPlanResult sourceRun,
        Func<string, bool> fileExists,
        Action<string> ensureDirectory,
        Action<string, string> moveFile,
        CancellationToken cancellationToken = default)
    {
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

            if (fileExists(operation.ToPath))
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

            var targetDirectory = Path.GetDirectoryName(operation.ToPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                failedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "failed",
                    Reason = "UndoMissingTargetDirectory"
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
                    Reason = "Moved"
                });
            }
            catch (Exception ex)
            {
                failedCount++;
                itemResults.Add(new UndoApplyItemResult
                {
                    FromPath = operation.FromPath,
                    ToPath = operation.ToPath,
                    Status = "failed",
                    Reason = $"UndoMoveFailed:{ex.GetType().Name}"
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
            Results = itemResults.ToArray()
        };
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
