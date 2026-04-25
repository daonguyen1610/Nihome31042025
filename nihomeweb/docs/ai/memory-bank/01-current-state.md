# Current State

Last reviewed: 2026-04-25

## Stack

- Vite `5.x`
- React `18.3.x`
- React DOM `18.3.x`
- TypeScript `5.8.x`
- React Router DOM `6.x`
- Tailwind CSS `3.4.x`
- shadcn/ui and Radix UI primitives
- TanStack React Query `5.x`
- Vitest `3.x`

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
- `src/lib/auth.ts`
- `src/lib/adminStore.ts`
- `src/lib/settingsStore.ts`
- `src/lib/i18n.tsx`

`nihomeweb/` now ships the Vite + React source tree as the active frontend. The old Materialize starter-kit and Next.js shell have been moved under `legacy/` and are no longer the active frontend architecture.

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

Admin route groups:

- `/admin`
- `/admin/posts`, post create/view/edit routes
- `/admin/projects`, project create/view/edit routes
- `/admin/contacts`
- `/admin/recruitment`
- `/admin/settings` and detailed settings routes
- `/admin/categories`
- `/admin/customers`, customer roles, online customers, and activity log
- `/admin/clients`, `/admin/partners`, `/admin/suppliers`
- `/admin/awards`, slideshow, map, about, and help placeholder/simple pages
- `/admin/processes/*`
- `/admin/system/*`

The catch-all route renders `src/pages/NotFound.tsx`.

## Current Portal Shell Behavior

- `src/App.tsx` wraps the app in `QueryClientProvider`, `I18nProvider`, `TooltipProvider`, toast providers, and `BrowserRouter`.
- Public pages use the public header/footer layout where implemented.
- Admin pages use `AdminLayout` for sidebar navigation, admin topbar behavior, and language controls.
- `/login` and `/register` use mock localStorage auth from `src/lib/auth.ts`.
- Admin pages are currently demo routes; production route protection is not implemented.

## Current Config Reality

- `vite.config.ts` defines the Vite build, dev server, and path alias behavior.
- The dev server defaults to port `8080`.
- `components.json` configures shadcn/ui with aliases under `@/`.
- `tailwind.config.ts` and `src/index.css` own the active design tokens and utility classes.
- `vitest.config.ts` and `src/test/setup.ts` define the current test setup.
- There is no committed production `.env.example` contract yet.
- The repo includes repo-local AI docs, a project brief, and a memory bank under `docs/ai/`.
- Repo-local skills live under `.agents/skills/`.

## Current Gaps

- No real auth provider or backend session model is implemented yet.
- No production API client or server-state contract is established yet.
- Admin content/settings persistence is still localStorage-backed demo behavior.
- No documented deployment/environment contract is committed yet.
- Legacy source under `legacy/` has not been deleted because it remains useful as reference material during the refactor.

## Agent Notes

- Do not assume Next.js Pages Router, `_app.tsx`, `_document.tsx`, `next.config.*`, MUI, Emotion, or Materialize are active in this repo.
- `src/pages/` means React page components routed from `src/App.tsx`, not filesystem routing.
- Treat localStorage stores as demo scaffolding until a backend API decision is recorded.
