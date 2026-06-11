#!/usr/bin/env python3
"""Offline WorkProcesses scraper for the old nicon.vn admin pages.

This is intentionally outside the ASP.NET runtime. It downloads legacy process
assets into static storage and writes seed metadata to Data/Seeds/processes.json.
"""

from __future__ import annotations

import argparse
import html
import http.cookiejar
import json
import mimetypes
import os
import re
import sys
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import unquote, urljoin, urlparse
from urllib.request import HTTPCookieProcessor, Request, build_opener


LEGACY_PAGES = [
    ("general", "GeneralProcess"),
    ("ptcskh", "PTCSKHProcess"),
    ("dt", "DTProcess"),
    ("tk", "TKProcess"),
    ("tc", "TCProcess"),
    ("ttqtct", "TTQTCTProcess"),
    ("qlns", "QLNSProcess"),
    ("mhdgncu", "MHDGNCUProcess"),
]

SHOW_HIDE_TITLE_RE = re.compile(
    r'<a\s+class="showhideTable"[^>]*>\s*(.*?)\s*</a>',
    re.IGNORECASE | re.DOTALL,
)
H2_RE = re.compile(r"<h2[^>]*>\s*(.*?)\s*</h2>", re.IGNORECASE | re.DOTALL)
IMAGE_RE = re.compile(r'<img\s+[^>]*src="([^"]+)"', re.IGNORECASE)
TABLE_ROW_RE = re.compile(r"<tr[^>]*>(.*?)</tr>", re.IGNORECASE | re.DOTALL)
TABLE_CELL_RE = re.compile(r"<td[^>]*>(.*?)</td>", re.IGNORECASE | re.DOTALL)
HREF_RE = re.compile(r'href="([^"]+)"', re.IGNORECASE)
TAG_RE = re.compile(r"<.*?>", re.DOTALL)
WHITESPACE_RE = re.compile(r"\s+")


@dataclass
class ParsedAsset:
    display_name: str
    url: str
    sort_order: int


@dataclass
class ParsedProcess:
    title: str
    code: str | None
    images: list[ParsedAsset] = field(default_factory=list)
    files: list[ParsedAsset] = field(default_factory=list)


def clean_text(value: str) -> str:
    return WHITESPACE_RE.sub(" ", html.unescape(TAG_RE.sub("", value))).strip()


def clean_file_name(value: str | None, fallback: str) -> str:
    value = html.unescape(value or "").strip()
    return value if value else fallback


def is_downloadable_url(url: str) -> bool:
    if not url or url.startswith("#"):
        return False
    parsed = urlparse(url)
    if parsed.scheme:
        return parsed.scheme in ("http", "https")
    return not url.lower().startswith(("javascript:", "mailto:"))


def extract_code(group_key: str, title: str) -> str | None:
    if group_key.lower() != "tc":
        return None
    dot_index = title.find(".")
    if dot_index < 0 or dot_index > 3:
        return None
    return title[:dot_index].strip()


def parse_process_segment(title: str, segment: str) -> ParsedProcess:
    images: list[ParsedAsset] = []
    for match in IMAGE_RE.finditer(segment):
        src = html.unescape(match.group(1))
        if not is_downloadable_url(src):
            continue
        if "/Themes/Nicon/Content/file/" not in src:
            continue
        images.append(
            ParsedAsset(
                clean_file_name(Path(urlparse(src).path).name, f"process-image-{len(images) + 1}.jpg"),
                src,
                len(images),
            )
        )

    files: list[ParsedAsset] = []
    for row in TABLE_ROW_RE.finditer(segment):
        row_html = row.group(1)
        cells = list(TABLE_CELL_RE.finditer(row_html))
        href = HREF_RE.search(row_html)
        if not cells or not href:
            continue

        url = html.unescape(href.group(1))
        if not is_downloadable_url(url):
            continue

        display_name = clean_text(cells[0].group(1))
        if not display_name:
            display_name = clean_file_name(Path(urlparse(url).path).name, f"process-file-{len(files) + 1}")
        files.append(ParsedAsset(display_name, url, len(files)))

    return ParsedProcess(title, None, images, files)


def parse_legacy_page(group_key: str, page_name: str, page_html: str) -> list[ParsedProcess]:
    title_matches = list(SHOW_HIDE_TITLE_RE.finditer(page_html))
    processes: list[ParsedProcess] = []

    if title_matches:
        for index, match in enumerate(title_matches):
            title = clean_text(match.group(1))
            start = match.end()
            end = title_matches[index + 1].start() if index + 1 < len(title_matches) else len(page_html)
            processes.append(parse_process_segment(title, page_html[start:end]))
    else:
        heading = H2_RE.search(page_html)
        title = clean_text(heading.group(1)) if heading else page_name
        start = heading.end() if heading else 0
        footer_index = page_html.lower().find('<div class="main-footer', start)
        end = footer_index if footer_index >= 0 else len(page_html)
        processes.append(parse_process_segment(title, page_html[start:end]))

    return [
        ParsedProcess(p.title, extract_code(group_key, p.title), p.images, p.files)
        for p in processes
        if p.images or p.files
    ]


def request_text(opener, url: str) -> str:
    request = Request(url, headers={"User-Agent": "NihomeWorkProcessOfflineScraper/1.0"})
    with opener.open(request, timeout=120) as response:
        return response.read().decode("utf-8", errors="replace")


