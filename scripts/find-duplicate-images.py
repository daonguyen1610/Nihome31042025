#!/usr/bin/env python3
"""
find-duplicate-images.py
------------------------
Scans nihomebackend/wwwroot/images/ for duplicate image files.

Duplicates are identified by MD5 content hash (file size is used as a
fast pre-filter to avoid hashing clearly different files).

Usage:
    python3 scripts/find-duplicate-images.py [--delete] [--dir <path>]

Options:
    --delete    Delete duplicate files (keeps the FIRST occurrence
                sorted by full path, so deterministic).
    --dir       Directory to scan (default: nihomebackend/wwwroot/images).

The report groups duplicates together so you can see which files
are identical before deciding whether to delete them.
"""

import argparse
import hashlib
import os
import sys
from collections import defaultdict
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DIR = REPO_ROOT / "nihomebackend" / "wwwroot" / "images"
IMAGE_EXTENSIONS = {".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".avif"}


def file_md5(path: Path) -> str:
    h = hashlib.md5()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def scan(directory: Path) -> dict[str, list[Path]]:
    """Return {md5: [path, ...]} for all image files in directory."""
    # Step 1: group by file size (cheap pre-filter)
    by_size: dict[int, list[Path]] = defaultdict(list)
    total = 0
    for root, _, files in os.walk(directory):
        for name in files:
            p = Path(root) / name
            if p.suffix.lower() in IMAGE_EXTENSIONS:
                by_size[p.stat().st_size].append(p)
                total += 1

    print(f"Scanned {total} image files in {directory}")

    # Step 2: for size-collision groups, compute MD5
    by_hash: dict[str, list[Path]] = defaultdict(list)
    candidates = sum(len(v) for v in by_size.values() if len(v) > 1)
    print(f"Files sharing a size with at least one other file: {candidates}")

    for size, paths in by_size.items():
        if len(paths) == 1:
            continue  # unique size → can't be a duplicate
        for p in paths:
            digest = file_md5(p)
            by_hash[digest].append(p)

    return by_hash


def main() -> None:
    parser = argparse.ArgumentParser(description="Find duplicate images by MD5 hash.")
    parser.add_argument("--delete", action="store_true", help="Delete duplicates (keeps first by path).")
    parser.add_argument("--dir", type=Path, default=DEFAULT_DIR, help="Directory to scan.")
    args = parser.parse_args()

    scan_dir: Path = args.dir.resolve()
    if not scan_dir.is_dir():
        print(f"Error: {scan_dir} is not a directory.", file=sys.stderr)
        sys.exit(1)

    by_hash = scan(scan_dir)

    duplicate_groups = {h: paths for h, paths in by_hash.items() if len(paths) > 1}

    if not duplicate_groups:
        print("\nNo duplicate images found.")
        return

    total_dupes = sum(len(v) - 1 for v in duplicate_groups.values())
    total_bytes = sum((len(v) - 1) * v[0].stat().st_size for v in duplicate_groups.values())

    print(f"\nFound {len(duplicate_groups)} duplicate groups "
          f"({total_dupes} redundant files, "
          f"{total_bytes / 1024 / 1024:.1f} MB wasted)\n")

    for i, (digest, paths) in enumerate(sorted(duplicate_groups.items()), 1):
        paths_sorted = sorted(paths)
        size_kb = paths_sorted[0].stat().st_size / 1024
        print(f"Group {i} — MD5: {digest}  size: {size_kb:.0f} KB")
        for j, p in enumerate(paths_sorted):
            rel = p.relative_to(scan_dir)
            marker = "KEEP" if j == 0 else "DUPE"
            print(f"  [{marker}]  {rel}")
        print()

    if args.delete:
        deleted = 0
        freed = 0
        for digest, paths in duplicate_groups.items():
            paths_sorted = sorted(paths)
            for p in paths_sorted[1:]:  # keep first, delete rest
                size = p.stat().st_size
                p.unlink()
                print(f"Deleted: {p}")
                deleted += 1
                freed += size
        print(f"\nDeleted {deleted} files, freed {freed / 1024 / 1024:.1f} MB")
    else:
        print("Run with --delete to remove duplicate files (keeps the first path in each group).")


if __name__ == "__main__":
    main()
