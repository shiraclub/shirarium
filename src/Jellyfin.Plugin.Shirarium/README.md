# Jellyfin.Plugin.Shirarium

Plugin component for Shirarium's local-first metadata and file-organization planning workflow.

## Current Functionality

- Plugin metadata and configuration surface.
- Engine HTTP client for filename parsing.
- Post-library-scan dry-run task that generates:
  - metadata suggestions
  - organization plan snapshot
- Snapshot persistence:
  - `dryrun-suggestions.json`
  - `organization-plan.json`
  - `apply-journal.json`

## Admin API

- `POST /shirarium/scan`
- `GET /shirarium/suggestions`
- `POST /shirarium/plan-organize`
- `GET /shirarium/organization-plan`
- `GET /shirarium/organization-plan-view`
- `GET /shirarium/organization-plan-summary`
- `PATCH /shirarium/organization-plan-entry-overrides`
- `POST /shirarium/preflight-reviewed-plan`
- `POST /shirarium/apply-plan`
- `POST /shirarium/apply-plan-by-filter`
- `POST /shirarium/apply-reviewed-plan`
- `POST /shirarium/review-locks`
- `GET /shirarium/review-locks`
- `GET /shirarium/review-locks/{reviewId}`
- `POST /shirarium/review-locks/{reviewId}/apply`
- `GET /shirarium/organization-plan-history`
- `GET /shirarium/organization-plan-overrides-history`
- `POST /shirarium/undo-apply`
- `GET /shirarium/ops-status`

## Notes

- Planning follows Jellyfin naming best practices for movie/episode structures.
- Planning templates are configurable via `MoviePathTemplate` and `EpisodePathTemplate`.
- Template tokens: movie `{Title}`, `{TitleWithYear}`, `{Year}`; episode `{Title}`, `{Season}`, `{Season2}`, `{Episode}`, `{Episode2}`.
- Target conflicts are configurable via `TargetConflictPolicy`: `fail`, `skip`, or `suffix`.
- Undo target conflicts are configurable per request via `UndoApplyRequest.TargetConflictPolicy`: `fail` (default), `skip`, or `suffix`.
  - `fail`: keep current behavior and report `UndoTargetAlreadyExists`.
  - `skip`: skip conflicting undo item.
  - `suffix`: move existing target aside using `(... undo-conflict N)` and then restore source.
- Behavior is non-destructive by default: no automatic file move/rename is performed.
- Apply is explicit and selection-based: only source paths chosen by admin and marked `move` in plan are executed.
- Filtered apply supports deterministic selection by strategy/reason/path prefix/confidence/limit with preview mode.
- Plan review view supports server-side filtering/sorting/paging plus override visibility.
- Per-entry overrides are persisted by plan fingerprint and support action/target path edits.
- Reviewed preflight simulates exact would-move/would-skip/would-fail outcomes without filesystem mutation.
- Reviewed apply combines latest plan + persisted overrides under the same fingerprint safety guard.
- Immutable review locks freeze reviewed selection + effective plan for deterministic apply-by-id execution.
- Plan and override revision histories are persisted and queryable for operator auditability.
- Jellyfin dashboard page (`Shirarium Review`) provides in-app tabs for Review, Preflight, Locks, and History.
- Controller endpoints require Jellyfin elevation policy (`Policies.RequiresElevation`).
- Apply requires `expectedPlanFingerprint` and rejects stale plan submissions.
- Apply and undo are serialized by a filesystem lock (`apply.lock`) to prevent concurrent file mutation runs.
- Apply performs preflight safety checks before moving: canonical path validation, root-bound target validation, same-volume requirement, and target collision checks.
- Successful apply runs store inverse move operations so `undo-apply` can restore files.
- `ops-status` provides a compact operational summary of latest scan/plan/apply/undo runs for ops visibility.
- Scan snapshots now include observability buckets for candidate reasons, parser sources, and confidence ranges.
- Snapshot storage is strict in-dev: unsupported or missing `schemaVersion` values are ignored (no legacy migration path).
