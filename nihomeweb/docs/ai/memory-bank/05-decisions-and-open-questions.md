# Decisions And Open Questions

Last reviewed: 2026-04-25

## Decisions

### 2026-04-20 - `AGENTS.md` is the canonical AI rules file

Rationale: Codex, Claude, and future agents need one shared contract for read order, memory usage, and collaboration rules.

### 2026-04-20 - Durable repo memory lives under `docs/ai/memory-bank/`

Rationale: Important context should survive across chat sessions and across different agent tools.

### 2026-04-20 - Repo-facing AI docs stay in English

Rationale: English is more stable across Claude, Codex, and Vercel skill guidance, even if humans discuss work in Vietnamese.

### 2026-04-20 - Frontend rules are moderately opinionated

Rationale: The repo needs enough structure to prevent drift between contributors, but not so much rigidity that early product work becomes slow.

### 2026-04-20 - One owner should control one task slice at a time

Rationale: Clear ownership prevents Claude, Codex, and human collaborators from editing the same surface with conflicting assumptions.

### 2026-04-20 - Durable repo docs override chat memory

Rationale: Important context must remain discoverable in the repository even when chat sessions, tools, or agents change.

### 2026-04-21 - `docs/ai/project-brief.md` is the repo-local execution brief

Rationale: Future agents should use a stable brief inside the repository instead of depending on chat-only context.

### 2026-04-25 - `nihomeweb/` now uses the Vite/Lovable source tree as the active frontend

Rationale: The team decided to continue future frontend feature work from the existing Lovable/Vite source direction instead of the old Next.js / Materialize starter-kit baseline.

### 2026-04-25 - Vite + React Router replaces the prior Next.js Pages Router assumptions

Rationale: The current source tree is a Vite SPA with React Router, Tailwind, shadcn/ui, and Radix UI. Agent docs must match source reality instead of the previous Next.js Pages Router and MUI/Materialize starter-kit direction.

### 2026-04-25 - Materialize starter-kit and Next 16 shell are legacy reference only

Rationale: The team explicitly chose not to continue with starter-kit or full admin template imports. Future admin features should grow from the current `src/` components unless a new decision is recorded.

### 2026-04-25 - localStorage auth/admin stores are demo scaffolding

Rationale: They make the UI usable during frontend development but do not define production auth, authorization, persistence, or API contracts.

### 2026-05-05 - About/Profile content must be sourced from backend seed and API, not frontend fallbacks

Rationale: Public `Profile` content and admin `AboutContent` should reflect the same source of truth. Frontend hardcoded fallback datasets caused drift between client rendering and admin editing, especially for `organization-main`. The backend `ContentSeeder` and `about-sections` API now define the default content baseline.

## Open Questions

### Should the `legacy/` reference folders remain long term?

Why it matters: they are useful during transition but can confuse future agents if treated as active architecture.

### Which auth strategy should NICON / Nihome use after the demo baseline?

Why it matters: route protection, login flows, permissions, and session ownership all depend on it.

### What API access pattern should the frontend adopt?

Why it matters: future modules need a clear decision on API base URL handling, request wrappers, TanStack Query usage, and error/auth behavior before real data work begins.

### What persistence model should replace localStorage admin stores?

Why it matters: content, project, recruitment, settings, and system screens currently feel interactive but do not persist outside the browser.

### What deployment/environment contract should this Vite app use?

Why it matters: Vercel or another host will need clear build commands, output directory, environment variables, and API routing/proxy assumptions.

## Handoff Notes

- When resolving an open question, convert the outcome into a dated decision and update the related memory-bank file in the same task.
- If a future task changes product scope, architecture, or UI conventions, update this file along with the corresponding detail file.
- Before closing a non-trivial task, confirm that the owner was clear, the task boundary stayed clear, and any durable decision was written into the repo.
- Do not delete `legacy/` as part of routine agent work without explicit user approval immediately before the destructive operation.
