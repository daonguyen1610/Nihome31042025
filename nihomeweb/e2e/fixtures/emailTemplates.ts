import type { Page } from "@playwright/test";

/**
 * Opens an email template by its name. Below the lg breakpoint the template
 * list folds into a picker, which is opened first.
 */
export async function openEmailTemplate(page: Page, name: string) {
  const picker = page.getByTestId("email-template-picker");
  if ((await picker.isVisible()) && (await picker.getAttribute("aria-expanded")) !== "true")
    await picker.click();
  await page.getByRole("button", { name, exact: true }).click();
}
