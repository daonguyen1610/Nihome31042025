import { expect, test, TEST_USERS } from "../fixtures/auth";

test("operational projects expose navigation and preserve filters between pages", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/operational-projects");
  const pagination = page.getByTestId("list-pagination");
  await expect(pagination).toBeVisible();
  const next = pagination.getByTestId("pagination-next");
  if (await next.isEnabled()) {
    const response = page.waitForResponse((res) => {
      const url = new URL(res.url());
      return url.pathname === "/api/operational-projects" && url.searchParams.get("page") === "2";
    });
    await next.click();
    expect((await response).ok()).toBeTruthy();
    await expect(pagination.getByTestId("pagination-previous")).toBeEnabled();
    const filtered = page.waitForResponse((res) => {
      const url = new URL(res.url());
      return (
        url.pathname === "/api/operational-projects" &&
        url.searchParams.get("search") === "PJ-SAMPLE"
      );
    });
    await page.locator("#project-search").fill("PJ-SAMPLE");
    const url = new URL((await filtered).url());
    expect(url.searchParams.get("page")).toBe("1");
    await expect(pagination.getByTestId("pagination-previous")).toBeDisabled();
  }
});

test("work-package quick typing sends one settled search request", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/work-packages/projects");
  const input = page.locator("#wp-search");
  await expect(input).toBeVisible();
  const requests: string[] = [];
  page.on("request", (request) => {
    const url = new URL(request.url());
    if (url.pathname === "/api/work-packages" && url.searchParams.has("search"))
      requests.push(url.searchParams.get("search")!);
  });
  const result = page.waitForResponse((response) => {
    const url = new URL(response.url());
    return url.pathname === "/api/work-packages" && url.searchParams.get("search") === "Nicon";
  });
  await input.pressSequentially("Nicon", { delay: 30 });
  expect((await result).ok()).toBeTruthy();
  await expect(input).toBeFocused();
  expect(requests).toEqual(["Nicon"]);
});

test("HSE list exposes record pagination for the selected project", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/operational-projects?pageSize=1", { headers });
  expect(response.ok()).toBeTruthy();
  const project = (await response.json()).items[0];
  expect(project).toBeTruthy();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`/admin/construction/hse?projectId=${project.id}`);
  await expect(page.locator("#hse-search")).toBeVisible();
  await expect(page.getByTestId("list-pagination")).toBeVisible();
  await expect(page.getByTestId("pagination-previous")).toBeDisabled();
});

test("design record tabs retain pagination at mobile and tablet widths", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/design-projects?pageSize=1", { headers });
  expect(response.ok()).toBeTruthy();
  const project = (await response.json()).items[0];
  expect(project, "seeded design project required").toBeTruthy();
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  for (const [tab, endpoint] of [
    ["concept", "concept-options"],
    ["basic", "basic-design-docs"],
    ["shop", "shop-drawings"],
    ["ifc", "ifc-releases"],
  ]) {
    await page.setViewportSize({ width: tab === "shop" || tab === "ifc" ? 768 : 390, height: 900 });
    const listResponse = page.waitForResponse((res) => {
      const url = new URL(res.url());
      return (
        url.pathname === `/api/${endpoint}` &&
        url.searchParams.get("designProjectId") === String(project.id)
      );
    });
    await page.goto(`/admin/design-projects/${project.id}?tab=${tab}`);
    const result = await listResponse;
    expect(result.ok()).toBeTruthy();
    const data = await result.json();
    expect(data.page).toBe(1);
    expect(data.pageSize).toBe(20);
    const pagination = page.getByTestId("list-pagination");
    await expect(pagination).toBeVisible();
    await expect(pagination.getByTestId("pagination-previous")).toBeDisabled();
    if (data.total <= data.pageSize)
      await expect(pagination.getByTestId("pagination-next")).toBeDisabled();
  }
});