def login(opener, base_url: str, email: str, password: str) -> None:
    from urllib.parse import urlencode

    login_url = urljoin(base_url, "/login?ReturnUrl=%2fAdmin%2fGeneralProcess")
    opener.open(Request(login_url, headers={"User-Agent": "NihomeWorkProcessOfflineScraper/1.0"}), timeout=120)

    payload = urlencode({"Email": email, "Password": password, "RememberMe": "true"}).encode()
    request = Request(
        login_url,
        data=payload,
        headers={
            "Content-Type": "application/x-www-form-urlencoded",
            "User-Agent": "NihomeWorkProcessOfflineScraper/1.0",
        },
    )
    with opener.open(request, timeout=120) as response:
        body = response.read().decode("utf-8", errors="replace")

    if "html-login-page" in body.lower() or 'class="login-page"' in body.lower():
        raise RuntimeError("Legacy process login failed.")


def resolve_url(base_url: str, url: str) -> str | None:
    parsed = urlparse(url)
    if parsed.scheme:
        return url if parsed.scheme in ("http", "https") else None
    return urljoin(base_url, url)


def infer_extension(display_name: str, url: str, content_type: str | None) -> str:
    extension = Path(unquote(display_name)).suffix or Path(unquote(urlparse(url).path)).suffix
    if extension:
        return extension
    if content_type:
        return mimetypes.guess_extension(content_type.split(";", 1)[0].strip()) or ".bin"
    return ".bin"


def download_asset(opener, base_url: str, asset: ParsedAsset, asset_type: str, asset_root: Path) -> dict | None:
    url = resolve_url(base_url, asset.url)
    if not url:
        print(f"skip unsupported URL: {asset.url}", file=sys.stderr)
        return None

    try:
        request = Request(url, headers={"User-Agent": "NihomeWorkProcessOfflineScraper/1.0"})
        with opener.open(request, timeout=180) as response:
            content_type = response.headers.get("Content-Type")
            body = response.read()
    except (HTTPError, URLError, TimeoutError) as exc:
        print(f"skip failed download: {asset.display_name} ({asset.url}) -> {exc}", file=sys.stderr)
        return None

    target_folder = "images" if asset_type == "image" else "files"
    extension = infer_extension(asset.display_name, url, content_type)
    target_dir = asset_root / target_folder
    target_dir.mkdir(parents=True, exist_ok=True)
    file_name = f"{uuid.uuid4().hex}{extension}"
    target_path = target_dir / file_name
    target_path.write_bytes(body)

    return {
        "type": asset_type,
        "displayName": asset.display_name,
        "url": f"/process-assets/{target_folder}/{file_name}",
        "originalFileName": clean_file_name(asset.display_name, file_name),
        "contentType": content_type,
        "fileSizeBytes": len(body),
        "sortOrder": asset.sort_order,
    }


def build_manifest(opener, base_url: str, asset_root: Path, dry_run: bool) -> list[dict]:
    manifest: list[dict] = []
    for group_key, page_name in LEGACY_PAGES:
        page_html = request_text(opener, urljoin(base_url, f"/Admin/{page_name}"))
        processes = parse_legacy_page(group_key, page_name, page_html)
        print(f"{page_name}: {len(processes)} processes", file=sys.stderr)

        for sort_order, process in enumerate(processes):
            item = {
                "groupKey": group_key,
                "code": process.code,
                "title": process.title,
                "sortOrder": sort_order,
                "images": [],
                "files": [],
            }

            if not dry_run:
                item["images"] = [
                    stored
                    for asset in process.images
                    if (stored := download_asset(opener, base_url, asset, "image", asset_root))
                ]
                item["files"] = [
                    stored
                    for asset in process.files
                    if (stored := download_asset(opener, base_url, asset, "file", asset_root))
                ]

            manifest.append(item)

    return manifest


def main() -> int:
    parser = argparse.ArgumentParser(description="Scrape legacy WorkProcesses into offline assets and seed JSON.")
    parser.add_argument("--base-url", default=os.getenv("NICON_LEGACY_BASE_URL", "https://nicon.vn"))
    parser.add_argument("--email", default=os.getenv("NICON_LEGACY_EMAIL"))
    parser.add_argument("--password", default=os.getenv("NICON_LEGACY_PASSWORD"))
    parser.add_argument("--asset-root", type=Path, default=Path("nihomebackend/wwwroot/process-assets"))
    parser.add_argument("--manifest", type=Path, default=Path("nihomebackend/Data/Seeds/processes.json"))
    parser.add_argument("--dry-run", action="store_true", help="Parse pages and counts without downloading files.")
    args = parser.parse_args()

    if not args.email or not args.password:
        raise SystemExit("Set NICON_LEGACY_EMAIL and NICON_LEGACY_PASSWORD, or pass --email/--password.")

    base_url = args.base_url.rstrip("/") + "/"
    opener = build_opener(HTTPCookieProcessor(http.cookiejar.CookieJar()))
    login(opener, base_url, args.email, args.password)
    manifest = build_manifest(opener, base_url, args.asset_root, args.dry_run)

    image_count = sum(len(item["images"]) for item in manifest)
    file_count = sum(len(item["files"]) for item in manifest)
    print(f"total: {len(manifest)} processes, {image_count} images, {file_count} files", file=sys.stderr)

    if not args.dry_run:
        args.manifest.parent.mkdir(parents=True, exist_ok=True)
        args.manifest.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
