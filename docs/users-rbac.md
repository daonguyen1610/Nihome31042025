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
