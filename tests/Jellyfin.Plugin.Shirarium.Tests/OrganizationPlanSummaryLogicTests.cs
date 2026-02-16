using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OrganizationPlanSummaryLogicTests
{
    [Fact]
    public void Build_ComputesCountsAndTopFolders()
    {
        var root = @"D:\organized";
        var plan = new OrganizationPlanSnapshot
        {
            PlanFingerprint = "plan-v1",
            RootPath = root,
            SourceSuggestionCount = 6,
            PlannedCount = 3,
            NoopCount = 1,
            SkippedCount = 1,
            ConflictCount = 1,
            Entries =
            [
                new OrganizationPlanEntry
                {
                    ItemId = "1",
                    SourcePath = @"D:\incoming\a.mkv",
                    TargetPath = @"D:\organized\Noroi (2005)\Noroi (2005).mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.9
                },
                new OrganizationPlanEntry
                {
                    ItemId = "2",
                    SourcePath = @"D:\incoming\b.mkv",
                    TargetPath = @"D:\organized\Noroi (2005)\Noroi (2005) (2).mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "PlannedWithSuffix",
                    Confidence = 0.9
                },
                new OrganizationPlanEntry
                {
                    ItemId = "3",
                    SourcePath = @"D:\incoming\c.mkv",
                    TargetPath = @"D:\organized\Kowasugi\Season 01\Kowasugi - S01E02.mkv",
                    Strategy = "episode",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.9
                },
                new OrganizationPlanEntry
                {
                    ItemId = "4",
                    SourcePath = @"D:\incoming\d.mkv",
                    TargetPath = @"D:\organized\Noroi (2005)\Noroi (2005).mkv",
                    Strategy = "movie",
                    Action = "conflict",
                    Reason = "TargetAlreadyExists",
                    Confidence = 0.8
                },
                new OrganizationPlanEntry
                {
                    ItemId = "5",
                    SourcePath = @"D:\incoming\e.mkv",
                    TargetPath = @"D:\organized\Kowasugi\Season 01\Kowasugi - S01E03.mkv",
                    Strategy = "episode",
                    Action = "none",
                    Reason = "AlreadyOrganized",
                    Confidence = 0.8
                },
                new OrganizationPlanEntry
                {
                    ItemId = "6",
                    SourcePath = @"D:\incoming\f.mkv",
                    Strategy = "episode",
                    Action = "skip",
                    Reason = "MissingSeasonOrEpisode",
                    Confidence = 0.8
                }
            ]
        };

        var summary = OrganizationPlanSummaryLogic.Build(plan);

        Assert.Equal("plan-v1", summary.PlanFingerprint);
        Assert.Equal(root, summary.RootPath);
        Assert.Equal(6, summary.TotalEntries);
        Assert.Equal(3, summary.PlannedCount);
        Assert.Equal(1, summary.NoopCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Equal(1, summary.ConflictCount);

        var moveAction = Assert.Single(summary.ActionCounts, bucket => bucket.Key == "move");
        Assert.Equal(3, moveAction.Count);

        var movieStrategy = Assert.Single(summary.StrategyCounts, bucket => bucket.Key == "movie");
        Assert.Equal(3, movieStrategy.Count);

        var plannedReason = Assert.Single(summary.ReasonCounts, bucket => bucket.Key == "Planned");
        Assert.Equal(2, plannedReason.Count);

        var topFolder = Assert.Single(summary.TopTargetFolders, bucket => bucket.Folder == "Noroi (2005)");
        Assert.Equal(2, topFolder.Count);
    }
}
