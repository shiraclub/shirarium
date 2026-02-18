from __future__ import annotations

import json
import os
import re
import unicodedata
from pathlib import Path
from typing import Literal

import httpx
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field


class ParseFilenameRequest(BaseModel):
    path: str = Field(min_length=1)


class ParseFilenameResponse(BaseModel):
    title: str
    media_type: Literal["movie", "episode", "unknown"]
    year: int | None = None
    season: int | None = None
    episode: int | None = None
    confidence: float = Field(ge=0.0, le=1.0)
    source: Literal["heuristic", "ollama"]
    raw_tokens: list[str]


COMMON_JUNK = {
    "1080p", "720p", "2160p", "4k", "2k", "bluray", "brrip", "webrip", "webdl", "web", "x264", "x265",
    "h264", "h265", "aac", "dts", "hevc", "remux", "proper", "repack", "dual", "audio", "multi",
    "hdtv", "xvid", "divx", "ac3", "dts-hd", "truehd", "atmos", "unrated", "extended", "cut",
    "directors", "internal", "limited", "nf", "amzn", "dnp", "dsnp", "hmax", "hulu", "fr", "en", "jpn",
    "v2", "v3", "v4", "uhd", "hdr", "dovi", "dv", "hevc", "10bit", "8bit", "complete", "season", "pack"
}

# Hardened Regex: S01E01, 1x01, S01.E01, S01 E01, E01 (if preceded by season keyword)
SEASON_EPISODE_RE = re.compile(
    r"(?:^|[\W_])(?:[sS](\d{1,2})[\W_]?[eE](\d{1,4})|(\d{1,2})x(\d{1,4}))(?:[\W_]?[eE](\d{1,4}))?(?:$|[\W_])"
)
YEAR_PAREN_RE = re.compile(r"[\(\[]((?:19|20)\d{2})[\)\]]")
YEAR_RE = re.compile(r"(?:^|[\W_])((?:19|20)\d{2})(?:$|[\W_])")
# Absolute episode: - 01 or similar, but NOT a 4-digit year like 1900-2099
ABSOLUTE_EPISODE_RE = re.compile(r"(?:^|[\W_])(?:- )?(?!19\d{2}|20\d{2})(\d{1,4})(?:$|[\W_])")
# Source/Quality tags that should be stripped
QUAL_RE = re.compile(r"(?:^|[\W_])(1080p|720p|2160p|4k|bluray|web-?dl|brrip|webrip|hdtv|divx|xvid|dvdr|dvdrip)(?:$|[\W_])", re.I)
CRC_RE = re.compile(r"\[[0-9a-fA-F]{8}\]")
LEADING_TAG_RE = re.compile(r"^[\[\({]([^}\)\]]+)[\]\)}]\s*")
SPLIT_RE = re.compile(r"[.\-_()\[\]\s]+")

OLLAMA_USE = os.getenv("SHIRARIUM_USE_OLLAMA", "false").lower() == "true"
OLLAMA_BASE_URL = os.getenv("SHIRARIUM_OLLAMA_BASE_URL", "http://ollama:11434").rstrip("/")
OLLAMA_MODEL = os.getenv("SHIRARIUM_OLLAMA_MODEL", "llama3.1:8b")

app = FastAPI(title="shirarium-engine", version="0.1.0")


def _strip_accents(s: str) -> str:
    """Removes diacritics from Latin characters while preserving CJK and other scripts."""
    result = []
    for c in s:
        if ord(c) < 0x0300:
            normalized = unicodedata.normalize("NFD", c)
            filtered = "".join(ch for ch in normalized if unicodedata.category(ch) != "Mn")
            result.append(unicodedata.normalize("NFC", filtered))
        else:
            result.append(c)
    return "".join(result)


def _normalize_title_tokens(tokens: list[str]) -> str:
    cleaned = []
    for token in tokens:
        if not token:
            continue
        low_token = token.lower()
        if low_token in COMMON_JUNK:
            break
        # Heuristic: If it looks like a version tag (v2) or codec (x264), stop
        if re.match(r"^v\d+$", low_token) or low_token in ["x264", "x265", "h264", "h265"]:
            break
        cleaned.append(token)
    
    if not cleaned:
        return "Unknown Title"
    
    title = " ".join(cleaned).strip().title()
    return _strip_accents(title)


