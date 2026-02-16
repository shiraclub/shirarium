using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Api;

/// <summary>
/// Administrative endpoints for scanning, suggestion snapshots, and organization planning snapshots.
/// </summary>
[ApiController]
[Route("shirarium")]
public sealed class ShirariumController : ControllerBase
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly OrganizationPlanApplier _applier;
    private readonly OrganizationPlanner _planner;
    private readonly ShirariumScanner _scanner;
    private readonly OrganizationPlanUndoer _undoer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShirariumController"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="applicationPaths">Jellyfin application paths.</param>
    /// <param name="logger">Logger instance.</param>
    public ShirariumController(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger<ShirariumController> logger)
    {
        _applicationPaths = applicationPaths;
        _scanner = new ShirariumScanner(libraryManager, applicationPaths, logger);
        _planner = new OrganizationPlanner(applicationPaths, logger);
        _applier = new OrganizationPlanApplier(applicationPaths, logger);
        _undoer = new OrganizationPlanUndoer(applicationPaths, logger);
    }

    /// <summary>
    /// Gets the latest stored dry-run suggestion snapshot.
    /// </summary>
    /// <returns>The current suggestion snapshot.</returns>
    [HttpGet("suggestions")]
    public ActionResult<ScanResultSnapshot> GetSuggestions()
    {
        return Ok(SuggestionStore.Read(_applicationPaths));
    }

    /// <summary>
    /// Triggers a dry-run scan and returns the generated snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated scan snapshot.</returns>
    [HttpPost("scan")]
    public async Task<ActionResult<ScanResultSnapshot>> RunScan(CancellationToken cancellationToken)
    {
        var snapshot = await _scanner.RunAsync(cancellationToken);
        return Ok(snapshot);
    }

    /// <summary>
    /// Gets the latest stored organization planning snapshot.
    /// </summary>
    /// <returns>The current organization plan snapshot.</returns>
    [HttpGet("organization-plan")]
    public ActionResult<OrganizationPlanSnapshot> GetOrganizationPlan()
    {
        return Ok(OrganizationPlanStore.Read(_applicationPaths));
    }

    /// <summary>
    /// Gets a compact summary view of the latest organization plan snapshot.
    /// </summary>
    /// <returns>Aggregated organization plan summary.</returns>
    [HttpGet("organization-plan-summary")]
    public ActionResult<OrganizationPlanSummaryResponse> GetOrganizationPlanSummary()
    {
        var plan = OrganizationPlanStore.Read(_applicationPaths);
        return Ok(OrganizationPlanSummaryLogic.Build(plan));
    }

    /// <summary>
    /// Gets aggregated operational status from latest plan/apply/undo snapshots.
    /// </summary>
    /// <returns>Aggregated operational status.</returns>
    [HttpGet("ops-status")]
    public ActionResult<OpsStatusResponse> GetOpsStatus()
    {
        var planSnapshot = OrganizationPlanStore.Read(_applicationPaths);
        var journalSnapshot = ApplyJournalStore.Read(_applicationPaths);
        return Ok(OpsStatusLogic.Build(planSnapshot, journalSnapshot));
    }

    /// <summary>
    /// Generates a non-destructive organization plan from the latest suggestion snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated organization plan snapshot.</returns>
    [HttpPost("plan-organize")]
    public async Task<ActionResult<OrganizationPlanSnapshot>> RunOrganizationPlanning(CancellationToken cancellationToken)
    {
        var snapshot = await _planner.RunAsync(cancellationToken: cancellationToken);
        return Ok(snapshot);
    }

    /// <summary>
    /// Applies explicitly selected move entries from the latest organization plan snapshot.
    /// </summary>
    /// <param name="request">Apply request containing source paths to apply.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result for requested entries.</returns>
    [HttpPost("apply-plan")]
    public async Task<ActionResult<ApplyOrganizationPlanResult>> ApplyOrganizationPlan(
        [FromBody] ApplyOrganizationPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || request.SourcePaths.Length == 0)
        {
            return BadRequest("At least one source path must be provided.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return BadRequest("ExpectedPlanFingerprint is required.");
        }

        try
        {
            var result = await _applier.RunAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("PlanFingerprintMismatch", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Another apply or undo operation is already in progress.");
        }
    }

    /// <summary>
    /// Selects plan entries by filters and optionally applies selected moves.
    /// </summary>
    /// <param name="request">Filter request with expected fingerprint and filter options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Selection preview and optional apply results.</returns>
    [HttpPost("apply-plan-by-filter")]
    public async Task<ActionResult<ApplyPlanByFilterResponse>> ApplyOrganizationPlanByFilter(
        [FromBody] ApplyPlanByFilterRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var validationError = OrganizationPlanFilterLogic.Validate(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return BadRequest(validationError);
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        if (!request.ExpectedPlanFingerprint.Equals(plan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }

        var selection = OrganizationPlanFilterLogic.Select(plan, request);
        var selectedSourcePaths = selection.SelectedSourcePaths;

        if (request.DryRunOnly || selectedSourcePaths.Length == 0)
        {
            return Ok(new ApplyPlanByFilterResponse
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                PlanFingerprint = plan.PlanFingerprint,
                DryRunOnly = true,
                MoveCandidateCount = selection.MoveCandidateCount,
                SelectedCount = selectedSourcePaths.Length,
                FilteredOutCount = selection.MoveCandidateCount - selectedSourcePaths.Length,
                SelectedSourcePaths = selectedSourcePaths
            });
        }

        try
        {
            var applyResult = await _applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = request.ExpectedPlanFingerprint,
                    SourcePaths = selectedSourcePaths
                },
                cancellationToken);

            return Ok(new ApplyPlanByFilterResponse
            {
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                PlanFingerprint = plan.PlanFingerprint,
                DryRunOnly = false,
                MoveCandidateCount = selection.MoveCandidateCount,
                SelectedCount = selectedSourcePaths.Length,
                FilteredOutCount = selection.MoveCandidateCount - selectedSourcePaths.Length,
                SelectedSourcePaths = selectedSourcePaths,
                ApplyResult = applyResult
            });
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("PlanFingerprintMismatch", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Another apply or undo operation is already in progress.");
        }
    }

    /// <summary>
    /// Undoes one previously applied run from the apply journal.
    /// </summary>
    /// <param name="request">Undo request; when run id is omitted the latest run is selected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Undo result for the selected apply run.</returns>
    [HttpPost("undo-apply")]
    public async Task<ActionResult<UndoApplyResult>> UndoApply(
        [FromBody] UndoApplyRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _undoer.RunAsync(request ?? new UndoApplyRequest(), cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Another apply or undo operation is already in progress.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("NoApplyRunsInJournal", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("ApplyRunNotFound", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("ApplyRunAlreadyUndone", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("ApplyRunHasNoUndoOperations", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ex.Message);
        }
    }
}
