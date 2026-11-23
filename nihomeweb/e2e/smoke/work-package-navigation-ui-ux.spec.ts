import { expect, test, TEST_USERS } from "../fixtures/auth";

test("package detail returns to its section with filters intact", async ({
  page,
  loginInBrowserAs,
}) => {
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  for (const width of [390, 768, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("/admin/work-packages/projects");
    const card = page.getByTestId("work-package-card").first();
    await expect(card).toBeVisible();
    const code = (await card.getByTestId("work-package-code").innerText()).trim();
    const listUrl = `/admin/work-packages/projects?search=${encodeURIComponent(code)}`;
    await page.goto(listUrl);
    await expect(page.getByTestId("work-package-card")).toHaveCount(1);
    await page.getByTestId("work-package-card").click();
    await expect(page.getByTestId("work-package-back")).toBeVisible();
    await page.reload();
    await page.getByTestId("work-package-back").click();
    await expect(page).toHaveURL(`${new URL(page.url()).origin}${listUrl}`);
    await expect(page.locator("#wp-search")).toHaveValue(code);
    await expect(page.getByTestId("work-package-card")).toHaveCount(1);
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth),
    ).toBe(true);
    await page.screenshot({ path: `test-results/work-packages-${width}.png`, fullPage: true });
  }
});

test("direct project package links return to project work", async ({
  page,
  api,
  loginAs,
  loginInBrowserAs,
}) => {
  const headers = { Authorization: `Bearer ${await loginAs(TEST_USERS.superAdmin)}` };
  const response = await api.get("/api/work-packages?kind=Project&pageSize=1", { headers });
  expect(response.ok()).toBeTruthy();
  const { items } = await response.json();
  expect(items.length).toBeGreaterThan(0);
  await loginInBrowserAs(page, TEST_USERS.superAdmin);
  await page.goto(`/admin/work-packages/${items[0].id}`);
  await expect(page.getByTestId("work-package-back")).toHaveAttribute(
    "href",
    "/admin/work-packages/projects",
  );
});
