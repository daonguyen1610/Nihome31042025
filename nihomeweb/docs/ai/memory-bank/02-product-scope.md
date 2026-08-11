# Product Scope

Last reviewed: 2026-08-10

## Product Intent

`nihomeweb/` is the active Vite + React frontend for the NICON / Nihome platform.
It supports both a public corporate website and an internal operational platform for a design-and-build business.

## Intended Surface Areas

Implemented frontend surface areas include:

- public corporate pages for profile, services, projects, news, activities, clients, recruitment, and contact
- authentication entry points
- admin dashboard, content, recruitment, contacts, and company-logo workflows
- users, dynamic RBAC, notifications, audit, settings, translations, master data, workflows, and process documents
- CRM for leads, customers, opportunities, quotes, capability documents, tenders, surveys, contracts, appendices, attachments, and variation orders
- design projects, concepts, basic design, shop drawings, revisions, IFC tracking, and permit checklists
- operational procurement vendor profiles, scoped ownership, private documents, project evaluations, audit history, and filtered export
- construction tasks/Gantt, site diaries, punch lists, partial acceptance, as-built records, and project handover

The detailed route and API catalogs live in the user and developer guides. Partial areas are called out explicitly rather than treating the whole operational platform as aspirational.

## In Scope Right Now

- Vite/React production application inside `nihomeweb/`
- shared AI workflow and durable repo memory
- NICON / Nihome public site and admin shell already present in the source tree
- the current React Router route surface declared in `src/App.tsx`
- backend JWT/refresh authentication, permission-aware routing, and centralized API services
- the implemented public, CRM, design, permitting, construction, content, recruitment, and administration modules listed above
- route-specific localStorage state only for the editable language list on `/admin/master-data`

## Not Yet Implemented

- migration of `/admin/master-data` language configuration to backend persistence
- survey media management
- procurement BOQ, material-request, and warehouse operations beyond the implemented vendor module
- cash flow, profit-and-loss, Google Drive integration, and broad cross-module analytics
- finalized server-state strategy across future modules beyond the existing focused service/page patterns
- migration back to Next.js or Materialize/full-template admin architecture

## Scope Discipline

- Do not generalize route-specific localStorage behavior as the platform architecture.
- Do not present aspirational product areas as implemented functionality.
- When a new screen family becomes real, update this file and the current-state file together.
- If the team narrows or expands the product direction, record the decision in `05-decisions-and-open-questions.md`.
