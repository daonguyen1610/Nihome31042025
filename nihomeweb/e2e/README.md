# End-to-end tests (Playwright)

Two suites:

| Suite | Location | When it runs |
|---|---|---|
| **Smoke** | `e2e/smoke/**` | Every PR & push (`ci.yml`). ~8 critical journeys, must stay green. |
| **Full**  | `e2e/full/**`  | Nightly cron + manual dispatch (`e2e-full.yml`). Exhaustive coverage, sharded across `public`, `admin-auth`, `admin-crud`, `cross` projects. |

## Running locally

```bash
# 1. Bring the full stack up so the API + seeded DB are reachable
docker compose up -d --build

# 2. Install Playwright browsers (once)
cd nihomeweb
npm install
npm run test:e2e:install

# 3. Run the smoke suite (default) against http://localhost:5043
npm run test:e2e:smoke

# Or run the full suite
npm run test:e2e:full

# Point at a different deployment
BASE_URL=https://staging.nihome.vn npm run test:e2e:smoke
```

## Layout

```
e2e/
  fixtures/         # shared Playwright fixtures (auth, api client)
  smoke/            # ~8 critical journeys — gates every PR
  full/
    public/         # every public page renders + i18n
    admin-auth/     # login, logout, role gates
    admin-crud/    # CRUD per entity (copy projects.spec.ts as a template)
    cross/          # CORS, rate limiting, security smoke
```

## Adding a new CRUD spec

Copy `e2e/full/admin-crud/projects.spec.ts`, change the endpoint and payload. Each CRUD spec must be **self-contained** — create its own data with a unique slug, then delete it at the end.

## Test credentials

The seeded users in `nihomebackend/Data/DbSeeder.cs` are mirrored in `e2e/fixtures/auth.ts`:

| Role | Phone | Password |
|---|---|---|
| SUPER_ADMIN | `0335240370` | `Admin@123` |
| ADMIN | `0911111111` | `Admin@123` |

Update both files together if you change them.
