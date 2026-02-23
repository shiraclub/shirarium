# Shirarium

<img src="thumb.png" width="396" height="216" alt="Shirarium Logo" />

Shirarium organizes file and folder names into pristine, Jellyfin-friendly structures using advanced heuristics and AI-assisted parsing. 

![License: GPL-3.0](https://img.shields.io/badge/license-GPL--3.0-blue.svg)
![Jellyfin](https://img.shields.io/badge/Jellyfin-10.11.6-00A4DC?logo=jellyfin&logoColor=white)
![Native C#](https://img.shields.io/badge/Architecture-Native%20C%23-28a745?logo=csharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-9-512BD4?logo=dotnet&logoColor=white)
[![CI](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/shiraclub/shirarium/actions/workflows/ci.yml?query=branch%3Amaster)

## Missions

Shirarium has two core missions:

1.  **Tidy Up (Filesystem Organizer):** Recursively scans your media folders directly (bypassing Jellyfin's DB) to find every file, even those Jellyfin missed. It proposes a clean, standardized folder structure (e.g., `Movies/Title (Year)/Title (Year) [Resolution].mkv`) based on filename parsing. Templates are fully customizable.
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

## Configuration

Go to **Dashboard -> Plugins -> Shirarium** to configure:

- **AI Parsing**: Enable/Disable managed local inference.
- **Path Templates**: Customize how movies and episodes are renamed.

## Safety

- No automatic file move/rename happens in the current pipeline.
- Apply operations require explicit source-path selection.
- Apply is guarded by plan fingerprint matching.
- **Undo** is fully supported via the "History" tab or API.

## Benchmarks
We evaluate Small Language Models (SLMs) for media filename parsing using **ShirariumBench**. Results below are from the `tier-b-synthetic.json` dataset (200 items).

**Hardware**: `RTX 5070` | `AMD64 Family 25` | `Windows 11`

| Model | Accuracy | Latency | Params |
| :--- | :--- | :--- | :--- |
| **Gemma 3 4B IT** | **76.3%** | 2751ms | 4B |
| Qwen 3 4B Thinking | 75.8% | 2580ms | 4B |
| IBM Granite 3.3 2B Instruct | 75.8% | 2767ms | 2.5B |
| Ministral 3 3B Instruct 2512 | 75.4% | 2447ms | 3B |
| Qwen 3 4B Instruct | 75.3% | 2378ms | 4B |
| Qwen 2.5 Coder 3B Instruct | 72.9% | 2406ms | 3B |
| Llama 3.2 3B Instruct | 71.9% | 2266ms | 3B |
| Phi-4 Mini Instruct | 69.3% | 4047ms | 3.8B |
| SmolLM3 3B Instruct | 68.6% | 2464ms | 3B |

Detailed reports and more models are available in the [`shirariumbench/`](shirariumbench/) directory.

## Roadmap

1. Optional queueing model for very large libraries.
2. Throughput benchmarking for large remote libraries.
3. Bulk override presets and saved filter sets.

Contributing guide: [`CONTRIBUTING.md`](CONTRIBUTING.md)
