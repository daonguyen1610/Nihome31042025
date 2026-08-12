# Users Section RBAC

Date: 2026-05-16 (updated 2026-08-10 for current platform RBAC and project handover authorization)

## Overview

The admin Users section is backed by the ASP.NET Core `/api/users` endpoints. Authorization is now permission-based, enforced globally by `PermissionAuthorizationFilter` (registered in `Program.cs`) which reads `[RequirePermission(module, action)]` attributes off the class and the action. All requirements must match (AND semantics); `[AllowAnonymous]` short-circuits the check.

Roles are still seeded from the JSON bundle (`nihomebackend/Data/Rbac/rbac-defaults.json`), but controllers no longer name roles directly. Instead they declare the permission codes they need; what each role can do is derived from the role × permission matrix in the DB.

### System roles

| Role | Source of truth | Customisable? |
|---|---|---|
| `SUPER_ADMIN` | Force-synced to the full catalog on every boot. Lockout safety net. | No |
| `ADMIN` | Force-synced on every boot via the bundle pattern (`**` minus `users.manage`, `system.audit.manage`). | No |
| `USER` | Force-synced on every boot via the bundle pattern (`profile.me.*`). | No |
| Business roles (`SALE`, `DESIGN`, …) | Seeded once via `Role.InitialPermissionsSeeded`; subsequent edits in the admin matrix editor are preserved on restart. | Yes |

