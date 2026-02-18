import sqlite3
import json
import os
import argparse
from pathlib import Path

def process_magnetdb(db_path, output_path, limit=None):
    if not os.path.exists(db_path):
        print(f"Error: MagnetDB SQLite file not found at {db_path}")
        return

    print(f"Connecting to MagnetDB: {db_path}")
    conn = sqlite3.connect(db_path)
    cursor = conn.cursor()

    # Query for matched video files (Movies and Episodes)
    # The schema usually has a table like 'matched_files' or similar based on research
    # We will attempt to find the right table and columns
    query = """
    SELECT 
        f.filename, 
        m.title, 
        m.year, 
        m.type, 
        m.season, 
        m.episode,
        m.imdb_id
    FROM matched_files AS m
    JOIN files AS f ON m.file_id = f.id
    """
    
    if limit:
        query += f" LIMIT {limit}"

    print("Extracting matched records...")
    entries = []
    try:
        cursor.execute(query)
        for row in cursor:
            filename, title, year, mtype, season, episode, imdb_id = row
            
            entry = {
                "relativePath": filename,
                "expected": {
                    "mediaType": "movie" if mtype.lower() == "movie" else "episode",
                    "title": title,
                    "year": int(year) if year else None,
                    "season": int(season) if season else None,
                    "episode": int(episode) if episode else None,
                    "imdbId": imdb_id
                }
            }
            entries.append(entry)
    except sqlite3.Error as e:
        print(f"Database error: {e}")
        # List tables to help developer debug
        cursor.execute("SELECT name FROM sqlite_master WHERE type='table';")
        print("Available tables:", [r[0] for r in cursor.fetchall()])
        return

    manifest = {
        "schemaVersion": "1.0",
        "name": "magnetdb-matched-total",
        "description": f"Extracted ground-truth dataset from MagnetDB.",
        "entries": entries
    }

    print(f"Writing {len(entries)} entries to {output_path}...")
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(manifest, f, indent=2)
    
    print("Done.")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="MagnetDB Matched Record Extractor")
    parser.add_argument("db", help="Path to magnetdb_public.sqlite3")
    parser.add_argument("--output", default="datasets/benchmark/magnetdb-matched.json", help="Output JSON path")
    parser.add_argument("--limit", type=int, help="Limit number of records")
    args = parser.parse_args()

    process_magnetdb(args.db, args.output, limit=args.limit)
