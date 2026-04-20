# Decisions And Open Questions

Last reviewed: 2026-04-20

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

## Open Questions

### Should `output: "export"` remain the frontend deployment strategy?

Why it matters: planned backend integration, route handlers, or runtime proxy behavior may require a Next.js server runtime instead of static export.

### What is the intended backend integration shape for web-to-API traffic?

Why it matters: the current landing page fetches `/api/system/health`, but the repo does not yet commit the rewrite, proxy, or route-handler strategy that would make that path reliable.

### When should a shared component system be introduced?

Why it matters: the app is still small, but repeated route-local patterns should not grow unchecked before a reusable layer appears.

## Handoff Notes

- When resolving an open question, convert the outcome into a dated decision and update the related memory-bank file in the same task.
- If a future task changes product scope, architecture, or UI conventions, update this file along with the corresponding detail file.
- Before closing a non-trivial task, confirm that the owner was clear, the task boundary stayed clear, and any durable decision was written into the repo.
