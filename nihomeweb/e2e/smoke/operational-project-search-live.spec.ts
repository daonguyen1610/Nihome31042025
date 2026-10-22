import { test, expect, TEST_USERS } from "../fixtures/auth";

for (const width of [390, 768, 1440]) {
  test(`project search retains focus and updates live at ${width}px`, async ({
    page,
    api,
    loginAs,
    loginInBrowserAs,
  }, testInfo) => {
    const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
    const response = await api.get("/api/operational-projects?pageSize=1", { headers });
    expect(response.status()).toBe(200);
    const project = (await response.json()).items[0];
    expect(project?.code).toBeTruthy();
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/admin/operational-projects");
    const search = page.locator("#project-search");
    await expect(page.getByText(project.code, { exact: true })).toBeVisible();
    let navigations = 0;
    page.on("framenavigated", (frame) => {
      if (frame === page.mainFrame()) navigations++;
    });
    await search.click();
    // Pause between characters so every query finishes, exposing input remounts.
    for (const prefix of [project.code.slice(0, 1), project.code.slice(0, 2)]) {
      const result = page.waitForResponse((res) => {
        const url = new URL(res.url());
        return (
          url.pathname === "/api/operational-projects" && url.searchParams.get("search") === prefix
        );
      });
      await page.keyboard.type(prefix.slice(-1));
      expect((await result).status()).toBe(200);
      await expect(search).toBeFocused();
      await expect(search).toHaveValue(prefix);
    }
    const finalResult = page.waitForResponse((res) => {
      const url = new URL(res.url());
      return (
        url.pathname === "/api/operational-projects" &&
        url.searchParams.get("search") === project.code
      );
    });
    await page.keyboard.type(project.code.slice(2), { delay: 35 });
    expect((await finalResult).status()).toBe(200);
    await expect(search).toHaveValue(project.code);
    await expect(search).toBeFocused();
    await expect(page.getByText(project.code, { exact: true })).toBeVisible();
    const missingTerm = `NICON-no-match-${Date.now()}`;
    const missingResult = page.waitForResponse(
      (res) => new URL(res.url()).searchParams.get("search") === missingTerm,
    );
    await search.fill(missingTerm);
    expect((await missingResult).status()).toBe(200);
    await expect(page.locator("h2")).toHaveCount(0);
    await expect(search).toBeFocused();
    await search.fill("");
    await expect(page.getByText(project.code, { exact: true })).toBeVisible();
    await expect(search).toBeFocused();
    expect(navigations).toBe(0);
    const overflow = await page.locator("html").evaluate((el) => el.scrollWidth > el.clientWidth);
    expect(overflow).toBe(false);
    await page.screenshot({ path: testInfo.outputPath(`project-search-${width}.png`) });
  });
}

test("project search ignores stale responses and remains usable after errors (simulated network)", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto("/admin/operational-projects");
  const search = page.locator("#project-search");
  await expect(page.locator("h2").first()).toBeVisible();
  let releaseSlow!: () => void;
  let startedSlow!: () => void;
  const slowStarted = new Promise<void>((resolve) => {
    startedSlow = resolve;
  });
  const slowGate = new Promise<void>((resolve) => {
    releaseSlow = resolve;
  });
  await page.route("**/api/operational-projects?*", async (route) => {
    const term = new URL(route.request().url()).searchParams.get("search");
    if (term === "slow") {
      startedSlow();
      await slowGate;
      await route.fulfill({ status: 500, json: { message: "Stale search failure" } });
    } else if (term === "search-error") {
      await route.fulfill({ status: 500, json: { message: "Current search failure" } });
    } else {
      await route.fulfill({ json: { items: [], total: 0 } });
    }
  });
  await search.fill("slow");
  await slowStarted;
  const latest = page.waitForResponse(
    (res) => new URL(res.url()).searchParams.get("search") === "latest",
  );
  await search.fill("latest");
  await latest;
  await expect(page.locator("h2")).toHaveCount(0);
  const stale = page.waitForResponse(
    (res) => new URL(res.url()).searchParams.get("search") === "slow",
  );
  releaseSlow();
  await stale;
  await expect(search).toBeFocused();
  await expect(page.getByRole("alert")).toHaveCount(0);
  await search.fill("search-error");
  await expect(page.getByRole("alert")).toContainText("Current search failure");
  await expect(search).toBeFocused();
  await search.fill("recovered");
  await expect(page.getByRole("alert")).toHaveCount(0);
  await expect(search).toBeFocused();
});
