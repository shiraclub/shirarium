# Changelog

All notable changes to this project will be documented in this file.

## [0.10.0] - 2026-02-16

### Added
- Jellyfin plugin dashboard page: `Shirarium Review` with Review, Preflight, Locks, and History tabs.
- In-UI reviewed workflow actions:
  - paged plan view loading
  - row-level override patching
  - reviewed preflight execution
  - reviewed apply execution
  - review lock create/list/get/apply
- Snapshot schema version fields for plan, overrides, and review locks.
- Integration coverage for:
  - idempotent `POST /shirarium/review-locks/{reviewId}/apply`
  - controller elevation policy presence

### Changed
- API controller now requires Jellyfin elevation policy (`Policies.RequiresElevation`).
- Review-lock apply now returns the previously created apply run when the same lock is re-applied.
- Snapshot readers now use strict in-dev schema handling (unsupported/missing schema versions are ignored instead of migrated).
- Plugin package version set to `0.10.0`.

