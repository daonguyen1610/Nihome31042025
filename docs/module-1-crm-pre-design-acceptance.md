# Module 1 CRM and Pre-Design Acceptance Review

## Verdict

**Approved by Business Analysis and conditionally approved by Functional QA —
the complete Module 1 contract is implemented across the API, Admin UI,
migrations, permissions, configurable master data, and customer-fillable CSV
workflows. No open Blocker, High, or Medium functional defect remains.**

The system owns the import templates and validation contract; customer data can
be loaded without waiting for an external template definition.

Production release still requires the operational checks listed below for the
supported physical mobile/browser matrix and live Google Drive credentials.

## Customer Contract

Module 1 must provide:

1. Lead contact, source, segment, and consultation history.
2. A controlled pipeline: Approach → Survey → Quotation/Tender → Negotiation →
   Contract signed.
3. Direct quotation from material norms and a square-metre rate, with a
   preliminary quote output.
4. Tender preparation checklist, bid estimate, deadline, and result.
5. Mobile survey capture for media, coordinates, right-of-way, elevation, and
   infrastructure, synchronized to the project's `01_Khao_sat` Drive folder.

## Acceptance Matrix

| ID | Requirement | Status | Source evidence | Required follow-up |
|---|---|---|---|---|
| M1-01 | Lead contact information | **Pass** | Lead model, requests, service, and Admin Lead forms store and display contact data. | Retain validation parity between API and UI when fields change. |
| M1-02 | Lead source | **Pass** | Lead source uses managed master data and is available in Lead filters/forms. | Confirm the production source list with Sales operations. |
| M1-03 | Lead segment | **Pass** | Lead writes require one active `lead_segment` code; Admin list, forms, detail, and filters use backend master data. Migration backfills `unclassified`. | Sales Operations may revise the seeded taxonomy through Master Data. |
| M1-04 | Lead consultation history | **Pass** | Lead activities provide dated consultation/history entries. | Confirm whether immutable audit or reminder requirements extend beyond current activities. |
| M1-05 | Required five-step sales pipeline | **Pass** | Service and UI enforce the five stages. Deployment converts historical rows directly to current semantics: contract-backed Won is preserved, unsupported Won becomes Negotiation, and Lost metadata is normalized. | Review the migration report before production rollout. |
| M1-06 | Prevent skipped and backward pipeline movement | **Pass in change set** | Unit and HTTP integration tests reject skipped/backward transitions and preserve stored state. | Retain these tests for every lifecycle change. |
| M1-07 | Contract signed completion gate | **Pass** | Completion requires a qualifying same-customer Contract. Contract update, transition, unlink, and delete cannot remove the final qualifying evidence from a Won Opportunity. | Retain closure-invariant regression tests. |
| M1-08 | Lost opportunity branch | **Pass** | An open opportunity can move to Lost only with a reason code and note; probability becomes zero. | Confirm whether Lost may be reopened by a privileged role. Current behavior is terminal. |
| M1-09 | Square-metre quotation calculation | **Pass** | Unit-cost quotes calculate area × effective catalog rate, discount, VAT, and total. Permission-gated overrides require a Vietnamese reason and retain provenance. | None. |
| M1-10 | Material-norm-derived quotation rate | **Pass** | Versioned catalogs support strict atomic CSV import, effective periods, approval, immutable approved revisions, and `NormPerSqm × UnitRate × (1 + WastePercent/100)`. Quotes retain revision provenance and snapshots. | None. |
| M1-11 | Preliminary quotation output | **Pass** | Protected PDF export supports vi/en/zh/ja, Unicode, localized preliminary markers, customer/opportunity data, rate provenance, and totals. | Final branding can be changed through centralized translations. |
| M1-12 | Tender preparation checklist | **Pass** | Submission requires a complete checklist and an approved estimate; terminal results lock mutable preparation data. | None. |
| M1-13 | Tender deadline and result | **Pass** | Preparing → Submitted requires readiness; Won/Lost require Submitted; Won enforces same-customer Opportunity; cancellation is allowed only from open states. | Deadline escalation remains outside this contract. |
| M1-14 | Tender bid estimate | **Pass** | Downloadable CSV creates atomic, hashed revisions with cost, bid, VAT, totals, and Draft → Submitted → Approved/Rejected governance. | None. |
| M1-15 | Mobile survey media and coordinates | **Partial** | Responsive Survey UI supports private photo/video/file upload, capture notes/time, and latitude/longitude. Survey access is limited to the surveyor, Survey creator, Operational Project manager/creator, or explicitly elevated `view.all`/`manage.all` users; inaccessible records and subresources return `404`. | Run device acceptance on the supported mobile/browser matrix. |
| M1-16 | Structured right-of-way, elevation, and infrastructure | **Pass** | Responsive condition editors and atomic CSV/JSON replacement use stable categories, controlled statuses/units, required access-width/elevation rows, managed infrastructure types, audit fields, and localized PDF output. | Row-level historical versions are outside this contract. |
| M1-17 | Automatic project `01_Khao_sat` Drive sync | **Pass** | Every Survey requires an Operational Project; consistency is enforced and media routes through the project Survey category. Migration backfills from linked Opportunities and aborts atomically if any Survey cannot be routed. | Preflight production data for unmappable Surveys and verify deployment Drive credentials. |

