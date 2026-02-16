using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class PlanFingerprintTests
{
    [Fact]
    public void Compute_IsStable_ForEquivalentPlansWithDifferentEntryOrder()
    {
        var entryA = new OrganizationPlanEntry
        {
            ItemId = "1",
            SourcePath = @"D:\incoming\a.mkv",
            TargetPath = @"D:\organized\A (2020)\A (2020).mkv",
            Strategy = "movie",
            Action = "move",
            Reason = "Planned",
            Confidence = 0.9,
            SuggestedTitle = "A",
            SuggestedMediaType = "movie"
        };
        var entryB = new OrganizationPlanEntry
        {
            ItemId = "2",
            SourcePath = @"D:\incoming\b.mkv",
            TargetPath = @"D:\organized\B (2021)\B (2021).mkv",
            Strategy = "movie",
            Action = "move",
            Reason = "Planned",
            Confidence = 0.9,
            SuggestedTitle = "B",
            SuggestedMediaType = "movie"
        };

        var planA = CreatePlan([entryA, entryB]);
        var planB = CreatePlan([entryB, entryA]);

        var fingerprintA = PlanFingerprint.Compute(planA);
        var fingerprintB = PlanFingerprint.Compute(planB);

        Assert.Equal(fingerprintA, fingerprintB);
    }

    [Fact]
    public void Compute_Changes_WhenPlanPayloadChanges()
    {
        var basePlan = CreatePlan(
        [
            new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = @"D:\incoming\a.mkv",
                TargetPath = @"D:\organized\A (2020)\A (2020).mkv",
                Strategy = "movie",
                Action = "move",
                Reason = "Planned",
                Confidence = 0.9,
                SuggestedTitle = "A",
                SuggestedMediaType = "movie"
            }
        ]);

        var changedPlan = CreatePlan(
        [
            new OrganizationPlanEntry
            {
                ItemId = "1",
                SourcePath = @"D:\incoming\a.mkv",
                TargetPath = @"D:\organized\A (2020)\A (2020).mkv",
                Strategy = "movie",
                Action = "conflict",
                Reason = "TargetAlreadyExists",
                Confidence = 0.9,
                SuggestedTitle = "A",
                SuggestedMediaType = "movie"
            }
        ]);

        Assert.NotEqual(
            PlanFingerprint.Compute(basePlan),
            PlanFingerprint.Compute(changedPlan));
    }

    private static OrganizationPlanSnapshot CreatePlan(OrganizationPlanEntry[] entries)
    {
        return new OrganizationPlanSnapshot
        {
            RootPath = @"D:\organized",
            DryRunMode = true,
            SourceSuggestionCount = entries.Length,
            PlannedCount = entries.Count(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase)),
            NoopCount = entries.Count(entry => entry.Action.Equals("none", StringComparison.OrdinalIgnoreCase)),
            SkippedCount = entries.Count(entry => entry.Action.Equals("skip", StringComparison.OrdinalIgnoreCase)),
            ConflictCount = entries.Count(entry => entry.Action.Equals("conflict", StringComparison.OrdinalIgnoreCase)),
            Entries = entries
        };
    }
}
