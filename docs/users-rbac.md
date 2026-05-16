# Users Section RBAC

Date: 2026-05-16

## Overview

The admin Users section is backed by the ASP.NET Core `/api/users` endpoints and the existing `UserRole` enum:

- `SUPER_ADMIN`
- `ADMIN`
- `USER`

Roles are fixed system roles in this phase. There is no dynamic role table or permission editor.

## Access Rules

- `SUPER_ADMIN` can list, create, update, deactivate, and soft-delete users.
- `SUPER_ADMIN` can view the role catalog and permission matrix.
- `ADMIN` can access the admin area but cannot access `/admin/users` or `/admin/roles`.
- `USER` cannot access the admin area.

The backend prevents a super admin from changing their own role, deactivating their own account, or removing the last active `SUPER_ADMIN`.

## API Surface

- `GET /api/users`
- `GET /api/users/{id}`
- `POST /api/users`
- `PUT /api/users/{id}`
- `PATCH /api/users/{id}/toggle-active`
- `DELETE /api/users/{id}`
- `GET /api/users/roles`

`DELETE /api/users/{id}` is a soft delete. It sets `IsActive = false` and keeps the row for audit/history continuity.

## Frontend Surface

- `/admin/users` lists users with search, role filter, pagination, create/edit modal, status toggle, and soft delete.
- `/admin/roles` displays the backend role catalog and informational permission matrix.
- Admin route protection lives in `nihomeweb/src/components/auth/ProtectedRoute.tsx`.
