# AGENTS.md

## Project context

This repository contains a React frontend and an ASP.NET Core 8 backend developed and tested with Docker Compose. Work as a careful senior software engineer: understand existing behavior, make the smallest safe change, and leave the repository verifiable.

## Instruction precedence

Follow instructions in this order:

1. System and platform instructions.
2. User instructions for the current task.
3. This file and repository documentation.
4. Existing code conventions and local defaults.
5. Divine and conquer technique. Break into smaller things, test carefully, evaluate risks.

When repository instructions conflict, use the more specific and recent instruction, document the conflict, and ask the user when the choice could affect business behavior or data.

## Working principles

- Read relevant nearby code, tests, migrations, and documentation before editing.
- Prefer small, focused, reversible changes.
- Preserve existing behavior and public contracts unless the task requires a change.
- Reuse existing services, validators, components, and patterns. Search first, especially in 'src/lib/', 'Services/', and shared frontend utilities.
- Avoid duplicating stable technical identifiers and shared logic. Define named
  constants close to their owner, and reuse shared modules when multiple callers
  need the same value or behavior. Keep configurable business values in ADMIN,
  not constants. For API codes used across languages, prefer an existing shared
  contract or generated types; otherwise define each value once per language and
  verify the wire contract with tests. Do not create abstractions solely to hide
  an independent test expectation.
- Do not refactor unrelated code, add dependencies without justification, or invent requirements, APIs, translations, or business rules.
- Keep frontend files formatted with the repository Prettier configuration.
  Do not compress JSX, TypeScript, CSS, JSON, or configuration into long
  single lines. Run `npm run format` after frontend edits and
  `npm run format:check` before committing.
- Record meaningful trade-offs, assumptions, blocked checks, and residual risks.

## Issue-report workflow

For every reported defect, establish evidence before changing product code:

1. Record the reported symptom, expected behavior, affected actor/data, and
   environment. Check the current business contract and clarify ambiguity that
   could change the expected outcome.
2. Reproduce the issue on the unchanged implementation. Write and run the
   smallest regression test at the appropriate layer first. For browser issues,
   also exercise the real UI manually in the live Docker Compose application;
   use a live Playwright test when the behavior can be automated. Capture the
   exact failing assertion or manual steps and observed result. A test that
   fails for setup, permissions, or missing fixtures does not prove the defect.
3. Use that failure to define the issue precisely. Investigate the relevant
   UI, API, validation, persistence, data, permissions, and history until the
   root cause is understood. Then choose the smallest complete fix.
4. Run the same reproduction after the fix and relevant nearby regression
   checks, including negative paths. Report before/after evidence and any
   reproduction or validation blocked by the environment. Do not call an issue
   fixed when its original symptom has not been verified.

If automation is impractical, perform and record a manual reproduction before
editing, then add an executable regression test for changed behavior where
feasible. Do not change a failing test merely to make it pass without proving
whether the failure is in the test or the product.

## Task lifecycle

For every non-trivial task:

1. Inspect repository state and identify affected code, data, documentation, and test layers.
2. Clarify acceptance criteria and affected API or data contracts.
3. Implement the smallest complete change.
4. Add or update tests at the lowest appropriate test layer when executable
   behavior changes.
5. Run relevant checks and investigate failures to distinguish a bad test from
   a product defect. For every .NET source or test change, run `dotnet format`
   in the backend container for the affected projects, then run
   `dotnet format --no-restore --verify-no-changes` for the solution, matching
   CI. Include unit and integration test projects; a passing build or test run
   does not replace format verification. Fix format failures and rerun the
   affected tests after their final edit. For documentation-only or other
   non-code changes that cannot affect build/runtime behavior, use
   document-focused checks only; do not run code builds or automated test
   suites.
   Every new or changed integration or E2E test must be executed after its final
   edit and pass before declaring the task complete. If its required environment
   is unavailable, report the test as not run and the work as unverified; do not
   claim a passing result from compilation, test listing, or an earlier version.
6. Review the final diff for scope, security, compatibility, and accidental changes.
7. Update documentation and seed data when required. The Seed data must quality, the data is crucial to reflect the business flow, as well as get the real context when look at the system. The name must be meaningful, and reflect to the business of Nicon.
8. Commit the completed change using the Git rules below.
9. Avoid run the full test suite, which takes a lot of time to complete. Only focus on the relevant test to confirm.

