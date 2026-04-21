# Current State

Last reviewed: 2026-04-21

## Stack

- Next.js `16.2.3`
- React `19.2.4`
- React DOM `19.2.4`
- TypeScript `^5`
- Tailwind CSS `^4`
- ESLint `^9`

## Current App Structure

The committed app shell now includes:

- `app/layout.tsx`
- `app/not-found.tsx`
- `app/(auth)/layout.tsx`
- `app/(auth)/login/page.tsx`
- `app/(auth)/forgot-password/page.tsx`
- `app/(client)/layout.tsx`
- `app/(client)/page.tsx`
- `app/(client)/projects/page.tsx`
- `app/(client)/notifications/page.tsx`
- `app/(admin)/admin/layout.tsx`
- `app/(admin)/admin/page.tsx`
- `app/(admin)/admin/dashboard/page.tsx`
- `app/globals.css`
- `app/favicon.ico`

There is also a small shared component and utility layer under `components/`, `lib/utils/`, and `types/`.

## Current Portal Shell Behavior

- `/` is served by `app/(client)/page.tsx`.
- The client home keeps the warm branded landing direction and acts as the Phase 1 client portal shell.
- `/projects` and `/notifications` are placeholder pages inside the client layout.
- `/admin` redirects to `/admin/dashboard`.
- `/login` and `/forgot-password` are placeholder auth pages with no real auth flow yet.

## Current Config Reality

- `next.config.ts` no longer sets `output: "export"`.
- There is no rewrite, proxy, route protection, or route handler committed yet.
- No `.env.example` is committed in this repo.
- The repo now includes repo-local AI docs, a project brief, and a memory bank under `docs/ai/`.

## Current Gaps

- No real auth layer is implemented yet.
- No API client or server-state strategy is established yet.
- No deep business modules such as CRM, design, construction, procurement, or finance are implemented yet.
- No frontend test suite is committed yet.
- No route protection is implemented yet.

## Agent Notes

- Do not assume older Next.js examples apply directly; this repo explicitly targets Next.js 16.
- In fresh environments, `node_modules/` may not be present yet, so local Next.js docs may require dependency installation before they can be consulted.
