import { test, expect, TEST_USERS } from "../fixtures/auth";

/**
 * Browser-side smoke for the focused role editor and comparison matrix.
 *
 * Scope (per AGENTS.md test-layering rules): only what integration
 * tests structurally cannot prove — namely that the SPA actually mounts,
 * fetches /api/admin/rbac/roles + /permissions + per-role permissions,
 * and renders the role-focused editor plus the optional matrix without
 * client-side errors.
 *
 * CRUD round-trips and authorization rules are covered by
 * nihomebackend.integration.tests/Controllers/RbacControllerTests.cs and
 * e2e/smoke/admin-rbac.spec.ts.
 */
test("role editor renders dynamic roles and the comparison matrix for SUPER_ADMIN", async ({
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

  // The primary workspace focuses on one role instead of rendering every role
  // as a wide table column.
  await expect(page.getByTestId("rbac-role-editor")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByTestId("rbac-role-SUPER_ADMIN")).toBeVisible();
  await expect(page.getByTestId("rbac-role-ADMIN")).toBeVisible();
  await expect(page.getByTestId("rbac-role-USER")).toBeVisible();
  await expect(page.getByTestId("rbac-role-SALE")).toBeVisible();

  // Search exposes matching permission groups and their well-known codes.
  await page.getByTestId("rbac-role-SALE").click();
  await page.locator("#rbac-search").fill("dashboard.view");
  await expect(page.getByText("dashboard.view", { exact: true })).toBeVisible();
  await page.getByLabel("SALE dashboard.view").click();
  await expect(page.getByTestId("rbac-save-SALE")).toBeEnabled();
  await page.getByTestId("rbac-role-editor").getByRole("button", { name: /Hoàn tác|Reset/i }).click();
  await expect(page.getByTestId("rbac-save-SALE")).toBeDisabled();

  // Create-role button is gated by Can; SUPER_ADMIN must see it.
  await expect(page.getByTestId("rbac-create-role")).toBeVisible();
  await page.getByTestId("rbac-create-role").click();
  await page.getByTestId("rbac-create-group").selectOption({ label: "Thiết kế" });
  await expect(page.getByTestId("rbac-import-baseline")).toBeVisible();
  await page.getByTestId("rbac-create-cancel").click();

  // System roles are read-only; business roles expose their actions.
  await page.getByTestId("rbac-role-ADMIN").click();
  await expect(page.getByTestId("rbac-delete-ADMIN")).toHaveCount(0);
  await page.getByTestId("rbac-role-SUPER_ADMIN").click();
  await expect(page.getByTestId("rbac-delete-SUPER_ADMIN")).toHaveCount(0);
  await page.getByTestId("rbac-role-SALE").click();
  await expect(page.getByTestId("rbac-delete-SALE")).toBeVisible();

  // Department groups expose their independent one-time permission baseline.
  await page.getByTestId("rbac-view-groups").click();
  await expect(page.getByTestId("rbac-groups-editor")).toBeVisible();
  await expect(page.getByTestId("rbac-group-CRM")).toBeVisible();
  await expect(page.getByTestId("rbac-group-HR_ADMIN")).toBeVisible();
  await expect(page.getByTestId("rbac-group-DESIGN")).toBeVisible();
  await expect(page.getByTestId("rbac-group-CONSTRUCTION")).toBeVisible();
  await expect(page.getByTestId("rbac-group-FINANCE")).toBeVisible();
  await page.getByTestId("rbac-group-DESIGN").click();
  await expect(page.getByTestId("rbac-save-baseline")).toBeDisabled();

  // Administrators can still switch to a dense comparison matrix.
  await page.getByTestId("rbac-view-editor").click();
  await page.locator("#rbac-search").fill("");
  await page.getByTestId("rbac-view-matrix").click();
  await expect(page.getByTestId("rbac-col-SUPER_ADMIN")).toBeVisible();
  await expect(page.getByTestId("rbac-col-ADMIN")).toBeVisible();
  await expect(page.getByTestId("rbac-col-USER")).toBeVisible();
  await expect(page.getByTestId("rbac-col-SALE")).toBeVisible();
  await expect(page.getByText("rbac.roles.manage", { exact: true }).first()).toBeVisible();

  expect(errors, "no console errors on /admin/roles").toEqual([]);
});

test("role editor stays focused and overflow-free on mobile and tablet", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);

  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    await page.goto("/admin/roles");
    await expect(page.getByTestId("rbac-role-editor")).toBeVisible({ timeout: 15_000 });
    await page.getByTestId("rbac-role-SALE").click();
    await page.locator("#rbac-search").fill("crm.opportunities.view");
    await expect(page.getByText("crm.opportunities.view", { exact: true })).toBeVisible();

    const dimensions = await page.locator("html").evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }));
    expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth);

    await page.getByTestId("rbac-view-groups").click();
    await expect(page.getByTestId("rbac-groups-editor")).toBeVisible();
    const groupDimensions = await page.locator("html").evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }));
    expect(groupDimensions.scrollWidth).toBeLessThanOrEqual(groupDimensions.clientWidth);
  }
});