Do not declare work complete when required validation is skipped. If a check cannot run, state why and identify the risk.

## Business validation

For non-trivial features and business-rule changes, use these skills after implementation, in order:

1. '.github/skills/senior-business-analyst/SKILL.md' — validate actors, intent, rules, lifecycle, contracts, and requirement-to-test traceability.
2. '.github/skills/business-functional-qa/SKILL.md' — design and execute risk-based functional validation from the approved business contract.

Use both before declaring a feature business-ready. Use a separate agent or clean review context when available. If the same agent validates its implementation, reread source evidence and record the independence limitation.

Report ambiguity, missing evidence, defects, blocked checks, and residual risk. Only the product or business owner may accept requirement risk. Fix findings only when requested, then rerun affected validation.

## Branching and repository safety

- Start on a task branch unless the user requests the current branch or the change is a small documentation-only update.
- Branch from the designated default branch, normally 'main', unless instructed otherwise.
- Check for uncommitted changes before switching branches. Never overwrite, reset, stash, or discard user changes without explicit approval.
- Do not rewrite history, force-push, squash, or amend another commit unless explicitly requested.
- Never commit secrets, credentials, private keys, tokens, local environment files, or sensitive production data. Stop and report the issue if discovered.

## UI/UX for nontechnical users

The website serves nontechnical staff. Every page must be easy to understand
and use without knowledge of the implementation or help from a developer.

- Make the page purpose, current project or record, current status, and next
  available action clear at a glance. Use meaningful business names and codes
  instead of internal IDs.
- Use plain, familiar language. Explain necessary business abbreviations on
  first use. Keep API names, database terms, JSON, and other implementation
  details out of normal user workflows.
- Give each task one clear primary action. Label buttons with a specific verb
  and object, such as "Save plan" or "Send for approval". Keep secondary actions
  visually quieter and destructive actions clearly distinguishable.
- Organize information in the order users do the work. Group related fields,
  place instructions beside the relevant control, and reveal advanced details
  only when needed. Avoid duplicate headings, repeated instructions, and walls
  of text.
- Make features discoverable through clearly named navigation. Keep sidebar
  labels, page titles, breadcrumbs, tabs, and action labels consistent. Preserve
  relevant project and record context when navigating between related pages.
- Explain prerequisites before users attempt an action. If an action is
  blocked, state why and provide the next step their role can perform. Never
  leave an unexplained disabled button or an empty selector as the only clue.
- Use persistent field labels, readable numbers and dates, and useful examples.
  Clearly identify required fields. Prefer searchable choices with meaningful
  names over asking users to enter technical identifiers.
- Distinguish loading, first use, no search results, missing prerequisites,
  permission restrictions, and failures. Empty states should explain how to
  proceed; errors should explain what needs correction or how to recover.
- Confirm what was saved, submitted, approved, or rejected. Preserve entered
  work after a failed operation and make retry behavior clear. Follow the
  existing confirmation and deletion policies for consequential actions.
- Keep text readable, controls easy to reach, and layouts usable on desktop,
  tablet, and mobile. Support keyboard navigation, visible focus, accessible
  labels, and sufficient contrast. Never communicate status through color or
  icons alone.
- Keep all user-facing guidance in the supported translation system. Check
  that translated text and long project names remain readable without hiding
  the information needed to act.
- Before calling a UI change complete, inspect the rendered page and walk
  through the intended user's task. Verify that a nontechnical person can tell
  where they are, what to do next, why something is blocked, and whether their
  action succeeded. Follow the task's agreed validation scope.

## Frontend and backend integration

- Keep frontend behavior, backend behavior, DTOs, validation, and API contracts aligned. Update both sides when a contract changes.
- Handle loading, error, empty, success, and responsive states where applicable.
- Never hardcode a value that can be configured in ADMIN. Runtime behavior must
  read the persisted administrator setting or managed master data through the
  backend, including limits, thresholds, options, recipients, and templates.
  Do not keep a second fixed value in React, services, controllers,
  `appsettings`, or environment variables that overrides an ADMIN change.
  An ADMIN change must take effect without a code change or redeployment.
- Put initial values in the appropriate seed or migration only to initialize
  persisted settings. Keep technical safety ceilings distinct from business
  settings, document them, and expose any effective limit to the ADMIN UI;
  never silently clamp a configured value. Validate administrator changes on
  the server, enforce management permissions, and test that a saved change
  affects the actual business operation.
