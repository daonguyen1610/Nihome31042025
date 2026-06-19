#!/usr/bin/env python3
"""End-to-end legacy nicon.vn importer.

Single entrypoint that runs the full pipeline:

  1. scrape  — pull activities / news / projects (vi + en) and the admin
               WorkProcesses pages into seed JSON + asset binaries
  2. migrate — move any binaries that landed in `wwwroot/images/upload/`
               into their owning slug folders so /upload/ stays empty for
               runtime user uploads
  3. clean   — content-hash dedupe across `wwwroot/images/`, drop the
               legacy `wwwroot/processes/` folder, rewrite manifests +
               ContentSeeder.cs to point at canonical paths

Usage (run from repo root):

  python3 tools/scrape_legacy_data/run.py                       # full pipeline
  python3 tools/scrape_legacy_data/run.py --no-clean            # scrape only
  python3 tools/scrape_legacy_data/run.py --skip-scrape         # dedupe only
  python3 tools/scrape_legacy_data/run.py --target processes    # admin pages
  python3 tools/scrape_legacy_data/run.py --dry-run             # parse, no I/O
"""

from __future__ import annotations

import argparse
import hashlib
import html
import http.cookiejar
import json
import mimetypes
import os
import re
import shutil
import subprocess
import sys
import unicodedata
import uuid
from collections import defaultdict
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable
from urllib.error import HTTPError, URLError
from urllib.parse import quote, unquote, urljoin, urlparse, urlencode
from urllib.request import HTTPCookieProcessor, Request, build_opener


# ─── Repo paths ─────────────────────────────────────────────────────────────

REPO = Path(__file__).resolve().parents[2]
WWWROOT = REPO / "nihomebackend" / "wwwroot"
IMAGES_ROOT = WWWROOT / "images"
UPLOAD_DIR = IMAGES_ROOT / "upload"
SEEDS_ROOT = REPO / "nihomebackend" / "Data" / "Seeds"
CONTENT_SEEDS_ROOT = SEEDS_ROOT / "content"
SEEDER_CS = REPO / "nihomebackend" / "Data" / "ContentSeeder.cs"

PUBLIC_MANIFESTS = {
    "activities": CONTENT_SEEDS_ROOT / "activities.json",
    "news": CONTENT_SEEDS_ROOT / "news.json",
    "projects": CONTENT_SEEDS_ROOT / "projects.json",
}


# ─── HTTP helpers ────────────────────────────────────────────────────────────

USER_AGENT = "NihomeLegacyScraper/2.0"
LANGUAGE_IDS = {"en": 1, "vi": 2}  # Empirically determined on nicon.vn


def http_get(opener, url: str, timeout: int = 120) -> str:
    request = Request(url, headers={"User-Agent": USER_AGENT})
    with opener.open(request, timeout=timeout) as response:
        return response.read().decode("utf-8", errors="replace")


def http_get_bytes(opener, url: str, timeout: int = 180) -> tuple[bytes, str | None]:
    request = Request(url, headers={"User-Agent": USER_AGENT})
    with opener.open(request, timeout=timeout) as response:
        return response.read(), response.headers.get("Content-Type")


def make_opener() -> tuple:
    jar = http.cookiejar.CookieJar()
    return build_opener(HTTPCookieProcessor(jar)), jar


def switch_language(opener, base_url: str, lang_code: str) -> None:
    """Set ASP.NET session language cookie via /changelanguage/{id}."""
    lang_id = LANGUAGE_IDS[lang_code]
    url = urljoin(base_url, f"/changelanguage/{lang_id}?returnurl=%2f")
    http_get(opener, url)


# ─── HTML utilities ──────────────────────────────────────────────────────────

TAG_RE = re.compile(r"<.*?>", re.DOTALL)
WHITESPACE_RE = re.compile(r"\s+")
HREF_RE = re.compile(r'href="([^"]+)"', re.IGNORECASE)
IMAGE_RE = re.compile(r'<img\s+[^>]*src="([^"]+)"', re.IGNORECASE)


def strip_html(value: str) -> str:
    return WHITESPACE_RE.sub(" ", html.unescape(TAG_RE.sub("", value))).strip()


def extract_paragraphs(body_html: str) -> list[str]:
    """Convert a chunk of legacy HTML into a list of plain-text paragraphs."""
    # Drop script/style.
    body_html = re.sub(r"<script.*?</script>", "", body_html, flags=re.S | re.I)
    body_html = re.sub(r"<style.*?</style>", "", body_html, flags=re.S | re.I)

    # Split on common block-level boundaries before stripping tags.
    chunks = re.split(r"</?(?:p|div|br|li|h[1-6])[^>]*>", body_html, flags=re.I)
    paragraphs: list[str] = []
    for chunk in chunks:
        text = strip_html(chunk)
        if text:
            paragraphs.append(text)
    return paragraphs


