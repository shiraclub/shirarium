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
# Added support for S\d+ standalone (e.g. S4)
SEASON_EPISODE_RE = re.compile(
    r"(?:^|[\W_])(?:[sS](\d{1,2})[\W_]?[eE](\d{1,4})|(\d{1,2})x(\d{1,4})|[sS](\d{1,2}))(?:[\W_]?[eE](\d{1,4}))?(?:$|[\W_])"
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
OLLAMA_MODEL = os.getenv("SHIRARIUM_OLLAMA_MODEL", "qwen3:4b")

app = FastAPI(title="shirarium-engine", version="0.1.0")

SYSTEM_PROMPT = """You are Shirarium-Core, a high-precision metadata extraction engine.
TASK: Extract media metadata from the provided path.
OUTPUT: Strict JSON only. No markdown, no conversational filler.

LOGIC RULES:
1. TITLE vs YEAR: If a title looks like a year (e.g., '1917', '2012'), use context to disambiguate. 
2. ABSOLUTE NUMBERING: For anime, 3-4 digit numbers (e.g., '1050') are likely absolute episodes, not years.
3. SCRIPT FIDELITY: Preserve original scripts (CJK, Cyrillic) exactly. DO NOT transliterate.

SCHEMA:
{
  "title": "string",
  "media_type": "movie" | "episode" | "unknown",
  "year": integer | null,
  "season": integer | null,
  "episode": integer | null,
  "confidence": float
}"""


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
    
    # 1. Base Parse of the current stem
    result = _parse_core(stem)
    
    # 2. Folder Context Enrichment
    if len(parts) > 1:
        # We merge results from parents to fill in gaps
        for i in range(len(parts)-2, -1, -1):
            parent_name = parts[i]
            if parent_name.lower() in ["movies", "tv", "media", "organized", "incoming"]:
                continue
                
            parent_result = _parse_core(parent_name)
            
            # Merge logic
            if result.title == "Unknown Title" or len(result.title) < 3 or result.title.isdigit():
                result.title = parent_result.title
            
            if result.year is None:
                result.year = parent_result.year
            
            if result.media_type == "unknown":
                result.media_type = parent_result.media_type
            
            if result.season is None:
                result.season = parent_result.season
            if result.episode is None:
                result.episode = parent_result.episode
            
            # If we reached a stable state, stop looking at parents
            if result.title != "Unknown Title" and result.media_type != "unknown":
                break

    return result


def _parse_core(stem: str) -> ParseFilenameResponse:
    stem = CRC_RE.sub("", stem)
    
    # Strip leading group tags
    while True:
        match = LEADING_TAG_RE.search(stem)
        if match:
            stem = stem[match.end():]
        else:
            break
    
    # Match Season/Episode
    season: int | None = None
    episode: int | None = None
    media_type: Literal["movie", "episode", "unknown"] = "unknown"
    confidence = 0.4
    title_stem = stem
    
    match = SEASON_EPISODE_RE.search(stem)
    if match:
        media_type = "episode"
        confidence += 0.35
        # Extract the first non-None pairs
        gs = [g for g in match.groups() if g is not None]
        if len(gs) >= 2:
            season = int(gs[0])
            episode = int(gs[1])
        elif len(gs) == 1:
            season = int(gs[0])
        else:
            season = 1
            episode = 1
        title_stem = stem[:match.start()]
    else:
        # Absolute numbering check
        match = ABSOLUTE_EPISODE_RE.search(stem)
        if match:
            num = int(match.group(1))
            is_year_like = 1900 <= num <= 2100
            if " - " in stem or (not is_year_like and any(x in stem.lower() for x in ["season", "ep", "subs", "raws"])):
                media_type = "episode"
                confidence += 0.25
                season = 1
                episode = num
                title_stem = stem[:match.start()]

    # Match Year (Decisive but careful)
    year: int | None = None
    year_match = YEAR_PAREN_RE.search(stem)
    if year_match:
        year = int(year_match.group(1))
        if media_type == "unknown":
            media_type = "movie"
        idx = stem.find(year_match.group(0))
        if idx > 2:
            title_stem = stem[:idx]
    else:
        year_match = YEAR_RE.search(stem)
        if year_match:
            found_year = int(year_match.group(1))
            after_year = stem[year_match.end():].lower()
            is_followed_by_junk = any(junk in after_year for junk in COMMON_JUNK)
            is_at_start = year_match.start() < 2
            
            if is_at_start and not is_followed_by_junk and not stem.strip().endswith(str(found_year)):
                title_stem = stem
            elif is_followed_by_junk or stem.strip().endswith(str(found_year)):
                year = found_year
                if media_type == "unknown":
                    media_type = "movie"
                title_stem = stem[:year_match.start()]
            else:
                if media_type == "unknown":
                    media_type = "movie"
                year = found_year

    if media_type == "unknown":
        low_stem = stem.lower()
        if re.search(r"[sS]\d+", low_stem) or "season" in low_stem:
            media_type = "episode"

    # Final Title Extraction
    tokens = [token for token in SPLIT_RE.split(title_stem) if token]
    title = _normalize_title_tokens(tokens)
    
    if " - " in title:
        title_parts = title.split(" - ")
        if title_parts[-1].lower() in COMMON_JUNK or QUAL_RE.search(title_parts[-1]):
            title = " ".join(title_parts[:-1]).strip()

    if media_type == "movie" and year:
        confidence += 0.2

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


async def _ollama_parse(path: str) -> ParseFilenameResponse | None:
    is_ollama = "/api/chat" in OLLAMA_BASE_URL or "11434" in OLLAMA_BASE_URL
    
    # Adaptive endpoint detection
    if is_ollama:
        url = f"{OLLAMA_BASE_URL}/api/chat" if not OLLAMA_BASE_URL.endswith("/api/chat") else OLLAMA_BASE_URL
        payload = {
            "model": OLLAMA_MODEL,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": f"Path: {path}"}
            ],
            "stream": False,
            "options": {
                "temperature": 0.0,
                "num_predict": 128
            }
        }
    else:
        # Assume OpenAI-compatible (llama-server)
        url = f"{OLLAMA_BASE_URL}/v1/chat/completions" if not OLLAMA_BASE_URL.endswith("/v1/chat/completions") else OLLAMA_BASE_URL
        payload = {
            "model": OLLAMA_MODEL,
            "messages": [
                {"role": "system", "content": SYSTEM_PROMPT},
                {"role": "user", "content": f"Path: {path}"}
            ],
            "temperature": 0.0,
            "max_tokens": 128
        }

    try:
        async with httpx.AsyncClient(timeout=30.0) as client:
            response = await client.post(url, json=payload)
            response.raise_for_status()
            data = response.json()
            
            if is_ollama:
                content = data.get("message", {}).get("content", "")
            else:
                content = data.get("choices", [{}])[0].get("message", {}).get("content", "")

            # 1. Strip <think> blocks if present
            content = re.sub(r"<think>.*?</think>", "", content, flags=re.DOTALL).strip()
            
            # 2. Extract JSON if wrapped in markdown
            json_match = re.search(r"\{.*\}", content, re.DOTALL)
            if json_match:
                content = json_match.group(0)

            parsed = json.loads(content)
            
            return ParseFilenameResponse(
                title=parsed.get("title", "Unknown Title"),
                media_type=parsed.get("media_type", "unknown"),
                year=parsed.get("year"),
                season=parsed.get("season"),
                episode=parsed.get("episode"),
                confidence=parsed.get("confidence", 0.95),
                source="ollama",
                raw_tokens=[content[:50]]
            )
    except Exception as e:
        print(f"Inference Error ({url}): {e}")
        return None


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok", "ollama_enabled": str(OLLAMA_USE).lower()}


@app.post("/v1/parse-filename", response_model=ParseFilenameResponse)
async def parse_filename(request: ParseFilenameRequest) -> ParseFilenameResponse:
    heuristic_result = _heuristic_parse(request.path)

    if OLLAMA_USE:
        if heuristic_result.confidence < 0.90 or heuristic_result.media_type == "unknown":
            try:
                ai_result = await _ollama_parse(request.path)
                if ai_result:
                    return ai_result
            except Exception as e:
                print(f"Ollama Fallback Error: {e}")

    return heuristic_result
