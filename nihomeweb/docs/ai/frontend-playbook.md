# Frontend Playbook

This document explains how AI agents should approach frontend work in `nihomeweb/`.
Use it together with `AGENTS.md`, `docs/ai/project-brief.md`, and the memory bank.

## Purpose

- align Claude and Codex on the same frontend workflow
- route Codex to the right Vercel skill for the task
- keep the Vite / React / shadcn baseline consistent across the repo
- reduce styling and architecture drift when multiple agents contribute

## Required Baseline

- Treat the current Vite + React SPA as authoritative.
- `src/main.tsx` mounts the app.
- `src/App.tsx` owns top-level providers, `BrowserRouter`, and the route table.
- `src/pages/` contains React page components; it is not Next.js Pages Router.
- `src/components/ui/` contains shadcn/ui primitives configured by `components.json`.
- `src/index.css` and `tailwind.config.ts` own design tokens and global utilities.
- The app currently uses local seed data and localStorage-backed demo stores.
- `legacy/materialize-starter-kit/` and `legacy/next16-shell/` are archived references, not active app code.
- Do not reintroduce the old Materialize starter-kit, full admin template, or Next.js assumptions without a dated decision and migration plan.

## Vercel Skill Routing

Use these skills intentionally in Codex environments:

### `vercel:react-best-practices`

Use when:

- reviewing React or TSX quality
- editing multiple components in one task
- checking hooks, accessibility, typing, performance, and component structure after a UI pass

### `vercel:shadcn`

Use when:

- introducing or standardizing shared primitives such as buttons, cards, tabs, dialogs, forms, selects, or tables
- changing `src/components/ui/` or `components.json`
- moving repeated UI patterns into reusable building blocks

Do not bypass the existing shadcn/Radix primitives with one-off copies unless there is a clear reason.

### `vercel:agent-browser-verify` and `vercel:agent-browser`

Use when:

- a dev server is running and a visual gut-check is needed
- testing routes, forms, admin flows, or responsive layout behavior in a browser is part of the task

### `vercel:swr`

Use only if the team explicitly chooses SWR later.
The current project already includes TanStack Query, so server-state work should normally evaluate that existing dependency first.

### `vercel:nextjs`

Do not use this as the default project skill.
Use it only if a future task explicitly starts a Next.js migration and the decision is recorded in the memory bank.

## Default Frontend Conventions

### Architecture

- Vite + React SPA first.
- Route declarations live in `src/App.tsx`.
- Add a new route by adding the page component and an explicit `Route` entry.
- Public pages use `src/components/layout/Layout.tsx` unless there is a documented exception.
- Admin pages use `src/components/layout/AdminLayout.tsx` for sidebar, topbar, and admin navigation.
- Shared primitive UI belongs in `src/components/ui/`.
- Shared app-specific components belong in `src/components/` by role, not by page location.
- Static seed content belongs in `src/data/`.
- Client-side demo persistence belongs in `src/lib/`, but production data access should be centralized separately.
- Use the `@/` alias configured by Vite and TypeScript.

### Data Fetching And State

- Keep the current static/localStorage demo baseline explicit.
- Do not treat `src/lib/auth.ts`, `src/lib/adminStore.ts`, or `src/lib/settingsStore.ts` as production-ready backend integration.
- If real API calls are introduced, centralize API access instead of scattering raw `fetch` calls across presentation components.
- Prefer TanStack Query for shared client-side server state if the existing dependency fits the use case.
- Keep backend base URLs and environment contracts documented; Vite-exposed variables must use the `VITE_` prefix.

### UI System

- Tailwind CSS, shadcn/ui, Radix UI, and lucide-react are the active UI foundation.
- Preserve the NICON / Nihome visual language currently encoded in `src/index.css`: Be Vietnam Pro, red/orange primary gradients, neutral surfaces, and selected indigo accents.
- Public pages should stay media-rich and brand-forward, using real project/company assets where available.
- Admin pages should stay dense, scannable, and work-focused rather than marketing-like.
- Use lucide icons for recognizable actions where possible.
- Avoid silently mixing in MUI, Materialize, or another token system.
- If a visual convention changes durably, update `docs/ai/memory-bank/04-ui-system.md` in the same task.

### Accessibility And Responsiveness

- Design and review at both desktop and mobile widths.
- Use semantic HTML before adding ARIA fallbacks.
- Keep keyboard access intact for menus, dialogs, forms, filters, and admin actions.
- Ensure text fits within buttons, cards, sidebars, and navigation labels.
- Keep contrast and touch targets readable without relying on one perfect viewport.

### Documentation

- If a task changes durable frontend conventions, update the memory bank in the same task.
- If implementation and docs disagree, fix the docs or log the mismatch before building more on top of it.
- Do not add a new library, pattern, or visual direction just because an agent suggested it; it must fit this playbook and be documented if durable.

## Review Expectations

Before closing a non-trivial frontend task, verify:

- the assigned owner and task boundary stayed clear
- routing still matches the Vite + React Router baseline
- no old Next.js, Materialize starter-kit, or full admin template assumption was reintroduced
- no new convention conflicts with the memory bank
- presentational components do not hide infrastructure assumptions
- any new library or reusable pattern was intentionally chosen
- the UI remains responsive and keyboard-usable for changed surfaces
- any durable decision or blocker was written down in the memory bank