The current business-role catalog contains `SALE`, `SALES_MANAGER`, `DESIGN`, `DESIGN_LEAD`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER`, `PM`, `LEGAL_OFFICER`, `QS`, `ACCOUNTANT`, `WAREHOUSE`, and `BGD`. Authorized administrators may also create or delete non-system roles and edit their permission matrices.

## Access Rules

- `SUPER_ADMIN` can list, create, update, deactivate, and soft-delete users; manage role × permission matrix; manage audit retention.
- `ADMIN` has every permission in the catalog **except** `users.manage` and `system.audit.manage`. Concretely:
  - Can list/view users (`users.view`) but cannot create, update, toggle-active, or delete them.
  - Can view audit logs (`system.audit.view`) but cannot mutate retention config.
  - Can manage all content, processes, mail, recruitment, translations, and site settings.
- `USER` has only `profile.me.view` / `profile.me.update` — they can read/update their own profile and nothing else.

The backend additionally prevents a super admin from changing their own role, deactivating their own account, or removing the last active `SUPER_ADMIN`.

### Project handover permissions

The handover workspace uses separate read, write, and completion capabilities. The API remains the authority; frontend route and action gates only improve the user experience.

| Permission | Capability |
|---|---|
| `construction.handover.view` | Open the handover list/detail and export records within the caller's project scope. |
| `construction.handover.view.all` | Read and export records across every project. It does not grant unrestricted mutations. |
| `construction.handover.manage` | Create, update, delete, and perform non-final lifecycle transitions within the caller's project scope. |
| `construction.handover.manage.all` | Perform handover mutations across every project. |
| `construction.handover.complete` | Complete a ready handover; project scope still applies unless `manage.all` is also granted. |

Scoped access includes records created by or assigned to the caller and projects where the caller is project manager or design lead. Reassigning a responsible user requires project leadership or `manage.all`. Wildcard PM, design-lead, admin, and super-admin patterns inherit the broad permissions defined in `rbac-defaults.json`; technical business roles with explicit `view`/`manage` entries remain project-scoped.

## API Surface

- `GET /api/users` — `users.view`
- `GET /api/users/{id}` — `users.view`
- `POST /api/users` — `users.manage`
- `PUT /api/users/{id}` — `users.manage`
- `PATCH /api/users/{id}/toggle-active` — `users.manage`
- `DELETE /api/users/{id}` — `users.manage` (soft delete; sets `IsActive = false`)
- `DELETE /api/users/{id}/hard` — `users.manage` (hard delete; permanently removes user and related data)
- `GET /api/users/roles` — `users.view`

### Project handover API

- `GET /api/handover-records` and `GET /api/handover-records/{id}` — `construction.handover.view`
- `GET /api/handover-records/export` — `construction.handover.view`
- `POST /api/handover-records` — `construction.handover.manage`
- `PUT /api/handover-records/{id}` — `construction.handover.manage`
- `POST /api/handover-records/{id}/status` — `construction.handover.manage`
- `POST /api/handover-records/{id}/complete` — `construction.handover.complete`
- `DELETE /api/handover-records/{id}` — `construction.handover.manage`

The same endpoints are also exposed below `/api/v1/handover-records`. Unauthorized callers receive `401` or `403`; out-of-scope or missing records return `404`; business validation returns `400`; duplicate or concurrent writes return `409`.

## Frontend Surface

- `/admin/users` lists users with search, role filter, pagination, create/edit modal, status toggle, and soft delete. The route requires `users.view`; mutation controls additionally require `users.manage`.
- `/admin/roles` displays the backend role catalog and supports creating/deleting non-system roles plus editing their permission matrices. The route requires `rbac.roles.view`; each operation is gated by its corresponding RBAC permission.
- Admin route protection lives in `nihomeweb/src/components/auth/ProtectedRoute.tsx`. Frontend permission gates improve navigation and action UX; the API remains authoritative using the permission set returned by `/api/users/me/permissions`.

## Seeded test users (dev + integration tests)

Development and integration seeders provide deterministic accounts for the system and business roles used by manual smoke, integration, and Playwright tests. Account identifiers and development credentials are defined in `DbSeeder`, `BusinessRoleUserSeeder`, integration `TestDataSeeder`, and `nihomeweb/e2e/fixtures/auth.ts`; keep those sources aligned rather than duplicating secrets here.

The development seed now provides a deterministic login for every declared business role: `SALE`, `SALES_MANAGER`, `DESIGN`, `DESIGN_LEAD`, `ARCHITECT`, `MEP_ENGINEER`, `STRUCT_ENGINEER`, `PM`, `LEGAL_OFFICER`, `QS`, `ACCOUNTANT`, `WAREHOUSE`, and `BGD`. Integration tests additionally provide a deterministic `USER` account.

System roles are stored using the legacy `UserRole` enum; business-role users carry `Role = USER` and the real role link via `RoleEntityId`. `PermissionService` reads `RoleEntityId` first, so the business-role permission matrix from `rbac-defaults.json` applies as-is.

### Integration test helper

`AuthTestHelper.LoginAsRoleAsync(client, "SALE")` works for any seeded role code (system or business) and returns a fresh JWT. Prefer it over the per-role helpers when writing parameterised theories.

### Stale business-role permissions on long-lived dev DBs

Business roles are seeded **once** (tracked by `Role.InitialPermissionsSeeded`) so operator edits in the current matrix editor survive restarts. The trade-off: a dev DB created before the catalog grew may show business-role users with fewer permissions than the JSON pattern would currently expand to.

To realign a local dev DB with the current `rbac-defaults.json` patterns, run:

```sql
DELETE FROM role_permissions
WHERE role_id IN (SELECT id FROM roles WHERE is_system = 0);
UPDATE roles SET initial_permissions_seeded = 0 WHERE is_system = 0;
```

Then restart the backend; `RbacSeeder.SeedInitialBusinessRolePermissionsIfMissing` will rebuild the rows from the current patterns. Integration and E2E suites are unaffected because they start from a fresh DB on every run.

### Lockdown regression safety net

`UnauthorizedMutationProbeTests` (in `nihomebackend.integration.tests/Controllers/`) reflects over every controller via `ProtectedEndpointInventory.Discover()` and emits two theory rows per `[RequirePermission]`-guarded endpoint:

- anonymous caller → `401 Unauthorized`
- `USER`-role caller (only has `profile.me.*`) → `403 Forbidden`

There is no manual route list to maintain — adding a new `[RequirePermission(...)]` action automatically opts that route into both checks. The scanner currently finds ~79 protected endpoints (`POST/PUT/DELETE` + guarded `GET`s). A sanity `Fact` fails if discovery ever returns fewer than 20 routes (catches reflection breakage in refactors).

For per-controller happy-path coverage (admin/SA returns 2xx with a valid payload), use the existing per-controller test files; the dynamic probe intentionally only asserts the deny path so it stays maintenance-free.

### Browser-level RBAC matrix

`nihomeweb/e2e/smoke/admin-rbac-matrix.spec.ts` drives the seeded role accounts through the admin route surface and asserts that each role can reach its permitted set and gets the inline `<Forbidden />` screen on the rest. Allow/deny sets are kept in sync with `/api/users/me/permissions` returned by the live stack so drift between `rbac-defaults.json`, the seeder, and the frontend permission map fails the suite. Run with `BASE_URL=http://localhost:5043 npx playwright test admin-rbac-matrix`.


