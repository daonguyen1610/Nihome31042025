# AGENTS.md

## Project Context

This is a React + ASP.NET Core 8 project running with Docker Compose.

The assistant should behave like a careful senior software engineer working inside this repository.

---

## Critical Instructions

You MUST follow ALL sections in this file.

Do NOT skip any rule even if it is not explicitly mentioned in the prompt.

---

## Core Rules

* Think before editing.
* Prefer small, safe, surgical changes.
* Do not refactor unrelated code.
* Do not introduce new dependencies unless necessary.
* Follow existing project structure and conventions.
* Preserve existing behavior unless explicitly requested.
* Read nearby code before modifying.
* Do not invent requirements or APIs.

---

## Business Validation Skill Routing

For non-trivial features and business-rule changes, use the repository skills in this order after implementation:

1. `.github/skills/senior-business-analyst/SKILL.md` — validate business intent, actors, rules, lifecycle, contracts, and requirement-to-test traceability.
2. `.github/skills/business-functional-qa/SKILL.md` — design and execute risk-based functional validation from the approved business contract and BA handoff.

Use both skills before declaring a feature business-ready. The BA review approves the business contract as the QA test basis; the QA review proves release readiness with evidence at the correct test layer. Agents without native skill discovery must read and follow these files directly.

Use a separate agent or clean review context for BA and QA validation when available. If the same agent must validate its implementation, treat the implementation summary as untrusted, re-read source evidence, and record that independence limitation as a risk. Report ambiguities, missing evidence, defects, blocked checks, and residual risks explicitly. Only the product or business owner may accept requirement risk; only fix findings when the user requests remediation, then rerun the affected validation.

---

## Fast Delivery Rules

* Optimize for speed but keep changes safe.
* Prefer simple working solutions.
* Avoid over-engineering.
* Reuse existing code whenever possible.
* If trade-offs are made, clearly state them.

---

## Git Branching

* Always create a new branch before starting a task unless already on a task branch or instructed otherwise.
* Small documentation or instruction-only changes may stay on the current branch when the user requests a quick update.
* Branch from `main` unless instructed otherwise.
* Do not switch branches if there are uncommitted user changes that could be disrupted; ask first.

---

## Code Quality Rules

* Keep code readable and maintainable.
* Use meaningful naming.
* Keep methods small and focused.
* Avoid duplicate logic.
* Before adding a new helper/utility (URL resolution, localized-name selection, formatting, etc.), search the codebase (`src/lib/`, `Services/`) for an existing one that already does the job and reuse or extend it instead of writing a parallel implementation.
* Avoid deep nesting.
* Remove unused code.

---

## Clean Code Rules

* Follow SOLID principles where practical.
* Keep business logic out of controllers.
* Use clear and explicit logic.
* Handle edge cases properly.
* Avoid magic values.

---

## Design Pattern Rules

* Use patterns only when necessary.
* Prefer patterns already used in the project.
* Avoid adding unnecessary abstraction layers.

---

## React + ASP.NET Rules

* Keep frontend and backend aligned.
* Do not break API contracts.
* Update frontend if backend changes.
* Handle loading, error, and empty states.
* Update the translation and content keys in the ASP .NET DbSeeder aligned with frontend.
* Check `npm run lint` and fix the issues.
* No hardcode like the category in the React. All need to fetch from the backend API to avoid the hardcode.
* Do not hardcode frontend media/backend hosts such as localhost. Backend-served media must be stored as host-relative paths like `/images/...`; frontend helpers may resolve those paths against the current API origin.
* Entities with per-language fields (e.g. `NameVi`/`Name`/`NameZh`/`NameJa`) must have every language field populated on every write path — create, auto-create-from-legacy-data, seed, and migration. Do not rely on read-time fallback to compensate for missing writes.
* Follow the nihomeweb/CLAUDE.md for strictly developing the web UI application.
**The content translations must be to updated in Content Translations**
- Must be centralized the content translations in the `/admin/translations` endpoint.

---

## Input Validation Rules

A field that accepts anything is a field that will be filled with anything. Every
user-writable field needs its rules decided once and enforced on both sides.

**The check to perform before finishing any form or write endpoint:** for each
field the user can type into, name the rule that rejects a bad value, and point
at where it runs on the server. If either answer is missing, the field is not
done.

* **Validate on both layers.** The frontend tells the user before the request
  leaves; the server is what actually protects the data, because the API can be
  called directly. Frontend-only validation is not validation.
* **Format, not just presence.** "Has a value" and "has a usable value" are
  different rules. Requiring one of phone or email says nothing about whether
  either can be dialled or delivered to.
* **Do not trust `[EmailAddress]` or `[Phone]` to mean what they say.**
  `[EmailAddress]` accepts `345@434`; `[Phone]` is looser still. Where the shape
  matters, write the rule explicitly.
