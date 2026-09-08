# NIH-166 business pipeline validation

## Verdict — Fail (8 September 2026)

The expanded API pipeline passes. The real-browser pipeline finds a blocking
Accountant handoff defect after contract signing and warehouse operations.
This supersedes the earlier feature-level QA verdict for claims about the
complete customer journey. No production behavior or permissions were changed
in this validation work.

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
| PIPE-07 | Accountant finds signed contract and creates its invoice | Pass using generated contract ID | **Fail: contract absent from selector** |
| PIPE-08 | Accountant submits/validates, BGD approves, Accountant pays | Pass | **Not Run: blocked by PIPE-07** |
| PIPE-09 | Paid state, invoice amount/vendor and actor history persist/read back | Pass, including public finance list | **Not Run: blocked by PIPE-07** |

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
staff handoffs. The later payment UI steps remain in the test to verify the
intended completed journey after the defect is fixed. They are not reported as
executed.

## Confirmed defect

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

Actual: the selector is empty. The E2E run reproduced this for
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

Related usability limitation: the Accountant's user-directory lookup is denied
by the existing users permission, so the payment form falls back to entering a
numeric accountant ID. Receipt quantity and selection fields also lack complete
label associations; the browser test must scope them by form and native role.

Proposed correction, pending the user's decision: provide minimal finance
payment-reference choices under the existing payment-preparation permission,
then use them in the payment form. Include eligible downstream contracts,
matching vendor identity and active accountants. Preserve the existing general
CRM ownership restrictions; verify reference authorization and rerun the full
browser journey. Do not solve the lookup by silently granting broad CRM/user
administration permissions.

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
# Full integration suite: 1,847 passed, including the new pipeline.
docker exec nih-166-sdk dotnet test \
  nihomebackend.integration.tests/nihomebackend.integration.tests.csproj \
  --no-build --no-restore -p:SkipNihomeWebBuild=true

# Focused pipeline; rerun after adding final Paid finance-list assertions: passed.
docker exec nih-166-sdk dotnet test \
  nihomebackend.integration.tests/nihomebackend.integration.tests.csproj \
  --no-restore -p:SkipNihomeWebBuild=true \
  --filter FullyQualifiedName~RfqProcurementPipelineTests

# Real UI pipeline: fails at the missing Accountant contract option.
cd nihomeweb
BASE_URL=http://localhost:5044 npx playwright test \
  e2e/smoke/rfq-business-pipeline.spec.ts
```

Final combined RFQ browser run: **six passed, one failed**. The pipeline failed
again at PIPE-07 for `PO-RFQ-14-5540a8573ad647d3b2c2`; there were no skipped tests.
Frontend ESLint, an explicit TypeScript compile of the new E2E spec, and targeted
.NET formatting verification passed. Production builds and unit tests were not
repeated because this follow-up changes only tests and documentation.
The original RFQ browser cases cover desktop creation, mobile/tablet comparison,
and four-language rendering; their feature-level results do not resolve this
pipeline defect. The new journey produces comparison, signed-contract and
warehouse screenshots, plus failure screenshot/video/trace under the ignored
Playwright `test-results` directory.

## Boundaries and follow-up

- **Full browser payment journey remains unvalidated** until PIPE-07 is fixed.
  The test deliberately fails; there is no skip, expected-failure annotation,
  mocked selector, or API workaround hiding it.
- Live Google Drive transport remains untested without credentials. Finance
  evidence in the current tests validates document metadata, not file existence,
  transport or substantive invoice accuracy.
- `ProjectReportService.GetUnavailableMetrics` explicitly reports actual
  cashflow, revenue, expenditure and P&L unavailable. `user_guide.md` distinguishes
  contractual milestones from cash/actual-ledger values. Paid requests appear in
  the finance register and payment-processing KPI; no actual-ledger posting is
  claimed by these tests.