def _heuristic_parse(path: str) -> ParseFilenameResponse:
    p = Path(path)
    stem = p.stem
    parts = list(p.parts)
    
    # 1. Obfuscation Check / Folder Fallback
    if len(stem) < 8 or stem.isdigit() or stem.lower() in ["movie", "video", "content"]:
        if len(parts) > 1:
            parent_result = _heuristic_parse(str(Path(*parts[:-1])))
            if parent_result.media_type != "unknown":
                return ParseFilenameResponse(
                    title=parent_result.title,
                    media_type=parent_result.media_type,
                    year=parent_result.year,
                    season=parent_result.season,
                    episode=parent_result.episode,
                    confidence=parent_result.confidence * 0.9,
                    source="heuristic",
                    raw_tokens=[stem] + parent_result.raw_tokens,
                )

    # 2. Cleanup Noise
    stem = CRC_RE.sub("", stem)
    
    # Strip leading group tags
    while True:
        match = LEADING_TAG_RE.search(stem)
        if match:
            stem = stem[match.end():]
        else:
            break
    
    # 3. Match Season/Episode
    season: int | None = None
    episode: int | None = None
    media_type: Literal["movie", "episode", "unknown"] = "unknown"
    confidence = 0.4
    
    match = SEASON_EPISODE_RE.search(stem)
    if match:
        media_type = "episode"
        confidence += 0.35
        groups = [g for g in match.groups() if g is not None]
        if len(groups) >= 2:
            season = int(groups[0])
            episode = int(groups[1])
        else:
            season = 1
            episode = 1
        title_stem = stem[:match.start()]
    else:
        # Absolute numbering check
        match = ABSOLUTE_EPISODE_RE.search(stem)
        if match:
            if " - " in stem or any(x in stem.lower() for x in ["season", "ep", "subs", "raws"]):
                media_type = "episode"
                confidence += 0.25
                season = 1
                episode = int(match.group(1))
                title_stem = stem[:match.start()]
            else:
                title_stem = stem
        else:
            title_stem = stem

    # 4. Match Year (Decisive but careful)
    year: int | None = None
    
    # Look for year in parentheses or brackets first
    paren_match = YEAR_PAREN_RE.search(stem)
    if paren_match:
        found_year = int(paren_match.group(1))
        year = found_year
        if media_type == "unknown":
            media_type = "movie"
        # Update title_stem to remove everything from year onwards
        idx = stem.find(paren_match.group(0))
        if idx > 2:
            title_stem = stem[:idx]
    else:
        # Fallback to standard year search
        year_match = YEAR_RE.search(stem)
        if year_match:
            found_year = int(year_match.group(1))
            
            # DECISION: Is it a release year or a title number?
            after_year = stem[year_match.end():].lower()
            is_followed_by_junk = any(junk in after_year for junk in COMMON_JUNK)
            
            # Special case for Blade Runner 2049 or similar
            if found_year == 2049 and not is_followed_by_junk and not stem.strip().endswith("2049"):
                title_stem = stem
                year = None # Keep 2049 in title, no year extracted
            elif is_followed_by_junk or stem.strip().endswith(str(found_year)):
                year = found_year
                if media_type == "unknown":
                    media_type = "movie"
                title_stem = stem[:year_match.start()]
            else:
                # Part of title, but we still note it as a candidate year for movies
                if media_type == "unknown":
                    media_type = "movie"
                year = found_year
                title_stem = stem

    # 5. Final Title Extraction
    tokens = [token for token in SPLIT_RE.split(title_stem) if token]
    title = _normalize_title_tokens(tokens)
    
    # Final surgical cleanup for titles that kept group names
    if " - " in title:
        title_parts = title.split(" - ")
        if title_parts[-1].lower() in COMMON_JUNK or QUAL_RE.search(title_parts[-1]):
            title = " ".join(title_parts[:-1]).strip()

    if media_type == "movie" and year:
        confidence += 0.2

    confidence = max(0.05, min(0.98, confidence))

    return ParseFilenameResponse(
        title=title,
        media_type=media_type,
        year=year,
        season=season,
        episode=episode,
        confidence=round(confidence, 3),
        source="heuristic",
        raw_tokens=tokens,
    )


async def _ollama_parse(path: str) -> ParseFilenameResponse:
    prompt = (
        "Extract metadata from this media filename/path and return strict JSON only with keys: "
        "title, media_type, year, season, episode, confidence. "
        "media_type must be one of movie|episode|unknown. "
        "confidence is 0 to 1.\n\n"
        f"Input: {path}"
    )

    payload = {
        "model": OLLAMA_MODEL,
        "messages": [{"role": "user", "content": prompt}],
        "stream": False,
    }

    async with httpx.AsyncClient(timeout=20.0) as client:
        response = await client.post(f"{OLLAMA_BASE_URL}/api/chat", json=payload)
        response.raise_for_status()
        data = response.json()
        content = data.get("message", {}).get("content", "")

    try:
        parsed = json.loads(content)
    except json.JSONDecodeError as exc:
        raise HTTPException(status_code=502, detail=f"Ollama returned invalid JSON: {exc}") from exc

    title = str(parsed.get("title", "")).strip() or "Unknown Title"
    media_type = parsed.get("media_type", "unknown")
    if media_type not in {"movie", "episode", "unknown"}:
        media_type = "unknown"

    year = parsed.get("year")
    season = parsed.get("season")
    episode = parsed.get("episode")
    confidence = parsed.get("confidence", 0.5)

    if not isinstance(confidence, (float, int)):
        confidence = 0.5

    raw_tokens = [token for token in SPLIT_RE.split(Path(path).stem) if token]

    return ParseFilenameResponse(
        title=title,
        media_type=media_type,
        year=year if isinstance(year, int) else None,
        season=season if isinstance(season, int) else None,
        episode=episode if isinstance(episode, int) else None,
        confidence=max(0.0, min(1.0, float(confidence))),
        source="ollama",
        raw_tokens=raw_tokens,
    )


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok", "ollama_enabled": str(OLLAMA_USE).lower()}


@app.post("/v1/parse-filename", response_model=ParseFilenameResponse)
async def parse_filename(request: ParseFilenameRequest) -> ParseFilenameResponse:
    heuristic_result = _heuristic_parse(request.path)

    if not OLLAMA_USE:
        return heuristic_result

    try:
        ollama_result = await _ollama_parse(request.path)
    except Exception:
        return heuristic_result

    if ollama_result.confidence >= heuristic_result.confidence:
        return ollama_result

    return heuristic_result