* **Reuse the shared validators; do not write a fourth regex.** Contact rules
  live in `nihomebackend/Services/ContactValidation.cs` and its mirror
  `nihomeweb/src/lib/validation.ts`. Extend those when the rule changes, and keep
  the two in step — the comment at the top of each says so.
* **Validation messages are user-facing.** Write them in Vietnamese, name the
  offending field, and show an example of an accepted value. Add the frontend
  copy as an i18n key in all four languages, like any other display string.
* **Test data has to satisfy the rules.** A fixture that generates
  `"0911" + Guid` is producing letters where digits belong. When a validator
  turns fixtures red, fix the fixtures — that red is the validator working.

## Hard Delete Rules

Hard delete is a business operation, not a direct `DbSet.Remove` call. Every
user-facing root delete must use the shared preview-and-confirm contract
described in `docs/hard-delete-convention.md`.

* Provide an authorized `GET .../{id}/deletion-impact` endpoint before the
  delete endpoint. Classify every dependent group as `Delete`, `Unlink`, or
  `Block`, including files and external-system bindings.
* Require a typed resource code and the preview's deterministic plan token in
  the DELETE body. Recompute the plan inside the delete transaction and return
  `409 Conflict` when it changed.
* Enforce authorization, confirmation, concurrency, and blockers on the server.
  Hiding a button or disabling a dialog is not protection.
* Execute aggregate database changes in one transaction. Delete aggregate-owned
  records, unlink independent business records, and preserve unrelated roots.
* Never silently orphan or destroy files. Stage managed-file cleanup through
  the existing document services; block while cleanup is pending. Explicitly
  disclose external folders that will only be unlinked and preserved.
* Use the shared frontend deletion-impact dialog. Do not use `window.confirm`,
  generic confirmation text, or parallel client-side loops to delete an
  aggregate graph.
* Seed every user-visible dependency label and message in all four languages:
  Vietnamese, English, Chinese, and Japanese.
* Integration tests must prove preview authorization, dependency counts and
  actions, invalid/missing confirmation, blockers, stale plans/concurrency,
  successful cleanup/unlinking, and unchanged state after rejected requests.
* Seeded and demo roots follow the same hard-delete contract as user-created
  data. Do not add undeletable seed-only guards.

---

## Docker Development

* This project runs in Docker. Do not run `dotnet` or database commands directly on the host.
* Use `docker exec nihome31042025-backend <command>` for backend tasks.
* Run migrations inside the container: `docker exec nihome31042025-backend dotnet ef migrations add <Name>`

---

## ASP.NET Core Rules

* Keep controllers thin.
* Put logic in services.
* Use dependency injection.
* Use DTOs instead of entities.
* Use async/await for I/O.

---

## Entity Framework Rules

* Do not change schema without migrations.
* Use EF Core migrations for all changes.
* Avoid N+1 queries.
* Use AsNoTracking for read operations.

---

## EF Migration Rules

### Create Migration

```bash
dotnet ef migrations add <Name>
```

### Apply Migration

```bash
dotnet ef database update
```

### Remove Migration

```bash
dotnet ef migrations remove
```

### Generate Script

```bash
dotnet ef migrations script
```

Always review migration files before applying.

---

## ASP.NET Testing Rules

* When develop the feature, please cover the test cases properly in `nihomebackend.tests`
* Ensure the linter with `dotnet format`
* Ensure the test cases cover the feature, and follow the design pattern

---

## Test Layering — avoid duplication across layers

Each test layer has a clear, non-overlapping responsibility. Do not duplicate
coverage between layers — pick the lowest layer that can prove the behavior.

### Unit tests — `nihomebackend.tests`

* Service-level logic in isolation: validation, branching, JSON shape handling,
  cache invalidation, file-resolution helpers, etc.
* Use InMemory EF and Moq. No HTTP, no docker.
* This is where edge cases and validation matrices live.

### Integration tests — `nihomebackend.integration.tests`

* Boots the real ASP.NET pipeline via `WebApplicationFactory` (controllers,
  middleware, auth, model binding, EF).
* Owns **all** API behavior: CRUD round-trips, auth/role enforcement,
  validation 400s, contract shape, error paths.
* Fast (seconds), runs on every PR, no docker required.

### E2E tests — `nihomeweb/e2e/smoke` (Playwright)

* Scope is intentionally narrow: only what integration tests structurally
  cannot cover.
  * Real-browser rendering: SPA mounts, no JS errors, public routes resolve,
    detail pages render with seeded data.
  * Deployment-only contracts against the live `docker compose` stack:
    CORS preflight, brute-force tolerance, health endpoint, etc.
* **Do not** add API-only specs here (CRUD round-trips, auth checks,
  validation 400s). If the assertion can be made with `HttpClient` against
  `WebApplicationFactory`, it belongs in `nihomebackend.integration.tests`.
