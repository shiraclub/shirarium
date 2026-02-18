import json
import random
from pathlib import Path

# Metadata Pools
TITLES = [
    "The Matrix", "Interstellar", "Inception", "Arrival", "Blade Runner 2049",
    "Spirited Away", "Parasite", "Godzilla Minus One", "Alien", "The Thing",
    "Breaking Bad", "The Office", "Better Call Saul", "Succession", "Dark",
    "One Piece", "Attack on Titan", "Demon Slayer", "Jujutsu Kaisen"
]

YEARS = [1979, 1982, 1999, 2001, 2010, 2014, 2017, 2019, 2021, 2023, 2024]
QUALITIES = ["1080p", "720p", "2160p", "4K", "UHD", "DVDRip", "BRRip"]
SOURCES = ["BluRay", "WEB-DL", "WEBRip", "HDTV", "HDR", "DoVi"]
CODECS = ["x264", "x265", "h264", "HEVC", "AV1"]
GROUPS = ["SPARKS", "DON", "AMIABLE", "FRAme", "Erai-raws", "SubsPlease", "YTS", "RARBG"]

def generate_movie(title, year):
    quality = random.choice(QUALITIES)
    source = random.choice(SOURCES)
    codec = random.choice(CODECS)
    group = random.choice(GROUPS)
    
    # Randomize Template
    templates = [
        f"{title.replace(' ', '.')}.{year}.{quality}.{source}.{codec}-{group}.mkv",
        f"{title} ({year}) [{quality}] [{codec}].mp4",
        f"{title}.{year}.{source}.{codec}.mkv",
        f"[{group}] {title} ({year}) {quality}.avi"
    ]
    
    return {
        "relativePath": random.choice(templates),
        "expected": {
            "mediaType": "movie",
            "title": title,
            "year": year
        }
    }

def generate_episode(title, season, episode):
    quality = random.choice(QUALITIES)
    group = random.choice(GROUPS)
    
    # Randomize Template
    templates = [
        f"{title.replace(' ', '.')}.S{season:02d}E{episode:02d}.{quality}.mkv",
        f"{title} - {season}x{episode:02d} - Episode Title [{quality}].mp4",
        f"{title}.S{season:02d}.E{episode:02d}.{quality}.WEBRip.{random.choice(CODECS)}-{group}.mkv",
        f"[{group}] {title} - {episode:02d} [{quality}].mkv"
    ]
    
    return {
        "relativePath": random.choice(templates),
        "expected": {
            "mediaType": "episode",
            "title": title,
            "season": season if "x" not in templates[0] else season,
            "episode": episode
        }
    }

def harvest(count=5000):
    entries = []
    for _ in range(count):
        if random.random() > 0.4:
            entries.append(generate_movie(random.choice(TITLES), random.choice(YEARS)))
        else:
            entries.append(generate_episode(random.choice(TITLES), random.randint(1, 10), random.randint(1, 24)))
    
    manifest = {
        "schemaVersion": "1.0",
        "name": "tier-b-synthetic-stress-large",
        "description": f"Large generated stress test dataset with {count} items.",
        "entries": entries
    }
    
    output_path = Path("datasets/regression/tier-b-synthetic.json")
    with open(output_path, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
    
    print(f"Successfully harvested {count} entries to {output_path}")

if __name__ == "__main__":
    harvest(5000)
