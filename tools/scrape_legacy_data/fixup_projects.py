#!/usr/bin/env python3
"""One-time cleanup pass over the raw nicon.vn project scrape.

Run after `run.py --target projects`, before the manifest is committed
as the seed source. Applies the dedup/rename/field-extraction/category
rules documented in
docs/superpowers/plans/2026-07-12-import-nicon-projects.md Task 3.
"""
import json
import re
import subprocess
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
MANIFEST_PATH = REPO_ROOT / "nihomebackend/Data/Seeds/content/projects.json"

# slug -> None means "drop this record" (only used for the 11 confirmed
# duplicates' losing side, after merge_gallery_and_content() has already
# folded its data into the winner below). slug -> "new-slug" is unused in
# this task (no renames needed — every winner's existing slug is already
# clean), kept as a dict for symmetry with the winner side.
SLUG_ACTIONS = {
    "nha-may-great-lotus": None,
    "semivina-nissi-el": None,
    "semivina-nissi-factory": None,
    "nha-may-clotex-labels": None,
    "nha-may-tien-len": None,
    "rebisco-factory": None,
    "nha-may-stfood-marketing-tai-viet-nam": None,
    "nha-hang-okonomiyaki": None,
    "akati-wood-factory": None,
    "mo-rong-nha-may-red-bull": None,
    "nha-may-scon": None,
}

# Winner slug -> loser slug whose gallery/content must be folded in
# before the loser is dropped via SLUG_ACTIONS above. See Task 3a.
MERGE_PAIRS = {
    "nha-may-great-lotus-vietnam": "nha-may-great-lotus",
    "nha-may-semivina-nissi": "semivina-nissi-el",
    "nha-may-advanced-casting-asia": "semivina-nissi-factory",
    "nha-may-clotex-labels-viet-nam": "nha-may-clotex-labels",
    "nha-may-tien-len-2": "nha-may-tien-len",
    "nha-may-rebisco": "rebisco-factory",
    "stfood-marketing-factory-vn": "nha-may-stfood-marketing-tai-viet-nam",
    "nha-hang-konimiyaki": "nha-hang-okonomiyaki",
    "nha-may-go-cong-ty-akati": "akati-wood-factory",
    "mo-rong-nha-may-red-bull-2": "mo-rong-nha-may-red-bull",
    "nha-may-cong-ty-scon": "nha-may-scon",
}

# After every merge above, force the winner's status to "completed" —
# 8 of the 11 pairs are the same project double-listed under ongoing
# AND completed; "completed" is the more truthful current state.
FORCE_STATUS_COMPLETED = set(MERGE_PAIRS.keys())

NAME_SUFFIX_ZONE = {
    "nha-may-lam-hiep-hung-tan-toan-phat": "1",
    "nha-may-lam-hiep-hung-tan-toan-phat-2": "2",
    "nha-may-lam-hiep-hung-tan-toan-phat-3": "3",
}
ZONE_SUFFIX_WORD = {"vi": "Khu", "en": "Zone"}

TITLE_FIXUPS = {
    # slug -> {lang: corrected title}
    "nha-may-san-xuat-bao-bi-amiba-nha-may-san-xuat-bao-bi-amiba": {
        "vi": "NHÀ MÁY SẢN XUẤT BAO BÌ AMIBA",
    },
}

# NOTE on deviations from the brief's original patterns (Task 3 report has
# the full rationale): real header lines use noticeably more label wording
# variety than the brief's first draft assumed. Both tables below were
# widened after grepping every text block in the real 85-project corpus
# for "label: value"-shaped lines that neither table recognized — every
# addition here is backed by at least one confirmed real occurrence, not
# a guess.
SKIP_PATTERNS = [
    # "Project" (37 occurrences) and "Dự án" (33) are bare, unlabeled
    # restatements of the project name/title used as often as "Project
    # name:"/"Tên dự án:" — same role, just terser wording.
    re.compile(r"^\s*(?:Tên dự án|Project name|Project|Dự án)\s*:\s*.*$", re.I),
]

