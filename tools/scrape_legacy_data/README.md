# Legacy nicon.vn Importer

Single-script pipeline that pulls content from the legacy `nicon.vn` website
into the new backend's seed format.

```
python3 tools/scrape_legacy_data/run.py
```

That one command does everything:

1. **scrape** activities, news, and projects (Vietnamese + English)
2. **migrate** any binaries that landed in `wwwroot/images/upload/` into
   their owning slug folders (so `/upload/` stays empty for runtime use)
3. **clean** — deduplicate identical files across `wwwroot/images/`,
   drop the obsolete `wwwroot/processes/` folder, rewrite seed manifests
   and `ContentSeeder.cs` to point at canonical paths

It writes:

| Output | Source |
| --- | --- |
| `nihomebackend/Data/Seeds/content/activities.json` | `/activities` |
| `nihomebackend/Data/Seeds/content/news.json` | `/news` |
| `nihomebackend/Data/Seeds/content/projects.json` | `/projectsongoing` + `/projectscompleted` |
| `nihomebackend/Data/Seeds/content/processes.json` | `/Admin/*Process` (only with `--target all`) |
| `nihomebackend/wwwroot/images/<section>/<slug>/...` | downloaded binaries |

Repeated runs are safe: cached binaries on disk are reused, and the clean
pass is idempotent.

## Requirements

* Python 3.9+ (standard library only — no `pip install`)
* Network access to `https://nicon.vn`
* Admin credentials only if you also want the `processes` target

## Common invocations

```bash
# Default — public sections only (activities, news, projects) + clean
python3 tools/scrape_legacy_data/run.py

# Include the admin-only WorkProcesses scrape
NICON_LEGACY_EMAIL=... NICON_LEGACY_PASSWORD=... \
  python3 tools/scrape_legacy_data/run.py --target all

# Limit to one section
python3 tools/scrape_legacy_data/run.py --target activities

# Re-run only the migrate + clean phases (no network)
python3 tools/scrape_legacy_data/run.py --skip-scrape

# Quick sanity check, no downloads
python3 tools/scrape_legacy_data/run.py --dry-run
```

## Flags

| Flag | Default | Purpose |
| --- | --- | --- |
| `--target` | `public` | `public` (= activities + news + projects), `all` (adds `processes`), or a single section |
| `--base-url` | `https://nicon.vn` | Override target host |
| `--email` / `--password` | env `NICON_LEGACY_*` | Admin credentials (only for `processes`) |
| `--skip-scrape` | off | Run only migrate + clean |
| `--no-clean` | off | Run only scrape |
| `--dry-run` | off | Parse pages without downloading or writing manifests |

## Pairing VI ↔ EN

The legacy CMS stores each article under different language slugs but points
at the same `/Content/Images/uploaded/...` assets. The scraper computes
Jaccard similarity over each item's image set and pairs VI ↔ EN when
overlap ≥ 0.5; items without a confident match keep VI only.

## Output layout

```
nihomebackend/wwwroot/images/<section>/<slug>/
    thumb.jpg
    img-01.png
    img-02.png
```

`/images/upload/` is reserved for runtime user uploads and stays empty
(only a `.gitkeep`).

## Troubleshooting

* **`HTTP Error 404` for image URLs** — the legacy CMS occasionally references
  files that no longer exist; those are logged and skipped.
* **`Page not found` body** — the language cookie was lost; the scraper
  resets it before each request, so re-running is safe.
