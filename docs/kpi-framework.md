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
| Tendering | Tender estimate accuracy | 30% | Approved tender estimate versus final execution BOQ | Implemented |
| Design | On-time drawing delivery | 40% | Design schedule task assignee, planned/actual end | Implemented |
| Design | First-pass drawing approval | 30% | Drawing owner, approval/release and revision count | Implemented |
| Design | Site-reported design errors | 30% | Verified Punch Items with confirmed Design root cause | Implemented; target required |
| Site | Construction progress variance | 30% | Construction task owner and planned/actual duration | Implemented; target required |
| Site | Material waste rate | 30% | Excess posted warehouse issue quantity versus final BOQ allowance | Implemented; target required |
| Site | First-pass acceptance | 20% | Acceptance creator, approval and revision count | Implemented |
| Site | HSE violation count | 20% | Confirmed HSE violations attributed to the responsible site user | Implemented; target required |
| Procurement | Purchase cost optimization | 40% | Final BOQ unit ceiling versus signed downstream Contract line cost | Implemented |
| Procurement | Delivery time | 30% | Approved Material Request to final posted Warehouse Receipt | Implemented; target required |
| Procurement | Vendor quality rating | 30% | PM-approved per-contract Vendor Rating | Implemented |
| Project Accounting | On-time receivable collection | 40% | Due-month upstream milestones attributed to an accountant | Implemented |
| Project Accounting | Partner payment processing time | 30% | Validated Payment Request to paid timestamp | Implemented; default target 72 hours |
| Project Accounting | Financial data accuracy | 30% | Approved post-close Accounting Corrections | Implemented; default target 1 correction |

## Source-workflow delivery

The detailed data contracts, lifecycle proposals, permission boundaries, and
decision gates are defined in [KPI Source Workflow Design](kpi-source-workflows.md).

The source workflows are delivered in migrations
`AddPunchRootCauseAttribution`, `AddReceivableAccountability`,
`AddHseViolations`, `AddProcurementControlChain`, and
`AddFinanceControlWorkflows`. Numeric targets without an approved value remain
administrator configuration; source availability and target configuration are
reported separately.

Each source workflow requires its own authorization, audit, migration,
multilingual UI, integration tests, and unchanged-state checks before the
dependent KPI definition may be treated as available.