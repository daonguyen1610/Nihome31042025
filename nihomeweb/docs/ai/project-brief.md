# Nihomeweb Product And Architecture Brief

This document is the repo-local product, architecture, and phased execution brief for `nihomeweb/`.
It is not a one-shot scaffold script.
Agents should read it before any non-trivial implementation task together with `AGENTS.md`, the working procedure, the frontend playbook, and the memory bank.

## Project Identity

- Repo name: `nihomeweb`
- Product name: NICON / Nihome
- Product shape: public corporate website plus an internal CRM, design, permitting, construction, content, and system-administration platform for a design-and-build business
- Current phase: API-backed Vite/React operational platform with selected incomplete workflows documented below

## Current Technical Baseline

- Vite `5.x`
- React `18.3.x`
- TypeScript `5.8.x`
- React Router DOM `6.x`
- Tailwind CSS `3.4.x`
- shadcn/ui and Radix UI primitives
- TanStack React Query `5.x`
- Playwright `1.x`

Current repo reality:

- `nihomeweb/` now contains the Vite + React frontend copied from the Lovable source direction.
- The app is a client-rendered SPA, not a Next.js app.
- Public website pages and an admin shell already exist.
- Authentication uses the ASP.NET Core JWT/refresh contract with Redux state and protected routes.
- Public and admin modules use centralized typed services under `src/services/`. `/admin/master-data` is the known exception whose editable language list still uses localStorage.
- Permission-based route/action gates consume the backend permission contract.

## Architectural Defaults

- Browser routing is centralized in `src/App.tsx`.
- `src/main.tsx` owns app bootstrapping.
- Public pages live under `src/pages/`.
- Admin pages live under `src/pages/admin/`.
- Public layout belongs in `src/components/layout/Layout.tsx`.
- Admin layout belongs in `src/components/layout/AdminLayout.tsx`.
- Shared shadcn/Radix primitives belong in `src/components/ui/`.
- Static display-only seed data belongs in `src/data/`.
- Backend access belongs in `src/services/`; shared permissions, URL resolution, i18n, and other utilities belong in `src/lib/`.

## Current Scope

Completed in the baseline refactor:

- made the Vite/Lovable source tree the active frontend inside `nihomeweb/`
- removed the Materialize starter-kit and Next.js shells entirely
- rewrote AI docs and memory around Vite, React Router, Tailwind, shadcn, and the current source tree
- preserved the existing public and admin route surfaces in `src/App.tsx`
- established centralized backend APIs, Redux authentication, protected routes, and permission-driven admin navigation
- added API-backed CRM workflows for leads, customers, opportunities, quotes, capability documents, tenders, surveys, contracts, and variation orders
- added API-backed design projects, concepts, basic design, shop drawings, revisions, IFC tracking, and permit checklists
- added API-backed construction tasks/Gantt, site diaries, partial acceptance, as-built records, punch lists, and project handover
- added backend-backed content, recruitment, contacts, notifications, users/RBAC, audit, settings, translations, workflows, logos, and process-document administration

Explicitly deferred:

- migration of the `/admin/master-data` editable language list from localStorage to backend persistence
- survey media management, procurement/warehouse operations, Google Drive integration, cash flow, profit-and-loss, and broader cross-module analytics
- consistent server-state strategy for future modules
- broad imports from Materialize or another full admin template
- migration back to Next.js

## AI Working Rules

Before non-trivial work, read:

1. `AGENTS.md`
2. `docs/ai/working-procedure.md`
3. `docs/ai/frontend-playbook.md`
4. `docs/ai/project-brief.md`
5. `docs/ai/memory-bank/README.md`
6. The memory-bank files relevant to the task

Update the memory bank in the same task when a durable decision changes architecture, product direction, shared UI conventions, or implementation assumptions.

## Acceptance Criteria

The refactor documentation baseline is only complete when:

- `npm run build` passes
- `npm run lint` passes or any existing lint baseline issues are documented
- relevant Playwright E2E smoke coverage passes against the integrated stack
- the official public and admin route surfaces render cleanly
- no Materialize, Next.js, or stale starter-kit assumptions remain in active repo docs
- repo docs and memory reflect the same implementation reality

## Future Phases

Future work may introduce:

- stronger authentication/session hardening
- backend persistence for the remaining master-data language configuration
- completion of the explicitly partial operational areas
- stronger client portal flows
- deployment and environment-variable hardening

Those phases should only start after the relevant decisions are documented in the memory bank.
