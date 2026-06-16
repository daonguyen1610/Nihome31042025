# Users Section RBAC

Date: 2026-05-16 (updated 2026-06-16 for NIH-366 — permission-based authorization)

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

## Access Rules

- `SUPER_ADMIN` can list, create, update, deactivate, and soft-delete users; manage role × permission matrix; manage audit retention.
- `ADMIN` has every permission in the catalog **except** `users.manage` and `system.audit.manage`. Concretely:
  - Can list/view users (`users.view`) but cannot create, update, toggle-active, or delete them.
  - Can view audit logs (`system.audit.view`) but cannot mutate retention config.
  - Can manage all content, processes, mail, recruitment, translations, and site settings.
- `USER` has only `profile.me.view` / `profile.me.update` — they can read/update their own profile and nothing else.

The backend additionally prevents a super admin from changing their own role, deactivating their own account, or removing the last active `SUPER_ADMIN`.

## API Surface

- `GET /api/users` — `users.view`
- `GET /api/users/{id}` — `users.view`
- `POST /api/users` — `users.manage`
- `PUT /api/users/{id}` — `users.manage`
- `PATCH /api/users/{id}/toggle-active` — `users.manage`
- `DELETE /api/users/{id}` — `users.manage` (soft delete; sets `IsActive = false`)
- `GET /api/users/roles` — `users.view`

## Frontend Surface

- `/admin/users` lists users with search, role filter, pagination, create/edit modal, status toggle, and soft delete. **UX gate**: only `SUPER_ADMIN` sees the link and can reach the route (`App.tsx` wraps it in `<ProtectedRoute roles={["SUPER_ADMIN"]}>`). The backend still serves `users.view` to `ADMIN` (so any direct API call is allowed) but blocks mutations with `users.manage`.
- `/admin/roles` displays the backend role catalog and informational permission matrix; same SUPER_ADMIN-only UX gate.
- Admin route protection lives in `nihomeweb/src/components/auth/ProtectedRoute.tsx`. Role-name gating is a UX shortcut; the API authority is the permission set returned by `/api/users/me/permissions`.

## Seeded test users (dev + integration tests)

Every role has a deterministic test account so manual smoke tests, integration tests, and Playwright suites can log in as any role without bespoke setup. All accounts use the same password.

**Password (all users): `Admin@123`**

| Role | Phone | Notes |
|---|---|---|
| `SUPER_ADMIN` | `0335240370` | Default super admin (DbSeeder). |
| `ADMIN` | `0911111111` | Ops admin (DbSeeder). |
| `ADMIN` | `0922222222` | Leasing admin (DbSeeder). |
| `USER` | `0900000001` | Customer placeholder (integration TestDataSeeder only — dev DB lets you register via OTP). |
| `SALE` | `0911000003` | dashboard + contacts.* + recruitment.applications.view |
| `DESIGN` | `0911000004` | dashboard + content.** + processes.view |
| `PM` | `0911000005` | dashboard + content.projects.* + processes.* + recruitment.applications.view |
| `QS` | `0911000006` | dashboard + content.projects.view + processes.view |
| `ACCOUNTANT` | `0911000007` | dashboard + contacts.view + system.audit.view |
| `WAREHOUSE` | `0911000008` | dashboard + processes.view |
| `BGD` | `0911000009` | dashboard + **.view + system.audit.view |

System roles are stored using the legacy `UserRole` enum; business-role users carry `Role = USER` and the real role link via `RoleEntityId`. `PermissionService` reads `RoleEntityId` first, so the business-role permission matrix from `rbac-defaults.json` applies as-is.

### Integration test helper

`AuthTestHelper.LoginAsRoleAsync(client, "SALE")` works for any seeded role code (system or business) and returns a fresh JWT. Prefer it over the per-role helpers when writing parameterised theories.

### Stale business-role permissions on long-lived dev DBs

Business roles are seeded **once** (tracked by `Role.InitialPermissionsSeeded`) so operator edits in the future matrix editor survive restarts. The trade-off: a dev DB created before the catalog grew may show business-role users with fewer permissions than the JSON pattern would currently expand to.

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

`nihomeweb/e2e/smoke/admin-rbac-matrix.spec.ts` drives all 9 seeded accounts through every admin route surface and asserts each role can reach exactly its permitted set and gets the inline `<Forbidden />` screen on the rest. Allow/deny sets are kept in sync with `/api/users/me/permissions` returned by the live stack so a drift between `rbac-defaults.json`, the seeder, and the FE `ADMIN_PERMS` map fails the suite. Run with `BASE_URL=http://localhost:5043 npx playwright test admin-rbac-matrix`.


