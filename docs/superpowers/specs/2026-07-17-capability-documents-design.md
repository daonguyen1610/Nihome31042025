# Capability Documents (NIH-98) — design & delivery notes

Date: 2026-07-17
Status: Implemented in `feat/nih-98-capability-documents`

## Context

`NIH-98` under Story `NIH-85 Đấu thầu` (Tender) asks for a shared
Capability-Document library so that Sales does not have to re-upload the
same portfolio / ISO / permit PDFs for every bid. The library also feeds
the "Chọn từ library" modal on the Tender detail page (`NIH-97`), which
is not yet built — this slice lands the repository so `NIH-97` can plug
into it later without reworking the storage layer.

## Backend

### Entities

* `CapabilityDocument` — canonical row per document. Fields: `Name`,
  `TagCode` (FK-like reference to `MasterDataOption.Code` in category
  `capability_document_tag`), `IssuedDate`, `ExpiryDate`, `Description`,
  `FilePath` (host-relative under `/files/capability/`), `OriginalFileName`,
  `FileSize`, `ContentType`, `CurrentVersion`, `UploadedByUserId`,
  audit timestamps.
* `CapabilityDocumentVersion` — immutable snapshot of every previous file
  when the current file is replaced. Cascade-delete when the parent goes.

### Storage

Physical files land under `wwwroot/files/capability/{guid}.{ext}`.
`Program.cs` mounts that folder as a static-file endpoint at
`/files/capability` so authorised admins can download originals via a
direct `<a>` tag. The path prefix `/files/capability/` is a hard invariant
enforced by `CapabilityDocumentService.NormalizeManagedPath` — any request
whose file path does not resolve inside that prefix is rejected. This
guards against path-traversal payloads and prevents rows referencing
foreign upload folders (see `image_handling_exploration` memory for the
same pattern applied to images).

### API surface

All routes live under `/api/capability-documents` and `/api/v1/…`.

| Verb + path | Permission | Purpose |
|-------------|------------|---------|
| `GET /`                              | `crm.capability-docs.view`    | Paginated list with `tagCode` / `issuedYear` / `search` / `expiryState` filters. |
| `GET /{id}`                          | `crm.capability-docs.view`    | Detail with version history. |
| `POST /upload` *(multipart)*         | `crm.capability-docs.manage`  | Stores a single file, returns `{filePath, originalFileName, fileSize, contentType}` for a follow-up `POST /`. |
| `POST /`                             | `crm.capability-docs.manage`  | Create metadata row bound to an already-uploaded file. |
| `PUT /{id}`                          | `crm.capability-docs.manage`  | Update metadata (file preserved). |
| `POST /{id}/replace-file`            | `crm.capability-docs.manage`  | Bump `CurrentVersion`, snapshot the old file, swap in the new one. |
| `DELETE /{id}`                       | `crm.capability-docs.manage`  | Two-step confirm on the FE; server removes rows and physical files (current + all versions). |
| `POST /download-zip`                 | `crm.capability-docs.view`    | Bulk ZIP export preserving Vietnamese filenames (duplicates suffixed with `-{id}`). |

### Expiry state buckets

`CapabilityDocumentService.ComputeExpiryState` maps `ExpiryDate` to one
of `none` · `expired` · `critical` (≤30d) · `warning` (≤60d) · `ok`. The
FE uses these to render the badge and to drive the `expiryState` filter.

### RBAC

Two new permissions already existed in `rbac-defaults.json`:

* `crm.capability-docs.view` — granted to `SALE`, `SALES_MANAGER`, `BGD`.
* `crm.capability-docs.manage` — granted to `SALES_MANAGER`, `BGD`, `ADMIN`.

## Frontend

* Types + service methods in `nihomeweb/src/services/adminApi.ts`.
* Admin page `nihomeweb/src/pages/admin/CapabilityDocuments.tsx` under
  `/admin/capability-documents`, gated on `ADMIN_PERMS.capabilityDocs`.
* Nav entry sits inside the CRM group in `AdminLayout` right below Quotes.
* Multi-file drag-and-drop that requires a Tag to be selected first
  (single default tag applies to every dropped file, matching the AC
  which specifies a batch upload with grid entries per file).
* Filter row: search / tag / expiry-state.
* Row actions: download original, edit metadata, replace file (bumps
  version), delete (two-step alert dialog).
* Bulk ZIP download from checkbox selection.
* Edit dialog surfaces the version history inline so admins can pull
  older PDFs when needed.

## Deferred / follow-ups

* **NIH-97 “Chọn từ library” integration.** When the Tender detail lands,
  its `Chọn từ library` modal should call `GET /api/capability-documents`
  and reference selected `Id`s inside `TenderChecklistItem`.
* **In-use guard on delete.** AC says delete should be blocked if the
  document is referenced by any open (preparing) tender. Because Tender
  does not exist yet, the current implementation only enforces structural
  guards; the in-use check will be added alongside `NIH-96` when the
  Tender module lands.
* **CRUD for `capability_document_tag` from the master-data admin.** The
  category is already visible in `/admin/master-data` because
  `MasterDataService` is generic — no per-category page needed.

## Test coverage

* Unit — `nihomebackend.tests/Services/CapabilityDocumentServiceTests.cs`
  (17 cases): tag validation, path normalisation / traversal, expiry
  buckets, version snapshotting, delete cleanup.
* Integration — `nihomebackend.integration.tests/Controllers/CapabilityDocumentsControllerTests.cs`
  (10 cases): RBAC 401/403, two-step upload flow, replace-file version
  bump, tag / search filters, ZIP export.
* E2E smoke — `nihomeweb/e2e/smoke/admin-capability-documents.spec.ts`
  (1 case): the SPA renders the page for `SALES_MANAGER` without JS
  errors. RBAC route matrix is extended in
  `admin-rbac-matrix.spec.ts` so `SALE` / `SALES_MANAGER` are allowed and
  every other role is denied.
