from __future__ import annotations

import sys
import unittest
from pathlib import Path

from fastapi.testclient import TestClient

ENGINE_ROOT = Path(__file__).resolve().parents[1]
if str(ENGINE_ROOT) not in sys.path:
    sys.path.insert(0, str(ENGINE_ROOT))

import app.main as main_module  # noqa: E402


class HeuristicParserTests(unittest.TestCase):
    def test_parses_episode_pattern(self) -> None:
        result = main_module._heuristic_parse("My.Show.S02E07.1080p.WEBRip.mkv")

        self.assertEqual(result.media_type, "episode")
        self.assertEqual(result.season, 2)
        self.assertEqual(result.episode, 7)
        self.assertGreaterEqual(result.confidence, 0.65)

    def test_parses_movie_year(self) -> None:
        result = main_module._heuristic_parse("Noroi (2005) 1080p BluRay x264.mkv")

        self.assertEqual(result.media_type, "movie")
        self.assertEqual(result.year, 2005)
        self.assertEqual(result.source, "heuristic")
        self.assertIn("Noroi", result.title)

    def test_handles_junk_only_tokens(self) -> None:
        result = main_module._heuristic_parse("1080p.x264.REMUX.mkv")

        self.assertEqual(result.title, "Unknown Title")
        self.assertEqual(result.media_type, "unknown")


class ApiTests(unittest.TestCase):
    def setUp(self) -> None:
        self.client = TestClient(main_module.app)

    def test_health(self) -> None:
        response = self.client.get("/health")
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertIn("status", payload)
        self.assertEqual(payload["status"], "ok")

    def test_parse_filename_endpoint_heuristic(self) -> None:
        response = self.client.post(
            "/v1/parse-filename",
            json={"path": "[Fansub]__Kowasugi__S01E01_(1080p).mkv"},
        )
        self.assertEqual(response.status_code, 200)
        payload = response.json()
        self.assertEqual(payload["media_type"], "episode")
        self.assertEqual(payload["season"], 1)
        self.assertEqual(payload["episode"], 1)

    def test_parse_filename_endpoint_requires_path(self) -> None:
        response = self.client.post("/v1/parse-filename", json={"path": ""})
        self.assertEqual(response.status_code, 422)

    def test_ollama_failure_falls_back_to_heuristic(self) -> None:
        original_use_ollama = main_module.OLLAMA_USE
        original_ollama_parse = main_module._ollama_parse

        async def _raise(_: str):
            raise RuntimeError("forced failure")

        try:
            main_module.OLLAMA_USE = True
            main_module._ollama_parse = _raise

            response = self.client.post(
                "/v1/parse-filename",
                json={"path": "Occult.2009.1080p.mkv"},
            )

            self.assertEqual(response.status_code, 200)
            payload = response.json()
            self.assertEqual(payload["source"], "heuristic")
            self.assertEqual(payload["media_type"], "movie")
            self.assertEqual(payload["year"], 2009)
        finally:
            main_module.OLLAMA_USE = original_use_ollama
            main_module._ollama_parse = original_ollama_parse


if __name__ == "__main__":
    unittest.main()
