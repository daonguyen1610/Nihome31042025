# KPI Framework

## Scope

The KPI platform implements the approved NICON framework from the NIH-447
customer attachment and `docs/Nicon_BreakTask_v1.xlsx`. It calculates only
metrics backed by structured source events. A missing source workflow is
reported as `MissingData`; it is never converted to a zero score.

## Platform contract

- One period exists per employee, year, and month.
- Period boundaries use `Asia/Ho_Chi_Minh` and are stored as UTC.
- Active definition weights cannot exceed 100% for a KPI position.
- A period can be locked only when every active metric is `Available` and the
  snapshot weights equal 100%.
- Snapshots freeze the definition code, version, weight, target, scoring
  direction, threshold, result, and source evidence.
- A locked period is immutable and cannot be recalculated.
- Automatic calculation runs daily at 01:00 Vietnam time. Authorized users may
  also calculate manually.
- A configured low-score threshold produces at most one notification per
  employee and period.
- `analytics.kpi.view` is own-scope. `view.all`, `manage`, and `export` grant
  cross-user viewing, definition/period management, and export respectively.

## Metric traceability

| Position | Metric | Weight | Source | Status |
| --- | --- | ---: | --- | --- |
| Sales | Lead-to-contract conversion | 40% | Lead owner, creation and conversion events | Implemented |
| Sales | New signed contract revenue | 40% | Upstream customer Contract owner, signed date and value | Implemented; target required |
| Sales | First customer interaction time | 20% | Lead creation and first non-note activity | Implemented; target required |
| Tendering | Tender win rate | 40% | Tender preparer, close date and result | Implemented |
| Tendering | On-time tender preparation | 30% | Tender checklist owner/deadline/completion | Implemented |
| Tendering | Tender estimate accuracy | 30% | Approved tender BOQ versus final actual BOQ | Blocked: no final actual BOQ source |
| Design | On-time drawing delivery | 40% | Design schedule task assignee, planned/actual end | Implemented |
| Design | First-pass drawing approval | 30% | Drawing owner, approval/release and revision count | Implemented |
| Design | Site-reported design errors | 30% | Design-attributed site defects | Blocked: Punch Item has no error-source classification |
| Site | Construction progress variance | 30% | Construction task owner and planned/actual duration | Implemented; target required |
| Site | Material waste rate | 30% | BOQ allowance versus warehouse issue quantity | Blocked: no material ledger or issue workflow |
| Site | First-pass acceptance | 20% | Acceptance creator, approval and revision count | Implemented |
| Site | HSE violation count | 20% | Structured HSE violation events | Blocked: no HSE violation entity |
| Procurement | Purchase cost optimization | 40% | BOQ unit allowance versus purchased unit price | Blocked: no PO/contract line-item source |
| Procurement | Delivery time | 30% | Approved material request to warehouse receipt | Blocked: no MR or warehouse receipt workflow |
| Procurement | Vendor quality rating | 30% | Per-project vendor scorecard | Blocked: no Vendor Rating workflow |
| Project Accounting | On-time receivable collection | 40% | Payment milestone due and actual paid date | Blocked: no accountant attribution on receipt event |
| Project Accounting | Partner payment processing time | 30% | Invoice receipt to approved disbursement | Blocked: no Payment Request workflow |
| Project Accounting | Financial data accuracy | 30% | Post-close accounting corrections | Blocked: no Accounting Correction workflow |

## Missing-source implementation order

The detailed data contracts, lifecycle proposals, permission boundaries, and
decision gates are defined in [KPI Source Workflow Design](kpi-source-workflows.md).

1. Approve and add the Procurement position mapping.
2. Add Punch Item error-source classification and accountant attribution to
  existing receivable milestones.
3. Add structured HSE violation events.
4. Add approved execution BOQ revisions.
5. Add downstream contract lines, Material Requests, warehouse receipts, and
  warehouse issues linked to the execution BOQ.
6. Add per-project Vendor Rating with quality, schedule, cost, and HSE scores.
7. Add Payment Request approval and payment evidence.
8. Add Accounting Period close and Accounting Correction workflows.

Each source workflow requires its own authorization, audit, migration,
multilingual UI, integration tests, and unchanged-state checks before the
dependent KPI definition may be treated as available.