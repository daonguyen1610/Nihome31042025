# Playwright browser tests

The Playwright project runs Chromium against the Docker Compose application. CI
uses two independent shards; each shard has its own database and runs with one
worker because some existing specs update shared seeded records.

## Test boundaries

- Keep end-to-end cases focused on real business behavior through the browser
  and integrated API/database. API setup is acceptable for prerequisites; use
  the browser for the action and verify the durable result through the API.
- Use meaningful, generated NICON scenario data and seeded master data. Never
  use production customer records, credentials, or real email delivery.
- Keep endpoint stubs for deliberate network-failure injection only. Tests
  that replace successful business API responses are UI contract checks, not
  end-to-end evidence; migrate them to a separate frontend test layer.
- Do not run viewport matrices, geometry assertions, or screenshot layout
  checks in this project. Review responsive presentation manually in Chrome.
- Do not use direct SQL to force a business state that the UI/API workflow is
  meant to produce. Prefer a supported workflow or verify the preceding step.

The existing suite still contains mock-backed UI checks and old layout checks
that have not yet been migrated. Do not add more of either kind to `smoke/`.

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
BASE_URL=http://localhost:5043 npx playwright test e2e/smoke/admin-precontract-department-controls.spec.ts --workers=1
```

CI builds the backend image with a shard-scoped BuildKit cache, then starts the
already-built image with the isolated E2E mailbox overlay. The image cache is an
optimization only; readiness still verifies that the running backend reports
the current commit SHA.

## Layout

```text
e2e/
  fixtures/         shared browser login and API helpers
  smoke/            currently executed browser checks
```

## Test credentials

Seeded role accounts are defined by backend seeders and mirrored in
`e2e/fixtures/auth.ts`. Read credentials from those development sources rather
than duplicating them in documentation, and never use deterministic test
accounts in an exposed production environment.
