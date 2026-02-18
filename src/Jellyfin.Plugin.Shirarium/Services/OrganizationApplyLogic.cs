using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OrganizationApplyLogic
{
    internal static ApplyOrganizationPlanResult ApplySelected(
        OrganizationPlanSnapshot plan,
        IEnumerable<string> selectedSourcePaths,
        CancellationToken cancellationToken = default)
    {
        return EvaluateSelected(
            plan,
            selectedSourcePaths,
            path => File.Exists(path) || Directory.Exists(path),
            path => _ = Directory.CreateDirectory(path),
            MovePath,
            executeMoves: true,
            cancellationToken);
    }

    private static void MovePath(string source, string target)
    {
        if (Directory.Exists(source))
        {
            Directory.Move(source, target);
        }
        else
        {
            File.Move(source, target);
        }
    }

    internal static ApplyOrganizationPlanResult ApplySelected(
        OrganizationPlanSnapshot plan,
        IEnumerable<string> selectedSourcePaths,
        Func<string, bool> pathExists,
        Action<string> ensureDirectory,
        Action<string, string> movePath,
        CancellationToken cancellationToken = default)
    {
        return EvaluateSelected(
            plan,
            selectedSourcePaths,
            pathExists,
            ensureDirectory,
            movePath,
            executeMoves: true,
            cancellationToken);
    }

    internal static ApplyOrganizationPlanResult PreviewSelected(
        OrganizationPlanSnapshot plan,
        IEnumerable<string> selectedSourcePaths,
        CancellationToken cancellationToken = default)
    {
        return EvaluateSelected(
            plan,
            selectedSourcePaths,
            path => File.Exists(path) || Directory.Exists(path),
            _ => { },
            (_, _) => { },
            executeMoves: false,
            cancellationToken);
    }

    private static ApplyOrganizationPlanResult EvaluateSelected(
        OrganizationPlanSnapshot plan,
        IEnumerable<string> selectedSourcePaths,
        Func<string, bool> pathExists,
        Action<string> ensureDirectory,
        Action<string, string> movePath,
        bool executeMoves,
        CancellationToken cancellationToken)
    {
        var rootValidation = TryGetCanonicalPath(plan.RootPath, out var canonicalRootPath);

        var normalizedSelections = new HashSet<string>(
            selectedSourcePaths.Where(path => !string.IsNullOrWhiteSpace(path)),
            PathComparison.Comparer);

        var itemResults = new List<ApplyOrganizationPlanItemResult>(normalizedSelections.Count);
        var undoOperations = new List<ApplyUndoMoveOperation>(normalizedSelections.Count);
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

            if (!pathExists(sourcePath))
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

            if (pathExists(targetPath))
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

            if (!executeMoves)
            {
                appliedCount++;
                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "preview",
                    Reason = "WouldMove"
                });
                continue;
            }

            try
            {
                ensureDirectory(targetDirectory);
                movePath(sourcePath, targetPath);
                appliedCount++;
                
                var associatedResults = new List<AssociatedFileResult>();
                
                // Move associated files and directories
                foreach (var associatedMove in entry.AssociatedFiles)
                {
                    try
                    {
                        var assocSource = associatedMove.SourcePath;
                        var assocTarget = associatedMove.TargetPath;
                        
                        // Ensure target directory for associated item exists
                        var assocTargetDir = Path.GetDirectoryName(assocTarget);
                        if (!string.IsNullOrWhiteSpace(assocTargetDir))
                        {
                            ensureDirectory(assocTargetDir);
                        }
                        
                        if (!pathExists(assocSource))
                        {
                            associatedResults.Add(new AssociatedFileResult
                            {
                                SourcePath = assocSource,
                                TargetPath = assocTarget,
                                Status = "failed",
                                ErrorMessage = "SourceNotFound"
                            });
                            continue;
                        }

                        movePath(assocSource, assocTarget);

                        associatedResults.Add(new AssociatedFileResult
                        {
                            SourcePath = assocSource,
                            TargetPath = assocTarget,
                            Status = "applied"
                        });
                        
                        undoOperations.Add(new ApplyUndoMoveOperation
                        {
                            FromPath = assocTarget,
                            ToPath = assocSource
                        });
                    }
                    catch (Exception ex)
                    {
                        associatedResults.Add(new AssociatedFileResult
                        {
                            SourcePath = associatedMove.SourcePath,
                            TargetPath = associatedMove.TargetPath,
                            Status = "failed",
                            ErrorMessage = ex.Message
                        });
                    }
                }

                itemResults.Add(new ApplyOrganizationPlanItemResult
                {
                    SourcePath = entry.SourcePath,
                    TargetPath = targetPath,
                    Status = "applied",
                    Reason = "Moved",
                    AssociatedResults = associatedResults.ToArray()
                });

                undoOperations.Add(new ApplyUndoMoveOperation
                {
                    FromPath = targetPath,
                    ToPath = sourcePath
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
            PlanFingerprint = plan.PlanFingerprint,
            RequestedCount = normalizedSelections.Count,
            AppliedCount = appliedCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            Results = itemResults.ToArray(),
            UndoOperations = undoOperations.ToArray()
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
        if (PathComparison.Equals(path, rootPath))
        {
            return true;
        }

        var rootWithSeparator = rootPath + Path.DirectorySeparatorChar;
        return PathComparison.StartsWith(path, rootWithSeparator);
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
            return PathComparison.Equals(leftFull, rightFull);
        }
        catch
        {
            return PathComparison.Equals(left, right);
        }
    }
}
