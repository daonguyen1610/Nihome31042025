# NIH-166 business pipeline validation

## Verdict — Pass for the supported Supply pipeline (8 September 2026)

The Accountant handoff defect found by the expanded browser journey is fixed.
The API pipeline and real-browser journey now complete through invoice approval,
payment and persisted Paid readback using ordinary business roles. The original
failure and its root cause are retained below for regression traceability.

## Business test basis

Sources: `Nicon-QLVH.md`, Modules 5–6; `Nicon-workflow.md`;
`Nicon_BreakTask_v1.xlsx`; `rfq-bid-comparison.md`; and the existing procurement,
contract, finance and reporting contracts.

Supply scenario: the factory needs power cable within an approved BOQ budget.
Procurement requests and revises a supplier quote; BGD selects the vendor;
Sales Manager signs the generated downstream contract. Site demand follows a
separate approved Material Request. Warehouse receives and issues the cable.
Accountant registers and validates the supplier invoice, BGD approves it, and
Accountant records payment.

RFQs do not automatically create Material Requests or invoices. There is no
receipt/acceptance-to-payable foreign key or three-way matching rule in the
existing finance contract. The browser receipt uses the supported optional
contract-line omission; the integration test explicitly links the receipt to
the awarded contract line. This report validates the Supply branch; it does not
claim an unimplemented subcontract acceptance-to-payment integration.

## Coverage and execution

| ID | Actor and business handoff | API integration | Real browser |
|---|---|---|---|
| PIPE-01 | Procurement drafts/submits BOQ; BGD approves budget | Pass | Pass |
| PIPE-02 | Procurement issues RFQ and records vendor pricing | Pass: two vendors | Pass: original and revised quote |
| PIPE-03 | BGD awards; project/customer/vendor/price carry into draft contract | Pass | Pass |
| PIPE-04 | Sales Manager signs the generated Supply contract | Pass | Pass; also exploratory walkthrough |
| PIPE-05 | PM submits separate MR; Procurement approves | Pass | Pass |
| PIPE-06 | Warehouse posts receipt, then issues stock to site/work item | Pass, explicitly linked contract line | Pass, optional contract line omitted |
| PIPE-07 | Accountant finds signed contract and creates its invoice | Pass using finance reference discovery | Pass; also exploratory walkthrough |
| PIPE-08 | Accountant submits/validates, BGD approves, Accountant pays | Pass | Pass |
| PIPE-09 | Paid state, invoice amount/vendor and actor history persist/read back | Pass, including public finance list | Pass, including reload |

Integration regression checks in the same journey cover forbidden approvals,
invoice creation before signing, wrong vendor, duplicate invoice, over-receipt,
over-issue, reversal after stock consumption, premature payment, repeated pay,
and attempted cancellation of a paid invoice. Assertions verify rejection
without unintended persisted changes and a single Paid event.

Only foundational customer/project/vendor/team data is seeded in the API test.
Intermediate BOQ, RFQ, contract, MR, receipt, issue and payment states are reached
through authenticated public endpoints using ordinary business roles.

The E2E test uses APIs only for authentication and project/team foundations.
Business transitions are performed through real screens against SQL Server;
there are no mocked business responses or elevated-role substitutes for normal
staff handoffs. The payment steps now execute through Paid and reload the
finance register to verify persistence.

## Resolved defect

### PIPE-07 — Accountant cannot select the awarded, signed contract

Severity: **High; blocks the normal invoice/payment journey.**

Reproduced through exploratory UI interaction and the repeatable E2E test:

1. Use Sales Manager to open an RFQ-generated Supply contract owned by
   Procurement, and click **Mark as Signed**.
2. Sign in as the normal seeded Accountant.
3. Open **Finance control → Create request → Downstream contract**.
4. Try to select the signed contract.

Expected: an authorized payment preparer can select the eligible downstream
contract and matching vendor to register the supplier invoice.

Before the fix: the selector was empty. The E2E run reproduced this for
`PO-RFQ-13-4fcc237e68f24b2c9e79` in project `PJ-2026-0004`, after successful
receipt and stock issue. An independent exploratory walkthrough reproduced it
for signed contract ID 11 in the dedicated demo project.

Root cause evidence:

- `FinanceControlPage.tsx` loads payment choices from the general CRM contract
  list, and treats a successful empty response as a usable selector.
- `ContractsController` checks `crm.contracts.view.all`; `ContractService.ListAsync`
  otherwise filters to `OwnerUserId == callerUserId`.
- RFQ awards assign the contract to Procurement. Accountant does not own that
  contract and lacks general all-contract access.
- Finance's create endpoint accepts the eligible signed contract ID. API tests
  that supply IDs directly therefore miss the UI handoff defect.

The Accountant's user-directory lookup was also denied
by the existing users permission, so the payment form fell back to entering a
numeric accountant ID. Both payment choices now use the finance lookup.
Receipt quantity and selection fields also lack complete
label associations; the browser test must scope them by form and native role.

### Delivered correction and API contract

`GET /api/finance/payment-references` (also under `/api/v1`) requires the existing
`finance.payments.manage` permission. It returns:

- `contracts`: ID, number, vendor ID/name and milestone ID/order/name.
- `vendors`: ID, code and company name, limited to eligible contracts.
- `accountants`: ID and display name of active Accountant users.

Contract eligibility shares the existing payment-create rule: Downstream,
non-null vendor, status other than Draft or Cancelled. Existing InProgress,
OnHold and Completed eligibility is preserved. General CRM ownership filtering
and user-directory permissions are unchanged. No schema or permission migration
is required; financial references expose no customer/contact/commercial details.
Payment creation continues to revalidate contract, vendor, milestone, accountant,
invoice fields, attachments and duplicate invoice rules on the server.

