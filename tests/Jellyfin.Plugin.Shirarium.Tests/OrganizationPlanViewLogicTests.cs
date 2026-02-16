using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OrganizationPlanViewLogicTests
{
    [Fact]
    public void Build_AppliesOverrides_Filtering_Sorting_AndPaging()
    {
        var plan = new OrganizationPlanSnapshot
        {
            PlanFingerprint = "plan-v1",
            Entries =
            [
                new OrganizationPlanEntry
                {
                    ItemId = "1",
                    SourcePath = @"D:\incoming\c.mkv",
                    TargetPath = @"D:\organized\C\C.mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.7,
                    SuggestedTitle = "C",
                    SuggestedMediaType = "movie"
                },
                new OrganizationPlanEntry
                {
                    ItemId = "2",
                    SourcePath = @"D:\incoming\a.mkv",
                    TargetPath = @"D:\organized\A\A.mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.95,
                    SuggestedTitle = "A",
                    SuggestedMediaType = "movie"
                },
                new OrganizationPlanEntry
                {
                    ItemId = "3",
                    SourcePath = @"D:\incoming\b.mkv",
                    Strategy = "episode",
                    Action = "skip",
                    Reason = "MissingSeasonOrEpisode",
                    Confidence = 0.9,
                    SuggestedTitle = "B",
                    SuggestedMediaType = "episode"
                }
            ]
        };

        var overrides = new OrganizationPlanOverridesSnapshot
        {
            PlanFingerprint = "plan-v1",
            Entries =
            [
                new OrganizationPlanEntryOverride
                {
                    SourcePath = @"D:\incoming\a.mkv",
                    Action = "skip"
                },
                new OrganizationPlanEntryOverride
                {
                    SourcePath = @"D:\incoming\c.mkv",
                    Action = "move",
                    TargetPath = @"D:\organized\C\C-custom.mkv"
                }
            ]
        };

        var response = OrganizationPlanViewLogic.Build(
            plan,
            overrides,
            new OrganizationPlanViewRequest
            {
                Strategies = ["movie"],
                Actions = ["move"],
                MinConfidence = 0.6,
                SortBy = "confidence",
                SortDirection = "desc",
                Page = 1,
                PageSize = 1
            });

        Assert.Equal("plan-v1", response.PlanFingerprint);
        Assert.Equal(3, response.TotalEntries);
        Assert.Equal(1, response.FilteredEntries);
        Assert.Equal(2, response.OverrideCount);
        Assert.Single(response.Entries);
        Assert.Equal(@"D:\incoming\c.mkv", response.Entries[0].SourcePath);
        Assert.True(response.Entries[0].HasOverride);
        Assert.Equal("move", response.Entries[0].EffectiveAction);
        Assert.Equal(@"D:\organized\C\C-custom.mkv", response.Entries[0].EffectiveTargetPath);
    }

    [Fact]
    public void ValidateRequest_RejectsInvalidPagingAndSort()
    {
        var badPage = OrganizationPlanViewLogic.ValidateRequest(new OrganizationPlanViewRequest { Page = 0 });
        Assert.Equal("Page must be greater than 0.", badPage);

        var badPageSize = OrganizationPlanViewLogic.ValidateRequest(new OrganizationPlanViewRequest { PageSize = 5000 });
        Assert.Equal("PageSize must be within [1, 1000].", badPageSize);

        var badSortBy = OrganizationPlanViewLogic.ValidateRequest(new OrganizationPlanViewRequest { SortBy = "unknown" });
        Assert.Equal("SortBy must be one of: sourcePath, targetPath, confidence, strategy, action, reason.", badSortBy);

        var badDirection = OrganizationPlanViewLogic.ValidateRequest(new OrganizationPlanViewRequest { SortDirection = "sideways" });
        Assert.Equal("SortDirection must be asc or desc.", badDirection);
    }
}

