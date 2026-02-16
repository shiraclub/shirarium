using Jellyfin.Plugin.Shirarium.Api;
using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.IntegrationTests;

public sealed class ControllerContractTests
{
    [Fact]
    public async Task PatchOrganizationPlanEntryOverrides_ReturnsConflict_WhenPlanFingerprintIsStale()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            var controller = CreateController(applicationPaths);
            var response = await controller.PatchOrganizationPlanEntryOverrides(
                new PatchOrganizationPlanEntryOverridesRequest
                {
                    ExpectedPlanFingerprint = "stale-fingerprint",
                    Patches =
                    [
                        new OrganizationPlanEntryOverridePatch
                        {
                            SourcePath = sourcePath,
                            Action = "skip"
                        }
                    ]
                },
                CancellationToken.None);

            var conflict = Assert.IsType<ConflictObjectResult>(response.Result);
            var message = Assert.IsType<string>(conflict.Value);
            Assert.Contains("fingerprint mismatch", message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PatchOrganizationPlanEntryOverrides_ReturnsBadRequest_ForInvalidPatchAction()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            var controller = CreateController(applicationPaths);
            var response = await controller.PatchOrganizationPlanEntryOverrides(
                new PatchOrganizationPlanEntryOverridesRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    Patches =
                    [
                        new OrganizationPlanEntryOverridePatch
                        {
                            SourcePath = sourcePath,
                            Action = "invalid-action"
                        }
                    ]
                },
                CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
            var message = Assert.IsType<string>(badRequest.Value);
            Assert.Contains("Unsupported action override", message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyReviewedPlan_ReturnsBadRequest_WhenNoReviewedMovesRemain()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            await OrganizationPlanOverridesStore.WriteAsync(
                applicationPaths,
                new OrganizationPlanOverridesSnapshot
                {
                    PlanFingerprint = plan.PlanFingerprint,
                    Entries =
                    [
                        new OrganizationPlanEntryOverride
                        {
                            SourcePath = sourcePath,
                            Action = "skip"
                        }
                    ]
                });

            var controller = CreateController(applicationPaths);
            var response = await controller.ApplyReviewedPlan(
                new ApplyReviewedPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint
                },
                CancellationToken.None);

            var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
            var message = Assert.IsType<string>(badRequest.Value);
            Assert.Equal("No reviewed move entries selected to apply.", message);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ApplyReviewedPlan_ReturnsOk_AndPersistsApplyRun()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            var controller = CreateController(applicationPaths);
            var response = await controller.ApplyReviewedPlan(
                new ApplyReviewedPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint
                },
                CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response.Result);
            var payload = Assert.IsType<ApplyOrganizationPlanResult>(ok.Value);

            Assert.Equal(1, payload.AppliedCount);
            Assert.True(File.Exists(targetPath));
            Assert.False(File.Exists(sourcePath));

            var journal = ApplyJournalStore.Read(applicationPaths);
            Assert.Single(journal.Runs);
            Assert.Equal(payload.RunId, journal.Runs[0].RunId);
            Assert.Equal(1, journal.Runs[0].AppliedCount);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task GetOrganizationPlanView_ReturnsBadRequest_ForInvalidPageInput()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            var controller = CreateController(applicationPaths);
            var response = controller.GetOrganizationPlanView(new OrganizationPlanViewRequest
            {
                Page = 0
            });

