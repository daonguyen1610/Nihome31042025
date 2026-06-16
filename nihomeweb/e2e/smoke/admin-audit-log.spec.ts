import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Browser-side smoke for /admin/activity-log (NIH-229).
 *
 * Scope (per AGENTS.md test-layering rules): only what integration tests
 * structurally cannot prove — the SPA actually mounts, fetches
 * /api/audit-logs + /config, and renders the filter, retention and table
 * shells without client-side errors. CRUD and authorization are covered by
 * nihomebackend.integration.tests/Controllers/AuditLogsControllerTests.cs and
 * nihomebackend.tests/Controllers/AuditLogsControllerTests.cs.
 */
test("activity log page renders shell + retention card for SUPER_ADMIN", async ({
  page,
  loginInBrowserAs,
}) => {
  const errors: string[] = [];
  const isIgnorable = (text: string) =>
    /WebSocket connection to .* failed/i.test(text) || /\[vite\]/i.test(text);
  page.on("pageerror", (err) => {
    if (!isIgnorable(err.message)) errors.push(err.message);
  });
  page.on("console", (msg) => {
    if (msg.type() !== "error") return;
    const text = msg.text();
    if (!isIgnorable(text)) errors.push(text);
  });

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const res = await page.goto("/admin/activity-log");
  expect(res?.status(), "page responds 2xx").toBeLessThan(400);

  // Heading + table shell are part of every render path (loading / empty / data).
  await expect(page.getByTestId("audit-log-title")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("audit-log-filter-card")).toBeVisible();
  await expect(page.getByTestId("audit-log-table")).toBeVisible();

  // Retention card is gated on SUPER_ADMIN; must be present for this user.
  await expect(page.getByTestId("audit-log-retention-card")).toBeVisible();

  expect(errors, "no console errors on /admin/activity-log").toEqual([]);
});

test("activity log page hides retention card for ADMIN (non super)", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.admin);
  const res = await page.goto("/admin/activity-log");
  expect(res?.status(), "page responds 2xx").toBeLessThan(400);

  await expect(page.getByTestId("audit-log-title")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("audit-log-table")).toBeVisible();
  // Retention config is SUPER_ADMIN-only — must not render for ADMIN.
  await expect(page.getByTestId("audit-log-retention-card")).toHaveCount(0);
});
