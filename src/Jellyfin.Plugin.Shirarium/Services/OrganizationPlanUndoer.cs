using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Undoes a previously applied organization plan run using journaled inverse operations.
/// </summary>
public sealed class OrganizationPlanUndoer
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly ILibraryManager? _libraryManager;
    private readonly IEnumerable<string>? _extraProtectedPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationPlanUndoer"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="libraryManager">Optional Jellyfin library manager.</param>
    /// <param name="extraProtectedPaths">Optional extra paths to protect from cleanup.</param>
    public OrganizationPlanUndoer(
        IApplicationPaths applicationPaths, 
        ILogger logger,
        ILibraryManager? libraryManager = null,
        IEnumerable<string>? extraProtectedPaths = null)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
        _libraryManager = libraryManager;
        _extraProtectedPaths = extraProtectedPaths;
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

        if (!UndoApplyLogic.IsSupportedTargetConflictPolicy(request.TargetConflictPolicy))
        {
            throw new InvalidOperationException("InvalidUndoTargetConflictPolicy");
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

        var protectedPaths = new List<string>();
        if (_libraryManager != null)
        {
            protectedPaths.AddRange(_libraryManager.GetVirtualFolders().SelectMany(f => f.Locations));
        }

        if (_extraProtectedPaths != null)
        {
            protectedPaths.AddRange(_extraProtectedPaths);
        }

        var result = UndoApplyLogic.UndoRun(
            selectedRun,
            request.TargetConflictPolicy,
            protectedPaths.Distinct(PathComparison.Comparer),
            cancellationToken);
        await ApplyJournalStore.AppendUndoAsync(_applicationPaths, result, cancellationToken);

        // If library manager is available, we try to update Jellyfin's knowledge of these files.
        if (_libraryManager != null && result.AppliedCount > 0)
        {
            _ = Task.Run(() => 
            {
                try 
                {
                    _logger.LogInformation("Triggering Jellyfin library scan after undo.");
                    _libraryManager.ValidateMediaLibrary(new Progress<double>(), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to trigger Jellyfin library scan after undo.");
                }
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Shirarium undo apply complete. UndoRunId={UndoRunId} SourceApplyRunId={SourceApplyRunId} Requested={Requested} Applied={Applied} Skipped={Skipped} Failed={Failed} ConflictsResolved={ConflictsResolved}",
            result.UndoRunId,
            result.SourceApplyRunId,
            result.RequestedCount,
            result.AppliedCount,
            result.SkippedCount,
            result.FailedCount,
            result.ConflictResolvedCount);

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
