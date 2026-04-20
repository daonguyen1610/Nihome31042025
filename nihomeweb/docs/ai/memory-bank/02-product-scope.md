# Product Scope

Last reviewed: 2026-04-20

## Product Intent

`nihomeweb/` is intended to be the Next.js frontend for the Nihome platform.
It should eventually support a calm, premium property-operations experience for staff and related users.

## Intended Surface Areas

Planned frontend surface areas include:

- marketing or landing entry points
- staff-facing dashboard views
- authentication flows
- tenant-related records and workflows
- apartment or inventory management
- billing or payment-oriented screens

These are product intent categories, not a promise that the routes already exist.

## In Scope Right Now

- establishing shared agent workflow and durable repo memory
- preserving a coherent frontend direction while the app is still early-stage
- preparing for future App Router expansion under `app/`
- documenting the current gap between intended backend integration and actual committed config

## Not Yet Implemented

- production-ready auth flows
- dashboard information architecture
- concrete tenant management screens
- apartment management flows
- billing UX and payment handling
- finalized backend integration contract for the frontend

## Scope Discipline

- Do not present aspirational product areas as implemented functionality.
- When a new screen family becomes real, update this file and the current-state file together.
- If the team narrows or expands the product direction, record the decision in `05-decisions-and-open-questions.md`.
