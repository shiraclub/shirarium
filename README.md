# Shirarium

Local-first Jellyfin metadata and file-organization planner for chaotic libraries.

![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.10.3-00A4DC?logo=jellyfin&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)
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
- .NET SDK 8.0+
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

### View latest snapshots

```powershell
.\scripts\show-suggestions.ps1
.\scripts\show-organization-plan.ps1
.\scripts\show-apply-journal.ps1
```

Snapshot locations:

```text
data/jellyfin/config/data/plugins/Shirarium/dryrun-suggestions.json
data/jellyfin/config/data/plugins/Shirarium/organization-plan.json
data/jellyfin/config/data/plugins/Shirarium/apply-journal.json
```

## API Endpoints

- `POST /Shirarium/scan`
- `GET /Shirarium/suggestions`
- `POST /Shirarium/plan-organize`
- `GET /Shirarium/organization-plan`
- `POST /Shirarium/apply-plan`
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

Current test coverage:
- Heuristic filename parsing behavior.
- API validation/contract behavior.
- Ollama failure fallback behavior.
- Plugin scan logic (candidate reasons, extension support, confidence gating).
- Organization planning logic (path normalization, movie/episode path conventions, conflict/no-op handling, duplicate target detection).
- Organization apply logic (selected-entry gating, preflight safety checks, move execution, skipped/failed reason handling).

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

## Roadmap (Near-Term)

1. Approval-based apply mode for selected file moves/renames.
2. Rollback journal for any future apply operations.
3. Better scan observability (counts by reason/source/confidence bucket).
4. Optional queueing model for very large libraries.

## Safety

- No automatic file move/rename happens in the current pipeline.
- Apply operations require explicit source-path selection and only execute entries currently marked `move`.
- Apply preflight blocks unsafe operations: invalid paths, target outside root, cross-volume moves, and existing targets.
- Suggestions and organization plans are persisted for review and auditing before any future apply phase.
- Apply runs are written to `apply-journal.json` for auditability.
- Core repo license: `GPL-3.0`.
