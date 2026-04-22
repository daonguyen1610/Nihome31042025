# Decisions And Open Questions

Last reviewed: 2026-04-23

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

### 2026-04-21 — `nihomeweb` officially shifted to the starter-kit baseline

Rationale: The team prioritized speed and a working admin template over a risky forward-port from an older template into the retired Next 16 App Router shell.

### 2026-04-21 — MUI and Emotion are now the active admin UI system

Rationale: The imported starter-kit provides the official layout, theme, and shared shell until a later redesign or framework migration is documented.

### 2026-04-21 — The larger full template stays deferred

Rationale: Pulling in the full template now would increase scope and dependency risk; future work should mine it selectively for page-specific components.

### 2026-04-23 — Login and register screens were selectively imported from `full-version`

Rationale: The team needed auth entry screens ready for backend API wiring, but still wanted to preserve the Pages Router starter baseline and avoid a broad template merge.

## Open Questions

### Which auth strategy should Nihome use after the starter baseline reset?

Why it matters: route protection, login flows, and session ownership all depend on it.

### What API access pattern should the frontend adopt after the starter baseline reset?

Why it matters: future modules need a clear decision on native fetch, a wrapper layer, or another client approach before real data work begins.

### What state-management approach is justified once modules become interactive?

Why it matters: the current baseline intentionally avoids SWR, TanStack Query, and Zustand until there is a real need and a shared decision.

### Should the UI layer stay on the imported MUI starter baseline long term?

Why it matters: the repo now uses the starter-kit MUI baseline, but later work may still decide to deepen that direction or replace parts of it.

## Handoff Notes

- When resolving an open question, convert the outcome into a dated decision and update the related memory-bank file in the same task.
- If a future task changes product scope, architecture, or UI conventions, update this file along with the corresponding detail file.
- Before closing a non-trivial task, confirm that the owner was clear, the task boundary stayed clear, and any durable decision was written into the repo.
