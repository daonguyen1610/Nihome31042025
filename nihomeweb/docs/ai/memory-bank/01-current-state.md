# Current State

Last reviewed: 2026-08-12

## Stack

- Vite `5.x`
- React `18.3.x`
- React DOM `18.3.x`
- TypeScript `5.8.x`
- React Router DOM `6.x`
- Tailwind CSS `3.4.x`
- shadcn/ui and Radix UI primitives
- TanStack React Query `5.x`
- Playwright `1.x`

## Current App Structure

The active frontend includes:

- `src/main.tsx`
- `src/App.tsx`
- `src/index.css`
- `src/App.css`
- `src/components/layout/Layout.tsx`
- `src/components/layout/AdminLayout.tsx`
- `src/components/ui/`
- `src/components/admin/SettingsControls.tsx`
- `src/pages/Index.tsx`
- `src/pages/Profile.tsx`
- `src/pages/Services.tsx`
- `src/pages/Projects.tsx`
- `src/pages/News.tsx`
- `src/pages/Activities.tsx`
- `src/pages/Clients.tsx`
- `src/pages/Recruitment.tsx`
- `src/pages/Contact.tsx`
- `src/pages/Login.tsx`
- `src/pages/Register.tsx`
- `src/pages/admin/**/*.tsx`
- `src/data/`
- `src/services/`
- `src/lib/adminPermissions.ts`
- `src/lib/url.ts`
- `src/lib/i18n.tsx`

`nihomeweb/` ships the Vite + React source tree as the active frontend. The old Materialize starter-kit and Next.js shell are no longer present and are not part of the active architecture.

## Current Route Surface

Public route groups:

- `/`
- `/profile`
- `/services` and `/services/:slug`
- `/projects` and `/projects/:id`
- `/news` and `/news/:id`
- `/activities` and `/activities/:id`
- `/clients`
- `/recruitment`
- `/contact`
- `/login`
- `/register`
- `/forgot-password`
- `/my-profile`
- `/forbidden`

Admin route groups:

- `/admin`
- `/admin/users` and `/admin/roles` for permission-gated user and dynamic-role management
- content routes for activities, news, projects, services, categories, about content, logos, contacts, and recruitment
- CRM routes for leads, customers, opportunities, quotes, capability documents, tenders, surveys, and contracts
- design routes for design projects and their concepts, basic design, shop drawings, revisions, and IFC data
- permitting and construction routes for permits, tasks/Gantt, site diaries, punch lists, partial acceptance, as-built records, and handover
- procurement vendor routes at `/admin/vendors` and `/admin/vendors/:id`
- settings, languages, translations, master data, workflows, notifications, email templates, and audit/activity log routes
- `/admin/processes/*`

`/admin/posts/*`, `/admin/project-categories`, and `/admin/slideshow` are compatibility redirects to active routes rather than separate modules.

The catch-all route renders `src/pages/NotFound.tsx`.

## Current Portal Shell Behavior

- `src/App.tsx` wraps the app in the Redux `Provider`, `QueryClientProvider`, `I18nProvider`, `TooltipProvider`, toast providers, and `BrowserRouter`.
- Public pages use the public header/footer layout where implemented.
- Admin pages use `AdminLayout` for sidebar navigation, admin topbar behavior, and language controls.
- `/login` and `/register` use the backend auth API through Redux auth state.
- Admin routes are protected with `ProtectedRoute`; backend permissions govern route visibility and actions, including `/admin/users` and `/admin/roles`.

## Current Config Reality

- `vite.config.ts` defines the Vite build, dev server, and path alias behavior.
- The dev server defaults to port `8080`.
- `components.json` configures shadcn/ui with aliases under `@/`.
- `tailwind.config.ts` and `src/index.css` own the active design tokens and utility classes.
- `playwright.config.ts` and `e2e/smoke/` define real-browser E2E coverage; separate frontend unit tests are not part of the active test strategy.
- Split frontend development uses `VITE_API_URL`; integrated deployment serves the built SPA and API from ASP.NET Core. `NIHOMEWEB_DIST_PATH` can override the SPA distribution path.
- The repo includes repo-local AI docs, a project brief, and a memory bank under `docs/ai/`.

## Current Gaps

- Backend JWT/refresh authentication, Redux auth state, and permission loading are implemented.
- API-backed modules cover public content, CRM, contracts, design, permitting, construction, recruitment, settings, and system administration.
- `/admin/master-data` still persists its editable language list through `src/lib/masterDataStore.ts` and localStorage.
- Survey records and timeline are implemented, but survey media management is pending. Procurement vendor management is API-backed; BOQ, material requests, warehouse operations, cash flow, profit-and-loss, Google Drive integration, and broad cross-module analytics are pending.

## Agent Notes

- Do not assume Next.js Pages Router, `_app.tsx`, `_document.tsx`, `next.config.*`, MUI, Emotion, or Materialize are active in this repo.
- `src/pages/` means React page components routed from `src/App.tsx`, not filesystem routing.
- Reuse typed functions under `src/services/` for backend access; treat `src/lib/masterDataStore.ts` as known migration debt, not the platform default.
