import json
import time
import argparse
from pathlib import Path
import sys
from concurrent.futures import ThreadPoolExecutor

# Add app to path
sys.path.append(str(Path(__file__).parent.parent / "app"))
from main import _heuristic_parse

def evaluate_item(entry):
    rel_path = entry.get("relativePath") or entry.get("source_path")
    expected = entry.get("expected") or entry
    
    start_time = time.perf_counter()
    result = _heuristic_parse(rel_path)
    duration = time.perf_counter() - start_time
    
    # Matching Logic
    title_match = True
    if expected.get("title"):
        actual_clean = "".join(filter(str.isalnum, result.title)).lower()
        expected_clean = "".join(filter(str.isalnum, expected.get("title"))).lower()
        title_match = actual_clean == expected_clean

    year_match = expected.get("year") == result.year if expected.get("year") else True
    season_match = expected.get("season") == result.season if expected.get("season") else True
    episode_match = expected.get("episode") == result.episode if expected.get("episode") else True
    type_match = expected.get("mediaType") == result.media_type if expected.get("mediaType") and expected.get("mediaType") != "ignored" else True

    passed = title_match and year_match and season_match and episode_match and type_match
    
    return {
        "passed": passed,
        "duration": duration,
        "type": result.media_type,
        "result": result
    }

def run_benchmark(dataset_path, threads=4, verbose=False):
    with open(dataset_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    entries = data.get("entries", [])
    total = len(entries)
    print(f"Benchmarking Dataset: {data.get('name', 'Unknown')}")
    print(f"Total Entries: {total}")
    print(f"Threads: {threads}")
    print("-" * 60)

    start_bench = time.perf_counter()
    
    with ThreadPoolExecutor(max_workers=threads) as executor:
        results = list(executor.map(evaluate_item, entries))
    
    total_duration = time.perf_counter() - start_bench
    passed_count = sum(1 for r in results if r["passed"])
    accuracy = (passed_count / total) * 100 if total > 0 else 0
    avg_latency = (sum(r["duration"] for r in results) / total) * 1000 if total > 0 else 0
    throughput = total / total_duration if total_duration > 0 else 0

    print(f"Accuracy:   {accuracy:.2f}% ({passed_count}/{total})")
    print(f"Latency:    {avg_latency:.2f}ms (avg per item)")
    print(f"Throughput: {throughput:.2f} items/sec")
    print(f"Total Time: {total_duration:.2f}s")
    print("-" * 60)

    if verbose:
        print("\nTop 20 Failures:")
        fail_count = 0
        for i, r in enumerate(results):
            if not r["passed"]:
                entry = entries[i]
                rel_path = entry.get("relativePath") or entry.get("source_path")
                expected = entry.get("expected") or entry
                print(f"Path: {rel_path}")
                print(f"  Exp: {expected}")
                print(f"  Act: {r['result']}")
                fail_count += 1
                if fail_count >= 20:
                    break

    # Type Breakdown
    types = {}
    for r in results:
        t = r["type"]
        types[t] = types.get(t, 0) + 1
    
    print("Classification Breakdown:")
    for t, count in types.items():
        print(f"  {t:10}: {count}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Shirarium Parser Benchmark")
    parser.add_argument("dataset", help="Path to the JSON dataset file")
    parser.add_argument("--threads", type=int, default=8, help="Number of concurrent parsing threads")
    parser.add_argument("--verbose", action="store_true", help="Output failure details")
    args = parser.parse_args()

    run_benchmark(args.dataset, threads=args.threads, verbose=args.verbose)