FIELD_LABEL_PATTERNS = {
    "client": [
        re.compile(r"^\s*Khách hàng\s*:\s*(.*)$", re.I),
        re.compile(r"^\s*Client\s*:\s*(.*)$", re.I),
        # "Nhà đầu tư"/"Investor" sit in the exact slot Client/Khách hàng
        # normally occupies (immediately after the title line, immediately
        # before Location) on a handful of pages — e.g.
        # nha-may-advanced-casting-asia (both vi+en) has no "Khách hàng"/
        # "Client" line at all, only "Nhà đầu tư"/"Investor". Confirmed by
        # position, not just wording, across 6 real occurrences.
        re.compile(r"^\s*Nhà đầu tư\s*:\s*(.*)$", re.I),
        re.compile(r"^\s*Investor\s*:\s*(.*)$", re.I),
    ],
    "location": [
        re.compile(r"^\s*(?:Vị trí|Địa điểm)\s*:\s*(.*)$", re.I),
        re.compile(r"^\s*(?:Location|Located)\s*:\s*(.*)$", re.I),
    ],
    "scale": [
        # brief's "Diện tích" alone doesn't match the real "Diện tích dự
        # án:" wording (4 occurrences) — same optional-suffix pattern
        # already used for "Quy mô(?: dự án)?".
        re.compile(r"^\s*(?:Quy mô(?: dự án)?|Diện tích(?: dự án)?)\s*:\s*(.*)$", re.I),
        # "Project Scale" (3 occurrences) is a real en alias alongside the
        # brief's "Scale/Size of project".
        re.compile(r"^\s*(?:Scale of project|Size of project|Project size|Project Scale)\s*:\s*(.*)$", re.I),
    ],
    "scope": [
        # brief's "Phạm vi" alone doesn't match the real "Phạm vi công
        # việc:" wording (19 occurrences, the most common vi scope label
        # in the corpus).
        re.compile(r"^\s*(?:Hợp đồng|Phạm vi(?: công việc)?)\s*:\s*(.*)$", re.I),
        re.compile(r"^\s*(?:Scope of work|Contract Type|Contract)\s*:\s*(.*)$", re.I),
    ],
}

CATEGORY_RULES = [
    (re.compile(r"\bpharma\b|central pharmaceutical|dược phẩm", re.I), "Nhà máy dược phẩm"),
    (re.compile(r"\bwarehouse\b|nhà kho", re.I), "Nhà kho logistics"),
    (re.compile(r"\binterior\b.*\boffice\b|\boffice\b.*\binterior\b|nội thất.*văn phòng", re.I), "Nội thất văn phòng"),
    (re.compile(r"\binterior\b|nội thất", re.I), "Nội thất công nghiệp"),
    (re.compile(r"\brestaurant\b|nhà hàng", re.I), "Nhà hàng"),
    (re.compile(r"\bhotel\b|\bresort\b|khách sạn", re.I), "Khách sạn"),
    (re.compile(r"\bhouse\b|\bresidence\b|\bdormitory\b|nhà ở|ký túc xá", re.I), "Nhà ở"),
    (re.compile(r"\bshow flat\b", re.I), "Bất động sản"),
    (re.compile(r"\bshowroom\b", re.I), "Thương mại"),
    (re.compile(r"\bschool\b|trường học", re.I), "Giáo dục"),
    (re.compile(r"\bstudio\b", re.I), "Studio"),
    (re.compile(r"sport center|multi-purpose building|service building|swimming pool|trung tâm thể thao", re.I), "Công trình công cộng"),
    (re.compile(r"\bcanteen\b|\bcoffee\b|căng tin", re.I), "Nội thất công nghiệp"),
    (re.compile(r"\boffice\b|văn phòng", re.I), "Văn phòng"),
    (re.compile(r"\bfactory\b|\bworkshop\b|\bworksop\b|nhà máy|nhà xưởng", re.I), "Nhà máy công nghiệp"),
]


def infer_category(search_text: str) -> str | None:
    for pattern, category in CATEGORY_RULES:
        if pattern.search(search_text):
            return category
    return None


def combined_search_text(item: dict) -> str:
    parts = []
    for lang in ("en", "vi"):
        title = item.get("translations", {}).get(lang, {}).get("title")
        if title:
            parts.append(title)
    parts.append(item.get("slug", "").replace("-", " "))
    parts.append(item.get("scope", ""))
    return " ".join(parts)


def _match_field_pattern(text: str):
    """Return (field_name, cleaned_value) if `text` matches one of
    FIELD_LABEL_PATTERNS, else None. Strips stray leading '*' markdown-bold
    artifacts seen in at least one real page (noi-that-great-lotus en's
    "Location:** Lot 3, ...")."""
    for field_name, patterns in FIELD_LABEL_PATTERNS.items():
        for pattern in patterns:
            m = pattern.match(text)
            if m:
                value = re.sub(r"^\*+\s*", "", m.group(1).strip())
                return field_name, value
    return None