def find_div_block(html_text: str, class_name: str, start: int = 0) -> tuple[int, int] | None:
    """Return (inner_start, inner_end) of the first <div class="class_name"> at or
    after `start`, walking nested <div> tags to find the matching close."""
    pat = re.compile(r'<div[^>]*class="' + re.escape(class_name) + r'"[^>]*>', re.I)
    match = pat.search(html_text, start)
    if not match:
        return None
    inner_start = match.end()
    depth = 1
    i = inner_start
    open_re = re.compile(r"<div\b", re.I)
    close_re = re.compile(r"</div\s*>", re.I)
    while i < len(html_text):
        op = open_re.search(html_text, i)
        cl = close_re.search(html_text, i)
        if not cl:
            break
        if op and op.start() < cl.start():
            depth += 1
            i = op.end()
        else:
            depth -= 1
            if depth == 0:
                return inner_start, cl.start()
            i = cl.end()
    return None


def slice_div(html_text: str, class_name: str) -> str | None:
    span = find_div_block(html_text, class_name)
    if not span:
        return None
    return html_text[span[0]:span[1]]


# ─── Slug helpers ────────────────────────────────────────────────────────────

def ascii_slug(value: str) -> str:
    """Generate a clean ASCII slug (used to namespace asset folders)."""
    normalised = unicodedata.normalize("NFKD", value)
    stripped = "".join(c for c in normalised if not unicodedata.combining(c))
    stripped = stripped.replace("đ", "d").replace("Đ", "D")
    stripped = re.sub(r"[^A-Za-z0-9]+", "-", stripped).strip("-").lower()
    # Reject slugs that collapsed to just digits (e.g. legacy "/-2") — they
    # produce useless folder names like "2/". Prefix with "item-" so the slug
    # remains stable across reruns but is at least visually meaningful.
    if not stripped or stripped.isdigit():
        stripped = f"item-{stripped or 'unknown'}"
    return stripped


# ─── Asset download ──────────────────────────────────────────────────────────

def infer_extension(display_name: str, url: str, content_type: str | None) -> str:
    extension = Path(unquote(display_name)).suffix or Path(unquote(urlparse(url).path)).suffix
    if extension:
        return extension
    if content_type:
        guessed = mimetypes.guess_extension(content_type.split(";", 1)[0].strip())
        if guessed:
            return guessed
    return ".bin"


def is_downloadable_url(url: str) -> bool:
    if not url or url.startswith("#"):
        return False
    parsed = urlparse(url)
    if parsed.scheme:
        return parsed.scheme in ("http", "https")
    return not url.lower().startswith(("javascript:", "mailto:"))


def resolve_url(base_url: str, url: str) -> str | None:
    parsed = urlparse(url)
    if parsed.scheme:
        if parsed.scheme not in ("http", "https"):
            return None
        absolute = url
    else:
        absolute = urljoin(base_url, url)
    # Re-quote the path component so spaces / unicode are valid HTTP characters.
    parts = urlparse(absolute)
    safe_path = quote(parts.path, safe="/%")
    return parts._replace(path=safe_path).geturl()


def download_to(opener, base_url: str, url: str, target_dir: Path,
                file_stem: str | None = None,
                skip_if_exists: bool = True) -> tuple[Path, str | None] | None:
    full_url = resolve_url(base_url, url)
    if not full_url:
        return None
    extension_hint: str | None = None
    if skip_if_exists and file_stem:
        # When a stable stem is provided, prefer the cached binary if any
        # extension matches — saves bandwidth on repeated runs.
        for candidate in target_dir.glob(f"{file_stem}.*"):
            if candidate.is_file():
                return candidate, None
    try:
        body, content_type = http_get_bytes(opener, full_url)
    except (HTTPError, URLError, TimeoutError) as exc:
        print(f"[skip] {url} -> {exc}", file=sys.stderr)
        return None
    target_dir.mkdir(parents=True, exist_ok=True)
    extension = infer_extension(Path(urlparse(full_url).path).name, full_url, content_type)
    name = f"{file_stem or uuid.uuid4().hex}{extension}"
    target_path = target_dir / name
    target_path.write_bytes(body)
    return target_path, content_type


# ─── Public listing scraper ──────────────────────────────────────────────────

@dataclass
class ListingCard:
    slug: str
    title: str
    excerpt: str
    thumbnail: str | None
    date: str | None


CARD_LINK_RE_TEMPLATE = r'<a[^>]+href="/{section}/([^"]+)"[^>]*>(.*?)</a>'


