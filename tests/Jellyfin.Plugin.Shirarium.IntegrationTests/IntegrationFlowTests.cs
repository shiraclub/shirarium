using Jellyfin.Plugin.Shirarium.Configuration;
using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.IntegrationTests;

public sealed class IntegrationFlowTests
{
    [Fact]
    public async Task ApplyAndUndo_RoundTripFile_AndPersistJournalState()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "Noroi 2005.mkv");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");

            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);
            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);

            var applyResult = await applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    SourcePaths = [sourcePath]
                });

            Assert.Equal(1, applyResult.AppliedCount);
            Assert.True(File.Exists(targetPath));
            Assert.False(File.Exists(sourcePath));

            var afterApplyJournal = ApplyJournalStore.Read(applicationPaths);
            Assert.Single(afterApplyJournal.Runs);
            Assert.Empty(afterApplyJournal.UndoRuns);
            Assert.Single(afterApplyJournal.Runs[0].UndoOperations);
            Assert.Null(afterApplyJournal.Runs[0].UndoneByRunId);

            var undoer = new OrganizationPlanUndoer(applicationPaths, NullLogger.Instance);
            var undoResult = await undoer.RunAsync(
                new UndoApplyRequest
                {
                    RunId = applyResult.RunId
                });

            Assert.Equal(1, undoResult.AppliedCount);
            Assert.True(File.Exists(sourcePath));
            Assert.False(File.Exists(targetPath));

            var afterUndoJournal = ApplyJournalStore.Read(applicationPaths);
            Assert.Single(afterUndoJournal.Runs);
            Assert.Single(afterUndoJournal.UndoRuns);
            Assert.Equal(applyResult.RunId, afterUndoJournal.UndoRuns[0].SourceApplyRunId);
            Assert.Equal(undoResult.UndoRunId, afterUndoJournal.Runs[0].UndoneByRunId);
            Assert.NotNull(afterUndoJournal.Runs[0].UndoneAtUtc);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task Apply_RejectsStalePlanFingerprint()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "Noroi 2005.mkv");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");
            await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                applier.RunAsync(
                    new ApplyOrganizationPlanRequest
                    {
                        ExpectedPlanFingerprint = "stale-fingerprint",
                        SourcePaths = [sourcePath]
                    }));

            Assert.Equal("PlanFingerprintMismatch", ex.Message);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyAndUndo_AreBlocked_WhenOperationLockIsHeld()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "Noroi 2005.mkv");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");

            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);
            using var lockHandle = OperationLock.TryAcquire(applicationPaths);
            Assert.NotNull(lockHandle);

            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);
            var applyEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                applier.RunAsync(
                    new ApplyOrganizationPlanRequest
                    {
                        ExpectedPlanFingerprint = plan.PlanFingerprint,
                        SourcePaths = [sourcePath]
                    }));
            Assert.Equal("OperationAlreadyInProgress", applyEx.Message);

            var undoer = new OrganizationPlanUndoer(applicationPaths, NullLogger.Instance);
            var undoEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                undoer.RunAsync(new UndoApplyRequest()));
            Assert.Equal("OperationAlreadyInProgress", undoEx.Message);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task UndoApply_PartialCollision_AppliesWhatItCan_AndPersistsJournal()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);

            var sourcePathA = Path.Combine(root, "incoming", "A.mkv");
            var sourcePathB = Path.Combine(root, "incoming", "B.mkv");
            var targetPathA = Path.Combine(root, "organized", "A", "A.mkv");
            var targetPathB = Path.Combine(root, "organized", "B", "B.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePathA)!);
            File.WriteAllText(sourcePathA, "content-a");
            File.WriteAllText(sourcePathB, "content-b");

            var plan = await WritePlanAsync(
                applicationPaths,
                CreatePlanEntry(sourcePathA, targetPathA, "A"),
                CreatePlanEntry(sourcePathB, targetPathB, "B"));

            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);
            var applyResult = await applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    SourcePaths = [sourcePathA, sourcePathB]
                });

            Assert.Equal(2, applyResult.AppliedCount);
            Assert.False(File.Exists(sourcePathA));
            Assert.False(File.Exists(sourcePathB));
            Assert.True(File.Exists(targetPathA));
            Assert.True(File.Exists(targetPathB));

            File.WriteAllText(sourcePathB, "collision");

            var undoer = new OrganizationPlanUndoer(applicationPaths, NullLogger.Instance);
            var undoResult = await undoer.RunAsync(
                new UndoApplyRequest
                {
                    RunId = applyResult.RunId
                });

            Assert.Equal(2, undoResult.RequestedCount);
            Assert.Equal(1, undoResult.AppliedCount);
            Assert.Equal(1, undoResult.FailedCount);
            Assert.Contains(undoResult.Results, result => result.Reason == "UndoTargetAlreadyExists");

            Assert.True(File.Exists(sourcePathA));
            Assert.False(File.Exists(targetPathA));
            Assert.True(File.Exists(sourcePathB));
            Assert.True(File.Exists(targetPathB));

            Assert.Equal("collision", File.ReadAllText(sourcePathB));
            Assert.Equal("content-b", File.ReadAllText(targetPathB));

            var journal = ApplyJournalStore.Read(applicationPaths);
            Assert.Single(journal.Runs);
            Assert.Single(journal.UndoRuns);
            Assert.Equal(applyResult.RunId, journal.UndoRuns[0].SourceApplyRunId);
            Assert.Equal(undoResult.UndoRunId, journal.Runs[0].UndoneByRunId);
            Assert.Equal(1, journal.UndoRuns[0].AppliedCount);
            Assert.Equal(1, journal.UndoRuns[0].FailedCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task UndoApply_WithSuffixPolicy_ResolvesTargetCollisions_AndRestoresAllMoves()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);

            var sourcePathA = Path.Combine(root, "incoming", "A.mkv");
            var sourcePathB = Path.Combine(root, "incoming", "B.mkv");
            var targetPathA = Path.Combine(root, "organized", "A", "A.mkv");
            var targetPathB = Path.Combine(root, "organized", "B", "B.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePathA)!);
            File.WriteAllText(sourcePathA, "content-a");
            File.WriteAllText(sourcePathB, "content-b");

            var plan = await WritePlanAsync(
                applicationPaths,
                CreatePlanEntry(sourcePathA, targetPathA, "A"),
                CreatePlanEntry(sourcePathB, targetPathB, "B"));

            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);
            var applyResult = await applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    SourcePaths = [sourcePathA, sourcePathB]
                });

            Assert.Equal(2, applyResult.AppliedCount);
            Assert.False(File.Exists(sourcePathA));
            Assert.False(File.Exists(sourcePathB));
            Assert.True(File.Exists(targetPathA));
            Assert.True(File.Exists(targetPathB));

            File.WriteAllText(sourcePathB, "collision");

            var undoer = new OrganizationPlanUndoer(applicationPaths, NullLogger.Instance);
            var undoResult = await undoer.RunAsync(
                new UndoApplyRequest
                {
                    RunId = applyResult.RunId,
                    TargetConflictPolicy = "suffix"
                });

            Assert.Equal(2, undoResult.RequestedCount);
            Assert.Equal(2, undoResult.AppliedCount);
            Assert.Equal(0, undoResult.FailedCount);
            Assert.Equal(1, undoResult.ConflictResolvedCount);
            Assert.Contains(undoResult.Results, result => result.Reason == "MovedAfterConflictSuffix");

            Assert.True(File.Exists(sourcePathA));
            Assert.True(File.Exists(sourcePathB));
            Assert.False(File.Exists(targetPathA));
            Assert.False(File.Exists(targetPathB));
            Assert.Equal("content-a", File.ReadAllText(sourcePathA));
            Assert.Equal("content-b", File.ReadAllText(sourcePathB));

            var movedAside = undoResult.Results
                .Where(result => !string.IsNullOrWhiteSpace(result.ConflictMovedToPath))
                .Select(result => result.ConflictMovedToPath!)
                .ToArray();
            Assert.Single(movedAside);
            Assert.True(File.Exists(movedAside[0]));
            Assert.Equal("collision", File.ReadAllText(movedAside[0]));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task OpsStatus_ReturnsLatestPlanApplyAndUndoState()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "Noroi 2005.mkv");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");

            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);
            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);
            var applyResult = await applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    SourcePaths = [sourcePath]
                });

            var undoer = new OrganizationPlanUndoer(applicationPaths, NullLogger.Instance);
            var undoResult = await undoer.RunAsync(
                new UndoApplyRequest
                {
                    RunId = applyResult.RunId
                });

            var status = OpsStatusLogic.Build(
                OrganizationPlanStore.Read(applicationPaths),
                ApplyJournalStore.Read(applicationPaths));

            Assert.True(status.Plan.HasPlan);
            Assert.Equal(plan.PlanFingerprint, status.Plan.PlanFingerprint);
            Assert.Equal(plan.RootPath, status.Plan.RootPath);
            Assert.Equal(plan.PlannedCount, status.Plan.PlannedCount);

            Assert.NotNull(status.LastApplyRun);
            Assert.Equal(applyResult.RunId, status.LastApplyRun!.RunId);
            Assert.Equal(applyResult.AppliedCount, status.LastApplyRun.AppliedCount);
            Assert.True(status.LastApplyRun.WasUndone);
            Assert.Equal(undoResult.UndoRunId, status.LastApplyRun.UndoneByRunId);

            Assert.NotNull(status.LastUndoRun);
            Assert.Equal(undoResult.UndoRunId, status.LastUndoRun!.UndoRunId);
            Assert.Equal(applyResult.RunId, status.LastUndoRun.SourceApplyRunId);
            Assert.Equal(undoResult.AppliedCount, status.LastUndoRun.AppliedCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task Planning_WithSuffixPolicy_GeneratesDeterministicTargets_ForMixedLibrary()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var organizationRoot = Path.Combine(root, "organized");
            var existingTarget = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(existingTarget)!);
            File.WriteAllText(existingTarget, "existing");

            var sourceA = Path.Combine(root, "incoming", "a.mkv");
            var sourceB = Path.Combine(root, "incoming", "b.mkv");
            var sourceC = Path.Combine(root, "incoming", "c.mkv");
            var sourceEpisode = Path.Combine(root, "incoming", "ep.mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceA)!);
            File.WriteAllText(sourceA, "a");
            File.WriteAllText(sourceB, "b");
            File.WriteAllText(sourceC, "c");
            File.WriteAllText(sourceEpisode, "ep");

            var planner = new OrganizationPlanner(
                applicationPaths,
                NullLogger.Instance,
                new PluginConfiguration
                {
                    EnableFileOrganizationPlanning = true,
                    DryRunMode = true,
                    OrganizationRootPath = organizationRoot,
                    NormalizePathSegments = true,
                    TargetConflictPolicy = "suffix"
                });

            var plan = await planner.RunAsync(new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateScanSuggestion(sourceB, "Noroi", "movie", suggestedYear: 2005),
                    CreateScanSuggestion(sourceEpisode, "Kowasugi", "episode", suggestedSeason: 1, suggestedEpisode: 2),
                    CreateScanSuggestion(sourceA, "Noroi", "movie", suggestedYear: 2005),
                    CreateScanSuggestion(sourceC, "Noroi", "movie", suggestedYear: 2005)
                ]
            });

            Assert.Equal(4, plan.SourceSuggestionCount);
            Assert.Equal(4, plan.PlannedCount);
            Assert.Equal(0, plan.SkippedCount);
            Assert.Equal(0, plan.ConflictCount);

            var movieEntries = plan.Entries
                .Where(entry => string.Equals(entry.Strategy, "movie", StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry.SourcePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(3, movieEntries.Length);
            Assert.Equal(Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005) (2).mkv"), movieEntries[0].TargetPath);
            Assert.Equal(Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005) (3).mkv"), movieEntries[1].TargetPath);
            Assert.Equal(Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005) (4).mkv"), movieEntries[2].TargetPath);

            var persisted = OrganizationPlanStore.Read(applicationPaths);
            Assert.Equal(plan.PlanFingerprint, persisted.PlanFingerprint);
            Assert.Equal(plan.PlannedCount, persisted.PlannedCount);
            Assert.Equal(plan.Entries.Length, persisted.Entries.Length);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task Planning_WithSkipPolicy_SkipsConflicts_AndKeepsSafeMoves()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var organizationRoot = Path.Combine(root, "organized");
            var existingTarget = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(existingTarget)!);
            File.WriteAllText(existingTarget, "existing");

            var sourceA = Path.Combine(root, "incoming", "a.mkv");
            var sourceB = Path.Combine(root, "incoming", "b.mkv");
            var sourceEpisode = Path.Combine(root, "incoming", "ep.mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceA)!);
            File.WriteAllText(sourceA, "a");
            File.WriteAllText(sourceB, "b");
            File.WriteAllText(sourceEpisode, "ep");

            var planner = new OrganizationPlanner(
                applicationPaths,
                NullLogger.Instance,
                new PluginConfiguration
                {
                    EnableFileOrganizationPlanning = true,
                    DryRunMode = true,
                    OrganizationRootPath = organizationRoot,
                    NormalizePathSegments = true,
                    TargetConflictPolicy = "skip"
                });

            var plan = await planner.RunAsync(new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateScanSuggestion(sourceA, "Noroi", "movie", suggestedYear: 2005),
                    CreateScanSuggestion(sourceEpisode, "Kowasugi", "episode", suggestedSeason: 1, suggestedEpisode: 2),
                    CreateScanSuggestion(sourceB, "Noroi", "movie", suggestedYear: 2005)
                ]
            });

            Assert.Equal(3, plan.SourceSuggestionCount);
            Assert.Equal(1, plan.PlannedCount);
            Assert.Equal(2, plan.SkippedCount);
            Assert.Equal(0, plan.ConflictCount);
            Assert.Equal(0, plan.NoopCount);

            var skipped = plan.Entries.Where(entry => entry.Action == "skip").ToArray();
            Assert.Equal(2, skipped.Length);
            Assert.All(skipped, entry => Assert.Equal("TargetAlreadyExists", entry.Reason));

            var move = Assert.Single(plan.Entries, entry => entry.Action == "move");
            Assert.Equal(
                Path.Combine(organizationRoot, "Kowasugi", "Season 01", "Kowasugi S01E02.mkv"),
                move.TargetPath);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PlanApplyUndo_WithSuffixPolicy_RoundTripsMovedFiles_WithoutTouchingOriginalCollisionTarget()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var organizationRoot = Path.Combine(root, "organized");
            var existingTarget = Path.Combine(organizationRoot, "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(existingTarget)!);
            File.WriteAllText(existingTarget, "existing");

            var sourceA = Path.Combine(root, "incoming", "a.mkv");
            var sourceB = Path.Combine(root, "incoming", "b.mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourceA)!);
            File.WriteAllText(sourceA, "a");
            File.WriteAllText(sourceB, "b");

            var planner = new OrganizationPlanner(
                applicationPaths,
                NullLogger.Instance,
                new PluginConfiguration
                {
                    EnableFileOrganizationPlanning = true,
                    DryRunMode = false,
                    OrganizationRootPath = organizationRoot,
                    NormalizePathSegments = true,
                    TargetConflictPolicy = "suffix"
                });

            var plan = await planner.RunAsync(new ScanResultSnapshot
            {
                Suggestions =
                [
                    CreateScanSuggestion(sourceB, "Noroi", "movie", suggestedYear: 2005),
                    CreateScanSuggestion(sourceA, "Noroi", "movie", suggestedYear: 2005)
                ]
            });

            Assert.Equal(2, plan.PlannedCount);
            Assert.Equal(0, plan.ConflictCount);

            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);
            var applyResult = await applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    SourcePaths = [sourceA, sourceB]
                });

            Assert.Equal(2, applyResult.AppliedCount);
            Assert.False(File.Exists(sourceA));
            Assert.False(File.Exists(sourceB));
            Assert.True(File.Exists(existingTarget));

            var movedTargets = plan.Entries
                .Where(entry => entry.Action == "move")
                .Select(entry => entry.TargetPath!)
                .ToArray();
            Assert.All(movedTargets, movedTarget => Assert.True(File.Exists(movedTarget)));

            var undoer = new OrganizationPlanUndoer(applicationPaths, NullLogger.Instance);
            var undoResult = await undoer.RunAsync(new UndoApplyRequest { RunId = applyResult.RunId });

            Assert.Equal(2, undoResult.AppliedCount);
            Assert.True(File.Exists(sourceA));
            Assert.True(File.Exists(sourceB));
            Assert.True(File.Exists(existingTarget));
            Assert.All(movedTargets, movedTarget => Assert.False(File.Exists(movedTarget)));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task OverridesStore_PersistsAndScopesByPlanFingerprint()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var planFingerprint = "plan-v1";
            var snapshot = new OrganizationPlanOverridesSnapshot
            {
                PlanFingerprint = planFingerprint,
                Entries =
                [
                    new OrganizationPlanEntryOverride
                    {
                        SourcePath = @"D:\incoming\a.mkv",
                        Action = "skip"
                    }
                ]
            };

            await OrganizationPlanOverridesStore.WriteAsync(applicationPaths, snapshot);

            var matching = OrganizationPlanOverridesStore.ReadForFingerprint(applicationPaths, planFingerprint);
            Assert.Single(matching.Entries);
            Assert.Equal(@"D:\incoming\a.mkv", matching.Entries[0].SourcePath);

            var mismatched = OrganizationPlanOverridesStore.ReadForFingerprint(applicationPaths, "plan-v2");
            Assert.Empty(mismatched.Entries);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReviewedApply_UsesOverrides_AndRejectsStaleFingerprint()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePathA = Path.Combine(root, "incoming", "A.mkv");
            var sourcePathB = Path.Combine(root, "incoming", "B.mkv");
            var targetPathA = Path.Combine(root, "organized", "A", "A.mkv");
            var targetPathB = Path.Combine(root, "organized", "B", "B.mkv");
            var targetPathBOverride = Path.Combine(root, "organized", "B-custom", "B-custom.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePathA)!);
            File.WriteAllText(sourcePathA, "a");
            File.WriteAllText(sourcePathB, "b");

            var plan = await WritePlanAsync(
                applicationPaths,
                CreatePlanEntry(sourcePathA, targetPathA, "A"),
                CreatePlanEntry(sourcePathB, targetPathB, "B"));

            var patchResult = OrganizationPlanOverridesLogic.ApplyPatches(
                new OrganizationPlanOverridesSnapshot
                {
                    PlanFingerprint = plan.PlanFingerprint
                },
                new PatchOrganizationPlanEntryOverridesRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    Patches =
                    [
                        new OrganizationPlanEntryOverridePatch
                        {
                            SourcePath = sourcePathA,
                            Action = "skip"
                        },
                        new OrganizationPlanEntryOverridePatch
                        {
                            SourcePath = sourcePathB,
                            TargetPath = targetPathBOverride
                        }
                    ]
                },
                plan.PlanFingerprint);
            await OrganizationPlanOverridesStore.WriteAsync(applicationPaths, patchResult.Snapshot);

            var effectivePlan = OrganizationPlanReviewLogic.BuildEffectivePlan(
                plan,
                OrganizationPlanOverridesStore.ReadForFingerprint(applicationPaths, plan.PlanFingerprint));

            var reviewedSelection = effectivePlan.Entries
                .Where(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.SourcePath)
                .ToArray();
            Assert.Single(reviewedSelection);
            Assert.Equal(sourcePathB, reviewedSelection[0]);

            var applier = new OrganizationPlanApplier(applicationPaths, NullLogger.Instance);

            var staleEx = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                applier.RunAsync(
                    new ApplyOrganizationPlanRequest
                    {
                        ExpectedPlanFingerprint = "stale-fingerprint",
                        SourcePaths = reviewedSelection
                    },
                    effectivePlan));
            Assert.Equal("PlanFingerprintMismatch", staleEx.Message);

            var applyResult = await applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    SourcePaths = reviewedSelection
                },
                effectivePlan);

            Assert.Equal(1, applyResult.AppliedCount);
            Assert.False(File.Exists(sourcePathB));
            Assert.True(File.Exists(targetPathBOverride));
            Assert.True(File.Exists(sourcePathA));
            Assert.False(File.Exists(targetPathA));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static async Task<OrganizationPlanSnapshot> WritePlanAsync(
        TestApplicationPaths applicationPaths,
        string sourcePath,
        string targetPath)
    {
        return await WritePlanAsync(
            applicationPaths,
            CreatePlanEntry(sourcePath, targetPath, "Noroi"));
    }

    private static async Task<OrganizationPlanSnapshot> WritePlanAsync(
        TestApplicationPaths applicationPaths,
        params OrganizationPlanEntry[] entries)
    {
        var firstTargetPath = entries[0].TargetPath ?? string.Empty;
        var plan = new OrganizationPlanSnapshot
        {
            RootPath = Path.GetDirectoryName(Path.GetDirectoryName(firstTargetPath)!)!,
            DryRunMode = false,
            SourceSuggestionCount = entries.Length,
            PlannedCount = entries.Length,
            Entries = entries
        };

        await OrganizationPlanStore.WriteAsync(applicationPaths, plan);
        return OrganizationPlanStore.Read(applicationPaths);
    }

    private static OrganizationPlanEntry CreatePlanEntry(
        string sourcePath,
        string targetPath,
        string title)
    {
        return new OrganizationPlanEntry
        {
            ItemId = Guid.NewGuid().ToString("N"),
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Strategy = "movie",
            Action = "move",
            Reason = "Planned",
            Confidence = 0.95,
            SuggestedTitle = title,
            SuggestedMediaType = "movie"
        };
    }

    private static ScanSuggestion CreateScanSuggestion(
        string sourcePath,
        string suggestedTitle,
        string suggestedMediaType,
        int? suggestedYear = null,
        int? suggestedSeason = null,
        int? suggestedEpisode = null)
    {
        return new ScanSuggestion
        {
            ItemId = Guid.NewGuid().ToString("N"),
            Name = Path.GetFileNameWithoutExtension(sourcePath),
            Path = sourcePath,
            SuggestedTitle = suggestedTitle,
            SuggestedMediaType = suggestedMediaType,
            SuggestedYear = suggestedYear,
            SuggestedSeason = suggestedSeason,
            SuggestedEpisode = suggestedEpisode,
            Confidence = 0.95,
            Source = "integration-test"
        };
    }

    private static TestApplicationPaths CreateApplicationPaths(string root)
    {
        var dataPath = Path.Combine(root, "jellyfin-data");
        Directory.CreateDirectory(dataPath);

        return new TestApplicationPaths
        {
            DataPath = dataPath,
            ProgramDataPath = dataPath,
            ProgramSystemPath = dataPath,
            CachePath = Path.Combine(root, "cache"),
            TempDirectory = Path.Combine(root, "tmp"),
            PluginsPath = Path.Combine(dataPath, "plugins"),
            BackupPath = Path.Combine(root, "backup"),
            VirtualDataPath = dataPath,
            LogDirectoryPath = Path.Combine(root, "logs"),
            ConfigurationDirectoryPath = Path.Combine(dataPath, "config"),
            SystemConfigurationFilePath = Path.Combine(dataPath, "system.xml"),
            WebPath = Path.Combine(root, "web"),
            PluginConfigurationsPath = Path.Combine(dataPath, "plugin-configs"),
            ImageCachePath = Path.Combine(root, "image-cache"),
            TrickplayPath = Path.Combine(root, "trickplay")
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "shirarium-integration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        try
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
        catch
        {
        }
    }
}
