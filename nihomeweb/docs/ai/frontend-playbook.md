# Frontend Playbook

This document explains how AI agents should approach frontend work in `nihomeweb/`.
Use it together with `AGENTS.md` and the memory bank.

## Purpose

- align Claude and Codex on the same frontend workflow
- route Codex to the right Vercel skill for the task
- keep Next.js 16 decisions consistent across the repo
- reduce styling and architecture drift when multiple agents contribute

## Required Baseline

- Treat Next.js 16 behavior as authoritative, not older framework habits.
- Before changing app code, read the relevant local Next.js docs under `node_modules/next/dist/docs/` when dependencies are installed.
- Prefer App Router conventions everywhere in this frontend.
- Prefer Server Components by default and add `"use client"` only when a component genuinely needs browser-only interactivity or hooks.

## Vercel Skill Routing

Use these skills intentionally in Codex environments:

### `vercel:nextjs`

Use for any task touching:

- `app/`
- `next.config.*`
- metadata, layouts, routing, rendering mode, or route handlers
- cache behavior, revalidation, or fetch strategy
- server and client boundaries in the App Router

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

Do not introduce `shadcn/ui` just for one isolated element with no reuse value.

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

- App Router first
- Server Components by default
- Client Components only when interactivity requires them
- Route handlers only when the frontend truly owns the server-side behavior
- Avoid direct backend URLs inside presentational components

### Data Fetching

- Start with the simplest approach that matches the rendering model.
- Keep backend access patterns explicit and easy to centralize later.
- If client-side data fetching grows, move toward a shared server-state strategy instead of repeating `useEffect` fetch blocks everywhere.

### UI System

- Prefer shared tokens and composition over repeated hard-coded values.
- Preserve the current warm, premium visual direction unless the team explicitly changes brand direction.
- Promote repeated styles into reusable classes, utilities, or shared components before drift accumulates.
- Avoid mixing multiple unrelated design languages in the same pass.

### Documentation

- If a task changes durable frontend conventions, update the memory bank in the same task.
- If implementation and docs disagree, fix the docs or log the mismatch before building more on top of it.
- Do not add a new library, pattern, or visual direction just because an agent suggested it; it must fit this playbook and be documented if durable.

## Review Expectations

Before closing a non-trivial frontend task, verify:

- the assigned owner and task boundary stayed clear
- the chosen rendering model matches Next.js 16 expectations
- server/client boundaries are intentional
- no new convention conflicts with the memory bank
- presentational components do not hide infrastructure assumptions
- any new library or reusable pattern was intentionally chosen
- any durable decision or blocker was written down in the memory bank