def parse_listing(html_text: str, section: str) -> list[ListingCard]:
    """Parse a listing page (activities/news) into cards."""
    cards: list[ListingCard] = []
    seen: set[str] = set()
    container = html_text

    # The listing markup wraps each item in an <article> or repeated <div>; instead
    # of relying on a fragile container, we identify each unique slug and walk back
    # from the first link occurrence to gather the surrounding card content.
    link_pat = re.compile(rf'<a[^>]+href="/{section}/([^"#?]+)"', re.I)
    matches = list(link_pat.finditer(container))
    if not matches:
        return cards

    # Determine card boundaries as the spans between unique slug starts.
    boundaries: list[tuple[str, int, int]] = []
    used_positions: list[int] = []
    for m in matches:
        slug = m.group(1)
        if slug in seen:
            continue
        seen.add(slug)
        boundaries.append((slug, m.start(), -1))
        used_positions.append(m.start())
    used_positions.append(len(container))
    for idx, _ in enumerate(boundaries):
        slug, start, _ = boundaries[idx]
        end = used_positions[idx + 1]
        boundaries[idx] = (slug, start, end)

    for slug, start, end in boundaries:
        block_start = max(0, start - 800)
        block = container[block_start:end]

        title = ""
        excerpt = ""
        thumbnail: str | None = None
        date: str | None = None

        # Title: first anchor text inside the block linking to this slug
        title_match = re.search(
            rf'<a[^>]+href="/{section}/{re.escape(slug)}"[^>]*>(.*?)</a>',
            block,
            re.S | re.I,
        )
        if title_match:
            title = strip_html(title_match.group(1))

        # Date: <div class="date">DD/MM/YYYY</div>
        date_match = re.search(r'<div\s+class="date"[^>]*>\s*(\d{2}/\d{2}/\d{4})\s*</div>', block, re.I)
        if date_match:
            date = date_match.group(1)

        # Thumbnail: first <img> inside the block
        img_match = IMAGE_RE.search(block)
        if img_match:
            thumbnail = html.unescape(img_match.group(1))

        # Excerpt: content-text or subtitle
        excerpt_block = slice_div(block, "content-text")
        if not excerpt_block:
            excerpt_block = slice_div(block, "subtitle achievements-lists")
        if excerpt_block:
            paragraphs = extract_paragraphs(excerpt_block)
            excerpt = " ".join(paragraphs[:2])
            excerpt = WHITESPACE_RE.sub(" ", excerpt).strip()

        if not title:
            continue
        cards.append(ListingCard(slug=slug, title=title, excerpt=excerpt,
                                 thumbnail=thumbnail, date=date))
    return cards


def scrape_listing_pages(opener, base_url: str, listing_path: str,
                         section: str, max_pages: int = 30) -> list[ListingCard]:
    cards: list[ListingCard] = []
    seen: set[str] = set()
    page = 1
    while page <= max_pages:
        url = urljoin(base_url, f"/{listing_path}?page={page}") if page > 1 else urljoin(base_url, f"/{listing_path}")
        html_text = http_get(opener, url)
        page_cards = parse_listing(html_text, section)
        new_cards = [c for c in page_cards if c.slug not in seen]
        if not new_cards:
            break
        for card in new_cards:
            seen.add(card.slug)
            cards.append(card)
        page += 1
    return cards


# ─── Detail page parser ─────────────────────────────────────────────────────

@dataclass
class DetailContent:
    title: str
    date: str | None
    paragraphs: list[str]
    image_urls: list[str]


def parse_detail(html_text: str) -> DetailContent | None:
    title_block = slice_div(html_text, "page-title")
    date_block = slice_div(html_text, "news-date")

    body_block = None
    for cls in ("news-body activities", "news-body projectsUnderConstruction",
                "news-body construction", "news-body"):
        body_block = slice_div(html_text, cls)
        if body_block:
            break
    if body_block is None:
        return None

    title = strip_html(title_block) if title_block else ""
    date = strip_html(date_block) if date_block else None

    image_urls: list[str] = []
    for m in IMAGE_RE.finditer(body_block):
        src = html.unescape(m.group(1))
        if is_downloadable_url(src):
            image_urls.append(src)

    paragraphs = extract_paragraphs(body_block)
    return DetailContent(title=title, date=date, paragraphs=paragraphs, image_urls=image_urls)


# ─── Section orchestration (activities / news / projects) ───────────────────

@dataclass
class SectionConfig:
    name: str                # "activities", "news", "projects"
    listing_paths: list[tuple[str, str | None]]  # (path, status_label)
    detail_section: str      # detail URL prefix segment, e.g. "activities", "projectsongoing"
    output_subdir: str       # wwwroot/images/<output_subdir>
    output_manifest: str     # Data/Seeds/<file>


SECTIONS: dict[str, SectionConfig] = {
    "activities": SectionConfig(
        name="activities",
        listing_paths=[("activities", None)],
        detail_section="activities",
        output_subdir="activities",
        output_manifest="activities.json",
    ),
    "news": SectionConfig(
        name="news",
        listing_paths=[("news", None)],
        detail_section="news",
        output_subdir="news",
        output_manifest="news.json",
    ),
    "projects": SectionConfig(
        name="projects",
        listing_paths=[("projectsongoing", "ongoing"), ("projectscompleted", "completed")],
        detail_section="projects",  # handled per listing because URL differs
        output_subdir="projects",
        output_manifest="projects.json",
    ),
}


