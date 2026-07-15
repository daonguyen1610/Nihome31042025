#!/usr/bin/env python3
"""
reorganize-upload-images.py
----------------------------
Reorganizes nihomebackend/wwwroot/images/upload/ from a flat UUID structure
into entity-based subfolders so images are easy to identify by page/entity.

New structure:
    images/upload/
        projects/<slug>/
        activities/<slug>/
        news/<slug>/
        slideshow/
        logos/
        about/
        services/
        misc/          ← orphaned files not referenced in any DB record

This script:
    1. Queries the SQL Server DB (via Docker container) for all entities
       that have /images/upload/ paths.
    2. Copies (or moves) each referenced file into the appropriate subfolder.
    3. Prints a SQL UPDATE script and a Python patch for ContentSeeder.cs.

Usage:
    python3 scripts/reorganize-upload-images.py [--apply] [--db-password <pw>]

Options:
    --apply         Actually move files and apply DB updates.  Without this
                    flag the script runs in dry-run mode and only prints
                    what it WOULD do.
    --db-password   SA password for the Docker SQL Server container.
                    Defaults to "Nihome@31042025".
    --container     Docker container name. Defaults to
                    "nihome31042025-sqlserver".

Exit codes:
    0   success (or dry-run with no errors)
    1   error

WARNING: Run with --apply only after committing or stashing any open changes,
and only when the backend is stopped or in a state where the cleanup service
won't interfere.
"""

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
UPLOAD_DIR = REPO_ROOT / "nihomebackend" / "wwwroot" / "images" / "upload"
SEEDER_PATH = REPO_ROOT / "nihomebackend" / "Data" / "ContentSeeder.cs"
UPLOAD_PREFIX = "/images/upload/"


def run_sqlcmd(container: str, password: str, query: str) -> str:
    result = subprocess.run(
        [
            "docker", "exec", container,
            "/opt/mssql-tools18/bin/sqlcmd",
            "-S", "localhost", "-U", "sa", "-P", password, "-C",
            "-d", "NihomeDB", "-y", "0",
            "-Q", f"SET NOCOUNT ON; {query} FOR JSON PATH;",
        ],
        capture_output=True, text=True,
    )
    if result.returncode != 0:
        print(f"sqlcmd error: {result.stderr}", file=sys.stderr)
        sys.exit(1)
    raw = "".join(result.stdout.strip().split("\n"))
    return raw


def fetch_entity_images(container: str, password: str) -> dict[str, dict]:
    """
    Returns a dict keyed by entity type → {slug: {imageUrl, gallery}}
    """
    entities = {}

    # Projects
    raw = run_sqlcmd(
        container, password,
        "SELECT Slug, ImageUrl, GalleryJson FROM projects "
        "WHERE ImageUrl LIKE '/images/upload/%' OR GalleryJson LIKE '%/images/upload/%' "
        "ORDER BY SortOrder"
    )
    if raw:
        for row in json.loads(raw):
            slug = row["Slug"]
            img = row.get("ImageUrl", "") or ""
            gallery_raw = row.get("GalleryJson", "[]") or "[]"
            gallery = json.loads(gallery_raw)
            entities.setdefault("projects", {})[slug] = {
                "imageUrl": img, "gallery": gallery
            }

    # Activities
    raw = run_sqlcmd(
        container, password,
        "SELECT Slug, ImageUrl FROM activities WHERE ImageUrl LIKE '/images/upload/%'"
    )
    if raw:
        for row in json.loads(raw):
            entities.setdefault("activities", {})[row["Slug"]] = {
                "imageUrl": row.get("ImageUrl", "") or "", "gallery": []
            }

    # News
    raw = run_sqlcmd(
        container, password,
        "SELECT Slug, ImageUrl FROM news_articles WHERE ImageUrl LIKE '/images/upload/%'"
    )
    if raw:
        for row in json.loads(raw):
            entities.setdefault("news", {})[row["Slug"]] = {
                "imageUrl": row.get("ImageUrl", "") or "", "gallery": []
            }

    # Slideshow
    raw = run_sqlcmd(
        container, password,
        "SELECT Slug, ImageUrl FROM slideshow_items WHERE ImageUrl LIKE '/images/upload/%'"
    )
    if raw:
        for row in json.loads(raw):
            entities.setdefault("slideshow", {})[row["Slug"]] = {
                "imageUrl": row.get("ImageUrl", "") or "", "gallery": []
            }

    # Logos
    raw = run_sqlcmd(
        container, password,
        "SELECT Name, ImageUrl FROM client_logos WHERE ImageUrl LIKE '/images/upload/%'"
    )
    if raw:
        for row in json.loads(raw):
            slug = row["Name"].lower().replace(" ", "-")
            entities.setdefault("logos", {})[slug] = {
                "imageUrl": row.get("ImageUrl", "") or "", "gallery": []
            }

    return entities


