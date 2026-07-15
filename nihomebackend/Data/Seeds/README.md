# Data/Seeds/

Two unrelated kinds of seed data live here, separated into subfolders so the
distinction is impossible to miss.

## `i18n/` — UI string translations

Files here are loaded by [`TranslationSeeder`](../TranslationSeeder.cs) and
upserted into the `translations` table. The frontend reads them from
`/api/translations/{lang}` to render UI text (`actPage.eyebrow`,
`site.nav.home`, etc.).

Each file is an array where every entry carries the same key in all four
supported languages:

```json
[
  {
    "key": "actPage.eyebrow",
    "category": "actPage",
    "vi": "Hoạt động & tin tức",
    "en": "Activities & news",
    "zh": "活动与新闻",
    "ja": "活動とニュース"
  }
]
```

File naming is by feature area (`home.json`, `auth.json`, `recruitment.json`,
`admin-users.json`, …). Adding a new key only requires editing one of these
files and restarting the backend.

## `content/` — Domain entity content

Files here are loaded by [`ContentSeeder`](../ContentSeeder.cs) and become
rows in domain tables (`activities`, `news_articles`, `projects`,
`process_documents`). Each entity row links its non-VI translations as
`entity_translations` rows keyed by entity ID. The VI fields live directly
on the entity.

Schema (per file):

| File | Table | Manifest source |
| --- | --- | --- |
| `activities.json` | `activities` (+ `entity_translations`) | `tools/scrape_legacy_data/run.py` |
| `news.json` | `news_articles` (+ `entity_translations`) | `tools/scrape_legacy_data/run.py` |
| `projects.json` | _generated; not yet wired into seeder_ | `tools/scrape_legacy_data/run.py` |
| `processes.json` | `process_documents` | legacy admin scraper |

Each entry looks like:

```json
{
  "slug": "khoi-cong-stfm-2021",
  "imageUrl": "/images/activities/khoi-cong-stfm-2021/thumb.jpeg",
  "gallery": ["/images/activities/khoi-cong-stfm-2021/img-01.jpg"],
  "date": "12/06/2021",
  "category": "",
  "sortOrder": 5,
  "translations": {
    "vi": { "title": "...", "excerpt": "...", "content": ["..."] },
    "en": { "title": "...", "excerpt": "...", "content": ["..."] }
  }
}
```

`ContentSeeder` is **backfill-only** for `activities.json` / `news.json`: it
adds rows for slugs missing from the DB and adds `entity_translations` rows
that don't exist yet, but it never deletes or overwrites a row/translation
that's already in the database. This is deliberate — once an admin edits an
Activity/News entry or adds a translation via the CMS, that edit lives only
in the DB (never written back into the JSON manifest), so treating the
manifest as authoritative and re-seeding from it would silently destroy the
admin's work on every restart where the manifest and DB drift apart. That
used to happen (row-count mismatch or legacy stock-thumbnail detection
triggered a full drop-and-reseed) and was the root cause of translations
disappearing after a backend restart; the destructive path has been
removed.
