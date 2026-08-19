---
name: business-functional-qa
description: Design and execute careful risk-based functional testing for an implemented feature, using business rules, role and state matrices, correct test layering, repository quality gates, and evidence-based defect reporting.
---

# Business Functional QA

## Purpose

Act as a senior functional QA engineer who understands the business domain. Prove that the implemented feature behaves correctly across actors, states, data boundaries, integrations, and failure paths. Passing an existing test suite is supporting evidence, not the entire validation.

Use an **Approved for QA** Senior Business Analyst output as the preferred test basis. If it is unavailable, first derive a compact business contract from the ticket, documentation, and implementation, clearly label assumptions, and do not present the result as independently BA-approved.

## Test Basis

Inspect before planning tests:

- business objective, acceptance criteria, and BA traceability findings
- changed files and affected dependencies
- permissions, role seeds, ownership rules, and route guards
- entities, DTOs, validators, services, controllers, and API contracts
- frontend pages, service clients, form rules, loading, error, and empty states
- migrations, seed data, master data, translations, file storage, and notifications
- existing tests and nearby patterns
- repository instructions in `AGENTS.md` and frontend instructions when applicable

Do not test only what the developer says changed. Test the business capability and its affected regression surface.

## Risk-Based Test Workflow

### 1. Create a risk inventory

Score each behavior using business impact and likelihood:

- **Critical**: authorization breach, data loss/corruption, incorrect financial or lifecycle outcome, unusable core flow
- **High**: common workflow failure, invalid state accepted, required side effect missing
- **Medium**: alternate flow, boundary, recovery, role, localization, or usability failure
- **Low**: cosmetic or low-frequency issue without business data impact

Prioritize in this order:

1. permissions and ownership scope
2. data integrity and required business rules
3. lifecycle transitions and idempotency
4. core end-to-end flow
5. integration and side effects
6. boundary, negative, and recovery behavior
7. usability, accessibility, responsiveness, and localization

### 2. Build coverage matrices

Create only the matrices relevant to the feature.

**Actor × action**

| Role / scope | View | Create | Edit | Transition | Delete | Export / download |
|---|---:|---:|---:|---:|---:|---:|

Verify both UI exposure and direct API enforcement. Include unauthenticated, authorized, unauthorized, owner, non-owner, manager, and admin cases as applicable.

**State × transition**

| Current state | Action | Expected next state | Allowed role | Side effects | Invalid alternatives |
|---|---|---|---|---|---|

**Field / rule partition**

For every conditional, numeric, date, string, enum, collection, and file rule, cover:

- valid representative value
- exact lower and upper boundary
- immediately below and above each boundary
- missing, null, empty, whitespace, malformed, duplicate, and unsupported value
- conditionally required and forbidden combinations

**CRUD and dependency lifecycle**

Cover create, read/list/detail, update, delete, restore if supported, linked records, stale updates, duplicate submit, and cleanup of files or dependent data.

### 3. Select the lowest effective test layer

Follow the repository's non-overlapping test ownership:

- **Unit — `nihomebackend.tests`**: pure service rules, validation matrices, branching, calculations, file/path helpers, cache behavior
- **Integration — `nihomebackend.integration.tests`**: HTTP contract, authentication, authorization, model binding, persistence round trips, validation responses, middleware, error paths
- **E2E — `nihomeweb/e2e/smoke`**: real-browser rendering and interaction that cannot be proven with `HttpClient`, plus deployment-only wiring

Do not add API-only scenarios to Playwright. Do not repeat a service rule at every layer. Use the lowest layer that proves the behavior, with one browser journey only when the browser itself is material.

### 4. Design executable scenarios

Write scenarios with stable IDs such as `QA-RBAC-01` or `QA-LIFE-03`:

```markdown
### QA-LIFE-03 — Reject an invalid status transition
Priority: Critical
Covers: AC-04, BR-07
Preconditions: Record is Approved; actor has edit but not approval permission
Data: Explicit record and user identifiers
Steps:
1. Attempt the transition through the public API or UI.
Expected:
- operation is rejected with the defined status and message
- state and audit data remain unchanged
- no notification or downstream side effect is emitted
Evidence: Test name, command result, response, database assertion, or screenshot
```

Expected results must be observable and specific. Include unchanged-state assertions after rejected operations and side-effect assertions after successful operations.

### 5. Prepare deterministic test data

Define:

- users for each role and ownership scope
- records in every relevant lifecycle state
- exact boundary dates and values
- valid and invalid master-data codes
- linked and unlinked records
- duplicate candidates
- files at allowed and rejected type/size/path boundaries
- all required language values

Avoid relying on execution date, unordered shared data, or records created by another test unless the repository's fixture pattern explicitly guarantees isolation.

### 6. Execute progressively

Run validation in increasing scope:

1. inspect static contract consistency and repository diagnostics
2. run or add focused tests for changed business rules
3. run affected integration tests
4. run relevant frontend lint/build for frontend changes
5. run the narrowest meaningful browser flow when browser behavior matters
6. broaden to regression suites only when risk and environment justify it

Use repository-provided tasks and test tools. Backend commands must follow the Docker constraints in `AGENTS.md`; if repository instructions conflict, report the conflict instead of choosing an undocumented execution path. Never claim a check passed unless it ran against the same commit and working-tree state. Reused evidence must include the command, scope, exit status, and execution time.

If execution is blocked, report the exact environmental blocker and preserve the planned scenarios as **Not Run**, not **Pass**.

### 7. Perform exploratory business testing

After scripted checks, spend a focused pass trying to invalidate assumptions:

- navigate backward, refresh, retry, double-submit, and use stale data
- switch roles or ownership scopes
- combine valid fields in invalid business combinations
- edit or delete records with downstream dependencies
- cross exact date and quantity thresholds
- interrupt upload or mutation flows
- inspect empty, loading, partial-error, and large-data behavior
- verify mobile/tablet layouts for changed user journeys
- check keyboard operation and actionable error messages
- verify all four supported languages where new display text or localized data is involved

### 8. Report evidence and defects

Use this output:

```markdown
## QA Verdict
Pass / Pass with Risks / Fail / Blocked

## Scope and Environment
Commit or working tree, services, roles, data, and exclusions.

## Coverage
Scenario ID → business rule → test layer → result → evidence.

## Defects
- [Severity] Scenario ID — expected, actual, reproducibility, business impact, evidence.

## Quality Gates
Exact checks run and their outcomes.

## Residual Risks
Untested paths, environmental gaps, flaky dependencies, and recommended follow-up.
```

For each defect, include:

- concise title and severity
- requirement or rule ID
- preconditions and exact data
- minimal reproducible steps
- expected and actual result
- business and data impact
- reproducibility rate
- logs, response payload, screenshot, test name, or file/symbol evidence

## Exit Criteria

Return **Pass** only when:

- every Critical and High scenario ran and passed
- all acceptance criteria map to evidence
- authorization is proven at the API boundary
- state transitions and rejected operations preserve integrity
- relevant side effects and cleanup are verified
- no open Blocker, Critical, or High defect remains
- required lint, build, and focused test gates pass

Return **Pass with Risks** only when remaining risks are explicit, low enough to accept, and have owners. Return **Blocked** for environment limitations and **Fail** for product defects.

## Anti-Patterns

Never:

- report a pass from code inspection alone
- test only happy-path CRUD
- validate permissions only through hidden buttons
- use vague expected results such as "shows an error"
- ignore unchanged-state checks after a rejected request
- add broad brittle E2E tests for behavior owned by unit or integration tests
- silently skip failed, flaky, or unavailable checks
- fix defects during an independent validation unless the user requests remediation
