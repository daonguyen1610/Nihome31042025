## Nihome Web

This is the Next.js frontend for the Nihome platform.

## Development

Install dependencies if they are not present yet:

```bash
npm install
```

Then run the app:

```bash
npm run dev
```

Open `http://localhost:3000`.

## Current status

- The app is a Next.js 16 App Router project.
- The landing page lives in `app/page.tsx` and currently performs a client-side fetch to `/api/system/health`.
- `next.config.ts` currently only sets `output: "export"`.
- There is no committed `.env.example` in this repo.
- There is no configured API rewrite or route handler in the current tree, so backend integration is not fully wired yet.

## Main files

- `app/page.tsx`: landing page and backend status check UI
- `app/layout.tsx`: app metadata and root layout
- `app/globals.css`: shared visual tokens and landing page styles
- `next.config.ts`: current Next.js config

## Shared AI docs

Repo-level AI collaboration rules live here:

- `AGENTS.md`: canonical rules for Codex and other agents
- `CLAUDE.md`: Claude entrypoint into the same shared rules
- `docs/ai/working-procedure.md`: day-to-day operating procedure for teammates and agents
- `docs/ai/frontend-playbook.md`: frontend skill-routing and implementation conventions
- `docs/ai/memory-bank/`: durable project memory, decisions, and handoffs
