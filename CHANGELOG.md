# Changelog

All notable changes to this project will be documented in this file.

## [0.10.0] - 2026-02-16

### Added
- Jellyfin plugin dashboard page: `Shirarium Review` with Review, Preflight, Locks, and History tabs.
- In-UI reviewed workflow actions:
  - paged plan view loading
  - row-level override patching
  - bulk action overrides for selected entries
  - bulk target-prefix overrides for selected entries
  - reviewed preflight execution
  - reviewed apply execution
  - review lock create/list/get/apply
- Snapshot schema version fields for plan, overrides, and review locks.
- Reviewed preflight one-time token store and API contract fields.
- Integration coverage for:
  - idempotent `POST /shirarium/review-locks/{reviewId}/apply`
  - controller elevation policy presence
  - strict schema rejection for unsupported snapshot versions

### Changed
- API controller now requires Jellyfin elevation policy (`Policies.RequiresElevation`).
- API validation/conflict failures now return machine-readable payloads (`code`, `message`, optional `details`).
- `POST /shirarium/apply-reviewed-plan` now requires a valid preflight token.
- Review-lock apply now returns the previously created apply run when the same lock is re-applied.
- Snapshot readers now use strict in-dev schema handling (unsupported/missing schema versions are ignored instead of migrated).
- Local dev compose image updated to Jellyfin `10.11.6`.
- Plugin package version set to `0.10.0`.

### CI
- Added `Smoke` workflow to boot Jellyfin + plugin in Docker and verify:
  - Jellyfin startup (`/System/Info/Public`)
  - Shirarium route registration/auth gate (`/shirarium/organization-plan` returns 401/403)
  - embedded config-page resource presence in plugin assembly