def extract_fields_from_content(blocks: list, title: str = "") -> tuple[dict[str, str], list]:
    """Pull labeled header lines out of a content-block array.

    Deviation from the brief's original design, found necessary against
    real data (see Task 3 report): rather than skipping *at most one*
    leading block that exactly string-equals the page's own title, this
    recognizes the page's own title (exact match) and any
    "Tên dự án:"/"Project name:" echo (SKIP_PATTERNS) wherever they occur
    among the leading string blocks, plus labeled Client/Location/Scale/
    Scope lines (FIELD_LABEL_PATTERNS). Real pages restate the title in
    inconsistent, non-exact ways ("Nhà Máy BMA tại KCN Hựu Thạnh" as a
    leading echo of the title "NHÀ MÁY BMA - KCN HỰU THẠNH";
    nha-hang-wrap-roll has *two* such echo lines back to back before its
    real "Project Name:"/"Client:" lines) — those extra echo lines are no
    longer specially recognized, they simply fall through to the
    "unrecognized" case below.

    Critical safety property (see Task 3 fix report): the first line
    that is neither the title, a SKIP_PATTERNS match, nor a
    FIELD_LABEL_PATTERNS match ends the header scan and is itself kept —
    a prior version of this function used a lookahead
    (`has_upcoming_field`) to keep scanning past unrecognized lines
    whenever a genuine field appeared later in the same header region,
    silently `continue`-ing past (i.e. permanently discarding) the
    unrecognized line. That silently deleted real data whenever an
    unrecognized line happened to sit before a real field — e.g.
    nha-may-great-lotus-vietnam's "Khách hang: Công ty TNHH Great Lotus
    Manufacturing Vietnam" (a source typo missing the dấu on "hàng",
    so it didn't match the "Khách hàng" regex) vanished entirely. Never
    discard a line just because it's unrecognized: an unrecognized line
    always lands in `remaining` (Content), never silently dropped, even
    at the cost of leaving a couple of echo lines in Content instead of
    extracting a field from behind them.
    """
    fields: dict[str, str] = {}
    remaining: list = []
    title = title.strip()

    still_scanning_header = True
    for block in blocks:
        if still_scanning_header and isinstance(block, str):
            text = block.strip()
            if title and text == title:
                continue
            if any(p.match(text) for p in SKIP_PATTERNS):
                continue
            match = _match_field_pattern(text)
            if match:
                field_name, value = match
                fields[field_name] = value
                continue
            still_scanning_header = False
        else:
            still_scanning_header = False
        remaining.append(block)
    return fields, remaining


def _block_key(block):
    """Stable de-dup key for a content block: the string itself for
    text, the url for an image."""
    return block if isinstance(block, str) else block.get("url")


def merge_gallery_and_content(winner: dict, loser: dict) -> None:
    """Union the loser's gallery images and content blocks into the
    winner, de-duplicating so nothing is silently lost when the loser
    is dropped as a duplicate CMS record."""
    winner_gallery = list(winner.get("gallery", []))
    seen_images = set(winner_gallery)
    for url in loser.get("gallery", []):
        if url not in seen_images:
            winner_gallery.append(url)
            seen_images.add(url)
    winner["gallery"] = winner_gallery

    for lang in ("vi", "en"):
        loser_t = loser.get("translations", {}).get(lang)
        if loser_t is None:
            continue
        winner_t = winner.setdefault("translations", {}).setdefault(lang, {})
        winner_blocks = list(winner_t.get("content", []))
        seen_blocks = {_block_key(b) for b in winner_blocks}
        for block in loser_t.get("content", []):
            key = _block_key(block)
            if key not in seen_blocks:
                winner_blocks.append(block)
                seen_blocks.add(key)
        winner_t["content"] = winner_blocks


def retry_empty_content(slug: str) -> None:
    """One retry for a project whose vi content came back empty from a
    transient network failure during Task 1's scrape (see Task 3e).
    Re-running the scraper is safe/idempotent by its own design; this
    does not attempt anything scraper-internal, just re-invokes it."""
    print(f"[retry] re-running scraper once for empty-content project: {slug}", file=sys.stderr)
    subprocess.run(
        [sys.executable, "tools/scrape_legacy_data/run.py", "--target", "projects", "--no-clean"],
        cwd=REPO_ROOT, check=False,
    )