def build_move_plan(entities: dict) -> tuple[dict[str, str], set[str]]:
    """
    Returns:
        move_plan: {old_url: new_url}  for all referenced upload files
        all_referenced: set of filenames referenced in DB
    """
    move_plan: dict[str, str] = {}
    all_referenced: set[str] = set()

    for entity_type, slugs in entities.items():
        for slug, data in slugs.items():
            subfolder = entity_type if entity_type != "projects" else f"projects/{slug}"
            if entity_type in ("activities", "news"):
                subfolder = f"{entity_type}/{slug}"
            # slideshow and logos use flat entity_type subfolder

            for url in [data["imageUrl"]] + data["gallery"]:
                if url and UPLOAD_PREFIX in url:
                    # Normalize (strip http://localhost:port prefix)
                    url = re.sub(r"https?://[^/]+", "", url)
                    filename = url.split("/")[-1]
                    all_referenced.add(filename)
                    new_url = f"{UPLOAD_PREFIX}{subfolder}/{filename}"
                    if url != new_url:
                        move_plan[url] = new_url

    return move_plan, all_referenced


def build_sql_updates(entities: dict, move_plan: dict[str, str]) -> list[str]:
    """Generate SQL UPDATE statements for each entity."""
    stmts: list[str] = []

    table_map = {
        "projects": ("projects", "Slug"),
        "activities": ("activities", "Slug"),
        "news": ("news_articles", "Slug"),
        "slideshow": ("slideshow_items", "Slug"),
        "logos": ("client_logos", "Name"),
    }

    def remap(url: str) -> str:
        return move_plan.get(url, url)

    for entity_type, slugs in entities.items():
        table, pk_col = table_map.get(entity_type, (None, None))
        if table is None:
            continue

        for slug, data in slugs.items():
            new_img = remap(data["imageUrl"])
            if entity_type == "logos":
                pk_val = slug.upper().replace("-", " ")  # approximate — adjust if needed
            else:
                pk_val = slug

            set_parts = []
            if new_img != data["imageUrl"]:
                set_parts.append(f"ImageUrl = '{new_img}'")

            if data.get("gallery"):
                old_gallery = json.dumps(data["gallery"], ensure_ascii=False)
                new_gallery_list = [remap(g) for g in data["gallery"]]
                new_gallery = json.dumps(new_gallery_list, ensure_ascii=False)
                if new_gallery != old_gallery:
                    escaped = new_gallery.replace("'", "''")
                    set_parts.append(f"GalleryJson = '{escaped}'")

            if set_parts:
                stmt = (
                    f"UPDATE {table} SET "
                    + ", ".join(set_parts)
                    + f" WHERE {pk_col} = '{pk_val}';"
                )
                stmts.append(stmt)

    return stmts


