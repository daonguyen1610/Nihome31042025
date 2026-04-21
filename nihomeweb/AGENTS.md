<!-- BEGIN:nextjs-agent-rules -->
# This is NOT the Next.js you know

This version has breaking changes — APIs, conventions, and file structure may all differ from your training data. Read the relevant guide in `node_modules/next/dist/docs/` before writing any code. Heed deprecation notices.
<!-- END:nextjs-agent-rules -->

# Shared Agent Contract

`AGENTS.md` is the primary source of truth for AI agent workflow in `nihomeweb/`.
If another repo instruction file disagrees with this one, follow `AGENTS.md` and then repair the stale document.

## Required Read Order

Before doing non-trivial work, read these in order:

1. `AGENTS.md`
2. `docs/ai/working-procedure.md`
3. `docs/ai/frontend-playbook.md`
4. `docs/ai/project-brief.md`
5. `docs/ai/memory-bank/README.md`
6. The memory-bank files relevant to the task

Examples of non-trivial work:

- creating or restructuring routes, layouts, or components
- changing frontend architecture or data-fetching patterns
- adding shared UI conventions or reusable primitives
- changing API assumptions, environment usage, or deployment behavior

Tiny edits such as typos or isolated wording fixes may skip the memory bank if they do not change durable project context.

## Standard Workflow

Use the repo docs as the operating system for collaboration, not chat memory.

For any non-trivial task:

1. read the shared rules and memory in the required order
2. define one owner and one clear task boundary
3. let the chosen person or agent work only within that boundary
4. update the memory bank if the task changes durable project context
5. review the result against `docs/ai/frontend-playbook.md` before considering it done

`docs/ai/working-procedure.md` is the practical day-to-day guide for humans and agents following this contract.
`docs/ai/project-brief.md` is the repo-local product and phase brief for execution work.

## Frontend Skill Routing

For Codex environments with Vercel skills available, `docs/ai/frontend-playbook.md` is required reading before frontend implementation.

Use these skills intentionally:

- `vercel:nextjs` for work touching `app/`, `next.config.*`, metadata, layouts, rendering, route handlers, or caching
- `vercel:react-best-practices` after multi-component TSX edits and for React code review
- `vercel:shadcn` when introducing or standardizing reusable UI primitives
- `vercel:swr` when client-side server-state handling becomes more than a simple one-off fetch
- `vercel:geist` only if the team explicitly standardizes typography around Geist

Claude does not share Codex's native skill system, so Claude should mirror the same workflow by reading the playbook and applying its rules directly.

## Memory Bank Rules

The shared memory bank lives in `docs/ai/memory-bank/`.
It is the durable source of truth for project context, conventions, decisions, and handoffs.
Do not rely on chat history as the only record of important project context.

Agents must read the memory bank before non-trivial work and must update it in the same task when they change:

- architecture or rendering strategy
- shared UI system rules or reusable component conventions
- product scope or screen ownership
- backend/API assumptions used by the frontend
- major blockers, handoff context, or decisions future agents will need

Do not update the memory bank for every small implementation detail or cosmetic code change.
Each durable decision should include a date and a short rationale.

## Shared Collaboration Rules

When multiple people or agents work in this repo:

- assign one owner per task slice
- define the task boundary before implementation starts
- avoid parallel edits in the same files unless one side is review-only
- do not silently replace an existing convention with a different one
- check the memory bank before introducing new patterns
- if you intentionally change a convention, update the memory bank in the same task
- prefer additive handoffs over hidden prompt-only context
- do not assume a document is correct if implementation proves otherwise

Good ownership examples:

- one owner for landing or marketing UI
- one owner for dashboard shell
- one owner for backend integration strategy
- one owner for reusable UI primitives

Bad ownership examples:

- two people editing the same page at the same time
- one person changing architecture while another still builds against the old assumptions
- one agent silently changing shared styles while another adds new screens

## Docs vs Reality

If repo docs conflict with implementation:

1. trust observed repo reality over stale prose
2. fix the stale doc when safe to do so
3. if the right fix is not yet clear, record the mismatch in the memory bank before proceeding on a major task

## Decision and Handoff Hygiene

Write durable context where the next agent can find it quickly:

- current-state facts belong in `docs/ai/memory-bank/01-current-state.md`
- product direction belongs in `docs/ai/memory-bank/02-product-scope.md`
- architecture conventions belong in `docs/ai/memory-bank/03-frontend-architecture.md`
- UI conventions belong in `docs/ai/memory-bank/04-ui-system.md`
- decisions, blockers, and open questions belong in `docs/ai/memory-bank/05-decisions-and-open-questions.md`

Use role-specialized helpers, not separate sources of truth:

- use Codex for repo-grounded implementation, file-aware edits, and Vercel skill-guided frontend work
- use Claude for drafting, summarizing, reviewing, and product or UI thinking
- both must follow the same repo docs instead of inventing parallel rule sets

## Done Criteria

A task is only considered done correctly when:

- the owner was clear
- the task boundary stayed clear during implementation
- durable context was written into the repo instead of left only in chat
- no stale documentation was knowingly left behind
- the result passes the review expectations in `docs/ai/frontend-playbook.md`
- Claude and Codex would reach the same understanding by reading the repo docs alone

Keep repo-facing rules and memory in English for consistency across Claude, Codex, and Vercel skill guidance.
