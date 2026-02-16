using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Undoes a previously applied organization plan run using journaled inverse operations.
/// </summary>
public sealed class OrganizationPlanUndoer
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationPlanUndoer"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    public OrganizationPlanUndoer(IApplicationPaths applicationPaths, ILogger logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
    }

    /// <summary>
    /// Undoes one previous apply run.
    /// </summary>
    /// <param name="request">Undo request payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Undo result for the selected apply run.</returns>
    public async Task<UndoApplyResult> RunAsync(
        UndoApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        using var operationLock = OperationLock.TryAcquire(_applicationPaths);
        if (operationLock is null)
        {
            throw new InvalidOperationException("OperationAlreadyInProgress");
        }

        var journal = ApplyJournalStore.Read(_applicationPaths);
        if (journal.Runs.Length == 0)
        {
            throw new InvalidOperationException("NoApplyRunsInJournal");
        }

        var selectedRun = ResolveRun(journal, request.RunId);
        if (selectedRun is null)
        {
            throw new InvalidOperationException("ApplyRunNotFound");
        }

        if (!string.IsNullOrWhiteSpace(selectedRun.UndoneByRunId))
        {
            throw new InvalidOperationException("ApplyRunAlreadyUndone");
        }

        if (selectedRun.UndoOperations.Length == 0)
        {
            throw new InvalidOperationException("ApplyRunHasNoUndoOperations");
        }

        var result = UndoApplyLogic.UndoRun(selectedRun, cancellationToken);
        await ApplyJournalStore.AppendUndoAsync(_applicationPaths, result, cancellationToken);

        _logger.LogInformation(
            "Shirarium undo apply complete. UndoRunId={UndoRunId} SourceApplyRunId={SourceApplyRunId} Requested={Requested} Applied={Applied} Skipped={Skipped} Failed={Failed}",
            result.UndoRunId,
            result.SourceApplyRunId,
            result.RequestedCount,
            result.AppliedCount,
            result.SkippedCount,
            result.FailedCount);

        return result;
    }

    private static ApplyOrganizationPlanResult? ResolveRun(
        ApplyJournalSnapshot snapshot,
        string? runId)
    {
        if (!string.IsNullOrWhiteSpace(runId))
        {
            return snapshot.Runs.LastOrDefault(run =>
                run.RunId.Equals(runId, StringComparison.OrdinalIgnoreCase));
        }

        return snapshot.Runs.LastOrDefault();
    }
}