- Fetch business data such as categories from the backend API or ADMIN-managed
  master data; do not duplicate option lists in React.
- Do not hardcode 'localhost' or deployment-specific media/API hosts. Store backend media as host-relative paths such as '/images/example.png', resolved through a shared helper.
- Every number entering to the form, for example: 100000. The frontend must resolve to readable version. like 100.000. Apply for every scenarios in the system.
- Follow 'nihomeweb/CLAUDE.md' for web UI conventions when it exists.
- Centralize content translations in '/admin/translations'. Add i18n keys rather than embedding display text.
- Manage translations of user-managed content, including role-group names, in
  the Dịch nội dung section of '/admin/translations'. Create/edit dialogs collect
  only the source-language content; they must not require or edit translations
  for other languages. Preserve existing translations when source content changes.
- Throughout development, updates, refactors and bug fixes, identify every new
  or changed translatable content type and field. In the same change, register
  and maintain it in Dịch nội dung, including existing records, permissions,
  validation, translation progress, reset/deletion and runtime language reads.
  Keep source and translated values intact across edits and migrations; clearly
  distinguish an untranslated placeholder from a completed translation. Cover
  the management and displayed result with appropriate tests in every supported
  language. Do not treat a translation key or persisted placeholder alone as
  evidence that a content translation is complete.
- Keep translation keys and seeded content aligned in all supported languages.
- Every application email must have an editable template in `/admin/email-templates` (subject and body), a preview of the actual rendered message before sending, and a documented set of supported variables. Do not hardcode a send-only email body in a service or controller. Render variables server-side with appropriate HTML encoding, validate edited templates/content on the server, enforce template-management and send/edit permissions, and cover the rendered email in tests. Keep existing templates working when adding a new email type.
- For entities with fields such as 'NameVi', 'Name', 'NameZh', and 'NameJa', populate every required language field on every write path: create, seed, migration, and legacy-data auto-create. Do not rely on read-time fallback.

## Clear responses and diagnostic logs

- For every backend result shown to a customer or staff member, make the UI
  state what happened, which record or step is affected, why an action is blocked
  when the reason is known, and what the user can do next. Make success, pending,
  empty, validation, permission, conflict, and failure states truthful and
  distinct. Do not show a generic failure when the server knows a useful reason.
- Return a stable error code and field or workflow context from the API where
  applicable. The client must present an understandable, localized message and
  an actionable next step based on the actual response; do not silently replace
  a specific server error with a vague toast or invent a cause on the client.
  Give examples for input rules and tell the user when to retry, refresh, correct
  data, request access, or contact support. Suggest only actions their role can
  perform, and do not disclose restricted record details in permission errors.
- Keep customer-facing text free of stack traces, SQL, internal paths, secrets,
  raw exception names, and sensitive personal data. When an unexpected error
  has no safe explanation, state that the operation did not complete, preserve
  the user's work where possible, and provide a support reference or request ID.
- Write structured diagnostic logs with the operation, outcome, safe record
  identifier, reason or error code, and correlation ID needed to investigate.
  Use an appropriate severity and include the next operational step for failures
  that need intervention. Do not log credentials, tokens, full request bodies,
  sensitive customer data, or misleading success before a durable operation
  completes. Logs provide detail for operators; API and UI messages guide users.
- Cover the returned error code and message context in integration tests, and
  verify the rendered guidance in a browser test when user action depends on it.
  Keep all user-visible wording in the supported translation system.

## Validation rules

Every user-writable field needs explicit rules for presence, format, length, range, normalization, and cross-field relationships as applicable.

- Validate in the frontend for feedback and on the server for protection. Frontend-only validation is insufficient.
- Validate format, not only presence. A non-empty phone number or email address is not necessarily usable.
- Do not assume '[EmailAddress]' or '[Phone]' enforces the required project format.
- Reuse the shared validators in 'nihomebackend/Services/ContactValidation.cs' and 'nihomeweb/src/lib/validation.ts'; do not add a parallel regex.
- User-facing validation messages must identify the field, explain the rule, and include an accepted example when useful. Add frontend messages as i18n keys.
- Keep fixtures and seed data valid. Do not weaken validation merely to make invalid fixtures pass.

