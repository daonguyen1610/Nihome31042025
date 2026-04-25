# Product Scope

Last reviewed: 2026-04-25

## Product Intent

`nihomeweb/` is the active Vite + React frontend for the NICON / Nihome platform.
It is intended to support both a public corporate website and an internal admin/content portal for a design-and-build business.

## Intended Surface Areas

Planned frontend surface areas include:

- public corporate pages for profile, services, projects, news, activities, clients, recruitment, and contact
- authentication entry points
- admin dashboard and content-management workflows
- admin settings and system-operation screens
- future API-backed modules for content, customers, projects, recruitment, and site configuration

These are product intent categories, not a promise that every route already exists with production behavior.

## In Scope Right Now

- Vite/Lovable baseline adoption inside `nihomeweb/`
- shared AI workflow and durable repo memory
- NICON / Nihome public site and admin shell already present in the source tree
- the current React Router route surface declared in `src/App.tsx`
- localStorage-backed demo auth, admin content, and settings behavior as explicit placeholders
- legacy Next.js and Materialize code retained under `legacy/` as reference-only material

## Not Yet Implemented

- production-ready auth flows
- finalized API integration contract
- production persistence for admin content/settings
- backend-driven route protection
- finalized server-state strategy beyond the existing TanStack Query dependency
- migration back to Next.js or Materialize/full-template admin architecture

## Scope Discipline

- Do not present localStorage demo behavior as production functionality.
- Do not present aspirational product areas as implemented functionality.
- When a new screen family becomes real, update this file and the current-state file together.
- If the team narrows or expands the product direction, record the decision in `05-decisions-and-open-questions.md`.
