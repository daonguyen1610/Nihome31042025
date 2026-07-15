# Category i18n — end-to-end design

Date: 2026-07-12
Status: Approved for planning (revised after design review — see "Design
review revision" note)

## Problem

The user reported that categories for Activities, News, and Projects lack
multi-language support both in `admin/categories` and on the client
(public) site, and that `admin/translations` → Content Translation has no
section for categories.

Research corrected part of this premise: `admin/categories` **already**
supports per-language names (`NameVi/NameEn/NameZh/NameJa` columns added in
`56df8db`, 4-language form in `Categories.tsx`). Two real gaps remain:

1. **Client-facing display.** `Activity`/`News`/`Project` API responses
   always return the plain denormalized `Category` string (the Vietnamese
   value), ignoring the `?lang=` query param — unlike `Title`/`Excerpt`/
   `Content`, which are correctly localized via `EntityTranslation`. Public
   pages (`Activities.tsx`, `News.tsx`, `Projects.tsx`, and the three detail
   pages) render this raw string directly.
2. **Content Translation admin tab.** `TranslationsController.GetEntityTypes()`
   has no `Category` entries, and the generic `EntityTranslation`-backed
   switch statements (`GetEntitiesWithTranslationStatus`,
   `GetEntityTranslations`, `SaveEntityTranslations`) don't know how to
   handle Category, because Category i18n uses fixed columns, not
   `EntityTranslation` rows.

A third, smaller gap was found during research and folded into this work
per user decision: `ContentSeeder.cs` has 4 category-creation sites that
don't populate `NameVi`, violating the AGENTS.md rule (added in `c651e57`)
that every per-language field must be populated on every write path.

### Design review revision

The initial version of this spec (Part A below) proposed localizing the
`Category` field returned by `ActivityService`/`NewsService`/`ProjectService`
directly, keyed off the request's `lang`. A design review caught that this
breaks the admin edit forms: `NewsForm.tsx`, `ActivityForm.tsx`, and
`ProjectForm.tsx` all call the same `lang`-aware hooks
(`useNewsItem`/`useActivity`/`useProject`) used by the public site, seed
their category `<select>` from `existing.category`, and on submit resolve
`categoryId` by case-insensitive string match against the (VI-only)
category dropdown list (`NewsForm.tsx:137-140` and equivalents). Since
`AdminLayout.tsx` renders a `LanguageToggle`, an admin working in
`lang=en` would have the form pre-fill an English category name that
cannot match any VI option — silently dropping the `categoryId` and
auto-creating a duplicate category from the English string via
`ResolveCategoryAsync`.

The review also found that `Activities.tsx`/`News.tsx`/`Projects.tsx` track
the selected category filter as a raw display string
(`News.tsx:16,18-23`), which would silently reset to "All" on every
language switch once `Category` becomes lang-dependent — contradicting the
original claim that filters "localize for free."

Part A is revised below to localize category names **client-side only**,
by joining on `categoryId` against the existing category-list endpoints,
instead of changing what the content APIs return. This removes the
admin-form risk entirely (the API contract those forms depend on doesn't
change) and, as a side effect of resolving categories by id, lets the
public filter track selection by id instead of by string — fixing the
filter-reset issue rather than accepting it as a known limitation.

A fourth gap surfaced during the same review: `ActivityService.cs` and
`NewsService.cs` each have a private `ResolveCategoryAsync` that
auto-creates a category without setting `NameVi` (confirmed at
`ActivityService.cs:245-250` and `NewsService.cs:240-245`) — a live
runtime instance of the same bug being fixed in `ContentSeeder.cs` (C
below). `ProjectCategoryService.ResolveAsync` (`ProjectCategoryService.cs:109-143`)
already sets `NameVi` correctly and is a dedicated, reusable service,
unlike Activity/News which duplicate the resolution logic privately inside
their content service. This is folded into Part C.

## Decisions

- **Keep the fixed-column pattern for Category** (`NameVi/NameEn/NameZh/NameJa`
  on `ActivityCategory`/`NewsCategory`/`ProjectCategory`). Do **not** migrate
  Category onto the polymorphic `EntityTranslation` table — that would
  require a data migration and a rewrite of the already-working
  `admin/categories` UI, for no material benefit.
- Content Translation tab support for Category is implemented as a
  **special-cased branch** in `TranslationsController` that reads/writes the
  fixed columns directly, while presenting Category to the frontend as a
  single generic field named `Name` (per selected language) — matching the
  shape the existing generic frontend (`Translations.tsx`) already expects.
  **No frontend changes needed** for this tab; it is already generic over
  whatever `/translations/entity/types` returns.
- Category localization on the public site is done **client-side**, by
  joining each item's `categoryId`/`newsCategoryId` against the existing
  category-list endpoints (already lang-agnostic, already return all 4
  name columns) and reusing the existing `localizedName()` helper
  (`nihomeweb/src/lib/category.ts`). The content APIs' `Category` string
  field and category auto-resolution behavior are **not changed** — this
  keeps the admin forms' existing string-match round-trip intact.
  admin/categories list is unaffected (already complete, out of scope).
- The public list-page category filters (`Activities.tsx`, `News.tsx`,
  `Projects.tsx`) are included in scope: their selection state moves from
  a raw display string to `categoryId`, both to fix the filter-reset bug
  found in review and because the id is already needed for the
  client-side localization join.

## A. Frontend — localize category on public-facing pages (client-side join)

The content APIs (`ActivityService`/`NewsService`/`ProjectService`) are
**not changed**. `Category` keeps returning the legacy VI string exactly
as today; `ResolveCategoryAsync`/category auto-creation behavior is
untouched. This is what keeps the admin forms' string-match round-trip
working regardless of the admin's UI language.

Frontend-only changes:

- `nihomeweb/src/services/contentApi.ts`: add `nameVi`, `nameEn`, `nameZh`,
  `nameJa` to the `ActivityCategoryResponse`, `NewsCategoryResponse`, and
  `ProjectCategoryResponse` interfaces (currently only `id/name/isActive/sortOrder`
  — stale relative to what the backend already returns; `adminApi.ts`'s
  copies of these interfaces already declare the 4 fields correctly, so
  this brings the two in line).
- List pages (`Activities.tsx`, `News.tsx`, `Projects.tsx`) and detail pages
  (`ActivityDetail.tsx`, `NewsDetail.tsx`, `ProjectDetail.tsx`): call the
  existing `useActivityCategories()` / `useNewsCategories()` /
  `useProjectCategories()` hooks alongside the existing content hooks,
  build a `Map<id, category>`, and resolve each item's display category as
  `item.categoryId ? localizedName(categoriesById.get(item.categoryId), lang) : item.category`
  (falling back to the raw string for legacy items with no `categoryId`).
  Reuses the existing `localizedName()` helper from `src/lib/category.ts`
  — no new localization logic on the frontend, no new logic on the backend.
- List-page category filters (`Activities.tsx`, `News.tsx`, `Projects.tsx`):
  change the selected-filter state from a raw category string to
  `categoryId | "all"`. Build filter options from the category list
  (id + localized label) instead of `Set(items.map(i => i.category))`.
  This keeps the selected filter stable across a language switch (the id
  doesn't change, only its rendered label does) and was needed anyway once
  display resolution goes through `categoryId`.

## B. Backend — Category in the Content Translation tab

- `nihomebackend/Constants/EntityTypes.cs`: add `ActivityCategory`,
  `NewsCategory`, `ProjectCategory` constants.
- `TranslationsController.GetEntityTypes()`: add 3 entries, each with
  `fields = ["Name"]`.
- `GetEntitiesWithTranslationStatus`: add 3 switch cases reading from
  `db.ActivityCategories` / `NewsCategories` / `ProjectCategories`.
  `hasTranslation`/`translationCount` are computed directly from how many
  of `NameEn/NameZh/NameJa` are non-empty (not from the
  `EntityTranslation` table, which holds no rows for these types).
  `expectedFields = 3` — note this is a category-specific special case:
  for every other entity type `expectedFields` counts translatable
  *fields* (all done in one language pass), but for Category it counts
  *languages* for the single `Name` field, since the fixed-column
  layout has no other fields to translate. The `x/3` badge in the
  existing UI will read as "N of 3 languages done," not "N of 3 fields
  done" — acceptable since Category only ever has one field, but worth
  knowing if this code is read later expecting field-counting semantics.
- `GetEntityTranslations`: add 3 switch cases building
  `original = { ["Name"] = cat.NameVi ?? cat.Name }`. For these entity
  types, **skip** the existing call to
  `entitySvc.GetAllTranslationsForEntityAsync` (it would return nothing
  useful) and instead build `translations` directly:
  `{ en: { Name: NameEn }, zh: { Name: NameZh }, ja: { Name: NameJa } }`.
- `SaveEntityTranslations`: add a branch — when `entityType` is one of the
  3 category types, write `req.Translations["Name"]` directly into the
  `NameEn`/`NameZh`/`NameJa` column matching `req.LanguageCode` on the
  right DbSet, instead of calling `entitySvc.SetTranslationsAsync`.
- `DeleteEntityTranslations`: matching branch — "delete" means clearing
  `NameEn`/`NameZh`/`NameJa` to `""` for that category.

## C. Fix every category write path that skips `NameVi`

Four write sites, all populating `Name` but not `NameVi`, violating the
AGENTS.md rule (added in `c651e57`) that every per-language field must be
populated on every write path. All currently "work" only via the read-time
fallback (`NameVi ?? Name` in `MapToResponse`), which the rule explicitly
says not to rely on:

- `ContentSeeder.cs` — `SeedCategories()` and `LinkCategories()`
  (lines ~60, ~90, ~118, ~137). Add `NameVi = seed.Name` (or
  `NameVi = trimmed` for the auto-link paths).
- `ActivityService.cs:245-250` (private `ResolveCategoryAsync`, auto-create
  branch) and `NewsService.cs:240-245` (same pattern) — found in design
  review. Rather than patch `NameVi` into these private methods in place,
  align Activity/News with the pattern Project already uses: add a public
  `ResolveAsync(int? categoryId, string? categoryName)` to
  `ActivityCategoryService`/`NewsCategoryService`, mirroring
  `ProjectCategoryService.ResolveAsync` (`ProjectCategoryService.cs:109-143`,
  which already sets `NameVi = trimmed` on auto-create), inject the
  respective category service into `ActivityService`/`NewsService`, and
  delete the private duplicate methods. This removes duplicated
  auto-create logic in addition to fixing the missing `NameVi`.

**Addendum (found while writing the implementation plan, corrected during
execution):** `LinkCategories()` backfills `ActivityCategoryId`/
`ProjectCategoryId` on existing rows from the legacy `Category` string, but
has no equivalent for `NewsCategoryId`. An initial live-DB query appeared to
confirm this was an active bug (23 News rows, 22 with `NewsCategoryId =
NULL`), which is what motivated folding a News backfill into Part C.
**A closer query run after implementation showed this number was
misleading**: all 22 of those rows have an *empty* `Category` string (a
null FK is the correct state for them — there is nothing to link), matching
the seed manifest, which seeds every News item with `"category": ""`. Precisely
zero News rows have a non-empty `Category` with a null FK — the same as
Activities (0/14) and Projects (0/75). There was no active data-integrity
bug in this dataset after all. The `LinkCategories()` News block (mirroring
the existing Activity/Project pattern) was implemented anyway and is kept:
it's correct, harmless when there's nothing to link, consistent with the
other two content types, and defends against a future bulk import that sets
`Category` without the FK — but it should not have been described as fixing
an observed live bug.

## D. Testing

Per CLAUDE.md, backend changes require tests in `nihomebackend.tests`:

- `TranslationsController` round-trip for one category type (e.g.
  `ActivityCategory`): `POST entity/ActivityCategory/{id}` with
  `languageCode=en` then `GET entity/ActivityCategory/{id}` returns the
  saved value under `translations.en.Name`, without touching
  `NameVi`/`NameZh`/`NameJa`.
- `ActivityCategoryService.ResolveAsync` / `NewsCategoryService.ResolveAsync`
  (new methods): auto-create branch sets `NameVi` equal to the resolved
  name, matching `ProjectCategoryService.ResolveAsync`'s existing test
  coverage if any exists, or new coverage mirroring it.
- `ActivityService`/`NewsService`: existing category-resolution tests (if
  any) continue to pass after switching to the injected category service;
  add coverage confirming `Category`/`CategoryId` on create/update are
  unaffected by this refactor (still a pure DI/dedup change, no behavior
  change intended for the write path itself beyond the `NameVi` fix).
- `ContentSeeder` seeding: newly created categories have `NameVi` equal to
  their `Name`.

No frontend test files are required for the `contentApi.ts` type additions
or the public-page changes (per `nihomeweb/AGENTS.md`, frontend tests are
not required) — verify manually per the frontend-playbook review pass:
switch language on Activities/News/Projects list and detail pages, confirm
category labels localize and the selected filter survives a language
switch; then switch the admin UI to `en` and edit an existing
Activity/News/Project to confirm the category dropdown still round-trips
correctly (the regression this revision specifically prevents).

## Non-goals

- No DB migration — the `NameVi/NameEn/NameZh/NameJa` columns already
  exist (`56df8db`).
- No content-API response-shape changes — `Category`/`CategoryId` remain
  exactly as today on `ActivityResponse`/`NewsResponse`/`ProjectResponse`;
  only the 3 category-list response *types* gain fields the backend
  already sends.
- No changes to `admin/categories` (`Categories.tsx`) — already complete.
- No changes to `Translations.tsx` — already generic over the backend's
  entity-type list.
- No changes to `ActivityService.CreateAsync`/`UpdateAsync` beyond the DI
  swap in Part C — admin write behavior for category resolution is
  unchanged except for the `NameVi` fix itself.
- Nav/menu category references: not found to exist as hardcoded category
  lists elsewhere in the app (confirmed via research); out of scope unless
  discovered otherwise during implementation.