Before finishing a form or write endpoint, identify each field's invalid-value rule and the server location that enforces it.

## Core business alignment

All implementation decisions that affect business behavior must comply with
`docs/Nicon-QLVH.md`, `docs/Nicon_BreakTask_v1.xlsx`, `docs/Nicon-workflow.md`,
and the customer evidence retained under `docs/Nicon/`. The canonical Markdown
documents define the current working contract; `docs/Nicon/` provides the
auditable source material used to interpret and validate it.

- Before implementing, research the relevant documentation, existing behavior, code paths, data contracts, and tests. Do not start coding from assumptions.
- Think through the actors, business intent, workflow states, rules, permissions, dependencies, edge cases, and expected outcomes before choosing a solution.
- State unresolved ambiguity and request clarification when it could change business behavior or data; do not invent missing requirements.
- Prefer the smallest solution that satisfies the confirmed business flow, avoids unnecessary work, and improves delivery efficiency without weakening correctness or validation.
- After implementation, verify the result against the documented workflow and customer expectation before declaring the task complete.

### Customer evidence under docs/Nicon

- Read `docs/Nicon/MoMs/` for dated decisions, open questions, delivery
  priorities, and evidence timestamps before relying on older PRD wording.
- Read the PRD and workflow DOCX files for the eight-module scope, cross-role
  interactions, eleven-position KPI framework, Google Drive structure, and
  mobile/desktop requirements.
- Inspect the relevant schedule PDFs when implementing design, permitting, or
  construction planning. Preserve hierarchical WBS, task duration, planned
  start/finish, predecessors, milestones, calendars, and baseline data; do not
  reduce the schedule to a flat checklist.
- Inspect representative workbooks under `docs/Nicon/BOQ/` before designing an
  import contract. They contain both single-sheet and multi-sheet structures,
  summary/cover sheets, discipline sheets, bilingual labels, formulas, merged
  cells, and project-specific naming. Treat them as discovery evidence, not as
  one automatically approved schema.
- When sources conflict, the explicit current user request wins, followed by a
  later confirmed meeting decision, then the canonical requirements/workflow,
  then older PRD/workflow attachments. Record the conflict and preserve an open
  decision when later evidence is not confirmed.
- Do not treat a meeting proposal, effort estimate, sample workbook value, PDF
  date, color, sheet name, or customer-project name as a product rule unless it
  is confirmed in the canonical contract.
- Keep implementation status separate from target scope. A requirement in
  `docs/Nicon/` is not evidence that the API, UI, migration, seed, or test is
  delivered.
- Use only these delivery labels in canonical documentation: `Implemented`
  requires matching persistence, API authorization, UI and appropriate tests;
  `Partial` identifies the exact delivered subset and gap; `Target` has no
  delivery claim; `Open decision` names the owner/decision needed. Do not infer
  host/deployment status from the local branch.
- Treat `OperationalProject` as the business root. Existing Construction Task,
  Diary, Punch, Acceptance, As-Built, Handover and Permit records still use
  `DesignProjectId`; HSE, Procurement, Finance and project documents use
  `OperationalProjectId`. This is a compatibility boundary, not permission to
  create new cross-module aggregates on `DesignProjectId`. Any migration needs
  backfill, uniqueness/conflict rules, compatibility behavior, rehearsal and
  rollback.
- Distinguish preliminary Quote BOQ, Tender Estimate, Design quantity takeoff,
  approved Execution BOQ revision and Final BOQ. Only the applicable approved
  Execution BOQ constrains MR/warehouse; never promote a workbook based only on
  its module, filename or sheet label.
- The current Drive registry has separate `Survey` (`01_Khao_sat`) and
  `CrmPreDesign` (`01_CRM_PreDesign`) categories. The business target nests
  Survey under CRM Pre-Design. Preserve current bindings until an approved,
  reversible topology migration moves/rebinds and verifies historical files.

## Module 4 construction and acceptance rules

Treat the Module 4 contract in `docs/Nicon-QLVH.md`, `docs/Nicon-workflow.md`,
and `docs/application_developer.md` as mandatory for construction work.

- Preserve the two-stage lifecycle: Phòng CM prepares the project after the
  Upstream D&B/construction contract is signed; BCH and Phòng CM then operate
  in parallel during construction.
