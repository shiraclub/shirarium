# Changelog

All notable changes to this project will be documented in this file.

## [0.0.14]

### Added
- Cross-volume move support: implemented "Copy + Delete" fallback when direct filesystem moves between volumes/mounts fail.
- Smart Acronym Preservation: heuristic parser now preserves the original casing for acronyms (e.g., NASA, S.H.I.E.L.D.) instead of applying title casing.
- Template Preview API: added `POST /shirarium/test-template` for real-time preview of path templates in the UI.
- GPU-Accelerated Inference: implemented automatic detection of NVIDIA (CUDA) and Vulkan drivers to enable GPU layers in the managed inference engine.
- Jellyfin Metadata Hand-off: automatic triggering of library validation after successful organization runs to sync the Jellyfin database with the new file paths.
- Series-Level Asset Aggregation: organization plans now automatically include show-level metadata (tvshow.nfo, series-level posters) when moving episodes.

## [0.0.13]

### Added
- Configurable inference port in plugin configuration.
- Support for TV show asset discovery in season and special folders.
- XML documentation for all public API contracts and services.

### Changed
- Performance: Optimized scanner candidate discovery to use streaming enumeration for reduced memory overhead.
- Robustness: Inference engine binary extraction is now safer and handles "file in use" errors gracefully.
- Logging: Store logic now correctly uses the plugin's logger for persistence warnings.

## [0.10.0]

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
