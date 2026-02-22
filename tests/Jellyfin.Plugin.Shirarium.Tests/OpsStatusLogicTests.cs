using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using Xunit;

namespace Jellyfin.Plugin.Shirarium.Tests;

public sealed class OpsStatusLogicTests
{
    [Fact]
    public void Build_WithEmptySnapshots_ReturnsNoPlanAndNoRuns()
    {
        var status = OpsStatusLogic.Build(
            new OrganizationPlanSnapshot(),
            new ApplyJournalSnapshot());

        Assert.False(status.Plan.HasPlan);
        Assert.Null(status.LastApplyRun);
        Assert.Null(status.LastUndoRun);
    }

    [Fact]
    public void Build_SelectsLatestRuns_AndAggregatesReasons()
    {
        var scan = new ScanResultSnapshot
        {
            GeneratedAtUtc = DateTimeOffset.Parse("2026-02-16T21:59:00Z"),
            DryRunMode = true,
            ExaminedCount = 20,
            CandidateCount = 8,
            ParsedCount = 5,
            SkippedByLimitCount = 1,
            SkippedByConfidenceCount = 1,
            ParseFailureCount = 1,
            Suggestions =
            [
                new ScanSuggestion { ItemId = "scan-1", Path = @"D:\in\scan-a.mkv" },
                new ScanSuggestion { ItemId = "scan-2", Path = @"D:\in\scan-b.mkv" }
            ],
            CandidateReasonCounts =
            [
                new ScanCountBucket { Key = "MissingProviderIds", Count = 3 },
                new ScanCountBucket { Key = "NonStandardName", Count = 2 }
            ],
            ParserSourceCounts =
            [
                new ScanCountBucket { Key = "rule", Count = 4 },
                new ScanCountBucket { Key = "llm", Count = 1 }
            ],
            ConfidenceBucketCounts =
            [
                new ScanCountBucket { Key = "0.9-1.0", Count = 3 },
                new ScanCountBucket { Key = "0.8-0.9", Count = 2 }
            ]
        };

        var plan = new OrganizationPlanSnapshot
        {
            GeneratedAtUtc = DateTimeOffset.Parse("2026-02-16T22:00:00Z"),
            PlanFingerprint = "plan-v2",
            RootPath = @"D:\organized",
            SourceSuggestionCount = 10,
            PlannedCount = 6,
            NoopCount = 1,
            SkippedCount = 2,
            ConflictCount = 1,
            Entries =
            [
                new OrganizationPlanEntry
                {
                    ItemId = "1",
                    SourcePath = @"D:\in\a.mkv",
                    TargetPath = @"D:\organized\A\A.mkv",
                    Action = "move",
                    Reason = "Planned"
                }
            ]
        };

        var oldApply = new ApplyOrganizationPlanResult
        {
            RunId = "apply-old",
            AppliedAtUtc = DateTimeOffset.Parse("2026-02-16T22:01:00Z"),
            PlanFingerprint = "plan-v1"
        };

        var latestApply = new ApplyOrganizationPlanResult
        {
            RunId = "apply-new",
            AppliedAtUtc = DateTimeOffset.Parse("2026-02-16T22:02:00Z"),
            PlanFingerprint = "plan-v2",
            RequestedCount = 3,
            AppliedCount = 1,
            SkippedCount = 1,
            FailedCount = 1,
            Results =
            [
                new ApplyOrganizationPlanItemResult { Status = "failed", Reason = "TargetAlreadyExists" },
                new ApplyOrganizationPlanItemResult { Status = "failed", Reason = "targetalreadyexists" },
                new ApplyOrganizationPlanItemResult { Status = "skipped", Reason = "NotMoveAction" }
            ],
            UndoneByRunId = "undo-new",
            UndoneAtUtc = DateTimeOffset.Parse("2026-02-16T22:04:00Z")
        };

        var oldUndo = new UndoApplyResult
        {
            UndoRunId = "undo-old",
            SourceApplyRunId = "apply-old",
            UndoneAtUtc = DateTimeOffset.Parse("2026-02-16T22:03:00Z")
        };

        var latestUndo = new UndoApplyResult
        {
            UndoRunId = "undo-new",
            SourceApplyRunId = "apply-new",
            UndoneAtUtc = DateTimeOffset.Parse("2026-02-16T22:04:00Z"),
            RequestedCount = 2,
            AppliedCount = 1,
            FailedCount = 1,
            ConflictResolvedCount = 1,
            Results =
            [
                new UndoApplyItemResult { Status = "failed", Reason = "UndoTargetAlreadyExists" },
                new UndoApplyItemResult { Status = "skipped", Reason = "UndoSourceMissing" }
            ]
        };

        var status = OpsStatusLogic.Build(
            scan,
            plan,
            new ApplyJournalSnapshot
            {
                Runs = [oldApply, latestApply],
                UndoRuns = [oldUndo, latestUndo]
            });

        Assert.True(status.Scan.HasScan);
        Assert.Equal(20, status.Scan.ExaminedCount);
        Assert.Equal(2, status.Scan.SuggestionCount);
        Assert.Equal(2, status.Scan.CandidateReasonCounts.Length);
        Assert.Equal("MissingProviderIds", status.Scan.CandidateReasonCounts[0].Key);

        Assert.True(status.Plan.HasPlan);
        Assert.Equal("plan-v2", status.Plan.PlanFingerprint);
        Assert.Equal(6, status.Plan.PlannedCount);
        Assert.NotEmpty(status.Plan.ActionCounts);
        Assert.NotEmpty(status.Plan.StrategyCounts);
        Assert.NotEmpty(status.Plan.ReasonCounts);

        Assert.NotNull(status.LastApplyRun);
        Assert.Equal("apply-new", status.LastApplyRun!.RunId);
        Assert.True(status.LastApplyRun.WasUndone);
        Assert.Equal("undo-new", status.LastApplyRun.UndoneByRunId);
        Assert.Single(status.LastApplyRun.FailedReasons);
        Assert.Equal("TargetAlreadyExists", status.LastApplyRun.FailedReasons[0].Reason, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(2, status.LastApplyRun.FailedReasons[0].Count);
        Assert.Single(status.LastApplyRun.SkippedReasons);
        Assert.Equal("NotMoveAction", status.LastApplyRun.SkippedReasons[0].Reason);

        Assert.NotNull(status.LastUndoRun);
        Assert.Equal("undo-new", status.LastUndoRun!.UndoRunId);
        Assert.Equal("apply-new", status.LastUndoRun.SourceApplyRunId);
        Assert.Equal(1, status.LastUndoRun.ConflictResolvedCount);
        Assert.Single(status.LastUndoRun.FailedReasons);
        Assert.Equal("UndoTargetAlreadyExists", status.LastUndoRun.FailedReasons[0].Reason);
        Assert.Single(status.LastUndoRun.SkippedReasons);
        Assert.Equal("UndoSourceMissing", status.LastUndoRun.SkippedReasons[0].Reason);
    }
}
