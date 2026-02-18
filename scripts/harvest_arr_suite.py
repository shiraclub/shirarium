import requests
import re
import json
from pathlib import Path

# High-value remote test files from the research report
REMOTE_SOURCES = {
    "radarr": "https://raw.githubusercontent.com/Radarr/Radarr/develop/src/NzbDrone.Core.Test/ParserTests/ParsingServiceFixture.cs",
    "sonarr": "https://raw.githubusercontent.com/Sonarr/Sonarr/develop/src/NzbDrone.Core.Test/ParserTests/ParserTest.cs"
}

def harvest_arr_suite():
    all_entries = []
    print("Harvesting 'Arr' Suite test cases...")

    # Pattern for [TestCase("Filename", "Title", year, ...)]
    # This regex is a simplified heuristic to grab the most common format
    test_case_re = re.compile(r'TestCase\("([^"]+)",\s*"([^"]+)"')

    for name, url in REMOTE_SOURCES.items():
        print(f"Fetching {name} tests from GitHub...")
        try:
            response = requests.get(url, timeout=10)
            response.raise_for_status()
            
            matches = test_case_re.findall(response.text)
            print(f"Found {len(matches)} potential cases in {name}.")
            
            count = 0
            for filename, title in matches:
                # Basic normalization to our format
                all_entries.append({
                    "relativePath": filename,
                    "expected": {
                        "title": title
                    },
                    "tags": ["harvested", name]
                })
                count += 1
                if count >= 500: # Limit per source to keep manifest manageable
                    break
                    
        except Exception as e:
            print(f"Failed to fetch {name}: {e}")

    manifest = {
        "schemaVersion": "1.0",
        "name": "arr-suite-harvested",
        "description": "Automatically harvested test cases from Sonarr/Radarr source code.",
        "entries": all_entries
    }

    output_path = Path("datasets/regression/arr-suite-harvested.json")
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    
    print(f"Successfully harvested {len(all_entries)} entries to {output_path}")

if __name__ == "__main__":
    harvest_arr_suite()
