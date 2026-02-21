using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OrganizationApplyLogicTests
{
    [Fact]
    public void ApplySelected_AppliesMoveEntry_AndMovesFile()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");

            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "move",
                Reason = "Planned"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [sourcePath]);

            Assert.Equal(1, result.RequestedCount);
            Assert.Equal(1, result.AppliedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Single(result.Results);
            Assert.Equal("applied", result.Results[0].Status);
            Assert.Equal("Moved", result.Results[0].Reason);
            Assert.False(File.Exists(sourcePath));
            Assert.True(File.Exists(targetPath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_SkipsEntry_WhenActionIsNotMove()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");
            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "conflict",
                Reason = "TargetAlreadyExists"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [sourcePath]);

            Assert.Equal(1, result.RequestedCount);
            Assert.Equal(0, result.AppliedCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Equal("skipped", result.Results[0].Status);
            Assert.Equal("NotMoveAction", result.Results[0].Reason);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_Fails_WhenTargetAlreadyExists()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            File.WriteAllText(sourcePath, "source");
            File.WriteAllText(targetPath, "existing-target");

            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "move",
                Reason = "Planned"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [sourcePath]);

            Assert.Equal(1, result.RequestedCount);
            Assert.Equal(0, result.AppliedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal("failed", result.Results[0].Status);
            Assert.Equal("TargetAlreadyExists", result.Results[0].Reason);
            Assert.True(File.Exists(sourcePath));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_Skips_WhenSelectionNotInPlan()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");
            var unknownPath = Path.Combine(root, "incoming", "unknown.mkv");

            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "move",
                Reason = "Planned"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [unknownPath]);

            Assert.Equal(1, result.RequestedCount);
            Assert.Equal(0, result.AppliedCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Equal("NotFoundInPlan", result.Results[0].Reason);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_DeduplicatesRepeatedSelections()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "organized", "Noroi (2005)", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");

            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "move",
                Reason = "Planned"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [sourcePath, sourcePath]);

            Assert.Equal(1, result.RequestedCount);
            Assert.Equal(1, result.AppliedCount);
            Assert.Single(result.Results);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_CaseOnlySelectionDifference_IsPlatformAware()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "CaseFile.mkv");
            var selectedPath = Path.Combine(root, "incoming", "casefile.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "organized", "Case File", "Case File.mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "content");

            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "move",
                Reason = "Planned"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [selectedPath]);
            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(1, result.AppliedCount);
                Assert.Equal(0, result.SkippedCount);
                Assert.Equal("applied", result.Results[0].Status);
            }
            else
            {
                Assert.Equal(0, result.AppliedCount);
                Assert.Equal(1, result.SkippedCount);
                Assert.Equal("NotFoundInPlan", result.Results[0].Reason);
            }
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_Fails_WhenTargetIsOutsidePlanRoot()
    {
        var root = CreateTempRoot();
        try
        {
            var sourcePath = Path.Combine(root, "incoming", "noroi.mkv");
            var organizationRoot = Path.Combine(root, "organized");
            var targetPath = Path.Combine(root, "elsewhere", "Noroi (2005).mkv");
            Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
            File.WriteAllText(sourcePath, "source");

            var plan = CreatePlan(organizationRoot, new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = sourcePath,
                TargetPath = targetPath,
                Action = "move",
                Reason = "Planned"
            });

            var result = OrganizationApplyLogic.ApplySelected(plan, [sourcePath]);

            Assert.Equal(1, result.RequestedCount);
            Assert.Equal(0, result.AppliedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.Equal(1, result.FailedCount);
            Assert.Equal("failed", result.Results[0].Status);
            Assert.Equal("TargetOutsideRootPath", result.Results[0].Reason);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void ApplySelected_Succeeds_WhenCrossVolumeMoveIsRequired()
    {
        // Mock cross-volume move on any platform using different roots
        var sourcePath = OperatingSystem.IsWindows() ? @"C:\incoming\noroi.mkv" : "/mnt/a/incoming/noroi.mkv";
        var targetPath = OperatingSystem.IsWindows() ? @"D:\organized\Noroi (2005)\Noroi (2005).mkv" : "/mnt/b/organized/Noroi (2005)/Noroi (2005).mkv";
        var rootPath = OperatingSystem.IsWindows() ? @"D:\organized" : "/mnt/b/organized";

        var plan = CreatePlan(rootPath, new OrganizationPlanEntry
        {
            ItemId = "1",
            SourcePath = sourcePath,
            TargetPath = targetPath,
            Action = "move",
            Reason = "Planned"
        });

        var result = OrganizationApplyLogic.ApplySelected(
            plan,
            [sourcePath],
            path => path.Equals(sourcePath, StringComparison.OrdinalIgnoreCase),
            _ => { },
            (_, _) => { });

        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("applied", result.Results[0].Status);
    }

    private static OrganizationPlanSnapshot CreatePlan(string rootPath, params OrganizationPlanEntry[] entries)
    {
        return new OrganizationPlanSnapshot
        {
            RootPath = rootPath,
            DryRunMode = true,
            SourceSuggestionCount = entries.Length,
            PlannedCount = entries.Count(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase)),
            NoopCount = entries.Count(entry => entry.Action.Equals("none", StringComparison.OrdinalIgnoreCase)),
            SkippedCount = entries.Count(entry => entry.Action.Equals("skip", StringComparison.OrdinalIgnoreCase)),
            ConflictCount = entries.Count(entry => entry.Action.Equals("conflict", StringComparison.OrdinalIgnoreCase)),
            Entries = entries
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "shirarium-tests", Guid.NewGuid().ToString("N"));
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
