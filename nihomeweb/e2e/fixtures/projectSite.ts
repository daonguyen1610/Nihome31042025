import type { Locator, Page } from "@playwright/test";

/**
 * Fills the site details the project dialog requires (NICON feedback row 27): address,
 * new build or renovation, area and business sector (the first in the list when the
 * customer has none).
 */
export async function fillProjectSite(page: Page, dialog: Locator) {
  await dialog.getByLabel(/Địa chỉ dự án|Project address/).fill("Lô C5, KCN Tân Uyên, Bình Dương");
  await dialog.getByRole("combobox", { name: /Loại hình|Type of works/ }).click();
  await page.getByRole("option", { name: /^(Xây mới|New build)$/ }).click();
  await dialog.getByLabel(/Diện tích|Area/).fill("5000");
  const sector = dialog.getByRole("combobox", { name: /Ngành nghề kinh doanh|Business sector/ });
  if (/Chọn ngành nghề|Choose a sector/.test(await sector.innerText())) {
    await sector.click();
    await page.getByRole("option").first().click();
  }
}