def _pair_by_date(vi_cards: list[ListingCard],
                  en_cards: list[ListingCard]) -> list[tuple[ListingCard, ListingCard | None]]:
    """Pair VI ↔ EN cards by their listing date.

    The legacy CMS preserves an article's date across languages but assigns
    different slugs per language. Pairing on date works well when each date
    has at most one item per language. When a date has multiple items in
    either language we cannot safely match individual items, so we emit
    `None` for the English partner rather than risk a wrong translation.
    """

    en_by_date: dict[str, list[ListingCard]] = {}
    for card in en_cards:
        en_by_date.setdefault(card.date or "", []).append(card)
    vi_by_date: dict[str, int] = {}
    for card in vi_cards:
        key = card.date or ""
        vi_by_date[key] = vi_by_date.get(key, 0) + 1

    pairs: list[tuple[ListingCard, ListingCard | None]] = []
    for vi_card in vi_cards:
        key = vi_card.date or ""
        bucket = en_by_date.get(key) or []
        en_card: ListingCard | None = None
        if vi_by_date.get(key, 0) == 1 and len(bucket) == 1:
            en_card = bucket[0]
        pairs.append((vi_card, en_card))
    return pairs


def _pair_by_images(vi_details: list[DetailContent | None],
                    en_cards: list[ListingCard],
                    en_details: list[DetailContent | None]
                    ) -> list[ListingCard | None]:
    """Pair every VI item to its best-matching EN item via shared image URLs.

    The legacy CMS uses a single asset library across languages, so the same
    article references the same `/Content/Images/uploaded/...` URLs in both
    Vietnamese and English. Jaccard similarity over the image set therefore
    gives a reliable fingerprint to map items, even when the listings have
    different lengths or no per-item dates.
    """
    matched_indices: set[int] = set()
    pairings: list[ListingCard | None] = []
    for vi_detail in vi_details:
        if vi_detail is None or not vi_detail.image_urls:
            pairings.append(None)
            continue
        vi_set = set(vi_detail.image_urls)
        best_score = 0.0
        best_idx = -1
        for j, en_detail in enumerate(en_details):
            if j in matched_indices or en_detail is None or not en_detail.image_urls:
                continue
            en_set = set(en_detail.image_urls)
            union = vi_set | en_set
            if not union:
                continue
            score = len(vi_set & en_set) / len(union)
            if score > best_score:
                best_score = score
                best_idx = j
        if best_idx >= 0 and best_score >= 0.5:
            matched_indices.add(best_idx)
            pairings.append(en_cards[best_idx])
        else:
            pairings.append(None)
    return pairings


def fetch_section(opener, base_url: str, section: SectionConfig,
                  asset_root: Path, dry_run: bool) -> list[dict]:
    """Scrape a section in vi + en and return manifest items."""

    items: list[dict] = []
    asset_dir = asset_root / section.output_subdir

    sort_order_counter = 0
    for listing_path, status_label in section.listing_paths:
        # Per-listing detail prefix (project listings use a singular path segment).
        if listing_path == "projectsongoing":
            detail_prefix = "projectsongoing"
        elif listing_path == "projectscompleted":
            detail_prefix = "projectcompleted"
        else:
            detail_prefix = listing_path

        # Vietnamese pass: defines item identity, slugs, and listing dates.
        switch_language(opener, base_url, "vi")
        vi_cards = scrape_listing_pages(opener, base_url, listing_path, detail_prefix)
        print(f"[{section.name}/{listing_path}] vi cards: {len(vi_cards)}", file=sys.stderr)

        vi_details: list[DetailContent | None] = []
        for vi_card in vi_cards:
            try:
                vi_html = http_get(opener, urljoin(base_url, f"/{detail_prefix}/{vi_card.slug}"))
                vi_details.append(parse_detail(vi_html))
            except (HTTPError, URLError, TimeoutError) as exc:
                print(f"[warn] vi detail fetch failed for {vi_card.slug}: {exc}", file=sys.stderr)
                vi_details.append(None)

        # English pass: full listing + detail capture so we can fingerprint pair.
        switch_language(opener, base_url, "en")
        en_cards = scrape_listing_pages(opener, base_url, listing_path, detail_prefix)
        print(f"[{section.name}/{listing_path}] en cards: {len(en_cards)}", file=sys.stderr)

        en_details: list[DetailContent | None] = []
        for en_card in en_cards:
            try:
                en_html = http_get(opener, urljoin(base_url, f"/{detail_prefix}/{en_card.slug}"))
                en_details.append(parse_detail(en_html))
            except (HTTPError, URLError, TimeoutError) as exc:
                print(f"[warn] en detail fetch failed for {en_card.slug}: {exc}",
                      file=sys.stderr)
                en_details.append(None)

        en_pairings = _pair_by_images(vi_details, en_cards, en_details)

        # Switch back to VI for image downloads to ensure we hit the same canonical
        # asset URLs the legacy CMS resolves for VI views.
        if not dry_run:
            switch_language(opener, base_url, "vi")

        for idx, vi_card in enumerate(vi_cards):
            vi_detail = vi_details[idx]
            en_card = en_pairings[idx]
            en_detail: DetailContent | None = None
            if en_card:
                try:
                    en_detail = en_details[en_cards.index(en_card)]
                except ValueError:
                    en_detail = None
            ascii_id = ascii_slug(vi_card.slug)

            slug_dir = asset_dir / ascii_id
            relative_image_url = ""
            gallery: list[str] = []
            if not dry_run:
                wwwroot = asset_root.parent  # asset_root == <wwwroot>/images
                if vi_card.thumbnail:
                    saved = download_to(opener, base_url, vi_card.thumbnail, slug_dir, file_stem="thumb")
                    if saved:
                        path, _ = saved
                        relative_image_url = "/" + str(
                            path.relative_to(wwwroot)
                        ).replace(os.sep, "/")
                if vi_detail:
                    for i, img_url in enumerate(vi_detail.image_urls):
                        saved = download_to(opener, base_url, img_url, slug_dir,
                                            file_stem=f"img-{i + 1:02d}")
                        if saved:
                            path, _ = saved
                            rel = "/" + str(
                                path.relative_to(wwwroot)
                            ).replace(os.sep, "/")
                            if rel == relative_image_url:
                                continue
                            gallery.append(rel)

            translations: dict[str, dict] = {
                "vi": {
                    "slug": vi_card.slug,
                    "title": vi_card.title,
                    "excerpt": vi_card.excerpt,
                    "content": vi_detail.paragraphs if vi_detail else [],
                    "date": vi_detail.date if vi_detail and vi_detail.date else vi_card.date,
                }
            }
            if en_card:
                translations["en"] = {
                    "slug": en_card.slug,
                    "title": en_card.title,
                    "excerpt": en_card.excerpt,
                    "content": en_detail.paragraphs if en_detail else [],
                    "date": en_detail.date if en_detail and en_detail.date else en_card.date,
                }

            item: dict = {
                "slug": ascii_id,
                "legacySlug": vi_card.slug,
                "imageUrl": relative_image_url,
                "gallery": gallery,
                "date": vi_card.date or "",
                "category": "",
                "sortOrder": sort_order_counter,
                "translations": translations,
            }
            if status_label:
                item["status"] = status_label
            items.append(item)
            sort_order_counter += 1

    return items


