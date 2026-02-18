using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OrganizationPlanOverridesLogicTests
{
    [Fact]
    public void ValidateRequest_RejectsStaleFingerprint()
    {
        var plan = new OrganizationPlanSnapshot
        {
            PlanFingerprint = "plan-v1"
        };

        var error = OrganizationPlanOverridesLogic.ValidateRequest(
            new PatchOrganizationPlanEntryOverridesRequest
            {
                ExpectedPlanFingerprint = "plan-v2",
                Patches =
                [
                    new OrganizationPlanEntryOverridePatch
                    {
                        SourcePath = @"D:\incoming\a.mkv",
                        Action = "skip"
                    }
                ]
            },
            plan);

        Assert.Equal("PlanFingerprintMismatch", error);
    }

    [Fact]
    public void ApplyPatches_UpsertsAndRemovesOverrides()
    {
        var snapshot = new OrganizationPlanOverridesSnapshot
        {
            PlanFingerprint = "plan-v1",
            Entries =
            [
                new OrganizationPlanEntryOverride
                {
                    SourcePath = @"D:\incoming\a.mkv",
                    Action = "move",
                    TargetPath = @"D:\organized\A\A.mkv"
                }
            ]
        };

        var result = OrganizationPlanOverridesLogic.ApplyPatches(
            snapshot,
            new PatchOrganizationPlanEntryOverridesRequest
            {
                ExpectedPlanFingerprint = "plan-v1",
                Patches =
                [
                    new OrganizationPlanEntryOverridePatch
                    {
                        SourcePath = @"D:\incoming\a.mkv",
                        Action = "skip",
                        TargetPath = string.Empty
                    },
                    new OrganizationPlanEntryOverridePatch
                    {
                        SourcePath = @"D:\incoming\b.mkv",
                        Action = "move",
                        TargetPath = @"D:\organized\B\B.mkv"
                    },
                    new OrganizationPlanEntryOverridePatch
                    {
                        SourcePath = @"D:\incoming\b.mkv",
                        Remove = true
                    }
                ]
            },
            "plan-v1");

        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(1, result.RemovedCount);
        Assert.Single(result.Snapshot.Entries);
        Assert.Equal(@"D:\incoming\a.mkv", result.Snapshot.Entries[0].SourcePath);
        Assert.Equal("skip", result.Snapshot.Entries[0].Action);
        Assert.Null(result.Snapshot.Entries[0].TargetPath);
    }

    [Fact]
    public void ValidateRequest_CaseOnlyDuplicateSourcePath_IsPlatformAware()
    {
        var plan = new OrganizationPlanSnapshot
        {
            PlanFingerprint = "plan-v1"
        };

        var error = OrganizationPlanOverridesLogic.ValidateRequest(
            new PatchOrganizationPlanEntryOverridesRequest
            {
                ExpectedPlanFingerprint = "plan-v1",
                Patches =
                [
                    new OrganizationPlanEntryOverridePatch
                    {
                        SourcePath = "/media/incoming/CaseFile.mkv",
                        Action = "skip"
                    },
                    new OrganizationPlanEntryOverridePatch
                    {
                        SourcePath = "/media/incoming/casefile.mkv",
                        Action = "move"
                    }
                ]
            },
            plan);

        if (OperatingSystem.IsWindows())
        {
            Assert.NotNull(error);
            Assert.StartsWith("Duplicate patch source path:", error);
        }
        else
        {
            Assert.Null(error);
        }
    }
}
