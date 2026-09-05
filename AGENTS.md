# AGENTS.md

## Project context

This repository contains a React frontend and an ASP.NET Core 8 backend developed and tested with Docker Compose. Work as a careful senior software engineer: understand existing behavior, make the smallest safe change, and leave the repository verifiable.

## Instruction precedence

Follow instructions in this order:

1. System and platform instructions.
2. User instructions for the current task.
3. This file and repository documentation.
4. Existing code conventions and local defaults.

When repository instructions conflict, use the more specific and recent instruction, document the conflict, and ask the user when the choice could affect business behavior or data.

## Working principles

- Read relevant nearby code, tests, migrations, and documentation before editing.
- Prefer small, focused, reversible changes.
- Preserve existing behavior and public contracts unless the task requires a change.
- Reuse existing services, validators, components, and patterns. Search first, especially in 'src/lib/', 'Services/', and shared frontend utilities.
- Do not refactor unrelated code, add dependencies without justification, or invent requirements, APIs, translations, or business rules.
- Record meaningful trade-offs, assumptions, blocked checks, and residual risks.

## Task lifecycle

For every non-trivial task:

1. Inspect repository state and identify affected code, data, documentation, and test layers.
2. Clarify acceptance criteria and affected API or data contracts.
3. Implement the smallest complete change.
4. Add or update tests at the lowest appropriate test layer.
5. Run relevant checks and investigate failures to distinguish a bad test from a product defect.
6. Review the final diff for scope, security, compatibility, and accidental changes.
7. Update documentation and seed data when required.
8. Commit the completed change using the Git rules below.

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

## Frontend and backend integration

- Keep frontend behavior, backend behavior, DTOs, validation, and API contracts aligned. Update both sides when a contract changes.
- Handle loading, error, empty, success, and responsive states where applicable.
- Do not hardcode business data such as categories in React; fetch it from the backend API or an approved shared configuration source.
- Do not hardcode 'localhost' or deployment-specific media/API hosts. Store backend media as host-relative paths such as '/images/example.png', resolved through a shared helper.
- Follow 'nihomeweb/CLAUDE.md' for web UI conventions when it exists.
- Centralize content translations in '/admin/translations'. Add i18n keys rather than embedding display text.
- Keep translation keys and seeded content aligned in all supported languages.
- For entities with fields such as 'NameVi', 'Name', 'NameZh', and 'NameJa', populate every required language field on every write path: create, seed, migration, and legacy-data auto-create. Do not rely on read-time fallback.

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

All implementation decisions that affect business behavior must comply with 'docs/Nicon-QLVH.md', 'docs/Nicon_BreakTask_v1.xlsx' and 'docs/Nicon-workflow.md'. These documents are the authoritative source for customer expectations and business workflows.

- Before implementing, research the relevant documentation, existing behavior, code paths, data contracts, and tests. Do not start coding from assumptions.
- Think through the actors, business intent, workflow states, rules, permissions, dependencies, edge cases, and expected outcomes before choosing a solution.
- State unresolved ambiguity and request clarification when it could change business behavior or data; do not invent missing requirements.
- Prefer the smallest solution that satisfies the confirmed business flow, avoids unnecessary work, and improves delivery efficiency without weakening correctness or validation.
- After implementation, verify the result against the documented workflow and customer expectation before declaring the task complete.

## Hard-delete policy

Hard delete is a business operation, not a direct 'DbSet.Remove' call. Every user-facing root delete must follow 'docs/hard-delete-convention.md'.

- Add an authorized 'GET .../{id}/deletion-impact' endpoint. Classify every dependent group as 'Delete', 'Unlink', or 'Block', including files and external bindings.
- Require a typed resource code and deterministic plan token in the 'DELETE' body. Recompute the plan inside the delete transaction and return '409 Conflict' if it changed.
- Enforce authorization, confirmation, concurrency, and blockers on the server.
- Execute aggregate database changes in one transaction: delete owned records, unlink independent records, and preserve unrelated roots.
- Never silently orphan or destroy files. Use existing document services for cleanup, block while cleanup is pending, and disclose external folders that are unlinked but preserved.
- Use the shared frontend deletion-impact dialog. Do not use 'window.confirm' or client-side loops to delete an aggregate graph.
- Seed dependency labels and messages in Vietnamese, English, Chinese, and Japanese.
- Integration tests must cover authorization, counts/actions, confirmation, blockers, stale plans, concurrency, cleanup/unlinking, and unchanged state after rejection.
- Seeded and demo roots follow the same contract as user-created data.

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
- E2E tests ('nihomeweb/e2e/smoke'): narrow real-browser rendering, SPA mounting, JavaScript errors, route rendering, and deployed-stack wiring such as CORS and health checks. API-only behavior belongs in integration tests.
- Covers Happy path, Negative path testing. Ensure all test cases to cover business scenarios of the customers.
- Use high quality data for testing with the real use cases.

Use this rule: pure logic → unit; HTTP/auth/persistence contract → integration; browser or deployed-stack behavior → E2E. Tests must detect defects, not merely reproduce the implementation. Find the root cause before changing a failing test or product code.

## Documentation and quality checks

- Update 'docs/' when behavior, configuration, API contracts, workflows, or operations change.
- Update seed/demo data when needed to demonstrate normal, empty, error, and edge states.
- Keep manual API examples accurate and environment-appropriate.
- Run checks relevant to the changed area.

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
npm run lint
npm run build
```

E2E, when required:

```bash
docker compose up -d --build
cd nihomeweb
BASE_URL=http://localhost:5043 npx playwright test
```

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

