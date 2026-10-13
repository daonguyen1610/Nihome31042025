from __future__ import annotations

import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).with_name("check_translation_keys.py").resolve()


class TranslationKeyCheckerCliTests(unittest.TestCase):
    def create_repo(self, seed_entries: list[dict[str, str]], source: str) -> Path:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        root = Path(temporary.name)
        seed_dir = root / "nihomebackend" / "Data" / "Seeds" / "i18n"
        source_dir = root / "nihomeweb" / "src"
        seed_dir.mkdir(parents=True)
        source_dir.mkdir(parents=True)
        (seed_dir / "test.json").write_text(
            json.dumps(seed_entries, ensure_ascii=False),
            encoding="utf-8",
        )
        (source_dir / "Example.tsx").write_text(source, encoding="utf-8")
        return root

    def run_checker(self, repo_root: Path, *arguments: str) -> subprocess.CompletedProcess[str]:
        return subprocess.run(
            [sys.executable, str(SCRIPT), "--repo-root", str(repo_root), *arguments],
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )

    def test_missing_key_prints_summary_table_and_fails(self) -> None:
        repo_root = self.create_repo([], 'const label = t("example.missing");')
        report = repo_root / "translation-report.md"

        result = self.run_checker(
            repo_root,
            "--format",
            "markdown",
            "--output",
            str(report),
            "--no-warnings",
        )

        self.assertEqual(1, result.returncode)
        self.assertIn("| Check | Checked | Missing |", result.stdout)
        self.assertIn("| frontend t() literals | 1 | 1 |", result.stdout)
        self.assertIn("`example.missing`", result.stdout)
        self.assertEqual(result.stdout, report.read_text(encoding="utf-8"))

    def test_complete_key_set_returns_success(self) -> None:
        repo_root = self.create_repo(
            [
                {
                    "key": "example.present",
                    "category": "test",
                    "vi": "Ví dụ",
                    "en": "Example",
                    "zh": "示例",
                    "ja": "例",
                }
            ],
            'const label = t("example.present");',
        )

        result = self.run_checker(repo_root, "--no-warnings")

        self.assertEqual(0, result.returncode)
        self.assertIn("all referenced keys are seeded", result.stdout)
        self.assertIn("| frontend t() literals      |       1 |       0 |", result.stdout)
        self.assertIn("| TOTAL                      |       2 |       0 |", result.stdout)


if __name__ == "__main__":
    unittest.main()
