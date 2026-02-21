#!/usr/bin/env python3
import argparse
import json
import os
import random
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
PLUGIN_ARTIFACTS = REPO_ROOT / "artifacts" / "plugin"
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
    """Call Shirarium API."""
    resp = call_jf_api(f"shirarium/{path}", method=method, body=body, args=args)
    if resp:
        print(json.dumps(resp, indent=2))
    return resp

def call_jf_api(path, method="GET", body=None, args=None, token=None):
    """Call Jellyfin API."""
    url = f"{args.url.rstrip('/')}/{path}"
    
    # Use provided token, or token from args, or nothing
    effective_token = token or (args.token if hasattr(args, "token") else None)
    
    # Jellyfin requires a specific Authorization header for many endpoints
    auth_header = f'MediaBrowser Client="Shirarium-CLI", Device="CLI", DeviceId="shirarium-cli", Version="0.0.14"'
    if effective_token:
        auth_header += f', Token="{effective_token}"'
    
    headers = {
        "X-Emby-Authorization": auth_header,
        "Accept": "application/json"
    }
    
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    else:
        data = None
    
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    
    try:
        with urllib.request.urlopen(req) as f:
            content = f.read().decode("utf-8")
            if not content:
                return {}
            return json.loads(content)
    except urllib.error.HTTPError as e:
        # Silently fail for expected errors during probing
        return None
    except Exception as e:
        return None

def cmd_login(args):
    """Authenticate with Jellyfin and print the token."""
    print(f"Logging into {args.url} as {args.username}...")
    payload = {
        "Username": args.username,
        "Pw": args.password or ""
    }
    
    resp = call_jf_api("Users/AuthenticateByName", method="POST", body=payload, args=args)
    if resp and "AccessToken" in resp:
        token = resp["AccessToken"]
        print(f"\nAuthentication Successful!")
        print(f"Token: {token}")
        
        # Save token locally for convenience
        token_file = REPO_ROOT / ".jf_token"
        token_file.write_text(token)
        print(f"Token saved to {token_file}")
    else:
        print("Authentication failed.")
        sys.exit(1)

def cmd_quick_setup(args):
    """Automate the Jellyfin setup wizard using the default root user."""
    print(f"Waiting for Jellyfin to be ready at {args.url}...")
    
    # Wait for server to be fully started
    max_retries = 60
    server_info = None
    for i in range(max_retries):
        try:
            # Info/Public is always available
            url = f"{args.url.rstrip('/')}/System/Info/Public"
            with urllib.request.urlopen(url) as f:
                server_info = json.loads(f.read().decode("utf-8"))
                if server_info and server_info.get("Version"): 
                    break
        except: pass
        time.sleep(2)
        if i == max_retries - 1:
            print("Error: Server did not become ready in time.")
            sys.exit(1)

    print(f"Server is ready (Version {server_info.get('Version')}). Starting automated setup...")
    
    # Check if a user already exists (Jellyfin 10.11+ often pre-creates 'root')
    existing_user = None
    try:
        url = f"{args.url.rstrip('/')}/Startup/User"
        with urllib.request.urlopen(url) as f:
            existing_user = json.loads(f.read().decode("utf-8"))
            print(f"Found existing startup user: {existing_user.get('Name')}")
    except: pass

    def call_setup(path, body=None):
        url = f"{args.url.rstrip('/')}/{path}"
        headers = {
            "X-Emby-Authorization": 'MediaBrowser Client="Jellyfin Web", Device="CLI", DeviceId="shirarium-cli", Version="10.11.6"',
            "Accept": "application/json",
            "Content-Type": "application/json"
        }
        data = json.dumps(body).encode("utf-8") if body else None
        req = urllib.request.Request(url, data=data, headers=headers, method="POST")
        try:
            with urllib.request.urlopen(req) as f:
                print(f"Setup Step {path} succeeded")
                return True
        except urllib.error.HTTPError as e:
            print(f"Setup Step {path} status: {e.code}")
            return False

    # 1. Set initial configuration
    print("Setting initial configuration...")
    call_setup("Startup/Configuration", {
        "UICulture": "en-US",
        "MetadataCountryCode": "US",
        "PreferredMetadataLanguage": "en"
    })

    # 2. Create or update the admin user
    if existing_user and existing_user.get("Name") == "root":
        print("Root user exists, skipping creation.")
    else:
        print("Creating admin user: root...")
        call_setup("Startup/User", {"Name": "root", "Password": "root"})
    
    # 3. Finalize setup
    print("Finalizing setup...")
    call_setup("Startup/Complete")
    
    print("\nQuick setup complete! Attempting login as root...")
    time.sleep(3)
    
    # Try login with empty password first (standard default for root in this setup)
    print("Attempting login with empty password...")
    payload = {"Username": "root", "Pw": ""}
    resp = call_jf_api("Users/AuthenticateByName", method="POST", body=payload, args=args)
    
    if not resp:
        print("Login with empty password failed, trying 'root'...")
        payload["Pw"] = "root"
        resp = call_jf_api("Users/AuthenticateByName", method="POST", body=payload, args=args)

    if resp and "AccessToken" in resp:
        token = resp["AccessToken"]
        print(f"Authentication Successful! Token: {token}")
        REPO_ROOT.joinpath(".jf_token").write_text(token)
        
        # Ensure password is set to 'root' for consistency
        user_id = resp["User"]["Id"]
        # Try to set password if it was empty
        if not payload["Pw"]:
            print("Setting password to 'root' for consistency...")
            pw_payload = {"CurrentPassword": "", "NewPassword": "root"}
            call_jf_api(f"Users/{user_id}/Password", method="POST", body=pw_payload, args=args, token=token)
    else:
        print("Authentication failed.")
        sys.exit(1)

