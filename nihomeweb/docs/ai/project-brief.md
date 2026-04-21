# Nihomeweb Product And Architecture Brief

This document is the repo-local product, architecture, and phased execution brief for `nihomeweb/`.
It is not a one-shot scaffold script.
Agents should read it before any non-trivial implementation task together with `AGENTS.md`, the working procedure, the frontend playbook, and the memory bank.

## Project Identity

- Repo name: `nihomeweb`
- Product name: Nihome
- Product shape: internal admin portal plus client portal for a design-and-build business
- Current phase: starter-kit baseline reset

## Current Technical Baseline

- Next.js `13.0.0`
- React `18.2.0`
- TypeScript `4.8.x`
- MUI `5.x`
- Emotion `11.x`
- Pages Router

Current repo reality:

- starter-kit layout, theme, and settings system are the active frontend baseline
- mock JWT auth is still present as a placeholder
- no real API client
- no production auth provider or route ownership decision beyond the current mock baseline
- no deep feature modules yet

## Architectural Defaults

- Pages Router is the active routing model.
- Route files live under `src/pages/`.
- `_app.tsx` owns providers, guards, theme setup, and page defaults.
- `_document.tsx` owns document markup and Emotion SSR integration.
- Build by phase instead of scaffolding every future module at once.

## Current Scope

Included in the current baseline reset:

- replace the retired Next 16 App Router shell with the imported starter-kit baseline
- personalize the starter-kit for Nihome and remove vendor-facing template language
- preserve these routes: `/`, `/projects`, `/notifications`, `/login`, `/forgot-password`, `/admin`, `/admin/dashboard`
- keep mock auth only as an explicit placeholder
- align repo-local AI docs and memory to the active baseline

Explicitly deferred:

- auth provider choice
- route protection
- API client layer
- state-management libraries
- deep business-logic feature components
- broad `full-template` imports
- framework modernization back to a newer Next.js baseline

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

The starter baseline reset is only complete when:

- `npm run build` passes
- `npx tsc --noEmit` passes
- the official route surface renders cleanly
- visible legacy vendor references are gone from the shipped UI
- no second active frontend architecture remains in normal use
- repo docs and memory reflect the same implementation reality

## Future Phases

Future work may introduce:

- auth and route protection
- API integration
- state management
- deeper admin modules
- richer client portal flows
- selective component imports from the larger full template

Those phases should only start after the relevant decisions are documented in the memory bank.
