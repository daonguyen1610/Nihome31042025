# Memory Bank

This directory stores durable project memory for the `nihomeweb/` frontend.
It exists so Claude, Codex, and human collaborators can share stable context without depending on chat history.

## When To Read It

Read the memory bank before any non-trivial task, especially when the work may affect:

- route structure, rendering, or data-fetching behavior
- shared UI conventions or reusable components
- product scope, screen ownership, or workflow design
- backend integration assumptions used by the frontend
- decisions that future agents will need to continue the work safely

For tiny edits such as spelling fixes or isolated wording changes, reading every file is optional.

## When To Update It

Update the memory bank in the same task when you make a durable change to:

- frontend architecture
- rendering or data-fetching conventions
- shared design system rules
- product scope or screen intent
- backend/API assumptions the frontend now depends on
- major blockers, open questions, or handoff context

Do not update the memory bank for every minor refactor, every class name tweak, or every single bug fix.

If a task may change architecture, API shape, or shared UI rules, pause and update the memory bank before more implementation piles on top of an undocumented assumption.

## What Not To Store Here

Do not put these in the memory bank:

- secrets, tokens, passwords, or private credentials
- long command logs or raw terminal transcripts
- temporary scratch notes that will expire immediately
- duplicate copies of code that already lives in the repo
- one-off chat summaries with no durable value

## File Map

- `01-current-state.md`: factual snapshot of the current frontend
- `02-product-scope.md`: intended product surface and current scope
- `03-frontend-architecture.md`: technical conventions and boundaries
- `04-ui-system.md`: visual system and component conventions
- `05-decisions-and-open-questions.md`: decisions, blockers, and handoffs

## Update Rules

- Prefer editing the existing section over creating redundant notes.
- Use short, direct prose that a fresh agent can scan quickly.
- Add dates for decisions and notable blockers.
- If repo reality conflicts with memory-bank text, repair the text before relying on it.
- Durable repo docs override chat memory when there is a conflict.