def main() -> None:
    items = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    by_slug = {item["slug"]: item for item in items}

    # Retry known-empty content once (Task 3e) before anything else, so
    # the rest of the pipeline sees the best available data.
    for item in items:
        if item["slug"] == "nha-may-tan-thanh-long-4" and not item.get("translations", {}).get("vi", {}).get("content"):
            retry_empty_content(item["slug"])
            items = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
            by_slug = {i["slug"]: i for i in items}
            break

    # Title fixups (Task 3c) — applied before merge/category so a fixed
    # title participates correctly in combined_search_text() below.
    for slug, lang_titles in TITLE_FIXUPS.items():
        item = by_slug.get(slug)
        if item is None:
            print(f"[warn] title fixup slug not found: {slug}", file=sys.stderr)
            continue
        for lang, title in lang_titles.items():
            t = item.get("translations", {}).get(lang)
            if t is not None:
                t["title"] = title

    # Merge BEFORE the drop pass below, using original slugs. Asserts on
    # its own before/after counts — this must not depend on a later,
    # separate verification step re-deriving pre-merge state, since
    # that's fragile and easy to skip.
    for winner_slug, loser_slug in MERGE_PAIRS.items():
        winner = by_slug.get(winner_slug)
        loser = by_slug.get(loser_slug)
        if winner is None or loser is None:
            print(f"[warn] merge pair not found: {winner_slug} / {loser_slug}", file=sys.stderr)
            continue

        before_winner_gallery = len(winner.get("gallery", []))
        before_loser_gallery = len(loser.get("gallery", []))
        before_winner_content = {
            lang: len(winner.get("translations", {}).get(lang, {}).get("content", []))
            for lang in ("vi", "en")
        }
        before_loser_content = {
            lang: len(loser.get("translations", {}).get(lang, {}).get("content", []))
            for lang in ("vi", "en")
        }

        merge_gallery_and_content(winner, loser)
        if winner_slug in FORCE_STATUS_COMPLETED:
            winner["status"] = "completed"

        after_gallery = len(winner.get("gallery", []))
        assert after_gallery >= max(before_winner_gallery, before_loser_gallery), (
            f"merge {loser_slug} -> {winner_slug}: gallery shrank "
            f"({before_winner_gallery} winner + {before_loser_gallery} loser -> {after_gallery})"
        )
        if before_loser_gallery > 0:
            assert after_gallery > before_winner_gallery, (
                f"merge {loser_slug} -> {winner_slug}: loser had "
                f"{before_loser_gallery} gallery images but winner's count "
                f"({before_winner_gallery}) didn't grow — union silently added nothing"
            )
        for lang in ("vi", "en"):
            after_content = len(winner.get("translations", {}).get(lang, {}).get("content", []))
            assert after_content >= max(before_winner_content[lang], before_loser_content[lang]), (
                f"merge {loser_slug} -> {winner_slug}: {lang} content blocks shrank"
            )
            if before_loser_content[lang] > 0:
                assert after_content > before_winner_content[lang], (
                    f"merge {loser_slug} -> {winner_slug}: loser had "
                    f"{before_loser_content[lang]} {lang} content blocks but winner's count "
                    f"({before_winner_content[lang]}) didn't grow"
                )

        print(
            f"[merge] {loser_slug} -> {winner_slug}: "
            f"gallery {before_winner_gallery}+{before_loser_gallery}->{after_gallery}, "
            f"vi content {before_winner_content['vi']}+{before_loser_content['vi']}->"
            f"{len(winner.get('translations', {}).get('vi', {}).get('content', []))}"
        )

    unmatched_categories = []
    kept = []
    for item in items:
        slug = item["slug"]
        if slug in SLUG_ACTIONS and SLUG_ACTIONS[slug] is None:
            print(f"[drop] {slug}")
            continue
        if slug in NAME_SUFFIX_ZONE:
            zone = NAME_SUFFIX_ZONE[slug]
            for lang, word in ZONE_SUFFIX_WORD.items():
                t = item.get("translations", {}).get(lang)
                if t and t.get("title"):
                    t["title"] = f"{t['title']} - {word} {zone}".strip()

        # Extract Client/Location/Scale/Scope: vi first, fall back to en
        # per-field if vi yielded nothing for it (Task 3f, nha-hang-hokkaido).
        vi_fields: dict[str, str] = {}
        en_fields: dict[str, str] = {}
        for lang, translation in item.get("translations", {}).items():
            title = translation.get("title", "")
            fields, remaining = extract_fields_from_content(translation.get("content", []), title=title)
            translation["content"] = remaining
            if lang == "vi":
                vi_fields = fields
            elif lang == "en":
                en_fields = fields
        for field_name in ("client", "location", "scale", "scope"):
            item[field_name] = vi_fields.get(field_name) or en_fields.get(field_name, "")

        category = infer_category(combined_search_text(item))
        if category is None:
            unmatched_categories.append(slug)
        item["category"] = category
        kept.append(item)

    MANIFEST_PATH.write_text(
        json.dumps(kept, indent=2, ensure_ascii=False) + "\n", encoding="utf-8"
    )
    print(f"\n{len(kept)} projects kept (from {len(items)} scraped)")
    if unmatched_categories:
        print(f"[warn] {len(unmatched_categories)} projects have no inferred category:", file=sys.stderr)
        for s in unmatched_categories:
            print(f"  - {s}", file=sys.stderr)


if __name__ == "__main__":
    main()
