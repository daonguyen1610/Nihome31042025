import { defineConfig, devices } from "@playwright/test";

/**
 * E2E test config for the Nihome frontend.
 *
 * Two test modes via PLAYWRIGHT_SUITE env var:
 *   - "smoke"  (default): runs e2e/smoke/**  — small set of critical journeys, gating CI
 *   - "full"             : runs e2e/full/**  — exhaustive coverage, nightly only
 *
 * BASE_URL points to the running stack. Locally:
 *   docker compose up -d        →  http://localhost:5043
 *   npm run dev (FE only)       →  http://localhost:8080  (with VITE_API_URL set)
 */
const suite = process.env.PLAYWRIGHT_SUITE ?? "smoke";
const baseURL = process.env.BASE_URL ?? "http://localhost:5043";

export default defineConfig({
  testDir: "./e2e",
  testMatch: suite === "full" ? "full/**/*.spec.ts" : "smoke/**/*.spec.ts",
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
  projects:
    suite === "full"
      ? [
          { name: "public", testDir: "./e2e/full/public", use: { ...devices["Desktop Chrome"] } },
          { name: "admin-auth", testDir: "./e2e/full/admin-auth", use: { ...devices["Desktop Chrome"] } },
          { name: "admin-crud", testDir: "./e2e/full/admin-crud", use: { ...devices["Desktop Chrome"] } },
          { name: "cross", testDir: "./e2e/full/cross", use: { ...devices["Desktop Chrome"] } },
        ]
      : [{ name: "smoke", use: { ...devices["Desktop Chrome"] } }],
});
