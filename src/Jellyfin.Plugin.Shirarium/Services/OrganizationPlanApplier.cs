using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Applies explicitly selected move entries from the latest organization plan snapshot.
/// </summary>
public sealed class OrganizationPlanApplier
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;
    private readonly ILibraryManager? _libraryManager;
    private readonly IEnumerable<string>? _extraProtectedPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationPlanApplier"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="libraryManager">Optional Jellyfin library manager.</param>
    /// <param name="extraProtectedPaths">Optional extra paths to protect from cleanup.</param>
    public OrganizationPlanApplier(
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
    /// Applies selected move entries from the latest stored organization plan.
    /// </summary>
    /// <param name="request">Apply request payload containing selected source paths.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result containing per-item outcomes.</returns>
    public async Task<ApplyOrganizationPlanResult> RunAsync(
        ApplyOrganizationPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        return await RunInternalAsync(request, null, cancellationToken);
    }

    /// <summary>
    /// Applies selected move entries from the provided effective plan snapshot.
    /// </summary>
    /// <param name="request">Apply request payload containing selected source paths.</param>
    /// <param name="planOverride">Effective plan snapshot to apply against.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result containing per-item outcomes.</returns>
    public async Task<ApplyOrganizationPlanResult> RunAsync(
        ApplyOrganizationPlanRequest request,
        OrganizationPlanSnapshot planOverride,
        CancellationToken cancellationToken = default)
    {
        return await RunInternalAsync(request, planOverride, cancellationToken);
    }

    private async Task<ApplyOrganizationPlanResult> RunInternalAsync(
        ApplyOrganizationPlanRequest request,
        OrganizationPlanSnapshot? planOverride,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            throw new InvalidOperationException("MissingExpectedPlanFingerprint");
        }

        using var operationLock = OperationLock.TryAcquire(_applicationPaths);
        if (operationLock is null)
        {
            throw new InvalidOperationException("OperationAlreadyInProgress");
        }

        var plan = planOverride ?? OrganizationPlanStore.Read(_applicationPaths);
        if (!request.ExpectedPlanFingerprint.Equals(plan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("PlanFingerprintMismatch");
        }

        var protectedPaths = new List<string>();
        if (!string.IsNullOrWhiteSpace(plan.RootPath))
        {
            protectedPaths.Add(plan.RootPath);
        }

        if (_libraryManager != null)
        {
            protectedPaths.AddRange(_libraryManager.GetVirtualFolders().SelectMany(f => f.Locations));
        }

        if (_extraProtectedPaths != null)
        {
            protectedPaths.AddRange(_extraProtectedPaths);
        }

        var result = OrganizationApplyLogic.ApplySelected(
            plan,
            request.SourcePaths,
            protectedPaths.Distinct(PathComparison.Comparer),
            cancellationToken);

        await ApplyJournalStore.AppendApplyAsync(_applicationPaths, result, cancellationToken);

        // If library manager is available, we try to update Jellyfin's knowledge of these files.
        if (_libraryManager != null && result.AppliedCount > 0)
        {
            _ = Task.Run(() => 
            {
                try 
                {
                    _logger.LogInformation("Triggering Jellyfin library scan for {Count} moved items.", result.AppliedCount);
                    // A full scan is the safest way to ensure path changes are picked up correctly
                    // without causing 'missing file' duplicates in the database during transition.
                    _libraryManager.ValidateMediaLibrary(new Progress<double>(), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to trigger Jellyfin library scan after apply.");
                }
            }, cancellationToken);
        }

        _logger.LogInformation(
            "Shirarium apply plan complete. RunId={RunId} Fingerprint={PlanFingerprint} Requested={Requested} Applied={Applied} Skipped={Skipped} Failed={Failed}",
            result.RunId,
            result.PlanFingerprint,
            result.RequestedCount,
            result.AppliedCount,
            result.SkippedCount,
            result.FailedCount);

        return result;
    }
}
