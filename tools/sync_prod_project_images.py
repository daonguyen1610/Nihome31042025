#!/usr/bin/env python3
"""Sync project images from https://niconvn.com into the local wwwroot.

For every project returned by the public /api/projects endpoint:
  - download its `imageUrl` and each entry in `gallery` from prod
  - save them under nihomebackend/wwwroot/<same relative path>
  - rewrite the matching row in ContentSeeder.cs so ImageUrl / GalleryJson
    match the prod values.

Slugs present locally but missing in prod are left untouched. A manifest is
written to tools/sync_prod_project_images_report.json.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path
from urllib.parse import quote, urljoin
from urllib.request import Request, urlopen

REPO = Path(__file__).resolve().parent.parent
WWWROOT = REPO / "nihomebackend" / "wwwroot"
SEEDER = REPO / "nihomebackend" / "Data" / "ContentSeeder.cs"
REPORT = REPO / "tools" / "sync_prod_project_images_report.json"
API_URL = "https://niconvn.com/api/projects?pageSize=500"
BASE = "https://niconvn.com"
HEADERS = {"User-Agent": "NiconAdminProjectSync/1.0"}


def fetch(url: str) -> bytes:
    with urlopen(Request(url, headers=HEADERS), timeout=60) as resp:
        return resp.read()


def download(rel_path: str) -> bool:
    """Download `/images/upload/...` from prod into wwwroot. Returns True if file is present afterwards."""
    rel = rel_path.lstrip("/")
    local = WWWROOT / rel
    if local.exists() and local.stat().st_size > 0:
        return True
    local.parent.mkdir(parents=True, exist_ok=True)
    url = urljoin(BASE + "/", quote(rel, safe="/"))
    try:
        blob = fetch(url)
    except Exception as exc:  # noqa: BLE001
        print(f"  FAILED {url}: {exc}", file=sys.stderr)
        return False
    local.write_bytes(blob)
    print(f"  saved {rel} ({len(blob)} bytes)", file=sys.stderr)
    return True


def update_seeder(updates: dict[str, dict]) -> int:
    src = SEEDER.read_text()
    changed = 0
    for slug, payload in updates.items():
        image_url = payload["imageUrl"]
        gallery = payload["gallery"]

        # 1. Replace ImageUrl on the row matching this slug.
        row_re = re.compile(
            rf'(Slug = "{re.escape(slug)}", ImageUrl = ")[^"]+(")',
        )
        new_src, n = row_re.subn(rf'\g<1>{image_url}\g<2>', src)
        if n != 1:
            print(f"WARN ImageUrl replace {slug}: {n} matches", file=sys.stderr)
            continue
        src = new_src

        # 2. Replace/insert GalleryJson on the same row.
        gal_literal = (
            "JsonSerializer.Serialize(new[] { "
            + ", ".join(f'"{p}"' for p in gallery)
            + " })"
        ) if gallery else None

        existing_gal_re = re.compile(
            rf'(Slug = "{re.escape(slug)}", ImageUrl = "{re.escape(image_url)}", )GalleryJson = JsonSerializer\.Serialize\(new\[\] \{{[^}}]*\}}\)(, )',
        )
        if existing_gal_re.search(src):
            if gal_literal:
                src = existing_gal_re.sub(rf'\1GalleryJson = {gal_literal}\2', src)
            else:
                src = existing_gal_re.sub(r'\1', src)
        elif gal_literal:
            insert_re = re.compile(
                rf'(Slug = "{re.escape(slug)}", ImageUrl = "{re.escape(image_url)}", )(Name = )',
            )
            new_src, n = insert_re.subn(rf'\1GalleryJson = {gal_literal}, \2', src)
            if n != 1:
                print(f"WARN GalleryJson insert {slug}: {n} matches", file=sys.stderr)
                continue
            src = new_src
        changed += 1

    SEEDER.write_text(src)
    return changed


def main() -> int:
    print(f"Fetching {API_URL}", file=sys.stderr)
    projects = json.loads(fetch(API_URL))

    seeder_text = SEEDER.read_text()
    local_slugs = set(re.findall(r'Slug = "([^"]+)"', seeder_text))

    report: list[dict] = []
    updates: dict[str, dict] = {}

    for proj in projects:
        slug = proj.get("slug")
        if not slug or slug not in local_slugs:
            report.append({"slug": slug, "skipped": "not in local seeder"})
            continue

        image_url = proj.get("imageUrl") or ""
        gallery = proj.get("gallery") or []
        all_paths = [p for p in [image_url, *gallery] if p]

        print(f"[{slug}] {len(all_paths)} image(s)", file=sys.stderr)
        ok_paths: list[str] = []
        for p in all_paths:
            if download(p):
                ok_paths.append(p)

        if not image_url or image_url not in ok_paths:
            report.append({"slug": slug, "skipped": "primary image missing"})
            continue

        confirmed_gallery = [p for p in gallery if p in ok_paths]
        updates[slug] = {"imageUrl": image_url, "gallery": confirmed_gallery}
        report.append({
            "slug": slug,
            "imageUrl": image_url,
            "gallery": confirmed_gallery,
        })

    changed = update_seeder(updates)
    REPORT.write_text(json.dumps(report, indent=2, ensure_ascii=False))
    print(f"\nSeeder rows updated: {changed}", file=sys.stderr)
    print(f"Report: {REPORT}", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
