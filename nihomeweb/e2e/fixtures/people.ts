import type { Page } from "@playwright/test";

/** Match the person's label while preserving the role/department description. */
export function personOption(page: Page, name: string) {
  return page
    .locator('[data-people-picker="true"][data-state="open"] [cmdk-list]')
    .getByRole("option")
    .filter({ has: page.getByText(name, { exact: true }) });
}