- A signed Upstream D&B/construction contract only makes CM preparation
  eligible. Do not set `ReadyToStart`/`InConstruction` until a server-side gate
  verifies applicable released IFC, valid required permits, published schedule
  baseline, approved BCH/resource plan and locked Budget Baseline. Apply site
  handover, method statement and HSE plan when the approved project/ISO template
  marks them required; retain an evidence snapshot and actor for the gate.
- Preserve the organizational boundary. BCH creates field evidence and runs the
  assigned site; Phòng CM supervises multiple projects and performs independent
  level-2 review. Do not collapse both into one role, menu permission, or status
  transition.
- Model the preparation baseline explicitly: WBS/Gantt, critical path,
  Baseline S-Curve, BCH organization, resource plan, and locked Budget Baseline.
  Never overwrite a published baseline; version changes and keep their audit
  history. Budget/scope changes require the applicable VO authorization.
- Resource requests must be typed as material, equipment/machinery, or labour.
  Reuse the existing MR/BOQ workflow for material requests. Over-limit or
  unplanned requests require CM level-2 approval before Procurement.
- Keep QA/QC, HSE, Punchlist, daily diary, Upstream QS, and Downstream QS as
  distinct business records with project/WBS links, lifecycle, responsible
  actor, evidence, timestamps, and concurrency control.
- Upstream QS links to the Owner/CĐT contract and receivable milestone;
  Downstream QS links to the subcontractor/team contract and payable dossier.
  Do not merge directions or infer direction from amount signs.
- Compute schedule variance against the published Baseline S-Curve. A delay
  strictly greater than 5% raises a red alert to the Site Manager and requires
  an explanation/recovery plan. Persist calculation inputs and baseline version
  so the result is reproducible.
- Enforce separation of duties server-side: an author cannot perform the
  independent CM review of the same MR, QA/QC dossier, QS batch, HSE violation,
  or VO. UI hiding alone is insufficient.
- Only released IFC revisions are visible as construction instructions. Keep
  suppliers, Downstream contracts, payment milestones, VOs, and KPI ownership
  in Modules 5, 6, and 8; Module 4 references them by stable IDs rather than
  copying master data.
- Synchronize quality records to `04_Thi_cong_Nghiem_thu` through the existing
  durable document workflow. Database metadata, workflow state, permissions,
  versions, and audit history remain authoritative; a Drive failure must be
  visible and retryable.
- Design BCH screens mobile-first for weak connectivity and safe retry/offline
  behavior. Design CM controls for desktop comparison, exception review, and
  portfolio monitoring. Test duplicate submission and reconnect conflicts.
- Before implementing unresolved details such as variance period/formula,
  baseline unlock authority, approval matrix, mandatory QA/QC checklist, and
  payment dossier composition, obtain a product/business decision; do not
  invent them.

## Hard-delete policy

Hard delete is a business operation, not a direct 'DbSet.Remove' call. Every user-facing root delete must follow the Permanent Aggregate Deletion section in 'docs/application_developer.md'.

- Add an authorized 'GET .../{id}/deletion-impact' endpoint. Classify every dependent group as 'Delete', 'Unlink', or 'Block', including files and external bindings.
- Require a typed resource code and deterministic plan token in the 'DELETE' body. Recompute the plan inside the delete transaction and return '409 Conflict' if it changed.
- Enforce authorization, confirmation, concurrency, and blockers on the server.
- Execute aggregate database changes in one transaction: delete owned records, unlink independent records, and preserve unrelated roots.
- Never silently orphan or destroy files. Use existing document services for cleanup, block while cleanup is pending, and disclose external folders that are unlinked but preserved.
- Use the shared frontend deletion-impact dialog. Do not use 'window.confirm' or client-side loops to delete an aggregate graph.
- Seed dependency labels and messages in Vietnamese, English, Chinese, and Japanese.
- Integration tests must cover authorization, counts/actions, confirmation, blockers, stale plans, concurrency, cleanup/unlinking, and unchanged state after rejection.
- Seeded and demo roots follow the same contract as user-created data.
- Support delete images, files whenever those resources did not link or use in the background job.

## ASP.NET Core and data access

