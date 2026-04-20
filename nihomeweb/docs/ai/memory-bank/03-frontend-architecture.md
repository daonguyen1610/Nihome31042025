# Frontend Architecture

Last reviewed: 2026-04-20

## Core Direction

- Use the Next.js App Router.
- Treat Server Components as the default.
- Add Client Components only for interactivity, browser APIs, or hooks that require them.
- Keep architecture choices compatible with Next.js 16 behavior.

## Route and Component Boundaries

- Route files should stay focused on page, layout, loading, error, and metadata concerns.
- Push reusable UI into shared components only after repetition becomes real.
- Avoid placing backend-specific assumptions directly inside presentational components.
- If multiple routes need the same backend access pattern, centralize that pattern instead of copying fetch logic.

## Data Fetching Defaults

- Match the data-fetching approach to the rendering model instead of defaulting to client-side `useEffect`.
- Prefer server-side data access when the route can benefit from it and the deployment model supports it.
- Use client-side fetching for interactive or browser-owned state when that is the right fit.
- If client-side server state becomes widespread, standardize it rather than creating many custom fetch effects.

## Environment and Integration Rules

- Treat environment usage as an explicit contract, not an implicit assumption.
- Do not document env variables that are not actually committed or configured.
- Do not hardcode backend base URLs in presentational UI.
- If the frontend depends on backend proxying, rewrites, or route handlers, commit that configuration and document it in the same task.

## Static Export Caveat

The repo currently sets `output: "export"` in `next.config.ts`.
That choice limits or complicates features that expect a Next.js server runtime, such as route handlers, runtime proxy behavior, and some server-driven integration patterns.

Until the team deliberately revisits output strategy:

- do not assume server runtime features are available
- do not document proxy behavior that is not committed
- flag any task that depends on runtime-only Next.js features as an architecture decision, not a routine implementation detail

## Documentation Rule

If architecture changes in a durable way, update this file and `05-decisions-and-open-questions.md` in the same task.
