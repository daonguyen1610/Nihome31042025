# Claude Code Instructions

Read `AGENTS.md` before any non-trivial work in this repository.
`AGENTS.md` is the canonical shared contract for all AI agents (Claude, Codex, Gemini) working here.

---

## Quick Reference — Rules Claude Must Follow

### i18n / Translations
- **Never hardcode display strings** in React components. All user-visible text must use `t("key")` via `useI18n()`.
- When adding new UI strings, add the key to the matching seed file in `nihomebackend/Data/Seeds/` (`admin-system.json` for `proc.*`, `common.json` for `common.*`, etc.).
- Provide all four languages: `vi`, `en`, `zh`, `ja`.
- Restart the backend so `TranslationSeeder` upserts the new keys into the DB.

### Backend ↔ Frontend alignment
- If the backend response shape changes, update the TypeScript types in `nihomeweb/src/services/`.
- No hardcoded category values, group keys, or option lists in React — fetch from the API.
- Handle loading, error, and empty states in every new UI section.

### EF Core / Migrations
- All schema changes require a migration. Use `dotnet ef migrations add <Name>` inside the container, or create manually with `[Migration]` + `[DbContext]` attributes.
- Update `AppDbContextModelSnapshot.cs` when creating migrations manually.
- Use `AsNoTracking()` for read-only queries.

### Testing
- When building or modifying a backend feature, **write or update test cases** in `nihomebackend.tests`.
- Run `dotnet format` to pass the backend linter before closing a task.
- Tests must follow the design patterns already used in the test project.

### Documentation
- When maturing a feature (not just a quick fix), update the relevant doc in `docs/`.
- Design decisions and API changes belong in `docs/` — not only in chat.

### Git workflow
- **Always commit when a feature or bug fix is complete** — do not leave work uncommitted.
- Write commit messages following the **50/72 rule**: subject ≤ 50 chars, body lines ≤ 72 chars.
- Only stage files related to the commit — do not use `git add -A` blindly.
- After committing, **create a pull request** targeting `main` using `gh pr create` with a descriptive title and body.
- The development branch should be rebased onto `main` before the PR is opened.

### Quality gates — run before closing any task
```bash
# Frontend
cd nihomeweb && npm run lint

# Backend
dotnet build          # or: docker exec nihome31042025-backend dotnet build
dotnet format --verify-no-changes

# Full stack
docker compose up --build
```

---

## Required response format after completing a task

Every completed task response must include:

### Summary
What was done and why.

### Files Changed
List of files modified/created.

### Quality Check
Results of lint / build / test runs.

### Assumptions / Risks
Any trade-offs made or things to watch out for.

---

For full project rules see `AGENTS.md`.
For frontend-specific rules see `nihomeweb/AGENTS.md` and `nihomeweb/docs/ai/frontend-playbook.md`.
