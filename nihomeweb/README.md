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

- The app now follows the imported starter-kit baseline: Next.js 13 Pages Router, MUI 5, and Emotion.
- Core routes live under `src/pages/`.
- The official round-1 route surface is `/`, `/projects`, `/notifications`, `/login`, `/forgot-password`, `/admin`, and `/admin/dashboard`.
- Mock JWT auth is still in place as a placeholder until a real auth strategy is chosen.
- The larger `full-template` is intentionally deferred; future screens can adopt its compatible components selectively.

## Main files

- `src/pages/index.tsx`: Nihome workspace overview
- `src/pages/admin/dashboard/index.tsx`: admin dashboard baseline
- `src/pages/login/index.tsx`: starter-kit auth screen, Nihome-personalized
- `src/layouts/UserLayout.tsx`: shared workspace layout
- `styles/globals.css`: global styles imported by `_app.tsx`
- `next.config.js`: current Next.js config

## Shared AI docs

Repo-level AI collaboration rules live here:

- `AGENTS.md`: canonical rules for Codex and other agents
- `CLAUDE.md`: Claude entrypoint into the same shared rules
- `docs/ai/working-procedure.md`: day-to-day operating procedure for teammates and agents
- `docs/ai/frontend-playbook.md`: frontend skill-routing and implementation conventions
- `docs/ai/project-brief.md`: current product and phase execution brief
- `docs/ai/memory-bank/`: durable project memory, decisions, and handoffs