# ─── WorkProcess scraper (preserved from previous version) ───────────────────

LEGACY_PROCESS_PAGES = [
    ("general", "GeneralProcess"),
    ("ptcskh", "PTCSKHProcess"),
    ("dt", "DTProcess"),
    ("tk", "TKProcess"),
    ("tc", "TCProcess"),
    ("ttqtct", "TTQTCTProcess"),
    ("qlns", "QLNSProcess"),
    ("mhdgncu", "MHDGNCUProcess"),
]

SHOW_HIDE_TITLE_RE = re.compile(r'<a\s+class="showhideTable"[^>]*>\s*(.*?)\s*</a>', re.I | re.S)
H2_RE = re.compile(r"<h2[^>]*>\s*(.*?)\s*</h2>", re.I | re.S)
TABLE_ROW_RE = re.compile(r"<tr[^>]*>(.*?)</tr>", re.I | re.S)
TABLE_CELL_RE = re.compile(r"<td[^>]*>(.*?)</td>", re.I | re.S)


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


def clean_file_name(value: str | None, fallback: str) -> str:
    value = html.unescape(value or "").strip()
    return value if value else fallback


def extract_process_code(group_key: str, title: str) -> str | None:
    if group_key.lower() != "tc":
        return None
    dot_index = title.find(".")
    if 0 <= dot_index <= 3:
        return title[:dot_index].strip()
    return None


def parse_process_segment(title: str, segment: str) -> ParsedProcess:
    images: list[ParsedAsset] = []
    for match in IMAGE_RE.finditer(segment):
        src = html.unescape(match.group(1))
        if not is_downloadable_url(src):
            continue
        if "/Themes/Nicon/Content/file/" not in src:
            continue
        images.append(ParsedAsset(
            clean_file_name(Path(urlparse(src).path).name, f"process-image-{len(images) + 1}.jpg"),
            src,
            len(images),
        ))

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
        display_name = strip_html(cells[0].group(1))
        if not display_name:
            display_name = clean_file_name(Path(urlparse(url).path).name, f"process-file-{len(files) + 1}")
        files.append(ParsedAsset(display_name, url, len(files)))

    return ParsedProcess(title, None, images, files)


def parse_process_page(group_key: str, page_name: str, page_html: str) -> list[ParsedProcess]:
    title_matches = list(SHOW_HIDE_TITLE_RE.finditer(page_html))
    processes: list[ParsedProcess] = []
    if title_matches:
        for index, match in enumerate(title_matches):
            title = strip_html(match.group(1))
            start = match.end()
            end = title_matches[index + 1].start() if index + 1 < len(title_matches) else len(page_html)
            processes.append(parse_process_segment(title, page_html[start:end]))
    else:
        heading = H2_RE.search(page_html)
        title = strip_html(heading.group(1)) if heading else page_name
        start = heading.end() if heading else 0
        footer_index = page_html.lower().find('<div class="main-footer', start)
        end = footer_index if footer_index >= 0 else len(page_html)
        processes.append(parse_process_segment(title, page_html[start:end]))
    return [
        ParsedProcess(p.title, extract_process_code(group_key, p.title), p.images, p.files)
        for p in processes if p.images or p.files
    ]


