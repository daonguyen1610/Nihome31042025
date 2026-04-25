# Nihomeweb Product And Architecture Brief

This document is the repo-local product, architecture, and phased execution brief for `nihomeweb/`.
It is not a one-shot scaffold script.
Agents should read it before any non-trivial implementation task together with `AGENTS.md`, the working procedure, the frontend playbook, and the memory bank.

## Project Identity

- Repo name: `nihomeweb`
- Product name: NICON / Nihome
- Product shape: public corporate website plus lightweight admin/content portal for a design-and-build business
- Current phase: Vite/Lovable baseline adoption after the template refactor

## Current Technical Baseline

- Vite `5.x`
- React `18.3.x`
- TypeScript `5.8.x`
- React Router DOM `6.x`
- Tailwind CSS `3.4.x`
- shadcn/ui and Radix UI primitives
- TanStack React Query `5.x`
- Vitest `3.x`

Current repo reality:

- `nihomeweb/` now contains the Vite + React frontend copied from the Lovable source direction.
- The app is a client-rendered SPA, not a Next.js app.
- Public website pages and an admin shell already exist.
- Demo auth, admin CRUD, and settings behavior are localStorage-backed placeholders.
- No production API client, auth provider, or backend session model is committed yet.

## Architectural Defaults

- Browser routing is centralized in `src/App.tsx`.
- `src/main.tsx` owns app bootstrapping.
- Public pages live under `src/pages/`.
- Admin pages live under `src/pages/admin/`.
- Public layout belongs in `src/components/layout/Layout.tsx`.
- Admin layout belongs in `src/components/layout/AdminLayout.tsx`.
- Shared shadcn/Radix primitives belong in `src/components/ui/`.
- Static seed data belongs in `src/data/`.
- Demo localStorage stores belong in `src/lib/` until a real API strategy replaces them.

## Current Scope

Completed in the baseline refactor:

- made the Vite/Lovable source tree the active frontend inside `nihomeweb/`
- removed the Materialize starter-kit and Next.js shells entirely
- rewrote AI docs and memory around Vite, React Router, Tailwind, shadcn, and the current source tree
- preserved the existing public and admin route surfaces in `src/App.tsx`
- kept demo auth and localStorage stores as explicit placeholders

Explicitly deferred:

- production auth provider choice
- route protection and backend session ownership
- API client layer and backend integration contract
- production persistence for admin content/settings
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
- `npm run test` passes when tests are present and dependencies are installed
- the official public and admin route surfaces render cleanly
- no Materialize, Next.js, or stale starter-kit assumptions remain in active repo docs
- repo docs and memory reflect the same implementation reality

## Future Phases

Future work may introduce:

- production auth and route protection
- API integration with the backend
- persistent admin content management
- richer admin modules
- stronger client portal flows
- deployment and environment-variable hardening

Those phases should only start after the relevant decisions are documented in the memory bank.
