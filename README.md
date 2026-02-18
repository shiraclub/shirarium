# Shirarium

Clean up your Jellyfin library.

Shirarium turns messy file and folder names into Jellyfin-friendly structure using AI-assisted parsing and a review-first workflow.

Dry-run by default. Apply only what you approve. Undo supported.

![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.6-00A4DC?logo=jellyfin&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![Python](https://img.shields.io/badge/Python-3.12-3776AB?logo=python&logoColor=white)
![FastAPI](https://img.shields.io/badge/FastAPI-0.115-009688?logo=fastapi&logoColor=white)
[![CI](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml?query=branch%3Amaster)
![Safety](https://img.shields.io/badge/safety-dry--run%20by%20default-orange)

Contributing guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)

## What It Does

- Finds likely-unmatched items after library scans.
- Figures out what a file probably is from its name (movie vs episode, title/year/season/episode).
- Generates a clean, Jellyfin-friendly folder/file organization plan you can review and apply.
- Keeps it safe: dry-run by default, plus override, preflight, apply, and undo.
- Runs on your machine by default (no cloud required); optional local AI via Ollama.

## Installation (The Easy Way)

Shirarium is distributed via a managed plugin repository for seamless updates.

1.  Open your Jellyfin dashboard.
2.  Navigate to **Plugins** -> **Repositories**.
3.  Add a new repository with the following URL:
    ```text
    https://raw.githubusercontent.com/shiraclub/shirarium/master/manifest.json
    ```
4.  Navigate to the **Catalog** tab, find **Shirarium**, and click **Install**.
5.  Restart your Jellyfin server.

## Current Architecture

- Plugin: `src/Jellyfin.Plugin.Shirarium`
- Engine: `engine/app`
- Local stack: `docker-compose.yml`
- Dev scripts: `scripts/`

Design intent:
- Keep plugin runtime lightweight.
- Keep heavy parsing logic in `engine`.
- Stay non-destructive by default.

## Quick Start

### Prerequisites

- Docker Desktop
- .NET SDK 9.0+
- PowerShell 7+
- Local media path for read-only mount

### Setup

1. Copy env file:

```powershell
Copy-Item .env.example .env
```

2. Set media path in `.env`:

```env
MEDIA_PATH=D:/Media
```

3. Start stack:

```powershell
.\scripts\dev-up.ps1
```

4. Build/reload plugin:

```powershell
.\scripts\dev-reload.ps1
```

5. Open Jellyfin:

```text
http://localhost:8097
```

## Current Dev Handoff (2026-02-16)

What is verified working now:
- Plugin loads on Jellyfin `10.11.6` in local Docker dev stack.
- Shirarium endpoints are reachable and protected by Jellyfin admin auth.
- Review UI auth bug is fixed: the Review Console now calls APIs through authenticated `ApiClient` requests (no more unauthenticated `fetch` 401s).
- CI is green on current branch.

Important gotchas to remember:
- `An error occurred while getting the plugin details from the repository.` on the plugin detail page is expected for side-loaded local plugins that are not present in the configured plugin manifest.
- In Docker dev, media paths inside Jellyfin are Linux-style (`/media/...`), not Windows paths (`D:\...`).
- If no files exist under mounted media, Shirarium snapshots will correctly show zero examined/candidate/plan entries.
- A stale plugin `meta.json` status (for example `Malfunctioned`) can cause Jellyfin to skip loading the plugin until corrected.
- `scripts/dev-reload.ps1` currently builds to `artifacts/plugin/Shirarium`; if nested plugin layout causes load issues, build directly to `artifacts/plugin` and restart Jellyfin.

Fast path to get first non-empty plan:
1. Seed synthetic files:

```powershell
.\scripts\seed-dev-media.ps1 -CleanIncoming
```

2. Put additional sample files under `data/media/incoming` if needed (or point `.env` `MEDIA_PATH` to a real folder with files).
3. If `.env` `MEDIA_PATH` points outside the repo, seed there explicitly:

```powershell
.\scripts\seed-dev-media.ps1 -CleanIncoming -MediaRoot "D:\Media"
```

4. In Jellyfin, create/verify a library that points to `/media/incoming` (or `/media`).
5. Trigger a library scan, then run Shirarium scan/plan (post-scan task or scripts below).
6. In Review Console filters, use `/media/incoming` as `Path prefix`.
7. Reload Review tab and confirm non-zero entries.

## Synthetic Dataset Seeding

Seed script:

```powershell
.\scripts\seed-dev-media.ps1
```

Common options:
- `-CleanIncoming`: remove `${MEDIA_PATH}/incoming` before seeding.
- `-Force`: overwrite existing files that match manifest entries.
- `-MediaRoot "<path>"`: override target root instead of reading `.env` `MEDIA_PATH`.
- `-DatasetPath "<path>"`: load a custom dataset manifest file.
- `-Preset "<name>"`: load `datasets/jellyfin-dev/<name>.json` (default `synthetic-community-v1`).

Current built-in manifest:
- `datasets/jellyfin-dev/synthetic-community-v1.json`
- Mixes movies, TV, anime-style releases, duplicates, and ignored non-media extensions.

## Dry-Run Pipelines

### Automatic path

- Post-library-scan task (`ILibraryPostScanTask`) runs when enabled.
- It generates both snapshots in order: `dryrun-suggestions.json` then `organization-plan.json`.

### Manual path

```powershell
.\scripts\run-dryrun-scan.ps1
```

### Build organization plan (manual)

```powershell
.\scripts\run-organization-plan.ps1
```

### Apply selected plan entries (manual)

```powershell
.\scripts\apply-organization-plan.ps1 -SourcePath "D:\media\incoming\example.mkv"
```

### Apply plan by filters (preview by default)

```powershell
.\scripts\apply-plan-by-filter.ps1 -Strategy movie -Reason PlannedWithSuffix -Limit 50
```

Execute filtered apply (non-preview):

```powershell
.\scripts\apply-plan-by-filter.ps1 -Strategy movie -Apply
```

### Reviewed apply workflow (recommended)

Open Jellyfin dashboard page:

```text
Dashboard -> Plugins -> Shirarium -> Shirarium Review Console
```

UI tabs:
- `Review`: server-side paged plan browsing, row override patching, selection, and bulk actions.
- `Preflight`: exact reviewed move simulation (`WouldMove`, skips, failures) before mutation.
- `Locks`: immutable review lock create/list/get/apply.
- `History`: plan and override revision snapshots.

List reviewed plan entries with server-side filtering/paging:

```powershell
.\scripts\show-organization-plan-view.ps1 -MovesOnly -PageSize 50
```

Preflight reviewed entries (no file changes):

```powershell
.\scripts\preflight-reviewed-plan.ps1
```

Patch an entry override (set action/target/remove):

```powershell
.\scripts\patch-organization-plan-overrides.ps1 -SourcePath "D:\media\incoming\example.mkv" -Action skip
.\scripts\patch-organization-plan-overrides.ps1 -SourcePath "D:\media\incoming\example.mkv" -TargetPath "D:\media\organized\Movies\Example (2020)\Example (2020).mkv"
.\scripts\patch-organization-plan-overrides.ps1 -SourcePath "D:\media\incoming\example.mkv" -Remove
```

Create immutable review lock from current reviewed selection:

```powershell
.\scripts\create-review-lock.ps1
```

Inspect locks:

```powershell
.\scripts\show-review-locks.ps1 -Limit 20
.\scripts\show-review-lock.ps1 -ReviewId "<review-id>"
```

Apply by immutable review lock id:

```powershell
.\scripts\apply-review-lock.ps1 -ReviewId "<review-id>"
```

Apply reviewed move entries directly (all effective `move` entries by default):

```powershell
.\scripts\apply-reviewed-plan.ps1
```

Note:
- `apply-reviewed-plan` now requires a valid reviewed preflight token.
- `scripts/apply-reviewed-plan.ps1` auto-runs preflight first when `-PreflightToken` is not supplied.

Apply only specific reviewed entries:

```powershell
.\scripts\apply-reviewed-plan.ps1 -SourcePath "D:\media\incoming\example.mkv"
```

### Undo latest apply run (manual)

```powershell
.\scripts\undo-apply-plan.ps1
```

Undo a specific run:

```powershell
.\scripts\undo-apply-plan.ps1 -RunId "<apply-run-id>"
```

Resolve undo target collisions by moving existing files aside:

```powershell
.\scripts\undo-apply-plan.ps1 -RunId "<apply-run-id>" -TargetConflictPolicy suffix
```

### View latest snapshots

```powershell
.\scripts\show-suggestions.ps1
.\scripts\show-organization-plan.ps1
.\scripts\show-apply-journal.ps1
.\scripts\show-ops-status.ps1
.\scripts\show-review-locks.ps1
```

Snapshot locations:

```text
data/jellyfin/config/data/plugins/Shirarium/dryrun-suggestions.json
data/jellyfin/config/data/plugins/Shirarium/organization-plan.json
data/jellyfin/config/data/plugins/Shirarium/apply-journal.json
```

## Snapshot Schema (In-Dev)

- Snapshot payloads include explicit `schemaVersion` fields for:
  - `organization-plan`
  - `organization-plan-overrides`
  - `review-lock`
- Current behavior is intentionally strict during in-dev:
  - unsupported/missing schema versions are ignored instead of migrated.
  - no legacy/backward-compat snapshot upgrade path is applied.

## API Endpoints

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
- `POST /v1/parse-filename` (engine)
- `GET /health` (engine)

## Screenshots

- UI screenshot guidance and target filenames are documented in `docs/screenshots/README.md`.

## Testing

### Engine tests (local)

```powershell
cd .\engine
python -m unittest discover -s tests -v
```

### Engine tests (Docker)

```powershell
.\scripts\test-engine.ps1
```

### Plugin tests (.NET)

```powershell
.\scripts\test-plugin.ps1
```

Direct commands:

```powershell
dotnet test .\tests\Jellyfin.Plugin.Shirarium.Tests\Jellyfin.Plugin.Shirarium.Tests.csproj -c Release
dotnet test .\tests\Jellyfin.Plugin.Shirarium.IntegrationTests\Jellyfin.Plugin.Shirarium.IntegrationTests.csproj -c Release
```

Current test coverage:
- Heuristic filename parsing behavior.
- API validation/contract behavior.
- Ollama failure fallback behavior.
- Plugin scan logic (candidate reasons, extension support, confidence gating).
- Organization planning logic (path normalization, movie/episode path conventions, conflict/no-op handling, duplicate target detection).
- Organization apply logic (selected-entry gating, preflight safety checks, move execution, rollback operation journaling).
- Plan fingerprint determinism.
- Undo logic for journaled apply runs.
- Undo conflict resolution policies (`fail`/`skip`/`suffix`) with collision-safe rollback behavior.
- Review override logic (persisted entry-level action/target overrides).
- Integration flow coverage (`plan -> apply -> journal -> undo`) with lock/fingerprint safety checks.
- Integration flow coverage for reviewed apply (`plan -> override -> apply`) including stale fingerprint rejection.
- Controller contract coverage for review ops: reviewed preflight, immutable review lock create/list/get/apply, and history endpoints.
- Ops status aggregation coverage from persisted plan/apply/undo snapshots.
- Ops status scan observability coverage (candidate reason/source/confidence buckets).
- Filesystem integration matrix for conflict policies (`fail`/`skip`/`suffix`) on mixed movie/episode planning and suffix-based round trips.

## Configuration Notes

Plugin configuration includes:
- `EngineBaseUrl`
- `EnableAiParsing`
- `DryRunMode`
- `EnablePostScanTask`
- `MaxItemsPerRun`
- `MinConfidence`
- `ScanFileExtensions`
- `EnableFileOrganizationPlanning`
- `OrganizationRootPath`
- `NormalizePathSegments`
- `MoviePathTemplate`
- `EpisodePathTemplate`
- `TargetConflictPolicy` (`fail`, `skip`, `suffix`)

Template tokens:
- Movie: `{Title}`, `{TitleWithYear}`, `{Year}`
- Episode: `{Title}`, `{Season}`, `{Season2}`, `{Episode}`, `{Episode2}`

## Roadmap (Near-Term)

1. Optional queueing model for very large libraries.
2. Dry-run/apply throughput benchmarking for large remote libraries.
3. Bulk override presets and saved filter sets.

## Safety

- No automatic file move/rename happens in the current pipeline.
- Apply operations require explicit source-path selection and only execute entries currently marked `move`.
- Apply is guarded by plan fingerprint matching to ensure operations run only against the exact reviewed plan.
- Apply and undo are serialized with a filesystem lock to prevent concurrent operations.
- Apply preflight blocks unsafe operations: invalid paths, target outside root, cross-volume moves, and existing targets.
- Suggestions and organization plans are persisted for review and auditing before any future apply phase.
- Apply runs are written to `apply-journal.json` with inverse move operations for rollback auditability.
- Undo target conflict handling is explicit and operator-controlled (`fail` default, optional `skip`/`suffix`).
- Core repo license: `GPL-3.0`.
