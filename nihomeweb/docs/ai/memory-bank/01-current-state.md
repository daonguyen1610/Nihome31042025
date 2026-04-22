# Current State

Last reviewed: 2026-04-23

## Stack

- Next.js `13.0.0`
- React `18.2.0`
- React DOM `18.2.0`
- TypeScript `4.8.x`
- MUI `5.x`
- Emotion `11.x`
- ESLint `8.x`

## Current App Structure

The active frontend now includes:

- `src/pages/_app.tsx`
- `src/pages/_document.tsx`
- `src/pages/index.tsx`
- `src/pages/projects/index.tsx`
- `src/pages/notifications/index.tsx`
- `src/pages/login/index.tsx`
- `src/pages/register/index.tsx`
- `src/pages/forgot-password/index.tsx`
- `src/pages/admin/index.tsx`
- `src/pages/admin/dashboard/index.tsx`
- `src/layouts/UserLayout.tsx`
- `src/configs/themeConfig.ts`
- `styles/globals.css`

The retired Next 16 App Router shell has been moved under `legacy/next16-shell/` and is no longer the active frontend architecture.

## Current Portal Shell Behavior

- `/` is the MUI-based workspace overview page.
- `/projects` and `/notifications` are Nihome placeholder pages on top of the starter-kit layout system.
- `/admin` redirects to `/admin/dashboard`.
- `/login`, `/register`, and `/forgot-password` are starter-kit auth entry screens adapted from the template baseline for Nihome.
- Pages are protected by the mock auth guard unless they are explicitly marked as guest routes.

## Current Config Reality

- `next.config.js` follows the starter-kit baseline.
- The active route surface is Pages Router under `src/pages/`.
- Mock JWT auth runs through `src/@fake-db/auth/jwt.ts`.
- The current auth entry screens selectively reuse full-template auth layouts without importing the larger template wholesale.
- There is still no committed `.env.example`; the mock auth now has safe local fallbacks so the baseline can boot without local env setup.
- The repo includes repo-local AI docs, a project brief, and a memory bank under `docs/ai/`.

## Current Gaps

- No real auth provider or backend session model is implemented yet.
- No API client or server-state strategy is established yet.
- No deep business modules such as CRM, design, construction, procurement, or finance are implemented yet.
- No frontend test suite is committed yet.
- The larger full template is not imported yet.

## Agent Notes

- Do not assume the retired App Router shell is still active; the repo currently ships the starter-kit baseline.
- In fresh environments, `node_modules/` may not be present yet, so local Next.js docs may require dependency installation before they can be consulted.
