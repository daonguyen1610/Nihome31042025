# Frontend Architecture

Last reviewed: 2026-08-21

## Core Direction

- Use the Vite + React SPA architecture currently present in `nihomeweb/`.
- Use React Router DOM for routing.
- Keep route declarations centralized in `src/App.tsx`.
- Keep `src/main.tsx` focused on app bootstrap.
- Keep Tailwind CSS, shadcn/ui, Radix UI, and lucide-react as the active UI foundation.
- Build by phase instead of scaffolding every future module at once.

## Route and Component Boundaries

- `src/pages/` contains React page components; it is not filesystem routing.
- Public pages should use `src/components/layout/Layout.tsx` unless a documented exception applies.
- Admin pages should use `src/components/layout/AdminLayout.tsx`.
- Shared shadcn/Radix primitives belong in `src/components/ui/`.
- Shared app-specific components belong under `src/components/` by role.
- Admin-specific reusable controls belong under `src/components/admin/`.
- Avoid placing backend-specific assumptions directly inside presentational components.
- If multiple routes need the same backend access pattern, centralize that pattern instead of copying fetch logic.

## Data Fetching Defaults

- Authentication uses the backend JWT/refresh contract with Redux state and `ProtectedRoute`.
- API-backed public and admin functions are centralized under `src/services/`; extend the existing service boundary rather than creating route-local clients.
- `src/lib/masterDataStore.ts` is the known route-specific localStorage persistence exception for `/admin/master-data`; it must not be treated as the default data model. Other localStorage usage is UI preference state.
- TanStack Query is already installed and wrapped at the app level; evaluate it before adding another server-state library.
- Centralize request handling and document new API/environment contracts in the same task.
- Avoid route-local `useEffect` fetch blocks becoming the default integration style.
- Auth state is centralized in Redux. Route protection uses `src/components/auth/ProtectedRoute.tsx`, which refreshes persisted cookie tokens before deciding redirects.
- API-backed operational service functions belong in the existing typed modules under `src/services/`. User CRUD uses `adminApi.ts`; role and permission management uses `rbacApi.ts`.

## Environment and Integration Rules

- Treat environment usage as an explicit contract, not an implicit assumption.
- Vite client-exposed variables must use the `VITE_` prefix.
- Do not document env variables that are not actually committed or configured.
- Do not hardcode backend base URLs in presentational UI.
- Store backend-served media as host-relative paths such as `/images/...`; frontend helpers may resolve relative paths against the current API origin, but must not special-case fixed development hosts.
- If the frontend depends on backend proxying or Vite dev-server configuration, commit that configuration and document it in the same task.

## Private Document Integration

- Treat managed paths under `/files/quotes`, `/files/customers`, `/files/contracts`, `/files/capability`, `/files/business-documents`, and `/files/design` as metadata, not public browser URLs.
- Fetch private bytes through the typed resource-bound functions in `src/services/adminApi.ts` and pass the authenticated Blob loader to `AdminFilePreview`.
- Keep external HTTP(S) document URLs on the existing safe-link path; do not send them through managed-content APIs.
- A two-step upload is not previewable through the server until the owning resource metadata is persisted. Create/edit forms must suppress the managed preview while a staged path differs from the persisted record, rather than showing stale content or a predictable `404`.
- Keep permission-aware mutation controls, error/retry states, keyboard operation, horizontal mobile reachability, and at least 44px touch targets for changed document journeys.

## Runtime Direction

The app should be treated as a client-rendered Vite SPA on top of the current Lovable/shadcn source tree.
The current baseline intentionally favors immediate productivity over framework migration.

For future phases:

- do not document auth or API behavior that is not committed
- extend the existing Redux authentication and centralized API service interfaces unless a replacement is explicitly decided
- do not reintroduce the old Next.js or Materialize starter-kit baseline
- do not import a full admin template as a second active architecture

## Documentation Rule

If architecture changes in a durable way, update this file and `05-decisions-and-open-questions.md` in the same task.
