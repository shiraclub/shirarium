#!/usr/bin/env python3
import argparse
import json
import os
import shutil
import subprocess
import sys
import time
import urllib.request
import urllib.error
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
DATA_DIR = REPO_ROOT / "data"
MEDIA_DIR = DATA_DIR / "media"
PLUGIN_SRC = REPO_ROOT / "src" / "Jellyfin.Plugin.Shirarium"
PLUGIN_ARTIFACTS = REPO_ROOT / "artifacts" / "plugin" / "Shirarium"
BENCHMARK_DIR = REPO_ROOT / "datasets" / "benchmark"

def run_command(cmd, cwd=REPO_ROOT, env=None):
    """Run a shell command."""
    print(f"Executing: {' '.join(cmd)}")
    try:
        subprocess.check_call(cmd, cwd=cwd, env=env)
    except subprocess.CalledProcessError as e:
        print(f"Error: command failed with exit code {e.returncode}")
        sys.exit(e.returncode)

def call_api(path, method="GET", body=None, args=None):
    """Call Jellyfin/Shirarium API."""
    url = f"{args.url.rstrip('/')}/shirarium/{path}"
    headers = {"X-Emby-Token": args.token} if args.token else {}
    data = json.dumps(body).encode("utf-8") if body else None
    
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    if body:
        req.add_header("Content-Type", "application/json")
    
    try:
        with urllib.request.urlopen(req) as f:
            resp = json.loads(f.read().decode("utf-8"))
            print(json.dumps(resp, indent=2))
            return resp
    except urllib.error.HTTPError as e:
        print(f"API Error ({e.code}): {e.read().decode('utf-8')}")
        sys.exit(1)
    except Exception as e:
        print(f"Failed to connect: {e}")
        sys.exit(1)

def cmd_up(args):
    """Start the dev environment."""
    compose_file = REPO_ROOT / "docker-compose.yml"
    cmd = ["docker", "compose", "-f", str(compose_file)]
    if args.prod:
        cmd.extend(["--profile", "prod"])
    cmd.extend(["up", "-d"])
    if args.build:
        cmd.append("--build")
    run_command(cmd)
    
    print("\nStack is running.")
    print("Jellyfin Dev:  http://localhost:8097")
    print("Jellyfin Prod: http://localhost:8098")

def cmd_reload(args):
    """Build the plugin and restart the dev container."""
    print("Building plugin...")
    cmd_build = [
        "dotnet", "build", 
        str(PLUGIN_SRC / "Jellyfin.Plugin.Shirarium.csproj"), 
        "-c", "Debug", 
        "-o", str(PLUGIN_ARTIFACTS)
    ]
    run_command(cmd_build)

    target = "jellyfin-prod" if args.prod else "jellyfin-dev"
    print(f"Restarting {target}...")
    cmd_restart = ["docker", "compose", "restart", target]
    run_command(cmd_restart)
    print("Plugin reloaded.")

def cmd_test(args):
    """Run the test suite."""
    if args.system:
        cmd = ["dotnet", "test", str(REPO_ROOT / "tests" / "Jellyfin.Plugin.Shirarium.SystemTests" / "Jellyfin.Plugin.Shirarium.SystemTests.csproj"), "-c", "Release"]
    elif args.integration:
        cmd = ["dotnet", "test", str(REPO_ROOT / "tests" / "Jellyfin.Plugin.Shirarium.IntegrationTests" / "Jellyfin.Plugin.Shirarium.IntegrationTests.csproj"), "-c", "Release"]
    else:
        cmd = ["dotnet", "test", str(REPO_ROOT / "tests" / "Jellyfin.Plugin.Shirarium.Tests" / "Jellyfin.Plugin.Shirarium.Tests.csproj"), "-c", "Release"]
    run_command(cmd)

