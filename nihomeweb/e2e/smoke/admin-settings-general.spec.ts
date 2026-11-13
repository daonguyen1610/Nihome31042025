import { expect, test, TEST_USERS } from "../fixtures/auth";

test("general settings holds the six panels on one sheet", async ({ page, loginInBrowserAs }) => {
  await page.addInitScript(() => localStorage.setItem("nicon_lang", "en"));
  await loginInBrowserAs(page, TEST_USERS.superAdmin);

  // Old per-panel links still land on the matching section of the merged sheet.
  await page.goto("/admin/settings?tab=slideshow");
  await expect(page).toHaveURL(/tab=general&section=slideshow/);
  await expect(page.getByRole("tab", { name: "General settings", exact: true })).toHaveAttribute(
    "aria-selected",
    "true",
  );
  for (const heading of [
    "Time zone and currency",
    "Schedule alert threshold",
    "Security",
    "Contract file size limit",
    "Google Maps embed",
    "Slideshow & media",
  ]) {
    await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
  }
  await expect(
    page.getByRole("button", { name: "Slideshow & media", exact: true }),
  ).toHaveAttribute("aria-current", "location");

  await page.getByRole("button", { name: "Map", exact: true }).click();
  await expect(page).toHaveURL(/section=map/);
  await expect(page.getByRole("button", { name: "Map", exact: true })).toHaveAttribute(
    "aria-current",
    "location",
  );

  await page.getByRole("tab", { name: "Google Drive", exact: true }).click();
  await expect(page).toHaveURL(/\?tab=drive$/);
  await expect(
    page.getByRole("heading", { name: "Google Drive connection", exact: true }),
  ).toBeVisible();

  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto("/admin/settings");
  await expect(page.getByRole("heading", { name: "Settings", exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth)).toBeLessThanOrEqual(390);
});
