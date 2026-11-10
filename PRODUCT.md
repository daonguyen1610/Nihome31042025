# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Primary users are Nicon's construction project managers and technical site leads. They run a project day to day in the admin workspace: staffing the project team, assigning and commenting on work, tracking design and procurement progress, reviewing cost and contract state, and closing the project through partial acceptance, as-built records, punch lists, and handover.

The same workspace also carries the departments those managers coordinate with (CRM, design, procurement, finance, legal, site command, content, administration), but the project manager is the actor whose job the product exists to support. In construction this is a deliberate two-sided model: Phòng CM (the construction management office) plans, supervises, and manages records across its project portfolio, while BCH (site command, QA/QC, QS) runs the assigned site and records the field evidence. Each side works through its own permission-scoped portals in the same workspace.

## Product Purpose

Nicon is the internal operations platform of a Vietnamese design-and-build construction company. It holds the company's commercial and delivery work — customers, opportunities, quotes, contracts, design projects, procurement, construction records, and handover — in one system rather than in separate department tools and spreadsheets.

Success means a project can be traced from first opportunity to handover without any department re-entering data that another department already recorded.

## Positioning

The differentiator is one continuous chain: CRM → quote → contract → design → procurement → construction → acceptance and handover, with shared records and department-scoped access at every step. A generic project-management tool cannot truthfully copy it, because the value comes from the links between the commercial record, the design record, the purchase record, and the site record — not from task management alone.

## Operating Context

- Vietnamese construction practice: signed contracts, design projects with disciplines and modules, work packages, BOQ and material rates, RFQs and vendor price comparison, financial review and approval, legal and permitting, site diaries, partial acceptance, as-built dossiers, punch lists, and handover.
- Work is organized by operating project, contract, and internal department work; team assignment follows a RACI-style role model.
- Department membership drives what each user sees and can act on; Super Admin holds full access.
- Construction is two-sided on purpose. Phòng CM prepares the project after the upstream D&B/construction contract is signed (schedule baseline, BCH organization, resource plan, budget baseline), supervises multiple projects, and performs independent level-2 review; CM may also create or correct a record on BCH's behalf. BCH executes on site — daily diary, tasks, punch items, QA/QC dossiers, HSE violations, resource requests, QS batches — and supplies the field evidence. Execution and independent approval never collapse into one actor.
- Construction records stay distinct and linked: QA/QC, HSE, punch list, daily diary, Upstream QS, Downstream QS, acceptance, as-built, and handover each carry project/WBS links, lifecycle, responsible actor, evidence, timestamps, and concurrency control. Approval requires a different person from the author.
- The three BCH portals (Operations, QA-QC, QS) are designed mobile-first for the site; the five Phòng CM areas (Schedule, Commercial, Quality, Safety, Contracts) are designed desktop-first for portfolio comparison, exception review, and independent approval.
- Project documents are synchronized to Google Drive through OAuth; the platform stores document identity, placement, and sync state rather than file bodies.
- The same codebase also serves the public Nicon website and its content management.

## Capabilities and Constraints

- React 18 + TypeScript frontend; ASP.NET Core 8 + EF Core 8 + SQL Server 2022 backend; Docker Compose is the supported development environment.
- Every UI string and translated content type must exist in Vietnamese, English, Chinese, and Japanese. New translatable content types must be registered in the content-translation feature, and the translation key checker must pass.
- Authorization is permission-based and scoped to departments; every user-facing action maps to a permission and a visible data scope.
- Separation of duties is enforced server-side: an author cannot approve the same record, and UI hiding alone is not sufficient.
- Seed data must be meaningful and reflect real Nicon business flows, because seeded records are how the team and reviewers read the system.
- Google Drive credentials and tokens are encrypted at rest; secrets never appear in the repository, logs, or tickets.
- Undecided: quantitative targets (project volume, adoption, cycle-time) are not established in the repository or the product interview.

## Brand Commitments

The product name is Nicon (Nicon — Partner of Development). The admin workspace and public site share that identity.

## Evidence on Hand

- Seeded demonstration records cover the full flow: operating projects (PJ-SAMPLE-*), quotes (QT-SAMPLE-*), work packages (WP-DEMO-*), RFQs (RFQ-SAMPLE-*), and design projects (DP-SAMPLE-*) with real Vietnamese construction terminology.
- Repository documentation: `AGENTS.md`, `CLAUDE.md`, `README.md`, and `NICON_BUSINESS_REQUIREMENTS_CODE_REVIEW_2026-10-16.md`.
- No real customer testimonials, press, benchmarks, or public case studies are present; future work must not fabricate them.

## Product Principles

1. One record, one chain — data is entered once and referenced by every later stage.
2. The department is the default lens — people see the work they are accountable for.
3. Every visible word speaks four languages — no screen ships with a single-language string.
4. Seed data tells the real business story — demonstration records must be plausible Nicon work.
5. Documents keep their identity in Nicon and their bytes in Drive — placement and sync state are product data.
