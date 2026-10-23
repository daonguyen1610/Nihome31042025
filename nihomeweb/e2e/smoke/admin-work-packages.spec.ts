import { expect, test, TEST_USERS } from "../fixtures/auth";
import { execSql } from "../fixtures/db";

test("shared-project and internal work package tabs render without browser errors", async ({
  page,
  loginInBrowserAs,
}) => {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error" && !/WebSocket|\[vite\]/i.test(message.text()))
      errors.push(message.text());
  });

  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  const response = await page.goto("/admin/work-packages");
  expect(response?.status()).toBeLessThan(400);
  await expect(page.getByTestId("work-packages-page")).toBeVisible({ timeout: 15_000 });
  await expect(page.getByRole("tab", { name: /Theo dự án|Project \/ contract/i })).toBeVisible();

  await page.getByRole("tab", { name: /Công việc nội bộ|Internal work/i }).click();
  await expect(page).toHaveURL(/kind=Internal/);
  await expect(page.getByTestId("work-packages-page")).toBeVisible();
  expect(errors).toEqual([]);
});

test("work package workspace remains usable at mobile and tablet widths", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);

  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    await page.goto("/admin/work-packages");
    await expect(page.getByTestId("work-packages-page")).toBeVisible({ timeout: 15_000 });
    const dimensions = await page.locator("html").evaluate((element) => ({
      clientWidth: element.clientWidth,
      scrollWidth: element.scrollWidth,
    }));
    expect(dimensions.scrollWidth).toBeLessThanOrEqual(dimensions.clientWidth);
  }
});

test("back from a work package detail restores the package list", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/work-packages");
  await expect(page.getByTestId("work-packages-page")).toBeVisible({ timeout: 15_000 });

  const firstPackage = page.getByTestId("work-package-card").first();
  await expect(firstPackage.or(page.getByText("Chưa có nhóm công việc phù hợp."))).toBeVisible();
  test.skip(
    (await firstPackage.count()) === 0,
    "The seeded environment has no work package to open",
  );
  await firstPackage.click();
  await expect(page).toHaveURL(/\/admin\/work-packages\/\d+$/);

  await page.getByTestId("work-package-back").click();

  await expect(page).toHaveURL(/\/admin\/work-packages$/);
  await expect(page.getByRole("heading", { name: /Nhóm công việc|Work packages/i })).toBeVisible();
  await expect(page.getByTestId("work-package-back")).toHaveCount(0);
});

