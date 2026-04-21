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
- The app now uses route groups for `(auth)`, `(client)`, and `(admin)`.
- The client home at `/` keeps the branded Nihome visual direction while acting as the Phase 1 portal shell.
- `next.config.ts` no longer uses `output: "export"`.
- There is no committed `.env.example` in this repo.
- There is no auth layer, API client, route protection, or server-state library yet.

## Main files

- `app/(client)/page.tsx`: branded client home shell
- `app/(admin)/admin/dashboard/page.tsx`: admin dashboard placeholder
- `app/(auth)/login/page.tsx`: auth placeholder shell
- `app/layout.tsx`: app metadata and root layout
- `app/globals.css`: shared visual tokens and landing page styles
- `next.config.ts`: current Next.js config

## Shared AI docs

Repo-level AI collaboration rules live here:

- `AGENTS.md`: canonical rules for Codex and other agents
- `CLAUDE.md`: Claude entrypoint into the same shared rules
- `docs/ai/working-procedure.md`: day-to-day operating procedure for teammates and agents
- `docs/ai/frontend-playbook.md`: frontend skill-routing and implementation conventions
- `docs/ai/project-brief.md`: current product and phase execution brief
- `docs/ai/memory-bank/`: durable project memory, decisions, and handoffs
