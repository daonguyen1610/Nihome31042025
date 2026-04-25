# Nihomeweb

Active frontend for the NICON / Nihome web experience.

This project has been refactored away from the prior Next.js / Materialize starter-kit baseline. The active app is now a Vite + React single-page app using React Router, Tailwind CSS, shadcn/ui, Radix UI, TanStack Query, and Vitest.

The older Next.js and Materialize code is retained only under `legacy/` as reference material. New feature work should build on the current source tree under `src/`.

## Current Stack

- Vite `5.x`
- React `18.3.x`
- TypeScript `5.8.x`
- React Router DOM `6.x`
- Tailwind CSS `3.4.x`
- shadcn/ui and Radix UI primitives
- TanStack React Query `5.x`
- Vitest `3.x`

## Source Map

- `src/main.tsx`: app bootstrap
- `src/App.tsx`: providers, browser router, and route table
- `src/pages/`: public page components and admin page components
- `src/components/layout/`: public and admin shell components
- `src/components/ui/`: shadcn/ui primitives
- `src/components/admin/`: admin-specific reusable controls
- `src/data/`: static seed data
- `src/lib/`: localStorage-backed demo auth, admin, settings, i18n, and utilities
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
npm run test
```

The Vite dev server is configured in `vite.config.ts` and defaults to port `8080`.

## Current Constraints

- Auth is demo-only and localStorage-backed.
- Admin CRUD and settings persistence are localStorage-backed demo behavior.
- No production API client or backend session model has been committed yet.
- `legacy/materialize-starter-kit/` and `legacy/next16-shell/` are not active frontend architecture.
