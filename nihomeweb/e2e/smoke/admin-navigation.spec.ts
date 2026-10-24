import { test, expect, TEST_USERS } from "../fixtures/auth";

for (const width of [1440, 768, 390]) {
  test(`admin navigates from Roles without reloading at ${width}px`, async ({
    page,
    loginInBrowserAs,
  }) => {
    test.setTimeout(90_000);
    await page.setViewportSize({ width, height: 900 });
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(message.text());
    });
    await page.addInitScript(() => {
      localStorage.setItem("nicon.admin.sidebar.collapsed", "1");
      localStorage.setItem("nicon_lang", "en");
    });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/roles");
    await expect(page.getByTestId("rbac-role-editor")).toBeVisible();
    const documentId = await page.evaluate(() => {
      const id = crypto.randomUUID();
      Object.assign(window, { navigationDocumentId: id });
      return id;
    });
    for (const [path, heading] of [
      ["/admin/users", "User management"],
      ["/admin/customers", "Customer Management"],
      ["/admin/design-projects", "Design project management"],
      ["/admin/roles", "Roles & Permissions"],
      ["/admin/settings", "Settings"],
    ]) {
      const sidebar = page.locator("aside").first();
      if (width < 1024) {
        await page.locator("header button").first().click();
        const closedGroups = sidebar.locator('nav button[aria-expanded="false"]');
        while (await closedGroups.count()) {
          await closedGroups.first().click();
        }
      }
      await sidebar.locator(`a[href="${path}"]`).click();
      await expect(page).toHaveURL(new RegExp(`${path}$`));
      await expect(
        page.locator("main").getByRole("heading", { name: heading, exact: true }),
      ).toBeVisible();
      if (path !== "/admin/roles") {
        await expect(page.getByTestId("rbac-role-editor")).toHaveCount(0);
      }
      expect(await page.evaluate(() => Reflect.get(window, "navigationDocumentId"))).toBe(
        documentId,
      );
      expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(
        width,
      );
    }
    await page.goBack();
    await expect(page.getByTestId("rbac-role-editor")).toBeVisible();
    await page.goForward();
    await expect(page).toHaveURL(/\/admin\/settings$/);
    await expect(page.getByTestId("rbac-role-editor")).toHaveCount(0);
    expect(errors).toEqual([]);
  });
}

test("role draft survives permission refetch and reset uses the latest server snapshot", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const code = `NICON_NAV_${Date.now()}`;
  const created = await api.post("/api/admin/rbac/roles", {
    headers,
    data: { code, name: "NICON navigation draft review", permissions: ["dashboard.view"] },
  });
  expect(created.status()).toBe(201);
  const roleId = (await created.json()).id as number;
  try {
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto(`/admin/roles/${roleId}`);
    await page.locator("#rbac-search").fill("dashboard.view");
    const permission = page.getByRole("checkbox", { name: `${code} dashboard.view`, exact: true });
    await expect(permission).toBeChecked();
    await permission.uncheck();
    await expect(page.getByTestId(`rbac-save-${code}`)).toBeEnabled();
    const updated = await api.put(`/api/admin/rbac/roles/${roleId}/permissions`, {
      headers,
      data: { permissions: ["dashboard.view", "crm.customers.view"] },
    });
    expect(updated.ok(), await updated.text()).toBeTruthy();
    const refreshed = page.waitForResponse(
      (response) =>
        response.url().endsWith(`/api/admin/rbac/roles/${roleId}/permissions`) &&
        response.request().method() === "GET",
    );
    await page.evaluate(() => window.dispatchEvent(new Event("visibilitychange")));
    await refreshed;
    await expect(permission).not.toBeChecked();
    await expect(page.getByTestId(`rbac-save-${code}`)).toBeEnabled();
    await page.getByRole("button", { name: "Reset", exact: true }).click();
    await expect(permission).toBeChecked();
    await page.locator("#rbac-search").fill("crm.customers.view");
    await expect(
      page.getByRole("checkbox", { name: `${code} crm.customers.view`, exact: true }),
    ).toBeChecked();
    await expect(page.getByTestId(`rbac-save-${code}`)).toBeDisabled();
  } finally {
    const deleted = await api.delete(`/api/admin/rbac/roles/${roleId}`, { headers });
    expect(deleted.ok(), await deleted.text()).toBeTruthy();
  }
});
