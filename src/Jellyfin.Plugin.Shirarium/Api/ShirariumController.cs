using Jellyfin.Plugin.Shirarium.Models;
using Jellyfin.Plugin.Shirarium.Services;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Shirarium.Api;

/// <summary>
/// Administrative endpoints for scanning, suggestion snapshots, and organization planning snapshots.
/// </summary>
[ApiController]
[Route("shirarium")]
[Authorize(Policy = Policies.RequiresElevation)]
public sealed class ShirariumController : ControllerBase
{
    private readonly IApplicationPaths _applicationPaths;
    private readonly OrganizationPlanApplier _applier;
    private readonly OrganizationPlanner _planner;
    private readonly ShirariumScanner _scanner;
    private readonly OrganizationPlanUndoer _undoer;
    private readonly EngineClient _engineClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShirariumController"/> class.
    /// </summary>
    public ShirariumController(
        ILibraryManager libraryManager,
        IApplicationPaths applicationPaths,
        ILogger<ShirariumController> logger,
        ILoggerFactory loggerFactory)
    {
        _applicationPaths = applicationPaths;
        
        // Manual Composition for Plugin Internal Services
        // Ideally we would register these in DI, but for a plugin this is robust.
        var heuristicParser = new HeuristicParser();
        var ollamaService = new OllamaService(loggerFactory.CreateLogger<OllamaService>());
        _engineClient = new EngineClient(heuristicParser, ollamaService);

        _scanner = new ShirariumScanner(
            libraryManager,
            applicationPaths,
            logger, // Controller logger is fine here, or use factory
            _engineClient);

        _planner = new OrganizationPlanner(applicationPaths, logger);
        _applier = new OrganizationPlanApplier(applicationPaths, logger, libraryManager);
        _undoer = new OrganizationPlanUndoer(applicationPaths, logger, libraryManager);
    }

    /// <summary>
    /// Gets the current status of the managed local inference engine.
    /// </summary>
    [HttpGet("inference-status")]
    public ActionResult<InferenceStatusResponse> GetInferenceStatus()
    {
        var manager = Plugin.Instance?.InferenceManager;
        if (manager == null)
        {
            return Ok(new InferenceStatusResponse { Status = "NotInitialized" });
        }

        var (status, progress, error) = manager.GetStatus();
        return Ok(new InferenceStatusResponse
        {
            Status = status,
            Progress = progress,
            Error = error
        });
    }

