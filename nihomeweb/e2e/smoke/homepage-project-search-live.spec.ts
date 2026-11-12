import { test, expect } from "../fixtures/auth";

test("project search keeps selected filters after reload and clears them", async ({ page }) => {
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
