using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OpsStatusLogic
{
    internal static OpsStatusResponse Build(
        OrganizationPlanSnapshot planSnapshot,
        ApplyJournalSnapshot journalSnapshot)
    {
        var hasPlan = HasPlan(planSnapshot);
        var planStatus = new OpsStatusPlanStatus
        {
            HasPlan = hasPlan,
            GeneratedAtUtc = hasPlan ? planSnapshot.GeneratedAtUtc : null,
            PlanFingerprint = hasPlan ? planSnapshot.PlanFingerprint : null,
            RootPath = hasPlan ? planSnapshot.RootPath : null,
            SourceSuggestionCount = planSnapshot.SourceSuggestionCount,
            PlannedCount = planSnapshot.PlannedCount,
            NoopCount = planSnapshot.NoopCount,
            SkippedCount = planSnapshot.SkippedCount,
            ConflictCount = planSnapshot.ConflictCount
        };

        var latestApply = journalSnapshot.Runs
            .OrderBy(run => run.AppliedAtUtc)
            .LastOrDefault();

        var latestUndo = journalSnapshot.UndoRuns
            .OrderBy(run => run.UndoneAtUtc)
            .LastOrDefault();

        return new OpsStatusResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Plan = planStatus,
            LastApplyRun = latestApply is null ? null : new OpsStatusApplyRunStatus
            {
                RunId = latestApply.RunId,
                AppliedAtUtc = latestApply.AppliedAtUtc,
                PlanFingerprint = latestApply.PlanFingerprint,
                RequestedCount = latestApply.RequestedCount,
                AppliedCount = latestApply.AppliedCount,
                SkippedCount = latestApply.SkippedCount,
                FailedCount = latestApply.FailedCount,
                WasUndone = !string.IsNullOrWhiteSpace(latestApply.UndoneByRunId),
                UndoneByRunId = latestApply.UndoneByRunId,
                UndoneAtUtc = latestApply.UndoneAtUtc,
                FailedReasons = BuildReasonCounts(
                    latestApply.Results
                        .Where(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                        .Select(result => result.Reason)),
                SkippedReasons = BuildReasonCounts(
                    latestApply.Results
                        .Where(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                        .Select(result => result.Reason))
            },
            LastUndoRun = latestUndo is null ? null : new OpsStatusUndoRunStatus
            {
                UndoRunId = latestUndo.UndoRunId,
                SourceApplyRunId = latestUndo.SourceApplyRunId,
                UndoneAtUtc = latestUndo.UndoneAtUtc,
                RequestedCount = latestUndo.RequestedCount,
                AppliedCount = latestUndo.AppliedCount,
                SkippedCount = latestUndo.SkippedCount,
                FailedCount = latestUndo.FailedCount,
                FailedReasons = BuildReasonCounts(
                    latestUndo.Results
                        .Where(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                        .Select(result => result.Reason)),
                SkippedReasons = BuildReasonCounts(
                    latestUndo.Results
                        .Where(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
                        .Select(result => result.Reason))
            }
        };
    }

    private static bool HasPlan(OrganizationPlanSnapshot snapshot)
    {
        return !string.IsNullOrWhiteSpace(snapshot.RootPath)
            || !string.IsNullOrWhiteSpace(snapshot.PlanFingerprint)
            || snapshot.Entries.Length > 0
            || snapshot.SourceSuggestionCount > 0
            || snapshot.PlannedCount > 0
            || snapshot.NoopCount > 0
            || snapshot.SkippedCount > 0
            || snapshot.ConflictCount > 0;
    }

    private static OpsStatusReasonCount[] BuildReasonCounts(IEnumerable<string> reasons)
    {
        return reasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OpsStatusReasonCount
            {
                Reason = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(reason => reason.Count)
            .ThenBy(reason => reason.Reason, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
