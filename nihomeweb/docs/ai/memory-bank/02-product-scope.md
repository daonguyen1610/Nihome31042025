# Product Scope

Last reviewed: 2026-04-21

## Product Intent

`nihomeweb/` is the Next.js frontend for the Nihome platform.
It is intended to support both an internal admin portal and a client-facing portal for a design-and-build business.

## Intended Surface Areas

Planned frontend surface areas include:

- client portal home, projects, and notifications
- admin portal shell and dashboard views
- authentication flows
- future design-and-build business modules such as CRM, design, construction, procurement, and finance

These are product intent categories, not a promise that the routes already exist.

## In Scope Right Now

- Phase 1 shell work
- shared AI workflow and durable repo memory
- route groups, layouts, placeholder pages, and a small shared component layer
- preserving the branded client-home direction while introducing the portal skeleton

## Not Yet Implemented

- production-ready auth flows
- finalized API integration contract
- state-management strategy
- deep feature modules beyond the admin dashboard shell and client placeholders

## Scope Discipline

- Do not present aspirational product areas as implemented functionality.
- When a new screen family becomes real, update this file and the current-state file together.
- If the team narrows or expands the product direction, record the decision in `05-decisions-and-open-questions.md`.
