import { expect, test, TEST_USERS } from "../fixtures/auth";

test("email template settings expose the quote template and its required variables", async ({ loginInBrowserAs, page }) => {
  await loginInBrowserAs(page, TEST_USERS.admin);
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "vi"));

  for (const width of [390, 768]) {
    await page.setViewportSize({ width, height: 844 });
    await page.goto("/admin/email-templates", { waitUntil: "networkidle" });
    await page.getByRole("button", { name: "Báo giá", exact: true }).click();
    await expect(page.locator("#email-subject")).toHaveValue(/\{\{quoteCode\}\}/);
    await expect(page.locator("#email-body")).toHaveValue(/\{\{quoteLines\}\}/);
    await expect(page.locator("#email-body")).toHaveValue(/\{\{grandTotal\}\}/);
    await expect(page.frameLocator('iframe[title="Email preview"]').locator("body")).toContainText("{{quoteLines}}");
    await expect(page.frameLocator('iframe[title="Email preview"]').locator("body")).toContainText("NICON");
    await expect(page.frameLocator('iframe[title="Email preview"]').locator("body")).toContainText("Tổng giá trị");
    await expect(page.getByTitle("Hạn hiệu lực")).toBeVisible();
    expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(width);
  }
});
