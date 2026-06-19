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

`ContentSeeder` is **idempotent** but it _will_ drop and re-seed the table
when the row count diverges from the manifest, or when an existing row
still carries one of the legacy stock thumbnails (see
`IsLegacyStockActivityImage` / `IsLegacyStockNewsImage`). Existing
EntityTranslation rows for the entity type are wiped at the same time so
they never dangle.
