# Design: Process Asset Viewing (Images + File Downloads)

**Date:** 2026-06-11
**Branch:** feat/work-processes-seed-only

## Problem

`processes.json` already contained full asset metadata (55 images, 298 files across 29 documents) but the data was discarded during seeding, never stored in the DB, and not exposed via the API. The admin UI had no way to view images or download files.

## Approach Chosen: JSON Columns (mirrors Project pattern)

Add `ImagesJson` and `FilesJson` as `nvarchar(max)` nullable columns to `ProcessDocument`. During seeding, serialize the arrays into these columns. The API deserializes and returns them in `ProcessResponse`.

Rejected alternatives:
- **Separate `process_assets` table** — overkill for seed-only, adds navigation property + eager loading
- **Static JSON in frontend** — data duplication, drifts if admin edits processes

## Backend Changes

### ProcessDocument model
Added two nullable string columns: `ImagesJson`, `FilesJson`.

### Migration
`20260611000001_AddProcessDocumentAssetColumns` — adds both columns to `process_documents` table. Manually created with `[Migration]` + `[DbContext]` attributes.

### ContentSeeder
- `ProcessSeedItem` extended with `Images` and `Files` lists
- `ProcessAssetSeedItem` inner class holds: `DisplayName`, `Url`, `OriginalFileName`, `ContentType`, `FileSizeBytes`, `SortOrder`
- Seeder guard updated: re-seeds if count differs OR if seed has assets but DB rows have null JSON columns
- `SeedProcesses` serializes arrays into `ImagesJson`/`FilesJson` on each `ProcessDocument`

### ProcessResponse DTO
Added `SortOrder`, `Images: List<ProcessAssetInfo>`, `Files: List<ProcessAssetInfo>`.
New `ProcessAssetInfo` class has: `DisplayName`, `Url`, `OriginalFileName`, `ContentType`, `FileSizeBytes`, `SortOrder`.

### ProcessService
`MapToResponse` now deserializes the JSON columns into typed lists.

## Frontend Changes

### contentApi.ts
New `ProcessAssetInfo` interface. `ProcessResponse` extended with `sortOrder`, `images`, `files`.

### ProcessList.tsx
Each process row now shows:
- Image count + file count subtitle
- "Xem" / "Ẩn" toggle button when the document has assets
- Expandable `AssetPanel` with:
  - **Image grid**: 80×60 thumbnails, clicking opens `ImageLightbox` with prev/next navigation
  - **File list**: each file is an `<a download>` link showing name, size, download icon on hover
- `ImageLightbox` is a fullscreen overlay with keyboard-navigable prev/next

## Physical Files

Asset files (`/process-assets/images/`, `/process-assets/files/`) are served from `nihomebackend/wwwroot/process-assets/` as static files. These are **not in git**. To populate them:

```bash
cd tools
python3 workprocess_legacy_scraper.py \
  --username ADMIN_USER \
  --password ADMIN_PASS \
  --output-dir ../nihomebackend/wwwroot/process-assets \
  --json-output ../nihomebackend/Data/Seeds/processes.json
```

The scraper downloads from the legacy `nicon.vn` admin pages. Requires valid admin credentials.

Images will show a broken-image placeholder until the files are placed; file download links will result in 404 until then.
