# Decisions And Open Questions

Last reviewed: 2026-04-21

## Decisions

### 2026-04-20 — `AGENTS.md` is the canonical AI rules file

Rationale: Codex, Claude, and future agents need one shared contract for read order, memory usage, and collaboration rules.

### 2026-04-20 — Durable repo memory lives under `docs/ai/memory-bank/`

Rationale: Important context should survive across chat sessions and across different agent tools.

### 2026-04-20 — Repo-facing AI docs stay in English

Rationale: English is more stable across Claude, Codex, and Vercel skill guidance, even if humans discuss work in Vietnamese.

### 2026-04-20 — Frontend rules are moderately opinionated

Rationale: The repo needs enough structure to prevent drift between contributors, but not so much rigidity that early product work becomes slow.

### 2026-04-20 — One owner should control one task slice at a time

Rationale: Clear ownership prevents Claude, Codex, and human collaborators from editing the same surface with conflicting assumptions.

### 2026-04-20 — Durable repo docs override chat memory

Rationale: Important context must remain discoverable in the repository even when chat sessions, tools, or agents change.

### 2026-04-21 — `docs/ai/project-brief.md` is the repo-local execution brief

Rationale: Future agents should use a stable brief inside the repository instead of depending on a file from `Downloads`.

### 2026-04-21 — Static export was removed from `next.config.ts`

Rationale: The planned multi-portal app needs a normal runtime Next.js shape for future auth, protected pages, and server-aware integrations.

### 2026-04-21 — Phase 1 keeps the branded client home while adding the portal skeleton

Rationale: The existing visual direction is worth preserving, and it can serve as the client-home shell without pretending deeper product features already exist.

## Open Questions

### Which auth strategy should Nihome use after Phase 1?

Why it matters: route protection, login flows, and session ownership all depend on it.

### What API access pattern should the frontend adopt after Phase 1?

Why it matters: future modules need a clear decision on native fetch, a wrapper layer, or another client approach before real data work begins.

### What state-management approach is justified once modules become interactive?

Why it matters: Phase 1 intentionally avoids TanStack Query, SWR, and Zustand until there is a real need and a shared decision.

### Should the UI layer stay custom or adopt a shared component library later?

Why it matters: Phase 1 uses hand-built shared primitives, but later modules may justify a broader UI system choice.

## Handoff Notes

- When resolving an open question, convert the outcome into a dated decision and update the related memory-bank file in the same task.
- If a future task changes product scope, architecture, or UI conventions, update this file along with the corresponding detail file.
- Before closing a non-trivial task, confirm that the owner was clear, the task boundary stayed clear, and any durable decision was written into the repo.
