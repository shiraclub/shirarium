# Shirarium

Local-first Jellyfin metadata and file-organization planner for chaotic libraries.

![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.6-00A4DC?logo=jellyfin&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![Python](https://img.shields.io/badge/Python-3.12-3776AB?logo=python&logoColor=white)
![FastAPI](https://img.shields.io/badge/FastAPI-0.115-009688?logo=fastapi&logoColor=white)
[![CI](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml?query=branch%3Amaster)
![Mode](https://img.shields.io/badge/mode-dry--run-orange)

Contributing guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)

## What It Does

- Runs a Jellyfin plugin (`.NET`) and an external parsing engine (`FastAPI`).
- Detects likely unmatched media candidates after library scans.
- Parses filenames via heuristics (and optional Ollama path).
- Stores dry-run suggestions without changing media files.
- Builds dry-run physical organization plans using Jellyfin naming best practices.
- Exposes richer scan observability buckets (candidate reasons, parser sources, confidence buckets).
- Targets practical folder hygiene for shared storage/FTP workflows (for example Hetzner boxes).
- Exposes scan/suggestion endpoints for admin workflows.

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
```

Snapshot locations:

```text
data/jellyfin/config/data/plugins/Shirarium/dryrun-suggestions.json
data/jellyfin/config/data/plugins/Shirarium/organization-plan.json
data/jellyfin/config/data/plugins/Shirarium/apply-journal.json
```

## API Endpoints

- `POST /shirarium/scan`
- `GET /shirarium/suggestions`
- `POST /shirarium/plan-organize`
- `GET /shirarium/organization-plan`
- `GET /shirarium/organization-plan-view`
- `GET /shirarium/organization-plan-summary`
- `PATCH /shirarium/organization-plan-entry-overrides`
- `POST /shirarium/apply-plan`
- `POST /shirarium/apply-plan-by-filter`
- `POST /shirarium/apply-reviewed-plan`
- `POST /shirarium/undo-apply`
- `GET /shirarium/ops-status`
- `POST /v1/parse-filename` (engine)
- `GET /health` (engine)

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
3. Web UI for reviewed plan override editing and paged browsing.

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