def admin_login(opener, base_url: str, email: str, password: str) -> None:
    login_url = urljoin(base_url, "/login?ReturnUrl=%2fAdmin%2fGeneralProcess")
    opener.open(Request(login_url, headers={"User-Agent": USER_AGENT}), timeout=120)
    payload = urlencode({"Email": email, "Password": password, "RememberMe": "true"}).encode()
    request = Request(
        login_url,
        data=payload,
        headers={
            "Content-Type": "application/x-www-form-urlencoded",
            "User-Agent": USER_AGENT,
        },
    )
    with opener.open(request, timeout=120) as response:
        body = response.read().decode("utf-8", errors="replace")
    if "html-login-page" in body.lower() or 'class="login-page"' in body.lower():
        raise RuntimeError("Legacy admin login failed.")


def fetch_processes(opener, base_url: str, asset_root: Path, dry_run: bool) -> list[dict]:
    manifest: list[dict] = []
    for group_key, page_name in LEGACY_PROCESS_PAGES:
        page_html = http_get(opener, urljoin(base_url, f"/Admin/{page_name}"))
        processes = parse_process_page(group_key, page_name, page_html)
        print(f"[processes/{page_name}] {len(processes)} entries", file=sys.stderr)
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
                    if (stored := _save_process_asset(opener, base_url, asset, "image", asset_root))
                ]
                item["files"] = [
                    stored
                    for asset in process.files
                    if (stored := _save_process_asset(opener, base_url, asset, "file", asset_root))
                ]
            manifest.append(item)
    return manifest


def _save_process_asset(opener, base_url: str, asset: ParsedAsset, asset_type: str,
                        asset_root: Path) -> dict | None:
    target_folder = "images" if asset_type == "image" else "files"
    target_dir = asset_root / target_folder
    saved = download_to(opener, base_url, asset.url, target_dir)
    if not saved:
        return None
    path, content_type = saved
    return {
        "type": asset_type,
        "displayName": asset.display_name,
        "url": f"/process-assets/{target_folder}/{path.name}",
        "originalFileName": clean_file_name(asset.display_name, path.name),
        "contentType": content_type,
        "sortOrder": asset.sort_order,
    }


# ─── CLI ─────────────────────────────────────────────────────────────────────

