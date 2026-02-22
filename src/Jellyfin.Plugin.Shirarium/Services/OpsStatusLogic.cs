using Jellyfin.Plugin.Shirarium.Models;

namespace Jellyfin.Plugin.Shirarium.Services;

internal static class OpsStatusLogic
{
    internal static OpsStatusResponse Build(
        ScanResultSnapshot scanSnapshot,
        OrganizationPlanSnapshot planSnapshot,
        ApplyJournalSnapshot journalSnapshot)
    {
        var hasScan = HasScan(scanSnapshot);
        var scanStatus = new OpsStatusScanStatus
        {
            HasScan = hasScan,
            GeneratedAtUtc = hasScan ? scanSnapshot.GeneratedAtUtc : null,
            DryRunMode = scanSnapshot.DryRunMode,
            ExaminedCount = scanSnapshot.ExaminedCount,
            CandidateCount = scanSnapshot.CandidateCount,
            ParsedCount = scanSnapshot.ParsedCount,
            SuggestionCount = scanSnapshot.Suggestions.Length,
            SkippedByLimitCount = scanSnapshot.SkippedByLimitCount,
            SkippedByConfidenceCount = scanSnapshot.SkippedByConfidenceCount,
            ParseFailureCount = scanSnapshot.ParseFailureCount,
            CandidateReasonCounts = BuildCountBuckets(scanSnapshot.CandidateReasonCounts),
            ParserSourceCounts = BuildCountBuckets(scanSnapshot.ParserSourceCounts),
            ConfidenceBucketCounts = BuildCountBuckets(scanSnapshot.ConfidenceBucketCounts)
        };

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
            ConflictCount = planSnapshot.ConflictCount,
            ActionCounts = BuildCountBuckets(planSnapshot.Entries.Select(entry => entry.Action)),
            StrategyCounts = BuildCountBuckets(planSnapshot.Entries.Select(entry => entry.Strategy)),
            ReasonCounts = BuildCountBuckets(planSnapshot.Entries.Select(entry => entry.Reason))
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
            Scan = scanStatus,
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
                ConflictResolvedCount = latestUndo.ConflictResolvedCount,
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

    internal static OpsStatusResponse Build(
        OrganizationPlanSnapshot planSnapshot,
        ApplyJournalSnapshot journalSnapshot)
    {
        return Build(new ScanResultSnapshot(), planSnapshot, journalSnapshot);
    }

    private static bool HasScan(ScanResultSnapshot snapshot)
    {
        return snapshot.ExaminedCount > 0
            || snapshot.CandidateCount > 0
            || snapshot.ParsedCount > 0
            || snapshot.SkippedByLimitCount > 0
            || snapshot.SkippedByConfidenceCount > 0
            || snapshot.ParseFailureCount > 0
            || snapshot.Suggestions.Length > 0
            || snapshot.CandidateReasonCounts.Length > 0
            || snapshot.ParserSourceCounts.Length > 0
            || snapshot.ConfidenceBucketCounts.Length > 0;
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

    private static OpsStatusCountBucket[] BuildCountBuckets(IEnumerable<string> keys)
    {
        return keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .GroupBy(key => key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new OpsStatusCountBucket
            {
                Key = group.Key,
                Count = group.Count()
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static OpsStatusCountBucket[] BuildCountBuckets(IEnumerable<ScanCountBucket> buckets)
    {
        return buckets
            .Where(bucket => !string.IsNullOrWhiteSpace(bucket.Key))
            .Select(bucket => new OpsStatusCountBucket
            {
                Key = bucket.Key,
                Count = bucket.Count
            })
            .OrderByDescending(bucket => bucket.Count)
            .ThenBy(bucket => bucket.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