def cmd_seed(args):
    """Seed the media directory with synthetic files."""
    dataset_path = Path(args.dataset)
    if not dataset_path.exists():
        dataset_path = REPO_ROOT / args.dataset
        if not dataset_path.exists():
            print(f"Error: Dataset not found: {args.dataset}")
            sys.exit(1)

    print(f"Seeding media from {dataset_path}...")
    with open(dataset_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    entries = manifest.get("entries", [])
    target_root = MEDIA_DIR
    target_root.mkdir(parents=True, exist_ok=True)

    if args.clean:
        print("Cleaning existing media...")
        for item in target_root.iterdir():
            if item.name == ".gitkeep": continue
            if item.is_dir(): shutil.rmtree(item)
            else: item.unlink()

    count = 0
    for entry in entries:
        rel_path = entry.get("relativePath")
        if not rel_path: continue
        rel_path = rel_path.replace("\\", "/")
        full_path = target_root / rel_path
        full_path.parent.mkdir(parents=True, exist_ok=True)
        if not full_path.exists() or args.force:
            with open(full_path, "w", encoding="utf-8") as f:
                f.write(f"Shirarium synthetic file\nSource: {rel_path}\nManifest: {manifest.get('name')}")
            count += 1
    print(f"Seeded {count} files to {target_root}")

def cmd_benchmark_setup(args):
    """Download large datasets for benchmarking."""
    BENCHMARK_DIR.mkdir(parents=True, exist_ok=True)
    datasets = [
        {
            "name": "tpb-titles-2023",
            "url": "https://huggingface.co/datasets/d2mw/thepiratebay-categorized-titles-2023-04/resolve/main/titles-cats-only-2023-04-01.csv?download=true",
            "file": "tpb-titles-2023.csv"
        },
        {
            "name": "magnetdb-lite-sample",
            "url": "https://zenodo.org/record/10688434/files/matched_videos_sample.csv?download=1",
            "file": "magnetdb-matched-sample.csv"
        }
    ]
    for ds in datasets:
        target = BENCHMARK_DIR / ds["file"]
        if target.exists() and not args.force:
            print(f"Skipping {ds['name']} (exists)")
            continue
        print(f"Downloading {ds['name']}...")
        urllib.request.urlretrieve(ds["url"], target)
    print("Benchmark data setup complete.")

def cmd_clean(args):
    """Wipe dev/prod data volumes."""
    target_dir = DATA_DIR / ("jellyfin-prod" if args.prod else "jellyfin")
    print(f"Wiping {target_dir}...")
    if target_dir.exists():
        run_command(["docker", "compose", "down"])
        shutil.rmtree(target_dir)
        print("Data wiped.")
    else:
        print("Directory does not exist, nothing to wipe.")

def cmd_api(args):
    """Execute API commands."""
    if args.api_command == "scan":
        call_api("scan", method="POST", args=args)
    elif args.api_command == "plan":
        call_api("plan-organize", method="POST", args=args)
    elif args.api_command == "status":
        call_api("ops-status", args=args)
    elif args.api_command == "suggestions":
        call_api("suggestions", args=args)
    elif args.api_command == "summary":
        call_api("organization-plan-summary", args=args)
    elif args.api_command == "history":
        call_api("organization-plan-history", args=args)
    elif args.api_command == "locks":
        call_api("review-locks", args=args)
    elif args.api_command == "apply":
        if not args.fingerprint:
            print("Error: --fingerprint is required for apply")
            sys.exit(1)
        body = {
            "expectedPlanFingerprint": args.fingerprint,
            "sourcePaths": args.paths if args.paths else []
        }
        call_api("apply-plan", method="POST", body=body, args=args)
    elif args.api_command == "undo":
        body = {"runId": args.run_id} if args.run_id else {}
        call_api("undo-apply", method="POST", body=body, args=args)

def main():
    parser = argparse.ArgumentParser(description="Shirarium Developer CLI")
    subparsers = parser.add_subparsers(dest="command", required=True)

    # up
    p_up = subparsers.add_parser("up", help="Start the stack")
    p_up.add_argument("--prod", action="store_true", help="Use production profile")
    p_up.add_argument("--build", action="store_true", help="Rebuild containers")
    p_up.set_defaults(func=cmd_up)

    # reload
    p_reload = subparsers.add_parser("reload", help="Build plugin and restart container")
    p_reload.add_argument("--prod", action="store_true", help="Restart production container")
    p_reload.set_defaults(func=cmd_reload)

    # test
    p_test = subparsers.add_parser("test", help="Run tests")
    p_test.add_argument("--integration", action="store_true", help="Run integration tests")
    p_test.add_argument("--system", action="store_true", help="Run system E2E tests")
    p_test.set_defaults(func=cmd_test)

    # seed
    p_seed = subparsers.add_parser("seed", help="Seed media data")
    p_seed.add_argument("--dataset", default="datasets/regression/tier-b-synthetic.json", help="Path to JSON dataset")
    p_seed.add_argument("--clean", action="store_true", help="Clean media dir before seeding")
    p_seed.add_argument("--force", action="store_true", help="Overwrite existing files")
    p_seed.set_defaults(func=cmd_seed)

    # benchmark-setup
    p_bench = subparsers.add_parser("benchmark-setup", help="Download benchmark datasets")
    p_bench.add_argument("--force", action="store_true", help="Force re-download")
    p_bench.set_defaults(func=cmd_benchmark_setup)

    # clean
    p_clean = subparsers.add_parser("clean", help="Wipe Jellyfin data volumes")
    p_clean.add_argument("--prod", action="store_true", help="Wipe production data")
    p_clean.set_defaults(func=cmd_clean)

    # api
    p_api = subparsers.add_parser("api", help="Call Shirarium API")
    p_api.add_argument("--url", default="http://localhost:8097", help="Jellyfin URL (default: dev port 8097)")
    p_api.add_argument("--token", help="API Access Token (Admin)")
    api_subs = p_api.add_subparsers(dest="api_command", required=True)
    
    api_subs.add_parser("scan", help="Trigger dry-run scan")
    api_subs.add_parser("plan", help="Generate organization plan")
    api_subs.add_parser("status", help="Get operational status")
    api_subs.add_parser("suggestions", help="Get latest scan suggestions")
    api_subs.add_parser("summary", help="Get organization plan summary")
    api_subs.add_parser("history", help="Get plan history")
    api_subs.add_parser("locks", help="List review locks")
    
    p_apply = api_subs.add_parser("apply", help="Apply organization plan")
    p_apply.add_argument("--fingerprint", required=True, help="Expected plan fingerprint")
    p_apply.add_argument("--paths", nargs="+", help="Specific source paths to apply (optional)")
    
    p_undo = api_subs.add_parser("undo", help="Undo last apply run")
    p_undo.add_argument("--run-id", help="Specific run ID to undo (optional)")

    p_api.set_defaults(func=cmd_api)

    args = parser.parse_args()
    args.func(args)

if __name__ == "__main__":
    main()
