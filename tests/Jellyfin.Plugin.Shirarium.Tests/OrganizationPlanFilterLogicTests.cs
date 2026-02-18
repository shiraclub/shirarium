using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OrganizationPlanFilterLogicTests
{
    [Fact]
    public void Select_AppliesFiltersDeterministically_AndRespectsLimit()
    {
        var plan = new OrganizationPlanSnapshot
        {
            PlanFingerprint = "plan-v1",
            Entries =
            [
                new OrganizationPlanEntry
                {
                    ItemId = "3",
                    SourcePath = @"D:\incoming\c.mkv",
                    TargetPath = @"D:\organized\Noroi (2005)\Noroi (2005).mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.50
                },
                new OrganizationPlanEntry
                {
                    ItemId = "2",
                    SourcePath = @"D:\incoming\b.mkv",
                    TargetPath = @"D:\organized\Noroi (2005)\Noroi (2005) (2).mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "PlannedWithSuffix",
                    Confidence = 0.95
                },
                new OrganizationPlanEntry
                {
                    ItemId = "1",
                    SourcePath = @"D:\incoming\a.mkv",
                    TargetPath = @"D:\organized\Noroi (2005)\Noroi (2005) (3).mkv",
                    Strategy = "movie",
                    Action = "move",
                    Reason = "PlannedWithSuffix",
                    Confidence = 0.90
                },
                new OrganizationPlanEntry
                {
                    ItemId = "4",
                    SourcePath = @"D:\incoming\episode.mkv",
                    TargetPath = @"D:\organized\Kowasugi\Season 01\Kowasugi S01E02.mkv",
                    Strategy = "episode",
                    Action = "move",
                    Reason = "Planned",
                    Confidence = 0.99
                },
                new OrganizationPlanEntry
                {
                    ItemId = "5",
                    SourcePath = @"D:\incoming\skip.mkv",
                    Strategy = "movie",
                    Action = "skip",
                    Reason = "TargetAlreadyExists",
                    Confidence = 0.99
                }
            ]
        };

        var selection = OrganizationPlanFilterLogic.Select(
            plan,
            new ApplyPlanByFilterRequest
            {
                ExpectedPlanFingerprint = "plan-v1",
                Strategies = ["movie"],
                Reasons = ["PlannedWithSuffix"],
                PathPrefix = @"D:\incoming\",
                MinConfidence = 0.85,
                Limit = 1
            });

        Assert.Equal(4, selection.MoveCandidateCount);
        Assert.Single(selection.SelectedSourcePaths);
        Assert.Equal(@"D:\incoming\a.mkv", selection.SelectedSourcePaths[0]);
    }

    [Fact]
    public void Validate_RejectsInvalidInputs()
    {
        var missingFingerprint = OrganizationPlanFilterLogic.Validate(
            new ApplyPlanByFilterRequest
            {
                ExpectedPlanFingerprint = ""
            });
        Assert.Equal("ExpectedPlanFingerprint is required.", missingFingerprint);

        var badConfidence = OrganizationPlanFilterLogic.Validate(
            new ApplyPlanByFilterRequest
            {
                ExpectedPlanFingerprint = "ok",
                MinConfidence = 1.1
            });
        Assert.Equal("MinConfidence must be within [0, 1].", badConfidence);

        var badLimit = OrganizationPlanFilterLogic.Validate(
            new ApplyPlanByFilterRequest
            {
                ExpectedPlanFingerprint = "ok",
                Limit = 0
            });
        Assert.Equal("Limit must be greater than 0 when provided.", badLimit);
    }
}
