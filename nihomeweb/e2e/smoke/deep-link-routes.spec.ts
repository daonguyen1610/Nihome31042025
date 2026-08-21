import { expect } from "@playwright/test";
import { test, TEST_USERS } from "../fixtures/auth";
import { createOwnCustomer } from "../fixtures/designProjects";

/**
 * Notification links use path form — /admin/leads/{id}, /admin/opportunities/{id},
 * /admin/roles/{id} — but App.tsx declared no matching routes, so every one of
 * those notifications landed on the 404 page. These cover the routes that fix it.
 */
test.describe("notification deep links", () => {
  test("a lead url opens that lead, not the 404 page", async ({ page, api, loginAs, loginInBrowserAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    await loginInBrowserAs(page, TEST_USERS.superAdmin);

    const listed = await api.get("/api/leads?pageSize=1", { headers: authHeader });
    expect(listed.ok()).toBeTruthy();
    const lead = (await listed.json()).items[0];
    test.skip(!lead, "no seeded lead to open");

    await page.goto(`/admin/leads/${lead.id}`);

    await expect(page.getByText("404", { exact: false })).toHaveCount(0);
    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("dialog")).toContainText(lead.name, { timeout: 15_000 });
  });

  test("an opportunity url opens that opportunity", async ({ page, api, loginAs, loginInBrowserAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    await loginInBrowserAs(page, TEST_USERS.superAdmin);

    const listed = await api.get("/api/opportunities?pageSize=1", { headers: authHeader });
    expect(listed.ok()).toBeTruthy();
    const opp = (await listed.json()).items[0];
    test.skip(!opp, "no seeded opportunity to open");

    await page.goto(`/admin/opportunities/${opp.id}`);

    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("dialog")).toContainText(opp.name, { timeout: 15_000 });
  });

  test("the ?open= form still works alongside the route", async ({ page, api, loginAs, loginInBrowserAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    await loginInBrowserAs(page, TEST_USERS.superAdmin);

    const listed = await api.get("/api/opportunities?pageSize=1", { headers: authHeader });
    const opp = (await listed.json()).items[0];
    test.skip(!opp, "no seeded opportunity to open");

    await page.goto(`/admin/opportunities?open=${opp.id}`);

    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("dialog")).toContainText(opp.name, { timeout: 15_000 });
  });

  test("a customer ?open= url opens that customer", async ({ page, api, loginAs, loginInBrowserAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    await loginInBrowserAs(page, TEST_USERS.superAdmin);

    // Own customer rather than the newest in the list: another spec's cleanup
    // can delete that one out from under this test mid-run.
    const customerId = await createOwnCustomer(api, authHeader, "DeepLink");
    const detail = await api.get(`/api/customers/${customerId}`, { headers: authHeader });
    const customer = await detail.json();

    await page.goto(`/admin/customers?open=${customerId}`);

    await expect(page.getByRole("dialog")).toBeVisible({ timeout: 15_000 });
    await expect(page.getByRole("dialog")).toContainText(customer.name, { timeout: 15_000 });
  });

  test("a role url reaches the roles page without a 404", async ({ page, api, loginAs, loginInBrowserAs }) => {
    const token = await loginAs(TEST_USERS.superAdmin);
    const authHeader = { Authorization: `Bearer ${token}` };
    await loginInBrowserAs(page, TEST_USERS.superAdmin);

    const listed = await api.get("/api/admin/rbac/roles", { headers: authHeader });
    expect(listed.ok()).toBeTruthy();
    const role = (await listed.json())[0];
    test.skip(!role, "no seeded role to open");

    await page.goto(`/admin/roles/${role.id}`);

    await expect(page.locator(`[data-role-id="${role.id}"]`).first()).toBeVisible({ timeout: 15_000 });
  });
});
