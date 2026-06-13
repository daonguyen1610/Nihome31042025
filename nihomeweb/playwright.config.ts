import { defineConfig, devices } from "@playwright/test";

/**
 * E2E test config for the Nihome frontend.
 *
 * Scope: real-browser checks that integration tests cannot cover —
 *   - SPA renders cleanly across public routes (no JS errors, React mounted)
 *   - public detail pages render with seeded entities
 *   - deployment-only contracts (CORS, brute-force tolerance)
 *
 * Pure API behavior (CRUD, auth, validation, contracts) lives in
 * `nihomebackend.integration.tests` and is intentionally NOT duplicated here.
 *
 * BASE_URL points to the running stack. Locally:
 *   docker compose up -d        →  http://localhost:5043
 *   npm run dev (FE only)       →  http://localhost:8080  (with VITE_API_URL set)
 */
const baseURL = process.env.BASE_URL ?? "http://localhost:5043";

export default defineConfig({
  testDir: "./e2e",
  testMatch: "smoke/**/*.spec.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 4 : undefined,
  reporter: process.env.CI
    ? [["list"], ["html", { open: "never" }], ["junit", { outputFile: "playwright-report/junit.xml" }]]
    : "list",
  timeout: 30_000,
  expect: { timeout: 5_000 },
  use: {
    baseURL,
    trace: "retain-on-failure",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
    locale: "en-US",
    actionTimeout: 10_000,
    navigationTimeout: 20_000,
  },
  projects: [{ name: "e2e", use: { ...devices["Desktop Chrome"] } }],
});
