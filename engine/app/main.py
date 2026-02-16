from __future__ import annotations

import json
import os
import re
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
    "1080p",
    "720p",
    "2160p",
    "bluray",
    "brrip",
    "webrip",
    "webdl",
    "x264",
    "x265",
    "h264",
    "h265",
    "aac",
    "dts",
    "hevc",
    "remux",
    "proper",
    "repack",
}

SEASON_EPISODE_RE = re.compile(r"(?:^|[\W_])[sS](\d{1,2})[eE](\d{1,3})(?:$|[\W_])")
YEAR_RE = re.compile(r"(?:^|[\W_])((?:19|20)\d{2})(?:$|[\W_])")
SPLIT_RE = re.compile(r"[.\-_()\[\]\s]+")

OLLAMA_USE = os.getenv("SHIRARIUM_USE_OLLAMA", "false").lower() == "true"
OLLAMA_BASE_URL = os.getenv("SHIRARIUM_OLLAMA_BASE_URL", "http://ollama:11434").rstrip("/")
OLLAMA_MODEL = os.getenv("SHIRARIUM_OLLAMA_MODEL", "llama3.1:8b")

app = FastAPI(title="shirarium-engine", version="0.1.0")


def _normalize_title_tokens(tokens: list[str]) -> str:
    cleaned = [token for token in tokens if token and token.lower() not in COMMON_JUNK]
    if not cleaned:
        return "Unknown Title"
    return " ".join(cleaned).strip().title()


def _heuristic_parse(path: str) -> ParseFilenameResponse:
    stem = Path(path).stem
    episode_match = SEASON_EPISODE_RE.search(stem)
    year_match = YEAR_RE.search(stem)
    tokens = [token for token in SPLIT_RE.split(stem) if token]
    title_tokens = list(tokens)

    season: int | None = None
    episode: int | None = None
    year: int | None = None
    media_type: Literal["movie", "episode", "unknown"] = "unknown"
    confidence = 0.42

    if episode_match:
        season = int(episode_match.group(1))
        episode = int(episode_match.group(2))
        media_type = "episode"
        confidence += 0.28
        clip = episode_match.group(0)
        title_tokens = [t for t in title_tokens if t.lower() not in clip.lower()]

    if year_match:
        year = int(year_match.group(1))
        confidence += 0.2
        media_type = "movie" if media_type == "unknown" else media_type
        clip = year_match.group(1)
        title_tokens = [t for t in title_tokens if t != clip]

    if media_type == "unknown":
        confidence -= 0.08

    title = _normalize_title_tokens(title_tokens)
    confidence = max(0.05, min(0.95, confidence))

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
