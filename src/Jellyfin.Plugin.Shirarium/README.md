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

- `POST /Shirarium/scan`
- `GET /Shirarium/suggestions`
- `POST /Shirarium/plan-organize`
- `GET /Shirarium/organization-plan`
- `POST /Shirarium/apply-plan`

## Notes

- Planning follows Jellyfin naming best practices for movie/episode structures.
- Behavior is non-destructive by default: no automatic file move/rename is performed.
- Apply is explicit and selection-based: only source paths chosen by admin and marked `move` in plan are executed.
- Apply performs preflight safety checks before moving: canonical path validation, root-bound target validation, same-volume requirement, and target collision checks.
