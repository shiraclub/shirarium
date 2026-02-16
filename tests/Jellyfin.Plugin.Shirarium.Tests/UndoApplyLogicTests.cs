using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class UndoApplyLogicTests
{
    [Fact]
    public void UndoRun_AppliesInverseMoves_InReverseOrder()
    {
        var root = CreateTempRoot();
        try
        {
            var folder = Path.Combine(root, "organized");
            Directory.CreateDirectory(folder);

            var fromA = Path.Combine(folder, "A.mkv");
            var fromB = Path.Combine(folder, "B.mkv");
            var toA = Path.Combine(root, "incoming", "A.mkv");
            var toB = Path.Combine(root, "incoming", "B.mkv");
            File.WriteAllText(fromA, "a");
            File.WriteAllText(fromB, "b");

            var run = new ApplyOrganizationPlanResult
            {
                RunId = "run-1",
                UndoOperations =
                [
                    new ApplyUndoMoveOperation { FromPath = fromA, ToPath = toA },
                    new ApplyUndoMoveOperation { FromPath = fromB, ToPath = toB }
                ]
            };

            var callOrder = new List<string>();
            var result = UndoApplyLogic.UndoRun(
                run,
                File.Exists,
                path => _ = Directory.CreateDirectory(path),
                (source, target) =>
                {
                    callOrder.Add(Path.GetFileName(source));
                    File.Move(source, target);
                });

            Assert.Equal(2, result.RequestedCount);
            Assert.Equal(2, result.AppliedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.Equal(0, result.FailedCount);
            Assert.Equal(new[] { "B.mkv", "A.mkv" }, callOrder);
            Assert.True(File.Exists(toA));
            Assert.True(File.Exists(toB));
            Assert.False(File.Exists(fromA));
            Assert.False(File.Exists(fromB));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Fact]
    public void UndoRun_Skips_WhenUndoSourceIsMissing()
    {
        var run = new ApplyOrganizationPlanResult
        {
            RunId = "run-2",
            UndoOperations =
            [
                new ApplyUndoMoveOperation
                {
                    FromPath = @"D:\missing\from.mkv",
                    ToPath = @"D:\missing\to.mkv"
                }
            ]
        };

        var result = UndoApplyLogic.UndoRun(
            run,
            _ => false,
            _ => { },
            (_, _) => { });

        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("UndoSourceMissing", result.Results[0].Reason);
    }

    [Fact]
    public void UndoRun_Fails_WhenUndoTargetAlreadyExists()
    {
        var run = new ApplyOrganizationPlanResult
        {
            RunId = "run-3",
            UndoOperations =
            [
                new ApplyUndoMoveOperation
                {
                    FromPath = @"D:\from\file.mkv",
                    ToPath = @"D:\to\file.mkv"
                }
            ]
        };

        var result = UndoApplyLogic.UndoRun(
            run,
            path => path.Equals(@"D:\from\file.mkv", StringComparison.OrdinalIgnoreCase)
                || path.Equals(@"D:\to\file.mkv", StringComparison.OrdinalIgnoreCase),
            _ => { },
            (_, _) => { });

        Assert.Equal(1, result.RequestedCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal("UndoTargetAlreadyExists", result.Results[0].Reason);
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