The payment dialog loads these references, offers named choices, and shows retry
or prerequisite guidance for failed/empty responses. It disables creation until
references are available. The new prerequisite message is seeded in Vietnamese,
English, Chinese and Japanese. Finance smoke coverage deliberately injects a
lookup failure to verify retry and an empty response to verify prerequisite
guidance; these are separate from the unmocked pipeline.

Five focused integration cases verify exact response fields, eligible/ineligible
contract states, vendor scope, active accountants, anonymous 401 and non-payment
role 403 responses. They also verify the same Accountant still receives an empty
general CRM list and 404 for the Procurement-owned contract detail.

## Environment, evidence and commands

Local isolated stack: `nih-166-app` at port 5044, `nih-166-sql` (SQL Server 2022),
and `nih-166-sdk` for .NET execution. Existing unrelated checkout containers on
ports 5043/1433 were not modified. Generated QA projects remain in the isolated
database for inspection. The working branch builds on the RFQ implementation
and preserves the separately created `4c450a5` test commit.

The browser-plugin connection reported no available browser. Exploratory
interaction used a local standalone Playwright browser against the real app,
including actual login forms for Sales Manager and Accountant. It was an
agent-driven walkthrough with inspected screenshots, not human acceptance
sign-off. The independent reviewer inspected the business contract, test design
and source; the implementing agent executed the browser walkthrough.

```bash
# Full integration suite, including references and business pipeline.
docker exec nih-166-sdk dotnet test \
  nihomebackend.integration.tests/nihomebackend.integration.tests.csproj \
  --no-restore -p:SkipNihomeWebBuild=true

# Unit regression suite.
docker exec nih-166-sdk dotnet test \
  nihomebackend.tests/nihomebackend.tests.csproj \
  --no-restore -p:SkipNihomeWebBuild=true

# Real UI pipeline, RFQ responsive/language coverage and finance recovery.
cd nihomeweb
BASE_URL=http://localhost:5044 npx playwright test \
  e2e/smoke/rfq-business-pipeline.spec.ts \
  e2e/smoke/admin-rfqs.spec.ts \
  e2e/smoke/admin-finance-control.spec.ts --workers=1
```

Combined browser run: **8 passed**, including the complete pipeline for invoice
`INV-PIPE-66e72c92` against `PO-RFQ-15-0bce6531bb7d4ca58207`. The Paid register shows
3,600,000 VND, the selected supplier and assigned Accountant after reload.
Final checks: **1,854 integration tests** and **2,007 unit tests** passed with
zero skips; the focused reference/pipeline run passed all six cases. Frontend
ESLint, application and E2E TypeScript checks, production build, backend build
and whole-solution formatting verification pass. The E2E TypeScript command was
rerun successfully from `nihomeweb` with `--types node` after a repository-root
invocation could not resolve the installed Node types.

The finance smoke test was rerun after adding empty-reference guidance and
creation-disabled assertions: **1 passed**. Independent senior business analyst
review approved the basis for QA; subsequent functional QA passed the supported
Supply pipeline, with no open High/Blocker defect and the boundaries below.

Exploratory retest: log in through the real Accountant login form; open payment
creation; select the previously missing `PO-RFQ-13-4fcc237e68f24b2c9e79`; confirm
matching vendor and named Accountant choices. Inspected screenshots at 390px
and 768px confirm the dialog fits without horizontal overflow and its actions
remain reachable by scrolling. Cancelled the exploratory draft without saving.

The journey produces comparison, signed-contract, warehouse and Paid screenshots
under ignored Playwright `test-results`. Exploratory mobile/tablet screenshots
are local temporary artifacts. The independent reviewer evaluated the business
contract and source before QA; browser execution and visual inspection were
performed by the implementing agent, limiting review independence.

## Rebase compatibility — 8 September 2026

Rebased the four RFQ commits onto `9ded3c0` (`Cover complete procurement
pipeline`). The route conflict preserves both the separately guarded RFQ route
and upstream Warehouse access to the procurement workspace. Git range comparison
confirms the other three commits replayed without patch changes.

Upstream validates excessive receipt/issue quantities during draft creation.
The RFQ integration journey now expects those create requests to fail and proves
no draft is persisted, before continuing with valid receipt and issue quantities.
Upstream also moved posting into warehouse transaction detail pages; the browser
journey now opens each transaction, posts it and returns to the warehouse list.
These changes align tests with upstream behavior; they do not change production
business rules.

Post-rebase checks: seven focused integration cases pass, including both RFQ and
upstream procurement pipelines and finance reference authorization. Three
warehouse browser cases and all eight RFQ/finance browser cases pass, including
the complete journey through Paid. Frontend lint, application/E2E TypeScript checks,
production build and targeted backend formatting pass. The original full-suite
counts above describe the pre-rebase run; full suites were not repeated for this
route resolution and test compatibility adjustment.

## Boundaries and follow-up

- Live Google Drive transport remains untested without credentials. Finance
  evidence in the current tests validates document metadata, not file existence,
  transport or substantive invoice accuracy.
- `ProjectReportService.GetUnavailableMetrics` explicitly reports actual
  cashflow, revenue, expenditure and P&L unavailable. `user_guide.md` distinguishes
  contractual milestones from cash/actual-ledger values. Paid requests appear in
  the finance register and payment-processing KPI; no actual-ledger posting is
  claimed by these tests.