- Keep controllers thin; put business logic in services and use dependency injection.
- Use DTOs at API boundaries rather than exposing persistence entities.
- Use 'async'/'await' for I/O and cancellation tokens where supported.
- Do not change schema without an EF Core migration.
- Avoid N+1 queries. Use 'AsNoTracking()' for read-only queries when tracking is unnecessary.
- Review migration files for correctness, data safety, rollback implications, and environment compatibility before applying them.
- Do not run 'dotnet', EF, or database commands directly on the host when the backend container is available.

## Docker and EF commands

Confirm the actual container with 'docker compose ps'; the name may differ from this example.

```bash
docker compose ps
docker exec <backend-container> dotnet build
docker exec <backend-container> dotnet ef migrations add <Name>
docker exec <backend-container> dotnet ef database update
docker exec <backend-container> dotnet ef migrations remove
docker exec <backend-container> dotnet ef migrations script
```

Always inspect generated migrations before applying them.

## Test strategy

Choose the lowest test layer that can prove the behavior. Do not duplicate the same assertion at every layer.

- Unit tests ('nihomebackend.tests'): isolated services, validation, branching, JSON handling, cache invalidation, and file-resolution helpers. Use InMemory EF and Moq where appropriate; no HTTP or Docker.
- Integration tests ('nihomebackend.integration.tests'): the real ASP.NET pipeline through 'WebApplicationFactory', including middleware, auth, model binding, EF persistence, API contracts, CRUD, validation, and authorization.
- E2E tests ('nihomeweb/e2e/smoke'): live browser tests against the running Docker Compose environment, using its served frontend, backend API, SQL Server, configuration, and seeded accounts. Cover browser rendering, SPA mounting, JavaScript errors, route rendering, and deployed-stack wiring such as CORS and health checks. API-only behavior belongs in integration tests.
- Start Docker Compose and confirm its services and application endpoint are ready before running E2E. Set `BASE_URL` to the Compose-served application. A Vite dev server, mocked/intercepted API responses, or an in-memory backend may support a separate UI test, but its result does not count as a live E2E pass. Report mock-based and live-stack results separately.
- E2E and Integration tests validate fully business pipeline test. Not only feature level.
- Covers Happy path, Negative path testing. Ensure all test cases to cover business scenarios of the customers.
- Use high quality data for testing with the real use cases.

Use this rule: pure logic → unit; HTTP/auth/persistence contract → integration; browser or deployed-stack behavior → E2E. Tests must detect defects, not merely reproduce the implementation. Find the root cause before changing a failing test or product code.

## Documentation and quality checks

Keep the root of `docs/` limited to these six canonical files plus the curated
`docs/Nicon/` customer-evidence directory:

- `application_developer.md` — setup, architecture, API contracts, migrations, operations, and testing guidance.
- `Nicon_BreakTask_v1.xlsx` — authoritative customer task breakdown.
- `Nicon-QLVH.md` — authoritative customer business requirements.
- `Nicon-workflow.md` — authoritative customer workflows.
- `user_guide.md` — delivered user workflows and operating instructions.
- `users-rbac.md` — roles, permissions, and access behavior.

`docs/Nicon/` may contain only customer-provided or customer-derived source
evidence organized as follows:

- `MoMs/<YYYY-MM-DD>/` — dated meeting minutes, BA synthesis, decisions, open
  questions, and delivery priorities.
- `BOQ/` — representative BOQ/quotation workbooks retained byte-for-byte.
- Root-level PRD/workflow DOCX and schedule PDF files supplied by NICON.

Do not place implementation notes, generated previews, extracted text, test
artifacts, temporary conversions, or duplicate canonical guides in
`docs/Nicon/`.

During every change:

- Update the relevant canonical file in place when behavior, configuration, API contracts, workflows, or operations change.
- Do not add other root files or subdirectories under `docs/` unless the user
  explicitly changes this allowlist.
- Do not create separate ticket notes, feature documents, review reports, scenario matrices, or dated validation reports in `docs/`. Put reusable guidance in the appropriate canonical guide; report run-specific evidence, defects, and residual risks in the task response, PR description, or test artifacts.
- Consolidate overlapping guidance and replace obsolete sections instead of appending duplicate histories. Preserve current business and safety contracts when cleaning up documentation.
- Do not rewrite the three customer source documents merely for housekeeping; update them only for confirmed requirement changes.
- Before committing, verify that `docs/` contains only the six canonical files
  and `docs/Nicon/`, that the evidence directory follows the structure above,
  and that documentation links and references still resolve.
