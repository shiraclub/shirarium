# Shirarium

<img src="thumb.png" width="120" height="120" alt="Shirarium Logo" />

Clean up your Jellyfin library.

Shirarium turns messy file and folder names into a pristine, Jellyfin-friendly structure using advanced heuristics and optional AI-assisted parsing. 

![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.6-00A4DC?logo=jellyfin&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
![Native C#](https://img.shields.io/badge/Architecture-Native%20C%23-28a745?logo=csharp&logoColor=white)
[![CI](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml?query=branch%3Amaster)
![Safety](https://img.shields.io/badge/safety-dry--run%20by%20default-orange)

## Features

- **Native Speed**: Zero-latency heuristic parsing engine (ported from Python to pure C#).
- **Batteries Included**: No external Docker containers required. Just install the plugin.
- **Managed AI**: Can optionally download and manage a local LLM (Qwen 3 4B via llama-server) for 100% offline intelligence.
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

- Docker Desktop (for testing/smoke tests only)
- .NET SDK 9.0+
- PowerShell 7+

### Setup

1. Copy env file:

```powershell
Copy-Item .env.example .env
```

2. Start Jellyfin dev instance:

```powershell
.\scripts\dev-up.ps1
```

3. Build and reload plugin:

```powershell
.\scripts\dev-reload.ps1
```

4. Open Jellyfin: `http://localhost:8097`

## Architecture

- **Core**: `src/Jellyfin.Plugin.Shirarium` - A native .NET plugin.
- **Heuristics**: In-process high-performance Regex engine.
- **AI**: Managed `llama-server` process (auto-downloaded) or connection to external Ollama.

## Testing

Run the full suite (Unit + Integration):

```powershell
.\scripts\test-plugin.ps1
```

Or manually:

```powershell
dotnet test tests/Jellyfin.Plugin.Shirarium.Tests/Jellyfin.Plugin.Shirarium.Tests.csproj -c Release
```

## Configuration

Go to **Dashboard -> Plugins -> Shirarium** to configure:

- **AI Parsing**: Enable/Disable managed local inference or set an external Ollama URL.
- **Conflict Policy**: Choose how to handle filename collisions (`fail`, `skip`, `suffix`).
- **Path Templates**: Customize how movies and episodes are renamed.

## Safety

- No automatic file move/rename happens in the current pipeline.
- Apply operations require explicit source-path selection.
- Apply is guarded by plan fingerprint matching to ensure operations run only against the exact reviewed plan.
- **Undo** is fully supported via the "History" tab or API.

## Roadmap

1. Optional queueing model for very large libraries.
2. Throughput benchmarking for large remote libraries.
3. Bulk override presets and saved filter sets.

Contributing guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)
