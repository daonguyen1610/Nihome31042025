# End-to-end tests (Playwright)

The repository currently has one Playwright project named `e2e`. It runs Chromium desktop tests matching `e2e/smoke/**/*.spec.ts` against the integrated Docker application. There is no committed full/nightly suite or sharded Playwright project set.

The smoke directory currently covers public rendering, authentication, permission-driven admin routes, CRM, design, permitting, construction workflows, and deployment contracts. Treat file/test counts as snapshots because parameterized role matrices expand at runtime.

## Running locally

```bash
# Start SQL Server plus the ASP.NET application that builds/serves the SPA.
docker compose up -d --build

cd nihomeweb
npm ci
npm run test:e2e:install

# Run all committed Playwright specs.
BASE_URL=http://localhost:5043 npx playwright test

# Run one feature.
BASE_URL=http://localhost:5043 npx playwright test e2e/smoke/admin-handover.spec.ts --workers=1
```

The `test:e2e:smoke` and `test:e2e:full` package scripts currently set `PLAYWRIGHT_SUITE`, but `playwright.config.ts` does not consume that variable; both therefore select the same smoke specs. Prefer `npx playwright test` until those scripts/configuration are simplified or a real full suite is added.

## Layout

```text
e2e/
  fixtures/         shared browser login and API helpers
  smoke/            all currently executed specs
```

Playwright is configured with full parallelism and four CI workers. Specs that mutate shared seeded roles or records must use unique data, isolate cleanup, or run serially to avoid cross-worker interference.

## Test layering

- Unit tests prove isolated service rules, validation matrices, and helpers.
- Backend integration tests prove HTTP contracts, model binding, authorization, and persistence round-trips.
- Playwright should prove browser rendering, navigation, downloads, responsive interactions, and deployment-only wiring.

Some existing smoke specs still make API-only CRUD or RBAC assertions. That is known layering debt: move assertions that `HttpClient` can prove into `nihomebackend.integration.tests` rather than copying that pattern into new browser specs.

## Project handover smoke

The NIH-144 smoke test signs in through the real browser and verifies that an authorized user can render the localized handover workspace without leaking raw translation keys. The broader role-to-route contract is covered by `admin-rbac-matrix.spec.ts`; handover service and HTTP edge cases remain in backend test projects.

## Test credentials

Seeded role accounts are defined by backend seeders and mirrored in `e2e/fixtures/auth.ts`. Read credentials from those development sources rather than duplicating them in documentation, and never use deterministic test accounts in an exposed production environment.
