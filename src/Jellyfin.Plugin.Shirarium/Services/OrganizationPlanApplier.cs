using Jellyfin.Plugin.Shirarium.Models;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Services;

/// <summary>
/// Applies explicitly selected move entries from the latest organization plan snapshot.
/// </summary>
public sealed class OrganizationPlanApplier
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrganizationPlanApplier"/> class.
    /// </summary>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    public OrganizationPlanApplier(IApplicationPaths applicationPaths, ILogger logger)
    {
        _applicationPaths = applicationPaths;
        _logger = logger;
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
        var plan = OrganizationPlanStore.Read(_applicationPaths);
        var result = OrganizationApplyLogic.ApplySelected(
            plan,
            request.SourcePaths,
            cancellationToken);

        await ApplyJournalStore.AppendAsync(_applicationPaths, result, cancellationToken);

        _logger.LogInformation(
            "Shirarium apply plan complete. RunId={RunId} Requested={Requested} Applied={Applied} Skipped={Skipped} Failed={Failed}",
            result.RunId,
            result.RequestedCount,
            result.AppliedCount,
            result.SkippedCount,
            result.FailedCount);

        return result;
    }
}
