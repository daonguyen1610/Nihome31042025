import { expect, test, TEST_USERS } from "../fixtures/auth";

test("shared-project and internal work package tabs render without browser errors", async ({
  page,
  loginInBrowserAs,
}) => {
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    if (message.type() === "error" && !/WebSocket|\[vite\]/i.test(message.text())) errors.push(message.text());
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
    const dimensions = await page.locator("html").evaluate(element => ({
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
  test.skip(await firstPackage.count() === 0, "The seeded environment has no work package to open");
  await firstPackage.click();
  await expect(page).toHaveURL(/\/admin\/work-packages\/\d+$/);

  await page.getByTestId("work-package-back").click();

  await expect(page).toHaveURL(/\/admin\/work-packages$/);
  await expect(page.getByRole("heading", { name: /Nhóm công việc|Work packages/i })).toBeVisible();
  await expect(page.getByTestId("work-package-back")).toHaveCount(0);
});
