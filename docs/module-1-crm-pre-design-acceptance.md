# Module 1 CRM and Pre-Design Acceptance Review

## Verdict

**Blocked — the corrected Opportunity pipeline is implemented and covered at
service and HTTP layers, but the complete customer contract is not ready for
customer acceptance. Product definitions, missing data capabilities, legacy
stage reconciliation, and cross-module lifecycle invariants remain open.**

Passing tests in this review prove only the listed implemented behavior. They
do not prove customer segmentation, material-norm pricing, generated
preliminary quotations, tender bid estimates, or structured site-condition
capture.

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
| M1-03 | Lead segment | **Fail** | No Lead segment field, relation, filter, or report exists. | Product must define the taxonomy, single/multi-select behavior, ownership, lifecycle, and reporting contract. |
| M1-04 | Lead consultation history | **Pass** | Lead activities provide dated consultation/history entries. | Confirm whether immutable audit or reminder requirements extend beyond current activities. |
| M1-05 | Required five-step sales pipeline | **Partial** | Opportunity service enforces creation at Approach and exact sequential movement through Survey, Quotation/Tender, Negotiation, and Contract signed. UI labels are localized in Vietnamese, English, Chinese, and Japanese. Existing persisted stages have not been semantically reconciled. | Approve the legacy-data reconciliation strategy, deploy translations, and verify cached translations after release. |
| M1-06 | Prevent skipped and backward pipeline movement | **Pass in change set** | Unit and HTTP integration tests reject skipped/backward transitions and preserve stored state. | Retain these tests for every lifecycle change. |
| M1-07 | Contract signed completion gate | **Partial** | Transition requires a same-customer Contract linked to the Opportunity, a signed date, and status other than Draft or Cancelled. Terminal Opportunity edits are blocked. A later Contract update/delete can still invalidate the completion evidence. | Define and enforce the invariant across Contract update, status transition, unlink, and delete operations. Confirm whether approvals or mandatory documents are also required. |
| M1-08 | Lost opportunity branch | **Pass** | An open opportunity can move to Lost only with a reason code and note; probability becomes zero. | Confirm whether Lost may be reopened by a privileged role. Current behavior is terminal. |
| M1-09 | Square-metre quotation calculation | **Partial** | Unit-cost quotation calculates area × a user-entered square-metre rate. | Define whether and when manual override is allowed. |
| M1-10 | Material-norm-derived quotation rate | **Fail** | No authoritative material-norm/rate catalog or effective-dated pricing source is used. | Approve norm source, package applicability, effective dates, rate provenance, override authority, currency, and VAT rules. |
| M1-11 | Preliminary quotation output | **Unknown / not proven** | BOQ totals and workflow exist, but no generated preliminary-quote PDF endpoint or approved template was found. | Approve output format/template and acceptance criteria before implementation. |
| M1-12 | Tender preparation checklist | **Partial** | Tender checklist items support shared capability documents or direct upload and lock after terminal result. Tender result currently does not require checklist completion. | Confirm mandatory templates and result preconditions by tender type. |
| M1-13 | Tender deadline and result | **Partial** | Deadline and Won/Lost results are implemented; Won now rejects an Opportunity from another customer. Complete Preparing/Submitted/Cancelled transition behavior is not proven. | Define the transition matrix, timezone, overdue escalation, and cancellation policy. |
| M1-14 | Tender bid estimate | **Fail** | No bid-estimate amount, currency, version, or approval model exists. | Define internal cost vs submitted bid vs both, VAT, versions, approvals, and visibility. |
| M1-15 | Mobile survey media and coordinates | **Partial** | Responsive Survey UI supports private photo/video/file upload, capture notes/time, and latitude/longitude. | Run device acceptance on the supported mobile/browser matrix. |
| M1-16 | Structured right-of-way, elevation, and infrastructure | **Fail** | These site conditions are not represented as structured fields. | Define field types, units, required/optional rules, option lists, and edit history. |
| M1-17 | Automatic project `01_Khao_sat` Drive sync | **Partial** | Survey media stages to the project Survey category only when the Survey's linked Opportunity has an Operational Project. Unlinked surveys use a legacy root workflow. | Decide whether project linkage is mandatory before upload and approve migration/backfill behavior. |

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

## Validation Evidence

- Focused Opportunity and Tender service unit tests: **101 passed, 0 failed**.
- Focused Opportunity and Quote HTTP integration tests: **47 passed, 0 failed**.
- Focused Opportunity browser/deployment acceptance tests: **6 passed, 0 failed**.
- Frontend lint: **passed**.
- Frontend production build: **passed** with existing non-blocking bundle-size
  and Tailwind ambiguity warnings.
- No database migration is required for the pipeline correction.

The BA review was performed in a separate read-only agent context against the
source diff. It rejected customer readiness because passing tests cover only
the implemented subset and do not resolve the blockers below.

## Required Product Decisions

The following decisions are blockers and must be approved by the product or
business owner before implementation:

1. Lead segment taxonomy and cardinality.
2. Material-norm source, rate governance, overrides, currency, VAT, and quote
   output template.
3. Tender bid-estimate meaning, versions, approvals, and visibility.
4. Survey field definitions and units for right-of-way, elevation, and
   infrastructure.
5. Whether project linkage is mandatory before survey media upload, including
   migration and failure behavior.
6. Entry/exit evidence for Survey and Quotation/Tender pipeline stages, legacy
   stage reconciliation, and whether existing `Won` records require backfill.
7. Whether Contract updates/deletion may invalidate a Contract signed
   Opportunity, and the required recovery path if evidence is revoked.

Until those decisions are approved and implemented, Module 1 must not be
represented as fully customer-ready.