for (const width of [390, 1440]) {
  test(`work package filters combine and survive reload at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const response = await api.get("/api/work-packages?kind=Project&pageSize=100", { headers });
    expect(response.status()).toBe(200);
    const { items } = await response.json();
    expect(items.length, "seeded project packages required").toBeGreaterThan(0);
    const fixture = items[0];
    await page.setViewportSize({ width, height: 900 });
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/work-packages");
    const expectFiltered = async (action: () => Promise<unknown>, key: string, value: string) => {
      const loaded = page.waitForResponse((res) => {
        const url = new URL(res.url());
        return url.pathname === "/api/work-packages" && url.searchParams.get(key) === value;
      });
      await action();
      const result = await loaded;
      expect(result.status()).toBe(200);
      const body = await result.json();
      await expect(page.getByTestId("work-package-card")).toHaveCount(body.items.length);
      return body.items;
    };
    const departments = await expectFiltered(
      () => page.locator("#wp-department").selectOption(String(fixture.departmentRoleGroupId)),
      "departmentRoleGroupId",
      String(fixture.departmentRoleGroupId),
    );
    expect(departments.length).toBeGreaterThan(0);
    expect(
      departments.every(
        (item: { departmentRoleGroupId: number }) =>
          item.departmentRoleGroupId === fixture.departmentRoleGroupId,
      ),
    ).toBe(true);
    await expectFiltered(
      () => page.locator("#wp-status").selectOption(fixture.status),
      "status",
      fixture.status,
    );
    await expectFiltered(
      () => page.locator("#wp-purpose").selectOption(fixture.purpose),
      "purpose",
      fixture.purpose,
    );
    await expectFiltered(
      () => page.locator("#wp-project").selectOption(String(fixture.operationalProjectId)),
      "operationalProjectId",
      String(fixture.operationalProjectId),
    );
    await expectFiltered(
      () => page.locator("#wp-search").fill(fixture.code),
      "search",
      fixture.code,
    );
    await expect(page.getByTestId("work-package-card")).toHaveCount(1);
    await page.reload();
    await expect(page.locator("#wp-department")).toHaveValue(String(fixture.departmentRoleGroupId));
    await expect(page.locator("#wp-status")).toHaveValue(fixture.status);
    await expect(page.locator("#wp-purpose")).toHaveValue(fixture.purpose);
    await expect(page.locator("#wp-project")).toHaveValue(String(fixture.operationalProjectId));
    await expect(page.getByTestId("work-package-card")).toHaveCount(1);
    await page.screenshot({
      path: testInfo.outputPath(`work-package-filters-${width}.png`),
      fullPage: true,
    });
    await expectFiltered(
      () => page.locator("#wp-search").fill("NICON_NO_MATCH_74918"),
      "search",
      "NICON_NO_MATCH_74918",
    );
    await expect(page.getByText("Chưa có nhóm công việc phù hợp.")).toBeVisible();
    await page.getByRole("tab", { name: /Công việc nội bộ|Internal work/i }).click();
    await expect(page.locator("#wp-project")).toHaveCount(0);
    await expect(page.locator("#wp-department")).toHaveValue(String(fixture.departmentRoleGroupId));
    await expect(page).not.toHaveURL(/projectId=/);
    await page.getByRole("button", { name: "Xóa bộ lọc", exact: true }).click();
    await expect(page).toHaveURL(/\?kind=Internal$/);
    await expect(page.locator("#wp-department")).toHaveValue("");
    await expect(page.locator("#wp-status")).toHaveValue("");
    await expect(page.locator("#wp-purpose")).toHaveValue("");
    await expect(page.locator("#wp-search")).toHaveValue("");
    const dimensions = await page
      .locator("html")
      .evaluate((element) => ({ width: element.clientWidth, scroll: element.scrollWidth }));
    expect(dimensions.scroll).toBeLessThanOrEqual(dimensions.width);
  });
}

test("department filter works without project-catalog permission", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const code = `WORK_FILTER_${Date.now()}`;
  const role = await api.post("/api/admin/rbac/roles", {
    headers,
    data: {
      code,
      name: "NICON lọc công việc nội bộ",
      permissions: ["dashboard.view", "work.packages.view"],
    },
  });
  expect(role.status()).toBe(201);
  const { id: roleId } = await role.json();
  const user = {
    phoneNumber: `09${String(Date.now()).slice(-8)}`,
    password: "Admin@123",
    role: code,
  };
  let userId: number | undefined;
  try {
    const created = await api.post("/api/users", {
      headers,
      data: {
        ...user,
        fullName: "NICON kiểm tra bộ lọc phòng ban",
        email: `${code.toLowerCase()}@e2e.nihome.local`,
      },
    });
    expect(created.status()).toBe(201);
    userId = (await created.json()).id;
    const denied: string[] = [];
    page.on("response", (response) => {
      if (response.status() === 403) denied.push(response.url());
    });
    await loginInBrowserAs(page, user);
    await page.goto("/admin/work-packages?kind=Internal");
    await expect(page.locator("#wp-department option").nth(1)).toBeAttached();
    await expect(page.locator("#wp-project")).toHaveCount(0);
    const departmentId = await page.locator("#wp-department option").nth(1).getAttribute("value");
    const loaded = page.waitForResponse(
      (response) =>
        new URL(response.url()).pathname === "/api/work-packages" &&
        new URL(response.url()).searchParams.get("departmentRoleGroupId") === departmentId,
    );
    await page.locator("#wp-department").selectOption(departmentId!);
    expect((await loaded).status()).toBe(200);
    expect(denied).toEqual([]);
  } finally {
    if (Number.isInteger(userId)) {
      execSql(`UPDATE users SET RoleEntityId = NULL WHERE Id = ${userId}`);
      execSql(`DELETE FROM refresh_tokens WHERE UserId = ${userId}`);
      execSql(`DELETE FROM users WHERE Id = ${userId}`);
    }
    expect((await api.delete(`/api/admin/rbac/roles/${roleId}`, { headers })).status()).toBe(200);
  }
});

test("project filter loads all catalog pages without requesting an extra page", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/operational-projects?page=1&pageSize=100", { headers });
  expect(response.status()).toBe(200);
  const catalog = await response.json();
  expect(Number.isInteger(catalog.total)).toBe(true);
  const requestedPages: number[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname === "/api/operational-projects" && url.searchParams.get("pageSize") === "100")
      requestedPages.push(Number(url.searchParams.get("page")));
  });
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/work-packages");
  await expect(page.locator("#wp-project option")).toHaveCount(catalog.total + 1);
  const expectedPages = Array.from(
    { length: Math.max(1, Math.ceil(catalog.total / 100)) },
    (_, index) => index + 1,
  );
  expect(requestedPages).toEqual(expectedPages);
});
