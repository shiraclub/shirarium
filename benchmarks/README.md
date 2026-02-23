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
| `gemma-3-4b-it` | Gemma 3 4B IT | **The Multimodal Champion.** Google's latest SLM with best-in-class reasoning and native vision support. |
| `qwen3-4b-thinking` | Qwen 3 4B Thinking | **The Logic Specialist.** Uses internal reasoning steps to resolve ambiguous titles and absolute numbering. |
| `ministral-3-3b` | Ministral 3 3B | **The Syntax Master.** Mistral's edge-optimized model with exceptional JSON schema adherence. |
| `granite-3.3-2b` | IBM Granite 3.3 2B | **The Efficiency King.** Extremely low latency with surprisingly high accuracy for its size. |
| `phi-4-mini` | Phi-4 Mini 3.8B | **The Rationalist.** Microsoft's SOTA for reasoning and logical extraction. |

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

Evaluated on 200 items from the **Tier B Synthetic** dataset using an NVIDIA RTX 5070 (ngl=99, flash-attn=on, seed=42).

| Model | Accuracy | Latency | Params | Quant |
| :--- | :--- | :--- | :--- | :--- |
| Gemma 3 4B IT | **76.3%** | 2751ms | 4B | Q6_K |
| Qwen 3 4B Thinking | 75.8% | 2580ms | 4B | Q6_K |
| IBM Granite 3.3 2B Instruct | 75.8% | 2767ms | 2.5B | IQ4_XS |
| Ministral 3 3B Instruct 2512 | 75.4% | 2447ms | 3B | Q6_K_L |
| Qwen 3 4B Instruct | 75.3% | 2378ms | 4B | Q6_K |
| Qwen 2.5 Coder 3B Instruct | 72.9% | 2406ms | 3B | Q6_K |
| Llama 3.2 3B Instruct | 71.9% | 2266ms | 3B | Q6_K |
| Phi-4 Mini Instruct | 69.3% | 4047ms | 3.8B | IQ4_XS |
| SmolLM3 3B Instruct | 68.6% | 2464ms | 3B | Q6_K |

### Analysis
- **Gemma 3 4B IT** is the new accuracy leader, showing superior reasoning on messy filenames while remaining performant.
- **Granite 3.3 2B** remains the best "per-parameter" performer, maintaining high accuracy with a significantly smaller footprint.
- **Thinking Models**: Qwen 3 Thinking provides deep reasoning for the "Hard Tail" but the instruct variant is faster for general parsing.

## How to Run Benchmarks
