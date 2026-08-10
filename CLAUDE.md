# Claude Code Instructions

Read `AGENTS.md` before any non-trivial work in this repository.
`AGENTS.md` is the canonical shared contract for all AI agents (Claude, Codex, Gemini) working here.

---

## Quick Reference — Rules Claude Must Follow

### i18n / Translations
- **Never hardcode display strings** in React components. All user-visible text must use `t("key")` via `useI18n()`.
- When adding new UI strings, add the key to the matching seed file in `nihomebackend/Data/Seeds/i18n/` (`admin-system.json` for `proc.*`, `common.json` for `common.*`, etc.).
- Provide all four languages: `vi`, `en`, `zh`, `ja`.
- Restart the backend so `TranslationSeeder` upserts the new keys into the DB.

### Backend ↔ Frontend alignment
- If the backend response shape changes, update the TypeScript types in `nihomeweb/src/services/`.
- No hardcoded category values, group keys, or option lists in React — fetch from the API.
- Handle loading, error, and empty states in every new UI section.

### EF Core / Migrations
- All schema changes require an EF Core migration generated in a Docker-based .NET 8 SDK environment. The current running backend image does not include `dotnet-ef`, so do not claim that `docker exec nihome31042025-backend dotnet ef ...` works unless the tooling image has first been provisioned.
- Review generated migration and snapshot files before applying them; do not hand-author migration metadata.
- Use `AsNoTracking()` for read-only queries.

### Testing
- Put isolated service logic in `nihomebackend.tests`, HTTP/auth/contract behavior in `nihomebackend.integration.tests`, and browser/deployment-only behavior in the Playwright smoke suite.
- The running backend container mounts only `nihomebackend/`, not the sibling test projects; use the CI-equivalent Docker test environment rather than documenting `docker exec` test commands that cannot resolve those projects.
- Run `dotnet format` to pass the backend linter before closing a task.
- Tests must follow the design patterns already used in the test project.

### Documentation
- When maturing a feature (not just a quick fix), update the relevant doc in `docs/`.
- Design decisions and API changes belong in `docs/` — not only in chat.

### Git workflow
- **Always commit when a feature or bug fix is complete** — do not leave work uncommitted.
- Write commit messages following the **50/72 rule**: subject ≤ 50 chars, body lines ≤ 72 chars.
- Only stage files related to the commit — do not use `git add -A` blindly.

### Quality gates — run before closing any task
```bash
# Frontend
cd nihomeweb && npm run lint && npm run test && npm run build

# Backend
docker exec nihome31042025-backend dotnet build
docker exec nihome31042025-backend dotnet format --verify-no-changes

# Backend unit and integration tests
# Run through the repository's CI-equivalent .NET 8 SDK/Docker test environment;
# the running backend container does not mount the test projects.

# Browser/deployment smoke
docker compose up -d --build
cd nihomeweb && BASE_URL=http://localhost:5043 npx playwright test
```

---

## Required response format after completing a task

Every completed task response must include:

## Summary
What was done and why.

## Files Changed
List of files modified/created.

## Quality Check
Results of lint / build / test runs.

## Assumptions / Risks
Any trade-offs made or things to watch out for.

---

For full project rules see `AGENTS.md`.
For frontend-specific rules see `nihomeweb/AGENTS.md` and `nihomeweb/docs/ai/frontend-playbook.md`.
