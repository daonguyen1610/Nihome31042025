import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Browser-side smoke for the /admin/roles matrix UI built in C7.
 *
 * Scope (per AGENTS.md test-layering rules): only what integration
 * tests structurally cannot prove — namely that the SPA actually mounts,
 * fetches /api/admin/rbac/roles + /permissions + per-role permissions,
 * and renders one column per role and one row per permission without
 * client-side errors.
 *
 * CRUD round-trips and authorization rules are covered by
 * nihomebackend.integration.tests/Controllers/RbacControllerTests.cs and
 * e2e/smoke/admin-rbac.spec.ts.
 */
test("matrix page renders dynamic roles and permissions for SUPER_ADMIN", async ({
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
  const res = await page.goto("/admin/roles");
  expect(res?.status(), "page responds 2xx").toBeLessThan(400);

  // Wait until the matrix table has loaded its dynamic columns.
  await expect(page.getByTestId("rbac-col-SUPER_ADMIN")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("rbac-col-ADMIN")).toBeVisible();
  await expect(page.getByTestId("rbac-col-USER")).toBeVisible();
  await expect(page.getByTestId("rbac-col-SALE")).toBeVisible();

  // Permission rows include the well-known catalog codes.
  await expect(page.getByText("dashboard.view").first()).toBeVisible();
  await expect(page.getByText("rbac.roles.manage").first()).toBeVisible();

  // Create-role button is gated by Can; SUPER_ADMIN must see it.
  await expect(page.getByTestId("rbac-create-role")).toBeVisible();

  // System role columns must not expose a delete button (only business roles do).
  await expect(page.getByTestId("rbac-delete-ADMIN")).toHaveCount(0);
  await expect(page.getByTestId("rbac-delete-SUPER_ADMIN")).toHaveCount(0);

  // At least one business role (e.g. SALE from rbac-defaults.json seed)
  // should be rendered with its delete button visible.
  await expect(page.getByTestId("rbac-delete-SALE")).toBeVisible();

  expect(errors, "no console errors on /admin/roles").toEqual([]);
});
