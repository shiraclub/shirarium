import json
import hashlib
import sys
from pathlib import Path

def get_checksum(file_path):
    md5_hash = hashlib.md5()
    with open(file_path, "rb") as f:
        for byte_block in iter(lambda: f.read(4096), b""):
            md5_hash.update(byte_block)
    return md5_hash.hexdigest()

def update_manifest(version, zip_path):
    manifest_path = Path("manifest.json")
    if not manifest_path.exists():
        print("Error: manifest.json not found.")
        return

    with open(manifest_path, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    # Calculate checksum for the zip
    checksum = get_checksum(zip_path)
    
    new_version = {
        "version": version.replace("v", ""),
        "changelog": f"Release {version}",
        "targetAbi": "10.11.6.0",
        "sourceUrl": f"https://github.com/shiraclub/shirarium/releases/download/{version}/shirarium-{version}.zip",
        "checksum": checksum
    }

    # Add to Shirarium entry
    for plugin in manifest:
        if plugin["name"] == "Shirarium":
            # Avoid duplicates
            plugin["versions"] = [v for v in plugin["versions"] if v["version"] != new_version["version"]]
            plugin["versions"].insert(0, new_version)
            break

    with open(manifest_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    
    print(f"Successfully updated manifest.json with version {version}")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python update-manifest.py <version> <zip_path>")
    else:
        update_manifest(sys.argv[1], sys.argv[2])