* Single Playwright project, single CI job, runs on every PR + push to main.

### Rule of thumb when adding a test

1. Pure logic / a service method? → unit test.
2. HTTP contract, validation, auth, persistence round-trip? → integration test.
3. Does the user need to actually see a page render, or does the deployed
   stack need to wire up correctly? → E2E.

If a behavior is already proven at a lower layer, do not re-assert it at a
higher layer.

## Documentation

* When maturing the features, please update the documentation in `docs/`

### Manual API Test

Example:

```bash
curl -X GET http://localhost:5000/api/resource
```

---

## Quality Check

* Test manual with playwright in the integrated browser. Ensure all the changes match with the test.
* Ensure the quality of code: Clean code, no hardcode, reusable functions.
* Ensure all test passed: unittest, integration test, E2E test.
* Ensure no breaking changes the UI, the UI must be cleaned, easy to use for the users.
* Ensure the UI must be clean in every responsive. For example: Scale to mobile, tablet we should use cardview to show, ensure the spacings between the components.
* Ensure write the tests to cover all scenarios.
* Ensure the Seeder mock the data to showcase the functionalities.

### Backend

```bash
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format --verify-no-changes
docker exec nihome31042025-backend dotnet test nihomebackend.tests/nihomebackend.tests.csproj
dotnet test nihomebackend.integration.tests/nihomebackend.integration.tests.csproj
```

### Frontend

```bash
cd nihomeweb && npm run lint && npm run build
```

### E2E (browser + deployment smoke)

Requires the full stack to be running locally:

```bash
docker compose up -d --build
cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test
```

### Docker

```bash
docker compose up --build
```

---

## Final Response Format

Every response MUST include:

## Summary

## Files Changed

## Quality Check

## Assumptions / Risks

---

## Response Style

* Be clear and concise
* Focus on practical solutions
* Highlight risks when needed

## Git Commit Rules

These rules apply whenever a feature, bug fix, refactor, or other change is
complete and ready to be recorded in Git.

### Before committing

1. Inspect the working tree and review the diff:

   ```bash
   git status --short
   git diff
   git diff --cached
   ```

2. Run the relevant tests, linters, formatters, and build checks for the
   change. Do not commit when required checks are failing unless the user
   explicitly asks to commit the failing state.

3. Stage only files that belong to the current change. Use explicit paths:

   ```bash
   git add path/to/file1 path/to/file2
   ```

   Never use `git add -A`, `git add .`, or `git commit -a` blindly. Do not
   include unrelated edits, generated files, secrets, credentials, local
   configuration, or temporary files.

4. Review the staged patch before committing:

   ```bash
   git diff --cached --check
   git diff --cached
   ```

5. If unrelated changes are already present, preserve them and commit only
   the files and hunks required for the current task.

### Commit messages

1. Follow the 50/72 rule:

   - Keep the subject line at 50 characters or fewer when practical.
   - Wrap body lines at 72 characters or fewer.
   - Use imperative mood: `Add`, `Fix`, `Update`, `Remove`, or `Refactor`.
   - Do not end the subject line with a period.

2. Do not write a title-only commit. Every commit must include a body that
   explains the purpose and behavior of the change.

3. Keep the message simple and describe the feature or problem being solved,
   not the mechanics of the Git diff.

4. Use this structure:

   ```text
   <imperative summary, 50 characters or fewer>

   <why the change was needed and what behavior it provides>
   <relevant validation or important implementation constraint>
   ```

5. For a feature, list the user-visible functionalities delivered:

   ```text
   Add invoice payment workflow

   Add invoice creation, payment processing, and payment-status tracking.
   Validate payment requests and return clear errors for invalid invoices.
   Cover the workflow with unit and integration tests.
   ```

6. For a bug fix, state both the root cause and the solution:

   ```text
   Fix duplicate payment notifications

   The retry handler published the same notification more than once because
   it did not record the event before retrying. Persist the event key and
   make notification handling idempotent so retries produce one notification.
   Add regression coverage for repeated delivery.
   ```

7. Do not include vague messages such as `Update code`, `Fix issue`, or
   `Changes`.

### Creating the commit

Use an explicit commit command with the prepared message:

```bash
git commit -m "<subject>" -m "<body>"
```

After committing, verify the result:

```bash
git show --stat --oneline HEAD
git status --short
```

The final status should contain no unintended staged or committed files.
Report the commit hash, summary, validation performed, and any remaining
working-tree changes.

### Commit scope

- Prefer one focused commit per feature or bug fix.
- Keep unrelated refactors, formatting-only changes, dependency upgrades, and
  cleanup in separate commits unless they are required for the current work.
- Do not rewrite, squash, reset, or discard existing commits or user changes
  unless explicitly requested.
- Never commit secrets or sensitive values. If a secret is found, stop and
  report it instead of staging it.