            var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
            var message = Assert.IsType<string>(badRequest.Value);
            Assert.Equal("Page must be greater than 0.", message);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task PreflightReviewedPlan_ReturnsPreview_AndDoesNotMoveFiles()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);

            var controller = CreateController(applicationPaths);
            var response = controller.PreflightReviewedPlan(
                new PreflightReviewedPlanRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint
                },
                CancellationToken.None);

            var ok = Assert.IsType<OkObjectResult>(response.Result);
            var payload = Assert.IsType<PreflightReviewedPlanResponse>(ok.Value);

            Assert.Equal(plan.PlanFingerprint, payload.PlanFingerprint);
            Assert.Equal(1, payload.MoveCandidateCount);
            Assert.Equal(1, payload.PreviewResult.AppliedCount);
            Assert.Equal("preview", payload.PreviewResult.Results[0].Status);
            Assert.Equal("WouldMove", payload.PreviewResult.Results[0].Reason);

            Assert.True(File.Exists(sourcePath));
            Assert.False(File.Exists(targetPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReviewLock_ApplyById_UsesFrozenSelection_AfterOverridesChange()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);
            var controller = CreateController(applicationPaths);

            var createLockResponse = await controller.CreateReviewLock(
                new CreateReviewLockRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint
                },
                CancellationToken.None);
            var createdLock = Assert.IsType<OkObjectResult>(createLockResponse.Result);
            var lockPayload = Assert.IsType<CreateReviewLockResponse>(createdLock.Value);

            await controller.PatchOrganizationPlanEntryOverrides(
                new PatchOrganizationPlanEntryOverridesRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint,
                    Patches =
                    [
                        new OrganizationPlanEntryOverridePatch
                        {
                            SourcePath = sourcePath,
                            Action = "skip"
                        }
                    ]
                },
                CancellationToken.None);

            var applyLockResponse = await controller.ApplyReviewLock(lockPayload.ReviewId, CancellationToken.None);
            var applyLockOk = Assert.IsType<OkObjectResult>(applyLockResponse.Result);
            var applyResult = Assert.IsType<ApplyOrganizationPlanResult>(applyLockOk.Value);
            Assert.Equal(1, applyResult.AppliedCount);
            Assert.False(File.Exists(sourcePath));
            Assert.True(File.Exists(targetPath));

            var latestLock = ReviewLockStore.ReadById(applicationPaths, lockPayload.ReviewId);
            Assert.NotNull(latestLock);
            Assert.Equal(applyResult.RunId, latestLock!.AppliedRunId);
            Assert.NotNull(latestLock.AppliedAtUtc);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task ReviewLock_ListAndGet_ReturnCreatedLock()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPath = Path.Combine(root, "organized", "A", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var plan = await WritePlanAsync(applicationPaths, sourcePath, targetPath);
            var controller = CreateController(applicationPaths);

            var createLockResponse = await controller.CreateReviewLock(
                new CreateReviewLockRequest
                {
                    ExpectedPlanFingerprint = plan.PlanFingerprint
                },
                CancellationToken.None);
            var createdLock = Assert.IsType<OkObjectResult>(createLockResponse.Result);
            var lockPayload = Assert.IsType<CreateReviewLockResponse>(createdLock.Value);

            var listResponse = controller.GetReviewLocks(limit: 10);
            var listOk = Assert.IsType<OkObjectResult>(listResponse.Result);
            var listPayload = Assert.IsType<ReviewLockListResponse>(listOk.Value);
            Assert.True(listPayload.TotalCount >= 1);
            Assert.Contains(listPayload.Items, item => item.ReviewId == lockPayload.ReviewId);

            var getResponse = controller.GetReviewLock(lockPayload.ReviewId);
            var getOk = Assert.IsType<OkObjectResult>(getResponse.Result);
            var getPayload = Assert.IsType<ReviewLockSnapshot>(getOk.Value);
            Assert.Equal(lockPayload.ReviewId, getPayload.ReviewId);
            Assert.Equal(plan.PlanFingerprint, getPayload.PlanFingerprint);
            Assert.Single(getPayload.SelectedSourcePaths);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public async Task HistoryEndpoints_ReturnPlanAndOverrideRevisions()
    {
        var root = CreateTempRoot();
        try
        {
            var applicationPaths = CreateApplicationPaths(root);
            var sourcePath = Path.Combine(root, "incoming", "A.mkv");
            var targetPathA = Path.Combine(root, "organized", "A", "A.mkv");
            var targetPathB = Path.Combine(root, "organized", "B", "A.mkv");

            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content-a");
            var planA = await WritePlanAsync(applicationPaths, sourcePath, targetPathA);
            var planB = await WritePlanAsync(applicationPaths, sourcePath, targetPathB);

            await OrganizationPlanOverridesStore.WriteAsync(
                applicationPaths,
                new OrganizationPlanOverridesSnapshot
                {
                    PlanFingerprint = planB.PlanFingerprint,
                    Entries =
                    [
                        new OrganizationPlanEntryOverride
                        {
                            SourcePath = sourcePath,
                            Action = "skip"
                        }
                    ]
                });

            await OrganizationPlanOverridesStore.WriteAsync(
                applicationPaths,
                new OrganizationPlanOverridesSnapshot
                {
                    PlanFingerprint = planB.PlanFingerprint,
                    Entries =
                    [
                        new OrganizationPlanEntryOverride
                        {
                            SourcePath = sourcePath,
                            Action = "move",
                            TargetPath = targetPathB
                        }
                    ]
                });

            var controller = CreateController(applicationPaths);

            var planHistoryResponse = controller.GetOrganizationPlanHistory(limit: 10);
            var planHistoryOk = Assert.IsType<OkObjectResult>(planHistoryResponse.Result);
            var planHistoryPayload = Assert.IsType<OrganizationPlanHistoryResponse>(planHistoryOk.Value);
            Assert.True(planHistoryPayload.TotalCount >= 2);
            Assert.True(planHistoryPayload.Items.Length >= 2);
            Assert.Contains(planHistoryPayload.Items, item => item.PlanFingerprint == planA.PlanFingerprint);
            Assert.Contains(planHistoryPayload.Items, item => item.PlanFingerprint == planB.PlanFingerprint);

            var overrideHistoryResponse = controller.GetOrganizationPlanOverridesHistory(limit: 10);
            var overrideHistoryOk = Assert.IsType<OkObjectResult>(overrideHistoryResponse.Result);
            var overrideHistoryPayload = Assert.IsType<OrganizationPlanOverridesHistoryResponse>(overrideHistoryOk.Value);
            Assert.True(overrideHistoryPayload.TotalCount >= 2);
            Assert.True(overrideHistoryPayload.Items.Length >= 2);
            Assert.All(overrideHistoryPayload.Items, item => Assert.Equal(planB.PlanFingerprint, item.PlanFingerprint));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static ShirariumController CreateController(TestApplicationPaths applicationPaths)
    {
        return new ShirariumController(
            null!,
            applicationPaths,
            NullLogger<ShirariumController>.Instance);
    }

    private static async Task<OrganizationPlanSnapshot> WritePlanAsync(
        TestApplicationPaths applicationPaths,
        string sourcePath,
        string targetPath)
    {
        var plan = new OrganizationPlanSnapshot
        {
            RootPath = Path.GetDirectoryName(Path.GetDirectoryName(targetPath)!)!,
            DryRunMode = false,
            SourceSuggestionCount = 1,
            PlannedCount = 1,
            Entries =
            [
                new OrganizationPlanEntry
                {
                    ItemId = Guid.NewGuid().ToString("N"),
                    SourcePath = sourcePath,
                    TargetPath = targetPath,
                    Strategy = "movie",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.95,
                    SuggestedTitle = "A",
                    SuggestedMediaType = "movie"
                }
            ]
        };

        await OrganizationPlanStore.WriteAsync(applicationPaths, plan);
        return OrganizationPlanStore.Read(applicationPaths);
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
        var root = Path.Combine(Path.GetTempPath(), "shirarium-controller-contract-tests", Guid.NewGuid().ToString("N"));
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
