# Admin Services Management — Design Spec

Date: 2026-06-16
Branch: seed/service

## Goal

Build an admin CRUD page for `ServiceItem` entities so content editors can manage the data
shown on `/services` and `/services/:slug` without touching the database directly.

## Context

Backend is fully implemented:
- `ServicesController` — GET list, GET by slug, POST, PUT, DELETE at `/api/services`
- `ServiceItemService` — full CRUD with `AsNoTracking()` reads
- `UpsertServiceRequest` — Slug, Title, ShortTitle, Tagline, Intro, Sections (JsonElement),
  Highlights (string[]), SortOrder
- `ServiceResponse` — same shape + Id
- Integration and unit tests already exist

Frontend public pages (`Services.tsx`, `ServiceDetail.tsx`) are done and wired to `contentApi`.

Nothing admin-side exists yet: no `adminApi` methods, no admin page, no route, no nav item.

## Data Model

```
ServiceItem:
  id          int
  slug        string  (URL key, unique)
  title       string
  shortTitle  string  (eyebrow label on cards)
  tagline     string  (subtitle shown on list card and detail hero)
  intro       string  (paragraph shown in detail intro section)
  sections    [{ heading: string, body: string[] }]  (stored as JSON)
  highlights  string[]  (stored as JSON, shown as chips)
  sortOrder   int
```

## Approach Decision

**Single-page Dialog modal** — same pattern as `ProcessList.tsx`:
- No image upload so Dialog scope is fine
- Reduces routing complexity
- Keeps list and form in one file

Sections field uses **dynamic UI** (not raw JSON textarea) since editors are non-technical:
- Add / remove section rows
- Each section: heading text field + dynamic bullet list (body items add/remove)

## Files to Create / Modify

### New
- `nihomeweb/src/pages/admin/Services.tsx`
  — list + create/edit dialog in one file, following ProcessList pattern

### Modified
- `nihomeweb/src/services/adminApi.ts`
  — add `UpsertServiceAdminRequest` type and three methods:
    `createService`, `updateService`, `deleteService`
- `nihomeweb/src/App.tsx`
  — add `<Route path="/admin/services" element={<AdminServices />} />`
- `nihomeweb/src/components/layout/AdminLayout.tsx`
  — add `{ to: "/admin/services", label: t("nav.services"), icon: ConciergeBell }` to "content" group
- `nihomebackend/Data/Seeds/services.json`
  — add ~12 i18n keys for the admin UI (all four languages: vi/en/zh/ja)

## Component Breakdown

### `AdminServices` (page root)
- Fetches list via `contentApi.getServices()`
- Owns `openModal`, `form` state
- Renders: page header + Add button, search bar, table, Dialog

### Service table
Columns: Sort | Short Title | Title | Tagline (truncated) | Actions (edit/delete)

### Dialog form sections
1. **Basic info** — Slug (auto from title, editable), Title*, ShortTitle*, Tagline*, SortOrder
2. **Intro** — single `<textarea>`
3. **Highlights** — dynamic string list: text input row + Add button + X to remove each chip
4. **Sections** — dynamic section list:
   - Each section: heading field + body bullet list (each bullet: text + X)
   - "Add bullet" per section, "Add section" at bottom, drag-free ordering via position

### i18n keys to add (prefix `svc.admin`)

```
svc.admin.title        — Quản lý dịch vụ / Services / ...
svc.admin.add          — Thêm dịch vụ / Add service / ...
svc.admin.editTitle    — Sửa dịch vụ / Edit service / ...
svc.admin.createTitle  — Tạo dịch vụ / Create service / ...
svc.admin.searchPh     — Tìm kiếm... / Search... / ...
svc.admin.empty        — Chưa có dịch vụ / No services yet / ...
svc.admin.confirmDel   — Xác nhận xóa dịch vụ? / Confirm delete? / ...
svc.admin.slug         — Slug
svc.admin.shortTitle   — Short title / Tên ngắn / ...
svc.admin.tagline      — Tagline
svc.admin.intro        — Intro
svc.admin.highlights   — Highlights / Điểm nổi bật / ...
svc.admin.sections     — Sections / Mục nội dung / ...
svc.admin.addSection   — + Add section / + Thêm mục / ...
svc.admin.addBullet    — + Add bullet / + Thêm dòng / ...
svc.admin.heading      — Heading / Tiêu đề mục / ...
nav.services           — Dịch vụ / Services / 服务 / サービス
```

Also add `nav.services` to `admin-system.json`.

## Error Handling

- Use the same `getErrorMessage(err)` helper pattern from `ProcessList.tsx`
- Toast on success (created / updated / deleted) and on error
- Disable submit while `submitting === true`
- Required field validation client-side: title, shortTitle, slug, tagline, intro

## Quality Gates

- `npm run lint` passes
- `npm run build` passes
- All new UI strings added to seed files with vi/en/zh/ja
- Backend API receives correct `sections` shape (`JsonElement`): array of `{heading, body}`

## Out of Scope

- Drag-and-drop reordering (use sortOrder field instead)
- Image/media upload on service items
- Preview mode of public page from admin
