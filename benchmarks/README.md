# ShirariumBench

Empirical evaluation of Small Language Models (SLMs) for media filename parsing and structured metadata extraction.

## The Mission

Shirarium uses a hybrid parsing architecture. While regex handles 90% of files, the **AI layer** must resolve the "Hard Tail":
- **Temporal Disambiguation**: Distinguishing between titles like `2012 (2009).mkv`.
- **Scene Soup**: Filenames buried in site URLs, hashes, and non-standard tags.
- **Anime Absolute Numbering**: Mapping absolute episode counts to seasonal structures.

## Shortlisted Models

Based on our research, we focus on models between **1B and 4B parameters** that excel at instruction following and JSON extraction.

| Model ID | Name | Rationale |
| :--- | :--- | :--- |
| `qwen2.5-coder-3b` | Qwen 2.5 Coder 3B | **The Syntax Specialist.** Trained on code, it has innate discipline for JSON structures and key-value accuracy. |
| `phi-4-mini` | Phi-4 Mini 3.8B | **The Logical Engine.** Microsoft's SOTA for reasoning. Best at resolving "Year vs Title" logic. |
| `nuextract-2.0` | NuExtract 2.0 2B | **The Deterministic Extractor.** A specialized model from NuMind designed exclusively for Zero-Shot IE. |
| `llama-3.2-3b` | Llama 3.2 3B | **The Generalist.** Robust cultural knowledge helps identify obscure titles that synthetic models might miss. |
| `deepseek-r1-1.5b` | DeepSeek R1 Distill | **The Thinker.** Uses Chain-of-Thought to "reason" through a filename before emitting JSON. |

## How to Run Benchmarks

The benchmark runner is fully automated. It downloads the GGUF weights, spawns a local server, and scores results against our "Golden Standard" datasets.

### Prerequisites
- `curl` installed (for model downloads).
- `llama-server` in your system `PATH`.
- Python `requests` library.

### Commands

Run the default benchmark (top 10 items from Golden Set across all models):
```bash
python scripts/manage.py bench --limit 10
```

Benchmark a specific model:
```bash
python scripts/manage.py bench --model phi-4-mini --limit 50
```

Use a specific dataset:
```bash
python scripts/manage.py bench --dataset datasets/regression/arr-suite-curated.json
```

## Methodology

### Scoring
- **Exact Match (EM)**: The extracted Title, Year, Season, and Episode must exactly match the Ground Truth.
- **Schema Compliance**: The model must output valid JSON without conversational filler.
- **Latency**: Measured in milliseconds per filename.

### Performance Tiers
We evaluate models using the **Q6_K (6-bit)** quantization level. This provides a "near-lossless" accuracy profile compared to FP16, while keeping the memory footprint under 3GB—ideal for NAS and home server environments.

## Reports
Summary reports (Markdown) and detailed results (JSON) are saved to the `benchmarks/reports/` directory after every run.
