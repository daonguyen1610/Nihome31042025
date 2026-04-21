# Frontend Playbook

This document explains how AI agents should approach frontend work in `nihomeweb/`.
Use it together with `AGENTS.md`, `docs/ai/project-brief.md`, and the memory bank.

## Purpose

- align Claude and Codex on the same frontend workflow
- route Codex to the right Vercel skill for the task
- keep the starter-kit baseline decisions consistent across the repo
- reduce styling and architecture drift when multiple agents contribute

## Required Baseline

- Treat the installed Next.js version in this repo as authoritative.
- Before changing app code, read the relevant local Next.js docs under `node_modules/next/dist/docs/` when dependencies are installed.
- Prefer the current Pages Router architecture instead of mixing in a second active routing model.
- Preserve the starter-kit layout, theme, and settings system unless the team explicitly documents a replacement.

## Vercel Skill Routing

Use these skills intentionally in Codex environments:

### `vercel:nextjs`

Use for any task touching:

- `src/pages/`
- `_app.tsx`, `_document.tsx`, `next.d.ts`
- `next.config.*`
- layouts, routing, rendering mode, auth guards, or page-level redirects
- cache behavior, revalidation, or fetch strategy
- migration work that affects the current Pages Router baseline

This is the default skill for most work in this repo.

### `vercel:react-best-practices`

Use when:

- reviewing React or TSX quality
- editing multiple components in one task
- checking hooks, accessibility, typing, and component structure after a UI pass

### `vercel:shadcn`

Use when:

- introducing a shared primitive such as buttons, cards, tabs, dialogs, or form controls
- moving repeated UI patterns into reusable building blocks
- standardizing composition instead of continuing ad hoc styled sections

Do not introduce `shadcn/ui` just for one isolated element with no reuse value, and do not let it silently replace the current MUI baseline.

### `vercel:swr`

Use when:

- client-side server state becomes persistent, shared, retry-aware, or mutation-heavy
- a one-off `fetch` in `useEffect` is no longer enough
- the UI needs caching, revalidation, deduplication, or optimistic updates

Do not add SWR prematurely for simple static screens.

### `vercel:geist`

Use only if the team explicitly decides to standardize on Geist typography.
Do not quietly replace the current visual direction with Geist just because the skill exists.

## Default Frontend Conventions

### Architecture

- Pages Router first
- `src/pages/` is the active route surface
- `_app.tsx` owns app-level providers, guards, theme setup, and head defaults
- `_document.tsx` owns document-level markup and Emotion server integration
- Avoid direct backend URLs inside presentational components
- Keep phase work aligned with the current repo brief instead of jumping ahead to future modules or dependencies

### Data Fetching

- Start with the simplest approach that matches the rendering model.
- Keep backend access patterns explicit and easy to centralize later.
- The current baseline uses mock auth only and does not commit to a real API layer yet.
- If client-side data fetching grows, move toward a shared server-state strategy instead of repeating `useEffect` fetch blocks everywhere.

### UI System

- MUI 5 and Emotion are the official admin UI system in the current repo state.
- Preserve the starter-kit layout system while replacing vendor/demo language with Nihome language.
- Promote repeated UI into shared MUI-aware components or theme overrides before drift accumulates.
- Avoid mixing the retired Tailwind shell and the active MUI shell in the same implementation pass.

### Documentation

- If a task changes durable frontend conventions, update the memory bank in the same task.
- If implementation and docs disagree, fix the docs or log the mismatch before building more on top of it.
- Do not add a new library, pattern, or visual direction just because an agent suggested it; it must fit this playbook and be documented if durable.

## Review Expectations

Before closing a non-trivial frontend task, verify:

- the assigned owner and task boundary stayed clear
- the chosen rendering model matches the installed Next.js version and current Pages Router baseline
- no second active routing architecture was introduced by accident
- no new convention conflicts with the memory bank
- presentational components do not hide infrastructure assumptions
- any new library or reusable pattern was intentionally chosen
- any durable decision or blocker was written down in the memory bank
