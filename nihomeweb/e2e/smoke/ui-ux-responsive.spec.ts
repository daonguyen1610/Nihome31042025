import { test, expect, TEST_USERS } from "../fixtures/auth";

for (const width of [390, 768, 1440]) {
  test(`audit regressions at ${width}px`, async ({ page, loginInBrowserAs }) => {
    await page.setViewportSize({ width, height: 500 });
    await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
    await loginInBrowserAs(page, TEST_USERS.superAdmin);
    await page.goto("/admin/users");
    await page.locator("main button").first().click();
    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect
      .poll(async () => {
        const bounds = await dialog.boundingBox();
        return !!bounds && bounds.y >= 0 && bounds.y + bounds.height <= 501;
      })
      .toBeTruthy();
    await dialog.locator('button[type="submit"]').scrollIntoViewIfNeeded();
    await expect(dialog.locator('button[type="submit"]')).toBeInViewport();
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();

    await page.route(/\/api\/translations\/admin(?:\?|$)/, (route) =>
      route.fulfill({
        json: Array.from({ length: 241 }, (_, i) => ({
          key: `audit.key.${i}`,
          category: "audit",
          vietnameseValue: `Audit ${i}`,
          translations: { en: `Audit ${i}`, zh: "", ja: "" },
        })),
      }),
    );
    await page.goto("/admin/translations");
    const next = page.getByRole("button", { name: "Next", exact: true });
    await expect(next).toBeVisible();
    for (let i = 0; i < 12; i++) await next.click();
    await expect(page.locator('[aria-current="page"]').filter({ hasText: "13" })).toBeVisible();
    await expect(next).toBeDisabled();
    await page.getByRole("button", { name: /Previous|Prev/, exact: true }).click();
    await expect(page.locator('[aria-current="page"]').filter({ hasText: "12" })).toBeVisible();
    expect(
      await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth),
    ).toBeTruthy();

    await page.goto("/admin/kpi/configuration");
    const weight = page.locator('input[aria-label*="Weight"]').first();
    await expect(weight).toBeVisible();
    if (width < 1280) {
      const save = weight.locator("xpath=ancestor::tr").getByRole("button", { name: /Save/ });
      await save.scrollIntoViewIfNeeded();
      const rect = await save.boundingBox();
      expect(rect!.x).toBeGreaterThanOrEqual(0);
      expect(rect!.x + rect!.width).toBeLessThanOrEqual(width);
    }
    await page.goto("/contact");
    for (const id of ["name", "phone", "email", "subject", "message"]) {
      expect(
        await page.locator(`#contact-${id}`).evaluate((el: HTMLInputElement) => el.labels?.length),
      ).toBe(1);
    }
    await expect(page.locator("footer form")).toHaveCount(0);
    await expect(page.locator('footer a[href="/contact"]')).toBeVisible();
  });
}

test("homepage search retains filters through reload and clears them", async ({ page }) => {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await page.goto("/");
  const form = page.locator("main form").first();
  const selects = form.locator("select");
  await expect(selects).toHaveCount(3);
  await expect(selects.nth(2).locator("option").nth(1)).toBeAttached();
  const location = await selects.nth(2).locator("option").nth(1).getAttribute("value");
  await selects.nth(2).selectOption(location!);
  await form.getByRole("button").click();
  await expect(page).toHaveURL(/\/projects\?location=/);
  await page.reload();
  await expect(page.locator('main a[href^="/projects/"]').first()).toBeVisible();
  await page.getByRole("button", { name: /Clear search/ }).click();
  await expect(page).not.toHaveURL(/location=/);
});
