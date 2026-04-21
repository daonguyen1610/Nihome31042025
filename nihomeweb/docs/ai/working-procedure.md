# Working Procedure

This is the standard day-to-day procedure for you, your teammate, Claude, and Codex when working in `nihomeweb/`.
Use it with `AGENTS.md`, the frontend playbook, and the memory bank.

## Core Rule

Use repo docs as the operating system for collaboration, not chat memory.
Durable context must live in the repository.

## Before Starting A Non-Trivial Task

Read in this order:

1. `AGENTS.md`
2. `docs/ai/working-procedure.md`
3. `docs/ai/frontend-playbook.md`
4. `docs/ai/project-brief.md`
5. `docs/ai/memory-bank/README.md`
6. The relevant memory-bank files for the task

Treat this as mandatory for work that changes routes, layouts, UI patterns, architecture, API assumptions, or shared components.

## Ownership And Task Boundaries

Use one owner per task slice.
Choose the owner before implementation starts.

Good ownership splits:

- one owner for landing and marketing UI
- one owner for dashboard shell
- one owner for backend integration strategy
- one owner for reusable UI primitives

Bad ownership splits:

- two people editing the same page at the same time
- one person changing architecture while another still codes against old assumptions
- one agent changing shared styles while another is building new screens

Default rule: do not have Claude and Codex work in parallel on the same files unless one side is only reviewing.

## How To Use Claude And Codex

Use Claude and Codex as role-specialized helpers, not as separate sources of truth.

- use Codex for repo-grounded implementation, file-aware edits, and Vercel skill-guided frontend work
- use Claude for drafting, summarizing, reviewing, and thinking through product or UI direction
- both must follow `AGENTS.md`, this document, the frontend playbook, and the memory bank

Starting points:

- Claude starts from `CLAUDE.md`
- Codex starts from `AGENTS.md` and `docs/ai/frontend-playbook.md`

## During Implementation

If the task changes durable project context, update the memory bank in the same task.

Durable changes include:

- architecture or rendering strategy
- shared UI rules
- component conventions
- backend or API assumptions
- product scope
- major blockers or handoff notes

Do not update the memory bank for tiny fixes, wording edits, or minor styling polish.

If a task may change architecture, API shape, or shared UI rules, pause and document the decision before more implementation piles on top of it.

## Before Closing A Task

Run a review pass against the frontend playbook and confirm:

- the owner was clear
- the task boundary stayed clear
- the result is still aligned with Next.js 16
- Server and Client Component boundaries are intentional
- no undocumented convention was introduced
- the UI still fits the current visual direction
- any changed backend assumptions were written into the memory bank
- no stale documentation was knowingly left behind

## Non-Negotiable Rules

- `AGENTS.md` is the canonical rules file.
- If docs and implementation disagree, trust repo reality first, then fix the docs.
- Do not let chat history become the only place where important decisions live.
- Do not document planned work as if it already exists.
- Keep repo-facing AI docs in English for stability across tools.
- Record dated decisions and open questions in `docs/ai/memory-bank/05-decisions-and-open-questions.md`.
