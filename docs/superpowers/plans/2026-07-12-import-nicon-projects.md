# Import Real nicon.vn Project Data Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the current placeholder/mock `Project` seed data (75 hand-written, fictional-sounding entries hardcoded in `ContentSeeder.SeedProjects()`) with the real 74 projects (85 scraped minus 11 confirmed duplicates — see Task 3) that exist today on `nicon.vn`, including their full rich content (not just spec fields), wire the seeder to load them from a manifest file (matching the pattern already used for Activities/News) so future devs and deploys get the same data, and populate all 4 languages (vi/en/zh/ja) in the admin "Content Translations → Project" tab.

**Architecture:** `Project` gets a `ContentJson` field, mirroring `Activity.ContentJson` exactly (same DTO shape, same `SerializeContent`/`DeserializeContent` helpers, same admin `ContentBlockEditor` — which already exists and already sends `content` from `ProjectForm.tsx`, it's just silently dropped by the backend today). `ContentSeeder.SeedProjects()` converts from a hardcoded C# array to loading an embedded-resource JSON manifest (`Data/Seeds/content/projects.json`), reusing the scraper's native block-based output directly. Data comes from `tools/scrape_legacy_data/run.py`, an existing, working, in-repo scraper.

**Tech Stack:** ASP.NET Core 8 / EF Core (SQL Server) backend, xUnit for tests, Python 3 (stdlib only) for the scraper/fixup scripts, embedded JSON resources for seed manifests.

## Content Blocks decision — corrected mid-investigation, read this first

**This plan originally rejected Content Blocks.** That conclusion was wrong and has been reversed. Recording both the original reasoning and the correction here because the wrong version was already shared for review — silently replacing it would hide a real methodology bug worth knowing about.

**Original (wrong) conclusion:** all 85 live project pages were fetched via `WebFetch` and summarized. Every sample showed only labeled fields (Client/Location/Scope) + a static gallery, no rich content, no video. Concluded: `Project`'s existing columns (`Description`, `Gallery`, etc.) are sufficient, Content Blocks are unnecessary, Codex's plan is over-engineered.

**What was actually wrong:** `WebFetch` has no session/cookie persistence across calls. nicon.vn's language switch (`/changelanguage/2` for Vietnamese) sets a cookie and redirects — every stateless `WebFetch` call silently fell back to the English version, which is a sparse, cut-down rendering of the content. The real, native-language Vietnamese CMS content was never actually seen.

**The correction:** the user supplied a screenshot of `nicon.vn/projectscompleted/stfood-marketing-factory-vn` (the real Vietnamese slug — different from the English slug `stfood-marketing-factory-en` used earlier) showing a genuine long-form article: a heading, multiple paragraphs with sub-headings ("Thiết Kế Kiến Trúc và Công Nghệ", "2. Sử Dụng Vật liệu, Kỹ thuật xây dựng và tính bền vững"), and captioned images placed inline within the narrative. This was re-verified directly in this session using `curl` with a persistent cookie jar (not WebFetch) hitting the real VI listing pages and detail page — confirmed genuine, substantial Vietnamese content that a flat `Description` string would destroy the structure of.

**Scope of the correction, confirmed by Task 1's real output:** exactly **5/85** projects have substantial narrative content (a text block over 200 characters) — `nha-o-d22`, `thanh-cong-cua-du-an-trimas-viet-nam-van-hoa-va-tinh-than-nicon`, `stfood-marketing-factory-vn`, `nha-b37-2`, `mo-rong-nha-may-red-bull-2` — matching this plan's original "~5 flagship" estimate almost exactly. The other 80 scraped projects (74 after Task 3's dedup) are simple labeled-fields-plus-gallery pages. Either way, the fix (adding `ContentJson`) is the same cost whether 5 or 50 projects use it, and the scraper's native output is already block-shaped — see Task 1.

**Practical effect on the plan:** this reinstates the backend part of Codex's original proposal (`ContentJson` on `Project`, a migration, DTO/response fields, `ProjectService` changes, a `TranslationsController` field) almost exactly as Codex described it — the earlier rejection in this plan was the actual over-reach, not Codex's proposal. The one part of the original critique that still stands: Codex should have verified against real page content before proposing a schema change, same lesson this plan had to re-learn the hard way.

## Global Constraints

- **Base branch:** `refactor/categories-i18n`, not `main`. `main` is missing this branch's Projects-API i18n wiring, multilingual `ProjectCategory` columns (`NameEn/NameZh/NameJa`), and the fix for the seeder's old destructive drop-and-reseed bug (see `nihomebackend/Data/Seeds/README.md`, "backfill-only" section). Building on bare `main` would silently lose all of that.
- **Seeding must stay backfill-only.** Per `nihomebackend/Data/Seeds/README.md`: the seeder must never delete or overwrite a DB row that already exists (this caused a real translation-loss incident before it was fixed). The new `SeedProjects()` must only `AddRange` rows for slugs missing from the DB, exactly like the current implementation and like `SeedActivities`/`SeedNews`. Do not add any delete/overwrite logic.
- **Known consequence:** because seeding is backfill-only, any DB that already has the old 75 fake project rows (slugs like `nha-may-bma`, `nha-xuong-nbdc`, …) will keep them after this change ships — the seeder will not remove them. This is a deliberate tradeoff, not a bug; call it out in the PR description and in `docs/ai/memory-bank/05-decisions-and-open-questions.md` so a human decides whether to bulk-delete those rows via the admin UI on affected environments.
- **Docker build/test commands** (no local `dotnet` CLI in this environment):
  ```bash
  docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
    dotnet build nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
  docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
    dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
  ```
- Git: only stage files touched by the task at hand (no `git add -A`). Commit subject ≤ 50 chars, body lines ≤ 72 chars, per CLAUDE.md.
- Frontend: **no frontend code changes needed.** `ProjectForm.tsx` already renders `ContentBlockEditor` and sends `content` in its payload; `ProjectDetail.tsx` already renders `project.content` via `<ContentBlocks>` in its own section (`nihomeweb/src/pages/ProjectDetail.tsx:169-179`). Both were already built for this and have been silently no-op-ing because the backend dropped the field. Verify with `npm run lint`/`npm run build` per the standing quality gate, but expect zero diffs.
- All new/changed seed data must ship in **all 4 languages** (vi/en/zh/ja) per CLAUDE.md's non-negotiable i18n rule — `Name` and `Content` on every one of the 74 projects, not just a sample. `Description` is a real `Project` column and stays in the Content Translations field list for admin-entered projects generally, but none of the 74 imported real projects have distinct short-summary content for it (see Task 4) — it is seeded as an explicit empty string `""` in every language for all 74, uniformly, not fabricated and not left ambiguously `null`/omitted.
- All web-fetching against `nicon.vn` in this plan must use a **session-persistent HTTP client** (Python `urllib` with a cookiejar, or `curl -c/-b cookies.txt`), never a stateless single-shot fetch — this plan exists because a stateless fetch produced a wrong conclusion once already.

---

## Task 1: Scrape real project data from nicon.vn (scrape-only, no destructive cleanup)

**Files:**
- Runs: `tools/scrape_legacy_data/run.py` (already exists, do not modify in this task)
- Produces: `nihomebackend/Data/Seeds/content/projects.json` (overwritten with real data)
- Produces: `nihomebackend/wwwroot/images/projects/<slug>/...` (new image files)

The tool was already smoke-tested with `--dry-run` in this session: `[scrape] projects: 85 entries` (31 vi + 36 en cards for `projectsongoing`, 54 vi + 49 en cards for `projectscompleted`) — this matches a manual full crawl of all 85 live pages done independently in this session, so the tool is trustworthy for the scrape pass itself.

The scraper's `fetch_section()` (shared by activities/news/projects, `tools/scrape_legacy_data/run.py:515-651`) already produces exactly the shape `Project` now needs: `translations.vi.content` / `translations.en.content` as an ordered array of `{"type": "text", "value": ...}` and `{"type": "image", "url": ...}` blocks (`materialize_content()`, `run.py:607-621`) — this is the same shape `ContentBlockEditor.tsx`/`ContentBlocks.tsx` already consume for Activities/News. **What it does not do** is split out `Client`/`Location`/`Scale`/`Scope` as separate fields — those labeled lines (`"Khách hàng: ..."`, `"Client: ..."`) come through as ordinary text blocks mixed in with the real narrative content. Task 3 handles pulling them back out.

- [x] **Step 1: Run the scraper, scrape-only (skip the migrate/clean phases, which touch unrelated Activities/News files)**

```bash
python3 tools/scrape_legacy_data/run.py --target projects --no-clean
```

Expected stderr output (matches the dry-run already done):
```
[projects/projectsongoing] vi cards: 31
[projects/projectsongoing] en cards: 36
[projects/projectscompleted] vi cards: 54
[projects/projectscompleted] en cards: 49
[scrape] projects: 85 entries
```

- [x] **Step 2: Inspect real output — confirm the content-blocks shape and measure how many projects have substantial narrative content vs. just labeled-fields-and-gallery**

**Already run — see `.superpowers/sdd/task-1-report.md` for the full account.** The script below has been corrected in place from what was actually run: the original assumed text blocks were `{"type": "text", "value": ...}` dicts (matching the shape image blocks use); real output has text blocks as **bare strings** (only image blocks are dicts) and the `>4 text_blocks` half of the signal turned out to be non-discriminating (the standard 5-line simple template already satisfies it for 55/85 projects). The corrected, actually-discriminating script:

```bash
python3 -c "
import json
data = json.load(open('nihomebackend/Data/Seeds/content/projects.json'))
print('total:', len(data))
rich = []
for item in data:
    vi = item.get('translations', {}).get('vi', {})
    blocks = vi.get('content', [])
    text_blocks = [b for b in blocks if isinstance(b, str)]
    # The discriminating signal is a single block over 200 chars — a real
    # narrative paragraph. (Block *count* alone doesn't discriminate: the
    # standard 4-label header plus title already yields 5 text blocks for
    # the vast majority of 'simple' projects.)
    if any(len(b) > 200 for b in text_blocks):
        rich.append(item['slug'])
print(f'{len(rich)}/{len(data)} projects have substantial content blocks:')
for s in rich:
    print(' -', s)
"
```

**Already confirmed by Task 1's implementer against real output:** 5/85 (`nha-o-d22`, `thanh-cong-cua-du-an-trimas-viet-nam-van-hoa-va-tinh-than-nicon`, `stfood-marketing-factory-vn`, `nha-b37-2`, `mo-rong-nha-may-red-bull-2`), matching the "~5 flagship" estimate almost exactly. See `.superpowers/sdd/task-1-report.md` for the full distribution and the correction of this step's own script (the brief assumed a dict-shaped text block; real text blocks are bare strings — see Task 3's superseded-analysis note for the downstream fix).

- [x] **Step 3: Spot-check image downloads**

```bash
find nihomebackend/wwwroot/images/projects -type f | wc -l
find nihomebackend/wwwroot/images/projects -maxdepth 1 -type d | wc -l
```

Expected: directory count = 85 (one per project slug), file count in the low hundreds (galleries range from 1 to ~30 images per project per the manual crawl).

- [x] **Step 4: Commit the raw scrape as its own checkpoint (before any cleanup), so later cleanup is a reviewable diff**

**Done — commit `487b398d39505bb6213f6c65f049e2172a1561fb`.** Only `projects.json` was staged; `wwwroot/images/projects/` had zero working-tree changes (904 files across 128 directories already existed from an earlier sync commit, reused idempotently — 85 of those 128 directories match the current scrape's slugs, the other 43 are leftovers from an earlier slug-naming scheme that Task 3 doesn't need to touch). One project, `nha-may-tan-thanh-long-4`, has empty content from an SSL timeout mid-scrape — Task 3 Step 1 retries it once.

```bash
git add nihomebackend/Data/Seeds/content/projects.json nihomebackend/wwwroot/images/projects
git commit -m "seed: scrape raw project data from nicon.vn

Raw output from tools/scrape_legacy_data/run.py --target projects,
including native content-blocks (text/image sequences) per
language. Cleanup and field extraction happen in later commits
so this one is reviewable as a clean before/after."
```

---

## Task 2: Add `ContentJson` to `Project` (backend schema — mirrors `Activity` exactly)

**Files:**
- Modify: `nihomebackend/Models/Project.cs`
- Modify: `nihomebackend/Models/DTOs/Requests/UpsertProjectRequest.cs`
- Modify: `nihomebackend/Models/DTOs/Responses/ProjectResponse.cs`
- Modify: `nihomebackend/Services/ProjectService.cs`
- Modify: `nihomebackend/Controllers/TranslationsController.cs`
- Create: EF Core migration
- Test: `nihomebackend.tests/Services/ProjectServiceTests.cs`

**Interfaces:**
- Produces: `Project.ContentJson` (`string`, default `"[]"`), `UpsertProjectRequest.Content` (`object[]`, required), `ProjectResponse.Content` (`object[]`) — same types as `Activity`'s equivalents, so the already-built `ContentBlockEditor.tsx`/`ContentBlocks.tsx` frontend needs no changes.

- [ ] **Step 1: Add the column to the model**

In `nihomebackend/Models/Project.cs`, add after `HighlightsJson`:

```csharp
    /// <summary>JSON array of { type, value } | { type, url, caption } content blocks.</summary>
    public string ContentJson { get; set; } = "[]";
```

- [ ] **Step 2: Add `Content` to the request/response DTOs**

In `nihomebackend/Models/DTOs/Requests/UpsertProjectRequest.cs`, add after `Highlights`:

```csharp
    [Required] public object[] Content { get; set; } = [];
```

In `nihomebackend/Models/DTOs/Responses/ProjectResponse.cs`, add after `Highlights`:

```csharp
    public object[] Content { get; set; } = [];
```

- [ ] **Step 3: Write the failing test**

Add to `nihomebackend.tests/Services/ProjectServiceTests.cs` (follow whatever `[Fact]` pattern already exists in that file for create/update round-trips):

```csharp
[Fact]
public async Task CreateAsync_PersistsContentBlocks()
{
    var req = new UpsertProjectRequest
    {
        Slug = "test-content-blocks",
        ImageUrl = "/images/test.jpg",
        Name = "Test Project",
        Client = "Test Client",
        Location = "Test Location",
        Scope = "Design",
        Status = "ongoing",
        Content = new object[]
        {
            new Dictionary<string, object> { ["type"] = "text", ["value"] = "Intro paragraph" },
            new Dictionary<string, object> { ["type"] = "image", ["url"] = "/images/test-2.jpg", ["caption"] = "A caption" },
        },
    };

    var created = await _sut.CreateAsync(req);

    Assert.Equal(2, created.Content.Length);
    var fetched = await _sut.GetBySlugAsync("test-content-blocks");
    Assert.NotNull(fetched);
    Assert.Equal(2, fetched!.Content.Length);
}
```

- [ ] **Step 4: Run to verify it fails**

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~CreateAsync_PersistsContentBlocks"
```

Expected: compile error (`UpsertProjectRequest` has no `Content` property yet) until Step 2 above is done, then a runtime failure (`created.Content.Length` is always 0) until Step 5 below is done.

- [ ] **Step 5: Wire `ProjectService` — copy `Activity`'s `SerializeContent`/`DeserializeContent` pattern exactly**

In `nihomebackend/Services/ProjectService.cs`, in `CreateAsync`, add to the `entity` initializer (after `HighlightsJson`):

```csharp
            ContentJson = SerializeContent(req.Content),
```

In `UpdateAsync`, add after the `entity.HighlightsJson = ...` line:

```csharp
        entity.ContentJson = SerializeContent(req.Content);
```

In `MapToResponse`, add after `Highlights = ...`:

```csharp
        Content = t.TryGetValue("Content", out var contentJson)
            ? DeserializeContent(contentJson)
            : DeserializeContent(p.ContentJson),
```

Add these two private static helpers (copied verbatim from `nihomebackend/Services/ActivityService.cs:150-179`, which already has the double-encoding auto-repair logic worth reusing rather than re-deriving):

```csharp
    private static string SerializeContent(object[] content) => JsonSerializer.Serialize(content ?? []);

    private static object[] DeserializeContent(string? contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
            return [];

        var result = JsonSerializer.Deserialize<object[]>(contentJson) ?? [];

        // Auto-repair: previous admin UI bug stored the whole ContentItem JSON array
        // as a single escaped string element — e.g. ["[{\"type\":\"image\",...},\"text\"]"].
        // Unwrap and re-deserialize so images and text render correctly.
        if (result.Length == 1
            && result[0] is JsonElement el
            && el.ValueKind == JsonValueKind.String)
        {
            var inner = el.GetString() ?? "";
            if (inner.TrimStart().StartsWith('['))
            {
                try
                {
                    var repaired = JsonSerializer.Deserialize<object[]>(inner);
                    if (repaired is { Length: > 0 }) return repaired;
                }
                catch { /* not valid JSON — keep original */ }
            }
        }

        return result;
    }
```

- [ ] **Step 6: Run to verify the test now passes**

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~CreateAsync_PersistsContentBlocks"
```

- [ ] **Step 7: Add `Content` to the admin Content-Translations field list**

In `nihomebackend/Controllers/TranslationsController.cs:88`, change:

```csharp
            new { type = EntityTypes.Project, display = "Projects", fields = new[] { "Name", "Description", "Challenges", "Solutions" } },
```

to:

```csharp
            new { type = EntityTypes.Project, display = "Projects", fields = new[] { "Name", "Description", "Content", "Challenges", "Solutions" } },
```

Find the `case EntityTypes.Project:` block (around line 233) and add `Content` alongside however `Description`/`Challenges`/`Solutions` are already read from/written to the entity there (mirror the exact pattern already used for those three fields — read `proj.ContentJson` for the "original" value, write to `proj.ContentJson` is **not** done for translations, only the base VI row owns `ContentJson` directly; translated Content goes through `EntityTranslations` exactly like Activity's `Content` field already does).

- [ ] **Step 8: Create the EF Core migration**

**Verified in this session, don't use `docker exec nihome31042025-backend dotnet ef ...` for this step:** `nihome31042025-backend` has the .NET 8 SDK and the main project at `/app/nihomebackend.csproj` (so `docker exec ... dotnet build`/`dotnet format` in Task 7 work fine, matching CLAUDE.md's quality-gate commands exactly) — but `dotnet tool list -g` inside that container returns empty, so the `dotnet-ef` global tool is **not installed there**, and `docker exec nihome31042025-backend dotnet ef migrations add ...` will fail with a "command not found" error. Use the standalone SDK image instead, installing `dotnet-ef` inline (ephemeral, doesn't need to persist):

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace/nihomebackend mcr.microsoft.com/dotnet/sdk:8.0 \
  bash -c "dotnet tool install --global dotnet-ef --version 8.* >/dev/null 2>&1; \
           export PATH=\$PATH:/root/.dotnet/tools; \
           dotnet ef migrations add AddProjectContentJson --project . --startup-project ."
```

Review the generated migration: it should add one nullable-or-defaulted `ContentJson` column to the `projects` table, nothing else. Update `nihomebackend/Migrations/AppDbContextModelSnapshot.cs` if the `dotnet ef` command didn't already regenerate it (it normally does automatically).

- [ ] **Step 9: Run the full backend test suite**

No separate `dotnet ef database update` step is needed: `Program.cs:143` calls `app.MigrateDatabase()` on startup, so the migration created in Step 8 applies automatically the next time the backend container restarts (Task 6 Step 1 already does this). Running `dotnet ef database update` from an ephemeral container would also need `--network` wired to the compose network to even reach the `sqlserver` container, so relying on the app's own startup migration is both simpler and already-proven to work (every other migration in this repo's history relies on the same mechanism).

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
```

- [ ] **Step 10: Commit**

```bash
git add nihomebackend/Models/Project.cs \
  nihomebackend/Models/DTOs/Requests/UpsertProjectRequest.cs \
  nihomebackend/Models/DTOs/Responses/ProjectResponse.cs \
  nihomebackend/Services/ProjectService.cs \
  nihomebackend/Controllers/TranslationsController.cs \
  nihomebackend.tests/Services/ProjectServiceTests.cs \
  nihomebackend/Migrations
git commit -m "feat: persist Project content blocks

Project.ContentJson mirrors Activity.ContentJson exactly. The
admin ContentBlockEditor and public ContentBlocks renderer
already existed and already sent/expected this field — the
backend was silently dropping it. Also adds Content to the
Content Translations admin tab for Project."
```

---

## Task 3: Fix data-quality issues, extract structured fields out of content blocks, assign categories

> **Superseded analysis, replaced after real Task 1 output landed.** This task's original version was written from a manual crawl of nicon.vn done via stateless `WebFetch` calls, which (per the correction logged at the top of this plan) never actually saw the real Vietnamese-language content and used English URL slugs that don't exist anywhere in the real scraper's output — the real scraper generates its own slugs from the VI listing page's URL, in a completely different naming scheme. Everything below was rebuilt from the **actual committed output of Task 1** (commit `487b398`, `nihomebackend/Data/Seeds/content/projects.json`, 85 entries), cross-checked programmatically (grouping by extracted client+location, not by eyeballing titles) rather than assumed.

**Also superseded: the content-block shape.** Task 1's implementer found that text entries in `translations.<lang>.content` are **plain strings**, not `{"type": "text", "value": ...}` dicts — only image entries are dicts (`{"type": "image", "url": ...}`). Every block-handling function below is written against the real (string) shape. If you're comparing against an older version of this task, that is the one non-negotiable fix — the old dict-shaped code crashes immediately (`AttributeError: 'str' object has no attribute 'get'`) on real data.

**Files:**
- Create: `tools/scrape_legacy_data/fixup_projects.py`
- Modifies: `nihomebackend/Data/Seeds/content/projects.json` (in place, run after Task 1)

### 3a. Confirmed duplicates (11 pairs, verified by matching client+location extracted from real content, not by slug/title guessing)

A systemic pattern emerged once duplicates were found by content instead of by name: **8 of these 11 pairs are the same physical project listed on both `projectsongoing` and `projectscompleted`** — evidently the source CMS doesn't remove a project from the ongoing list when a "completed" record is added for it. General rule applied below: **when one side of a pair is `completed` and the other `ongoing`, the `completed` one wins** (it's the more truthful current state); when both sides share the same status, the side with an `en` translation present wins, tie-broken by larger `gallery`. In every case `merge_gallery_and_content()` (Step 1 script) unions *both* sides' images/content into the winner first — the "losing" slug is only dropped after nothing on it is unique anymore.

| # | Winner (kept, wins on) | Loser (dropped after merge) | Evidence |
|---|---|---|---|
| 1 | `nha-may-great-lotus-vietnam` (completed) | `nha-may-great-lotus` (ongoing) | Same location (Số 3, đường 24, VSIP II-A), same scale 31.187 m², both en title "GREAT LOTUS VIETNAM FACTORY" |
| 2 | `nha-may-cong-ty-scon` (completed) | `nha-may-scon` (ongoing) | Same road (đường 29, VSIP II-A), same scale ~8,337 m², matching en title "SCON FACTORY" |
| 3 | `nha-may-semivina-nissi` (completed, gallery 6 > 2) | `semivina-nissi-el` (completed) | Identical location "Số/NO 48 đường 6, VSIP II", identical scale 6.700 m² |
| 4 | `nha-may-advanced-casting-asia` (completed) | `semivina-nissi-factory` (ongoing — **slug is wrong**: its title/content is 100% "NHÀ MÁY ADVANCED CASTING ASIA", nothing to do with Semivina-Nissi) | Same road (đường 26, VSIP II), same en title "ADVANCED CASTING ASIA FACTORY"; this also fixes a slug/content mismatch identical in kind to the mislabeled listings the original (superseded) analysis found under different slugs |
| 5 | `nha-may-clotex-labels-viet-nam` (completed) | `nha-may-clotex-labels` (ongoing) | Same location (đường 24, thị xã Tân Uyên), same scale ~8,565.4 m² |
| 6 | `nha-may-tien-len-2` (completed, has `en` title) | `nha-may-tien-len` (completed) | Same location (Đường số 3, KCN Tân Tạo), same client family ("Tiến Lên") |
| 7 | `nha-may-rebisco` (completed, gallery 10 > 5) | `rebisco-factory` (completed) | Same client (Republic Biscuit), same location (VSIP II-A), same scale 20.000 m² |
| 8 | `stfood-marketing-factory-vn` (completed, gallery 18, one of the 5 flagship rich-content projects) | `nha-may-stfood-marketing-tai-viet-nam` (ongoing) | Same client (S.T.FOOD MARKETING Việt Nam), same address (đường 24, VSIP II-A) |
| 9 | `nha-hang-konimiyaki` (completed, has `en` title) | `nha-hang-okonomiyaki` (completed) | Same district (Quận 1), same scale 400 m² — and `nha-hang-okonomiyaki`'s own first content line literally reads "Tên dự án: Nhà hàng konimiyaki" (Project name: Konimiyaki restaurant), i.e. the source page names the other slug as its own title |
| 10 | `nha-may-go-cong-ty-akati` (completed, has `en` title) | `akati-wood-factory` (completed) | Same client (Akati Wood Việt Nam), same location (KCN VSIP 2), same scale 20.000 m² |
| 11 | `mo-rong-nha-may-red-bull-2` (completed, gallery 13 > 7) | `mo-rong-nha-may-red-bull` (ongoing) | Same client (Red Bull Việt Nam), same location, same scale 2.000 m² |

### 3b. Flagged, not merged — genuine ambiguity, left for manual review

`nha-may-lam-hiep-hung-2` (completed, gallery 6, client "Công ty TNHH Lâm Hiệp Hưng & Công ty TNHH Tân Toàn Phát") has a client string nearly identical to `nha-may-lam-hiep-hung-tan-toan-phat-2` (ongoing, gallery **32**, en title "LAM HIEP HUNG FACTORY") — matching the cross-status pattern in 3a closely enough that it might be pair #12. It is **deliberately not auto-merged**: `nha-may-lam-hiep-hung-tan-toan-phat-2` is also one of a trio (`nha-may-lam-hiep-hung-tan-toan-phat`, `-2`, `-3` — see 3d) that 3d's own evidence treats as three legitimately distinct buildings; merging `-2` here would remove it from that trio and there isn't enough evidence to be sure which reading is right. Leave all 4 rows as-is. Do not silently decide; if this needs resolving, ask before writing `SLUG_ACTIONS`/`MERGE_PAIRS` entries for it.

### 3c. Title cleanup (not a duplicate — a scrape artifact)

`nha-may-san-xuat-bao-bi-amiba-nha-may-san-xuat-bao-bi-amiba`'s vi title is the project name **rendered twice concatenated**: `"NHÀ MÁY SẢN XUẤT BAO BÌ AMIBA NHÀ MÁY SẢN XUẤT BAO BÌ AMIBA"`. Fix by replacing with the de-duplicated single occurrence: `"NHÀ MÁY SẢN XUẤT BAO BÌ AMIBA"`. Leave the slug as-is (already reasonable).

### 3d. Keep as-is, no fix needed (verified by content, documented so nobody "fixes" them incorrectly later)

- `nha-may-lam-hiep-hung-tan-toan-phat`, `-2`, `-3` — same client ("Lâm Hiệp Hưng & Tân Toàn Phát")/location ("Tỉnh Bình Dương") but three different galleries (5, 32, 14 images) — different buildings/phases of one large complex, all `status: ongoing`. Keep all three as separate `Project` rows, but append a distinguishing suffix to the display name in every language present so the admin list isn't three identical-looking rows: `" – Khu 1"` / `" – Khu 2"` / `" – Khu 3"`. (See 3b — do not fold `nha-may-lam-hiep-hung-2` into this trio.)
- `nha-xuong-nbdc` / `van-phong-nbdc` / `nha-an-nbdc` — same client (Công ty TNHH NBDC VN)/location (KCN Giang Điền), legitimately distinct buildings (factory/office/canteen). No change.
- `trung-tam-the-duc-the-thao-thu-duc` and its 5 siblings (`coffee-tdtt-thu-duc`, `nha-dich-vu-tdtt-thu-duc`, `nha-hang-tiec-cuoi-thu-duc`, `nha-tap-da-nang-thu-duc`, `nha-dich-vu-ho-boi`) — same client (Công ty Cổ phần Thủ Thiêm Group)/location, distinct buildings within one sports complex. No change.
- `noi-that-great-lotus` (GREAT LOTUS INTERIOR, gallery 12) vs. the merged `nha-may-great-lotus-vietnam` (GREAT LOTUS VIETNAM FACTORY, 3a #1) — same site/scale (31.187 m²), but interior-fitout vs. shell-construction scope (different en title, different `Scope`). Legitimately two different `Project` rows. No change.
- `nha-b37-2` (VĂN PHÒNG B37 / B37 OFFICE, gallery 31, one of the 5 flagship rich-content projects) vs. `noi-that-nha-b37` (NỘI THẤT - VĂN PHÒNG B37 / INTERIOR - B37 OFFICE, gallery 26) — same client/location, but same interior/main-building split as Great Lotus above. No change.
- `nha-xuong-d22` (D22 FACTORY) vs. `nha-o-d22` (D22 HOTEL, one of the 5 flagship) — both have "D22" in the name but are unrelated projects (different building types). No change, don't merge on name similarity alone.

### 3e. Known data gaps from Task 1 (network failures during scrape, not fixup bugs)

- `nha-may-tan-thanh-long-4`: vi detail page fetch failed with an SSL handshake timeout mid-scrape. `translations.vi.content` is `[]`, no `en` translation exists at all, gallery is empty in the JSON even though 4 images already exist on disk from a prior sync. **Step 1 of the fixup script attempts one retry** (re-running the scraper is safe/idempotent per its own design) before proceeding; if the retry also fails, leave the entry as-is with empty content and flag it in the run's stderr output rather than blocking the rest of the task on one flaky network call.
- `nha-hang-hokkaido`: not a fetch failure — its `vi` content genuinely has zero text blocks (only 2 image blocks), while its paired `en` content (slug `hokkaido-restaurant-2`) has the full labeled Client/Location/Size/Contract lines. Handled generically by 3f's VI→EN fallback below, no special-case code needed for this slug specifically.

### 3f. Extract structured fields (`Client`/`Location`/`Scale`/`Scope`) out of the content-block array

Confirmed real shape (`nha-may-bma-tai-kcn-huu-thanh`, vi, from Task 1's report) — **text entries are bare strings**:

```json
"content": [
  "Nhà Máy BMA tại KCN Hựu Thạnh",
  "Khách hàng: Công ty TNHH Bảo Minh Ân Việt Nam",
  "Địa điểm: Khu công nghiệp Hựu Thạnh, Đức Hòa, Tây Ninh",
  "Quy mô dự án: 15.000 m2",
  "Phạm vi công việc: Thiết kế và Thi công",
  {"type": "image", "url": "/images/projects/.../img-01.jpg"}
]
```

Two more real-data wrinkles found by re-inspecting actual output (not present in `bma-tai-kcn-huu-thanh`'s simple case, but real elsewhere):

1. **A separate `"Tên dự án: ..."` / `"Project name: ..."` line can appear**, sometimes *instead of* a bare title-echo, sometimes *in addition to* one (e.g. `nha-hang-konimiyaki`'s vi content is `["NHÀ HÀNG KONIMIYAKI", "Tên dự án: Nhà hàng Konimiyaki", "Khách hàng: ...", ...]` — a title-echo **and** a `Tên dự án:` line back to back). This must be recognized and discarded, not just the bare title-echo, or the header-scan stops one line too early and everything after it (including the real Khách hàng/Địa điểm/... lines) gets misclassified as narrative `Content`.
2. **`nha-hang-hokkaido`'s vi content has zero text blocks at all** (see 3e) — its Client/Location/Scale/Scope must come from the `en` translation instead. General rule: extract from `vi` first; for any of the 4 fields still empty afterward, extract the same field from `en` and use that as the fallback value for the base (vi) row.

```python
SKIP_PATTERNS = [
    re.compile(r"^\s*(?:Tên dự án|Project name)\s*:\s*.*$", re.I),
]

FIELD_LABEL_PATTERNS = {
    "client": [
        re.compile(r"^\s*Khách hàng\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*Client\s*:\s*(.+)$", re.I),
    ],
    "location": [
        re.compile(r"^\s*(?:Vị trí|Địa điểm)\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*Location\s*:\s*(.+)$", re.I),
    ],
    "scale": [
        re.compile(r"^\s*(?:Quy mô(?: dự án)?|Diện tích)\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*(?:Scale of project|Size of project|Project size)\s*:\s*(.+)$", re.I),
    ],
    "scope": [
        re.compile(r"^\s*(?:Hợp đồng|Phạm vi)\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*(?:Scope of work|Contract Type|Contract)\s*:\s*(.+)$", re.I),
    ],
}


def extract_fields_from_content(blocks: list, title: str = "") -> tuple[dict[str, str], list]:
    """Pull labeled header lines out of a content-block array. Text
    entries are bare strings; only image entries are dicts
    ({"type": "image", "url": ...}) — confirmed against real Task 1
    output, do not assume {"type": "text", "value": ...} dicts.

    Skips at most one leading title-echo block (exact match against the
    page's own title, passed in separately — never guessed) plus any
    number of "Tên dự án:"/"Project name:" lines wherever they appear
    in the header run, then matches labeled fields. The first block
    that is neither a title-echo, a SKIP_PATTERNS match, nor a
    FIELD_LABEL_PATTERNS match ends the header scan — real narrative
    paragraphs are long unlabeled prose and must not be swallowed by
    over-eager header detection.
    """
    fields: dict[str, str] = {}
    remaining: list = []
    still_scanning_header = True
    skipped_title_echo = False
    title = title.strip()
    for block in blocks:
        if still_scanning_header and isinstance(block, str):
            text = block.strip()
            if not skipped_title_echo and title and text == title:
                skipped_title_echo = True
                continue
            if any(p.match(text) for p in SKIP_PATTERNS):
                continue
            matched = False
            for field_name, patterns in FIELD_LABEL_PATTERNS.items():
                for pattern in patterns:
                    m = pattern.match(text)
                    if m:
                        fields[field_name] = m.group(1).strip()
                        matched = True
                        break
                if matched:
                    break
            if matched:
                continue
            still_scanning_header = False
        else:
            still_scanning_header = False
        remaining.append(block)
    return fields, remaining
```

### 3g. Category assignment

Match against the 16 existing `ProjectCategory` names from `ContentSeeder.SeedCategories()` (`nihomebackend/Data/ContentSeeder.cs:32-50`). Unaffected by the string-vs-dict content-block correction (it only reads titles/slug/scope, never content blocks). Apply rules in this order (first match wins) against a **combined search string per project**, not just the English title — 31 vi cards vs. 36 en cards for `projectsongoing` (54 vs. 49 for completed) confirms some projects have no paired English card at all, so an English-only rule set would silently return no category for those:

```python
CATEGORY_RULES = [
    (r"\bpharma\b|central pharmaceutical|dược phẩm", "Nhà máy dược phẩm"),
    (r"\bwarehouse\b|nhà kho", "Nhà kho logistics"),
    (r"\binterior\b.*\boffice\b|\boffice\b.*\binterior\b|nội thất.*văn phòng", "Nội thất văn phòng"),
    (r"\binterior\b|nội thất", "Nội thất công nghiệp"),
    (r"\brestaurant\b|nhà hàng", "Nhà hàng"),
    (r"\bhotel\b|\bresort\b|khách sạn", "Khách sạn"),
    (r"\bhouse\b|\bresidence\b|\bdormitory\b|nhà ở|ký túc xá", "Nhà ở"),
    (r"\bshow flat\b", "Bất động sản"),
    (r"\bshowroom\b", "Thương mại"),
    (r"\bschool\b|trường học", "Giáo dục"),
    (r"\bstudio\b", "Studio"),
    (r"sport center|multi-purpose building|service building|swimming pool|trung tâm thể thao", "Công trình công cộng"),
    (r"\bcanteen\b|\bcoffee\b|căng tin", "Nội thất công nghiệp"),
    (r"\boffice\b|văn phòng", "Văn phòng"),
    (r"\bfactory\b|\bworkshop\b|\bworksop\b|nhà máy|nhà xưởng", "Nhà máy công nghiệp"),
]
```

Anything matching none of these gets `category = null` and must be listed explicitly in the Task 3 commit message for manual follow-up.

- [ ] **Step 1: Write `tools/scrape_legacy_data/fixup_projects.py`**

```python
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

SKIP_PATTERNS = [
    re.compile(r"^\s*(?:Tên dự án|Project name)\s*:\s*.*$", re.I),
]

FIELD_LABEL_PATTERNS = {
    "client": [
        re.compile(r"^\s*Khách hàng\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*Client\s*:\s*(.+)$", re.I),
    ],
    "location": [
        re.compile(r"^\s*(?:Vị trí|Địa điểm)\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*Location\s*:\s*(.+)$", re.I),
    ],
    "scale": [
        re.compile(r"^\s*(?:Quy mô(?: dự án)?|Diện tích)\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*(?:Scale of project|Size of project|Project size)\s*:\s*(.+)$", re.I),
    ],
    "scope": [
        re.compile(r"^\s*(?:Hợp đồng|Phạm vi)\s*:\s*(.+)$", re.I),
        re.compile(r"^\s*(?:Scope of work|Contract Type|Contract)\s*:\s*(.+)$", re.I),
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


def extract_fields_from_content(blocks: list, title: str = "") -> tuple[dict[str, str], list]:
    fields: dict[str, str] = {}
    remaining: list = []
    still_scanning_header = True
    skipped_title_echo = False
    title = title.strip()
    for block in blocks:
        if still_scanning_header and isinstance(block, str):
            text = block.strip()
            if not skipped_title_echo and title and text == title:
                skipped_title_echo = True
                continue
            if any(p.match(text) for p in SKIP_PATTERNS):
                continue
            matched = False
            for field_name, patterns in FIELD_LABEL_PATTERNS.items():
                for pattern in patterns:
                    m = pattern.match(text)
                    if m:
                        fields[field_name] = m.group(1).strip()
                        matched = True
                        break
                if matched:
                    break
            if matched:
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
```

**Gallery/content merging is mandatory, not deferred, and self-verifying:** `main()` calls `merge_gallery_and_content()` for all 11 `MERGE_PAIRS` entries *before* the drop pass runs, capturing before/after counts and `assert`-ing the merged gallery and content-block counts actually grew (not just "> 0", which would pass even if the union silently did nothing). If a merge doesn't actually fold the loser's data in, **the script crashes with an `AssertionError` in Step 2, before any file is written** — this is the primary safety net, not a separate check run afterward.

- [ ] **Step 2: Run it**

```bash
python3 tools/scrape_legacy_data/fixup_projects.py
```

Expected: possibly one `[retry] ...` line (only if `nha-may-tan-thanh-long-4` is still empty from Task 1), then 11 `[merge]` lines showing real before→after counts with no `AssertionError`, then 11 `[drop]` lines matching `SLUG_ACTIONS`, ends with `74 projects kept (from 85 scraped)` (85 − 11 drops; the 3b-flagged pair and the 3c title fixup do not change the count), and ideally zero `[warn]` unmatched-category lines. A non-zero exit / traceback here means the merge itself is broken — fix `merge_gallery_and_content()` before touching anything else, do not proceed to Step 3.

- [ ] **Step 3: Spot-check the count and field extraction on the committed output**

This step is a lighter confirmatory read of the file Step 2 already wrote and verified — it exists to catch slug/data-shape mistakes the in-script assertions don't cover (they only check counts, not which slugs ended up where).

```bash
python3 -c "
import json
data = json.load(open('nihomebackend/Data/Seeds/content/projects.json'))
print('total:', len(data))
assert len(data) == 74, f'expected 74 projects, got {len(data)}'
by_slug = {d['slug']: d for d in data}
for winner in ('nha-may-great-lotus-vietnam', 'nha-may-advanced-casting-asia',
               'nha-may-rebisco', 'stfood-marketing-factory-vn',
               'mo-rong-nha-may-red-bull-2', 'nha-may-cong-ty-scon'):
    assert winner in by_slug, f'{winner} (merge winner) missing'
for loser in ('nha-may-great-lotus', 'semivina-nissi-el', 'semivina-nissi-factory',
              'nha-may-clotex-labels', 'nha-may-tien-len', 'rebisco-factory',
              'nha-may-stfood-marketing-tai-viet-nam', 'nha-hang-okonomiyaki',
              'akati-wood-factory', 'mo-rong-nha-may-red-bull', 'nha-may-scon'):
    assert loser not in by_slug, f'{loser} should have been dropped after merge'
# Task 3b: both sides of the flagged-not-merged pair must still be present.
assert 'nha-may-lam-hiep-hung-2' in by_slug
assert 'nha-may-lam-hiep-hung-tan-toan-phat-2' in by_slug

bma = by_slug.get('nha-may-bma-tai-kcn-huu-thanh')
assert bma, 'nha-may-bma-tai-kcn-huu-thanh missing'
assert bma.get('client'), 'client extraction failed for nha-may-bma-tai-kcn-huu-thanh'
assert bma.get('location'), 'location extraction failed for nha-may-bma-tai-kcn-huu-thanh'
print('client:', bma['client'])
print('location:', bma['location'])

amiba = by_slug.get('nha-may-san-xuat-bao-bi-amiba-nha-may-san-xuat-bao-bi-amiba')
assert amiba['translations']['vi']['title'] == 'NHÀ MÁY SẢN XUẤT BAO BÌ AMIBA', amiba['translations']['vi']['title']

hokkaido = by_slug.get('nha-hang-hokkaido')
assert hokkaido and hokkaido.get('client'), 'nha-hang-hokkaido client should be filled via en fallback'
print('hokkaido client (via en fallback):', hokkaido['client'])
print('OK')
"
```

- [ ] **Step 4: Commit**

```bash
git add nihomebackend/Data/Seeds/content/projects.json tools/scrape_legacy_data/fixup_projects.py
git commit -m "seed: dedupe, extract structured fields, categorize projects

Drops 11 confirmed duplicate CMS records, found by matching
extracted client+location rather than by slug/title similarity
(8 of the 11 are the same project double-listed under both
projectsongoing and projectscompleted — completed wins). Unions
gallery/content into the surviving record before each drop.

Fixes one doubled-title scrape artifact (AMIBA). Pulls labeled
Client/Location/Scale/Scope lines out of the scraped content
blocks into their own fields, with an en fallback for the one
project (nha-hang-hokkaido) whose vi page has no labeled text at
all. Infers ProjectCategory from project name/slug/scope in both
languages.

One pair (nha-may-lam-hiep-hung-2 / -tan-toan-phat-2) matches the
cross-status duplicate pattern closely enough to be a 12th case,
but is left unmerged — ambiguous evidence, flagged in Task 3b for
manual review rather than decided here."
```

---

## Task 4: Translate Name/Description/Content into vi/en/zh/ja

**Files:**
- Modifies: `nihomebackend/Data/Seeds/content/projects.json` (base language — vi)
- Modifies: `nihomebackend/Data/Seeds/content/project-translations.json`

**Base-language decision:** every other seeded `Project` today stores **Vietnamese** as the un-translated base (`Project.Name`/`Project.Description`/now `Project.ContentJson`), with `project-translations.json` carrying `en`/`zh`/`ja` overlays. The scraper already captured real native `vi` and `en` content separately (confirmed different card counts: 31 vi vs 36 en for ongoing, 54 vs 49 for completed) — **use the scraped native text for vi and en directly, do not machine-translate either of them.** Only fall back to AI-translating EN→VI for the handful of projects where the vi pass has no matching card (Task 1 Step 2's per-language counts show how many that is).

zh/ja have no native source on nicon.vn (confirmed: `/changelanguage/3` and `/changelanguage/5` were tested against 5 live pages in this session with proper cookie persistence, and project content did not change language for any of them). These must be AI-translated.

- [ ] **Step 1: Set `Name`/`Description`/`Content` for all 74 projects**

**vi (base, in `projects.json`):** `name` = the scraped `translations.vi.title` (already corrected for the one known scrape artifact — `nha-may-san-xuat-bao-bi-amiba-nha-may-san-xuat-bao-bi-amiba`'s doubled title, fixed in Task 3c). `description` = **do not fabricate, and do not leave it `null`** — set the explicit empty string `""` for every one of the 74 projects (none of the samples inspected in this session had a real short summary distinct from the full content; the S.T.FOOD/TRIMAS-style pages go straight from labeled fields into the full article, no separate excerpt). `""` vs. `null` matters here even though `Project.Description` is a nullable column: an explicit `""` in the seed JSON tells the next person reading it "this was considered and is genuinely empty," where `null`/an omitted key reads as "not yet filled in." `content` = the scraped `translations.vi.content` after Task 3's field-extraction (the real narrative + inline images, empty array `[]` for the ~69 simple projects).

**en (`project-translations.json`):** `Name` = scraped `translations.en.title`, light cleanup only (title-case instead of ALL CAPS, fix carried-over typos like "Worksop"→"Workshop" per the `NAME_OVERRIDES` already applied in Task 3 — don't re-invent that here). `Description` = `""` (same reasoning as vi — note `SeedProjectTranslations()` skips writing an `EntityTranslations` row for any whitespace-only value, so an empty-string `Description` here produces no DB row either way; the explicit `""` is for the seed JSON's own clarity, not a runtime requirement). `Content` = scraped `translations.en.content` after field-extraction, serialized as a JSON string (matching how `SeedProjectTranslations` already stores `Challenges`/`Solutions` as serialized-array strings — `Content` follows the same convention: the field value in `project-translations.json` is `JsonSerializer.Serialize(contentBlocksArray)`).

**zh / ja (`project-translations.json`):** AI-translate `Name` and each text block's `value` inside `Content` from the vi or en source (whichever is more complete for that project) into natural Simplified Chinese / Japanese, construction-industry register, matching the tone of existing zh/ja entries already in `project-translations.json`. `Description` = `""`, same as en. **Do not translate `url` or `caption`-less image blocks** — an image block's `url` must be copied through unchanged; only translate a `caption` key if present. If a project's `NAME_SUFFIX_ZONE` entry from Task 3 appended a zone marker to `vi.title`/`en.title` (the `lam-hiep-hung-tan-toan-phat-factory` trio), carry the equivalent marker into the zh/ja `Name` translation too — e.g. `"... - 区1"` / `"... - ゾーン1"` — so all 4 languages distinguish the 3 rows the same way.

**`Content` is a required key on every language object for every one of the 74 projects, with no exceptions** — this is what "cập nhật đa ngôn ngữ 4 thứ tiếng cho tất cả project" actually requires once `Content` is one of the fields Content Translations shows for Project (Task 2 Step 7). For the ~69 "simple" projects whose content is fully consumed by Task 3's field-extraction (leaving an empty `remaining` array), the correct value is the literal string `"[]"` — not an omitted key, not `null`. Task 2's `DeserializeContent()` treats `null`/empty-string/`"[]"` identically at read time, but the seed data itself must always carry `Content` explicitly so a missing key can never be silently mistaken for "translation not done yet" versus "genuinely empty for this simple project" during review.

Output shape for `project-translations.json` (extends the existing per-slug shape with `Content`). Two worked examples — a "simple" project (Content is empty after Task 3's field-extraction consumed everything) and a "flagship" one (Content keeps the real narrative + inline images):

```json
{
  "slug": "nha-may-bma-tai-kcn-huu-thanh",
  "en": {
    "Name": "BMA Factory - Huu Thanh Industrial Park",
    "Description": "",
    "Content": "[]"
  },
  "zh": { "Name": "BMA工厂 - 友诚工业园", "Description": "", "Content": "[]" },
  "ja": { "Name": "BMAファクトリー - フータイン工業団地", "Description": "", "Content": "[]" }
}
```

```json
{
  "slug": "stfood-marketing-factory-vn",
  "en": {
    "Name": "S.T.FOOD Marketing Factory in Vietnam",
    "Description": "",
    "Content": "[\"S.T.FOOD FACTORY AND THE INDUSTRIAL FACTORY DESIGN TREND\",\"The STFood Company factory at VSIP II IP, designed and built by Nicon, is clear evidence of progress in modern industrial architecture...\",{\"type\":\"image\",\"url\":\"/images/projects/stfood-marketing-factory-vn/img-03.jpg\"}]"
  },
  "zh": { "Name": "越南 S.T.FOOD 营销工厂", "Description": "", "Content": "[\"S.T.FOOD工厂与工业厂房设计趋势\",\"...\",{\"type\":\"image\",\"url\":\"/images/projects/stfood-marketing-factory-vn/img-03.jpg\"}]" },
  "ja": { "Name": "S.T.FOODマーケティング工場（ベトナム）", "Description": "", "Content": "[\"S.T.FOOD工場と工業工場デザインのトレンド\",\"...\",{\"type\":\"image\",\"url\":\"/images/projects/stfood-marketing-factory-vn/img-03.jpg\"}]" }
}
```

This is a translation-generation pass over 74 records, not a deterministic transform — execute it directly (as Claude or Codex, whichever agent runs this task) rather than writing a script. Budget it as its own reviewable commit, not folded into Task 3's mechanical fixups.

**Do not fabricate Challenges/Solutions/Highlights** for real projects — the live site has none of these sections (confirmed on every page inspected, including the rich S.T.FOOD/TRIMAS ones — the narrative prose is not structured into a "challenges" vs. "solutions" split anywhere). Leave `ChallengesJson`/`SolutionsJson`/`HighlightsJson` null for every imported project. This is a deliberate divergence from the old 75 fake entries, which had elaborate fabricated Challenges/Solutions text.

- [ ] **Step 2: Validate output shape before committing**

```bash
python3 -c "
import json
proj = json.load(open('nihomebackend/Data/Seeds/content/projects.json'))
trans = json.load(open('nihomebackend/Data/Seeds/content/project-translations.json'))
proj_slugs = {p['slug'] for p in proj}
trans_slugs = {t['slug'] for t in trans}
missing = proj_slugs - trans_slugs
assert not missing, f'{len(missing)} projects have no translation entry: {missing}'
for p in proj:
    assert p.get('name'), f\"{p['slug']} missing vi base name\"
    assert 'content' in p, f\"{p['slug']} missing vi content key (use [] if genuinely empty)\"
    assert isinstance(p['content'], list), f\"{p['slug']} vi content is not an array\"
    assert p.get('description', None) == '', f\"{p['slug']} vi description should be explicit '' (none of the 74 real projects have real summary text), got {p.get('description')!r}\"
for t in trans:
    for lang in ('en', 'zh', 'ja'):
        assert lang in t, f\"{t['slug']} missing {lang}\"
        assert t[lang].get('Name'), f\"{t['slug']} missing {lang}.Name\"
        assert t[lang].get('Description', None) == '', f\"{t['slug']} {lang}.Description should be explicit '', got {t[lang].get('Description')!r}\"
        assert 'Content' in t[lang], f\"{t['slug']} missing {lang}.Content (use the string '[]' if genuinely empty, never omit the key)\"
        parsed = json.loads(t[lang]['Content'])  # must be valid JSON-encoded array
        assert isinstance(parsed, list), f\"{t['slug']} {lang}.Content did not decode to a JSON array\"
print(f'{len(proj)} projects, all have vi name+description+content, en/zh/ja Name+Description+Content (valid JSON array). OK')
"
```

- [ ] **Step 3: Commit**

```bash
git add nihomebackend/Data/Seeds/content/projects.json nihomebackend/Data/Seeds/content/project-translations.json
git commit -m "seed: add vi/en/zh/ja text and content blocks for projects

vi/en are the scraped native CMS passes (not machine-translated).
zh/ja are AI-translated (nicon.vn has no native zh/ja project
content) — image block URLs are preserved unchanged."
```

---

## Task 5: Wire `ContentSeeder.SeedProjects()` to load from the manifest

**Files:**
- Modify: `nihomebackend/Data/ContentSeeder.cs:330-451` (replace `SeedProjects` body and remove the hardcoded `Project[]` literal)
- Test: `nihomebackend.tests/Data/ContentSeederTests.cs`

**Interfaces:**
- Consumes: `Data/Seeds/content/projects.json` (finalized by Task 4), embedded resource `nihomebackend.Data.Seeds.content.projects.json`
- Produces: `Project` rows with `Slug/ImageUrl/GalleryJson/Name/Client/Location/Scale/Scope/Status/Year/Category/Description/ContentJson/ChallengesJson/SolutionsJson/SortOrder` populated. `ProjectCategoryId` is left for `LinkCategories()` (already runs right after `SeedProjects()`, `nihomebackend/Data/ContentSeeder.cs:24`) to backfill by matching the `Category` string.

**`SeedProjectTranslations()` (`nihomebackend/Data/ContentSeeder.cs:452-522`) needs zero code changes for `Content` to work.** Read in full during this plan's investigation: its inner loop (`foreach (var fieldProp in langProp.Value.EnumerateObject())`) stores whatever JSON property names exist under each language object as `EntityTranslation.FieldName` — it has no hardcoded field list. Since Task 4 writes `project-translations.json` entries as `{"en": {"Name": "...", "Content": "..."}}` (both string-valued, matching what `fieldProp.Value.GetString()` expects), a `Content` row will flow into `EntityTranslations` with `FieldName == "Content"` the same way `Name` already does today, no different from any other field name that happens to appear in that JSON. Step 1's third test below asserts this directly rather than leaving it as an unverified read of the code.

- [ ] **Step 1: Write the failing test**

Add to `nihomebackend.tests/Data/ContentSeederTests.cs` (follow the existing `Seed_DoesNotOverwriteAdminEditedNewsTranslation` pattern already in that file). The new tests below use `EntityTypes.Project` — confirmed in this session that the file's existing `using NihomeBackend.Constants;` (its first import line) already covers this, no new `using` needed:

```csharp
[Fact]
public void Seed_LoadsProjectsFromManifest_NotHardcodedArray()
{
    ContentSeeder.Seed(_db);

    var bmaFactory = _db.Projects.FirstOrDefault(p => p.Slug == "nha-may-bma-tai-kcn-huu-thanh");
    Assert.NotNull(bmaFactory);
    Assert.False(string.IsNullOrWhiteSpace(bmaFactory!.Client));
    Assert.False(string.IsNullOrWhiteSpace(bmaFactory.Location));

    // The old fake placeholder slug (different from the real scraped one
    // above) must no longer be seeded by fresh runs.
    Assert.Null(_db.Projects.FirstOrDefault(p => p.Slug == "nha-may-bma"));
}

[Fact]
public void Seed_PopulatesContentJson_ForProjectsWithRealNarrative()
{
    ContentSeeder.Seed(_db);

    var stfood = _db.Projects.FirstOrDefault(p => p.Slug == "stfood-marketing-factory-vn");
    Assert.NotNull(stfood);
    Assert.NotEqual("[]", stfood!.ContentJson);
}

[Fact]
public void Seed_PopulatesEnContentTranslation_ViaExistingGenericLoader()
{
    // SeedProjectTranslations() is unmodified by this plan — it already
    // stores any string-valued field name it finds under each language
    // object in project-translations.json. This confirms "Content"
    // flows through it the same way "Name" already does, with zero
    // changes to that method.
    ContentSeeder.Seed(_db);

    var stfood = _db.Projects.FirstOrDefault(p => p.Slug == "stfood-marketing-factory-vn");
    Assert.NotNull(stfood);

    var enContent = _db.EntityTranslations.FirstOrDefault(t =>
        t.EntityType == EntityTypes.Project && t.EntityId == stfood!.Id &&
        t.FieldName == "Content" && t.LanguageCode == "en");
    Assert.NotNull(enContent);
    Assert.False(string.IsNullOrWhiteSpace(enContent!.Value));
}

[Fact]
public void Seed_IsBackfillOnly_ForProjects()
{
    ContentSeeder.Seed(_db);
    var before = _db.Projects.Count();

    var project = _db.Projects.First();
    project.Description = "Admin-edited description";
    _db.SaveChanges();

    ContentSeeder.Seed(_db);
    var after = _db.Projects.Count();
    Assert.Equal(before, after);
    Assert.Equal("Admin-edited description", _db.Projects.First(p => p.Id == project.Id).Description);
}
```

- [ ] **Step 2: Run to verify it fails**

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~ContentSeederTests"
```

Expected: FAIL — `nha-may-bma-tai-kcn-huu-thanh` doesn't exist yet (still hardcoded fake data).

- [ ] **Step 3: Replace `SeedProjects()` — delete the hardcoded array (lines 330-451), replace with:**

```csharp
private sealed class ProjectSeedItem
{
    public string Slug { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public List<string> Gallery { get; set; } = [];
    public string Name { get; set; } = "";
    public string Client { get; set; } = "";
    public string Location { get; set; } = "";
    public string Scale { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Status { get; set; } = "ongoing";
    public string? Year { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public List<JsonElement>? Content { get; set; }
    public List<string>? Challenges { get; set; }
    public List<string>? Solutions { get; set; }
    public int SortOrder { get; set; }
}

private static List<ProjectSeedItem> LoadProjectSeed()
{
    const string resourceName = "nihomebackend.Data.Seeds.content.projects.json";
    var asm = Assembly.GetExecutingAssembly();
    using var stream = asm.GetManifestResourceStream(resourceName);
    if (stream is null) return [];
    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    return JsonSerializer.Deserialize<List<ProjectSeedItem>>(stream, opts) ?? [];
}

private static void SeedProjects(AppDbContext db)
{
    var manifest = LoadProjectSeed();
    if (manifest.Count == 0) return;

    var existingSlugs = db.Projects.Select(p => p.Slug).ToHashSet();
    var newItems = manifest
        .Where(item => !existingSlugs.Contains(item.Slug))
        .Select(item => new Project
        {
            Slug = item.Slug,
            ImageUrl = item.ImageUrl,
            GalleryJson = item.Gallery.Count > 0 ? JsonSerializer.Serialize(item.Gallery) : null,
            Name = item.Name,
            Client = item.Client,
            Location = item.Location,
            Scale = item.Scale,
            Scope = item.Scope,
            Status = item.Status,
            Year = item.Year,
            Category = string.IsNullOrWhiteSpace(item.Category) ? null : item.Category,
            Description = item.Description,
            ContentJson = item.Content is { Count: > 0 } ? JsonSerializer.Serialize(item.Content) : "[]",
            ChallengesJson = item.Challenges is { Count: > 0 } ? JsonSerializer.Serialize(item.Challenges) : null,
            SolutionsJson = item.Solutions is { Count: > 0 } ? JsonSerializer.Serialize(item.Solutions) : null,
            SortOrder = item.SortOrder,
        })
        .ToList();

    if (newItems.Count > 0)
    {
        db.Projects.AddRange(newItems);
        db.SaveChanges();
    }
}
```

Note: the old implementation's trailing "stale `/images/upload/` backfill" block is deliberately **not** carried over — it migrated projects from the legacy `/images/upload/projects/...` runtime-upload path, which doesn't apply here: the scraper writes real images to `/images/projects/<slug>/...` directly (`/images/upload/` is reserved for runtime admin uploads and stays empty per `tools/scrape_legacy_data/README.md`).

- [ ] **Step 4: Run the tests again to verify they pass**

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true \
  --filter "FullyQualifiedName~ContentSeederTests"
```

- [ ] **Step 5: Full backend test suite (catch any other test hardcoding the old fake slugs/count)**

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
```

If any test references old fake slugs (`nha-may-bma`, etc.) or a hardcoded `Projects.Count()` of 75, update that test to use a real slug from the new manifest or a manifest-driven count instead of a magic number.

- [ ] **Step 6: Commit**

```bash
git add nihomebackend/Data/ContentSeeder.cs nihomebackend.tests/Data/ContentSeederTests.cs
git commit -m "feat: load Project seed data from manifest, not hardcoded array

SeedProjects() previously hardcoded ~75 fictional placeholder
projects directly in C#. It now loads from projects.json the
same way SeedActivities/SeedNews already do, including the real
content blocks added in the previous commits."
```

---

## Task 6: Manual QA

**Files:** none (verification only)

- [ ] **Step 1: Rebuild and restart the backend so `ContentSeeder` runs against a fresh DB**

```bash
docker compose up --build -d
```

- [ ] **Step 2: Verify the public site**

Open `http://localhost:5043/projects?status=ongoing` — expect **25** real projects (31 scraped `ongoing`, minus 6 of Task 3a's 11 merge pairs where the `ongoing`-status side was the loser and got dropped), matching names/images on `https://nicon.vn/projectsongoing`.
Open `http://localhost:5043/projects?status=completed` — expect **49** real projects (54 scraped `completed`, minus the other 5 merge pairs where the `completed`-status side was the loser — every one of the 11 merge winners already had `status: completed` before Task 3 ran, so `FORCE_STATUS_COMPLETED` never actually had to change anything; it's there defensively in case `MERGE_PAIRS` is ever edited to pick a different winner), matching `https://nicon.vn/projectscompleted`.
Open the S.T.FOOD project detail page specifically and confirm the "Content blocks" section (`ProjectDetail.tsx:169-179`) renders the real narrative + inline images, not just the gallery.
Confirm no leftover fake placeholder projects appear **if this is a genuinely fresh DB volume** — on a reused dev DB volume they will still be present per the Global Constraints backfill-only note; that's expected, not a bug.

- [ ] **Step 3: Verify admin → Content Translations → Project**

Log into `/admin/translations`, select Content type: Project. For a sample of 5 projects across different categories (including at least one "rich" one like S.T.FOOD/TRIMAS and one "simple" one), confirm all 4 languages (vi/en/zh/ja) show real, non-placeholder text for Name and Content.

- [ ] **Step 4: Verify the admin edit form round-trip**

Open `/admin/projects`, edit the S.T.FOOD project, confirm `ContentBlockEditor` shows the real scraped blocks (not empty), make a trivial edit (e.g. reorder two blocks), save, reload, confirm the edit persisted.

- [ ] **Step 5: Spot-check the data-quality fixes from Task 3**

- `nha-may-advanced-casting-asia` shows only once in the completed list (not also under `semivina-nissi-factory`'s old mislabeled slug).
- `nha-may-advanced-casting-asia`'s gallery has more images than either of the two pre-merge sources alone (union actually happened, not just a drop).
- `nha-may-rebisco` and `stfood-marketing-factory-vn` each appear exactly once (not duplicated under their dropped `rebisco-factory` / `nha-may-stfood-marketing-tai-viet-nam` slugs).
- `nha-may-san-xuat-bao-bi-amiba-nha-may-san-xuat-bao-bi-amiba`'s display name reads "NHÀ MÁY SẢN XUẤT BAO BÌ AMIBA" once, not doubled.
- `nha-may-bma-tai-kcn-huu-thanh`'s `Client`/`Location` fields show correctly extracted values, not raw "Khách hàng: ..." label text.
- `nha-hang-hokkaido`'s `Client`/`Location` fields are populated (via the vi→en fallback in Task 3f — its vi content has no labeled text at all).
- `nha-may-lam-hiep-hung-2` and `nha-may-lam-hiep-hung-tan-toan-phat-2` are both still present (Task 3b's flagged-not-merged pair) — confirms the ambiguous case wasn't silently dropped.

---

## Task 7: Quality gates

- [ ] **Step 1: Backend build**

```bash
docker exec nihome31042025-backend dotnet build
```

- [ ] **Step 2: Backend format check**

```bash
docker exec nihome31042025-backend dotnet format --verify-no-changes
```

- [ ] **Step 3: Full backend test suite** (already run in Task 5 Step 5, re-run here for a clean final pass after all commits)

```bash
docker run --rm -v "$(pwd)":/workspace -w /workspace mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test nihomebackend.tests/nihomebackend.tests.csproj -p:SkipNihomeWebBuild=true
```

- [ ] **Step 4: Frontend lint/build** (no frontend files expected to change — see Global Constraints — but this is the standing CLAUDE.md quality gate)

```bash
cd nihomeweb && npm run lint && npm run build
```

- [ ] **Step 5: Update the decisions log**

Append a dated entry to `docs/ai/memory-bank/05-decisions-and-open-questions.md` recording: (a) the Content-Blocks methodology mistake and correction (stateless WebFetch missed real VI content; verified with a cookie-persistent client instead), (b) the "old fake projects survive on already-seeded DBs" tradeoff from Global Constraints, (c) a pointer to this plan file.

- [ ] **Step 6: Commit the memory-bank update**

```bash
git add docs/ai/memory-bank/05-decisions-and-open-questions.md
git commit -m "docs: record nicon.vn project import decisions"
```
