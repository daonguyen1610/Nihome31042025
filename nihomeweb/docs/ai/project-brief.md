# Nihomeweb Product And Architecture Brief

This document is the repo-local product, architecture, and phased execution brief for `nihomeweb/`.
It is not a one-shot scaffold script.
Agents should read it before any non-trivial implementation task together with `AGENTS.md`, the working procedure, the frontend playbook, and the memory bank.

## Project Identity

- Repo name: `nihomeweb`
- Product name: Nihome
- Product shape: internal admin portal plus client portal for a design-and-build business
- Current phase: Phase 1 shell

## Current Technical Baseline

- Next.js `16.2.3`
- React `19.2.4`
- TypeScript `5.x`
- Tailwind CSS `4.x`
- App Router

Current repo reality:

- no auth layer
- no API client
- no route protection
- no server-state library
- no deep feature modules yet

## Architectural Defaults

- Server Components are the default.
- Add `"use client"` only when hooks, browser APIs, or active-state UI truly require it.
- Do not install unresolved dependencies just because older prompts mentioned them.
- Do not use outdated Next.js 14 or 15 conventions in this repo.
- Build by phase instead of scaffolding every future module at once.

## Phase 1 Scope

Phase 1 creates the app shell and shared scaffolding only.

Included in Phase 1:

- remove static export from `next.config.ts`
- route groups for `(auth)`, `(client)`, and `(admin)`
- auth placeholder pages for `/login` and `/forgot-password`
- client home, projects, and notifications placeholders
- admin shell and dashboard placeholder
- root `not-found.tsx`
- minimal shared layout and common components
- repo-local AI docs and memory aligned to the implementation

Explicitly deferred:

- auth provider choice
- route protection
- API client layer
- state-management libraries
- business-logic feature components
- deep admin modules such as CRM, design, construction, procurement, and finance

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

Phase 1 is only complete when:

- `npm run build` passes
- `npx tsc --noEmit` passes
- route groups and layouts render cleanly
- static export is no longer configured
- no file is empty
- no unresolved dependency is introduced
- repo docs and memory reflect the same implementation reality

## Future Phases

Future work may introduce:

- auth and route protection
- API integration
- state management
- deeper admin modules
- richer client portal flows

Those phases should only start after the relevant decisions are documented in the memory bank.
