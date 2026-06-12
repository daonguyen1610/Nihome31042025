#!/usr/bin/env python3
"""Download public project images from the legacy nicon.vn site.

For each (slug, legacy_path) in PROJECTS, fetch the public detail page,
extract <img> URLs under /Content/Images/uploaded/..., and save them to
nihomebackend/wwwroot/images/projects/<slug>/<index>.<ext>.

Outputs a JSON manifest at tools/project_image_scraper_report.json describing
what was downloaded so the seeder can be updated by hand.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from urllib.parse import quote, unquote, urljoin
from urllib.request import Request, urlopen

BASE_URL = "https://nicon.vn"
OUT_ROOT = Path(__file__).resolve().parent.parent / "nihomebackend" / "wwwroot" / "images" / "projects"
REPORT_PATH = Path(__file__).resolve().parent / "project_image_scraper_report.json"

# slug (in the new seeder) -> legacy detail path on nicon.vn
PROJECTS: list[tuple[str, str]] = [
    ("d56-house", "/projectsongoing/d56-house"),
    ("b37-interior", "/projectsongoing/interior-b37-house"),
    ("swimming-pool-service-building", "/projectsongoing/swimming-pool-service-building"),
    ("thu-duc-multi-purpose-building", "/projectsongoing/thu-duc-multi-purpose-building"),
    ("thu-duc-wedding-banquet", "/projectsongoing/thu-duc-wedding-banquet-restaurant"),
    ("thu-duc-sport-service-building", "/projectsongoing/service-building-thu-duc-center-sport"),
    ("thu-duc-sport-coffee", "/projectsongoing/coffee-thu-duc-center-sport"),
    ("d22-factory", "/projectsongoing/d22-factory"),
    ("salad-stop-restaurant", "/projectsongoing/salad-stop-restaurant"),
    ("champion-lee-factory", "/projectsongoing/champion-lee-group-factory"),
    ("jakob-workshop", "/projectsongoing/proposed-worksop-of-jakob-factory"),
    ("siegwerk-factory", "/projectsongoing/siegwerk-vietnam-factory"),
    ("great-lotus-interior", "/projectsongoing/great-lotus-interior"),
    ("quan-chi-factory", "/projectsongoing/quan-chi-factory"),
    ("velrco-office", "/projectsongoing/velrco-office"),
    ("semivina-nissi-factory", "/projectsongoing/semivina-nissi-factory-4"),
]

IMG_RE = re.compile(
    r'src="(/Content/Images/uploaded/[^"]+\.(?:jpg|jpeg|png|gif|webp))"',
    re.IGNORECASE,
)
NATURAL_KEY_RE = re.compile(r"(\d+)")
HEADERS = {"User-Agent": "NiconProjectImageSync/1.0"}


def natural_key(name: str) -> list:
    return [int(p) if p.isdigit() else p.lower() for p in NATURAL_KEY_RE.split(name)]


def fetch(url: str) -> bytes:
    with urlopen(Request(url, headers=HEADERS), timeout=60) as resp:
        return resp.read()


def extract_image_paths(html_bytes: bytes) -> list[str]:
    seen: list[str] = []
    for match in IMG_RE.finditer(html_bytes.decode("utf-8", errors="ignore")):
        path = match.group(1).strip()
        if path not in seen:
            seen.append(path)
    seen.sort(key=lambda p: natural_key(unquote(p.rsplit("/", 1)[-1])))
    return seen


def download_project(slug: str, legacy_path: str) -> dict:
    out_dir = OUT_ROOT / slug
    out_dir.mkdir(parents=True, exist_ok=True)

    page_url = urljoin(BASE_URL + "/", legacy_path.lstrip("/"))
    print(f"[{slug}] fetching {page_url}", file=sys.stderr)
    page = fetch(page_url)
    paths = extract_image_paths(page)
    if not paths:
        print(f"[{slug}] no images found", file=sys.stderr)
        return {"slug": slug, "source": page_url, "images": []}

    saved: list[str] = []
    for index, src in enumerate(paths, start=1):
        ext = Path(unquote(src)).suffix.lower() or ".jpg"
        local_name = f"{index:02d}{ext}"
        local_path = out_dir / local_name
        if local_path.exists() and local_path.stat().st_size > 0:
            print(f"  [{slug}] keep {local_name}", file=sys.stderr)
        else:
            full = urljoin(BASE_URL + "/", quote(src.lstrip("/"), safe="/"))
            try:
                blob = fetch(full)
            except Exception as exc:  # noqa: BLE001
                print(f"  [{slug}] FAILED {full}: {exc}", file=sys.stderr)
                continue
            local_path.write_bytes(blob)
            print(f"  [{slug}] saved {local_name} ({len(blob)} bytes)", file=sys.stderr)
        saved.append(f"/images/projects/{slug}/{local_name}")

    return {"slug": slug, "source": page_url, "images": saved}


def main() -> int:
    OUT_ROOT.mkdir(parents=True, exist_ok=True)
    report = []
    for slug, legacy_path in PROJECTS:
        try:
            report.append(download_project(slug, legacy_path))
        except Exception as exc:  # noqa: BLE001
            print(f"[{slug}] ERROR: {exc}", file=sys.stderr)
            report.append({"slug": slug, "error": str(exc), "images": []})

    REPORT_PATH.write_text(json.dumps(report, indent=2, ensure_ascii=False))
    print(f"\nReport written to {REPORT_PATH}", file=sys.stderr)
    total = sum(len(r.get("images", [])) for r in report)
    print(f"Total images: {total}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
