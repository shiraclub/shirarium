from __future__ import annotations

import sys
import unittest
from pathlib import Path

ENGINE_ROOT = Path(__file__).resolve().parents[1]
if str(ENGINE_ROOT) not in sys.path:
    sys.path.insert(0, str(ENGINE_ROOT))

import app.main as main_module  # noqa: E402


class SyntheticDatasetContractTests(unittest.TestCase):
    def test_representative_movie_and_episode_patterns_parse_as_expected(self) -> None:
        cases = [
            (
                "incoming/movies/Alien.1979.Directors.Cut.1080p.BluRay.x265-GRP.mkv",
                "movie",
                1979,
                None,
                None,
            ),
            (
                "incoming/movies/Spider-Man.No.Way.Home.2021.REPACK.1080p.BluRay.x264.mkv",
                "movie",
                2021,
                None,
                None,
            ),
            (
                "incoming/tv/Breaking.Bad.S01E01.1080p.BluRay.x265.mkv",
                "episode",
                None,
                1,
                1,
            ),
            (
                "incoming/tv/Doctor.Who.2005.S14E01.1080p.WEB.h264.mkv",
                "episode",
                2005,
                14,
                1,
            ),
            (
                "incoming/tv/One.Piece.S01E1071.1080p.WEB.h265.mkv",
                "episode",
                None,
                1,
                1071,
            ),
            (
                "incoming/tv/Arcane.S01E01E02.1080p.NF.WEB-DL.DDP5.1.Atmos.x264.mkv",
                "episode",
                None,
                1,
                1,
            ),
        ]

        for path, expected_type, expected_year, expected_season, expected_episode in cases:
            with self.subTest(path=path):
                result = main_module._heuristic_parse(path)
                self.assertEqual(result.media_type, expected_type)
                self.assertEqual(result.year, expected_year)
                self.assertEqual(result.season, expected_season)
                self.assertEqual(result.episode, expected_episode)
                self.assertNotEqual(result.title, "Unknown Title")


if __name__ == "__main__":
    unittest.main()