def get_saved_token():
    """Retrieve the token from the local cache file."""
    token_file = REPO_ROOT / ".jf_token"
    if token_file.exists():
        return token_file.read_text().strip()
    return None

def cmd_setup_libraries(args):
    """Create default libraries in Jellyfin."""
    token = args.token or get_saved_token()
    if not token:
        print("Error: No token provided. Use 'login' first or provide --token.")
        sys.exit(1)

    # Use the retrieved token for subsequent calls
    args.token = token

    libraries = [
        {"Name": "Movies", "CollectionType": "movies", "Paths": ["/media/Movies"]},
        {"Name": "TV Shows", "CollectionType": "tvshows", "Paths": ["/media/TV"]},
        {"Name": "Downloads (Movies)", "CollectionType": "movies", "Paths": ["/media/Downloads"]},
        {"Name": "Downloads (TV)", "CollectionType": "tvshows", "Paths": ["/media/TV-Downloads"]}
    ]

    for lib in libraries:
        print(f"Creating library: {lib['Name']}...")
        import urllib.parse
        name = urllib.parse.quote(lib['Name'])
        c_type = urllib.parse.quote(lib['CollectionType'])
        paths = ",".join([urllib.parse.quote(p) for p in lib['Paths']])
        
        path = f"Library/VirtualFolders?name={name}&collectionType={c_type}&paths={paths}"
        call_jf_api(path, method="POST", args=args)
    
    print("Library setup complete.")

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
    """Seed the media directory with realistic-looking synthetic files."""
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

    # Binary templates for common extensions to satisfy basic magic-byte checks
    EXT_TEMPLATES = {
        ".mkv": b"\x1A\x45\xDF\xA3\x01\x00\x00\x00", # EBML/Matroska
        ".mp4": b"\x00\x00\x00\x18ftypisom\x00\x00\x00\x00isomiso2avc1mp41", # MP4 header
        ".avi": b"RIFF\x00\x00\x00\x00AVI LIST",
        ".mov": b"\x00\x00\x00\x14ftypqt  ",
        ".jpg": b"\xFF\xD8\xFF\xE0\x00\x10JFIF\x00\x01\x01\x01",
        ".png": b"\x89PNG\r\n\x1a\n",
        ".nfo": "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<movie>\n  <title>{title}</title>\n  <year>{year}</year>\n  <video>\n    <resolution>{resolution}</resolution>\n    <codec>{codec}</codec>\n  </video>\n</movie>",
        ".srt": "1\n00:00:01,000 --> 00:00:04,000\nShirarium Test Subtitle\n\n2\n00:00:05,000 --> 00:00:08,000\nSynthetic Media for Development",
    }

    # Try to find or generate golden samples for video files
    sample_mkv = target_root / ".sample.mkv"
    sample_mp4 = target_root / ".sample.mp4"

    container_name = "shirarium-jellyfin-dev"
    container_running = False
    try:
        # Check if container is running
        check_cmd = ["docker", "inspect", "-f", "{{.State.Running}}", container_name]
        result = subprocess.run(check_cmd, capture_output=True, text=True)
        container_running = result.returncode == 0 and result.stdout.strip() == "true"
    except Exception:
        pass

    if (not sample_mkv.exists() or not sample_mp4.exists()) and container_running:
        print("Generating golden samples via container ffmpeg...")
        ffmpeg = "/usr/lib/jellyfin-ffmpeg/ffmpeg"
        # 5 seconds duration, standard 16:9 aspect ratio, libx264
        base_cmd = ["docker", "exec", container_name, ffmpeg, "-f", "lavfi", "-i", "color=c=black:s=640x360:d=5", "-f", "lavfi", "-i", "anullsrc=cl=mono:d=5", "-c:v", "libx264", "-t", "5", "-preset", "ultrafast", "-c:a", "aac", "-shortest"]
        try:
            run_command(base_cmd + ["/media/.sample.mkv", "-y"])
            run_command(base_cmd + ["/media/.sample.mp4", "-y"])
            time.sleep(2) # Ensure files are synced
        except Exception as e:
            print(f"Warning: Failed to generate golden samples: {e}")

    count = 0
    for entry in entries:
        rel_path = entry.get("relativePath")
        if not rel_path: continue
        rel_path = rel_path.replace("\\", "/")
        full_path = target_root / rel_path
        full_path.parent.mkdir(parents=True, exist_ok=True)
        
        if full_path.exists() and not args.force:
            continue

        ext = full_path.suffix.lower()
        template = EXT_TEMPLATES.get(ext)
        expected = entry.get("expected") or {}

        try:
            if ext == ".mkv" and sample_mkv.exists():
                shutil.copy(sample_mkv, full_path)
            elif ext == ".mp4" and sample_mp4.exists():
                shutil.copy(sample_mp4, full_path)
            elif isinstance(template, bytes):
                with open(full_path, "wb") as f:
                    f.write(template)
                    # Add some random padding to make the file look "non-empty" to basic probes
                    f.write(os.urandom(1024))
            else:
                content = template if template else f"Shirarium synthetic file\nSource: {rel_path}"
                if "{title}" in content:
                    content = content.format(
                        title=expected.get("title", "Unknown"), 
                        year=expected.get("year", ""),
                        resolution=expected.get("resolution", "Unknown"),
                        codec=expected.get("codec", "Unknown")
                    )
                with open(full_path, "w", encoding="utf-8") as f:
                    f.write(content)
            count += 1
        except Exception as e:
            print(f"Warning: Failed to seed {rel_path}: {e}")

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
    if not args.token:
        args.token = get_saved_token()
    
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
    elif args.api_command == "test-template":
        body = {
            "Path": args.path,
            "MoviePathTemplate": args.movie_template,
            "EpisodePathTemplate": args.episode_template,
            "RootPath": args.root_path,
            "NormalizePathSegments": not args.no_normalize
        }
        call_api("test-template", method="POST", body=body, args=args)

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

    # setup-libraries
    p_setup_libs = subparsers.add_parser("setup-libraries", help="Create default libraries in Jellyfin")
    p_setup_libs.add_argument("--url", default="http://localhost:8097", help="Jellyfin URL")
    p_setup_libs.add_argument("--token", help="Jellyfin Admin Token (optional if logged in)")
    p_setup_libs.set_defaults(func=cmd_setup_libraries)

    # login
    p_login = subparsers.add_parser("login", help="Authenticate with Jellyfin")
    p_login.add_argument("username", help="Jellyfin Username")
    p_login.add_argument("password", nargs="?", default="", help="Jellyfin Password")
    p_login.add_argument("--url", default="http://localhost:8097", help="Jellyfin URL")
    p_login.set_defaults(func=cmd_login)

    # quick-setup
    p_quick = subparsers.add_parser("quick-setup", help="Automate initial Jellyfin setup")
    p_quick.add_argument("username", nargs="?", default="root", help="Admin Username")
    p_quick.add_argument("password", nargs="?", default="root", help="Admin Password")
    p_quick.add_argument("--url", default="http://localhost:8097", help="Jellyfin URL")
    p_quick.set_defaults(func=cmd_quick_setup)

    # api
    p_api = subparsers.add_parser("api", help="Call Shirarium API")
    p_api.add_argument("--url", default="http://localhost:8097", help="Jellyfin URL (default: dev port 8097)")
    p_api.add_argument("--token", help="API Access Token (optional if logged in)")
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

    p_test_tmp = api_subs.add_parser("test-template", help="Test a path template")
    p_test_tmp.add_argument("--path", required=True, help="Sample path to test")
    p_test_tmp.add_argument("--movie-template", help="Movie path template")
    p_test_tmp.add_argument("--episode-template", help="Episode path template")
    p_test_tmp.add_argument("--root-path", help="Root organization path")
    p_test_tmp.add_argument("--no-normalize", action="store_true", help="Disable segment normalization")

    p_api.set_defaults(func=cmd_api)

    args = parser.parse_args()
    args.func(args)

if __name__ == "__main__":
    main()
