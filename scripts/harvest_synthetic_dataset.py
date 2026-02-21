import json
import random
from pathlib import Path
import re

# Metadata Pools
TITLES = [
    "The Matrix", "Interstellar", "Inception", "Arrival", "Blade Runner 2049",
    "Spirited Away", "Parasite", "Godzilla Minus One", "Alien", "The Thing",
    "Breaking Bad", "The Office", "Better Call Saul", "Succession", "Dark",
    "One Piece", "Attack on Titan", "Demon Slayer", "Jujutsu Kaisen", "Les Misérables"
]

# "Evil" Titles designed to trap heuristics
EVIL_TITLES = [
    "1917", "2012", "300", "The 100", "District 9", "Seven", "Ocean's 11",
    "Star Wars Episode I", "Fantastic 4", "Project X", "Movie 43"
]

YEARS = [1979, 1982, 1999, 2001, 2010, 2014, 2017, 2019, 2021, 2023, 2024]
QUALITIES = ["1080p", "720p", "2160p", "4K", "UHD", "DVDRip", "BRRip"]
SOURCES = ["BluRay", "WEB-DL", "WEBRip", "HDTV", "HDR", "DoVi"]
CODECS = ["x264", "x265", "h264", "HEVC", "AV1"]
GROUPS = ["SPARKS", "DON", "AMIABLE", "FRAme", "Erai-raws", "SubsPlease", "YTS", "RARBG"]

def get_clutter(base_name, parent_folder=""):
    clutter = []
    # 50% chance of NFO
    if random.random() > 0.5:
        clutter.append(f"{parent_folder}{base_name}.nfo")
    
    # 20% chance of Sample
    if random.random() > 0.8:
        clutter.append(f"{parent_folder}Sample/{base_name}-sample.mkv")
        
    # 10% chance of Proof
    if random.random() > 0.9:
        clutter.append(f"{parent_folder}Proof/{base_name}-proof.jpg")
    
    # 30% chance of random txt/url
    if random.random() > 0.7:
        clutter.append(f"{parent_folder}RARBG.txt")
        
    return clutter

def generate_movie(title, year):
    quality = random.choice(QUALITIES)
    source = random.choice(SOURCES)
    codec = random.choice(CODECS)
    group = random.choice(GROUPS)
    
    base_name = f"{title.replace(' ', '.')}.{year}.{quality}.{source}.{codec}-{group}"
    
    # Templates
    # 1. Flat file
    # 2. Scene Folder
    # 3. Clean Folder
    
    strategy = random.choice(["flat", "scene", "clean"])
    entries = []
    
    if strategy == "flat":
        rel_path = f"Downloads/{base_name}.mkv"
        entries.append({
            "relativePath": rel_path,
            "expected": { 
                "mediaType": "movie", 
                "title": title, 
                "year": year,
                "resolution": quality,
                "source": source,
                "codec": codec,
                "group": group
            }
        })
    elif strategy == "scene":
        folder = f"Downloads/{base_name}/"
        rel_path = f"{folder}{base_name}.mkv"
        entries.append({
            "relativePath": rel_path,
            "expected": { 
                "mediaType": "movie", 
                "title": title, 
                "year": year,
                "resolution": quality,
                "source": source,
                "codec": codec,
                "group": group
            }
        })
        for c in get_clutter(base_name, folder):
            entries.append({ "relativePath": c, "expected": None }) # Clutter has no expected metadata
            
    elif strategy == "clean":
        folder = f"Movies/{title} ({year})/"
        rel_path = f"{folder}{title} ({year}) [{quality}].mkv"
        entries.append({
            "relativePath": rel_path,
            "expected": { 
                "mediaType": "movie", 
                "title": title, 
                "year": year,
                "resolution": quality,
                "source": source,
                "codec": codec,
                "group": group
            }
        })
        # Even clean folders have clutter sometimes
        if random.random() > 0.8:
             entries.append({ "relativePath": f"{folder}fanart.jpg", "expected": None })

    return entries

def generate_episode(title, season, episode):
    quality = random.choice(QUALITIES)
    group = random.choice(GROUPS)
    codec = random.choice(CODECS)
    
    base_name = f"{title.replace(' ', '.')}.S{season:02d}E{episode:02d}.{quality}.{codec}-{group}"
    
    strategy = random.choice(["flat", "scene", "organized"])
    entries = []
    
    if strategy == "flat":
        rel_path = f"TV-Downloads/{base_name}.mkv"
        entries.append({
            "relativePath": rel_path,
            "expected": { 
                "mediaType": "episode", 
                "title": title, 
                "season": season, 
                "episode": episode,
                "resolution": quality,
                "codec": codec,
                "group": group
            }
        })
    elif strategy == "scene":
        folder = f"TV-Downloads/{base_name}/"
        rel_path = f"{folder}{base_name}.mkv"
        entries.append({
            "relativePath": rel_path,
            "expected": { 
                "mediaType": "episode", 
                "title": title, 
                "season": season, 
                "episode": episode,
                "resolution": quality,
                "codec": codec,
                "group": group
            }
        })
        for c in get_clutter(base_name, folder):
            entries.append({ "relativePath": c, "expected": None })
            
    elif strategy == "organized":
        folder = f"TV/{title}/Season {season}/"
        rel_path = f"{folder}{title} - S{season:02d}E{episode:02d}.mkv"
        entries.append({
            "relativePath": rel_path,
            "expected": { 
                "mediaType": "episode", 
                "title": title, 
                "season": season, 
                "episode": episode,
                "resolution": quality,
                "codec": codec,
                "group": group
            }
        })

    return entries

def harvest(count=5000, output_path=None):
    entries = []
    all_titles = TITLES + EVIL_TITLES
    
    for _ in range(count):
        if random.random() > 0.4:
            entries.extend(generate_movie(random.choice(all_titles), random.choice(YEARS)))
        else:
            entries.extend(generate_episode(random.choice(all_titles), random.randint(1, 10), random.randint(1, 24)))
    
    manifest = {
        "schemaVersion": "1.0",
        "name": "tier-b-synthetic-dirty",
        "description": f"Dirty dataset with {len(entries)} items including clutter (NFO, samples, etc).",
        "entries": entries
    }
    
    final_path = Path(output_path) if output_path else Path("datasets/regression/tier-b-synthetic-dirty.json")
    with open(final_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    
    print(f"Successfully harvested {len(entries)} entries to {final_path}")

if __name__ == "__main__":
    import sys
    count = 5000
    out = None
    if len(sys.argv) > 1:
        count = int(sys.argv[1])
    if len(sys.argv) > 2:
        out = sys.argv[2]
    harvest(count, out)