- When a guide states counts, estimates, statuses or supported languages, cite
  the source version/range or derive them from current code. Do not retain a
  numeric estimate that cannot be reconciled to `Nicon_BreakTask_v1.xlsx`.
- Update seed/demo data when needed to demonstrate normal, empty, error, and edge states.
- Keep manual API examples accurate and environment-appropriate.
- For documentation-only or other non-code changes that cannot affect compiled
  or runtime behavior (for example Markdown guides, customer evidence, or
  repository agent instructions), do not start/rebuild Docker and do not run
  backend builds, frontend builds, lint, unit, integration, or E2E tests.
  Validate only the affected artifacts: review the final diff, run
  `git diff --check`, verify the canonical `docs/` allowlist/evidence layout,
  check local links/anchors, and validate the format or integrity of any changed
  document, spreadsheet, PDF, or other artifact as applicable.
- Run code checks only when the change can affect source behavior, generated
  executable assets, dependencies, schema, deployment configuration, or the
  build/test toolchain. If a task mixes code and documentation, validate the
  code-affected area normally.

Backend:

```bash
docker exec <backend-container> dotnet build
docker exec <backend-container> dotnet format --verify-no-changes
docker exec <backend-container> dotnet test nihomebackend.tests/nihomebackend.tests.csproj
docker exec <backend-container> dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj
docker exec <backend-container> dotnet format --no-restore --verify-no-changes
```

Frontend:

```bash
cd nihomeweb
npm run format:check
npm run lint
npm run build
```

E2E, when required:

```bash
docker compose up -d --build
docker compose ps
curl -f http://localhost:5043/
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test
```

Run the affected E2E spec after its final edit. If Compose or the application
cannot start, report the E2E check as blocked with the startup error; do not
substitute a mock-based browser result for live-stack validation.

Verify affected screens at mobile and tablet widths, including spacing, readable states, and regression-free navigation.

## Git commit policy

Commit every completed feature or bug fix. Keep commits focused and include only files related to the current task.

Before committing:

```bash
git status --short
git diff
git diff --cached
```

Run relevant checks first. Stage explicit paths:

```bash
git add path/to/file1 path/to/file2
```

Never use 'git add -A', 'git add .', or 'git commit -a' blindly. Do not stage unrelated edits, generated files, secrets, credentials, local configuration, or temporary files. Preserve unrelated user changes and stage only the required files or hunks.

Review exactly what will be committed:

```bash
git diff --cached --check
git diff --cached
```

### Commit-message requirements

Follow the 50/72 rule:

- Subject: imperative, specific, and preferably 50 characters or fewer.
- Body: wrap lines at 72 characters or fewer.
- Separate subject and body with one blank line.
- Do not end the subject with a period.
- Do not use vague messages such as 'Update code', 'Fix issue', or 'Changes'.
- Do not create a title-only commit. Explain why the change was needed and what behavior it provides.
- Describe the feature or problem solved, not a list of edited files.

For a feature, list all meaningful delivered functionalities:

```text
Add invoice payment workflow

Add invoice creation, payment processing, and status tracking.
Validate payment requests and return clear errors for invalid invoices.
Cover the workflow with unit and integration tests.
```

For a bug fix, state the root cause and solution:

```text
Fix duplicate payment notifications

The retry handler republished notifications because it did not record the
event before retrying. Persist the event key and make notification handling
idempotent so retries produce one notification. Add regression coverage.
```

Create the commit explicitly:

```bash
git commit -m "<subject>" -m "<body>"
```

After committing:

```bash
git show --stat --oneline HEAD
git status --short
```

Report the commit hash, summary, validation performed, and any remaining working-tree changes. Do not amend, reset, squash, or discard existing work unless explicitly requested.

## Final response

Keep the response concise and include:

### Summary

What was implemented or diagnosed.

### Files changed

Relevant files and the purpose of each change.

### Quality checks

Checks run, plus failures or skipped checks and their reasons.

### Assumptions and risks

Unresolved ambiguity, compatibility concerns, migration risk, or required follow-up.

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- When graphify-out/graph.json exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- Dirty graphify-out/ files are expected after hooks or incremental updates; dirty graph files are not a reason to skip graphify. Only skip graphify if the task is about stale or incorrect graph output, or the user explicitly says not to use it.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).
- A committed graphify-out/graph.json is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild or its hooks.
