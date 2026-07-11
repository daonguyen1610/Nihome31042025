# Frontend Architecture

Last reviewed: 2026-05-16

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

- The current baseline uses static seed data and localStorage-backed demo stores.
- `src/lib/auth.ts` is mock UI auth, not production authentication.
- `src/lib/adminStore.ts` and `src/lib/settingsStore.ts` are demo persistence layers, not backend integration.
- TanStack Query is already installed and wrapped at the app level; evaluate it before adding another server-state library.
- If real API calls are introduced, centralize request handling and document the API/environment contract in the same task.
- Avoid route-local `useEffect` fetch blocks becoming the default integration style.
- Auth state is centralized in Redux. Route protection uses `src/components/auth/ProtectedRoute.tsx`, which refreshes persisted cookie tokens before deciding redirects.
- API-backed admin service functions belong in `src/services/adminApi.ts`; Users/RBAC follows that existing service boundary.

## Environment and Integration Rules

- Treat environment usage as an explicit contract, not an implicit assumption.
- Vite client-exposed variables must use the `VITE_` prefix.
- Do not document env variables that are not actually committed or configured.
- Do not hardcode backend base URLs in presentational UI.
- Store backend-served media as host-relative paths such as `/images/...`; frontend helpers may resolve relative paths against the current API origin, but must not special-case fixed development hosts.
- If the frontend depends on backend proxying or Vite dev-server configuration, commit that configuration and document it in the same task.

## Runtime Direction

The app should be treated as a client-rendered Vite SPA on top of the current Lovable/shadcn source tree.
The current baseline intentionally favors immediate productivity over framework migration.

Until later phases are opened:

- do not document auth or API behavior that is not committed
- do not introduce real auth or API client layers before the team decides those interfaces
- do not reintroduce the old Next.js or Materialize starter-kit baseline
- do not import a full admin template as a second active architecture

## Documentation Rule

If architecture changes in a durable way, update this file and `05-decisions-and-open-questions.md` in the same task.