## Compatibility Decision

The Opportunity enum names and numeric values remain unchanged to avoid
breaking persisted data and API clients. The customer-facing mapping is:

| Stored/API value | Customer stage |
|---|---|
| `Prospecting` | Approach / Tiếp cận |
| `Qualification` | Survey / Khảo sát |
| `Proposal` | Quotation/Tender / Báo giá/Đấu thầu |
| `Negotiation` | Negotiation / Thương thảo |
| `Won` | Contract signed / Ký hợp đồng |
| `Lost` | Lost / Thất bại (alternate terminal branch) |

## Verification

- Backend builds: **passed**; format verification is rerun before release.
- Full backend unit suite in the production-equivalent font image: **1,643 passed**.
- Full HTTP integration suite: **1,368 passed**.
- Functional QA focused regression: **394 unit and 185 HTTP integration tests passed**.
- Survey anti-enumeration integration coverage includes list, detail, timeline,
   PDF, JSON/CSV conditions, media upload/content/delete/retry, checklist,
   sync log, update, and delete. Explicit surveyor assignment grants scoped
   access without granting global access.
- Customer CSV imports use strict UTF-8 and ordered headers, bounded byte/row
   reads, SQL-compatible decimal precision, atomic replacement, and persisted
   importer/provenance fields appropriate to each workflow.
- Frontend lint and production build: **passed**.
- Focused Module 1 Playwright acceptance remains blocked by three historical
   Surveys in the local development database that have no deterministic project
   route; no relationship is inferred or invented.
- Migrations `20260901141836_CompleteModule1CrmPreDesign` and
   `20260901171502_FinalizeModule1NewOnly` passed isolated SQL Server fresh- and
   draft-schema conversion assertions. Permission migration
   `20260902120000_AddScopedSurveyPermissions` passed fresh and existing paths;
   Sales retained zero all-project grants. Guard migration
   `20260902123000_ValidateHistoricalSurveyProjectRouting` passed a fresh chain
   and rejected the existing three unresolved Surveys with SQL `51001` without
   recording the migration or changing the unresolved row count.
- A real application startup against a fresh SQL Server database completed all
   migrations and seeding; all five sample Surveys referenced valid Operational
   Projects.

## Assumptions and Operational Risks

1. Lead segmentation is single-select; administrators own taxonomy changes.
2. Tender estimates store both internal cost and submitted bid values in VND by
   default; each imported revision uses one VAT percentage.
3. Stable Opportunity enum names remain an API/storage contract, but no legacy
   runtime mode remains after migration.
4. Any historical Survey that cannot be deterministically mapped blocks the
   migration; the data owner must assign its linked Opportunity/project before
   retrying deployment.
5. Live Drive delivery still depends on valid deployment OAuth credentials and
   write access to the configured project folder.
6. Project Managers are intentionally Survey read-only unless they also have a
   manage permission or are assigned another role with that capability.
