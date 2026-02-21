# Shirarium

<img src="thumb.png" width="396" height="216" alt="Shirarium Logo" />

Clean up your Jellyfin library.

Shirarium turns messy file and folder names into a pristine, Jellyfin-friendly structure using advanced heuristics and optional AI-assisted parsing. 

![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.6-00A4DC?logo=jellyfin&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![Native C#](https://img.shields.io/badge/Architecture-Native%20C%23-28a745?logo=csharp&logoColor=white)
[![CI](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml?query=branch%3Amaster)
![Safety](https://img.shields.io/badge/safety-dry--run%20by%20default-orange)

## Missions

Shirarium has two core missions:

1.  **Tidy Up (Filesystem Organizer):** Recursively scans your media folders directly (bypassing Jellyfin's DB) to find every file, even those Jellyfin missed. It proposes a clean, standardized folder structure (e.g., `Movies/Title (Year) [Resolution]/Title.mkv`) based on filename parsing.
2.  **Identify "Unjellied" Content:** Cross-references the filesystem against Jellyfin's database to flag items that are **Unrecognized** (missing from Jellyfin) or **Missing Metadata** (recognized but unidentified).

## Features

- **Filesystem-First Scanning**: Finds files Jellyfin ignores.
- **Hybrid Intelligence**: Uses Jellyfin's accurate metadata when available (Probe data), falls back to advanced filename heuristics/AI when not.
- **Native Speed**: Zero-latency C# regex engine for initial parsing.
- **Batteries Included**: Managed local AI (Qwen 2.5 3B via llama-server) automatically downloaded and run for difficult filenames.
- **Review First**: Generates a detailed organization plan. You approve changes before any file moves.
- **Safety Net**: Complete Undo/Rollback support.

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

## Quick Start (Dev)

### Prerequisites

- Docker (Engine or Desktop) with Compose v2 (for testing/smoke tests only)
- .NET SDK 9.0+
- Python 3.10+

### Setup

1. Copy env file:

```bash
cp .env.example .env
```

2. Start Jellyfin dev instance:

```bash
python scripts/manage.py up
```

3. Seed test media (Optional but Recommended):

   **Option A: Clean Dataset (Good for basic testing)**
   ```bash
   python scripts/manage.py seed --dataset datasets/regression/tier-b-synthetic.json --clean
   ```

   **Option B: Dirty "Chaos" Dataset (Realistic)**
   ```bash
   # First generate the dataset (requires python)
   python scripts/harvest_synthetic_dataset.py
   
   # Then seed it
   python scripts/manage.py seed --dataset datasets/regression/tier-b-synthetic-dirty.json --clean
   ```

4. Build and reload plugin:

```bash
python scripts/manage.py reload
```

5. Open Jellyfin: `http://localhost:8097`

### Dev vs Prod Stacks

The local environment supports two distinct profiles managed via `docker-compose.yml`:

- **Jellyfin Dev (Port 8097)**: Designed for rapid iteration. It maps the `./artifacts/plugin` directory directly into the container. Run `python scripts/manage.py reload` to rebuild the C# code and restart this container to see changes immediately.
- **Jellyfin Prod (Port 8098)**: Simulates a clean production environment. It does not map local build artifacts, allowing you to test the official installation and update flows via the plugin repository.

Both stacks share the same `./data/media` directory but maintain separate configuration and database folders under `./data/jellyfin` and `./data/jellyfin-prod` respectively.

## Architecture

- **Core**: `src/Jellyfin.Plugin.Shirarium` - A native .NET plugin.
- **Scanning**: Hybrid `FilesystemCandidateProvider` + Jellyfin DB Cross-reference.
- **AI**: Managed `llama-server` process (auto-downloaded) running Qwen 2.5 3B Instruct.

## Testing

Run the full suite (Unit + Integration):

```bash
python scripts/manage.py test --integration
```

Or manually:

```bash
dotnet test tests/Jellyfin.Plugin.Shirarium.Tests/Jellyfin.Plugin.Shirarium.Tests.csproj -c Release
```

## Configuration

Go to **Dashboard -> Plugins -> Shirarium** to configure:

- **AI Parsing**: Enable/Disable managed local inference.
- **Path Templates**: Customize how movies and episodes are renamed.

## Safety

- No automatic file move/rename happens in the current pipeline.
- Apply operations require explicit source-path selection.
- Apply is guarded by plan fingerprint matching.
- **Undo** is fully supported via the "History" tab or API.

## Roadmap

1. Optional queueing model for very large libraries.
2. Throughput benchmarking for large remote libraries.
3. Bulk override presets and saved filter sets.

Contributing guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)
