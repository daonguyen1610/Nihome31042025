# Current State

Last reviewed: 2026-04-20

## Stack

- Next.js `16.2.3`
- React `19.2.4`
- React DOM `19.2.4`
- TypeScript `^5`
- Tailwind CSS `^4`
- ESLint `^9`

## Current App Structure

The committed `app/` tree is currently minimal:

- `app/layout.tsx`
- `app/page.tsx`
- `app/globals.css`
- `app/favicon.ico`

There are no committed route groups, no nested pages, no route handlers, and no shared component library in the current tree.

## Current Landing Page Behavior

- `app/page.tsx` is a client component.
- It fetches `/api/system/health` in `useEffect`.
- The page renders a branded landing screen plus a backend status panel.
- The UI currently uses custom classes defined in `app/globals.css`.
- Styling direction is warm, premium, and editorial rather than default starter styling.

## Current Config Reality

- `next.config.ts` currently sets only `output: "export"`.
- No rewrite, proxy, or route handler is committed for `/api/system/health`.
- No `.env.example` is committed in this repo.
- `README.md` previously described backend proxy behavior that does not exist in the current config; this mismatch was corrected when the shared AI docs were set up.

## Current Gaps

- No dashboard, auth, tenant, apartment, or billing routes are implemented yet.
- No shared data layer or server-state strategy is established yet.
- No reusable design-system primitives are committed yet.
- No frontend test suite is committed yet.
- No repo-local memory bank existed before 2026-04-20.

## Agent Notes

- Do not assume older Next.js examples apply directly; this repo explicitly targets Next.js 16.
- In fresh environments, `node_modules/` may not be present yet, so local Next.js docs may require dependency installation before they can be consulted.
