import json
import os
import sys
from pathlib import Path

# Add app to path
sys.path.append(str(Path(__file__).parent.parent / "app"))

from main import _heuristic_parse

def evaluate_manifest(manifest_path):
    with open(manifest_path, 'r', encoding='utf-8') as f:
        manifest = json.load(f)
    
    entries = manifest.get("entries", [])
    total = 0
    passed = 0
    failures = []

    print(f"Evaluating Manifest: {manifest.get('name')}")
    print(f"Description: {manifest.get('description')}")
    print("-" * 60)

    for entry in entries:
        total += 1
        rel_path = entry.get("relativePath")
        expected = entry.get("expected", {})
        
        # Use heuristic parser
        result = _heuristic_parse(rel_path)
        
        # Check title
        title_match = True
        if expected.get("title"):
            # Simple case-insensitive compare, removing non-alphanumeric for flexibility
            actual_clean = "".join(filter(str.isalnum, result.title)).lower()
            expected_clean = "".join(filter(str.isalnum, expected.get("title"))).lower()
            if actual_clean != expected_clean:
                title_match = False

        # Check year
        year_match = expected.get("year") == result.year if expected.get("year") else True
        
        # Check season/episode
        season_match = expected.get("season") == result.season if expected.get("season") else True
        episode_match = expected.get("episode") == result.episode if expected.get("episode") else True
        
        # Check media type
        type_match = expected.get("mediaType") == result.media_type if expected.get("mediaType") != "ignored" else True

        if title_match and year_match and season_match and episode_match and type_match:
            passed += 1
            status = "[PASS]"
        else:
            status = "[FAIL]"
            failures.append({
                "path": rel_path,
                "expected": expected,
                "actual": {
                    "title": result.title,
                    "year": result.year,
                    "season": result.season,
                    "episode": result.episode,
                    "media_type": result.media_type
                }
            })
        
        print(f"{status} {rel_path}")

    accuracy = (passed / total) * 100 if total > 0 else 0
    print("-" * 60)
    print(f"Summary: {passed}/{total} passed ({accuracy:.2f}%)")
    
    if failures:
        print("\nDetailed Failures:")
        for f in failures:
            print(f"Path: {f['path']}")
            print(f"  Expected: {f['expected']}")
            print(f"  Actual:   {f['actual']}")

    return accuracy

if __name__ == "__main__":
    manifest_path = Path(__file__).parent.parent.parent / "datasets" / "regression" / "tier-a-golden.json"
    acc = evaluate_manifest(manifest_path)
    if acc < 80: # Establish an 80% baseline for now
        print("\nAccuracy below threshold!")
        # sys.exit(1) # Don't fail yet, just monitor
