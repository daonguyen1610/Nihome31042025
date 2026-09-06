# Project Operational Reports (NIH-461)

## Purpose and scope

Project reports are operational summaries bounded by `OperationalProjectId`.
They do not calculate employee KPI scores. The API returns only projects that
the caller can access through `IProjectAccessService`.

- `GET /api/reports/projects` returns a portfolio when `projectId` is omitted.
- `GET /api/reports/projects?projectId={id}` returns one accessible project and
  returns `404` when that project does not exist or is inaccessible.
- `GET /api/reports/projects/export?format=xlsx|pdf&language=vi|en|zh|ja` uses
  the same project and date filters, renders complete filtered results on the
  server, and records an audit event with the filters and project count.
- `from` and `to` are optional inclusive `DateOnly` values. A reversed range is
  rejected with `400`.

The response includes server-generated `generatedAtUtc` and `asOfUtc` values.
Drill-downs contain only relative frontend routes and query strings.

## Permissions

| Role | View | Export |
| --- | --- | --- |
| `SUPER_ADMIN`, `ADMIN` | Yes, through system-role wildcard defaults | Yes, through system-role wildcard defaults |
| `BGD`, `ACCOUNTANT` | Yes, explicitly seeded | Yes, explicitly seeded |
| `PM` | Yes, explicitly seeded and project-scoped | No |
| Other business roles | No default grant | No default grant |

`BGD` already has the broad `**.view` pattern, which covers
`reports.projects.view`; it also receives an explicit
`reports.projects.export` grant because export is deliberately separate.

## Available metrics

- Project identity and manager/customer context.
- Design weighted progress using `design-schedule-weighted-v1`. The value is
  unavailable with `DESIGN_BASELINE_INCOMPLETE` until all three phase and task
  baselines satisfy the existing weight rules.
- Construction task counts by exact persisted status, overdue count, and
  overdue rows. No averaged construction percentage is calculated.
- Acceptance counts by exact persisted status and revision count, plus overdue
  count. No acceptance ratio or first-pass rate is calculated.
- Permit overdue, due-soon, and expiring counts and rows. Due soon and expiring
  use a 30-day inclusive warning window from `asOfUtc`.
- Quote totals, contract base value, approved VO delta, current contract value,
  and milestone scheduled values by status. Milestone values use the signed
  base contract value, matching existing contract semantics. They are labeled
  **Contractual schedule** and are not cash, revenue, or receivables.

## Date filter bases

The inclusive date range applies independently to each source date rather than
the project identity: construction planned end, acceptance date, permit target
deadline or expiry, quote creation, contract signed date (or creation date when
unsigned), approved VO decision date (or update date), and milestone due date.
An in-range VO or milestone remains visible when its parent contract was signed
before the range. Design progress is current-as-of because no historical
baseline snapshots exist.

## Explicitly unavailable metrics

The API and exports expose stable reason codes instead of synthetic zeroes:

| Metric | Reason code |
| --- | --- |
| Historical S-curve | `HISTORICAL_SNAPSHOTS_UNAVAILABLE` |
| Weighted construction progress | `CONSTRUCTION_WEIGHTS_UNAVAILABLE` |
| Acceptance ratios / first-pass | `ACCEPTANCE_OUTCOME_DATA_UNAVAILABLE` |
| Actual cashflow, revenue, expenditure, P&L, receivables | `ACTUAL_FINANCE_LEDGER_UNAVAILABLE` |
| Inventory | `INVENTORY_LEDGER_UNAVAILABLE` |
| BOQ usage | `BOQ_USAGE_DATA_UNAVAILABLE` |
| Vendor performance | `VENDOR_PERFORMANCE_DATA_UNAVAILABLE` |

A project without its design-linked construction, acceptance, or permit source
returns the affected section as `Unavailable` with `SOURCE_NOT_CONFIGURED`.
No schema migration or report snapshot table is introduced.
