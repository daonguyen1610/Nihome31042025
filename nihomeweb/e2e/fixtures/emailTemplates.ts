import { expect, type Page } from "@playwright/test";

/**
 * Opens an email template by its name. Below the lg breakpoint the template
 * list folds into a picker, which is opened first.
 */
export async function openEmailTemplate(page: Page, name: string) {
  const picker = page.getByTestId("email-template-picker");
  // Wait for the page to finish loading: the picker (phones) or the list (desktop).
  await expect(
    page.locator('[data-testid="email-template-picker"]:visible, #email-template-list:visible'),
  ).toBeVisible();
  if ((await picker.isVisible()) && (await picker.getAttribute("aria-expanded")) !== "true")
    await picker.click();
  await page.getByRole("button", { name, exact: true }).click();
}