    /// <summary>
    /// Performs a benchmark of the parsing engines.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Benchmark results.</returns>
    [HttpPost("benchmark")]
    public async Task<ActionResult<object>> RunBenchmark(CancellationToken cancellationToken)
    {
        var result = await _engineClient.BenchmarkAsync(cancellationToken);
        return Ok(result);
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
    /// Gets a filtered, sorted, and paged review view of the latest organization plan.
    /// </summary>
    /// <param name="request">View query options.</param>
    /// <returns>Plan review view payload.</returns>
    [HttpGet("organization-plan-view")]
    public ActionResult<OrganizationPlanViewResponse> GetOrganizationPlanView([FromQuery] OrganizationPlanViewRequest request)
    {
        var validationError = OrganizationPlanViewLogic.ValidateRequest(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return BadRequestError("ValidationError", "Invalid organization-plan view request.", validationError);
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        var overridesSnapshot = OrganizationPlanOverridesStore.ReadForFingerprint(_applicationPaths, plan.PlanFingerprint);
        var scanSnapshot = SuggestionStore.Read(_applicationPaths);
        return Ok(OrganizationPlanViewLogic.Build(plan, overridesSnapshot, request, scanSnapshot));
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
    /// Renders a sample path against a template to preview the result.
    /// </summary>
    /// <param name="request">Template test request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered target path preview.</returns>
    [HttpPost("test-template")]
    public async Task<ActionResult<object>> TestTemplate(
        [FromBody] TestTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequestError("PathRequired", "Sample path is required.");
        }

        var parsed = await _engineClient.ParseFilenameAsync(request.Path, cancellationToken);
        if (parsed is null)
        {
            return BadRequestError("ParseFailed", "Failed to parse sample path.");
        }

        var suggestion = new ScanSuggestion
        {
            Path = request.Path,
            SuggestedTitle = parsed.Title,
            SuggestedMediaType = parsed.MediaType,
            SuggestedYear = parsed.Year,
            SuggestedSeason = parsed.Season,
            SuggestedEpisode = parsed.Episode,
            Resolution = parsed.Resolution,
            VideoCodec = parsed.VideoCodec,
            AudioCodec = parsed.AudioCodec,
            AudioChannels = parsed.AudioChannels,
            ReleaseGroup = parsed.ReleaseGroup,
            Source = parsed.Source,
            Confidence = parsed.Confidence
        };

        var entry = OrganizationPlanLogic.BuildEntry(
            suggestion,
            request.RootPath ?? "/media",
            request.NormalizePathSegments ?? true,
            request.MoviePathTemplate ?? string.Empty,
            request.EpisodePathTemplate ?? string.Empty);

        return Ok(new
        {
            SourcePath = request.Path,
            TargetPath = entry.TargetPath,
            Action = entry.Action,
            Reason = entry.Reason,
            Strategy = entry.Strategy,
            Metadata = new
            {
                parsed.Title,
                parsed.MediaType,
                parsed.Year,
                parsed.Season,
                parsed.Episode,
                parsed.Resolution,
                parsed.VideoCodec,
                parsed.AudioCodec,
                parsed.AudioChannels,
                parsed.ReleaseGroup,
                parsed.Source,
                parsed.Confidence
            }
        });
    }

    /// <summary>
    /// Gets aggregated operational status from latest plan/apply/undo snapshots.
    /// </summary>
    /// <returns>Aggregated operational status.</returns>
    [HttpGet("ops-status")]
    public ActionResult<OpsStatusResponse> GetOpsStatus()
    {
        var scanSnapshot = SuggestionStore.Read(_applicationPaths);
        var planSnapshot = OrganizationPlanStore.Read(_applicationPaths);
        var journalSnapshot = ApplyJournalStore.Read(_applicationPaths);
        return Ok(OpsStatusLogic.Build(scanSnapshot, planSnapshot, journalSnapshot));
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
            return BadRequestError("SourcePathsRequired", "At least one source path must be provided.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return BadRequestError("ExpectedPlanFingerprintRequired", "ExpectedPlanFingerprint is required.");
        }

        try
        {
            var result = await _applier.RunAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("PlanFingerprintMismatch", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("OperationAlreadyInProgress", "Another apply or undo operation is already in progress.");
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
            return BadRequestError("RequestBodyRequired", "Request body is required.");
        }

        var validationError = OrganizationPlanFilterLogic.Validate(request);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            return BadRequestError("ValidationError", "Invalid apply-plan-by-filter request.", validationError);
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        if (!request.ExpectedPlanFingerprint.Equals(plan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
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
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("OperationAlreadyInProgress", "Another apply or undo operation is already in progress.");
        }
    }

    /// <summary>
    /// Updates persisted per-entry review overrides for the latest organization plan.
    /// </summary>
    /// <param name="request">Override patch request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Override patch result.</returns>
    [HttpPatch("organization-plan-entry-overrides")]
    public async Task<ActionResult<PatchOrganizationPlanEntryOverridesResponse>> PatchOrganizationPlanEntryOverrides(
        [FromBody] PatchOrganizationPlanEntryOverridesRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestError("RequestBodyRequired", "Request body is required.");
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        var validationError = OrganizationPlanOverridesLogic.ValidateRequest(request, plan);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            if (validationError.Equals("PlanFingerprintMismatch", StringComparison.OrdinalIgnoreCase))
            {
                return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
            }

            return BadRequestError("ValidationError", "Invalid organization-plan override patch request.", validationError);
        }

        var currentSnapshot = OrganizationPlanOverridesStore.ReadForFingerprint(
            _applicationPaths,
            request.ExpectedPlanFingerprint);
        var patchResult = OrganizationPlanOverridesLogic.ApplyPatches(
            currentSnapshot,
            request,
            request.ExpectedPlanFingerprint);
        await OrganizationPlanOverridesStore.WriteAsync(_applicationPaths, patchResult.Snapshot, cancellationToken);

        return Ok(new PatchOrganizationPlanEntryOverridesResponse
        {
            PlanFingerprint = patchResult.Snapshot.PlanFingerprint,
            UpdatedAtUtc = patchResult.Snapshot.UpdatedAtUtc,
            StoredCount = patchResult.Snapshot.Entries.Length,
            UpdatedCount = patchResult.UpdatedCount,
            RemovedCount = patchResult.RemovedCount
        });
    }

    /// <summary>
    /// Applies the reviewed plan by combining persisted overrides with the latest plan snapshot.
    /// </summary>
    /// <param name="request">Reviewed apply request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result for reviewed entries.</returns>
    [HttpPost("apply-reviewed-plan")]
    public async Task<ActionResult<ApplyOrganizationPlanResult>> ApplyReviewedPlan(
        [FromBody] ApplyReviewedPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestError("RequestBodyRequired", "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return BadRequestError("ExpectedPlanFingerprintRequired", "ExpectedPlanFingerprint is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PreflightToken))
        {
            return BadRequestError("PreflightTokenRequired", "PreflightToken is required. Run preflight-reviewed-plan first.");
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        if (!request.ExpectedPlanFingerprint.Equals(plan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }

        var overridesSnapshot = OrganizationPlanOverridesStore.ReadForFingerprint(
            _applicationPaths,
            request.ExpectedPlanFingerprint);
        var effectivePlan = OrganizationPlanReviewLogic.BuildEffectivePlan(plan, overridesSnapshot);

        var selectedSourcePaths = ResolveSelectedSourcePaths(request.SourcePaths, effectivePlan);

        if (selectedSourcePaths.Length == 0)
        {
            return BadRequestError("NoReviewedMoveEntries", "No reviewed move entries selected to apply.");
        }

        var consumeStatus = await ReviewedPreflightStore.ConsumeIfValidAsync(
            _applicationPaths,
            request.PreflightToken,
            request.ExpectedPlanFingerprint,
            selectedSourcePaths,
            cancellationToken);
        if (consumeStatus != ReviewedPreflightStore.ConsumeStatus.Success)
        {
            return consumeStatus switch
            {
                ReviewedPreflightStore.ConsumeStatus.MissingToken
                    => BadRequestError("PreflightTokenRequired", "PreflightToken is required. Run preflight-reviewed-plan first."),
                ReviewedPreflightStore.ConsumeStatus.TokenExpired
                    => BadRequestError("PreflightTokenExpired", "Preflight token expired. Run preflight-reviewed-plan again."),
                ReviewedPreflightStore.ConsumeStatus.PlanFingerprintMismatch
                    => ConflictError("PreflightPlanFingerprintMismatch", "Preflight token does not match current plan fingerprint."),
                ReviewedPreflightStore.ConsumeStatus.SelectedSourceMismatch
                    => ConflictError("PreflightSelectionMismatch", "Preflight token does not match selected source paths."),
                _ => BadRequestError("PreflightTokenInvalid", "Invalid preflight token. Run preflight-reviewed-plan again.")
            };
        }

        try
        {
            var result = await _applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = request.ExpectedPlanFingerprint,
                    SourcePaths = selectedSourcePaths
                },
                effectivePlan,
                cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("PlanFingerprintMismatch", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("OperationAlreadyInProgress", "Another apply or undo operation is already in progress.");
        }
    }

    /// <summary>
    /// Preflights reviewed plan entries and returns exactly what would move/skip/fail without mutating files.
    /// </summary>
    /// <param name="request">Reviewed preflight request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Preview result for reviewed selection.</returns>
    [HttpPost("preflight-reviewed-plan")]
    public async Task<ActionResult<PreflightReviewedPlanResponse>> PreflightReviewedPlan(
        [FromBody] PreflightReviewedPlanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestError("RequestBodyRequired", "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return BadRequestError("ExpectedPlanFingerprintRequired", "ExpectedPlanFingerprint is required.");
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        if (!request.ExpectedPlanFingerprint.Equals(plan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }

        var overridesSnapshot = OrganizationPlanOverridesStore.ReadForFingerprint(
            _applicationPaths,
            request.ExpectedPlanFingerprint);
        var effectivePlan = OrganizationPlanReviewLogic.BuildEffectivePlan(plan, overridesSnapshot);
        var selectedSourcePaths = ResolveSelectedSourcePaths(request.SourcePaths, effectivePlan);

        var previewResult = OrganizationApplyLogic.PreviewSelected(
            effectivePlan,
            selectedSourcePaths,
            cancellationToken);
        var preflightEntry = await ReviewedPreflightStore.IssueAsync(
            _applicationPaths,
            effectivePlan.PlanFingerprint,
            selectedSourcePaths,
            cancellationToken);

        return Ok(new PreflightReviewedPlanResponse
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            PlanFingerprint = effectivePlan.PlanFingerprint,
            PreflightToken = preflightEntry.Token,
            PreflightTokenExpiresAtUtc = preflightEntry.ExpiresAtUtc,
            MoveCandidateCount = effectivePlan.Entries.Count(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase)),
            SelectedSourcePaths = selectedSourcePaths,
            PreviewResult = previewResult
        });
    }

    /// <summary>
    /// Creates an immutable review lock snapshot that freezes reviewed selection and effective plan.
    /// </summary>
    /// <param name="request">Review lock creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created lock metadata.</returns>
    [HttpPost("review-locks")]
    public async Task<ActionResult<CreateReviewLockResponse>> CreateReviewLock(
        [FromBody] CreateReviewLockRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BadRequestError("RequestBodyRequired", "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.ExpectedPlanFingerprint))
        {
            return BadRequestError("ExpectedPlanFingerprintRequired", "ExpectedPlanFingerprint is required.");
        }

        var plan = OrganizationPlanStore.Read(_applicationPaths);
        if (!request.ExpectedPlanFingerprint.Equals(plan.PlanFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("PlanFingerprintMismatch", "Plan fingerprint mismatch. Refresh the organization plan and retry.");
        }

        var overridesSnapshot = OrganizationPlanOverridesStore.ReadForFingerprint(
            _applicationPaths,
            request.ExpectedPlanFingerprint);
        var effectivePlan = OrganizationPlanReviewLogic.BuildEffectivePlan(plan, overridesSnapshot);
        var selectedSourcePaths = ResolveSelectedSourcePaths(request.SourcePaths, effectivePlan);
        if (selectedSourcePaths.Length == 0)
        {
            return BadRequestError("NoReviewedMoveEntries", "No reviewed move entries selected to lock.");
        }

        var lockSnapshot = new ReviewLockSnapshot
        {
            ReviewId = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            PlanFingerprint = effectivePlan.PlanFingerprint,
            PlanRootPath = effectivePlan.RootPath,
            SelectedSourcePaths = selectedSourcePaths,
            EffectivePlan = effectivePlan,
            OverridesSnapshot = overridesSnapshot
        };

        await ReviewLockStore.AppendAsync(_applicationPaths, lockSnapshot, cancellationToken);

        return Ok(new CreateReviewLockResponse
        {
            ReviewId = lockSnapshot.ReviewId,
            CreatedAtUtc = lockSnapshot.CreatedAtUtc,
            PlanFingerprint = lockSnapshot.PlanFingerprint,
            MoveCandidateCount = effectivePlan.Entries.Count(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase)),
            SelectedCount = lockSnapshot.SelectedSourcePaths.Length
        });
    }

    /// <summary>
    /// Lists persisted immutable review lock snapshots.
    /// </summary>
    /// <param name="limit">Maximum returned items.</param>
    /// <returns>Review lock metadata list.</returns>
    [HttpGet("review-locks")]
    public ActionResult<ReviewLockListResponse> GetReviewLocks([FromQuery] int limit = 20)
    {
        if (limit <= 0 || limit > 200)
        {
            return BadRequestError("LimitOutOfRange", "limit must be within [1, 200].");
        }

        var entries = ReviewLockStore.Read(_applicationPaths)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .ToArray();

        return Ok(new ReviewLockListResponse
        {
            TotalCount = entries.Length,
            Items = entries
                .Take(limit)
                .Select(entry => new ReviewLockSummary
                {
                    ReviewId = entry.ReviewId,
                    CreatedAtUtc = entry.CreatedAtUtc,
                    PlanFingerprint = entry.PlanFingerprint,
                    SelectedCount = entry.SelectedSourcePaths.Length,
                    AppliedRunId = entry.AppliedRunId,
                    AppliedAtUtc = entry.AppliedAtUtc
                })
                .ToArray()
        });
    }

    /// <summary>
    /// Gets one immutable review lock snapshot by id.
    /// </summary>
    /// <param name="reviewId">Review lock id.</param>
    /// <returns>Full review lock payload.</returns>
    [HttpGet("review-locks/{reviewId}")]
    public ActionResult<ReviewLockSnapshot> GetReviewLock([FromRoute] string reviewId)
    {
        if (string.IsNullOrWhiteSpace(reviewId))
        {
            return BadRequestError("ReviewIdRequired", "reviewId is required.");
        }

        var lockSnapshot = ReviewLockStore.ReadById(_applicationPaths, reviewId);
        if (lockSnapshot is null)
        {
            return NotFoundError("ReviewLockNotFound", "Review lock not found.");
        }

        return Ok(lockSnapshot);
    }

    /// <summary>
    /// Applies one immutable review lock snapshot by id.
    /// </summary>
    /// <param name="reviewId">Review lock id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Apply result.</returns>
    [HttpPost("review-locks/{reviewId}/apply")]
    public async Task<ActionResult<ApplyOrganizationPlanResult>> ApplyReviewLock(
        [FromRoute] string reviewId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reviewId))
        {
            return BadRequestError("ReviewIdRequired", "reviewId is required.");
        }

        var lockSnapshot = ReviewLockStore.ReadById(_applicationPaths, reviewId);
        if (lockSnapshot is null)
        {
            return NotFoundError("ReviewLockNotFound", "Review lock not found.");
        }

        if (!string.IsNullOrWhiteSpace(lockSnapshot.AppliedRunId))
        {
            var existingRun = ApplyJournalStore.Read(_applicationPaths).Runs
                .FirstOrDefault(run => run.RunId.Equals(lockSnapshot.AppliedRunId, StringComparison.OrdinalIgnoreCase));
            if (existingRun is not null)
            {
                return Ok(existingRun);
            }

            return ConflictError("ReviewLockAlreadyApplied", "Review lock already applied.");
        }

        if (lockSnapshot.SelectedSourcePaths.Length == 0)
        {
            return BadRequestError("ReviewLockEmptySelection", "Review lock has no selected source paths.");
        }

        try
        {
            var result = await _applier.RunAsync(
                new ApplyOrganizationPlanRequest
                {
                    ExpectedPlanFingerprint = lockSnapshot.PlanFingerprint,
                    SourcePaths = lockSnapshot.SelectedSourcePaths
                },
                lockSnapshot.EffectivePlan,
                cancellationToken);

            await ReviewLockStore.MarkAppliedAsync(
                _applicationPaths,
                lockSnapshot.ReviewId,
                result.RunId,
                result.AppliedAtUtc,
                cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("PlanFingerprintMismatch", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("ReviewLockFingerprintMismatch", "Review lock fingerprint mismatch.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("OperationAlreadyInProgress", StringComparison.OrdinalIgnoreCase))
        {
            return ConflictError("OperationAlreadyInProgress", "Another apply or undo operation is already in progress.");
        }
    }

    /// <summary>
    /// Lists persisted organization plan revisions.
    /// </summary>
    /// <param name="limit">Maximum returned items.</param>
    /// <returns>Plan revision history.</returns>
    [HttpGet("organization-plan-history")]
    public ActionResult<OrganizationPlanHistoryResponse> GetOrganizationPlanHistory([FromQuery] int limit = 20)
    {
        if (limit <= 0 || limit > 200)
        {
            return BadRequestError("LimitOutOfRange", "limit must be within [1, 200].");
        }

        var entries = OrganizationPlanHistoryStore.Read(_applicationPaths)
            .OrderByDescending(entry => entry.GeneratedAtUtc)
            .ToArray();

        return Ok(new OrganizationPlanHistoryResponse
        {
            TotalCount = entries.Length,
            Items = entries.Take(limit).ToArray()
        });
    }

    /// <summary>
    /// Lists persisted organization plan override revisions.
    /// </summary>
    /// <param name="limit">Maximum returned items.</param>
    /// <returns>Override revision history.</returns>
    [HttpGet("organization-plan-overrides-history")]
    public ActionResult<OrganizationPlanOverridesHistoryResponse> GetOrganizationPlanOverridesHistory([FromQuery] int limit = 20)
    {
        if (limit <= 0 || limit > 200)
        {
            return BadRequestError("LimitOutOfRange", "limit must be within [1, 200].");
        }

        var entries = OrganizationPlanOverridesHistoryStore.Read(_applicationPaths)
            .OrderByDescending(entry => entry.UpdatedAtUtc)
            .ToArray();

        return Ok(new OrganizationPlanOverridesHistoryResponse
        {
            TotalCount = entries.Length,
            Items = entries.Take(limit).ToArray()
        });
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
            return ConflictError("OperationAlreadyInProgress", "Another apply or undo operation is already in progress.");
        }
        catch (InvalidOperationException ex) when (
            ex.Message.Equals("NoApplyRunsInJournal", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("ApplyRunNotFound", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("ApplyRunAlreadyUndone", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("ApplyRunHasNoUndoOperations", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Equals("InvalidUndoTargetConflictPolicy", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequestError(ex.Message, ex.Message);
        }
    }

    private static ObjectResult BadRequestError(string code, string message, object? details = null)
    {
        return ErrorResponse(StatusCodes.Status400BadRequest, code, message, details);
    }

    private static ObjectResult ConflictError(string code, string message, object? details = null)
    {
        return ErrorResponse(StatusCodes.Status409Conflict, code, message, details);
    }

    private static ObjectResult NotFoundError(string code, string message, object? details = null)
    {
        return ErrorResponse(StatusCodes.Status404NotFound, code, message, details);
    }

    private static ObjectResult ErrorResponse(int statusCode, string code, string message, object? details = null)
    {
        return new ObjectResult(new ApiErrorResponse
        {
            Code = code,
            Message = message,
            Details = details
        })
        {
            StatusCode = statusCode
        };
    }

    private static string[] ResolveSelectedSourcePaths(
        IEnumerable<string> requestedSourcePaths,
        OrganizationPlanSnapshot effectivePlan)
    {
        var normalizedRequestedPaths = NormalizeSourcePaths(requestedSourcePaths);
        if (normalizedRequestedPaths.Length > 0)
        {
            return normalizedRequestedPaths;
        }

        return effectivePlan.Entries
            .Where(entry => entry.Action.Equals("move", StringComparison.OrdinalIgnoreCase))
            .Select(entry => entry.SourcePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(PathComparison.Comparer)
            .OrderBy(path => path, PathComparison.Comparer)
            .ToArray();
    }

    private static string[] NormalizeSourcePaths(IEnumerable<string> sourcePaths)
    {
        return sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(PathComparison.Comparer)
            .OrderBy(path => path, PathComparison.Comparer)
            .ToArray();
    }
}
