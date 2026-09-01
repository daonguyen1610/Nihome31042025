# Tender estimate and lifecycle

## Estimate CSV

Download the UTF-8 template from `GET /api/tenders/{tenderId}/estimates/template`.
The required column order is:

`ItemCode,Description,Unit,Quantity,UnitCost,BidUnitPrice,VatPercent,Note`

Imports use `POST /api/tenders/{tenderId}/estimates/import` with multipart field `file`.
The file limit is 2 MB and 2,000 data rows. Quantity must be positive, prices must be nonnegative, item codes must be unique, and VAT must be identical on every row within 0–100. A valid import atomically creates the next Draft version and stores calculated cost/bid totals plus source filename, SHA-256, user, and time.

## Estimate lifecycle

- Draft → Submitted: `POST /api/tenders/{tenderId}/estimates/{revisionId}/submit`, permission `crm.tenders.manage`.
- Submitted → Approved: `POST /api/tenders/{tenderId}/estimates/{revisionId}/approve`, permission `crm.tenders.approve-estimate`.
- Submitted → Rejected: `POST /api/tenders/{tenderId}/estimates/{revisionId}/reject`, permission `crm.tenders.approve-estimate`; a rejection note is required.

List and detail endpoints require `crm.tenders.view`. Estimate mutations are allowed only while the tender is Preparing.

## Tender lifecycle

Use `POST /api/tenders/{tenderId}/transition` with `status`, optional `opportunityId`, `reasonCode`, and `note`. Existing mark-won and mark-lost endpoints remain compatibility aliases and enforce the same rules.

- Preparing → Submitted requires every checklist item to be Done or Submitted and at least one Approved estimate.
- Preparing or Submitted → Cancelled.
- Submitted → Won requires an opportunity belonging to the same customer.
- Submitted → Lost requires an active `opportunity_lost_reason` code.
- Won, Lost, and Cancelled are terminal and immutable.

Schema support is included in migration `20260901141836_CompleteModule1CrmPreDesign`.
