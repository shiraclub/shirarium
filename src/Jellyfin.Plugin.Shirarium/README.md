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

## Admin API

- `POST /Shirarium/scan`
- `GET /Shirarium/suggestions`
- `POST /Shirarium/plan-organize`
- `GET /Shirarium/organization-plan`

## Notes

- Planning follows Jellyfin naming best practices for movie/episode structures.
- Behavior is non-destructive: no automatic file move/rename is performed.