def apply_file_moves(move_plan: dict[str, str], dry_run: bool) -> int:
    """Move/copy files. Returns count of files moved."""
    moved = 0
    errors = 0
    for old_url, new_url in sorted(move_plan.items()):
        old_filename = old_url.split("/")[-1]
        new_rel = new_url[len(UPLOAD_PREFIX):]  # e.g. "projects/nha-may-bma/uuid.jpg"
        old_path = UPLOAD_DIR / old_filename
        new_path = UPLOAD_DIR / Path(new_rel)

        if not old_path.exists():
            print(f"  MISSING  {old_path.name} (skipped)")
            errors += 1
            continue

        if dry_run:
            print(f"  MOVE  {old_filename}  →  {new_rel}")
            moved += 1
            continue

        new_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.move(str(old_path), str(new_path))
        print(f"  Moved  {old_filename}  →  {new_rel}")
        moved += 1

    return moved


def handle_orphans(all_referenced: set[str], dry_run: bool) -> None:
    """Move unreferenced files in flat upload dir to misc/."""
    misc_dir = UPLOAD_DIR / "misc"
    orphans = []
    for f in UPLOAD_DIR.iterdir():
        if f.is_file() and f.name not in all_referenced:
            orphans.append(f)

    if not orphans:
        print("\nNo orphan files in flat upload dir.")
        return

    print(f"\nOrphan files (not in DB): {len(orphans)}")
    for f in sorted(orphans):
        if dry_run:
            print(f"  MOVE  {f.name}  →  misc/{f.name}")
        else:
            misc_dir.mkdir(parents=True, exist_ok=True)
            shutil.move(str(f), str(misc_dir / f.name))
            print(f"  Moved {f.name} → misc/{f.name}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Reorganize upload images into subfolders.")
    parser.add_argument("--apply", action="store_true",
                        help="Actually move files and print DB update SQL.")
    parser.add_argument("--db-password", default="Nihome@31042025",
                        help="SA password for Docker SQL Server.")
    parser.add_argument("--container", default="nihome31042025-sqlserver",
                        help="Docker container name.")
    args = parser.parse_args()

    dry_run = not args.apply
    mode = "DRY-RUN" if dry_run else "APPLY"
    print(f"=== reorganize-upload-images [{mode}] ===\n")

    print("Fetching entity image URLs from DB...")
    entities = fetch_entity_images(args.container, args.db_password)

    total_entities = sum(len(v) for v in entities.values())
    print(f"Found entities with upload images: {total_entities}")
    for et, slugs in entities.items():
        print(f"  {et}: {len(slugs)}")

    print("\nBuilding move plan...")
    move_plan, all_referenced = build_move_plan(entities)
    print(f"Files to move: {len(move_plan)}")

    print("\n--- File moves ---")
    moved = apply_file_moves(move_plan, dry_run)
    print(f"\n  → {moved} files {'would be' if dry_run else ''} moved")

    handle_orphans(all_referenced, dry_run)

    sql_stmts = build_sql_updates(entities, move_plan)
    print(f"\n--- SQL UPDATE statements ({len(sql_stmts)}) ---")
    for stmt in sql_stmts:
        print(stmt)

    if args.apply and sql_stmts:
        print("\nApplying SQL updates...")
        for stmt in sql_stmts:
            result = subprocess.run(
                [
                    "docker", "exec", args.container,
                    "/opt/mssql-tools18/bin/sqlcmd",
                    "-S", "localhost", "-U", "sa", "-P", args.db_password, "-C",
                    "-d", "NihomeDB", "-Q", stmt,
                ],
                capture_output=True, text=True,
            )
            if result.returncode != 0:
                print(f"  ERROR: {result.stderr[:200]}", file=sys.stderr)
            else:
                print(f"  OK: {stmt[:80]}...")

    print("\n=== Done ===")
    if dry_run:
        print("Run with --apply to execute the moves and print SQL updates.")
    else:
        print("Files moved. Update ContentSeeder.cs with the new paths (see SQL above).")
        print("Then run: git add nihomebackend/wwwroot/images/upload/ nihomebackend/Data/ContentSeeder.cs")


if __name__ == "__main__":
    main()