def write_manifest(path: Path, items: Iterable[dict]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    payload = list(items)
    path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


# ─── Step 2: migrate uploads into section/slug folders ──────────────────────

def step_migrate_uploads() -> None:
    """Move binaries that landed in /images/upload/ into their slug folders.

    Orphan upload files (no manifest reference) are removed via `git rm`."""
    if not UPLOAD_DIR.exists():
        return

    manifests_data: dict[str, list[dict]] = {}
    for section, path in PUBLIC_MANIFESTS.items():
        if path.exists():
            manifests_data[section] = json.loads(path.read_text(encoding="utf-8"))

    # Index references by upload filename: {filename: [(section, slug, item, field, idx)]}
    refs: dict[str, list[tuple[str, str, dict, str, int | None]]] = defaultdict(list)
    for section, items in manifests_data.items():
        for it in items:
            slug = it.get("slug", "")
            url = it.get("imageUrl") or ""
            if url.startswith("/images/upload/"):
                refs[Path(url).name].append((section, slug, it, "imageUrl", None))
            for i, gurl in enumerate(it.get("gallery") or []):
                if gurl.startswith("/images/upload/"):
                    refs[Path(gurl).name].append((section, slug, it, "gallery", i))

    moved = 0
    for upload_file in sorted(UPLOAD_DIR.iterdir()):
        if not upload_file.is_file():
            continue
        if upload_file.name not in refs:
            continue

        section, slug, *_ = refs[upload_file.name][0]
        target_dir = IMAGES_ROOT / section / slug
        target_dir.mkdir(parents=True, exist_ok=True)

        ext = upload_file.suffix
        if not any(p.name.startswith("thumb") for p in target_dir.iterdir()):
            new_name = f"thumb{ext}"
        else:
            existing = [p for p in target_dir.iterdir() if p.name.startswith("img-")]
            new_name = f"img-{len(existing) + 1:02d}{ext}"

        target_path = target_dir / new_name
        suffix = 1
        while target_path.exists():
            stem = target_path.stem.rstrip("0123456789").rstrip("-")
            target_path = target_dir / f"{stem}-{suffix}{ext}"
            suffix += 1

        shutil.move(str(upload_file), str(target_path))
        new_url = "/" + str(target_path.relative_to(WWWROOT)).replace(os.sep, "/")
        for _, _, item, field_name, idx in refs[upload_file.name]:
            if field_name == "imageUrl":
                item["imageUrl"] = new_url
            else:
                item["gallery"][idx] = new_url
        moved += 1

    for section, items in manifests_data.items():
        write_manifest(PUBLIC_MANIFESTS[section], items)

    # Remove anything left in upload/ (orphans).
    orphans = 0
    for f in list(UPLOAD_DIR.iterdir()):
        if not f.is_file() or f.name.startswith("."):
            continue
        rel = str(f.relative_to(REPO))
        try:
            subprocess.run(
                ["git", "-C", str(REPO), "rm", "--quiet", "-f", rel],
                check=True, capture_output=True,
            )
        except subprocess.CalledProcessError:
            f.unlink()
        orphans += 1

    UPLOAD_DIR.mkdir(parents=True, exist_ok=True)
    keep = UPLOAD_DIR / ".gitkeep"
    if not keep.exists():
        keep.touch()

    print(f"[migrate] moved {moved} files from upload/ → slug folders", file=sys.stderr)
    print(f"[migrate] removed {orphans} orphan upload files", file=sys.stderr)


# ─── Step 3: dedupe + drop orphans ──────────────────────────────────────────

def _hash_file(path: Path) -> str:
    h = hashlib.md5()
    with path.open("rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def _tracked_under_images() -> set[str]:
    out = subprocess.run(
        ["git", "-C", str(REPO), "ls-files", "nihomebackend/wwwroot/images"],
        check=True, capture_output=True, text=True,
    ).stdout
    return {line.strip() for line in out.splitlines() if line.strip()}


def _build_dup_redirects(tracked: set[str]) -> dict[str, str]:
    """{redundant_url: canonical_url} for every duplicate group, picking the
    canonical with preference: tracked > non-upload > shortest > alphabetic."""
    by_hash: dict[str, list[Path]] = defaultdict(list)
    for path in IMAGES_ROOT.rglob("*"):
        if not path.is_file():
            continue
        if path.suffix.lower() not in {".jpg", ".jpeg", ".png", ".webp", ".gif"}:
            continue
        by_hash[_hash_file(path)].append(path)

    redirects: dict[str, str] = {}
    for paths in by_hash.values():
        if len(paths) < 2:
            continue
        def sort_key(p: Path):
            rel = str(p.relative_to(REPO))
            in_upload = "/images/upload/" in rel.replace(os.sep, "/")
            return (0 if rel in tracked else 1, 1 if in_upload else 0, len(rel), rel)
        paths.sort(key=sort_key)
        canonical = paths[0]
        canonical_url = "/" + str(canonical.relative_to(WWWROOT)).replace(os.sep, "/")
        for redundant in paths[1:]:
            redundant_url = "/" + str(redundant.relative_to(WWWROOT)).replace(os.sep, "/")
            redirects[redundant_url] = canonical_url
    return redirects


def _rewrite_manifests(redirects: dict[str, str], slug_renames: dict[str, dict[str, str]]) -> None:
    for section, manifest in PUBLIC_MANIFESTS.items():
        if not manifest.exists():
            continue
        items = json.loads(manifest.read_text(encoding="utf-8"))
        renames = slug_renames.get(section, {})
        for it in items:
            old_slug = it.get("slug", "")
            new_slug = renames.get(old_slug)
            if new_slug:
                it["slug"] = new_slug
            url_renames: list[tuple[str, str]] = []
            if new_slug:
                url_renames.append((
                    f"/images/{section}/{old_slug}/",
                    f"/images/{section}/{new_slug}/",
                ))

            def remap(url: str) -> str:
                for old_prefix, new_prefix in url_renames:
                    if url.startswith(old_prefix):
                        url = new_prefix + url[len(old_prefix):]
                return redirects.get(url, url)

            if it.get("imageUrl"):
                it["imageUrl"] = remap(it["imageUrl"])
            it["gallery"] = [remap(g) for g in it.get("gallery", [])]
        write_manifest(manifest, items)


def _rewrite_seeder_cs(redirects: dict[str, str]) -> int:
    if not SEEDER_CS.exists():
        return 0
    text = SEEDER_CS.read_text(encoding="utf-8")
    replacements = 0
    for old_url, new_url in redirects.items():
        needle = f'"{old_url}"'
        if needle in text:
            replacements += text.count(needle)
            text = text.replace(needle, f'"{new_url}"')
    if replacements:
        SEEDER_CS.write_text(text, encoding="utf-8")
    return replacements


def _apply_slug_renames(slug_renames: dict[str, dict[str, str]]) -> None:
    for section, renames in slug_renames.items():
        section_dir = IMAGES_ROOT / section
        for old_slug, new_slug in renames.items():
            old = section_dir / old_slug
            new = section_dir / new_slug
            if not old.exists():
                continue
            if new.exists():
                for child in old.iterdir():
                    target = new / child.name
                    if not target.exists():
                        shutil.move(str(child), str(target))
                shutil.rmtree(old)
            else:
                old.rename(new)


def _drop_legacy_processes_dir() -> int:
    legacy = WWWROOT / "processes"
    if not legacy.exists():
        return 0
    files = sum(1 for p in legacy.rglob("*") if p.is_file())
    rel = str(legacy.relative_to(REPO))
    try:
        subprocess.run(["git", "-C", str(REPO), "rm", "-rf", "--quiet", rel],
                       check=True, capture_output=True)
    except subprocess.CalledProcessError:
        pass
    if legacy.exists():
        shutil.rmtree(legacy)
    return files


def _cleanup_empty_dirs(root: Path) -> int:
    removed = 0
    for path in sorted(root.rglob("*"), reverse=True):
        if path.is_dir() and path.name != "upload" and not any(path.iterdir()):
            path.rmdir()
            removed += 1
    return removed


def step_clean_wwwroot() -> None:
    if not WWWROOT.exists():
        print(f"[clean] missing: {WWWROOT}", file=sys.stderr)
        return

    slug_renames = {
        "activities": {"2": "khoi-cong-stfm-2021"},
    }
    _apply_slug_renames(slug_renames)
    tracked = _tracked_under_images()
    redirects = _build_dup_redirects(tracked)
    _rewrite_manifests(redirects, slug_renames)
    cs_replacements = _rewrite_seeder_cs(redirects)

    deleted = 0
    for url in redirects:
        path = WWWROOT / url.lstrip("/")
        if path.is_file():
            rel = str(path.relative_to(REPO))
            if rel in tracked:
                try:
                    subprocess.run(
                        ["git", "-C", str(REPO), "rm", "--quiet", "-f", rel],
                        check=True, capture_output=True,
                    )
                except subprocess.CalledProcessError:
                    path.unlink()
            else:
                path.unlink()
            deleted += 1

    legacy_processes = _drop_legacy_processes_dir()
    empty_dirs = _cleanup_empty_dirs(IMAGES_ROOT)

    print(f"[clean] deduped {deleted} duplicate files", file=sys.stderr)
    print(f"[clean] rewrote {cs_replacements} URLs in ContentSeeder.cs", file=sys.stderr)
    print(f"[clean] dropped {legacy_processes} legacy processes/ files", file=sys.stderr)
    print(f"[clean] removed {empty_dirs} empty dirs", file=sys.stderr)


# ─── Step 1: scrape ─────────────────────────────────────────────────────────

def step_scrape(targets: list[str], base_url: str, asset_root: Path,
                seed_root: Path, dry_run: bool,
                email: str | None, password: str | None) -> None:
    opener, _ = make_opener()

    if "processes" in targets:
        if not email or not password:
            raise SystemExit("Set NICON_LEGACY_EMAIL and NICON_LEGACY_PASSWORD to scrape processes.")
        admin_login(opener, base_url, email, password)
        manifest = fetch_processes(opener, base_url, asset_root / "process-assets", dry_run)
        if not dry_run:
            write_manifest(seed_root / "processes.json", manifest)
        image_count = sum(len(item["images"]) for item in manifest)
        file_count = sum(len(item["files"]) for item in manifest)
        print(f"[scrape] processes: {len(manifest)} entries, "
              f"{image_count} images, {file_count} files", file=sys.stderr)

    public_targets = [t for t in targets if t in SECTIONS]
    if public_targets:
        public_opener, _ = make_opener()
        for name in public_targets:
            section = SECTIONS[name]
            items = fetch_section(public_opener, base_url, section,
                                  asset_root / "images", dry_run)
            if not dry_run:
                write_manifest(seed_root / section.output_manifest, items)
            print(f"[scrape] {name}: {len(items)} entries", file=sys.stderr)


# ─── CLI ─────────────────────────────────────────────────────────────────────

def main() -> int:
    parser = argparse.ArgumentParser(
        description="End-to-end legacy nicon.vn importer: scrape + migrate + clean.",
    )
    parser.add_argument(
        "--target",
        choices=["all", "public", "processes", "activities", "news", "projects"],
        default="public",
        help="What to scrape. 'public' (default) = activities + news + projects. "
             "'all' adds the admin processes scrape (needs credentials). "
             "Pass a single section name to limit to that one.",
    )
    parser.add_argument("--base-url", default=os.getenv("NICON_LEGACY_BASE_URL", "https://nicon.vn"))
    parser.add_argument("--email", default=os.getenv("NICON_LEGACY_EMAIL"),
                        help="Admin email (only required for processes target).")
    parser.add_argument("--password", default=os.getenv("NICON_LEGACY_PASSWORD"),
                        help="Admin password (only required for processes target).")
    parser.add_argument("--skip-scrape", action="store_true",
                        help="Skip scraping; run migrate + clean on existing manifests only.")
    parser.add_argument("--no-clean", action="store_true",
                        help="Skip the migrate + clean phases (scrape only).")
    parser.add_argument("--dry-run", action="store_true",
                        help="Parse pages without downloading or writing manifests.")
    args = parser.parse_args()

    base_url = args.base_url.rstrip("/") + "/"
    if args.target == "all":
        targets = ["processes", "activities", "news", "projects"]
    elif args.target == "public":
        targets = ["activities", "news", "projects"]
    else:
        targets = [args.target]

    if not args.skip_scrape:
        step_scrape(
            targets=targets,
            base_url=base_url,
            asset_root=WWWROOT,
            seed_root=CONTENT_SEEDS_ROOT,
            dry_run=args.dry_run,
            email=args.email,
            password=args.password,
        )

    if args.dry_run or args.no_clean:
        return 0

    step_migrate_uploads()
    step_clean_wwwroot()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
