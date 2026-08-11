# Nihomeweb

Active frontend for the NICON / Nihome design-and-build platform. It is a production API-backed single-page application served with the ASP.NET Core backend in the Docker Compose stack.

This project has been refactored away from the prior Next.js / Materialize starter-kit baseline. The active app is now a Vite + React single-page app using React Router, Tailwind CSS, shadcn/ui, Radix UI, and TanStack Query.

The prior Next.js and Materialize starter sources are no longer present. New feature work must build on the current source tree under `src/`.

## Current Stack

- Vite `5.x`
- React `18.3.x`
- TypeScript `5.8.x`
- React Router DOM `6.x`
- Tailwind CSS `3.4.x`
- shadcn/ui and Radix UI primitives
- TanStack React Query `5.x`
- Playwright `1.x` for browser E2E smoke coverage

## Source Map

- `src/main.tsx`: app bootstrap
- `src/App.tsx`: providers, browser router, and route table
- `src/pages/`: public page components and admin page components
- `src/components/layout/`: public and admin shell components
- `src/components/ui/`: shadcn/ui primitives
- `src/components/admin/`: admin-specific reusable controls
- `src/data/`: static seed data
- `src/lib/`: permissions, URL/media resolution, i18n, and shared utilities
- `src/services/`: typed public, authentication, and administration API clients
- `src/index.css`: global Tailwind layers, tokens, and utilities
- `tailwind.config.ts`: Tailwind theme extension

## Agent Workflow

Before non-trivial work, read:

1. `AGENTS.md`
2. `docs/ai/working-procedure.md`
3. `docs/ai/frontend-playbook.md`
4. `docs/ai/project-brief.md`
5. `docs/ai/memory-bank/README.md`
6. the relevant memory-bank files

Repo-facing AI docs stay in English so Claude, Codex, and Vercel skill guidance share the same source of truth.

## Commands

```bash
npm run dev
npm run build
npm run lint
npm run test:e2e
```

The Vite dev server is configured in `vite.config.ts` and defaults to port `8080`. The integrated Docker application is available at `http://localhost:5043`.

## Project Handover

- Route: `/admin/construction/handover`
- Permission gate: `construction.handover.view`
- API client: handover types and operations are exposed by `src/services/adminApi.ts`.
- UI: responsive list/card presentation, filters, sorting, pagination, summary cards, CSV export, create/edit form, readiness details, and lifecycle actions.
- Security: document links use the shared URL resolver; unsafe or malformed values are rendered without clickable navigation.
- Completion: the action requires a ready record, `construction.handover.complete`, and at least one signatory.

## Current Product Areas

- Public content, recruitment, contact, authentication, and own-profile flows
- Users, dynamic RBAC, notifications, audit, translations, settings, workflows, and content administration
- CRM leads, customers, opportunities, quotes, capability documents, tenders, surveys, contracts, and variation orders
- Design projects, concepts, basic design, shop drawings, revisions, and IFC tracking
- Permit checklists and construction tasks/Gantt, site diaries, punch lists, partial acceptance, as-built records, and handover

Authentication, RBAC, translations, and production modules use backend APIs. The known exception is `/admin/master-data`, whose editable language list is still stored in localStorage; treat it as migration debt rather than a production persistence pattern.
