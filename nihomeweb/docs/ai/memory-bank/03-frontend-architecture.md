# Frontend Architecture

Last reviewed: 2026-04-21

## Core Direction

- Use the Next.js Pages Router.
- Keep the starter-kit layout, theme, and settings system as the active frontend baseline.
- Centralize app-level providers, guards, and theme wiring in `_app.tsx`.
- Keep architecture choices compatible with the installed Next.js version.
- Build by phase instead of scaffolding every future module at once.

## Route and Component Boundaries

- Route files live under `src/pages/` and should stay focused on page behavior, redirects, and page-level presentation.
- `_document.tsx` handles document markup and Emotion SSR setup.
- Shared layout behavior belongs in `src/layouts/` and `src/@core/layouts/`.
- Avoid placing backend-specific assumptions directly inside presentational components.
- If multiple routes need the same backend access pattern, centralize that pattern instead of copying fetch logic.

## Data Fetching Defaults

- Match the data-fetching approach to the rendering model instead of defaulting to client-side `useEffect`.
- The current baseline avoids real API calls and keeps placeholder pages static.
- Mock auth is allowed because it is an explicit baseline placeholder, not because it represents the future production architecture.
- Prefer simple page-level or guard-level behavior until the real API contract is decided.
- If client-side server state becomes widespread, standardize it rather than creating many custom fetch effects.

## Environment and Integration Rules

- Treat environment usage as an explicit contract, not an implicit assumption.
- Do not document env variables that are not actually committed or configured.
- Do not hardcode backend base URLs in presentational UI.
- If the frontend depends on backend proxying, rewrites, or route handlers, commit that configuration and document it in the same task.

## Runtime Direction

The app should be treated as a runtime Next.js application on top of the starter-kit baseline.
The current baseline intentionally favors immediate productivity over framework modernization.

Until those later phases are opened:

- do not document auth or proxy behavior that is not committed
- do not introduce real auth or API client layers before the team decides those interfaces
- do not reintroduce a second active App Router shell without a documented migration plan

## Documentation Rule

If architecture changes in a durable way, update this file and `05-decisions-and-open-questions.md` in the same task.
