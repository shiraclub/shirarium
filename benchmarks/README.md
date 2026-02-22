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

## SOTA Comparison (GPU Accelerated)

Evaluated on 100 items from the **Tier B Synthetic** dataset using an NVIDIA RTX 5070 (ngl=99, flash-attn=on).

| Model | Accuracy | Latency | Params | Quant |
| :--- | :--- | :--- | :--- | :--- |
| IBM Granite 3.3 2B Instruct | 77.0% | 2464ms | 2.5B | IQ4_XS |
| Qwen 3 4B Thinking 2507 | 76.0% | 3683ms | 4B | Q6_K |
| Qwen 2.5 Coder 3B Instruct | 72.4% | 2683ms | 3B | Q6_K |
| Phi-4 Mini Instruct | 70.8% | 3991ms | 3.8B | IQ4_XS |
| Qwen 2.5 Coder 1.5B | 66.0% | 2317ms | 1.5B | Q6_K |
| DeepSeek R1 Distill Qwen 1.5B | 56.2% | 2653ms | 1.5B | Q6_K |
| NuExtract 1.5 Smol (1.7B) | 48.0% | 7784ms | 1.7B | IQ4_XS |

### Analysis
- **Granite 3.3 2B** is the current price/performance champion, beating larger models in both accuracy and speed.
- **Thinking Models**: While `Qwen 3 Thinking` is accurate, its latency is higher due to internal reasoning steps.
- **Coder Models**: Still highly reliable for syntax, but enterprise-tuned models (Granite) are catching up.

## How to Run Benchmarks
